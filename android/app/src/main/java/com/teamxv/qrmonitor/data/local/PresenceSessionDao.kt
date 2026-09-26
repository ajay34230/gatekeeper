package com.teamxv.qrmonitor.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface PresenceSessionDao {
    @Insert
    suspend fun insert(session: PresenceSessionEntity)

    /** Merges a handover snapshot from another terminal -- sessionId is a globally unique UUID, so this only ever
     * overwrites a session this same handover already delivered once (re-sending is safe/idempotent). */
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(items: List<PresenceSessionEntity>)

    @Query("SELECT * FROM presence_sessions WHERE personId=:personId AND status='ACTIVE' ORDER BY entryAt DESC LIMIT 1")
    suspend fun activeForPerson(personId: String): PresenceSessionEntity?

    @Query("SELECT * FROM presence_sessions WHERE status='ACTIVE'")
    fun observeActive(): Flow<List<PresenceSessionEntity>>

    @Query("UPDATE presence_sessions SET status='CLOSED', exitAt=:at WHERE personId=:personId AND status='ACTIVE' AND sourceType='DIRECT'")
    suspend fun closeDirectForPerson(personId: String, at: Long): Int

    @Query("SELECT * FROM presence_sessions WHERE status='ACTIVE' ORDER BY entryAt DESC")
    suspend fun activeAll(): List<PresenceSessionEntity>

    @Query("SELECT * FROM presence_sessions WHERE sourceType='VEHICLE' AND sourceId=:manifestId AND status='ACTIVE'")
    suspend fun activeForManifest(manifestId: String): List<PresenceSessionEntity>

    @Query("""
        UPDATE presence_sessions
        SET status='CLOSED', exitEventId=:exitEventId, exitAt=:exitAt
        WHERE sessionId=:sessionId AND status='ACTIVE'
    """)
    suspend fun close(sessionId: String, exitEventId: String, exitAt: Long): Int

    @Query("""
        UPDATE presence_sessions
        SET status='CLOSED', exitEventId=:exitEventId, exitAt=:exitAt
        WHERE sourceType='VEHICLE' AND sourceId=:manifestId AND status='ACTIVE'
    """)
    suspend fun closeForManifest(manifestId: String, exitEventId: String, exitAt: Long): Int
}
