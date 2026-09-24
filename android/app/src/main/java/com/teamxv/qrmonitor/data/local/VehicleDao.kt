package com.teamxv.qrmonitor.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface VehicleDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(items: List<VehicleEntity>)

    @Query("SELECT * FROM vehicles WHERE id=:id LIMIT 1")
    suspend fun find(id: String): VehicleEntity?

    @Query("SELECT COUNT(*) FROM vehicles")
    suspend fun count(): Int
}
