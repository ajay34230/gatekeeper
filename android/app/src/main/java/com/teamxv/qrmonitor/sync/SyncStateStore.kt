package com.teamxv.qrmonitor.sync

import android.content.Context

class SyncStateStore(context: Context) {
    private val prefs = context.getSharedPreferences("teamxv_sync_state", Context.MODE_PRIVATE)

    val lastSuccessfulSyncAt: Long
        get() = prefs.getLong("lastSuccessfulSyncAt", 0L)

    val lastError: String
        get() = prefs.getString("lastError", "") ?: ""

    val lastUploadedCount: Int
        get() = prefs.getInt("lastUploadedCount", 0)

    val lastRoute: String
        get() = prefs.getString("lastRoute", "") ?: ""

    fun markSuccess(uploadedCount: Int, route: String = "") {
        prefs.edit()
            .putLong("lastSuccessfulSyncAt", System.currentTimeMillis())
            .putInt("lastUploadedCount", uploadedCount)
            .putString("lastRoute", route)
            .remove("lastError")
            .apply()
        com.teamxv.qrmonitor.diag.CrashLog.i("SyncState", "Marked successful: $uploadedCount events via $route")
    }

    fun markError(message: String) {
        prefs.edit().putString("lastError", message.take(500)).apply()
        com.teamxv.qrmonitor.diag.CrashLog.w("SyncState", "Marked error: ${message.take(500)}")
    }
}
