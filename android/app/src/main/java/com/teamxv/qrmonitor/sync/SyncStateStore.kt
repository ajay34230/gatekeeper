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

    fun markSuccess(uploadedCount: Int) {
        prefs.edit()
            .putLong("lastSuccessfulSyncAt", System.currentTimeMillis())
            .putInt("lastUploadedCount", uploadedCount)
            .remove("lastError")
            .apply()
    }

    fun markError(message: String) {
        prefs.edit().putString("lastError", message.take(500)).apply()
    }
}
