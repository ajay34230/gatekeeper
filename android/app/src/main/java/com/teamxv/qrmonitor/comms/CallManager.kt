package com.teamxv.qrmonitor.comms

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.media.AudioAttributes
import android.media.AudioManager
import android.media.Ringtone
import android.media.RingtoneManager
import android.os.Build
import android.os.Handler
import android.os.Looper
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.teamxv.qrmonitor.R
import com.teamxv.qrmonitor.config.AppConfig
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import java.util.UUID

/** Phase of the single active call. */
enum class CallPhase { RINGING_IN, RINGING_OUT, ACTIVE, ENDED }

data class CallUi(val id: String, val peer: String, val video: Boolean, val outgoing: Boolean, val phase: CallPhase, val message: String = "")

/**
 * Call engine on the terminal: voice / video calls with the Command Center.
 * Signalling ({"t":"call","op",…}) travels over the encrypted Comms engine; media is direct WebRTC in the shared call page.
 */
@android.annotation.SuppressLint("MissingPermission") // notify() calls are guarded by CommsNotifications.canNotify()
object CallManager {
    private const val RING_MS = 45_000L
    private const val CH_CALLS = "xv_calls"
    private const val NOTIF_CALL = 4301
    private const val NOTIF_MISSED = 4302
    private val json = Json { ignoreUnknownKeys = true }
    private val main = Handler(Looper.getMainLooper())

    private val _call = MutableStateFlow<CallUi?>(null)
    val call: StateFlow<CallUi?> = _call

    private var appContext: Context? = null
    private var ringtone: Ringtone? = null
    private var timeout: Runnable? = null
    private var answered = false

    // Messages for the call page, queued until the page is ready.
    private val outbox = ArrayDeque<String>()
    private var pageSink: ((String) -> Unit)? = null

    fun attachPage(sink: (String) -> Unit) { main.post { pageSink = sink; while (outbox.isNotEmpty()) sink(outbox.removeFirst()) } }
    fun detachPage() { main.post { pageSink = null } }

    private fun toPage(m: JsonObject) { val s = m.toString(); pageSink?.invoke(s) ?: outbox.addLast(s) }

    private fun send(op: String, id: String, extra: JsonObject? = null) {
        CommsEngine.sendSignal(buildJsonObject {
            put("t", "call"); put("op", op); put("callId", id)
            extra?.forEach { (k, v) -> put(k, v) }
        })
    }

    // ------------------------------------------------------------------ from the Command Center (Comms engine thread)

    fun onSignal(context: Context, f: JsonObject) = main.post {
        appContext = context.applicationContext
        val op = f["op"]?.jsonPrimitive?.content ?: return@post
        val id = f["callId"]?.jsonPrimitive?.content ?: return@post
        val current = _call.value
        if (op == "invite") {
            if (current != null && current.phase != CallPhase.ENDED) { send("busy", id); return@post }
            val video = runCatching { f["video"]!!.jsonPrimitive.boolean }.getOrDefault(false)
            val from = f["from"]?.jsonPrimitive?.content ?: "Command Center"
            begin(CallUi(id, from, video, outgoing = false, phase = CallPhase.RINGING_IN))
            startRinging(context)
            showIncomingNotification(context)
            timeout = Runnable { if (_call.value?.id == id && !answered) { send("decline", id); finish("Missed call", missed = true) } }.also { main.postDelayed(it, RING_MS) }
            return@post
        }
        if (current == null || current.id != id || current.phase == CallPhase.ENDED) return@post
        when (op) {
            "accept" -> { answered = true; clearTimeout(); _call.value = current.copy(phase = CallPhase.ACTIVE); toPage(buildJsonObject { put("t", "accepted") }) }
            "decline" -> finish("Call declined")
            "busy" -> finish("The Command Center is on another call")
            "cancel" -> finish("Missed call", missed = !answered)
            "hangup" -> finish("Call ended by the Command Center")
            "offer", "answer", "ice" -> {
                val payload = JsonObject(f.filterKeys { it != "t" && it != "callId" })
                toPage(buildJsonObject { put("t", "signal"); put("payload", payload) })
            }
        }
    }

    // ------------------------------------------------------------------ user actions

