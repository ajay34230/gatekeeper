package com.teamxv.qrmonitor.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface MovementEventDao {
    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insert(event: MovementEventEntity)

    /** Merges a handover snapshot from another terminal -- eventId is a globally unique UUID, so this only ever
     * overwrites an event this same handover already delivered once (re-sending is safe/idempotent). */
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(items: List<MovementEventEntity>)

    @Query("SELECT * FROM movement_events ORDER BY createdAt DESC, eventId DESC")
    fun observeAll(): Flow<List<MovementEventEntity>>

    @Query("SELECT * FROM movement_events ORDER BY createdAt DESC, eventId DESC")
    suspend fun snapshot(): List<MovementEventEntity>

    @Query("""
        SELECT * FROM movement_events
        WHERE syncStatus IN ('PENDING','FAILED','SYNCING')
        ORDER BY createdAt ASC, eventId ASC
    """)
    suspend fun pending(): List<MovementEventEntity>

    @Query("SELECT * FROM movement_events WHERE entityType=:type AND entityId=:id ORDER BY createdAt DESC, eventId DESC LIMIT 1")
    suspend fun latestForEntity(type: String, id: String): MovementEventEntity?

    @Query("UPDATE movement_events SET syncStatus=:status, syncAttempts=:attempts, lastError=:error, syncUpdatedAt=:updatedAt WHERE eventId=:eventId")
    suspend fun updateSync(eventId: String, status: String, attempts: Int, error: String?, updatedAt: Long)

    @Query("UPDATE movement_events SET serverSeq=:seq, serverRecordedAt=:recordedAt WHERE eventId=:eventId")
    suspend fun markRecorded(eventId: String, seq: Long, recordedAt: Long)

    @Query("UPDATE movement_events SET syncStatus='PENDING', lastError=NULL WHERE syncStatus='SYNCING' AND syncUpdatedAt < :cutoff")
    suspend fun recoverStaleSyncing(cutoff: Long)

    @Query("SELECT COUNT(*) FROM movement_events WHERE syncStatus IN ('PENDING','FAILED','SYNCING')")
    suspend fun pendingCount(): Int

    @Query("SELECT COUNT(*) FROM movement_events WHERE syncStatus IN ('PENDING','FAILED','SYNCING')")
    fun observePendingCount(): Flow<Int>

    @Query("SELECT COUNT(*) FROM movement_events WHERE syncStatus='CONFLICT' OR syncStatus='REJECTED' OR flags != ''")
    fun observeAttentionCount(): Flow<Int>

    @Query("""
        SELECT * FROM movement_events
        WHERE syncStatus='CONFLICT' OR syncStatus='REJECTED' OR flags != ''
        ORDER BY createdAt DESC, eventId DESC
    """)
    fun observeAttentionEvents(): Flow<List<MovementEventEntity>>

    @Query("SELECT DISTINCT entityId FROM movement_events WHERE entityType='PERSON'")
    fun observeTrackedPersonIds(): Flow<Set<String>>
}
