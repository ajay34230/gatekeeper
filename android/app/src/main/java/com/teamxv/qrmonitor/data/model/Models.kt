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
    val comingFrom: String = ""
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

enum class PresenceStatus { INSIDE, OUTSIDE }

data class PersonPresence(
    val id: String,
    val name: String,
    val currentStatus: PresenceStatus
)