    /** Starts a call to the Command Center; returns an error text when that is not possible now. */
    fun startOutgoing(context: Context, video: Boolean): String? {
        if (_call.value?.phase?.let { it != CallPhase.ENDED } == true) return "A call is already in progress"
        if (!CommsEngine.state.value.online) return "Comms engine offline — calls need a live connection to the Command Center"
        appContext = context.applicationContext
        val id = UUID.randomUUID().toString().replace("-", "")
        begin(CallUi(id, AppConfig(context).serverName.ifBlank { "Command Center" }, video, outgoing = true, phase = CallPhase.RINGING_OUT))
        val cfg = AppConfig(context)
        send("invite", id, buildJsonObject { put("video", video); put("from", cfg.operatorName.ifBlank { cfg.operatorId }.ifBlank { cfg.deviceId }) })
        timeout = Runnable { if (_call.value?.id == id && !answered) { send("cancel", id); finish("No answer") } }.also { main.postDelayed(it, RING_MS) }
        context.startActivity(Intent(context, CallActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        return null
    }

    fun accept() = main.post {
        val c = _call.value ?: return@post
        if (c.phase != CallPhase.RINGING_IN) return@post
        answered = true; clearTimeout(); stopRinging()
        appContext?.let { NotificationManagerCompat.from(it).cancel(NOTIF_CALL) }
        _call.value = c.copy(phase = CallPhase.ACTIVE)
        // "accept" is sent when the call page reports ready (see onPage), so the offer never arrives before the page.
    }

    fun decline() = main.post {
        val c = _call.value ?: return@post
        if (c.phase == CallPhase.RINGING_IN) { send("decline", c.id); finish("Call declined") }
    }

    fun hangup() = main.post {
        val c = _call.value ?: return@post
        if (c.phase == CallPhase.ENDED) return@post
        send(if (answered) "hangup" else "cancel", c.id)
        finish("Call ended")
    }

    /** Messages from the call page (JavaScript bridge thread). */
    fun onPage(raw: String) = main.post {
        val c = _call.value ?: return@post
        val m = runCatching { json.parseToJsonElement(raw).jsonObject }.getOrNull() ?: return@post
        when (m["t"]?.jsonPrimitive?.content) {
            "ready" -> if (!c.outgoing) send("accept", c.id)
            "signal" -> (m["payload"] as? JsonObject)?.let { p ->
                val op = p["op"]?.jsonPrimitive?.content ?: return@let
                send(op, c.id, JsonObject(p.filterKeys { it != "op" }))
            }
            "hangup" -> hangup()
            "state" -> if (m["state"]?.jsonPrimitive?.content == "ended" && c.phase != CallPhase.ENDED) {
                send(if (answered) "hangup" else "cancel", c.id); finish(m["detail"]?.jsonPrimitive?.content ?: "Call ended")
            }
        }
    }

    /** The call screen was closed. */
    fun clearEnded() { if (_call.value?.phase == CallPhase.ENDED) _call.value = null }

    // ------------------------------------------------------------------ internals

    private fun begin(c: CallUi) {
        clearTimeout(); outbox.clear(); answered = false
        _call.value = c
    }

    private fun finish(message: String, missed: Boolean = false) {
        val c = _call.value ?: return
        clearTimeout(); stopRinging()
        toPage(buildJsonObject { put("t", "end"); put("reason", message) })
        _call.value = c.copy(phase = CallPhase.ENDED, message = message)
        appContext?.let { ctx ->
            NotificationManagerCompat.from(ctx).cancel(NOTIF_CALL)
            if (missed && CommsNotifications.canNotify(ctx)) {
                ensureChannel(ctx)
                runCatching {
                    NotificationManagerCompat.from(ctx).notify(NOTIF_MISSED, NotificationCompat.Builder(ctx, CH_CALLS)
                        .setSmallIcon(R.drawable.ic_stat_xv).setContentTitle("Missed ${if (c.video) "video" else "voice"} call")
                        .setContentText("From ${c.peer}").setAutoCancel(true).setSilent(true).build())
                }
            }
        }
    }

    private fun clearTimeout() { timeout?.let { main.removeCallbacks(it) }; timeout = null }

    private fun startRinging(context: Context) {
        stopRinging()
        ringtone = runCatching {
            RingtoneManager.getRingtone(context, RingtoneManager.getDefaultUri(RingtoneManager.TYPE_RINGTONE))?.apply {
                audioAttributes = AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_NOTIFICATION_RINGTONE).build()
                if (Build.VERSION.SDK_INT >= 28) isLooping = true
                play()
            }
        }.getOrNull()
    }

    private fun stopRinging() { runCatching { ringtone?.stop() }; ringtone = null }

    private fun ensureChannel(context: Context) {
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(
            NotificationChannel(CH_CALLS, "Calls", NotificationManager.IMPORTANCE_HIGH).apply {
                description = "Voice and video calls from the Command Center"
                setSound(null, null) // the call engine plays the ringtone itself
            })
    }

    private fun showIncomingNotification(context: Context) {
        val c = _call.value ?: return
        if (!CommsNotifications.canNotify(context)) { runCatching { context.startActivity(Intent(context, CallActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }; return }
        ensureChannel(context)
        val open = PendingIntent.getActivity(context, 10, Intent(context, CallActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val n = NotificationCompat.Builder(context, CH_CALLS)
            .setSmallIcon(R.drawable.ic_stat_xv)
            .setContentTitle("Incoming ${if (c.video) "video" else "voice"} call")
            .setContentText(c.peer)
            .setCategory(NotificationCompat.CATEGORY_CALL)
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setOngoing(true)
            .setContentIntent(open)
            .setFullScreenIntent(open, true)
            .build()
        runCatching { NotificationManagerCompat.from(context).notify(NOTIF_CALL, n) }
    }

    /** Voice calls use the earpiece mode with speaker; video calls use the loudspeaker. */
    fun audioFor(context: Context, active: Boolean, video: Boolean) {
        val am = context.getSystemService(AudioManager::class.java) ?: return
        runCatching {
            if (active) { am.mode = AudioManager.MODE_IN_COMMUNICATION; @Suppress("DEPRECATION") run { am.isSpeakerphoneOn = video } }
            else { @Suppress("DEPRECATION") run { am.isSpeakerphoneOn = false }; am.mode = AudioManager.MODE_NORMAL }
        }
    }
}
