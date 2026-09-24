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
    val remarks: String = ""
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
