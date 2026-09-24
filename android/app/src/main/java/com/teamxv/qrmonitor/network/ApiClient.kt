package com.teamxv.qrmonitor.network

import android.util.Base64
import com.teamxv.qrmonitor.data.model.MovementEvent
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.encodeToJsonElement
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.X509Certificate
import java.util.concurrent.TimeUnit
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec
import javax.net.ssl.SSLContext
import javax.net.ssl.X509TrustManager

class HttpFailure(val code: Int, val bodyText: String) : Exception(
    runCatching { Json.parseToJsonElement(bodyText).jsonObject["message"]?.jsonPrimitive?.content }.getOrNull() ?: "HTTP $code"
) {
    val reason: String get() = runCatching { Json.parseToJsonElement(bodyText).jsonObject["reason"]?.jsonPrimitive?.content }.getOrNull() ?: ""
}

@Serializable data class LoginResponse(val status: String, val username: String = "", val name: String = "", val role: String = "", val accessToken: String = "", val expiresAt: Long = 0L, val offlineGraceSeconds: Long = 0L)
@Serializable data class RegisterResponse(val status: String, val username: String = "")
@Serializable data class HealthResponse(val status: String, val service: String = "", val serverId: String = "", val serverName: String = "", val version: String = "", val serverTime: Long = 0L, val publicUrl: String = "", val internetEnabled: Boolean = false)
@Serializable data class EventPayload(
    val eventId: String, val entityType: String, val entityId: String, val eventType: String,
    val locationId: String, val gateId: String, val deviceId: String, val operatorId: String,
    val eventTimestamp: Long, val createdAt: Long, val sourceType: String = "DIRECT", val sourceId: String? = null,
    val locationMismatch: Boolean = false, val scannedLocation: String = ""
)
@Serializable data class VehicleManifestPayload(
    val manifestId: String, val vehicleId: String, val entryEventId: String, val locationId: String,
    val gateId: String, val driverId: String, val coDriverId: String? = null, val occupants: List<String>,
    val createdAt: Long, val state: String = "ACTIVE", val exitEventId: String? = null, val exitAt: Long? = null
)
@Serializable data class VehicleTransactionPayload(val event: EventPayload, val manifest: VehicleManifestPayload)
@Serializable data class MasterPerson(
    val personId: String, val name: String, val category: String = "PERSONNEL", val active: Boolean = true,
    val secretCode: String = "", val rank: String = "", val serviceNo: String = "", val unit: String = "",
    val company: String = "", val role: String = "", val status: String = "ACTIVE", val accessLocations: String = "",
    val secretHash: String = ""
)
@Serializable data class MasterVehicle(
    val vehicleId: String, val registration: String, val type: String = "", val active: Boolean = true,
    val secretCode: String = "", val milReg: String = "", val model: String = "", val company: String = "", val status: String = "ACTIVE",
    val secretHash: String = ""
)
@Serializable data class NamedItem(val id: String, val name: String)
@Serializable data class PresenceItem(val personId: String, val entryAt: Long)
@Serializable data class ManifestItem(
    val manifestId: String, val vehicleId: String, val entryEventId: String, val locationId: String = "", val gateId: String = "",
    val driverId: String, val coDriverId: String? = null, val occupants: List<String> = emptyList(), val createdAt: Long = 0L
)
/** Result of an online credential check (Receive-only mode). Shown on screen, never stored with personal details. */
@Serializable data class VerifyResponse(
    val type: String, val id: String, val name: String = "", val rank: String = "", val serviceNo: String = "", val unit: String = "",
    val company: String = "", val category: String = "", val status: String = "ACTIVE", val inside: Boolean = false, val insideSince: Long = 0L,
    val registration: String = "", val vehicleType: String = "", val manifest: ManifestItem? = null
)
@Serializable data class MasterBootstrapResponse(
    val sharingMode: String = "FULL", val manifests: List<ManifestItem> = emptyList(),
    val version: String = "", val persons: List<MasterPerson> = emptyList(), val vehicles: List<MasterVehicle> = emptyList(),
    val locations: List<NamedItem> = emptyList(), val gates: List<NamedItem> = emptyList(),
    val presence: List<PresenceItem> = emptyList(), val serverTime: Long = 0L
)

/** Contents of the pairing QR shown by the PC Command Center ("XVGK1:" + base64url JSON). */
@Serializable data class PairingInfo(
    val id: String, val n: String = "", val h: List<String> = emptyList(), val p: Int = 8443,
    val u: String = "", val pc: Boolean = false, val fp: String, val c: String
) {
    companion object {
        fun parse(raw: String): PairingInfo? = runCatching {
            val text = raw.trim()
            require(text.startsWith("XVGK1:"))
            val decoded = String(Base64.decode(text.removePrefix("XVGK1:"), Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING))
            Json { ignoreUnknownKeys = true }.decodeFromString<PairingInfo>(decoded)
        }.getOrNull()
    }
}

