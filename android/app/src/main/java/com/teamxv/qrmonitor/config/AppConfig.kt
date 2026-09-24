package com.teamxv.qrmonitor.config

import android.content.Context
import com.teamxv.qrmonitor.BuildConfig
import com.teamxv.qrmonitor.security.SecureStore
import java.net.URI

class AppConfig(context: Context) {
    private val prefs = context.getSharedPreferences("teamxv_config", Context.MODE_PRIVATE)
    private val secure = SecureStore(context.applicationContext)

    init {
        // One-time migration from earlier plaintext prototype storage.
        val legacyDeviceKey = prefs.getString("deviceKey", null)
        if (!legacyDeviceKey.isNullOrBlank() && secure.get("deviceKey").isNullOrBlank()) {
            secure.put("deviceKey", legacyDeviceKey)
        }
        val legacyToken = prefs.getString("operatorToken", null)
        if (!legacyToken.isNullOrBlank() && secure.get("operatorToken").isNullOrBlank()) {
            secure.put("operatorToken", legacyToken)
        }
        if (!legacyDeviceKey.isNullOrBlank() || !legacyToken.isNullOrBlank()) {
            prefs.edit().remove("deviceKey").remove("operatorToken").apply()
        }
    }

    var serverHost: String
        get() = prefs.getString("serverHost", BuildConfig.DEFAULT_SERVER_HOST)
            ?: BuildConfig.DEFAULT_SERVER_HOST
        set(value) = prefs.edit().putString("serverHost", sanitizeHost(value)).apply()

    var serverPort: Int
        get() = prefs.getInt("serverPort", BuildConfig.DEFAULT_SERVER_PORT)
        set(value) = prefs.edit().putInt("serverPort", value.coerceIn(1, 65535)).apply()

    val protocol: String
        get() = if (BuildConfig.DEBUG) "http" else "https"

    val baseUrl: String
        get() = "$protocol://${sanitizeHost(serverHost)}:$serverPort"

    var locationId: String
        get() = prefs.getString("locationId", "LOC01") ?: "LOC01"
        set(value) = prefs.edit().putString("locationId", value.trim().uppercase()).apply()

    var gateId: String
        get() = prefs.getString("gateId", "G01") ?: "G01"
        set(value) = prefs.edit().putString("gateId", value.trim().uppercase()).apply()

    var deviceId: String
        get() = prefs.getString("deviceId", "DEV01") ?: "DEV01"
        set(value) = prefs.edit().putString("deviceId", value.trim().uppercase()).apply()

    var deviceKey: String
        get() = secure.get("deviceKey") ?: ""
        set(value) {
            if (value.isBlank()) secure.remove("deviceKey") else secure.put("deviceKey", value.trim())
        }

    var operatorToken: String
        get() = secure.get("operatorToken") ?: ""
        set(value) {
            if (value.isBlank()) secure.remove("operatorToken") else secure.put("operatorToken", value.trim())
        }

    var operatorTokenExpiresAt: Long
        get() = prefs.getLong("operatorTokenExpiresAt", 0L)
        set(value) = prefs.edit().putLong("operatorTokenExpiresAt", value).apply()

    var offlineSessionUntil: Long
        get() = prefs.getLong("offlineSessionUntil", 0L)
        set(value) = prefs.edit().putLong("offlineSessionUntil", value).apply()

    var operatorRole: String
        get() = prefs.getString("operatorRole", "OPERATOR") ?: "OPERATOR"
        set(value) = prefs.edit().putString("operatorRole", value.trim().uppercase()).apply()

    var operatorLoggedIn: Boolean
        get() = prefs.getBoolean("operatorLoggedIn", false)
        set(value) = prefs.edit().putBoolean("operatorLoggedIn", value).apply()

    var operatorId: String
        get() = prefs.getString("operatorId", "GK01") ?: "GK01"
        set(value) = prefs.edit().putString("operatorId", value.trim().uppercase()).apply()

    var soundEnabled: Boolean
        get() = prefs.getBoolean("soundEnabled", true)
        set(value) = prefs.edit().putBoolean("soundEnabled", value).apply()

    fun saveLogin(username: String, role: String, accessToken: String, expiresAt: Long, offlineGraceSeconds: Long) {
        operatorId = username
        operatorRole = role
        operatorToken = accessToken
        operatorTokenExpiresAt = expiresAt
        offlineSessionUntil = expiresAt + offlineGraceSeconds.coerceAtLeast(0L) * 1000L
        operatorLoggedIn = true
    }

    fun hasValidOnlineToken(now: Long = System.currentTimeMillis()): Boolean =
        operatorToken.isNotBlank() && operatorTokenExpiresAt > now

    fun canContinueOffline(now: Long = System.currentTimeMillis()): Boolean =
        operatorLoggedIn && offlineSessionUntil > now

    fun clearSession() {
        secure.remove("operatorToken")
        prefs.edit()
            .remove("operatorTokenExpiresAt")
            .remove("offlineSessionUntil")
            .putBoolean("operatorLoggedIn", false)
            .apply()
    }

    private fun sanitizeHost(value: String): String {
        val raw = value.trim().removeSuffix("/")
        if (raw.isBlank()) return BuildConfig.DEFAULT_SERVER_HOST
        return runCatching {
            val candidate = if (raw.contains("://")) raw else "http://$raw"
            val host = URI(candidate).host
            host?.takeIf { it.isNotBlank() } ?: raw.replace(Regex("^https?://"), "").trimEnd('/')
        }.getOrElse { raw.replace(Regex("^https?://"), "").trimEnd('/') }
    }
}
