package com.teamxv.qrmonitor.scanner

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.media.AudioManager
import android.media.ToneGenerator
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.ImageAnalysis
import androidx.camera.view.CameraController
import androidx.camera.view.LifecycleCameraController
import androidx.camera.view.PreviewView
import androidx.camera.mlkit.vision.MlKitAnalyzer
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.FlashlightOn
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

private val Ink = Color(0xFF18181B)
private val Ink2 = Color(0xFF09090B)
private val Emerald = Color(0xFF34D399)
private val Muted = Color(0xFFA1A1AA)
private val Border = Color(0xFF27272A)
private val White = Color(0xFFF4F4F5)
private val Mono = FontFamily.Monospace
private val Sans = FontFamily.SansSerif

/**
 * Production QR scanner surface. The camera/ML Kit pipeline remains the real scanner used by the
 * existing app; this composable only changes the presentation and adds a manual-code path.
 */
@Composable
fun QrScannerView(
    title: String,
    subtitle: String,
    soundEnabled: Boolean = true,
    onResult: (String) -> Unit,
    onCancel: () -> Unit
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    var hasPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED
        )
    }
    var torchOn by remember { mutableStateOf(false) }
    var manualOpen by remember { mutableStateOf(false) }
    var manualCode by remember { mutableStateOf("") }

    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
        hasPermission = it
    }

    LaunchedEffect(hasPermission) {
        if (!hasPermission) launcher.launch(Manifest.permission.CAMERA)
    }

    if (!hasPermission) {
        Column(
            Modifier
                .fillMaxSize()
                .background(Ink)
                .navigationBarsPadding()
                .padding(horizontal = 22.dp, vertical = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Box(
                Modifier
                    .size(64.dp)
                    .clip(RoundedCornerShape(17.dp))
                    .background(Color(0xFF27272A))
                    .border(1.dp, Border, RoundedCornerShape(17.dp)),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.CameraAlt, null, tint = Emerald, modifier = Modifier.size(31.dp))
            }
            Spacer(Modifier.height(16.dp))
            Text("CAMERA ACCESS REQUIRED", color = White, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(6.dp))
            Text(
                "Camera access is required to verify QR credentials on this terminal.",
                color = Muted,
                fontFamily = Sans,
                fontSize = 11.sp,
                textAlign = TextAlign.Center
            )
            Spacer(Modifier.height(18.dp))
            Button(onClick = { launcher.launch(Manifest.permission.CAMERA) }) { Text("ALLOW CAMERA") }
            Spacer(Modifier.height(8.dp))
            TextButton(onClick = onCancel) { Text("CANCEL", color = Muted, fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 10.sp) }
        }
        return
    }

    val executor = remember { Executors.newSingleThreadExecutor() }
    val scanner = remember {
        BarcodeScanning.getClient(
            BarcodeScannerOptions.Builder()
                .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
                .build()
        )
    }
    val controller = remember { LifecycleCameraController(context) }
    val locked = remember { AtomicBoolean(false) }

    DisposableEffect(lifecycleOwner) {
        controller.setEnabledUseCases(CameraController.IMAGE_ANALYSIS)
        controller.bindToLifecycle(lifecycleOwner)
        controller.setImageAnalysisAnalyzer(
            executor,
            MlKitAnalyzer(
                listOf(scanner),
                ImageAnalysis.COORDINATE_SYSTEM_VIEW_REFERENCED,
                executor
            ) { result ->
                if (locked.get()) return@MlKitAnalyzer
                val barcodeResult = result.getValue(scanner) ?: return@MlKitAnalyzer
                var raw: String? = null
                for (barcode in barcodeResult) {
                    val value = barcode.rawValue
                    if (!value.isNullOrBlank()) {
                        raw = value
                        break
                    }
                }
                val payload = raw ?: return@MlKitAnalyzer
                if (locked.compareAndSet(false, true)) {
                    if (soundEnabled) playScanTone(context)
                    ContextCompat.getMainExecutor(context).execute { onResult(payload.trim()) }
                }
            }
        )

        onDispose {
            controller.enableTorch(false)
            controller.clearImageAnalysisAnalyzer()
            controller.unbind()
            scanner.close()
            executor.shutdownNow()
        }
    }

    Box(Modifier.fillMaxSize().background(Ink2)) {
        AndroidView(
            modifier = Modifier.fillMaxSize(),
            factory = { PreviewView(it).apply { scaleType = PreviewView.ScaleType.FILL_CENTER } },
            update = { it.controller = controller }
        )

        Box(Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.48f)))

        Column(Modifier.fillMaxSize()) {
            Surface(color = Ink2.copy(alpha = .94f), modifier = Modifier.fillMaxWidth()) {
                Row(
                    Modifier.fillMaxWidth().padding(horizontal = 13.dp, vertical = 11.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    IconButton(onClick = onCancel, modifier = Modifier.size(40.dp)) {
                        Icon(Icons.Default.ArrowBack, null, tint = White, modifier = Modifier.size(21.dp))
                    }
                    Column(Modifier.weight(1f)) {
                        Text(title.uppercase(), fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = White)
                        Text("OPTICAL QR AUTHENTICATOR", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 8.sp, letterSpacing = 1.sp, color = Emerald)
                    }
                    StatusDot()
                }
            }

            Box(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                ScannerReticle()

                Text(
                    subtitle,
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .padding(bottom = 76.dp)
                        .clip(RoundedCornerShape(100.dp))
                        .background(Ink.copy(alpha = .80f))
                        .border(1.dp, Border, RoundedCornerShape(100.dp))
                        .padding(horizontal = 13.dp, vertical = 6.dp),
                    color = Color(0xFFD4D4D8),
                    fontFamily = Sans,
                    fontWeight = FontWeight.Medium,
                    fontSize = 10.sp,
                    textAlign = TextAlign.Center
                )
            }

            Surface(
                color = Ink2.copy(alpha = .96f),
                modifier = Modifier.fillMaxWidth().navigationBarsPadding(),
                border = androidx.compose.foundation.BorderStroke(1.dp, Border.copy(alpha = .85f))
            ) {
                Row(
                    Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 11.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    ScannerAction(
                        text = if (torchOn) "TORCH ON" else "TORCH",
                        icon = Icons.Default.FlashlightOn,
                        active = torchOn,
                        modifier = Modifier.weight(1f)
                    ) {
                        torchOn = !torchOn
                        controller.enableTorch(torchOn)
                    }
                    ScannerAction(
                        text = "MANUAL KEY",
                        icon = Icons.Default.Keyboard,
                        active = false,
                        modifier = Modifier.weight(1.15f)
                    ) { manualOpen = true }
                    ScannerAction(
                        text = "CANCEL",
                        icon = Icons.Default.ArrowBack,
                        active = false,
                        modifier = Modifier.weight(.9f)
                    ) { onCancel() }
                }
            }
        }

        if (manualOpen) {
            AlertDialog(
                onDismissRequest = { manualOpen = false },
                containerColor = Ink,
                title = { Text("ENTER CODE MANUALLY", color = White, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 15.sp) },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(7.dp)) {
                        Text("Enter the QR payload exactly as printed on the credential.", color = Muted, fontFamily = Sans, fontSize = 10.sp)
                        OutlinedTextField(
                            value = manualCode,
                            onValueChange = { manualCode = it.uppercase() },
                            singleLine = true,
                            placeholder = { Text("P-001 or V-001", color = Color(0xFF71717A), fontFamily = Mono, fontSize = 11.sp) },
                            textStyle = androidx.compose.ui.text.TextStyle(fontFamily = Mono, fontWeight = FontWeight.Bold, color = White, fontSize = 15.sp),
                            keyboardOptions = KeyboardOptions.Default
                        )
                    }
                },
                confirmButton = {
                    Button(
                        enabled = manualCode.trim().isNotEmpty(),
                        onClick = {
                            locked.set(true)
                            manualOpen = false
                            onResult(manualCode.trim())
                        }
                    ) { Text("VERIFY CODE") }
                },
                dismissButton = {
                    TextButton(onClick = { manualOpen = false }) { Text("CANCEL", color = Muted) }
                }
            )
        }
    }
}


