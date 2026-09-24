package com.teamxv.qrmonitor.config

import android.content.Context
import com.teamxv.qrmonitor.network.ConnectionProfile
import com.teamxv.qrmonitor.network.Endpoint
import com.teamxv.qrmonitor.security.SecureStore

/**
 * Terminal configuration. Secrets (device key, operator token) live in the Keystore-backed SecureStore;
 * everything else in private preferences. Nothing is pre-filled: the terminal learns its server by scanning
 * the Command Center's pairing QR.
 */
class AppConfig(context: Context) : ConnectionProfile {
    private val prefs = context.getSharedPreferences("xv_config", Context.MODE_PRIVATE)
    private val secure = SecureStore(context.applicationContext)

    // ------------------------------------------------------------------ pairing / server identity

    val paired: Boolean get() = deviceKey.isNotBlank() && serverFingerprint.isNotBlank()

    var serverId: String
        get() = prefs.getString("serverId", "") ?: ""
        set(value) = prefs.edit().putString("serverId", value).apply()

    var serverName: String
        get() = prefs.getString("serverName", "") ?: ""
        set(value) = prefs.edit().putString("serverName", value).apply()

    var serverFingerprint: String
        get() = prefs.getString("serverFingerprint", "") ?: ""
        set(value) = prefs.edit().putString("serverFingerprint", value.lowercase()).apply()

    /** LAN addresses of the Command Center, most recently working first. */
    var lanHosts: List<String>
        get() = (prefs.getString("lanHosts", "") ?: "").split(',').map { it.trim() }.filter { it.isNotEmpty() }
        set(value) = prefs.edit().putString("lanHosts", value.distinct().joinToString(",")).apply()

    var serverPort: Int
        get() = prefs.getInt("serverPort", 8443)
        set(value) = prefs.edit().putInt("serverPort", value.coerceIn(1, 65535)).apply()

    val serverHost: String get() = lanHosts.firstOrNull() ?: ""

    // ------------------------------------------------------------------ cloud / internet link

    /** AUTO = local network first then internet; LAN = local only; CLOUD = internet only. */
    var connectionMode: String
        get() = prefs.getString("connectionMode", "AUTO") ?: "AUTO"
        set(value) = prefs.edit().putString("connectionMode", value).apply()

    var publicUrl: String
        get() = prefs.getString("publicUrl", "") ?: ""
        set(value) = prefs.edit().putString("publicUrl", value.trim().trimEnd('/')).apply()

    /** True when the internet address is a tunnel with its own public CA certificate (pinning not possible there). */
    var publicUsesCaCertificate: Boolean
        get() = prefs.getBoolean("publicUsesCa", false)
        set(value) = prefs.edit().putBoolean("publicUsesCa", value).apply()

    // Comms engine (separate listener on the PC)
    var commsPort: Int
        get() = prefs.getInt("commsPort", 8444)
        set(value) = prefs.edit().putInt("commsPort", value).apply()
    var commsPublicUrl: String
        get() = prefs.getString("commsPublicUrl", "") ?: ""
        set(value) = prefs.edit().putString("commsPublicUrl", value.trim().trimEnd('/')).apply()
    var commsPublicUsesCa: Boolean
        get() = prefs.getBoolean("commsPublicUsesCa", false)
        set(value) = prefs.edit().putBoolean("commsPublicUsesCa", value).apply()

    /** Comms engine addresses: every known LAN host on the comms port first, then the internet URL. */
    fun commsEndpoints(): List<Endpoint> =
        lanHosts.map { Endpoint("https://$it:$commsPort", pinned = true, internet = false) } +
            listOfNotNull(commsPublicUrl.takeIf { it.startsWith("https://") }?.let { Endpoint(it, pinned = !commsPublicUsesCa, internet = true) })

    override fun endpoints(): List<Endpoint> {
        val lan = lanHosts.map { Endpoint("https://$it:$serverPort", pinned = true, internet = false) }
        val wan = listOfNotNull(publicUrl.takeIf { it.startsWith("https://") }?.let { Endpoint(it, pinned = !publicUsesCaCertificate, internet = true) })
        return when (connectionMode) {
            "LAN" -> lan
            "CLOUD" -> wan
            else -> lan + wan
        }
    }

    override fun onLanHostDiscovered(host: String, port: Int) {
        lanHosts = listOf(host) + lanHosts
        serverPort = port
    }

    /** First configured address (kept for call sites that log the target). */
    val baseUrl: String get() = endpoints().firstOrNull()?.baseUrl ?: ""

    val serverLabel: String
        get() = when {
            !paired -> "Not paired"
            else -> (serverName.ifBlank { serverId }) + (serverHost.takeIf { it.isNotBlank() }?.let { " • $it:$serverPort" } ?: "")
        }

    // ------------------------------------------------------------------ device identity (from pairing)

    override var deviceId: String
        get() = prefs.getString("deviceId", "") ?: ""
        set(value) = prefs.edit().putString("deviceId", value.trim()).apply()

