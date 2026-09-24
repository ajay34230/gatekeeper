package com.teamxv.qrmonitor.comms

import android.content.Context
import android.util.Base64
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.network.ApiClient
import com.teamxv.qrmonitor.network.Endpoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.long
import kotlinx.serialization.json.put
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.security.SecureRandom
import java.util.UUID
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

data class CommsState(val status: String = "OFFLINE", val route: String = "", val detail: String = "") {
    val online: Boolean get() = status == "ONLINE"
}

/**
 * Terminal side of the Comms engine: one encrypted WebSocket to the Command Center's separate Comms listener
 * (port 8444), independent of the gate API and its database. Messages are stored in the engine's own SQLCipher
 * database and re-sent until the PC confirms delivery. See docs/PROTOCOL.md → "Comms engine".
 */
object CommsEngine {
    const val MAX_BODY = 2000
    private val json = Json { ignoreUnknownKeys = true }
    private val random = SecureRandom()
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var loop: Job? = null
    private val wake = Channel<Unit>(Channel.CONFLATED)

    private val _state = MutableStateFlow(CommsState())
    val state: StateFlow<CommsState> = _state

    @Volatile private var socket: WebSocket? = null
    @Volatile private var link: Link? = null

    private class Link(val deviceId: String, val key: ByteArray, val nonce: String) {
        var outSeq = 0L
        var inSeq = 0L
    }

    fun start(context: Context) {
        if (loop?.isActive == true) { wake.trySend(Unit); return }
        val app = context.applicationContext
        loop = scope.launch { run(app) }
    }

    fun stop() {
        loop?.cancel(); loop = null
        socket?.close(1000, "stopped"); socket = null; link = null
        _state.value = CommsState()
    }

    /** Reconnect now (network came back, settings changed, …). */
    fun nudge() { wake.trySend(Unit) }

    private suspend fun run(context: Context) {
        var backoff = 2_000L
        while (scope.isActive) {
            val config = AppConfig(context)
            if (!config.paired) {
                _state.value = CommsState("OFFLINE", detail = "Terminal is not paired")
                withTimeoutOrNull(60_000) { wake.receive() }
                continue
            }
            val endpoints = config.commsEndpoints()
            var connectedFor = 0L
            for (ep in endpoints) {
                _state.value = CommsState("CONNECTING", ep.baseUrl)
                val started = System.currentTimeMillis()
                val ok = runCatching { session(context, config, ep) }.getOrElse { e ->
                    _state.value = CommsState("OFFLINE", ep.baseUrl, e.message ?: "Connection failed"); false
                }
                if (ok) { connectedFor = System.currentTimeMillis() - started; break }
            }
            socket = null; link = null
            if (_state.value.status != "OFFLINE") _state.value = CommsState("OFFLINE", detail = "Disconnected — reconnecting")
            backoff = if (connectedFor > 30_000) 2_000L else (backoff * 2).coerceAtMost(60_000L)
            withTimeoutOrNull(backoff) { wake.receive() }
        }
    }

