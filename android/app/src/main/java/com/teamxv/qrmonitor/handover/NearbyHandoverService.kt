package com.teamxv.qrmonitor.handover

import android.content.Context
import android.os.Build
import com.google.android.gms.nearby.Nearby
import com.google.android.gms.nearby.connection.AdvertisingOptions
import com.google.android.gms.nearby.connection.ConnectionInfo
import com.google.android.gms.nearby.connection.ConnectionLifecycleCallback
import com.google.android.gms.nearby.connection.ConnectionResolution
import com.google.android.gms.nearby.connection.ConnectionsClient
import com.google.android.gms.nearby.connection.ConnectionsStatusCodes
import com.google.android.gms.nearby.connection.DiscoveredEndpointInfo
import com.google.android.gms.nearby.connection.DiscoveryOptions
import com.google.android.gms.nearby.connection.EndpointDiscoveryCallback
import com.google.android.gms.nearby.connection.Payload
import com.google.android.gms.nearby.connection.PayloadCallback
import com.google.android.gms.nearby.connection.PayloadTransferUpdate
import com.google.android.gms.nearby.connection.Strategy
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.File

/** Progress of one handover attempt, driven entirely off Nearby Connections' own callbacks -- never a guess. */
sealed class HandoverPhase {
    data object Idle : HandoverPhase()
    data object Advertising : HandoverPhase()
    data object Searching : HandoverPhase()
    /** Both phones show the same code; the operators confirm it matches before anything is exchanged, exactly like
     * confirming a Bluetooth pairing code -- this is what stops a third phone from grabbing the handover. */
    data class ConfirmCode(val endpointId: String, val code: String, val remoteName: String) : HandoverPhase()
    data object Connecting : HandoverPhase()
    data class Transferring(val sentBytes: Long, val totalBytes: Long) : HandoverPhase()
    data class Succeeded(val summary: String) : HandoverPhase()
    data class Failed(val reason: String) : HandoverPhase()
}

/**
 * Phone-to-phone shift handover: the outgoing operator's terminal advertises itself, the incoming operator's
 * terminal searches for it, both operators confirm a short code shown on both screens (preventing a stray third
 * device from joining), and the outgoing phone's full local snapshot (registry, who is currently inside, any
 * gate record not yet confirmed synced) is sent as a file payload. Nearby Connections picks Bluetooth or a local
 * Wi-Fi hotspot automatically depending on what both devices support -- no server or internet connection involved
 * at any point, exactly the point of a peer-to-peer handover.
 */
class NearbyHandoverService(private val context: Context) {
    private val client: ConnectionsClient = Nearby.getConnectionsClient(context)
    private val strategy = Strategy.P2P_POINT_TO_POINT
    private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    private val _phase = MutableStateFlow<HandoverPhase>(HandoverPhase.Idle)
    val phase: StateFlow<HandoverPhase> = _phase

    private var payloadToSend: HandoverPayload? = null
    private var onReceived: (suspend (HandoverPayload) -> Unit)? = null
    private val incomingFiles = HashMap<Long, File>()
    private var sendFile: File? = null
    private var myLabel: String = ""

    private fun serviceId() = context.packageName + ".handover"
    private fun deviceLabel() = Build.MODEL ?: "Terminal"

    private val connectionLifecycleCallback = object : ConnectionLifecycleCallback() {
        override fun onConnectionInitiated(endpointId: String, info: ConnectionInfo) {
            _phase.value = HandoverPhase.ConfirmCode(endpointId, info.authenticationDigits, info.endpointName)
        }

        override fun onConnectionResult(endpointId: String, result: ConnectionResolution) {
            when (result.status.statusCode) {
                ConnectionsStatusCodes.STATUS_OK -> {
                    _phase.value = HandoverPhase.Transferring(0, 0)
                    payloadToSend?.let { sendNow(endpointId, it) }
                }
                ConnectionsStatusCodes.STATUS_CONNECTION_REJECTED -> _phase.value = HandoverPhase.Failed("The other phone declined the handover code.")
                else -> _phase.value = HandoverPhase.Failed("Could not connect to the other phone.")
            }
        }

        override fun onDisconnected(endpointId: String) {
            if (_phase.value !is HandoverPhase.Succeeded && _phase.value !is HandoverPhase.Failed) {
                _phase.value = HandoverPhase.Failed("Connection to the other phone was lost.")
            }
        }
    }

    private val endpointDiscoveryCallback = object : EndpointDiscoveryCallback() {
        override fun onEndpointFound(endpointId: String, info: DiscoveredEndpointInfo) {
            client.stopDiscovery()
            client.requestConnection(myLabel.ifBlank { deviceLabel() }, endpointId, connectionLifecycleCallback)
        }

        override fun onEndpointLost(endpointId: String) { /* still searching */ }
    }

