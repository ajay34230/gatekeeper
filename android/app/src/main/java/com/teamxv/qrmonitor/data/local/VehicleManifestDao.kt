package com.teamxv.qrmonitor.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface VehicleManifestDao {
    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insertManifest(manifest: VehicleManifestEntity)

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insertMembers(members: List<VehicleManifestMemberEntity>)

    @Query("SELECT * FROM vehicle_manifests WHERE manifestId=:id LIMIT 1")
    suspend fun findManifest(id: String): VehicleManifestEntity?

    @Query("SELECT * FROM vehicle_manifests WHERE eventId=:eventId LIMIT 1")
    suspend fun findManifestByEvent(eventId: String): VehicleManifestEntity?

    @Query("SELECT * FROM vehicle_manifests WHERE state='ACTIVE'")
    suspend fun activeAll(): List<VehicleManifestEntity>

    /** Marks a manifest closed because the vehicle left through another gate (already known to the server). */
    @Query("UPDATE vehicle_manifests SET state='EXITED', syncStatus='SYNCED' WHERE manifestId=:id AND state='ACTIVE'")
    suspend fun markExitedElsewhere(id: String): Int

    @Query("SELECT * FROM vehicle_manifests WHERE exitEventId=:eventId LIMIT 1")
    suspend fun findManifestByExitEvent(eventId: String): VehicleManifestEntity?

    @Query("SELECT * FROM vehicle_manifests WHERE state='ACTIVE'")
    fun observeActive(): kotlinx.coroutines.flow.Flow<List<VehicleManifestEntity>>

    @Query("SELECT * FROM vehicle_manifest_members WHERE manifestId=:id ORDER BY sequence")
    suspend fun members(id: String): List<VehicleManifestMemberEntity>

    @Query("SELECT * FROM vehicle_manifests WHERE syncStatus IN ('PENDING','FAILED','SYNCING') ORDER BY createdAt ASC, manifestId ASC")
    suspend fun pendingManifests(): List<VehicleManifestEntity>

    @Query("SELECT * FROM vehicle_manifests WHERE vehicleId=:vehicleId AND state='ACTIVE' ORDER BY createdAt DESC LIMIT 1")
    suspend fun activeForVehicle(vehicleId: String): VehicleManifestEntity?

    @Query("UPDATE vehicle_manifests SET syncStatus=:status, syncUpdatedAt=:updatedAt, lastError=:error WHERE manifestId=:id")
    suspend fun updateSync(id: String, status: String, updatedAt: Long, error: String?)

    @Query("UPDATE vehicle_manifests SET syncStatus='PENDING', lastError=NULL WHERE syncStatus='SYNCING' AND syncUpdatedAt < :cutoff")
    suspend fun recoverStaleSyncing(cutoff: Long)

    @Query("""
        UPDATE vehicle_manifests
        SET state='EXITED', exitEventId=:exitEventId, exitAt=:exitAt,
            syncStatus='PENDING', syncUpdatedAt=:exitAt, lastError=NULL
        WHERE manifestId=:manifestId AND state='ACTIVE'
    """)
    suspend fun closeManifest(manifestId: String, exitEventId: String, exitAt: Long): Int
}