private fun playScanTone(context: Context) {
    runCatching {
        val tone = ToneGenerator(AudioManager.STREAM_NOTIFICATION, 80)
        try {
            tone.startTone(ToneGenerator.TONE_PROP_BEEP, 90)
        } finally {
            tone.release()
        }
    }
}

@Composable
private fun StatusDot() {
    Box(
        Modifier
            .size(10.dp)
            .clip(CircleShape)
            .background(Emerald)
            .border(2.dp, Ink2, CircleShape)
    )
}

@Composable
private fun ScannerReticle() {
    val laserTransition = rememberInfiniteTransition(label = "scanner-laser")
    val laserFraction by laserTransition.animateFloat(
        initialValue = 0.08f,
        targetValue = 0.90f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 2000, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "laser-position"
    )
    Box(Modifier.size(274.dp)) {
        // Subtle framing HUD.
        Box(
            Modifier
                .fillMaxSize()
                .clip(RoundedCornerShape(18.dp))
                .border(1.dp, Color(0xFF3F3F46), RoundedCornerShape(18.dp))
        )
        ReticleCorner(Modifier.align(Alignment.TopStart), top = true, start = true)
        ReticleCorner(Modifier.align(Alignment.TopEnd), top = true, start = false)
        ReticleCorner(Modifier.align(Alignment.BottomStart), top = false, start = true)
        ReticleCorner(Modifier.align(Alignment.BottomEnd), top = false, start = false)
        Box(
            Modifier
                .align(Alignment.Center)
                .size(42.dp)
                .clip(CircleShape)
                .border(1.dp, Emerald.copy(alpha = .38f), CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Box(Modifier.size(5.dp).clip(CircleShape).background(Emerald))
        }
        Box(
            Modifier
                .align(Alignment.TopCenter)
                .offset(y = (laserFraction * 235f + 17f).dp)
                .width(250.dp)
                .height(2.dp)
                .background(Emerald.copy(alpha = .72f))
        )
        Text(
            "OPTICAL [QR]",
            modifier = Modifier.align(Alignment.TopStart).offset(y = (-18).dp),
            fontFamily = Mono,
            fontWeight = FontWeight.Bold,
            fontSize = 8.sp,
            letterSpacing = .9.sp,
            color = Emerald.copy(alpha = .84f)
        )
        Text(
            "AUTO-FOCUS ON",
            modifier = Modifier.align(Alignment.TopEnd).offset(y = (-18).dp),
            fontFamily = Mono,
            fontWeight = FontWeight.Bold,
            fontSize = 8.sp,
            letterSpacing = .9.sp,
            color = Emerald.copy(alpha = .84f)
        )
    }
}

@Composable
private fun ReticleCorner(
    modifier: Modifier,
    top: Boolean,
    start: Boolean
) {
    Box(modifier.size(34.dp)) {
        val shape = when {
            top && start -> RoundedCornerShape(topStart = 11.dp)
            top -> RoundedCornerShape(topEnd = 11.dp)
            start -> RoundedCornerShape(bottomStart = 11.dp)
            else -> RoundedCornerShape(bottomEnd = 11.dp)
        }
        Box(
            Modifier
                .size(29.dp)
                .align(when {
                    top && start -> Alignment.TopStart
                    top -> Alignment.TopEnd
                    start -> Alignment.BottomStart
                    else -> Alignment.BottomEnd
                })
                .border(3.dp, Emerald, shape)
        )
    }
}

@Composable
private fun ScannerAction(
    text: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    active: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    Surface(
        modifier = modifier.height(42.dp).clickable(onClick = onClick),
        color = if (active) Color(0xFFFFC107) else Color(0xFF18181B),
        contentColor = if (active) Ink else White,
        shape = RoundedCornerShape(100.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, if (active) Color(0xFFFFD54F) else Border)
    ) {
        Row(horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Icon(icon, null, modifier = Modifier.size(14.dp))
            Spacer(Modifier.width(5.dp))
            Text(text, fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 8.sp)
        }
    }
}
