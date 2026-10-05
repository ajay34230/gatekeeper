package com.teamxv.qrmonitor.data

import androidx.room.withTransaction
import com.teamxv.qrmonitor.data.local.*
import com.teamxv.qrmonitor.data.model.*
import com.teamxv.qrmonitor.network.ApiClient
import com.teamxv.qrmonitor.network.HttpFailure
import com.teamxv.qrmonitor.scanner.QrPayloadParser
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.util.UUID

sealed class OperationResult<out T> {
    data class Success<T>(val value: T): OperationResult<T>()
    data class Rejected(val reason: String): OperationResult<Nothing>()
}

data class IdentityResult(
    val person: PersonEntity? = null,
    val vehicle: VehicleEntity? = null,
    val error: String? = null,
    /** Location embedded in the QR (e.g. "P-001|LOC04"), when present. */
    val scannedLocation: String = "",
    val locationMismatch: Boolean = false
)

data class VehicleManifestDraft(
    val vehicle: VehicleEntity,
    val driver: PersonEntity,
    val coDriver: PersonEntity?,
    val occupants: List<PersonEntity>
) {
    fun uniquePeople(): List<PersonEntity> =
        (listOf(driver) + listOfNotNull(coDriver) + occupants).distinctBy { it.id }
}

data class VehicleTransaction(
    val event: MovementEvent,
    val manifest: VehicleManifestEntity,
    val members: List<VehicleManifestMemberEntity>
)

