package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "vehicles")
data class VehicleEntity(
    @PrimaryKey val id: String,
    val registration: String,
    val type: String,
    val active: Boolean = true
)
