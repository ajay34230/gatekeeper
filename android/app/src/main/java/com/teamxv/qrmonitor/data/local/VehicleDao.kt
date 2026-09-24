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

    @Query("SELECT * FROM vehicles WHERE UPPER(secretCode)=UPPER(:code) LIMIT 1")
    suspend fun findBySecret(code: String): VehicleEntity?

    @Query("SELECT * FROM vehicles WHERE REPLACE(REPLACE(UPPER(registration),'-',''),' ','')=:plate LIMIT 1")
    suspend fun findByPlate(plate: String): VehicleEntity?

    @Query("SELECT * FROM vehicles WHERE secretHash=:hash AND secretHash<>'' LIMIT 1")
    suspend fun findBySecretHash(hash: String): VehicleEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(item: VehicleEntity)

    @Query("SELECT * FROM vehicles ORDER BY id")
    fun observeAll(): kotlinx.coroutines.flow.Flow<List<VehicleEntity>>

    @Query("DELETE FROM vehicles WHERE id NOT IN (:keep)")
    suspend fun deleteAllExcept(keep: List<String>)

    @Query("UPDATE vehicles SET registration='', milReg='', model='', company='', secretCode='', secretHash=''")
    suspend fun blankDetails()

    @Query("DELETE FROM vehicles")
    suspend fun deleteAll()

    @Query("SELECT COUNT(*) FROM vehicles")
    suspend fun count(): Int
}
