package com.teamxv.qrmonitor.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(
    entities = [
        MovementEventEntity::class,
        PresenceSessionEntity::class,
        PersonEntity::class,
        VehicleEntity::class,
        VehicleManifestEntity::class,
        VehicleManifestMemberEntity::class
    ],
    version = 12,
    exportSchema = true
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun movementEventDao(): MovementEventDao
    abstract fun presenceSessionDao(): PresenceSessionDao
    abstract fun personDao(): PersonDao
    abstract fun vehicleDao(): VehicleDao
    abstract fun vehicleManifestDao(): VehicleManifestDao

    companion object {
        @Volatile private var INSTANCE: AppDatabase? = null

        // These migrations preserve the project's earlier versions instead of deleting data.
        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("CREATE TABLE IF NOT EXISTS persons (id TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, category TEXT NOT NULL, active INTEGER NOT NULL)")
                db.execSQL("CREATE TABLE IF NOT EXISTS vehicles (id TEXT NOT NULL PRIMARY KEY, registration TEXT NOT NULL, type TEXT NOT NULL, active INTEGER NOT NULL)")
                db.execSQL("CREATE TABLE IF NOT EXISTS vehicle_manifests (manifestId TEXT NOT NULL PRIMARY KEY, vehicleId TEXT NOT NULL, eventId TEXT NOT NULL, locationId TEXT NOT NULL, gateId TEXT NOT NULL, driverId TEXT NOT NULL, coDriverId TEXT, createdAt INTEGER NOT NULL, syncStatus TEXT NOT NULL)")
                db.execSQL("CREATE TABLE IF NOT EXISTS vehicle_manifest_members (manifestId TEXT NOT NULL, personId TEXT NOT NULL, sequence INTEGER NOT NULL, PRIMARY KEY(manifestId, personId))")
            }
        }

        val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE vehicle_manifests ADD COLUMN state TEXT NOT NULL DEFAULT 'ACTIVE'")
                db.execSQL("ALTER TABLE vehicle_manifests ADD COLUMN exitEventId TEXT")
                db.execSQL("ALTER TABLE vehicle_manifests ADD COLUMN exitAt INTEGER")
            }
        }

        val MIGRATION_3_4 = object : Migration(3, 4) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN syncUpdatedAt INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN sourceType TEXT NOT NULL DEFAULT 'DIRECT'")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN sourceId TEXT")
                db.execSQL("ALTER TABLE vehicle_manifests ADD COLUMN syncUpdatedAt INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE vehicle_manifests ADD COLUMN lastError TEXT")
                db.execSQL("CREATE TABLE IF NOT EXISTS presence_sessions (sessionId TEXT NOT NULL PRIMARY KEY, personId TEXT NOT NULL, vehicleId TEXT, sourceType TEXT NOT NULL, sourceId TEXT, entryEventId TEXT NOT NULL, entryAt INTEGER NOT NULL, locationId TEXT NOT NULL, gateId TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'ACTIVE', exitEventId TEXT, exitAt INTEGER)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_events_entity ON movement_events(entityType, entityId, createdAt)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_events_sync ON movement_events(syncStatus, createdAt)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_events_source ON movement_events(sourceType, sourceId)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_sessions_person_status ON presence_sessions(personId, status)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_sessions_source_status ON presence_sessions(sourceType, sourceId, status)")
                db.execSQL("CREATE INDEX IF NOT EXISTS idx_manifest_vehicle_state ON vehicle_manifests(vehicleId, state)")
            }
        }

        /** v6: hashed QR secrets for the Minimal data-sharing mode (keeps any queued, unsynced records). */
        val MIGRATION_5_6 = object : Migration(5, 6) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE persons ADD COLUMN secretHash TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE vehicles ADD COLUMN secretHash TEXT NOT NULL DEFAULT ''")
            }
        }

        /** v7: entry/exit reason and remarks on gate records (queued records are kept). */
        val MIGRATION_6_7 = object : Migration(6, 7) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN reason TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN remarks TEXT NOT NULL DEFAULT ''")
            }
        }

        /** v8: visitor pass validity on persons; expected return on gate records. */
        val MIGRATION_7_8 = object : Migration(7, 8) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE persons ADD COLUMN validFrom INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE persons ADD COLUMN validTo INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN expectedReturn INTEGER NOT NULL DEFAULT 0")
            }
        }

        /** v9: where the person is coming from, entered by the guard on ENTRY only. */
        val MIGRATION_8_9 = object : Migration(8, 9) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN comingFrom TEXT NOT NULL DEFAULT ''")
            }
        }

        /** v10: destination and approximate minutes of a vehicle exit. */
        val MIGRATION_9_10 = object : Migration(9, 10) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN destinationId TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN destinationName TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN transitMinutes INTEGER NOT NULL DEFAULT 0")
            }
        }

        /** v11: the server sequence number and time the Command Center recorded each entry. */
        val MIGRATION_10_11 = object : Migration(10, 11) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN serverSeq INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE movement_events ADD COLUMN serverRecordedAt INTEGER NOT NULL DEFAULT 0")
            }
        }

        /** v12: comma-separated flags for events requiring attention (e.g. "DUPLICATE_ENTRY"). */
        val MIGRATION_11_12 = object : Migration(11, 12) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE movement_events ADD COLUMN flags TEXT NOT NULL DEFAULT ''")
            }
        }

        /** Closes the open database (used before the terminal is wiped). */
        fun closeInstance() = synchronized(this) { INSTANCE?.close(); INSTANCE = null }

        val ALL_MIGRATIONS = arrayOf(MIGRATION_1_2, MIGRATION_2_3, MIGRATION_3_4)

        fun get(context: Context): AppDatabase =
            INSTANCE ?: synchronized(this) {
                INSTANCE ?: run {
                    // Remove the old unencrypted prototype database if it exists.
                    context.applicationContext.deleteDatabase("teamxv_qr_monitor.db")
                    System.loadLibrary("sqlcipher")
                    val passphrase = com.teamxv.qrmonitor.security.SecureStore(context.applicationContext).databasePassphrase()
                    Room.databaseBuilder(context.applicationContext, AppDatabase::class.java, "xv_access_control_secure.db")
                        .openHelperFactory(net.zetetic.database.sqlcipher.SupportOpenHelperFactory(passphrase))
                        .addMigrations(MIGRATION_5_6, MIGRATION_6_7, MIGRATION_7_8, MIGRATION_8_9, MIGRATION_9_10, MIGRATION_10_11, MIGRATION_11_12)
                        .fallbackToDestructiveMigration()
                        .build()
                }
                    .also { INSTANCE = it }
            }
    }
}
