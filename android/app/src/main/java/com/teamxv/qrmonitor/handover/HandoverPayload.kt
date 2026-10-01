package com.teamxv.qrmonitor.handover

import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.data.local.MovementEventEntity
import com.teamxv.qrmonitor.data.local.PersonEntity
import com.teamxv.qrmonitor.data.local.PresenceSessionEntity
import com.teamxv.qrmonitor.data.local.VehicleEntity
import kotlinx.coroutines.flow.first
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

@Serializable
data class PersonDto(
    val id: String, val name: String, val category: String, val active: Boolean, val secretCode: String,
    val rank: String, val serviceNo: String, val unit: String, val company: String, val role: String,
    val status: String, val accessLocations: String, val secretHash: String, val validFrom: Long, val validTo: Long
) {
    fun toEntity() = PersonEntity(id, name, category, active, secretCode, rank, serviceNo, unit, company, role, status, accessLocations, secretHash, validFrom, validTo)
    companion object { fun of(e: PersonEntity) = PersonDto(e.id, e.name, e.category, e.active, e.secretCode, e.rank, e.serviceNo, e.unit, e.company, e.role, e.status, e.accessLocations, e.secretHash, e.validFrom, e.validTo) }
}

@Serializable
data class VehicleDto(
    val id: String, val registration: String, val type: String, val active: Boolean, val secretCode: String,
    val milReg: String, val model: String, val company: String, val status: String, val secretHash: String
) {
    fun toEntity() = VehicleEntity(id, registration, type, active, secretCode, milReg, model, company, status, secretHash)
    companion object { fun of(e: VehicleEntity) = VehicleDto(e.id, e.registration, e.type, e.active, e.secretCode, e.milReg, e.model, e.company, e.status, e.secretHash) }
}

@Serializable
data class PresenceDto(
    val sessionId: String, val personId: String, val vehicleId: String?, val sourceType: String, val sourceId: String?,
    val entryEventId: String, val entryAt: Long, val locationId: String, val gateId: String, val status: String,
    val exitEventId: String?, val exitAt: Long?
) {
    fun toEntity() = PresenceSessionEntity(sessionId, personId, vehicleId, sourceType, sourceId, entryEventId, entryAt, locationId, gateId, status, exitEventId, exitAt)
    companion object { fun of(e: PresenceSessionEntity) = PresenceDto(e.sessionId, e.personId, e.vehicleId, e.sourceType, e.sourceId, e.entryEventId, e.entryAt, e.locationId, e.gateId, e.status, e.exitEventId, e.exitAt) }
}

@Serializable
data class MovementEventDto(
    val eventId: String, val entityType: String, val entityId: String, val eventType: String, val locationId: String,
    val gateId: String, val deviceId: String, val operatorId: String, val eventTimestamp: Long, val createdAt: Long,
    val syncStatus: String, val syncAttempts: Int, val lastError: String?, val syncUpdatedAt: Long, val sourceType: String,
    val sourceId: String?, val locationMismatch: Boolean, val scannedLocation: String, val reason: String,
    val remarks: String, val expectedReturn: Long, val comingFrom: String = ""
) {
    fun toEntity() = MovementEventEntity(
        eventId, entityType, entityId, eventType, locationId, gateId, deviceId, operatorId, eventTimestamp, createdAt,
        syncStatus, syncAttempts, lastError, syncUpdatedAt, sourceType, sourceId, locationMismatch, scannedLocation,
        reason, remarks, expectedReturn, comingFrom
    )
    companion object {
        fun of(e: MovementEventEntity) = MovementEventDto(
            e.eventId, e.entityType, e.entityId, e.eventType, e.locationId, e.gateId, e.deviceId, e.operatorId,
            e.eventTimestamp, e.createdAt, e.syncStatus, e.syncAttempts, e.lastError, e.syncUpdatedAt, e.sourceType,
            e.sourceId, e.locationMismatch, e.scannedLocation, e.reason, e.remarks, e.expectedReturn, e.comingFrom
        )
    }
}

/** Everything the next operator's phone needs to keep working immediately -- registry, who is currently inside, and
 * any gate record this phone hasn't yet confirmed synced -- sent phone-to-phone over Bluetooth or a local Wi-Fi
 * hotspot (see NearbyHandoverService), with no server involved. */
@Serializable
data class HandoverPayload(
    val protocolVersion: Int = 1,
    val fromOperatorId: String,
    val fromOperatorName: String,
    val fromDeviceId: String,
    val locationId: String,
    val gateId: String,
    val generatedAt: Long,
    val persons: List<PersonDto>,
    val vehicles: List<VehicleDto>,
    val activePresence: List<PresenceDto>,
    val pendingEvents: List<MovementEventDto>,
    val cachedLocations: List<Pair<String, String>>,
    val cachedGates: List<Pair<String, String>>
) {
    companion object {
        private val json = Json { ignoreUnknownKeys = true }

        suspend fun build(config: AppConfig, db: AppDatabase): HandoverPayload = HandoverPayload(
            fromOperatorId = config.operatorId,
            fromOperatorName = config.operatorName,
            fromDeviceId = config.deviceId,
            locationId = config.locationId,
            gateId = config.gateId,
            generatedAt = System.currentTimeMillis(),
            persons = db.personDao().observeAll().first().map(PersonDto::of),
            vehicles = db.vehicleDao().observeAll().first().map(VehicleDto::of),
            activePresence = db.presenceSessionDao().activeAll().map(PresenceDto::of),
            pendingEvents = db.movementEventDao().pending().map(MovementEventDto::of),
            cachedLocations = config.cachedLocations,
            cachedGates = config.cachedGates
        )

        fun encode(payload: HandoverPayload): ByteArray = json.encodeToString(serializer(), payload).toByteArray(Charsets.UTF_8)
        fun decode(bytes: ByteArray): HandoverPayload = json.decodeFromString(serializer(), String(bytes, Charsets.UTF_8))
    }

    /** Merges this snapshot into the receiving terminal's local database and configuration; returns a short summary
     * for the "succeeded" status chip. Every table is merged by upsert on a globally unique id, so receiving the
     * same handover twice (a retry after a dropped connection) never duplicates anything. */
    suspend fun applyTo(config: AppConfig, db: AppDatabase): String {
        if (persons.isNotEmpty()) db.personDao().upsertAll(persons.map { it.toEntity() })
        if (vehicles.isNotEmpty()) db.vehicleDao().upsertAll(vehicles.map { it.toEntity() })
        if (activePresence.isNotEmpty()) db.presenceSessionDao().upsertAll(activePresence.map { it.toEntity() })
        if (pendingEvents.isNotEmpty()) db.movementEventDao().upsertAll(pendingEvents.map { it.toEntity() })
        if (cachedLocations.isNotEmpty()) config.cachedLocations = (config.cachedLocations + cachedLocations).distinctBy { it.first }
        if (cachedGates.isNotEmpty()) config.cachedGates = (config.cachedGates + cachedGates).distinctBy { it.first }
        return "Received from ${fromOperatorName.ifBlank { fromOperatorId }}: ${persons.size} personnel, " +
            "${vehicles.size} vehicles, ${activePresence.size} inside, ${pendingEvents.size} queued record(s)"
    }
}
