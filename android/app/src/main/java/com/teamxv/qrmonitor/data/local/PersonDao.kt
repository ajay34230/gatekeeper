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

    @Query("SELECT * FROM persons WHERE UPPER(secretCode)=UPPER(:code) LIMIT 1")
    suspend fun findBySecret(code: String): PersonEntity?

    @Query("SELECT * FROM persons WHERE secretHash=:hash AND secretHash<>'' LIMIT 1")
    suspend fun findBySecretHash(hash: String): PersonEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(item: PersonEntity)

    @Query("SELECT * FROM persons WHERE UPPER(serviceNo)=UPPER(:no) AND serviceNo<>'' LIMIT 1")
    suspend fun findByServiceNo(no: String): PersonEntity?

    @Query("DELETE FROM persons WHERE id NOT IN (:keep)")
    suspend fun deleteAllExcept(keep: List<String>)

    @Query("UPDATE persons SET name='', rank='', serviceNo='', unit='', company='', role='', secretCode='', secretHash=''")
    suspend fun blankDetails()

    @Query("DELETE FROM persons")
    suspend fun deleteAll()

    @Query("SELECT COUNT(*) FROM persons")
    suspend fun count(): Int
}