@Serializable data class EnrollResponse(val deviceId: String, val deviceKey: String, val serverId: String = "", val serverName: String = "", val fingerprint: String = "")

/** One way of reaching the PC: a base URL and whether its certificate must match the pinned fingerprint. */
data class Endpoint(val baseUrl: String, val pinned: Boolean, val internet: Boolean)

/** Connection settings the client needs; supplied by AppConfig so changes apply immediately. */
interface ConnectionProfile {
    val deviceId: String
    val deviceKey: String
    val fingerprint: String
    fun endpoints(): List<Endpoint>
    fun onLanHostDiscovered(host: String, port: Int)
}

/**
 * Secure client for the XV Command Center.
 * - TLS: the PC's self-signed certificate is pinned by its SHA-256 fingerprint (from the pairing QR).
 *   For a cloud tunnel that presents a public certificate, the normal Android trust store is used instead.
 * - Every call after pairing is an AES-256-GCM envelope sealed with this terminal's own key,
 *   so data stays end-to-end encrypted even through relays.
 * - Endpoints are tried in order (LAN first, internet second); the one that worked is preferred next time.
 */
class ApiClient(private val profile: ConnectionProfile? = null) {
    private val json = Json { ignoreUnknownKeys = true; explicitNulls = false; encodeDefaults = true }
    private val random = SecureRandom()
    @Volatile private var preferred: String? = null
    /** Difference between the Command Center's clock and this phone's, learned automatically. */
    @Volatile private var clockOffsetMs: Long = 0L
    @Volatile var lastRoute: String = ""
        private set

    var operatorToken: String = ""

    fun clearCredentials() { operatorToken = "" }

    // ------------------------------------------------------------------ operations

    fun login(baseUrl: String, username: String, password: String): Result<LoginResponse> = runCatching {
        json.decodeFromJsonElement(LoginResponse.serializer(), rpc("auth.login", buildJsonObject { put("username", username); put("password", password) }, withToken = false))
    }

    fun register(name: String, username: String, password: String): Result<RegisterResponse> = runCatching {
        json.decodeFromJsonElement(RegisterResponse.serializer(), rpc("auth.register", buildJsonObject { put("name", name); put("username", username); put("password", password) }, withToken = false))
    }

    fun stations(): Result<Pair<List<Pair<String, String>>, List<Pair<String, String>>>> = runCatching {
        val r = rpc("stations.list", JsonObject(emptyMap()), withToken = false).jsonObject
        fun list(k: String) = json.decodeFromJsonElement(kotlinx.serialization.builtins.ListSerializer(NamedItem.serializer()), r[k] ?: kotlinx.serialization.json.JsonArray(emptyList())).map { it.id to it.name }
        list("locations") to list("gates")
    }

    fun verify(code: String, expected: String): Result<VerifyResponse> = runCatching {
        json.decodeFromJsonElement(VerifyResponse.serializer(), rpc("credential.verify", buildJsonObject { put("code", code); put("expected", expected) }))
    }

    fun logout(baseUrl: String): Result<String> = runCatching { rpc("auth.logout", JsonObject(emptyMap())).toString() }

    fun health(baseUrl: String): Result<HealthResponse> = runCatching {
        json.decodeFromJsonElement(HealthResponse.serializer(), rpc("health", JsonObject(emptyMap()), withToken = false))
    }

    fun fetchMaster(baseUrl: String): Result<MasterBootstrapResponse> = runCatching {
        json.decodeFromJsonElement(MasterBootstrapResponse.serializer(), rpc("master.bootstrap", JsonObject(emptyMap())))
    }

    fun heartbeat(baseUrl: String, deviceId: String, locationId: String, gateId: String, operatorId: String = "", pending: Int = 0, appVersion: String = ""): Result<String> = runCatching {
        rpc("heartbeat", buildJsonObject {
            put("locationId", locationId); put("gateId", gateId); put("operatorId", operatorId); put("pending", pending); put("appVersion", appVersion)
        }).toString()
    }

    fun submitEvent(baseUrl: String, event: MovementEvent): Result<String> = runCatching {
        rpc("events.create", json.encodeToJsonElement(EventPayload(
            event.eventId, event.entityType.name, event.entityId, event.eventType.name,
            event.locationId, event.gateId, event.deviceId, event.operatorId,
            event.eventTimestamp, event.createdAt, event.sourceType.name, event.sourceId,
            event.locationMismatch, event.scannedLocation
        ))).toString()
    }

