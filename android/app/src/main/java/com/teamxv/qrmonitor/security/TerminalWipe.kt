package com.teamxv.qrmonitor.security

import android.content.Context
import android.content.Intent
import android.os.Process
import androidx.work.WorkManager
import com.teamxv.qrmonitor.MainActivity
import com.teamxv.qrmonitor.comms.CommsDatabase
import com.teamxv.qrmonitor.comms.CommsService
import com.teamxv.qrmonitor.data.local.AppDatabase

/**
 * Remote wipe: the Command Center revoked this terminal, so everything it holds is erased — gate database,
 * Comms database, settings, pairing and the Keystore key that protected them. The app restarts unpaired.
 */
object TerminalWipe {
    private const val NOTICE = "xv_wipe_notice"
    private const val KEY_ALIAS = "teamxv_secure_store_v1"
    @Volatile private var running = false

    fun wipe(context: Context) {
        if (running) return
        running = true
        val app = context.applicationContext
        runCatching { CommsService.stop(app) }
        runCatching { WorkManager.getInstance(app).cancelAllWork() }
        runCatching { AppDatabase.closeInstance() }
        runCatching { CommsDatabase.closeInstance() }
        runCatching { java.io.File(app.filesDir, "logs").deleteRecursively(); java.io.File(app.cacheDir, "diag").deleteRecursively() }
        app.databaseList().forEach { runCatching { app.deleteDatabase(it) } }
        listOf("xv_config", "teamxv_secure", "teamxv_sync_state").forEach { runCatching { app.deleteSharedPreferences(it) } }
        runCatching { java.security.KeyStore.getInstance("AndroidKeyStore").apply { load(null) }.deleteEntry(KEY_ALIAS) }
        app.getSharedPreferences(NOTICE, Context.MODE_PRIVATE).edit().putLong("wipedAt", System.currentTimeMillis()).commit()
        // Start fresh: relaunch the UI and end this process so no data stays in memory.
        runCatching {
            app.startActivity(Intent(app, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK))
        }
        Process.killProcess(Process.myPid())
    }

    /** When the last remote wipe happened (0 = never), for the notice on the sign-in screen. */
    fun wipedAt(context: Context): Long = context.getSharedPreferences(NOTICE, Context.MODE_PRIVATE).getLong("wipedAt", 0L)

    fun clearNotice(context: Context) { context.getSharedPreferences(NOTICE, Context.MODE_PRIVATE).edit().clear().apply() }
}
