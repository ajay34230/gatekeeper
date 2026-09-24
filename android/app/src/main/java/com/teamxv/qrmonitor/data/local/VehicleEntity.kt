package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

/** Fleet record received from the Command Center (never created on the phone). */
@Entity(tableName = "vehicles", indices = [Index(value = ["secretCode"])])
data class VehicleEntity(
    @PrimaryKey val id: String,
    val registration: String,
    val type: String,
    val active: Boolean = true,
    val secretCode: String = "",
    val milReg: String = "",
    val model: String = "",
    val company: String = "",
    val status: String = "ACTIVE"
)
