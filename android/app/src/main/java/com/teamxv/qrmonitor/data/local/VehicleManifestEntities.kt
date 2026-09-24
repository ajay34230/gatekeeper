package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(tableName = "vehicle_manifests", indices = [Index(value = ["vehicleId", "state"])])
data class VehicleManifestEntity(
    @PrimaryKey val manifestId: String,
    val vehicleId: String,
    val eventId: String,
    val locationId: String,
    val gateId: String,
    val driverId: String,
    val coDriverId: String?,
    val createdAt: Long,
    val syncStatus: String = "PENDING",
    val state: String = "ACTIVE",
    val exitEventId: String? = null,
    val exitAt: Long? = null,
    val syncUpdatedAt: Long = createdAt,
    val lastError: String? = null
)

@Entity(tableName = "vehicle_manifest_members", primaryKeys = ["manifestId", "personId"])
data class VehicleManifestMemberEntity(
    val manifestId: String,
    val personId: String,
    val sequence: Int
)
