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
                PersonEntity(it.personId, it.name, it.category, it.active, it.secretCode, it.rank, it.serviceNo, it.unit, it.company, it.role, it.status, it.accessLocations)
            })
            vehicles.upsertAll(master.vehicles.map {
                VehicleEntity(it.vehicleId, it.registration, it.type, it.active, it.secretCode, it.milReg, it.model, it.company, it.status)
            })
            if (master.persons.isEmpty()) persons.deleteAll() else persons.deleteAllExcept(master.persons.map { it.personId })
            if (master.vehicles.isEmpty()) vehicles.deleteAll() else vehicles.deleteAllExcept(master.vehicles.map { it.vehicleId })

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
        if (code.startsWith("XVGK1:")) return IdentityResult(error = "This is a PC pairing QR. Use Sync Hub → Pair with PC.")
        if (expected != EntityType.VEHICLE) {
            val p = persons.findBySecret(code) ?: QrPayloadParser.personId(code)?.let { persons.find(it) } ?: persons.findByServiceNo(code)
            if (p != null) return ok(p = p)
        }
        if (expected != EntityType.PERSON) {
            val v = vehicles.findBySecret(code) ?: QrPayloadParser.vehicleId(code)?.let { vehicles.find(it) }
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
        scannedLocation: String = ""
    ): OperationResult<MovementEvent> = db.withTransaction {
        val p = persons.find(personId) ?: return@withTransaction OperationResult.Rejected("PERSON_NOT_FOUND")
        if (!p.active) return@withTransaction OperationResult.Rejected("INACTIVE_PERSON")

        val active = sessions.activeForPerson(personId)
        val type = if (active == null) EventType.ENTRY else EventType.EXIT
        val now = System.currentTimeMillis()
        val eventId = newEventId()
        val event = MovementEvent(
            eventId, EntityType.PERSON, personId, type, location, gate, device, operator,
            now, now, SyncStatus.PENDING,
            sourceType = if (active?.sourceType == "VEHICLE") PresenceSource.VEHICLE else PresenceSource.DIRECT,
            sourceId = active?.sourceId,
            locationMismatch = locationMismatch,
            scannedLocation = scannedLocation
        )
        events.insert(toEntity(event))
        if (type == EventType.ENTRY) {
            sessions.insert(PresenceSessionEntity(
                sessionId = newSessionId(), personId = personId, vehicleId = null,
                sourceType = "DIRECT", sourceId = null, entryEventId = eventId,
                entryAt = now, locationId = location, gateId = gate
            ))
        } else {
            if (sessions.close(active!!.sessionId, eventId, now) != 1) {
                return@withTransaction OperationResult.Rejected("PRESENCE_STATE_CHANGED")
            }
        }
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
        OperationResult.Success(VehicleTransaction(event, manifest, members))
    }

    suspend fun createVehicleExit(
        vehicleId: String,
        location: String,
        gate: String,
        device: String,
        operator: String,
        locationMismatch: Boolean = false,
        scannedLocation: String = ""
    ): OperationResult<Pair<MovementEvent, Long?>> = db.withTransaction {
        val vehicle = vehicles.find(vehicleId) ?: return@withTransaction OperationResult.Rejected("VEHICLE_NOT_FOUND")
        val activeManifest = manifests.activeForVehicle(vehicle.id)
            ?: return@withTransaction OperationResult.Rejected("VEHICLE_NOT_INSIDE")
        val now = System.currentTimeMillis()
        val event = MovementEvent(
            newEventId(), EntityType.VEHICLE, vehicle.id, EventType.EXIT,
            location, gate, device, operator, now, now, SyncStatus.PENDING,
            locationMismatch = locationMismatch, scannedLocation = scannedLocation
        )
        val closed = manifests.closeManifest(activeManifest.manifestId, event.eventId, now)
        if (closed != 1) return@withTransaction OperationResult.Rejected("MANIFEST_STATE_CHANGED")
        sessions.closeForManifest(activeManifest.manifestId, event.eventId, now)
        events.insert(toEntity(event))
        OperationResult.Success(event to (now - activeManifest.createdAt))
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
        var synced = 0

        // Vehicle events are synchronized as an atomic event+manifest transaction.
        for (eventEntity in events.pending()) {
            val event = toModel(eventEntity)
            if (event.entityType == EntityType.VEHICLE) {
                val manifest = manifests.findManifestByEvent(event.eventId) ?: manifests.findManifestByExitEvent(event.eventId)
                if (manifest == null) {
                    events.updateSync(event.eventId, SyncStatus.CONFLICT.name, eventEntity.syncAttempts + 1, "VEHICLE_MANIFEST_NOT_FOUND", now)
                    continue
                }
                val members = manifests.members(manifest.manifestId)
                events.updateSync(event.eventId, SyncStatus.SYNCING.name, eventEntity.syncAttempts + 1, null, now)
                manifests.updateSync(manifest.manifestId, SyncStatus.SYNCING.name, now, null)
                val result = api.submitVehicleTransaction(baseUrl(), com.teamxv.qrmonitor.network.VehicleTransactionPayload(
                    event = com.teamxv.qrmonitor.network.EventPayload(
                        eventId = event.eventId, entityType = event.entityType.name, entityId = event.entityId,
                        eventType = event.eventType.name, locationId = event.locationId, gateId = event.gateId,
                        deviceId = event.deviceId, operatorId = event.operatorId, eventTimestamp = event.eventTimestamp,
                        createdAt = event.createdAt, sourceType = event.sourceType.name, sourceId = event.sourceId,
                        locationMismatch = event.locationMismatch, scannedLocation = event.scannedLocation
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
                    events.updateSync(event.eventId, SyncStatus.SYNCED.name, eventEntity.syncAttempts + 1, null, now)
                    manifests.updateSync(manifest.manifestId, SyncStatus.SYNCED.name, now, null)
                    synced++
                } else {
                    val f = result.exceptionOrNull()
                    val permanent = f is HttpFailure && f.code in 400..499 && f.code != 429
                    val status = if (f is HttpFailure && f.code == 409) SyncStatus.CONFLICT else if (permanent) SyncStatus.REJECTED else SyncStatus.FAILED
                    events.updateSync(event.eventId, status.name, eventEntity.syncAttempts + 1, f?.message, now)
                    manifests.updateSync(manifest.manifestId, status.name, now, f?.message)
                    if (!permanent) throw SyncTransientException(f?.message ?: "Vehicle transaction sync failed", f)
                }
            } else {
                val attempts = eventEntity.syncAttempts + 1
                events.updateSync(event.eventId, SyncStatus.SYNCING.name, attempts, null, now)
                val result = api.submitEvent(baseUrl(), event)
                if (result.isSuccess) {
                    events.updateSync(event.eventId, SyncStatus.SYNCED.name, attempts, null, System.currentTimeMillis())
                    synced++
                } else {
                    val f = result.exceptionOrNull()
                    val permanent = f is HttpFailure && f.code in 400..499 && f.code != 429
                    val status = if (f is HttpFailure && f.code == 409) SyncStatus.CONFLICT else if (permanent) SyncStatus.REJECTED else SyncStatus.FAILED
                    events.updateSync(event.eventId, status.name, attempts, f?.message, System.currentTimeMillis())
                    if (!permanent) throw SyncTransientException(f?.message ?: "Event sync failed", f)
                }
            }
        }
        return synced
    }

    private fun toEntity(e: MovementEvent) = MovementEventEntity(
        e.eventId, e.entityType.name, e.entityId, e.eventType.name,
        e.locationId, e.gateId, e.deviceId, e.operatorId, e.eventTimestamp,
        e.createdAt, e.syncStatus.name, 0, null, e.createdAt,
        e.sourceType.name, e.sourceId, e.locationMismatch, e.scannedLocation
    )

    private fun toModel(e: MovementEventEntity) = MovementEvent(
        e.eventId, EntityType.valueOf(e.entityType), e.entityId,
        EventType.valueOf(e.eventType), e.locationId, e.gateId,
        e.deviceId, e.operatorId, e.eventTimestamp, e.createdAt,
        SyncStatus.valueOf(e.syncStatus), PresenceSource.valueOf(e.sourceType), e.sourceId,
        e.locationMismatch, e.scannedLocation
    )

    private fun newEventId() = "EVT-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()
    private fun newSessionId() = "SES-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()
    private fun newManifestId() = "MNF-" + UUID.randomUUID().toString().replace("-", "").take(12).uppercase()

    companion object {
        private val syncMutex = Mutex()
    }

    class SyncTransientException(message: String, cause: Throwable? = null): Exception(message, cause)
}