    /** One connection; returns true once it was established (and has now ended). */
    private suspend fun session(context: Context, config: AppConfig, ep: Endpoint): Boolean {
        val deviceKey = Base64.decode(config.deviceKey, Base64.NO_WRAP)
        val key = hmac(deviceKey, "XV-COMMS-1".toByteArray())
        val client: OkHttpClient = ApiClient(config).httpClient(ep.pinned)

        // Server clock (the hello must be within 10 minutes of the PC's time).
        val serverTime = client.newBuilder().callTimeout(10, java.util.concurrent.TimeUnit.SECONDS).build().newCall(Request.Builder().url(ep.baseUrl + "/comms/v1/ping").build()).execute().use { r ->
            if (!r.isSuccessful) throw IllegalStateException("Comms engine refused (${r.code})")
            json.parseToJsonElement(r.body!!.string()).jsonObject["serverTime"]!!.jsonPrimitive.long
        }
        val ts = serverTime
        val nonce = Base64.encodeToString(ByteArray(16).also { random.nextBytes(it) }, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
        val mac = hmac(key, "XVCM1|hello|${config.deviceId}|$ts|$nonce".toByteArray()).joinToString("") { "%02x".format(it) }
        val url = ep.baseUrl.replaceFirst("https://", "wss://") + "/comms/v1/ws?d=${config.deviceId}&t=$ts&n=$nonce&m=$mac"

        val inbox = Channel<String>(Channel.UNLIMITED)
        val closed = kotlinx.coroutines.CompletableDeferred<Boolean>()
        val l = Link(config.deviceId, key, nonce)
        val ws = client.newWebSocket(Request.Builder().url(url).build(), object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                _state.value = CommsState("ONLINE", ep.baseUrl, if (ep.internet) "Internet" else "Local network")
            }
            override fun onMessage(webSocket: WebSocket, text: String) { inbox.trySend(text) }
            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) { webSocket.close(1000, null) }
            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) { inbox.close(); closed.complete(true) }
            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                inbox.close()
                if (response?.code == 401) _state.value = CommsState("OFFLINE", ep.baseUrl, "Rejected by the Command Center (re-pair this terminal)")
                closed.complete(false)
            }
        })
        socket = ws; link = l
        val dao = CommsDatabase.get(context).dao()
        var opened = false
        for (text in inbox) {
            val frame = runCatching { open(l, text) }.getOrNull() ?: run { ws.cancel(); null } ?: break
            if (!opened) { opened = true; flush(context) }
            when (frame["t"]?.jsonPrimitive?.content) {
                "msg" -> {
                    val id = frame["id"]?.jsonPrimitive?.content ?: continue
                    val m = CommsMessageEntity(
                        id, "IN", if (frame["kind"]?.jsonPrimitive?.content == "ALERT") "ALERT" else "MESSAGE",
                        frame["body"]?.jsonPrimitive?.content ?: "", frame["sender"]?.jsonPrimitive?.content ?: "Command Center",
                        frame["ts"]?.jsonPrimitive?.long ?: System.currentTimeMillis(), "RECEIVED"
                    )
                    if (dao.insert(m) != -1L) CommsNotifications.showIncoming(context, m)
                    send(buildJsonObject { put("t", "ack"); put("id", id); put("state", "DELIVERED") })
                }
                "ack" -> {
                    val id = frame["id"]?.jsonPrimitive?.content ?: continue
                    dao.advanceOut(id, if (frame["state"]?.jsonPrimitive?.content == "READ") "READ" else "DELIVERED")
                }
                "call" -> CallManager.onSignal(context, frame)
            }
        }
        socket = null; link = null
        return closed.await() || opened
    }

    // ------------------------------------------------------------------ sending

    /** Stores a message/alert and sends it now if connected; otherwise it goes out on the next connection. */
    suspend fun queue(context: Context, kind: String, body: String, sender: String) {
        val text = body.trim()
        require(text.isNotEmpty()) { "Message is empty" }
        require(text.length <= MAX_BODY) { "Messages are limited to $MAX_BODY characters" }
        CommsDatabase.get(context).dao().insert(
            CommsMessageEntity(UUID.randomUUID().toString().replace("-", ""), "OUT", if (kind == "ALERT") "ALERT" else "MESSAGE", text, sender, System.currentTimeMillis(), "PENDING")
        )
        flush(context)
        if (!_state.value.online) nudge()
    }

    /** Marks received messages as seen and sends read receipts. */
    suspend fun markSeen(context: Context) {
        val dao = CommsDatabase.get(context).dao()
        val ids = dao.unseenIds()
        if (ids.isEmpty()) return
        dao.markAllSeen()
        ids.forEach { send(buildJsonObject { put("t", "ack"); put("id", it); put("state", "READ") }) }
        CommsNotifications.cancelIncoming(context)
    }

    /** Sends a call-signalling frame; false when not connected. */
    fun sendSignal(frame: JsonObject): Boolean = send(frame)

    private suspend fun flush(context: Context) {
        if (link == null) return
        val dao = CommsDatabase.get(context).dao()
        for (m in dao.pendingOut()) {
            val sent = send(buildJsonObject {
                put("t", "msg"); put("id", m.id); put("kind", m.kind); put("body", m.body); put("sender", m.sender); put("ts", m.createdAt)
            })
            if (sent && m.state == "PENDING") dao.setState(m.id, "SENT")
        }
    }

    @Synchronized
    private fun send(frame: JsonObject): Boolean {
        val l = link ?: return false
        val ws = socket ?: return false
        val seq = l.outSeq + 1
        val (iv, ct) = ApiClient.seal(l.key, frame.toString(), "XVCM1|c2s|${l.deviceId}|${l.nonce}|$seq")
        val ok = ws.send(buildJsonObject { put("iv", iv); put("ct", ct) }.toString())
        if (ok) l.outSeq = seq
        return ok
    }

    private fun open(l: Link, text: String): JsonObject {
        val env = json.parseToJsonElement(text).jsonObject
        l.inSeq += 1
        val plain = ApiClient.open(l.key, env["iv"]!!.jsonPrimitive.content, env["ct"]!!.jsonPrimitive.content, "XVCM1|s2c|${l.deviceId}|${l.nonce}|${l.inSeq}")
        return json.parseToJsonElement(plain).jsonObject
    }

    private fun hmac(key: ByteArray, data: ByteArray): ByteArray =
        Mac.getInstance("HmacSHA256").apply { init(SecretKeySpec(key, "HmacSHA256")) }.doFinal(data)
}