    fun submitVehicleTransaction(baseUrl: String, tx: VehicleTransactionPayload): Result<String> = runCatching {
        rpc("vehicle.transaction", json.encodeToJsonElement(tx)).toString()
    }

    // ------------------------------------------------------------------ pairing (no device key yet)

    /** Enrolls this terminal using the one-time code from the pairing QR. Tries LAN addresses, then the internet URL. */
    fun enroll(info: PairingInfo, deviceName: String, model: String): Result<Pair<EnrollResponse, Endpoint>> = runCatching {
        val candidates = info.h.map { Endpoint("https://$it:${info.p}", pinned = true, internet = false) } +
            listOfNotNull(info.u.takeIf { it.isNotBlank() }?.let { Endpoint(it.trimEnd('/'), pinned = !info.pc, internet = true) })
        require(candidates.isNotEmpty()) { "The QR does not contain any server address" }
        val body = buildJsonObject { put("code", info.c); put("deviceName", deviceName); put("model", model) }.toString()
        var lastError: Exception? = null
        for (ep in candidates) {
            try {
                val req = Request.Builder().url(ep.baseUrl + "/api/v1/pair/enroll").post(body.toRequestBody(JSON_TYPE)).build()
                client(ep.pinned, info.fp, quick = true).newCall(req).execute().use { res ->
                    val text = res.body?.string().orEmpty()
                    if (!res.isSuccessful) throw HttpFailure(res.code, text)
                    return@runCatching json.decodeFromString(EnrollResponse.serializer(), text) to ep
                }
            } catch (e: HttpFailure) { throw e } catch (e: Exception) { lastError = e }
        }
        throw IOException("Could not reach the Command Center at " + candidates.joinToString { it.baseUrl } + ". Check that this phone is on the same network.", lastError)
    }

    // ------------------------------------------------------------------ transport

    private fun rpc(op: String, data: JsonElement, withToken: Boolean = true): JsonElement {
        val p = profile ?: throw IllegalStateException("Terminal is not paired")
        if (p.deviceKey.isBlank() || p.deviceId.isBlank()) throw IllegalStateException("Terminal is not paired with a Command Center. Scan the pairing QR first.")
        val key = Base64.decode(p.deviceKey, Base64.NO_WRAP)
        val request = buildJsonObject {
            put("op", op); put("data", data)
            put("token", if (withToken && operatorToken.isNotBlank()) JsonPrimitive(operatorToken) else JsonNull)
        }.toString()

        var endpoints = p.endpoints().sortedByDescending { it.baseUrl == preferred }
        var lastError: Exception = IOException("No server address configured")
        for (attempt in 0..1) {
            for (ep in endpoints) {
                try {
                    val result = call(ep, p, key, request)
                    preferred = ep.baseUrl
                    lastRoute = if (ep.internet) "Internet • ${ep.baseUrl}" else "Local Wi-Fi • ${ep.baseUrl}"
                    return result
                } catch (e: HttpFailure) {
                    if (e.code == 401 && e.reason == "STALE_OR_REPLAYED" && syncClock(ep, p)) {
                        val result = call(ep, p, key, request)
                        preferred = ep.baseUrl
                        return result
                    }
                    throw e
                } catch (e: Exception) {
                    lastError = e
                }
            }
            // LAN address may have changed (DHCP): ask the network where the PC is now, then retry once.
            if (attempt == 0 && discover(p)) endpoints = p.endpoints() else break
        }
        throw lastError
    }

    private fun call(ep: Endpoint, p: ConnectionProfile, key: ByteArray, plaintext: String): JsonElement {
        val ts = System.currentTimeMillis() + clockOffsetMs
        val nonceBytes = ByteArray(16).also(random::nextBytes)
        val nonce = Base64.encodeToString(nonceBytes, Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)
        val (iv, ct) = seal(key, plaintext, "XVGK1|req|${p.deviceId}|$ts|$nonce")
        val body = buildJsonObject { put("iv", iv); put("ct", ct) }.toString()
        val req = Request.Builder().url(ep.baseUrl + "/api/v1/rpc")
            .header("X-GK-Device", p.deviceId).header("X-GK-Ts", ts.toString()).header("X-GK-Nonce", nonce)
            .post(body.toRequestBody(JSON_TYPE)).build()
        client(ep.pinned, p.fingerprint).newCall(req).execute().use { res ->
            val text = res.body?.string().orEmpty()
            if (!res.isSuccessful) throw HttpFailure(res.code, text)
            val env = json.parseToJsonElement(text).jsonObject
            val opened = json.parseToJsonElement(open(key, env["iv"]!!.jsonPrimitive.content, env["ct"]!!.jsonPrimitive.content, "XVGK1|res|${p.deviceId}|$nonce")).jsonObject
            val code = opened["code"]!!.jsonPrimitive.int
            val out = opened["body"] ?: JsonNull
            if (code >= 400) throw HttpFailure(code, out.toString())
            return out
        }
    }

