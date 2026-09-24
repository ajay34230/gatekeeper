package com.teamxv.qrmonitor.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "persons")
data class PersonEntity(
    @PrimaryKey val id: String,
    val name: String,
    val category: String,
    val active: Boolean = true
)
