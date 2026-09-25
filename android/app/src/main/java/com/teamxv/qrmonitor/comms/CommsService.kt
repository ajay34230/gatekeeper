package com.teamxv.qrmonitor.comms

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.AudioAttributes
import android.media.RingtoneManager
import android.net.ConnectivityManager
import android.net.Network
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.teamxv.qrmonitor.MainActivity
import com.teamxv.qrmonitor.R
import com.teamxv.qrmonitor.config.AppConfig
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

/** Keeps the Comms engine connected while the terminal is paired, so alerts arrive even when the app is closed. */
class CommsService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var netCallback: ConnectivityManager.NetworkCallback? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        CommsNotifications.ensureChannels(this)
        goForeground(CommsNotifications.serviceNotification(this, CommsState()))
        CommsEngine.start(applicationContext)
        scope.launch {
            CommsEngine.state.collect { st ->
                if (CommsNotifications.canNotify(this@CommsService)) NotificationManagerCompat.from(this@CommsService).let { nm ->
                    @android.annotation.SuppressLint("MissingPermission") runCatching { nm.notify(CommsNotifications.SERVICE_ID, CommsNotifications.serviceNotification(this@CommsService, st)) }
                }
            }
        }
        val cm = getSystemService(ConnectivityManager::class.java)
        netCallback = object : ConnectivityManager.NetworkCallback() {
            override fun onAvailable(network: Network) = CommsEngine.nudge()
        }.also { runCatching { cm.registerDefaultNetworkCallback(it) } }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (!AppConfig(this).paired) { stopSelf(); return START_NOT_STICKY }
        CommsEngine.start(applicationContext)
        return START_STICKY
    }

    override fun onDestroy() {
        netCallback?.let { cb -> runCatching { getSystemService(ConnectivityManager::class.java).unregisterNetworkCallback(cb) } }
        CommsEngine.stop()
        scope.cancel()
        super.onDestroy()
    }

    private fun goForeground(n: Notification) {
        // Android may refuse a foreground start (started from the background, battery restrictions): then the engine
        // runs only while the app is open instead of crashing the app.
        runCatching {
            if (Build.VERSION.SDK_INT >= 34) startForeground(CommsNotifications.SERVICE_ID, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_REMOTE_MESSAGING)
            else startForeground(CommsNotifications.SERVICE_ID, n)
        }.onFailure { android.util.Log.w("XV-COMMS", "Foreground start refused", it); stopSelf() }
    }

    companion object {
        /** Starts the engine service when the terminal is paired; safe to call repeatedly. */
        fun start(context: Context) {
            if (!AppConfig(context).paired) return
            runCatching { ContextCompat.startForegroundService(context, Intent(context, CommsService::class.java)) }
                .onFailure { CommsEngine.start(context) } // background start not allowed: run while the app is open
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, CommsService::class.java))
            CommsEngine.stop()
        }
    }
}

/** Restarts the Comms engine after the phone reboots or the app is updated. */
class CommsBootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED || intent.action == Intent.ACTION_MY_PACKAGE_REPLACED) CommsService.start(context)
    }
}

@android.annotation.SuppressLint("MissingPermission") // every notify() is guarded by canNotify()
object CommsNotifications {
    const val SERVICE_ID = 4201
    private const val INCOMING_ID = 4202
    private const val CH_SERVICE = "xv_comms_service"
    private const val CH_MESSAGES = "xv_comms_messages"
    private const val CH_ALERTS = "xv_comms_alerts"
    const val EXTRA_OPEN_COMMS = "open_comms"

    fun canNotify(context: Context): Boolean = Build.VERSION.SDK_INT < 33 ||
        ContextCompat.checkSelfPermission(context, android.Manifest.permission.POST_NOTIFICATIONS) == android.content.pm.PackageManager.PERMISSION_GRANTED

    fun ensureChannels(context: Context) {
        val nm = context.getSystemService(NotificationManager::class.java)
        nm.createNotificationChannel(NotificationChannel(CH_SERVICE, "Comms connection", NotificationManager.IMPORTANCE_MIN).apply {
            description = "Shows that the terminal is listening for messages and alerts from the Command Center"
            setShowBadge(false)
        })
        nm.createNotificationChannel(NotificationChannel(CH_MESSAGES, "Messages", NotificationManager.IMPORTANCE_DEFAULT).apply {
            description = "Messages from the Command Center"
        })
        nm.createNotificationChannel(NotificationChannel(CH_ALERTS, "Alerts", NotificationManager.IMPORTANCE_HIGH).apply {
            description = "Urgent alerts from the Command Center"
            enableVibration(true)
            vibrationPattern = longArrayOf(0, 600, 250, 600, 250, 900)
            setSound(RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM),
                AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build())
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        })
    }

    private fun openApp(context: Context, request: Int): PendingIntent = PendingIntent.getActivity(
        context, request,
        Intent(context, MainActivity::class.java).putExtra(EXTRA_OPEN_COMMS, true).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP),
        PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
    )

    fun serviceNotification(context: Context, st: CommsState): Notification =
        NotificationCompat.Builder(context, CH_SERVICE)
            .setSmallIcon(R.drawable.ic_stat_xv)
            .setContentTitle("XV Comms")
            .setContentText(when (st.status) {
                "ONLINE" -> "Connected to the Command Center (${st.detail})"
                "CONNECTING" -> "Connecting to the Command Center…"
                else -> st.detail.ifBlank { "Waiting for the Command Center" }
            })
            .setOngoing(true).setSilent(true).setPriority(NotificationCompat.PRIORITY_MIN)
            .setContentIntent(openApp(context, 1))
            .build()

    fun showIncoming(context: Context, m: CommsMessageEntity) {
        val alert = m.kind == "ALERT"
        val n = NotificationCompat.Builder(context, if (alert) CH_ALERTS else CH_MESSAGES)
            .setSmallIcon(R.drawable.ic_stat_xv)
            .setContentTitle(if (alert) "⚠ ALERT — ${m.sender}" else m.sender)
            .setContentText(m.body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(m.body))
            .setCategory(if (alert) NotificationCompat.CATEGORY_ALARM else NotificationCompat.CATEGORY_MESSAGE)
            .setPriority(if (alert) NotificationCompat.PRIORITY_MAX else NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)
            .setContentIntent(openApp(context, 2))
            .apply { if (alert) setFullScreenIntent(openApp(context, 3), true) }
            .build()
        if (canNotify(context)) runCatching { NotificationManagerCompat.from(context).notify(if (alert) INCOMING_ID + 1 else INCOMING_ID, n) }
    }

    fun cancelIncoming(context: Context) {
        NotificationManagerCompat.from(context).cancel(INCOMING_ID)
        NotificationManagerCompat.from(context).cancel(INCOMING_ID + 1)
    }
}
