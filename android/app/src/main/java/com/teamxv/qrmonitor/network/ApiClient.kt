package com.teamxv.qrmonitor.network

import com.teamxv.qrmonitor.data.model.MovementEvent
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

class HttpFailure(val code: Int, val bodyText: String): Exception("HTTP $code")

@Serializable data class LoginResponse(val status:String,val username:String="",val role:String="",val accessToken:String="",val expiresAt:Long=0L,val offlineGraceSeconds:Long=0L)
@Serializable data class LoginPayload(val username:String,val password:String)
@Serializable data class HealthResponse(val status: String, val service: String = "", val database: String = "", val timestamp: String = "")
@Serializable data class EventPayload(
    val eventId: String, val entityType: String, val entityId: String, val eventType: String,
    val locationId: String, val gateId: String, val deviceId: String, val operatorId: String,
    val eventTimestamp: Long, val createdAt: Long, val sourceType: String = "DIRECT", val sourceId: String? = null
)
@Serializable data class VehicleManifestPayload(
    val manifestId: String, val vehicleId: String, val entryEventId: String, val locationId: String,
    val gateId: String, val driverId: String, val coDriverId: String? = null, val occupants: List<String>,
    val createdAt: Long, val state: String = "ACTIVE", val exitEventId: String? = null, val exitAt: Long? = null
)
@Serializable data class VehicleTransactionPayload(
    val event: EventPayload,
    val manifest: VehicleManifestPayload
)
@Serializable data class HeartbeatPayload(val deviceId: String, val locationId: String, val gateId: String)
@Serializable data class MasterPerson(val personId: String, val name: String, val category: String, val active: Boolean)
@Serializable data class MasterVehicle(val vehicleId: String, val registration: String, val type: String, val active: Boolean)
@Serializable data class MasterBootstrapResponse(
    val version: String, val persons: List<MasterPerson>, val vehicles: List<MasterVehicle>,
    val locations: List<String>, val serverTime: Long
)

class ApiClient {
    private val client = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .writeTimeout(15, TimeUnit.SECONDS)
        .retryOnConnectionFailure(true)
        .build()

    private val json = Json { ignoreUnknownKeys = true }

    var deviceKey: String = ""
    var operatorToken: String = ""
    var deviceId: String = ""

    fun clearCredentials() {
        deviceKey = ""
        operatorToken = ""
        deviceId = ""
    }

    fun login(baseUrl: String, username: String, password: String): Result<LoginResponse> = runCatching {
        json.decodeFromString(post(baseUrl, "/api/v1/auth/login", json.encodeToString(LoginPayload(username, password))))
    }

    fun logout(baseUrl: String): Result<String> = runCatching {
        post(baseUrl, "/api/v1/auth/logout", "{}")
    }

    fun health(baseUrl: String): Result<HealthResponse> = runCatching {
        json.decodeFromString(get(baseUrl, "/api/v1/health"))
    }

    fun fetchMaster(baseUrl: String): Result<MasterBootstrapResponse> = runCatching {
        json.decodeFromString(get(baseUrl, "/api/v1/master/bootstrap"))
    }

    fun heartbeat(baseUrl: String, deviceId: String, locationId: String, gateId: String): Result<String> = runCatching {
        post(baseUrl, "/api/v1/heartbeat", json.encodeToString(HeartbeatPayload(deviceId, locationId, gateId)))
    }

    fun submitEvent(baseUrl: String, event: MovementEvent): Result<String> = runCatching {
        post(baseUrl, "/api/v1/events", json.encodeToString(EventPayload(
            event.eventId, event.entityType.name, event.entityId, event.eventType.name,
            event.locationId, event.gateId, event.deviceId, event.operatorId,
            event.eventTimestamp, event.createdAt, event.sourceType.name, event.sourceId
        )))
    }

    fun submitVehicleTransaction(baseUrl: String, tx: VehicleTransactionPayload): Result<String> = runCatching {
        post(baseUrl, "/api/v1/sync/vehicle-transaction", json.encodeToString(tx))
    }

    private fun get(baseUrl: String, path: String): String {
        val builder = Request.Builder().url(baseUrl.trimEnd('/') + path).get()
        if (deviceKey.isNotBlank()) builder.header("X-Device-Key", deviceKey)
        if (deviceId.isNotBlank()) builder.header("X-Device-Id", deviceId)
        if (operatorToken.isNotBlank() && path != "/api/v1/auth/login") builder.header("Authorization", "Bearer $operatorToken")
        val request = builder.build()
        client.newCall(request).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful) throw HttpFailure(response.code, body)
            return body
        }
    }

    private fun post(baseUrl: String, path: String, payload: String): String {
        val body = payload.toRequestBody("application/json; charset=utf-8".toMediaType())
        val builder = Request.Builder().url(baseUrl.trimEnd('/') + path).post(body)
        if (deviceKey.isNotBlank()) builder.header("X-Device-Key", deviceKey)
        if (deviceId.isNotBlank()) builder.header("X-Device-Id", deviceId)
        if (operatorToken.isNotBlank() && path != "/api/v1/auth/login") builder.header("Authorization", "Bearer $operatorToken")
        val request = builder.build()
        client.newCall(request).execute().use { response ->
            val text = response.body?.string().orEmpty()
            if (!response.isSuccessful) throw HttpFailure(response.code, text)
            return text
        }
    }
}