    override var deviceKey: String
        get() = secure.get("deviceKey") ?: ""
        set(value) { if (value.isBlank()) secure.remove("deviceKey") else secure.put("deviceKey", value.trim()) }

    override val fingerprint: String get() = serverFingerprint

    fun savePairing(serverId: String, serverName: String, fingerprint: String, hosts: List<String>, port: Int, publicUrl: String, publicCa: Boolean, deviceId: String, deviceKey: String) {
        this.serverId = serverId; this.serverName = serverName; serverFingerprint = fingerprint
        lanHosts = hosts; serverPort = port; this.publicUrl = publicUrl; publicUsesCaCertificate = publicCa
        this.deviceId = deviceId; this.deviceKey = deviceKey
        clearSession()
    }

    fun unpair() {
        deviceKey = ""; deviceId = ""; serverFingerprint = ""; serverId = ""; serverName = ""; lanHosts = emptyList(); publicUrl = ""; commsPublicUrl = ""
        clearSession()
    }

    // ------------------------------------------------------------------ gate post

    var locationId: String
        get() = prefs.getString("locationId", "") ?: ""
        set(value) = prefs.edit().putString("locationId", value.trim().uppercase()).apply()

    var locationName: String
        get() = prefs.getString("locationName", "") ?: ""
        set(value) = prefs.edit().putString("locationName", value).apply()

    var gateId: String
        get() = prefs.getString("gateId", "") ?: ""
        set(value) = prefs.edit().putString("gateId", value.trim().uppercase()).apply()

    var gateName: String
        get() = prefs.getString("gateName", "") ?: ""
        set(value) = prefs.edit().putString("gateName", value).apply()

    /** Cached lists from the last server bootstrap, used by the sign-in screen when offline. Format: "ID=Name;ID=Name". */
    var cachedLocations: List<Pair<String, String>>
        get() = decodePairs(prefs.getString("cachedLocations", "") ?: "")
        set(value) = prefs.edit().putString("cachedLocations", encodePairs(value)).apply()

    var cachedGates: List<Pair<String, String>>
        get() = decodePairs(prefs.getString("cachedGates", "") ?: "")
        set(value) = prefs.edit().putString("cachedGates", encodePairs(value)).apply()

    private fun encodePairs(v: List<Pair<String, String>>) = v.joinToString(";") { "${it.first.replace(";", "")}=${it.second.replace(";", ",")}" }
    private fun decodePairs(s: String) = s.split(';').mapNotNull { e -> e.split('=', limit = 2).takeIf { it.size == 2 }?.let { it[0] to it[1] } }

    // ------------------------------------------------------------------ operator session

    var operatorToken: String
        get() = secure.get("operatorToken") ?: ""
        set(value) { if (value.isBlank()) secure.remove("operatorToken") else secure.put("operatorToken", value.trim()) }

    var operatorTokenExpiresAt: Long
        get() = prefs.getLong("operatorTokenExpiresAt", 0L)
        set(value) = prefs.edit().putLong("operatorTokenExpiresAt", value).apply()

    var offlineSessionUntil: Long
        get() = prefs.getLong("offlineSessionUntil", 0L)
        set(value) = prefs.edit().putLong("offlineSessionUntil", value).apply()

    var operatorRole: String
        get() = prefs.getString("operatorRole", "") ?: ""
        set(value) = prefs.edit().putString("operatorRole", value.trim()).apply()

    var operatorName: String
        get() = prefs.getString("operatorName", "") ?: ""
        set(value) = prefs.edit().putString("operatorName", value.trim()).apply()

    var operatorLoggedIn: Boolean
        get() = prefs.getBoolean("operatorLoggedIn", false)
        set(value) = prefs.edit().putBoolean("operatorLoggedIn", value).apply()

    var operatorId: String
        get() = prefs.getString("operatorId", "") ?: ""
        set(value) = prefs.edit().putString("operatorId", value.trim().uppercase()).apply()

    var shiftStartedAt: Long
        get() = prefs.getLong("shiftStartedAt", 0L)
        set(value) = prefs.edit().putLong("shiftStartedAt", value).apply()

    /** Data Sharing mode set by the Command Center administrator: FULL, MINIMAL or RECEIVE_ONLY. */
    var sharingMode: String
        get() = prefs.getString("sharingMode", "MINIMAL") ?: "MINIMAL"
        set(value) = prefs.edit().putString("sharingMode", value).apply()

    var soundEnabled: Boolean
        get() = prefs.getBoolean("soundEnabled", true)
        set(value) = prefs.edit().putBoolean("soundEnabled", value).apply()

    fun saveLogin(username: String, name: String, role: String, accessToken: String, expiresAt: Long, offlineGraceSeconds: Long) {
        operatorId = username
        operatorName = name
        operatorRole = role
        operatorToken = accessToken
        operatorTokenExpiresAt = expiresAt
        offlineSessionUntil = expiresAt + offlineGraceSeconds.coerceAtLeast(0L) * 1000L
        operatorLoggedIn = true
        shiftStartedAt = System.currentTimeMillis()
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
}
