package com.teamxv.qrmonitor.comms

import android.Manifest
import android.annotation.SuppressLint
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import android.webkit.JavascriptInterface
import android.webkit.PermissionRequest
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Call
import androidx.compose.material.icons.filled.CallEnd
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.webkit.WebViewAssetLoader
import kotlinx.coroutines.delay
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/** Full-screen call UI: ringing, then the shared WebRTC call page. */
class CallActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        if (Build.VERSION.SDK_INT >= 27) { setShowWhenLocked(true); setTurnScreenOn(true) }
        else {
            @Suppress("DEPRECATION")
            window.addFlags(WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON)
        }
        if (CallManager.call.value == null) { finish(); return }
        setContent { MaterialTheme { CallScreen(onClose = { finish() }) } }
    }

    override fun onDestroy() {
        CallManager.detachPage()
        if (isFinishing) {
            val c = CallManager.call.value
            if (c != null && c.phase != CallPhase.ENDED) CallManager.hangup()
            CallManager.audioFor(this, active = false, video = false)
            CallManager.clearEnded()
        }
        super.onDestroy()
    }

    @Composable
    private fun CallScreen(onClose: () -> Unit) {
        val call by CallManager.call.collectAsState()
        val c = call
        BackHandler { if (c != null && c.phase != CallPhase.ENDED) CallManager.hangup() else onClose() }
        LaunchedEffect(c?.phase) {
            if (c == null) onClose()
            else if (c.phase == CallPhase.ACTIVE || c.phase == CallPhase.RINGING_OUT) CallManager.audioFor(this@CallActivity, true, c.video)
            else if (c.phase == CallPhase.ENDED) { delay(1800); onClose() }
        }
        Box(Modifier.fillMaxSize().background(Color(0xFF09090B))) {
            when {
                c == null -> {}
                c.phase == CallPhase.RINGING_IN -> Ringing(c)
                c.phase == CallPhase.ENDED && !pageStarted -> Ended(c.message)
                else -> MediaPage(c)
            }
        }
    }

    private var pageStarted by mutableStateOf(false)

    @Composable
    private fun Ringing(c: CallUi) {
        Column(Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).padding(28.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Spacer(Modifier.weight(1f))
            Box(Modifier.size(120.dp).clip(CircleShape).background(Color(0xFF27272A)).border(2.dp, Color(0xFF059669), CircleShape), contentAlignment = Alignment.Center) {
                Icon(if (c.video) Icons.Default.Videocam else Icons.Default.Call, null, tint = Color.White, modifier = Modifier.size(52.dp))
            }
            Spacer(Modifier.height(22.dp))
            Text(if (c.video) "INCOMING VIDEO CALL" else "INCOMING VOICE CALL", color = Color(0xFF6EE7B7), fontSize = 12.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.2.sp, fontFamily = FontFamily.SansSerif)
            Spacer(Modifier.height(8.dp))
            Text(c.peer, color = Color.White, fontSize = 26.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center, fontFamily = FontFamily.SansSerif)
            Spacer(Modifier.height(8.dp))
            Text("End-to-end encrypted • direct connection", color = Color(0xFFA1A1AA), fontSize = 13.sp, fontFamily = FontFamily.SansSerif)
            Spacer(Modifier.weight(1.4f))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                RoundAction(Icons.Default.CallEnd, "Decline", Color(0xFFE11D48)) { CallManager.decline() }
                RoundAction(if (c.video) Icons.Default.Videocam else Icons.Default.Call, "Accept", Color(0xFF059669)) { CallManager.accept() }
            }
            Spacer(Modifier.height(36.dp))
        }
    }

    @Composable
    private fun RoundAction(icon: ImageVector, label: String, color: Color, onClick: () -> Unit) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            FilledIconButton(onClick = onClick, modifier = Modifier.size(76.dp), colors = IconButtonDefaults.filledIconButtonColors(containerColor = color)) {
                Icon(icon, label, tint = Color.White, modifier = Modifier.size(34.dp))
            }
            Spacer(Modifier.height(8.dp))
            Text(label, color = Color.White, fontSize = 13.sp, fontFamily = FontFamily.SansSerif)
        }
    }

    @Composable
    private fun Ended(message: String) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Text(message.ifBlank { "Call ended" }, color = Color.White, fontSize = 18.sp, textAlign = TextAlign.Center, fontFamily = FontFamily.SansSerif, modifier = Modifier.padding(24.dp))
        }
    }

    /** Asks for microphone (and camera) access, then shows the call page. Without access the call still receives the other side. */
    @Composable
    private fun MediaPage(c: CallUi) {
        var asked by remember { mutableStateOf(false) }
        val needed = remember(c.video) { listOfNotNull(Manifest.permission.RECORD_AUDIO, if (c.video) Manifest.permission.CAMERA else null) }
        val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { asked = true }
        LaunchedEffect(Unit) {
            if (needed.all { granted(it) }) asked = true else launcher.launch(needed.toTypedArray())
        }
        if (asked) CallWebView(c)
        else Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { CircularProgressIndicator(color = Color.White) }
    }

    private fun granted(p: String) = ContextCompat.checkSelfPermission(this, p) == PackageManager.PERMISSION_GRANTED

    @SuppressLint("SetJavaScriptEnabled")
    @Composable
    private fun CallWebView(c: CallUi) {
        AndroidView(modifier = Modifier.fillMaxSize(), factory = { ctx ->
            val loader = WebViewAssetLoader.Builder().addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(ctx)).build()
            WebView(ctx).apply {
                setBackgroundColor(android.graphics.Color.BLACK)
                settings.javaScriptEnabled = true
                settings.mediaPlaybackRequiresUserGesture = false
                settings.allowFileAccess = false
                settings.allowContentAccess = false
                addJavascriptInterface(Bridge(), "XVHost")
                webChromeClient = object : WebChromeClient() {
                    override fun onPermissionRequest(request: PermissionRequest) {
                        val allowed = request.resources.filter {
                            (it == PermissionRequest.RESOURCE_AUDIO_CAPTURE && granted(Manifest.permission.RECORD_AUDIO)) ||
                                (it == PermissionRequest.RESOURCE_VIDEO_CAPTURE && granted(Manifest.permission.CAMERA))
                        }
                        runOnUiThread { if (allowed.isEmpty()) request.deny() else request.grant(allowed.toTypedArray()) }
                    }
                }
                webViewClient = object : WebViewClient() {
                    override fun shouldInterceptRequest(view: WebView, request: WebResourceRequest): WebResourceResponse? = loader.shouldInterceptRequest(request.url)
                    override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest) = true // the call page never navigates
                    override fun onPageFinished(view: WebView, url: String) {
                        if (pageStarted) return
                        pageStarted = true
                        val start = buildJsonObject {
                            put("role", if (c.outgoing) "caller" else "callee"); put("video", c.video); put("peer", c.peer); put("mobile", true)
                        }
                        view.evaluateJavascript("XVCall.start($start)", null)
                        CallManager.attachPage { js -> view.evaluateJavascript("XVCall.receive($js)", null) }
                    }
                }
                loadUrl("https://appassets.androidplatform.net/assets/call/call.html")
            }
        }, onRelease = { it.destroy() })
    }

    private class Bridge {
        @JavascriptInterface
        fun send(json: String) { CallManager.onPage(json) }
    }
}
