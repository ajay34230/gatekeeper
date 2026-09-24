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
    version = 4,
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

        val ALL_MIGRATIONS = arrayOf(MIGRATION_1_2, MIGRATION_2_3, MIGRATION_3_4)

        fun get(context: Context): AppDatabase =
            INSTANCE ?: synchronized(this) {
                INSTANCE ?: Room.databaseBuilder(
                    context.applicationContext,
                    AppDatabase::class.java,
                    "teamxv_qr_monitor.db"
                )
                    .addMigrations(*ALL_MIGRATIONS)
                    .build()
                    .also { INSTANCE = it }
            }
    }
}
