package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

/** Registry record received from the Command Center (never created on the phone). */
@Entity(tableName = "persons", indices = [Index(value = ["secretCode"])])
data class PersonEntity(
    @PrimaryKey val id: String,
    val name: String,
    val category: String,
    val active: Boolean = true,
    val secretCode: String = "",
    val rank: String = "",
    val serviceNo: String = "",
    val unit: String = "",
    val company: String = "",
    val role: String = "",
    val status: String = "ACTIVE",
    val accessLocations: String = "",
    /** SHA-256 of the QR secret (Minimal mode keeps only this, never the secret or personal details). */
    val secretHash: String = "",
    /** Visitor / temporary pass validity (epoch ms, 0 = no limit). Personnel credentials never expire. */
    val validFrom: Long = 0L,
    val validTo: Long = 0L
)