class EventRepository(
    private val db: AppDatabase,
    private val api: ApiClient,
    private val baseUrl: () -> String
) {
    private val events = db.movementEventDao()
    private val sessions = db.presenceSessionDao()
    private val persons = db.personDao()
    private val vehicles = db.vehicleDao()
    private val manifests = db.vehicleManifestDao()

    /**
     * Applies the Command Center's registry (the only source of personnel/vehicle data) and, when nothing is
     * waiting to upload, aligns local presence with the server so movements recorded at other gates are respected.
     */
    suspend fun applyMasterBootstrap(master: com.teamxv.qrmonitor.network.MasterBootstrapResponse) {
        db.withTransaction {
            persons.upsertAll(master.persons.map {
                PersonEntity(it.personId, it.name, it.category, it.active, it.secretCode, it.rank, it.serviceNo, it.unit, it.company, it.role, it.status, it.accessLocations, it.secretHash, it.validFrom, it.validTo)
            })
            vehicles.upsertAll(master.vehicles.map {
                VehicleEntity(it.vehicleId, it.registration, it.type, it.active, it.secretCode, it.milReg, it.model, it.company, it.status, it.secretHash)
            })
            if (master.sharingMode == "RECEIVE_ONLY") {
                // Nothing personal may stay on the phone: keep ID-only rows, blank every detail.
                persons.blankDetails(); vehicles.blankDetails()
            } else if (master.persons.isEmpty()) persons.deleteAll() else persons.deleteAllExcept(master.persons.map { it.personId })
            if (master.sharingMode != "RECEIVE_ONLY") { if (master.vehicles.isEmpty()) vehicles.deleteAll() else vehicles.deleteAllExcept(master.vehicles.map { it.vehicleId }) }

            if (events.pendingCount() == 0 && master.sharingMode != "RECEIVE_ONLY") {
                // Vehicles that entered through another gate: take the server's active manifests; close ours that ended elsewhere.
                val serverManifests = master.manifests.associateBy { it.manifestId }
                manifests.activeAll().filter { it.manifestId !in serverManifests }.forEach { manifests.markExitedElsewhere(it.manifestId) }
                val localActive = manifests.activeAll().map { it.manifestId }.toSet()
                master.manifests.filter { it.manifestId !in localActive && manifests.findManifest(it.manifestId) == null }.forEach { m ->
                    manifests.insertManifest(VehicleManifestEntity(
                        manifestId = m.manifestId, vehicleId = m.vehicleId, eventId = m.entryEventId, locationId = m.locationId, gateId = m.gateId,
                        driverId = m.driverId, coDriverId = m.coDriverId, createdAt = m.createdAt, syncStatus = SyncStatus.SYNCED.name, state = "ACTIVE"
                    ))
                    manifests.insertMembers(m.occupants.mapIndexed { i, pid -> VehicleManifestMemberEntity(m.manifestId, pid, i + 1) })
                }
            }
            if (events.pendingCount() == 0) {
                val now = System.currentTimeMillis()
                val serverInside = master.presence.associateBy { it.personId }
                val localActive = sessions.activeAll()
                localActive.filter { it.sourceType == "DIRECT" && it.personId !in serverInside }
                    .forEach { sessions.closeDirectForPerson(it.personId, now) }
                val localIds = localActive.map { it.personId }.toSet()
                serverInside.values.filter { it.personId !in localIds }.forEach {
                    sessions.insert(PresenceSessionEntity(
                        sessionId = "SRV-${it.personId}-${it.entryAt}", personId = it.personId, vehicleId = null,
                        sourceType = "DIRECT", sourceId = null, entryEventId = "SERVER", entryAt = it.entryAt,
                        locationId = "", gateId = ""
                    ))
                }
            }
        }
    }

    fun observeVehicles(): Flow<List<VehicleEntity>> = vehicles.observeAll()
    fun observeVehiclesInside(): Flow<Set<String>> = manifests.observeActive().map { list -> list.map { it.vehicleId }.toSet() }

    fun observeEvents(): Flow<List<MovementEvent>> =
        events.observeAll().map { list -> list.map(::toModel) }

    fun observePendingCount(): Flow<Int> = events.observePendingCount()

    suspend fun pendingCount(): Int = events.pendingCount()

    fun observeAttentionCount(): Flow<Int> = events.observeAttentionCount()

    fun observeAttentionEvents(): Flow<List<MovementEvent>> {
        return events.observeAttentionEvents().map { list -> list.map(::toModel) }
    }

    fun observePersonnel(): Flow<List<PersonPresence>> =
        combine(persons.observeAll(), sessions.observeActive()) { people, active ->
            val inside = active.map { it.personId }.toSet()
            people.map {
                PersonPresence(it.id, it.name, if (it.id in inside) PresenceStatus.INSIDE else PresenceStatus.OUTSIDE)
            }
        }

    /**
     * Resolves a scanned QR or typed code. Accepts the secret credential code printed by the Command Center,
     * an ID (P-001 / V014), a service number or a plate, optionally followed by "|location".
     */
    suspend fun lookupIdentity(rawQr: String, expected: EntityType? = null, currentLocation: String = ""): IdentityResult {
        val parsed = QrPayloadParser.parse(rawQr)
        val mismatch = parsed.location.isNotBlank() && currentLocation.isNotBlank() &&
            QrPayloadParser.normalizeLocation(parsed.location) != QrPayloadParser.normalizeLocation(currentLocation)
        fun ok(p: PersonEntity? = null, v: VehicleEntity? = null) = IdentityResult(p, v, null, parsed.location, mismatch)
        val code = parsed.code
        if (code.isBlank()) return IdentityResult(error = "Empty code")
        val hash = sha256(code.trim().uppercase())
        if (code.startsWith("XVGK1:")) return IdentityResult(error = "This is a PC pairing QR. Use Sync Hub → Pair with PC.")
        if (expected != EntityType.VEHICLE) {
            val p = persons.findBySecret(code) ?: persons.findBySecretHash(hash) ?: QrPayloadParser.personId(code)?.let { persons.find(it) } ?: persons.findByServiceNo(code)
            if (p != null) return ok(p = p.withDisplayName())
        }
        if (expected != EntityType.PERSON) {
            val v = vehicles.findBySecret(code) ?: vehicles.findBySecretHash(hash) ?: QrPayloadParser.vehicleId(code)?.let { vehicles.find(it) }
                ?: vehicles.findByPlate(code.uppercase().replace("-", "").replace(" ", ""))
            if (v != null) return ok(v = v)
        }
        return when {
            expected == EntityType.VEHICLE && QrPayloadParser.personId(code) != null -> IdentityResult(error = "This is a personnel credential. Scan the vehicle QR.")
            expected == EntityType.PERSON && QrPayloadParser.vehicleId(code) != null -> IdentityResult(error = "This is a vehicle credential. Scan a personnel badge.")
            persons.count() == 0 && vehicles.count() == 0 -> IdentityResult(error = "The registry on this terminal is empty. Sync with the Command Center first.")
            else -> IdentityResult(error = "Code $code is not registered in the Command Center")
        }
    }



    private fun sha256(s: String): String =
        java.security.MessageDigest.getInstance("SHA-256").digest(s.toByteArray()).joinToString("") { "%02x".format(it) }

    /** Minimal mode holds no names: show the ID instead. */
    private fun PersonEntity.withDisplayName() = if (name.isBlank()) copy(name = "ID " + id) else this

    /**
     * Receive-only mode: the Command Center verified the code online. Only an ID-level row is kept so the record can be
     * created and synced; the name and details are returned for display and never written to the phone.
     */
    suspend fun applyOnlineVerification(v: com.teamxv.qrmonitor.network.VerifyResponse): IdentityResult = db.withTransaction {
        if (v.type == "PERSON") {
            persons.upsert(PersonEntity(v.id, "", v.category, v.status == "ACTIVE", status = v.status, validFrom = v.validFrom, validTo = v.validTo))
            val local = sessions.activeForPerson(v.id)
            if (v.inside && local == null) sessions.insert(PresenceSessionEntity("SRV-${v.id}-${v.insideSince}", v.id, null, "DIRECT", null, "SERVER", v.insideSince, "", ""))
            if (!v.inside && local != null && events.pendingCount() == 0) sessions.closeDirectForPerson(v.id, System.currentTimeMillis())
            IdentityResult(person = PersonEntity(v.id, v.name.ifBlank { "ID " + v.id }, v.category, v.status == "ACTIVE", "", v.rank, v.serviceNo, v.unit, v.company, "", v.status, validFrom = v.validFrom, validTo = v.validTo))
        } else {
            vehicles.upsert(VehicleEntity(v.id, "", "", v.status == "ACTIVE", status = v.status))
            val m = v.manifest
            if (m != null && manifests.findManifest(m.manifestId) == null) {
                manifests.insertManifest(VehicleManifestEntity(m.manifestId, m.vehicleId, m.entryEventId, m.locationId, m.gateId, m.driverId, m.coDriverId, m.createdAt, SyncStatus.SYNCED.name, "ACTIVE"))
                manifests.insertMembers(m.occupants.mapIndexed { i, pid -> VehicleManifestMemberEntity(m.manifestId, pid, i + 1) })
                m.occupants.forEach { pid -> if (persons.find(pid) == null) persons.upsert(PersonEntity(pid, "", "PERSONNEL", true)) }
            }
            IdentityResult(vehicle = VehicleEntity(v.id, v.registration.ifBlank { v.id }, v.vehicleType, v.status == "ACTIVE", status = v.status))
        }
    }

    suspend fun isPersonInside(personId: String): Boolean = sessions.activeForPerson(personId) != null

    suspend fun insideSince(personId: String): Long = sessions.activeForPerson(personId)?.entryAt ?: 0L

    suspend fun isVehicleInside(vehicleId: String): Boolean = manifests.activeForVehicle(vehicleId) != null

    suspend fun createPersonEntryOrExit(
        personId: String,
        location: String,
        gate: String,
        device: String,
        operator: String,
        locationMismatch: Boolean = false,
        scannedLocation: String = "",
        reason: String = "",
        remarks: String = "",
        expectedReturn: Long = 0L,
        /** The operator's explicit Entry/Exit choice; null keeps the old behaviour of following the presence state. */
        forcedType: EventType? = null,
        /** Where the person is coming from -- entered by the guard on ENTRY only, ignored on EXIT. */
        comingFrom: String = ""
    ): OperationResult<MovementEvent> = db.withTransaction {
        val p = persons.find(personId) ?: return@withTransaction OperationResult.Rejected("PERSON_NOT_FOUND")
        if (!p.active) return@withTransaction OperationResult.Rejected("INACTIVE_PERSON")

        val active = sessions.activeForPerson(personId)
        val type = forcedType ?: if (active == null) EventType.ENTRY else EventType.EXIT
        // The gate guard's explicit Entry/Exit choice is final: this terminal's own presence tracking can be stale
        // (e.g. the matching exit/entry happened on another device that hasn't synced here yet), and the guard's
        // physical decision at the gate takes priority over that local bookkeeping -- the mismatch is corrected
        // below rather than blocking the record. Only credential validity (active/expired) still blocks; an
        // auto-detected direction (no explicit choice) still follows the presence state as before.
        if (forcedType == null) {
            if (type == EventType.ENTRY && active != null) return@withTransaction OperationResult.Rejected("ALREADY_INSIDE")
            if (type == EventType.EXIT && active == null) return@withTransaction OperationResult.Rejected("NOT_INSIDE")
        }
        val now = System.currentTimeMillis()
        // Visitor passes only admit entry inside their validity window (exit is always allowed).
        if (type == EventType.ENTRY && p.validTo > 0 && now > p.validTo) return@withTransaction OperationResult.Rejected("PASS_EXPIRED")
        if (type == EventType.ENTRY && p.validFrom > 0 && now < p.validFrom) return@withTransaction OperationResult.Rejected("PASS_NOT_YET_VALID")
        val eventId = newEventId()
        val event = MovementEvent(
            eventId, EntityType.PERSON, personId, type, location, gate, device, operator,
            now, now, SyncStatus.PENDING,
            sourceType = if (active?.sourceType == "VEHICLE") PresenceSource.VEHICLE else PresenceSource.DIRECT,
            sourceId = active?.sourceId,
            locationMismatch = locationMismatch,
            scannedLocation = scannedLocation,
            reason = reason.trim().take(60),
            remarks = remarks.trim().take(300),
            expectedReturn = if (type == EventType.EXIT) expectedReturn else 0L,
            comingFrom = if (type == EventType.ENTRY) comingFrom.trim().take(80) else ""
        )
        if (type == EventType.EXIT && active != null && sessions.close(active.sessionId, eventId, now) != 1)
            return@withTransaction OperationResult.Rejected("PRESENCE_STATE_CHANGED")
        if (type == EventType.ENTRY && active != null) {
            // Correct a stale "already inside" session before opening the one the guard is recording right now.
            sessions.close(active.sessionId, eventId, now)
        }
        events.insert(toEntity(event))
        if (type == EventType.ENTRY) {
            sessions.insert(PresenceSessionEntity(
                sessionId = newSessionId(), personId = personId, vehicleId = null,
                sourceType = "DIRECT", sourceId = null, entryEventId = eventId,
                entryAt = now, locationId = location, gateId = gate
            ))
        }
        com.teamxv.qrmonitor.diag.CrashLog.i("EventCreate", "PERSON $type: $eventId ($personId) at $location:$gate by $operator (status=${event.syncStatus})")
        OperationResult.Success(event)
    }

    suspend fun createVehicleEntry(
        draft: VehicleManifestDraft,
        location: String,
        gate: String,
        device: String,
        operator: String,
        locationMismatch: Boolean = false,
        scannedLocation: String = ""
    ): OperationResult<VehicleTransaction> = db.withTransaction {
        val vehicle = vehicles.find(draft.vehicle.id) ?: return@withTransaction OperationResult.Rejected("VEHICLE_NOT_FOUND")
        if (!vehicle.active) return@withTransaction OperationResult.Rejected("INACTIVE_VEHICLE")
        if (manifests.activeForVehicle(vehicle.id) != null) return@withTransaction OperationResult.Rejected("VEHICLE_ALREADY_INSIDE")

        val people = draft.uniquePeople()
        if (people.any { !it.active }) return@withTransaction OperationResult.Rejected("INACTIVE_MANIFEST_MEMBER")
        if (people.any { sessions.activeForPerson(it.id) != null }) return@withTransaction OperationResult.Rejected("PERSON_ALREADY_INSIDE")

        val now = System.currentTimeMillis()
        // Visitor passes on board are checked like a walk-in entry (validity is read from the registry, not the scan).
        val stored = people.mapNotNull { persons.find(it.id) }
        if (stored.any { it.validTo > 0 && now > it.validTo }) return@withTransaction OperationResult.Rejected("PASS_EXPIRED")
        if (stored.any { it.validFrom > 0 && now < it.validFrom }) return@withTransaction OperationResult.Rejected("PASS_NOT_YET_VALID")
        val eventId = newEventId()
        val event = MovementEvent(
            eventId, EntityType.VEHICLE, vehicle.id, EventType.ENTRY,
            location, gate, device, operator, now, now, SyncStatus.PENDING,
            locationMismatch = locationMismatch, scannedLocation = scannedLocation
        )
        val manifestId = newManifestId()
        val members = people.mapIndexed { index, p -> VehicleManifestMemberEntity(manifestId, p.id, index + 1) }
        val manifest = VehicleManifestEntity(
            manifestId = manifestId, vehicleId = vehicle.id, eventId = eventId,
            locationId = location, gateId = gate, driverId = draft.driver.id,
            coDriverId = draft.coDriver?.id, createdAt = now,
            syncStatus = SyncStatus.PENDING.name, state = "ACTIVE"
        )
        events.insert(toEntity(event))
        manifests.insertManifest(manifest)
        manifests.insertMembers(members)
        members.forEach { member ->
            sessions.insert(PresenceSessionEntity(
                sessionId = newSessionId(), personId = member.personId, vehicleId = vehicle.id,
                sourceType = "VEHICLE", sourceId = manifestId, entryEventId = eventId,
                entryAt = now, locationId = location, gateId = gate
            ))
        }
        com.teamxv.qrmonitor.diag.CrashLog.i("EventCreate", "VEHICLE ENTRY: $eventId ($manifestId) ${vehicle.id} at $location:$gate by $operator (driver=${draft.driver.id}, occupants=${members.size})")
        OperationResult.Success(VehicleTransaction(event, manifest, members))
    }

    suspend fun createVehicleExit(
        vehicleId: String,
        location: String,
        gate: String,
        device: String,
        operator: String,
        locationMismatch: Boolean = false,
        scannedLocation: String = "",
        /** Where the vehicle is going: a known location id or a typed place name, plus the approximate minutes (0 = not entered). */
        destinationId: String = "",
        destinationName: String = "",
        transitMinutes: Int = 0
    ): OperationResult<Pair<MovementEvent, Long?>> = db.withTransaction {
        val vehicle = vehicles.find(vehicleId) ?: return@withTransaction OperationResult.Rejected("VEHICLE_NOT_FOUND")
        val activeManifest = manifests.activeForVehicle(vehicle.id)
            ?: return@withTransaction OperationResult.Rejected("VEHICLE_NOT_INSIDE")
        val now = System.currentTimeMillis()
        val event = MovementEvent(
            newEventId(), EntityType.VEHICLE, vehicle.id, EventType.EXIT,
            location, gate, device, operator, now, now, SyncStatus.PENDING,
            locationMismatch = locationMismatch, scannedLocation = scannedLocation,
            destinationId = destinationId.trim().take(40), destinationName = destinationName.trim().take(60),
            transitMinutes = transitMinutes.coerceIn(0, 2880)
        )
        val closed = manifests.closeManifest(activeManifest.manifestId, event.eventId, now)
        if (closed != 1) return@withTransaction OperationResult.Rejected("MANIFEST_STATE_CHANGED")
        sessions.closeForManifest(activeManifest.manifestId, event.eventId, now)
        events.insert(toEntity(event))
        com.teamxv.qrmonitor.diag.CrashLog.i("EventCreate", "VEHICLE EXIT: ${event.eventId} (${activeManifest.manifestId}) ${vehicle.id} at $location:$gate by $operator (stayed=${now - activeManifest.createdAt}ms)")
        OperationResult.Success(event to (now - activeManifest.createdAt))
    }

    /** Records an entry for a vehicle this terminal cannot identify (not in the local registry and no connection
     * to verify it online) by the registration plate the guard reads and types in by eye. Stored as a standalone
     * event -- there is no local vehicle/manifest record to attach it to -- and syncs to the Command Center like
     * any other queued record, where the plate is what identifies it for reconciliation. */
    suspend fun createManualVehicleEntry(
        registration: String,
        location: String,
        gate: String,
        device: String,
        operator: String,
        remarks: String = ""
    ): OperationResult<MovementEvent> {
        val plate = registration.trim().uppercase()
        if (plate.isBlank()) return OperationResult.Rejected("REGISTRATION_REQUIRED")
        val entityId = "UNREG-" + plate.replace(Regex("[^A-Z0-9]"), "").take(24)
        val now = System.currentTimeMillis()
        val event = MovementEvent(
            newEventId(), EntityType.VEHICLE, entityId, EventType.ENTRY, location, gate, device, operator,
            now, now, SyncStatus.PENDING,
            remarks = "Manually recorded offline: registration $plate could not be verified against the registry. ${remarks.trim()}".trim().take(300)
        )
        events.insert(toEntity(event))
        com.teamxv.qrmonitor.diag.CrashLog.i("EventCreate", "MANUAL VEHICLE ENTRY: ${event.eventId} (plate=$plate, entityId=$entityId) at $location:$gate by $operator")
        return OperationResult.Success(event)
    }

    suspend fun activeVehicleManifest(vehicleId: String): Pair<VehicleManifestEntity, List<VehicleManifestMemberEntity>>? {
        val manifest = manifests.activeForVehicle(vehicleId) ?: return null
        return manifest to manifests.members(manifest.manifestId)
    }

    suspend fun syncPending(): Int = syncMutex.withLock { syncPendingLocked() }

    private suspend fun syncPendingLocked(): Int {
        val now = System.currentTimeMillis()
        events.recoverStaleSyncing(now - 10 * 60 * 1000L)
        manifests.recoverStaleSyncing(now - 10 * 60 * 1000L)
        val pending = events.pending()
        if (pending.isNotEmpty()) {
            com.teamxv.qrmonitor.diag.CrashLog.i("SyncStart", "Syncing ${pending.size} pending event(s) (attempt #${pending.maxOf { it.syncAttempts + 1 }})")
        }
        var synced = 0

        // Vehicle events are synchronized as an atomic event+manifest transaction.
        for (eventEntity in pending) {
            val event = toModel(eventEntity)
            if (event.entityType == EntityType.VEHICLE) {
                val manifest = manifests.findManifestByEvent(event.eventId) ?: manifests.findManifestByExitEvent(event.eventId)
                if (manifest == null) {
                    // No manifest at all means this was a manually-typed plate recorded offline for a vehicle not
                    // in the local registry (see createManualVehicleEntry) -- there is no driver/occupants to send,
                    // so it goes to the Command Center as a standalone sighting for an administrator to reconcile,
                    // not through the vehicle-transaction path that assumes a registered vehicle and manifest.
                    val attempts = eventEntity.syncAttempts + 1
                    events.updateSync(event.eventId, SyncStatus.SYNCING.name, attempts, null, now)
                    com.teamxv.qrmonitor.diag.CrashLog.i("SyncAttempt", "Manual vehicle: ${event.eventId} (attempt #$attempts)")
                    val result = api.submitManualVehicleSighting(baseUrl(), event)
                    if (result.isSuccess) {
                        events.updateSync(event.eventId, SyncStatus.SYNCED.name, attempts, null, System.currentTimeMillis())
                        markRecorded(event.eventId, result.getOrNull())
                        synced++
                        com.teamxv.qrmonitor.diag.CrashLog.i("SyncSuccess", "Manual vehicle: ${event.eventId} synced on attempt #$attempts")
                    } else {
                        val f = result.exceptionOrNull()
                        val permanent = f is HttpFailure && f.code in 400..499 && f.code != 429
                        val status = if (f is HttpFailure && f.code == 409) SyncStatus.CONFLICT else if (permanent) SyncStatus.REJECTED else SyncStatus.FAILED
                        events.updateSync(event.eventId, status.name, attempts, f?.message, System.currentTimeMillis())
                        com.teamxv.qrmonitor.diag.CrashLog.w("SyncFailed", "Manual vehicle: ${event.eventId} -> $status (attempt #$attempts): ${f?.message ?: "unknown error"}")
                        if (!permanent) throw SyncTransientException(f?.message ?: "Vehicle sighting sync failed", f)
                    }
                    continue
                }
                val members = manifests.members(manifest.manifestId)
                val attempts = eventEntity.syncAttempts + 1
                events.updateSync(event.eventId, SyncStatus.SYNCING.name, attempts, null, now)
                manifests.updateSync(manifest.manifestId, SyncStatus.SYNCING.name, now, null)
                com.teamxv.qrmonitor.diag.CrashLog.i("SyncAttempt", "Vehicle ${event.eventType}: ${event.eventId} (manifest=${manifest.manifestId}, members=${members.size}, attempt #$attempts)")
                val result = api.submitVehicleTransaction(baseUrl(), com.teamxv.qrmonitor.network.VehicleTransactionPayload(
                    event = com.teamxv.qrmonitor.network.EventPayload(
                        eventId = event.eventId, entityType = event.entityType.name, entityId = event.entityId,
                        eventType = event.eventType.name, locationId = event.locationId, gateId = event.gateId,
                        deviceId = event.deviceId, operatorId = event.operatorId, eventTimestamp = event.eventTimestamp,
                        createdAt = event.createdAt, sourceType = event.sourceType.name, sourceId = event.sourceId,
                        locationMismatch = event.locationMismatch, scannedLocation = event.scannedLocation,
                        destinationId = event.destinationId, destinationName = event.destinationName, transitMinutes = event.transitMinutes
                    ),
                    manifest = com.teamxv.qrmonitor.network.VehicleManifestPayload(
                        manifestId = manifest.manifestId, vehicleId = manifest.vehicleId, entryEventId = manifest.eventId,
                        locationId = manifest.locationId, gateId = manifest.gateId, driverId = manifest.driverId,
                        coDriverId = manifest.coDriverId, occupants = members.sortedBy { it.sequence }.map { it.personId },
                        createdAt = manifest.createdAt,
                        state = if (event.eventType == EventType.ENTRY) "ACTIVE" else "EXITED",
                        exitEventId = if (event.eventType == EventType.EXIT) event.eventId else null,
                        exitAt = if (event.eventType == EventType.EXIT) event.eventTimestamp else null
                    )
                ))
                if (result.isSuccess) {
                    events.updateSync(event.eventId, SyncStatus.SYNCED.name, attempts, null, now)
                    markRecorded(event.eventId, result.getOrNull())
                    manifests.updateSync(manifest.manifestId, SyncStatus.SYNCED.name, now, null)
                    synced++
                    com.teamxv.qrmonitor.diag.CrashLog.i("SyncSuccess", "Vehicle ${event.eventType}: ${event.eventId} synced on attempt #$attempts")
                } else {
                    val f = result.exceptionOrNull()
                    val permanent = f is HttpFailure && f.code in 400..499 && f.code != 429
                    val status = if (f is HttpFailure && f.code == 409) SyncStatus.CONFLICT else if (permanent) SyncStatus.REJECTED else SyncStatus.FAILED
                    events.updateSync(event.eventId, status.name, attempts, f?.message, now)
                    manifests.updateSync(manifest.manifestId, status.name, now, f?.message)
                    com.teamxv.qrmonitor.diag.CrashLog.w("SyncFailed", "Vehicle ${event.eventType}: ${event.eventId} -> $status (attempt #$attempts): ${f?.message ?: "unknown error"}")
                    if (!permanent) throw SyncTransientException(f?.message ?: "Vehicle transaction sync failed", f)
                }
            } else {
                val attempts = eventEntity.syncAttempts + 1
                events.updateSync(event.eventId, SyncStatus.SYNCING.name, attempts, null, now)
                com.teamxv.qrmonitor.diag.CrashLog.i("SyncAttempt", "Person ${event.eventType}: ${event.eventId} (personId=${event.entityId}, attempt #$attempts)")
                val result = api.submitEvent(baseUrl(), event)
                if (result.isSuccess) {
                    events.updateSync(event.eventId, SyncStatus.SYNCED.name, attempts, null, System.currentTimeMillis())
                    markRecorded(event.eventId, result.getOrNull())
                    synced++
                    com.teamxv.qrmonitor.diag.CrashLog.i("SyncSuccess", "Person ${event.eventType}: ${event.eventId} synced on attempt #$attempts")
                } else {
                    val f = result.exceptionOrNull()
                    val permanent = f is HttpFailure && f.code in 400..499 && f.code != 429
                    val status = if (f is HttpFailure && f.code == 409) SyncStatus.CONFLICT else if (permanent) SyncStatus.REJECTED else SyncStatus.FAILED
                    events.updateSync(event.eventId, status.name, attempts, f?.message, System.currentTimeMillis())
                    com.teamxv.qrmonitor.diag.CrashLog.w("SyncFailed", "Person ${event.eventType}: ${event.eventId} -> $status (attempt #$attempts): ${f?.message ?: "unknown error"}")
                    if (!permanent) throw SyncTransientException(f?.message ?: "Event sync failed", f)
                }
            }
        }
        if (pending.isNotEmpty()) {
            com.teamxv.qrmonitor.diag.CrashLog.i("SyncComplete", "Synced $synced of ${pending.size} event(s)")
        }
        return synced
    }

    private fun toEntity(e: MovementEvent) = MovementEventEntity(
        e.eventId, e.entityType.name, e.entityId, e.eventType.name,
        e.locationId, e.gateId, e.deviceId, e.operatorId, e.eventTimestamp,
        e.createdAt, e.syncStatus.name, 0, null, e.createdAt,
        e.sourceType.name, e.sourceId, e.locationMismatch, e.scannedLocation, e.reason, e.remarks, e.expectedReturn, e.comingFrom,
        e.destinationId, e.destinationName, e.transitMinutes, e.serverSeq, e.serverRecordedAt, e.flags
    )

    private fun toModel(e: MovementEventEntity) = MovementEvent(
        e.eventId, EntityType.valueOf(e.entityType), e.entityId,
        EventType.valueOf(e.eventType), e.locationId, e.gateId,
        e.deviceId, e.operatorId, e.eventTimestamp, e.createdAt,
        SyncStatus.valueOf(e.syncStatus), PresenceSource.valueOf(e.sourceType), e.sourceId,
        e.locationMismatch, e.scannedLocation, e.reason, e.remarks, e.expectedReturn, e.comingFrom,
        e.destinationId, e.destinationName, e.transitMinutes, e.serverSeq, e.serverRecordedAt, e.flags
    )

    /** The server sequence number and recording time from the Command Center's reply to an accepted record. */
    private fun recordedBy(body: String?): Pair<Long, Long> = try {
        val o = kotlinx.serialization.json.Json.parseToJsonElement(body ?: "").let { it as kotlinx.serialization.json.JsonObject }
        ((o["serverSequence"] as? kotlinx.serialization.json.JsonPrimitive)?.content?.toLongOrNull() ?: 0L) to
            ((o["recordedAt"] as? kotlinx.serialization.json.JsonPrimitive)?.content?.toLongOrNull() ?: 0L)
    } catch (_: Exception) { 0L to 0L }

    private suspend fun markRecorded(eventId: String, body: String?) {
        val (seq, at) = recordedBy(body)
        if (seq > 0L) events.markRecorded(eventId, seq, at)
    }

    private fun newEventId() = "EVT-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()
    private fun newSessionId() = "SES-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()
    private fun newManifestId() = "MNF-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()

    companion object {
        private val syncMutex = Mutex()
    }

    class SyncTransientException(message: String, cause: Throwable? = null): Exception(message, cause)
}
