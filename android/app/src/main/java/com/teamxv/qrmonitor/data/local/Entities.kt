package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "movement_events",
    indices = [
        Index(value = ["entityType", "entityId", "createdAt"]),
        Index(value = ["syncStatus", "createdAt"]),
        Index(value = ["sourceType", "sourceId"])
    ]
)
data class MovementEventEntity(
    @PrimaryKey val eventId: String,
    val entityType: String,
    val entityId: String,
    val eventType: String,
    val locationId: String,
    val gateId: String,
    val deviceId: String,
    val operatorId: String,
    val eventTimestamp: Long,
    val createdAt: Long,
    val syncStatus: String = "PENDING",
    val syncAttempts: Int = 0,
    val lastError: String? = null,
    val syncUpdatedAt: Long = createdAt,
    val sourceType: String = "DIRECT",
    val sourceId: String? = null,
    val locationMismatch: Boolean = false,
    val scannedLocation: String = "",
    /** Reason chosen at the gate (list set on the PC, or typed) and free remarks. */
    val reason: String = "",
    val remarks: String = "",
    /** Expected return (epoch ms) for exits on leave / TD; 0 when not asked. */
    val expectedReturn: Long = 0L,
    /** Where the person is coming from, entered by the guard on ENTRY only; blank on EXIT and on vehicle events. */
    val comingFrom: String = "",
    /** Vehicle EXIT only: the destination the guard entered and the approximate minutes (0 = not entered). */
    val destinationId: String = "",
    val destinationName: String = "",
    val transitMinutes: Int = 0,
    val serverSeq: Long = 0L,
    val serverRecordedAt: Long = 0L,
    /** Comma-separated flags for events requiring attention (e.g. "DUPLICATE_ENTRY"). */
    val flags: String = ""
)

@Entity(tableName = "presence_sessions", indices = [
    Index(value = ["personId", "status"]),
    Index(value = ["sourceType", "sourceId", "status"])
])
data class PresenceSessionEntity(
    @PrimaryKey val sessionId: String,
    val personId: String,
    val vehicleId: String?,
    val sourceType: String,
    val sourceId: String?,
    val entryEventId: String,
    val entryAt: Long,
    val locationId: String,
    val gateId: String,
    val status: String = "ACTIVE",
    val exitEventId: String? = null,
    val exitAt: Long? = null
)
