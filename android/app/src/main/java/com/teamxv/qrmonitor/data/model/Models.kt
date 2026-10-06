package com.teamxv.qrmonitor.data.model

enum class EntityType { PERSON, VEHICLE }
enum class EventType { ENTRY, EXIT }
enum class SyncStatus { PENDING, SYNCING, SYNCED, FAILED, REJECTED, CONFLICT }
enum class PresenceSource { DIRECT, VEHICLE }

data class Person(
    val id: String,
    val name: String,
    val category: String = "Personnel",
    val active: Boolean = true
)

data class Vehicle(
    val id: String,
    val registration: String,
    val type: String = "Vehicle",
    val active: Boolean = true
)

data class MovementEvent(
    val eventId: String,
    val entityType: EntityType,
    val entityId: String,
    val eventType: EventType,
    val locationId: String,
    val gateId: String,
    val deviceId: String,
    val operatorId: String,
    val eventTimestamp: Long,
    val createdAt: Long,
    val syncStatus: SyncStatus = SyncStatus.PENDING,
    val sourceType: PresenceSource = PresenceSource.DIRECT,
    val sourceId: String? = null,
    val locationMismatch: Boolean = false,
    val scannedLocation: String = "",
    val reason: String = "",
    val remarks: String = "",
    val expectedReturn: Long = 0L,
    /** Where the person is coming from, entered by the guard on ENTRY only -- shown on the gate record. */
    val comingFrom: String = "",
    /** Vehicle EXIT only: where the vehicle is going (a known location id, or a typed place) and the approximate minutes. */
    val destinationId: String = "",
    val destinationName: String = "",
    val transitMinutes: Int = 0,
    /** Set when the Command Center has stored the record: its server sequence number and the time it recorded it (0 = not recorded yet). */
    val serverSeq: Long = 0L,
    val serverRecordedAt: Long = 0L,
    /** Comma-separated flags for events requiring attention (e.g. "DUPLICATE_ENTRY"). */
    val flags: String = ""
)

data class NetworkStatus(
    val transport: String = "Unknown",
    val connected: Boolean = false,
    val serverReachable: Boolean = false,
    val apiAvailable: Boolean = false,
    val server: String = "",
    val localIp: String = "",
    val latencyMs: Long? = null,
    val message: String = "Not tested"
)

enum class PresenceStatus { INSIDE, OUTSIDE, NOT_TRACKED }

data class PersonPresence(
    val id: String,
    val name: String,
    val currentStatus: PresenceStatus
)
