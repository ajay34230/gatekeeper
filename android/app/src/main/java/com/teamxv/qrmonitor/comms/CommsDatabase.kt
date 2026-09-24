package com.teamxv.qrmonitor.comms

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import kotlinx.coroutines.flow.Flow

/**
 * A message or alert exchanged with the Command Center.
 * direction: OUT = sent by this terminal, IN = received from the Command Center.
 * state (OUT): PENDING → SENT → DELIVERED → READ.  state (IN): RECEIVED → SEEN.
 */
@Entity(tableName = "comms_messages")
data class CommsMessageEntity(
    @PrimaryKey val id: String,
    val direction: String,
    val kind: String,
    val body: String,
    val sender: String,
    val createdAt: Long,
    val state: String
)

@Dao
interface CommsDao {
    @Query("SELECT * FROM comms_messages ORDER BY createdAt ASC")
    fun observeAll(): Flow<List<CommsMessageEntity>>

    @Query("SELECT COUNT(*) FROM comms_messages WHERE direction = 'IN' AND state = 'RECEIVED'")
    fun observeUnseen(): Flow<Int>

    /** Not yet confirmed by the PC; re-sent on every connection (the PC ignores duplicates by id). */
    @Query("SELECT * FROM comms_messages WHERE direction = 'OUT' AND state IN ('PENDING', 'SENT') ORDER BY createdAt ASC")
    suspend fun pendingOut(): List<CommsMessageEntity>

    @Query("SELECT id FROM comms_messages WHERE direction = 'IN' AND state = 'RECEIVED'")
    suspend fun unseenIds(): List<String>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(m: CommsMessageEntity): Long

    @Query("UPDATE comms_messages SET state = :state WHERE id = :id")
    suspend fun setState(id: String, state: String)

    /** Delivery/read receipts only move forward (a late DELIVERED never overwrites READ). */
    @Query("UPDATE comms_messages SET state = :state WHERE id = :id AND direction = 'OUT' AND state <> 'READ'")
    suspend fun advanceOut(id: String, state: String)

    @Query("UPDATE comms_messages SET state = 'SEEN' WHERE direction = 'IN' AND state = 'RECEIVED'")
    suspend fun markAllSeen()

    @Query("DELETE FROM comms_messages")
    suspend fun clear()
}

/** The Comms engine's own SQLCipher database, separate from the gate records and keyed by its own Keystore-wrapped passphrase. */
@Database(entities = [CommsMessageEntity::class], version = 1, exportSchema = false)
abstract class CommsDatabase : RoomDatabase() {
    abstract fun dao(): CommsDao

    companion object {
        @Volatile private var INSTANCE: CommsDatabase? = null

        fun get(context: Context): CommsDatabase = INSTANCE ?: synchronized(this) {
            INSTANCE ?: run {
                System.loadLibrary("sqlcipher")
                val passphrase = com.teamxv.qrmonitor.security.SecureStore(context.applicationContext).namedPassphrase("commsDbPassphrase")
                Room.databaseBuilder(context.applicationContext, CommsDatabase::class.java, "xv_comms_secure.db")
                    .openHelperFactory(net.zetetic.database.sqlcipher.SupportOpenHelperFactory(passphrase))
                    .fallbackToDestructiveMigration()
                    .build().also { INSTANCE = it }
            }
        }
    }
}