    /** Reads the server clock from /ping so a phone with a wrong clock can still talk to the Command Center. */
    private fun syncClock(ep: Endpoint, p: ConnectionProfile): Boolean = runCatching {
        val before = System.currentTimeMillis()
        client(ep.pinned, p.fingerprint, quick = true).newCall(Request.Builder().url(ep.baseUrl + "/api/v1/ping").get().build()).execute().use { res ->
            val serverTime = json.parseToJsonElement(res.body?.string().orEmpty()).jsonObject["serverTime"]!!.jsonPrimitive.content.toLong()
            clockOffsetMs = serverTime - (before + System.currentTimeMillis()) / 2
        }
        true
    }.getOrDefault(false)

    /** UDP broadcast probe; the Command Center answers with its id so we only accept our own server. */
    private fun discover(p: ConnectionProfile): Boolean = runCatching {
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = 1500
            val probe = "XVGK_DISCOVER_V1".toByteArray()
            socket.send(DatagramPacket(probe, probe.size, InetAddress.getByName("255.255.255.255"), DISCOVERY_PORT))
            val buf = ByteArray(1024)
            val deadline = System.currentTimeMillis() + 1500
            while (System.currentTimeMillis() < deadline) {
                val packet = DatagramPacket(buf, buf.size)
                socket.receive(packet)
                val reply = json.parseToJsonElement(String(packet.data, 0, packet.length)).jsonObject
                if (reply["fingerprint"]?.jsonPrimitive?.content.equals(p.fingerprint, ignoreCase = true)) {
                    p.onLanHostDiscovered(packet.address.hostAddress ?: continue, reply["port"]?.jsonPrimitive?.int ?: 8443)
                    return@runCatching true
                }
            }
            false
        }
    }.getOrDefault(false)

    private fun client(pinned: Boolean, fingerprint: String, quick: Boolean = false): OkHttpClient {
        val builder = OkHttpClient.Builder()
            .connectTimeout(if (quick) 4 else 6, TimeUnit.SECONDS)
            .readTimeout(20, TimeUnit.SECONDS)
            .writeTimeout(20, TimeUnit.SECONDS)
        if (pinned) {
            val tm = PinnedTrustManager(fingerprint)
            val ctx = SSLContext.getInstance("TLS").apply { init(null, arrayOf(tm), random) }
            builder.sslSocketFactory(ctx.socketFactory, tm)
            // Identity is proven by the pinned certificate itself, so the host name (a LAN IP) is not checked.
            builder.hostnameVerifier { _, _ -> true }
        }
        return builder.build()
    }

    private class PinnedTrustManager(private val fingerprint: String) : X509TrustManager {
        override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) = throw java.security.cert.CertificateException("Not used")
        override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {
            val leaf = chain?.firstOrNull() ?: throw java.security.cert.CertificateException("No certificate")
            val actual = MessageDigest.getInstance("SHA-256").digest(leaf.encoded).joinToString("") { "%02x".format(it) }
            if (!actual.equals(fingerprint, ignoreCase = true)) throw java.security.cert.CertificateException("Server certificate does not match the paired Command Center")
        }
        override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
    }

    companion object {
        const val DISCOVERY_PORT = 47913
        private val JSON_TYPE = "application/json; charset=utf-8".toMediaType()

        fun seal(key: ByteArray, plaintext: String, aad: String): Pair<String, String> {
            val iv = ByteArray(12).also { SecureRandom().nextBytes(it) }
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv))
            c.updateAAD(aad.toByteArray())
            return Base64.encodeToString(iv, Base64.NO_WRAP) to Base64.encodeToString(c.doFinal(plaintext.toByteArray()), Base64.NO_WRAP)
        }

        fun open(key: ByteArray, ivB64: String, ctB64: String, aad: String): String {
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, Base64.decode(ivB64, Base64.NO_WRAP)))
            c.updateAAD(aad.toByteArray())
            return String(c.doFinal(Base64.decode(ctB64, Base64.NO_WRAP)))
        }
    }
}