    private val payloadCallback = object : PayloadCallback() {
        override fun onPayloadReceived(endpointId: String, payload: Payload) {
            if (payload.type == Payload.Type.FILE) payload.asFile()?.asJavaFile()?.let { incomingFiles[payload.id] = it }
        }

        override fun onPayloadTransferUpdate(endpointId: String, update: PayloadTransferUpdate) {
            when (update.status) {
                PayloadTransferUpdate.Status.IN_PROGRESS -> _phase.value = HandoverPhase.Transferring(update.bytesTransferred, update.totalBytes)
                PayloadTransferUpdate.Status.SUCCESS -> {
                    val file = incomingFiles.remove(update.payloadId)
                    if (file != null) receiveComplete(file) else finishSendSide()
                }
                PayloadTransferUpdate.Status.FAILURE -> _phase.value = HandoverPhase.Failed("The transfer failed partway through. Try again with both phones closer together.")
                PayloadTransferUpdate.Status.CANCELED -> _phase.value = HandoverPhase.Failed("The transfer was canceled.")
            }
        }
    }

    private fun receiveComplete(file: File) {
        val callback = onReceived
        scope.launch {
            try {
                val payload = HandoverPayload.decode(file.readBytes())
                file.delete()
                if (callback != null) callback(payload) else _phase.value = HandoverPhase.Failed("No receiver was registered for this handover.")
            } catch (e: Exception) {
                _phase.value = HandoverPhase.Failed("The received data could not be read (${e.javaClass.simpleName}).")
            }
        }
    }

    /** Called by the ViewModel once it has applied the payload to the local database, to show the real outcome. */
    fun markReceiveApplied(summary: String) { _phase.value = HandoverPhase.Succeeded(summary) }
    fun markReceiveFailed(reason: String) { _phase.value = HandoverPhase.Failed(reason) }

    private fun finishSendSide() {
        sendFile?.delete()
        _phase.value = HandoverPhase.Succeeded("Sent to the next operator's phone.")
    }

    private fun sendNow(endpointId: String, payload: HandoverPayload) {
        val file = File.createTempFile("handover", ".json", context.cacheDir)
        file.writeBytes(HandoverPayload.encode(payload))
        sendFile = file
        client.sendPayload(endpointId, Payload.fromFile(file))
    }

    /** Outgoing operator: advertise this phone so the next operator's phone can find and connect to it.
     * [label] identifies the operator (name/post) so the other screen's confirm-code step is meaningful instead of
     * just showing a phone model. */
    fun startSending(payload: HandoverPayload, label: String = deviceLabel()) {
        payloadToSend = payload
        _phase.value = HandoverPhase.Advertising
        val options = AdvertisingOptions.Builder().setStrategy(strategy).build()
        client.startAdvertising(label.ifBlank { deviceLabel() }, serviceId(), connectionLifecycleCallback, options)
            .addOnFailureListener { _phase.value = HandoverPhase.Failed(it.message ?: "Could not start advertising this phone.") }
    }

    /** Incoming operator: search for the outgoing operator's phone. [onPayloadApplied] receives the decoded
     * payload -- the caller is responsible for merging it into the local database and then calling
     * [markReceiveApplied] or [markReceiveFailed]. */
    fun startReceiving(label: String = deviceLabel(), onPayloadApplied: suspend (HandoverPayload) -> Unit) {
        onReceived = onPayloadApplied
        myLabel = label.ifBlank { deviceLabel() }
        _phase.value = HandoverPhase.Searching
        val options = DiscoveryOptions.Builder().setStrategy(strategy).build()
        client.startDiscovery(serviceId(), endpointDiscoveryCallback, options)
            .addOnFailureListener { _phase.value = HandoverPhase.Failed(it.message ?: "Could not search for the other phone.") }
    }

    /** Called once both operators have visually compared the code on both screens and confirmed it matches. */
    fun confirmCode(endpointId: String) {
        _phase.value = HandoverPhase.Connecting
        client.acceptConnection(endpointId, payloadCallback)
    }

    fun rejectCode(endpointId: String) {
        client.rejectConnection(endpointId)
        _phase.value = HandoverPhase.Failed("Handover canceled: the code did not match, or was declined.")
    }

    fun cancel() {
        client.stopAdvertising()
        client.stopDiscovery()
        client.stopAllEndpoints()
        sendFile?.delete(); sendFile = null
        incomingFiles.values.forEach { it.delete() }; incomingFiles.clear()
        payloadToSend = null
        onReceived = null
        myLabel = ""
        _phase.value = HandoverPhase.Idle
    }

    fun dispose() {
        cancel()
        scope.cancel()
    }
}
