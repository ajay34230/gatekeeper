package com.teamxv.qrmonitor.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface PersonDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(items: List<PersonEntity>)

    @Query("SELECT * FROM persons WHERE id=:id LIMIT 1")
    suspend fun find(id: String): PersonEntity?

    @Query("SELECT * FROM persons ORDER BY name")
    fun observeAll(): Flow<List<PersonEntity>>

    @Query("SELECT COUNT(*) FROM persons")
    suspend fun count(): Int
}
