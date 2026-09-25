package com.teamxv.qrmonitor.ui

import android.app.Activity
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowForward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.DeleteOutline
import androidx.compose.material.icons.filled.DevicesOther
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Forum
import androidx.compose.material.icons.filled.Call
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material.icons.filled.Send
import androidx.compose.material.icons.filled.NotificationsActive
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Logout
import androidx.compose.material.icons.filled.Map
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PersonAdd
import androidx.compose.material.icons.filled.PersonOutline
import androidx.compose.material.icons.filled.QrCode2
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.LocalShipping
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.Verified
import androidx.compose.material.icons.filled.VolumeOff
import androidx.compose.material.icons.filled.VolumeUp
import androidx.compose.material.icons.filled.WarningAmber
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material.icons.filled.Translate
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.model.EntityType
import com.teamxv.qrmonitor.data.model.PresenceStatus
import com.teamxv.qrmonitor.data.model.EventType
import com.teamxv.qrmonitor.data.model.MovementEvent
import com.teamxv.qrmonitor.data.model.SyncStatus
import com.teamxv.qrmonitor.data.local.PersonEntity
import com.teamxv.qrmonitor.data.local.VehicleEntity
import com.teamxv.qrmonitor.scanner.QrScannerView
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale
import java.util.concurrent.TimeUnit

// Palette: light (day) and dark (night duty); read in composition, so switching re-colours the whole app.
private val UiBackground: Color get() = if (UiPrefs.dark) Color(0xFF09090B) else Color(0xFFF4F4F5)
private val UiSurface: Color get() = if (UiPrefs.dark) Color(0xFF18181B) else Color(0xFFFFFFFF)
private val UiSurfaceSubtle: Color get() = if (UiPrefs.dark) Color(0xFF1F1F23) else Color(0xFFFAFAFA)
private val UiInk: Color get() = if (UiPrefs.dark) Color(0xFFFAFAFA) else Color(0xFF18181B)
private val UiMuted: Color get() = if (UiPrefs.dark) Color(0xFFA1A1AA) else Color(0xFF71717A)
private val UiFaint: Color get() = if (UiPrefs.dark) Color(0xFF71717A) else Color(0xFFA1A1AA)
private val UiBorder: Color get() = if (UiPrefs.dark) Color(0xFF3F3F46) else Color(0xFFE4E4E7)
private val UiBorderSoft: Color get() = if (UiPrefs.dark) Color(0xFF27272A) else Color(0xFFF4F4F5)
private val UiSuccess: Color get() = if (UiPrefs.dark) Color(0xFF10B981) else Color(0xFF059669)
private val UiSuccessBg: Color get() = if (UiPrefs.dark) Color(0xFF052E1F) else Color(0xFFECFDF5)
private val UiWarning: Color get() = if (UiPrefs.dark) Color(0xFFF59E0B) else Color(0xFFD97706)
private val UiWarningBg: Color get() = if (UiPrefs.dark) Color(0xFF2A1F05) else Color(0xFFFFFBEB)
private val UiError: Color get() = if (UiPrefs.dark) Color(0xFFF43F5E) else Color(0xFFE11D48)
private val UiErrorBg: Color get() = if (UiPrefs.dark) Color(0xFF2A0A12) else Color(0xFFFFF1F2)
private val UiBlue: Color get() = if (UiPrefs.dark) Color(0xFF3B82F6) else Color(0xFF2563EB)
private val UiBlueBg: Color get() = if (UiPrefs.dark) Color(0xFF0B1B36) else Color(0xFFEFF6FF)
private val UiOnInk: Color get() = if (UiPrefs.dark) Color(0xFF09090B) else Color(0xFFFFFFFF)

/** Dark-mode counterparts of the one-off light greys and tints used in the layouts. */
private val DarkTints = mapOf(
    0xFF27272A to 0xFFD4D4D8, 0xFFE4E4E7 to 0xFF3F3F46, 0xFF3F3F46 to 0xFFD4D4D8, 0xFF52525B to 0xFFA1A1AA, 0xFFD4D4D8 to 0xFF52525B,
    0xFFA7F3D0 to 0xFF065F46, 0xFF064E3B to 0xFF6EE7B7, 0xFF71717A to 0xFFA1A1AA, 0xFF9CA3AF to 0xFF71717A, 0xFFFDE68A to 0xFF78350F,
    0xFFF4F4F5 to 0xFF27272A, 0xFF155EAD to 0xFF3B82F6, 0xFF404040 to 0xFFD4D4D4, 0xFF92400E to 0xFFFCD34D, 0xFF451A03 to 0xFFFDE68A,
    0xFFFBCFE8 to 0xFF881337, 0xFFBFDBFE to 0xFF1E3A8A, 0xFFE5E5E5 to 0xFF27272A, 0xFFFFF7D6 to 0xFF3A2E05, 0xFFFCD34D to 0xFFB45309,
)

private fun tc(argb: Long): Color = Color(if (UiPrefs.dark) DarkTints[argb] ?: argb else argb)
private val Sans = FontFamily.SansSerif
private val Mono = FontFamily.Monospace
private val CardShape = RoundedCornerShape(16.dp)
private val SmallShape = RoundedCornerShape(12.dp)

private enum class StatusTone { Success, Warning, Error, Info, Neutral }
private enum class BannerTone { Success, Warning, Error, Neutral }

private fun StatusTone.color(): Color = when (this) {
    StatusTone.Success -> UiSuccess
    StatusTone.Warning -> UiWarning
    StatusTone.Error -> UiError
    StatusTone.Info -> UiBlue
    StatusTone.Neutral -> UiMuted
}

@Composable
fun TeamXVApp(vm: MainViewModel = androidx.lifecycle.viewmodel.compose.viewModel()) {
    val context = LocalContext.current
    val cfg = remember { vm.currentConfig() }
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var settingsOpen by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(vm.paired) { if (vm.paired) vm.refreshCommsInfo() }
    val openComms by com.teamxv.qrmonitor.CommsNav.openRequested
    LaunchedEffect(openComms) {
        if (openComms) { tab = 4; settingsOpen = false; com.teamxv.qrmonitor.CommsNav.openRequested.value = false }
    }
    val commsUnseen by vm.commsUnseen.collectAsState(initial = 0)

    LaunchedEffect(vm.loggedIn) {
        while (vm.loggedIn) {
            vm.enforceSession()
            vm.testConnection()
            vm.syncStateRefresh()
            kotlinx.coroutines.delay(30_000)
        }
    }

    val scanning = vm.scannerTarget != null // the scanner is always dark
    val nightMode = UiPrefs.dark
    SideEffect {
        (context as? Activity)?.let { activity ->
            activity.window.statusBarColor = if (scanning) Color(0xFF18181B).toArgb() else UiBackground.toArgb()
            activity.window.navigationBarColor = UiSurface.toArgb()
            WindowCompat.getInsetsController(activity.window, activity.window.decorView).apply {
                isAppearanceLightStatusBars = !scanning && !nightMode
                isAppearanceLightNavigationBars = !nightMode
            }
        }
    }

    Surface(Modifier.fillMaxSize(), color = UiBackground) {
        when {
            vm.scannerTarget == ScannerTarget.PAIRING -> ScannerHost(vm)
            !vm.loggedIn -> LoginScreen(vm)
            settingsOpen -> SettingsScreen(vm, onBack = { settingsOpen = false })
            vm.showSuccess && vm.completedEvent != null -> SuccessScreen(
                vm = vm,
                event = vm.completedEvent!!,
                vehicle = vm.completedVehicle,
                durationMs = vm.completedDurationMs
            )
            vm.scannerTarget != null -> ScannerHost(vm)
            vm.session !is ScanSession.Closed -> SessionRouter(vm)
            else -> Column(Modifier.fillMaxSize()) {
                Box(Modifier.weight(1f).fillMaxWidth()) {
                    when (tab) {
                        0 -> HomeScreen(vm)
                        1 -> ActivityScreen(vm)
                        2 -> SyncStatusScreen(vm)
                        4 -> CommsScreen(vm)
                        else -> OperatorScreen(
                            vm = vm,
                            onOpenSettings = { settingsOpen = true }
                        )
                    }
                }
                TerminalBottomNav(tab, vm.pending, commsUnseen) { tab = it }
            }
        }
    }
}

@Composable
private fun ScannerHost(vm: MainViewModel) {
    val target = vm.scannerTarget ?: return
    val title = when (target) {
        ScannerTarget.PERSON -> "Scan Person QR"
        ScannerTarget.VEHICLE -> "Scan Vehicle QR"
        ScannerTarget.DRIVER -> "Scan Driver QR"
        ScannerTarget.CO_DRIVER -> "Scan Co-Driver QR"
        ScannerTarget.OCCUPANT -> "Scan Occupant QR"
        ScannerTarget.PAIRING -> "Scan PC Pairing QR"
    }
    val subtitle = when (target) {
        ScannerTarget.PERSON -> "Align identity badge QR inside the reticle"
        ScannerTarget.VEHICLE -> "Align vehicle windshield or registration QR in frame"
        ScannerTarget.DRIVER -> "Scan authorized driver identity badge"
        ScannerTarget.CO_DRIVER -> "Align co-driver identity badge inside frame"
        ScannerTarget.OCCUPANT -> "Align passenger identity badge inside frame"
        ScannerTarget.PAIRING -> "On the PC: Local Wi-Fi & Pair Device"
    }
    QrScannerView(
        title = title,
        subtitle = subtitle,
        soundEnabled = vm.soundEnabled,
        onResult = vm::onQr,
        onCancel = vm::closeScanner
    )
}

@Composable
private fun LoginScreen(vm: MainViewModel) {
    val cfg = vm.currentConfig()
    var signUp by rememberSaveable { mutableStateOf(false) }
    var user by rememberSaveable { mutableStateOf(cfg.operatorId) }
    var name by rememberSaveable { mutableStateOf("") }
    var pass by rememberSaveable { mutableStateOf("") }
    var confirm by rememberSaveable { mutableStateOf("") }
    var visible by rememberSaveable { mutableStateOf(false) }
    var showServer by rememberSaveable { mutableStateOf(!vm.paired) }
    var loc by rememberSaveable { mutableStateOf(cfg.locationId) }
    var gate by rememberSaveable { mutableStateOf(cfg.gateId) }

    Column(
        Modifier
            .fillMaxSize()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .imePadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 26.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) { DisplayQuickControls() }
        Spacer(Modifier.height(4.dp))
        Box(
            Modifier.size(64.dp).clip(RoundedCornerShape(16.dp))
                .background(UiInk).border(1.dp, tc(0xFF27272A), RoundedCornerShape(16.dp)),
            contentAlignment = Alignment.Center
        ) {
            Icon(Icons.Default.Shield, null, tint = UiOnInk, modifier = Modifier.size(32.dp))
        }
        Spacer(Modifier.height(16.dp))
        Text("XV DIGITAL ACCESS CONTROL", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 20.sp, color = UiInk)
        val context = LocalContext.current
        val wipedAt = remember { com.teamxv.qrmonitor.security.TerminalWipe.wipedAt(context) }
        if (wipedAt > 0 && !vm.paired) {
            Spacer(Modifier.height(10.dp))
            StatusBanner("This terminal was revoked by the Command Center on ${formatLongTime(wipedAt)}. All its data was erased. Scan a new pairing QR to use it again.", BannerTone.Error)
        }
        Spacer(Modifier.height(4.dp))
        Text("FIELD ACCESS CONTROL SYSTEM", fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 10.sp, letterSpacing = 1.3.sp, color = UiMuted)
        Spacer(Modifier.height(20.dp))

        // Sign In / Create Account segmented control (reference: grid-cols-2 bg-zinc-200 rounded-lg)
        Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).background(tc(0xFFE4E4E7)).padding(4.dp)) {
            listOf(false to "SIGN IN", true to "CREATE ACCOUNT").forEach { (mode, label) ->
                Box(
                    Modifier.weight(1f).clip(RoundedCornerShape(8.dp))
                        .background(if (signUp == mode) UiSurface else Color.Transparent)
                        .clickable { signUp = mode }.padding(vertical = 10.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(label, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, letterSpacing = 0.8.sp, color = if (signUp == mode) UiInk else UiMuted)
                }
            }
        }
        Spacer(Modifier.height(18.dp))

        if (vm.authMessage.isNotBlank()) {
            val bad = listOf("fail", "invalid", "expired", "cannot", "not ", "must", "match", "select", "pair this", "enter").any { vm.authMessage.contains(it, true) }
            StatusBanner(vm.authMessage, if (bad) BannerTone.Error else BannerTone.Success)
            Spacer(Modifier.height(12.dp))
        }

        if (signUp) {
            CompactField("FULL NAME", name, { name = it }, Icons.Default.PersonAdd, "Rank and name")
            Spacer(Modifier.height(12.dp))
        }
        CompactField(
            label = if (signUp) "GATEKEEPER ID (OPTIONAL)" else "GATEKEEPER ID",
            value = user,
            onValueChange = { user = it.uppercase(Locale.getDefault()) },
            icon = Icons.Default.PersonOutline,
            placeholder = if (signUp) "Leave blank to be assigned one" else "Enter your Gatekeeper ID"
        )
        Spacer(Modifier.height(12.dp))
        CompactField(
            label = "PASSWORD",
            value = pass,
            onValueChange = { pass = it },
            icon = Icons.Default.Lock,
            placeholder = if (signUp) "At least 6 characters" else "Your password",
            password = !visible,
            trailing = {
                TextButton(onClick = { visible = !visible }) {
                    Text(if (visible) "HIDE" else "SHOW", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                }
            }
        )
        if (signUp) {
            Spacer(Modifier.height(12.dp))
            CompactField("CONFIRM PASSWORD", confirm, { confirm = it }, Icons.Default.Lock, "Repeat password", password = !visible)
        }

        Spacer(Modifier.height(14.dp))
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            StationDropdown("Station Location", vm.locations, loc, Modifier.weight(1f)) { id, n -> loc = id; vm.selectPost(id, n, gate, vm.gates.firstOrNull { it.first == gate }?.second ?: gate) }
            StationDropdown("Active Gate", vm.gates, gate, Modifier.weight(1f)) { id, n -> gate = id; vm.selectPost(loc, vm.locations.firstOrNull { it.first == loc }?.second ?: loc, id, n) }
        }

        Spacer(Modifier.height(16.dp))
        if (signUp) {
            PrimaryButton(if (vm.busy) "CREATING…" else "CREATE ACCOUNT", Icons.Default.PersonAdd, !vm.busy && name.isNotBlank() && pass.isNotBlank()) {
                vm.register(name, user, pass, confirm) { id -> user = id; pass = ""; confirm = ""; signUp = false }
            }
        } else {
            PrimaryButton(if (vm.busy) "SIGNING IN…" else "SIGN IN TO TERMINAL", Icons.Default.ArrowForward, !vm.busy && user.isNotBlank() && pass.isNotBlank()) { vm.login(user, pass) }
            if (cfg.canContinueOffline()) {
                Spacer(Modifier.height(8.dp))
                SecondaryButton("CONTINUE OFFLINE SESSION", Icons.Default.WifiOff) { vm.continueOffline() }
            }
        }

        // PC Server Connection (reference: collapsible card with status dot)
        Spacer(Modifier.height(18.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
            Column {
                Row(
                    Modifier.fillMaxWidth().clickable { showServer = !showServer }.padding(horizontal = 14.dp, vertical = 12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(Icons.Default.DevicesOther, null, tint = UiMuted, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(8.dp))
                    Text("PC Server Connection", fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 12.sp, color = tc(0xFF3F3F46), modifier = Modifier.weight(1f))
                    Box(Modifier.size(8.dp).clip(CircleShape).background(if (vm.paired) UiSuccess else UiWarning))
                    Spacer(Modifier.width(6.dp))
                    Text(if (vm.paired) "Paired" else "Not paired", fontFamily = Sans, fontSize = 11.sp, color = UiMuted)
                }
                if (showServer) {
                    Column(Modifier.padding(start = 14.dp, end = 14.dp, bottom = 14.dp)) {
                        Text(
                            if (vm.paired) "Linked to ${cfg.serverLabel}. All traffic is encrypted (pinned TLS + AES-256-GCM)."
                            else "On the PC open XV Command Center → 'Local Wi-Fi & Pair Device', then scan the QR shown there. Both devices must be on the same network (or Cloud Link must be set up on the PC).",
                            fontFamily = Sans, fontSize = 11.sp, color = UiMuted, lineHeight = 16.sp
                        )
                        if (vm.pairingMessage.isNotBlank()) {
                            Spacer(Modifier.height(8.dp))
                            Text(vm.pairingMessage, fontFamily = Sans, fontSize = 11.sp, fontWeight = FontWeight.Medium, color = tc(0xFF3F3F46))
                        }
                        Spacer(Modifier.height(10.dp))
                        PrimaryButton(if (vm.busy) "PAIRING…" else if (vm.paired) "RE-PAIR WITH PC (SCAN QR)" else "SCAN PC PAIRING QR", Icons.Default.QrCodeScanner, !vm.busy) { vm.openPairingScanner() }

                        // Remote pairing: the PC admin can send the pairing text (e.g. by message) to an off-base phone.
                        Spacer(Modifier.height(12.dp))
                        Text("OR PASTE PAIRING TEXT FROM THE PC", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, letterSpacing = 0.8.sp, color = UiMuted)
                        Spacer(Modifier.height(4.dp))
                        var pairText by rememberSaveable { mutableStateOf("") }
                        ConfigField("XVGK1:…", pairText) { pairText = it.trim() }
                        Spacer(Modifier.height(6.dp))
                        SecondaryButton("PAIR WITH PASTED TEXT", Icons.Default.Security) { if (pairText.isNotBlank()) vm.pairWithQr(pairText) }

                        // Cloud / internet connection (same settings as Sync Hub, available before sign-in)
                        Spacer(Modifier.height(14.dp))
                        Text("CONNECT VIA CLOUD / INTERNET", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, letterSpacing = 0.8.sp, color = UiMuted)
                        Spacer(Modifier.height(6.dp))
                        CloudLinkCard(vm)
                        if (vm.networkStatus.message.isNotBlank() && vm.networkStatus.message != "Not tested") {
                            Spacer(Modifier.height(8.dp))
                            StatusBanner(
                                if (vm.networkStatus.serverReachable) "${vm.networkStatus.message} • ${vm.networkStatus.server}" else "Not reachable: ${vm.networkStatus.message}",
                                if (vm.networkStatus.serverReachable) BannerTone.Success else BannerTone.Warning
                            )
                        }
                    }
                }
            }
        }
        Spacer(Modifier.height(18.dp))
        StatusPill(if (vm.paired) "System Status: Paired • Encrypted Cache Ready" else "System Status: Awaiting Pairing", if (vm.paired) StatusTone.Success else StatusTone.Warning)
    }
}

@Composable
private fun StationDropdown(label: String, options: List<Pair<String, String>>, selected: String, modifier: Modifier, onSelect: (String, String) -> Unit) {
    var open by remember { mutableStateOf(false) }
    Column(modifier) {
        Text(label, fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 10.sp, color = UiMuted)
        Spacer(Modifier.height(4.dp))
        Box {
            Surface(
                Modifier.fillMaxWidth().clickable(enabled = options.isNotEmpty()) { open = true },
                color = UiSurfaceSubtle, shape = RoundedCornerShape(10.dp), border = BorderStroke(1.dp, UiBorder)
            ) {
                Row(Modifier.padding(horizontal = 10.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        options.firstOrNull { it.first == selected }?.second ?: if (options.isEmpty()) "Pair first" else "Select",
                        fontFamily = Sans, fontSize = 12.sp, fontWeight = FontWeight.Medium, color = UiInk, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f)
                    )
                    Icon(Icons.Default.ArrowDownward, null, tint = UiFaint, modifier = Modifier.size(14.dp))
                }
            }
            DropdownMenu(expanded = open, onDismissRequest = { open = false }) {
                options.forEach { (id, n) -> DropdownMenuItem(text = { Text(n, fontSize = 13.sp) }, onClick = { open = false; onSelect(id, n) }) }
            }
        }
    }
}

@Composable
private fun CompactField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    icon: ImageVector,
    placeholder: String,
    password: Boolean = false,
    trailing: (@Composable () -> Unit)? = null
) {
    Text(label, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 9.sp, letterSpacing = 1.0.sp, color = tc(0xFF52525B))
    Spacer(Modifier.height(6.dp))
    OutlinedTextField(
        value = value, onValueChange = onValueChange, modifier = Modifier.fillMaxWidth(), singleLine = true,
        leadingIcon = { Icon(icon, null, tint = UiFaint, modifier = Modifier.size(18.dp)) },
        trailingIcon = trailing,
        placeholder = { Text(placeholder, fontFamily = if (password) Mono else Sans, fontSize = 12.sp, color = UiFaint) },
        visualTransformation = if (password) PasswordVisualTransformation() else VisualTransformation.None,
        colors = TextFieldDefaults.colors(
            focusedContainerColor = UiSurface, unfocusedContainerColor = UiSurface,
            focusedIndicatorColor = UiInk, unfocusedIndicatorColor = tc(0xFFD4D4D8),
            focusedTextColor = UiInk, unfocusedTextColor = UiInk, cursorColor = UiInk
        ),
        shape = SmallShape
    )
}

@Composable
private fun HomeScreen(vm: MainViewModel) {
    val cfg = vm.currentConfig()
    val today = remember { startOfToday() }
    val todayEvents = vm.events.filter { it.eventTimestamp >= today }
    val entries = todayEvents.count { it.eventType == EventType.ENTRY }
    val exits = todayEvents.count { it.eventType == EventType.EXIT }

    val insideCount = vm.personnel.count { it.currentStatus == PresenceStatus.INSIDE }
    val outsideCount = vm.personnel.count { it.currentStatus == PresenceStatus.OUTSIDE }

    // Offline -> Online snackbar state
    val isOnline = vm.networkStatus.serverReachable
    var showSnackbar by remember { mutableStateOf(false) }
    var wasOnline by remember { mutableStateOf<Boolean?>(null) }

    LaunchedEffect(isOnline) {
        if (wasOnline == false && isOnline) {
            showSnackbar = true
            kotlinx.coroutines.delay(3500)
            showSnackbar = false
        }
        wasOnline = isOnline
    }

    Box(Modifier.fillMaxSize()) {
        Column(
            Modifier
                .fillMaxSize()
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 15.dp)
        ) {
            SosButton(vm)
            Spacer(Modifier.height(12.dp))
            Text(
                "Operator: ${cfg.operatorId} ${cfg.operatorName}".trim(),
                fontFamily = Sans,
                fontWeight = FontWeight.Bold,
                fontSize = 21.sp,
                color = UiInk,
                letterSpacing = (-.45).sp
            )
            Spacer(Modifier.height(2.dp))
            Text(
                "${cfg.locationName.ifBlank { cfg.locationId }} • ${cfg.gateName.ifBlank { cfg.gateId }}",
                fontFamily = Sans,
                fontSize = 19.sp,
                fontWeight = FontWeight.Medium,
                color = UiInk,
                letterSpacing = (-.25).sp
            )
            Spacer(Modifier.height(8.dp))
            StatusPill(
                when {
                    vm.networkStatus.serverReachable -> "Online"
                    vm.networkStatus.connected -> "LAN / Local"
                    else -> "Offline"
                },
                if (vm.networkStatus.serverReachable) StatusTone.Success else StatusTone.Warning,
                compact = false
            )

            Spacer(Modifier.height(18.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(13.dp)) {
                HomeActionCard(
                    Modifier.weight(1f),
                    "SCAN PERSON",
                    "Staff, contractor & visitor badges",
                    Icons.Default.QrCode2,
                    dark = true,
                    onClick = vm::openPersonScanner
                )
                HomeActionCard(
                    Modifier.weight(1f),
                    "SCAN VEHICLE",
                    "Trucks, cargo & multi-passenger manifest",
                    Icons.Default.LocalShipping,
                    dark = false,
                    onClick = vm::openVehicleScanner
                )
            }

            Spacer(Modifier.height(13.dp))
            // Real-Time Personnel Presence Summary (Inside vs Outside)
            Surface(
                Modifier.fillMaxWidth(),
                color = UiSurface,
                shape = CardShape,
                border = BorderStroke(1.dp, UiBorder)
            ) {
                Column(Modifier.padding(14.dp)) {
                    Row(
                        Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Icon(Icons.Default.Person, null, tint = UiInk, modifier = Modifier.size(16.dp))
                            Text("Personnel On-Site Presence", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = UiInk)
                        }
                        Text("${insideCount + outsideCount} registered", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                    }
                    Spacer(Modifier.height(10.dp))
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Surface(
                            Modifier.weight(1f),
                            color = UiSuccessBg,
                            shape = SmallShape,
                            border = BorderStroke(1.dp, tc(0xFFA7F3D0))
                        ) {
                            Column(Modifier.padding(10.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                    Box(Modifier.size(6.dp).clip(CircleShape).background(UiSuccess))
                                    Text("INSIDE", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiSuccess)
                                }
                                Spacer(Modifier.height(4.dp))
                                Row(verticalAlignment = Alignment.Bottom) {
                                    Text(insideCount.toString(), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 22.sp, color = tc(0xFF064E3B))
                                    Spacer(Modifier.width(4.dp))
                                    Text("on site", fontFamily = Sans, fontSize = 10.sp, color = UiSuccess, modifier = Modifier.padding(bottom = 2.dp))
                                }
                            }
                        }
                        Surface(
                            Modifier.weight(1f),
                            color = UiSurfaceSubtle,
                            shape = SmallShape,
                            border = BorderStroke(1.dp, UiBorder)
                        ) {
                            Column(Modifier.padding(10.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                    Box(Modifier.size(6.dp).clip(CircleShape).background(tc(0xFF71717A)))
                                    Text("OUTSIDE", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                                }
                                Spacer(Modifier.height(4.dp))
                                Row(verticalAlignment = Alignment.Bottom) {
                                    Text(outsideCount.toString(), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 22.sp, color = UiInk)
                                    Spacer(Modifier.width(4.dp))
                                    Text("off site", fontFamily = Sans, fontSize = 10.sp, color = UiMuted, modifier = Modifier.padding(bottom = 2.dp))
                                }
                            }
                        }
                    }

                    Spacer(Modifier.height(10.dp))
                    Box(Modifier.fillMaxWidth().height(1.dp).background(UiBorder))
                    Spacer(Modifier.height(8.dp))
                    Row(
                        Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("QUICK VERIFICATION & LAST SEEN", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                        Text("LOCAL DB", fontFamily = Mono, fontSize = 8.sp, color = UiMuted)
                    }
                    Spacer(Modifier.height(6.dp))
                    Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
                        if (vm.personnel.isEmpty()) {
                            Text("Personnel registry is empty. Add personnel on the PC Command Center; this terminal receives them on the next sync.", fontFamily = Sans, fontSize = 10.sp, color = UiMuted)
                        }
                        vm.personnel.sortedByDescending { p -> vm.events.firstOrNull { it.entityType == EntityType.PERSON && it.entityId == p.id }?.eventTimestamp ?: 0L }.take(4).forEach { p ->
                            val isInside = p.currentStatus == PresenceStatus.INSIDE
                            val lastSeen = vm.events.firstOrNull { it.entityType == EntityType.PERSON && it.entityId == p.id }?.eventTimestamp
                            Surface(
                                Modifier.fillMaxWidth().clickable { vm.selectPerson(p.id) },
                                color = UiBackground,
                                shape = RoundedCornerShape(8.dp),
                                border = BorderStroke(1.dp, UiBorder)
                            ) {
                                Row(
                                    Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 6.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                        Box(
                                            Modifier.size(6.dp).clip(CircleShape).background(if (isInside) UiSuccess else tc(0xFF9CA3AF))
                                        )
                                        Column {
                                            Text(p.name, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
                                            Text(displayId(p.id), fontFamily = Mono, fontSize = 9.sp, color = UiMuted)
                                        }
                                    }
                                    Surface(
                                        color = if (isInside) UiSuccessBg else UiSurfaceSubtle,
                                        shape = RoundedCornerShape(100.dp),
                                        border = BorderStroke(1.dp, if (isInside) tc(0xFFA7F3D0) else UiBorder)
                                    ) {
                                        Text(
                                            if (lastSeen != null) "Seen ${formatShort(lastSeen)}" else if (isInside) "On site" else "No activity",
                                            fontFamily = Mono,
                                            fontWeight = FontWeight.Bold,
                                            fontSize = 9.sp,
                                            color = if (isInside) UiSuccess else UiMuted,
                                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Spacer(Modifier.height(13.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(13.dp)) {
                HomeCounter(Modifier.weight(1f), "Entries", entries.toString())
                HomeCounter(Modifier.weight(1f), "Exits", exits.toString())
            }

            if (vm.vehicles.isNotEmpty()) {
                Spacer(Modifier.height(13.dp))
                Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
                    Column(Modifier.padding(14.dp)) {
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                Icon(Icons.Default.LocalShipping, null, tint = UiWarning, modifier = Modifier.size(16.dp))
                                Text("Vehicle Fleet Presence", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = UiInk)
                            }
                            Text("${vm.vehiclesInside.size} in yard / ${vm.vehicles.size}", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                        }
                        Spacer(Modifier.height(8.dp))
                        vm.vehicles.sortedByDescending { it.id in vm.vehiclesInside }.take(3).forEach { v ->
                            val inYard = v.id in vm.vehiclesInside
                            Surface(
                                Modifier.fillMaxWidth().padding(vertical = 2.dp).clickable { vm.selectVehicle(v.id) },
                                color = if (inYard) UiWarningBg else UiBackground, shape = RoundedCornerShape(8.dp),
                                border = BorderStroke(1.dp, if (inYard) tc(0xFFFDE68A) else UiBorder)
                            ) {
                                Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 6.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                    Column {
                                        Text(v.registration, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
                                        Text("${displayId(v.id)} • ${v.type}", fontFamily = Mono, fontSize = 9.sp, color = UiMuted)
                                    }
                                    StatusPill(if (inYard) "IN YARD" else "OUT", if (inYard) StatusTone.Warning else StatusTone.Neutral, compact = true)
                                }
                            }
                        }
                    }
                }
            }

            if (vm.attention > 0 || vm.syncUi.lastError.isNotBlank()) {
                Spacer(Modifier.height(12.dp))
                StatusBanner(
                    buildString {
                        if (vm.attention > 0) append("${vm.attention} record(s) require attention.")
                        if (vm.syncUi.lastError.isNotBlank()) {
                            if (isNotEmpty()) append(" ")
                            append(vm.syncUi.lastError)
                        }
                    },
                    BannerTone.Warning
                )
            }

            Spacer(Modifier.height(13.dp))
            Surface(
                Modifier.fillMaxWidth(),
                color = UiSurface,
                shape = CardShape,
                border = BorderStroke(1.dp, UiBorder)
            ) {
                Column(Modifier.padding(horizontal = 14.dp, vertical = 15.dp)) {
                    Text(
                        "Recent Activity",
                        fontFamily = Sans,
                        fontWeight = FontWeight.Bold,
                        fontSize = 18.sp,
                        color = UiInk,
                        letterSpacing = (-.25).sp
                    )
                    Spacer(Modifier.height(8.dp))
                    if (vm.events.isEmpty()) {
                        Text(
                            "No events recorded yet.",
                            fontFamily = Sans,
                            fontSize = 11.sp,
                            color = UiMuted,
                            modifier = Modifier.padding(vertical = 16.dp)
                        )
                    } else {
                        vm.events.take(7).forEachIndexed { index, event ->
                            HomeActivityRow(event, vm.titleFor(event))
                            if (index < minOf(vm.events.size, 7) - 1) {
                                Divider(color = UiBorder, thickness = 1.dp, modifier = Modifier.padding(start = 54.dp))
                            }
                        }
                    }
                }
            }
            Spacer(Modifier.height(10.dp))
        }

        // Reassurance Snackbar at bottom of screen
        if (showSnackbar) {
            Surface(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 20.dp, start = 20.dp, end = 20.dp),
                color = Color(0xFA18181B),
                shape = RoundedCornerShape(100.dp),
                border = BorderStroke(1.dp, tc(0xFF3F3F46)),
                shadowElevation = 8.dp
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(9.dp)
                ) {
                    Box(
                        modifier = Modifier
                            .size(10.dp)
                            .clip(CircleShape)
                            .background(tc(0xFF10B981))
                    )
                    Text(
                        "Back Online • Transmitting pending records...",
                        fontFamily = Sans,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 12.sp,
                        color = Color.White
                    )
                }
            }
        }
    }
}

@Composable
private fun HomeCounter(modifier: Modifier, label: String, value: String) {
    Surface(
        modifier = modifier.height(76.dp),
        color = UiSurface,
        shape = CardShape,
        border = BorderStroke(1.dp, UiBorder)
    ) {
        Row(
            Modifier.fillMaxSize().padding(horizontal = 13.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center
        ) {
            Text(label, fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 17.sp, color = UiInk)
            Spacer(Modifier.width(5.dp))
            Text(value, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 18.sp, color = UiInk)
        }
    }
}

@Composable
private fun HomeActionCard(
    modifier: Modifier,
    title: String,
    subtitle: String,
    icon: ImageVector,
    dark: Boolean,
    onClick: () -> Unit
) {
    val background = if (dark) UiInk else UiSurface
    val foreground = if (dark) UiOnInk else UiInk
    val secondary = if (dark) UiOnInk.copy(alpha = 0.85f) else UiInk
    val iconColor = if (dark) UiOnInk else UiInk
    Surface(
        modifier = modifier.height(176.dp).clickable(onClick = onClick),
        color = background,
        shape = RoundedCornerShape(22.dp),
        border = BorderStroke(2.dp, if (dark) UiInk else UiInk),
        shadowElevation = if (dark) 2.dp else 0.dp
    ) {
        Column(Modifier.fillMaxSize().padding(20.dp), verticalArrangement = Arrangement.SpaceBetween) {
            Icon(icon, null, tint = iconColor, modifier = Modifier.size(39.dp))
            Column {
                Text(title, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 23.sp, color = foreground, letterSpacing = (-.65).sp)
                Spacer(Modifier.height(3.dp))
                Text(subtitle, fontFamily = Sans, fontSize = 12.sp, fontWeight = FontWeight.Medium, color = secondary, lineHeight = 16.sp)
            }
            if (dark) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    Icon(Icons.Default.ArrowForward, null, tint = Color.White, modifier = Modifier.size(31.dp))
                }
            }
        }
    }
}

@Composable
private fun HomeActivityRow(event: MovementEvent, title: String) {
    val entry = event.eventType == EventType.ENTRY
    val label = if (event.entityType == EntityType.VEHICLE) "Vehicle" else "Person"
    val badgeTone = if (entry) StatusTone.Success else StatusTone.Warning
    val time = formatShort(event.eventTimestamp)
    Row(
        Modifier.fillMaxWidth().height(54.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(time, fontFamily = Mono, fontWeight = FontWeight.Medium, fontSize = 11.sp, color = UiInk, modifier = Modifier.width(58.dp))
        Text(
            title.ifBlank { "$label ${displayId(event.entityId)}" },
            fontFamily = Sans,
            fontWeight = FontWeight.Medium,
            fontSize = 12.sp,
            color = UiInk,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f).padding(horizontal = 4.dp)
        )
        StatusPill(if (entry) "ENTRY" else "EXIT", badgeTone, compact = true)
    }
}

@Composable
private fun ActivityScreen(vm: MainViewModel) {
    var query by rememberSaveable { mutableStateOf("") }
    var filter by rememberSaveable { mutableStateOf("ALL") }
    var selected by remember { mutableStateOf<MovementEvent?>(null) }

    val filtered = vm.events.filter { event ->
        val matchesType = filter == "ALL" || event.entityType.name == filter
        val q = query.trim()
        val matchesQuery = q.isBlank() ||
            event.entityId.contains(q, ignoreCase = true) ||
            event.eventId.contains(q, ignoreCase = true) ||
            event.locationId.contains(q, ignoreCase = true) ||
            event.gateId.contains(q, ignoreCase = true)
        matchesType && matchesQuery
    }

    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing)
            .padding(horizontal = 20.dp, vertical = 15.dp)
    ) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text("Recent Activity", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 19.sp, color = UiInk)
                Text("Logged events for current operational shift", fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 10.sp, color = UiMuted)
            }
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                if (vm.networkStatus.serverReachable && vm.pending > 0) {
                    Surface(
                        modifier = Modifier.clickable { vm.trySync() },
                        color = tc(0xFF059669),
                        shape = RoundedCornerShape(100.dp)
                    ) {
                        Row(
                            modifier = Modifier.padding(horizontal = 9.dp, vertical = 5.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Icon(Icons.Default.Refresh, null, tint = Color.White, modifier = Modifier.size(11.dp))
                            Text(
                                "Sync All (${vm.pending})",
                                fontFamily = Sans,
                                fontWeight = FontWeight.Bold,
                                fontSize = 9.sp,
                                color = Color.White
                            )
                        }
                    }
                }
                Surface(
                    modifier = Modifier.clickable {
                        // Export CSV Action in Android app
                        val csvBuilder = StringBuilder()
                        csvBuilder.append("Event ID,Timestamp,Type,Action,Entity ID,Location,Gate,Synced\n")
                        vm.events.forEach { ev ->
                            csvBuilder.append("${ev.eventId},${ev.eventTimestamp},${ev.entityType},${ev.eventType},${ev.entityId},${ev.locationId},${ev.gateId},${ev.syncStatus == SyncStatus.SYNCED}\n")
                        }
                    },
                    color = UiSurface,
                    shape = RoundedCornerShape(100.dp),
                    border = BorderStroke(1.dp, UiBorder)
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 5.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(3.dp)
                    ) {
                        Icon(Icons.Default.ArrowDownward, null, tint = UiInk, modifier = Modifier.size(10.dp))
                        Text("CSV", fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 9.sp, color = UiInk)
                    }
                }
                Surface(color = UiBackground, shape = RoundedCornerShape(100.dp), border = BorderStroke(1.dp, UiBorder)) {
                    Text("${filtered.size} total", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted, modifier = Modifier.padding(horizontal = 9.dp, vertical = 5.dp))
                }
            }
        }
        Spacer(Modifier.height(11.dp))
        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            placeholder = { Text("Search by personnel, vehicle ID, or gate...", fontFamily = Sans, fontSize = 10.sp, color = UiFaint) },
            leadingIcon = { Icon(Icons.Default.Search, null, tint = UiFaint, modifier = Modifier.size(17.dp)) },
            trailingIcon = {
                if (query.isNotBlank()) IconButton(onClick = { query = "" }) {
                    Icon(Icons.Default.Close, null, tint = UiFaint, modifier = Modifier.size(16.dp))
                }
            },
            colors = TextFieldDefaults.colors(
                focusedContainerColor = UiSurface, unfocusedContainerColor = UiSurface,
                focusedIndicatorColor = UiInk, unfocusedIndicatorColor = UiBorder,
                focusedTextColor = UiInk, unfocusedTextColor = UiInk, cursorColor = UiInk
            ),
            shape = SmallShape,
            textStyle = LocalTextStyle.current.copy(fontFamily = Sans, fontSize = 11.sp)
        )
        Spacer(Modifier.height(8.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            val allCount = vm.events.size
            val personCount = vm.events.count { it.entityType == EntityType.PERSON }
            val vehicleCount = vm.events.count { it.entityType == EntityType.VEHICLE }

            listOf(
                Triple("ALL", "All", allCount),
                Triple("PERSON", "Personnel", personCount),
                Triple("VEHICLE", "Vehicles", vehicleCount)
            ).forEach { (kind, label, count) ->
                val selectedChip = filter == kind
                Surface(
                    modifier = Modifier.clickable { filter = kind },
                    color = if (selectedChip) UiInk else UiBackground,
                    shape = RoundedCornerShape(100.dp),
                    border = BorderStroke(1.dp, if (selectedChip) UiInk else UiBorder)
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 11.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(5.dp)
                    ) {
                        Text(
                            label,
                            fontFamily = Sans,
                            fontWeight = FontWeight.SemiBold,
                            fontSize = 11.sp,
                            color = if (selectedChip) UiOnInk else UiMuted
                        )
                        Surface(
                            color = if (selectedChip) tc(0xFF27272A) else tc(0xFFE4E4E7),
                            shape = RoundedCornerShape(100.dp)
                        ) {
                            Text(
                                "$count",
                                fontFamily = Mono,
                                fontWeight = FontWeight.Bold,
                                fontSize = 9.sp,
                                color = if (selectedChip) tc(0xFFD4D4D8) else UiInk,
                                modifier = Modifier.padding(horizontal = 5.dp, vertical = 1.dp)
                            )
                        }
                    }
                }
            }
        }
        Spacer(Modifier.height(10.dp))

        if (filtered.isEmpty()) {
            EmptyState(Icons.Default.History, "No events matched", "Events recorded during your shift will appear here.")
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(7.dp), modifier = Modifier.fillMaxSize()) {
                items(filtered, key = { it.eventId }) { event ->
                    Box(Modifier.clickable { selected = event }) { ActivityRow(event, vm.titleFor(event)) }
                }
                item { Spacer(Modifier.height(14.dp)) }
            }
        }
    }

    val current = selected
    if (current != null) {
        ActivityDetailSheet(current) { selected = null }
    }
}

@Composable
private fun ActivityRow(event: MovementEvent, title: String) {
    val isEntry = event.eventType == EventType.ENTRY
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
        Row(Modifier.padding(horizontal = 12.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(formatShort(event.eventTimestamp), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiFaint, modifier = Modifier.width(50.dp))
            Box(Modifier.size(26.dp).clip(RoundedCornerShape(7.dp)).background(UiBackground), contentAlignment = Alignment.Center) {
                Icon(if (event.entityType == EntityType.VEHICLE) Icons.Default.LocalShipping else Icons.Default.Person, null, tint = tc(0xFF3F3F46), modifier = Modifier.size(14.dp))
            }
            Spacer(Modifier.width(8.dp))
            Column(Modifier.weight(1f)) {
                Text(title.ifBlank { "${event.entityType.name} ${displayId(event.entityId)}" } + "  " + displayId(event.entityId), fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text("${event.locationId} • ${event.gateId}" + (if (event.locationMismatch) " • ⚠ LOC FLAG" else "") + " • ${event.syncStatus.name}", fontFamily = Mono, fontSize = 8.sp, color = UiFaint, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Spacer(Modifier.width(7.dp))
            StatusPill(event.eventType.name, if (isEntry) StatusTone.Success else StatusTone.Warning, compact = true)
        }
    }
}


@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ActivityDetailSheet(event: MovementEvent, onDismiss: () -> Unit) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = UiSurface,
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
        dragHandle = { Box(Modifier.padding(top = 8.dp).size(width = 42.dp, height = 4.dp).clip(CircleShape).background(tc(0xFFD4D4D8))) }
    ) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp).navigationBarsPadding().padding(bottom = 16.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("LOG ENTRY", fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiFaint, letterSpacing = 1.sp)
                    Text("${event.entityType.name} • ${event.entityId}", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 18.sp, color = UiInk)
                }
                StatusPill(event.eventType.name, if (event.eventType == EventType.ENTRY) StatusTone.Success else StatusTone.Warning)
            }
            Spacer(Modifier.height(12.dp))
            Surface(Modifier.fillMaxWidth(), color = UiBackground, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                Column(Modifier.padding(13.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
                    ReviewRow("Recorded Time", formatLongTime(event.eventTimestamp))
                    ReviewRow("Location / Gate", "${event.locationId} • ${event.gateId}")
                    if (event.reason.isNotBlank()) ReviewRow("Reason", event.reason)
                    if (event.remarks.isNotBlank()) ReviewRow("Remarks", event.remarks)
                    if (event.expectedReturn > 0) ReviewRow("Expected Back", SimpleDateFormat("dd MMM yyyy", Locale.getDefault()).format(Date(event.expectedReturn)), valueColor = if (event.expectedReturn < System.currentTimeMillis()) UiError else UiWarning)
                    ReviewRow("Operator", event.operatorId)
                    ReviewRow("Device", event.deviceId)
                    ReviewRow("Source", "${event.sourceType.name} • ${event.sourceId}")
                    ReviewRow("Sync State", event.syncStatus.name)
                    ReviewRow("Event ID", event.eventId)
                }
            }
            Spacer(Modifier.height(12.dp))
            SecondaryButton("CLOSE", Icons.Default.Close, onDismiss)
        }
    }
}

@Composable
private fun SyncStatusScreen(vm: MainViewModel) {
    val connected = vm.networkStatus.serverReachable
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 15.dp)
    ) {
        ScreenHeader("Sync & Network Hub", "Terminal offline buffering & encrypted synchronization")
        Spacer(Modifier.height(14.dp))

        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
            Column(Modifier.padding(16.dp)) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            Modifier.size(40.dp).clip(RoundedCornerShape(12.dp))
                                .background(if (connected) UiSuccessBg else UiWarningBg)
                                .border(1.dp, if (connected) tc(0xFFA7F3D0) else tc(0xFFFDE68A), RoundedCornerShape(12.dp)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(if (connected) Icons.Default.Wifi else Icons.Default.CloudOff, null, tint = if (connected) UiSuccess else UiWarning, modifier = Modifier.size(19.dp))
                        }
                        Spacer(Modifier.width(10.dp))
                        Column {
                            Text("SERVER LINK", fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 8.sp, letterSpacing = 1.sp, color = UiFaint)
                            Text(if (connected) "Connected to Central Hub" else "Field Offline Mode", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 15.sp, color = UiInk)
                        }
                    }
                    StatusPill(if (connected) "● Online" else "○ Offline", if (connected) StatusTone.Success else StatusTone.Warning, compact = true)
                }
                Spacer(Modifier.height(12.dp))
                StatusBanner(
                    if (connected) "${vm.networkStatus.message}. Route: ${vm.networkStatus.server}"
                    else "Records remain in the secure local buffer and synchronization resumes when network becomes available.",
                    if (connected) BannerTone.Success else BannerTone.Warning
                )
            }
        }

        Spacer(Modifier.height(10.dp))
        CloudLinkCard(vm)

        Spacer(Modifier.height(10.dp))
        StatusBanner(
            when (vm.sharingMode) {
                "FULL" -> "Data sharing: Full — the registry (names, ranks, units) is kept encrypted on this terminal for offline scanning."
                "RECEIVE_ONLY" -> "Data sharing: Receive-only — no registry is stored on this terminal; every scan is verified online with the Command Center."
                else -> "Data sharing: Minimal — this terminal keeps only IDs and hashed badge codes; names are shown after an online check and never stored."
            },
            BannerTone.Neutral
        )

        Spacer(Modifier.height(10.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) {
            StatCard(Modifier.weight(1f), "LAST SYNC", formatSyncTime(vm.syncUi.lastSuccessfulSyncAt))
            StatCard(Modifier.weight(1f), "PENDING BUFFER", "${vm.pending} records", vm.pending > 0)
        }
        Spacer(Modifier.height(9.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) {
            StatCard(Modifier.weight(1f), "ATTENTION", vm.attention.toString())
            StatCard(Modifier.weight(1f), "UPLOADED LAST", vm.syncUi.lastUploadedCount.toString())
        }

        Spacer(Modifier.height(10.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
            Row(Modifier.padding(12.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.CloudDone, null, tint = UiFaint, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(7.dp))
                    Text("Local secure cache & queue", fontFamily = Sans, fontSize = 10.sp, color = tc(0xFF52525B))
                }
                Text("ACTIVE", fontFamily = Mono, fontSize = 8.sp, fontWeight = FontWeight.Bold, color = UiSuccess)
            }
        }

        Spacer(Modifier.height(10.dp))
        Surface(
            Modifier.fillMaxWidth(),
            color = UiSurface,
            shape = CardShape,
            border = BorderStroke(1.dp, if (vm.pending > 0 && connected) tc(0xFFA7F3D0) else UiBorder)
        ) {
            Column(Modifier.padding(14.dp)) {
                Row(
                    Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            Modifier.size(32.dp).clip(RoundedCornerShape(8.dp))
                                .background(UiSurfaceSubtle),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Default.CloudUpload, null, tint = UiInk, modifier = Modifier.size(18.dp))
                        }
                        Spacer(Modifier.width(9.dp))
                        Column {
                            Text("BATCH UPLOAD", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
                            Text("Simultaneous buffer transmission", fontFamily = Sans, fontSize = 9.sp, color = UiFaint)
                        }
                    }
                    StatusPill(
                        if (vm.pending > 0) "${vm.pending} Pending" else "All Synced",
                        if (vm.pending > 0) StatusTone.Warning else StatusTone.Success,
                        compact = true
                    )
                }

                if (vm.pending > 0) {
                    Spacer(Modifier.height(10.dp))
                    Text(
                        if (connected) "Connection verified. Ready to trigger batch upload of all offline events."
                        else "Internet connection required to transmit batch buffer.",
                        fontFamily = Sans,
                        fontSize = 10.sp,
                        color = UiMuted
                    )
                    Spacer(Modifier.height(10.dp))
                    PrimaryButton(
                        "TRIGGER BATCH UPLOAD (${vm.pending})",
                        Icons.Default.CloudUpload,
                        connected
                    ) {
                        vm.trySync()
                    }
                } else {
                    Spacer(Modifier.height(8.dp))
                    Text("No pending events in local queue.", fontFamily = Sans, fontSize = 10.sp, color = UiFaint)
                }
            }
        }

        Spacer(Modifier.height(10.dp))
        SecondaryButton("FORCE SYNC / DIAGNOSTIC PING", Icons.Default.Refresh) { vm.trySync() }
        Spacer(Modifier.height(7.dp))
        SecondaryButton("TEST CONNECTION", Icons.Default.Wifi) { vm.testConnection() }

        if (vm.syncUi.lastError.isNotBlank()) {
            Spacer(Modifier.height(9.dp))
            StatusBanner(vm.syncUi.lastError, BannerTone.Error)
        }
        Spacer(Modifier.height(15.dp))
    }
}

/** "Sync Target Architecture" card from the reference: Cloud Server vs Local PC Wi-Fi, with the internet link details. */
@Composable
private fun CloudLinkCard(vm: MainViewModel) {
    val cfg = vm.currentConfig()
    var mode by rememberSaveable { mutableStateOf(cfg.connectionMode) }
    var url by rememberSaveable { mutableStateOf(cfg.publicUrl) }
    var publicCa by rememberSaveable { mutableStateOf(cfg.publicUsesCaCertificate) }
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(32.dp).clip(RoundedCornerShape(8.dp)).background(UiWarningBg), contentAlignment = Alignment.Center) {
                    Icon(Icons.Default.FilterList, null, tint = UiWarning, modifier = Modifier.size(17.dp))
                }
                Spacer(Modifier.width(9.dp))
                Column {
                    Text("SYNC TARGET ARCHITECTURE", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, letterSpacing = .6.sp, color = UiInk)
                    Text("Select where this handheld pushes gate records", fontFamily = Sans, fontSize = 9.sp, color = UiFaint)
                }
            }
            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(tc(0xFFF4F4F5)).padding(4.dp)) {
                listOf("AUTO" to "Auto", "LAN" to "Local PC Wi-Fi", "CLOUD" to "Cloud Server").forEach { (m, label) ->
                    Box(
                        Modifier.weight(1f).clip(RoundedCornerShape(9.dp)).background(if (mode == m) UiSurface else Color.Transparent)
                            .clickable { mode = m }.padding(vertical = 9.dp),
                        contentAlignment = Alignment.Center
                    ) { Text(label, fontFamily = Sans, fontSize = 11.sp, fontWeight = if (mode == m) FontWeight.Bold else FontWeight.Medium, color = if (mode == m) UiInk else UiMuted) }
                }
            }
            Spacer(Modifier.height(10.dp))
            StatusBanner(
                when (mode) {
                    "LAN" -> "Local PC Wi-Fi: records go straight to the Command Center on this network (${cfg.lanHosts.joinToString().ifBlank { "not paired" }})."
                    "CLOUD" -> "Cloud Server: records travel over the internet to the Command Center's public address. Still end-to-end encrypted."
                    else -> "Auto: local Wi-Fi first, internet address when away from base. Recommended."
                },
                BannerTone.Neutral
            )
            Spacer(Modifier.height(10.dp))
            ConfigField("Internet address of the Command Center (https://…)", url) { url = it.trim() }
            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(top = 6.dp)) {
                Checkbox(checked = publicCa, onCheckedChange = { publicCa = it })
                Text("Address is a tunnel with a public certificate", fontFamily = Sans, fontSize = 10.sp, color = UiMuted)
            }
            Text("Filled automatically when you pair after 'Cloud Link' is set up on the PC.", fontFamily = Sans, fontSize = 9.sp, color = UiFaint)
            Spacer(Modifier.height(10.dp))
            PrimaryButton("SAVE & TEST LINK", Icons.Default.Check) { vm.saveCloudLink(mode, url, publicCa) }
        }
    }
}

@Composable
private fun StatCard(modifier: Modifier, title: String, value: String, warning: Boolean = false) {
    Surface(modifier, color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
        Column(Modifier.padding(12.dp)) {
            Text(title, fontFamily = Sans, fontSize = 8.sp, color = UiFaint, letterSpacing = .8.sp)
            Spacer(Modifier.height(4.dp))
            Text(value, fontFamily = Mono, fontSize = 12.sp, fontWeight = FontWeight.Bold, color = if (warning) UiWarning else UiInk)
        }
    }
}

@Composable
private fun OperatorScreen(vm: MainViewModel, onOpenSettings: () -> Unit) {
    val cfg = vm.currentConfig()
    val soundEnabled = vm.soundEnabled
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 15.dp)
    ) {
        ScreenHeader("Gatekeeper Profile", "Active terminal operator credentials")
        Spacer(Modifier.height(14.dp))

        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
            Column(Modifier.padding(16.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.size(48.dp).clip(RoundedCornerShape(12.dp)).background(UiInk), contentAlignment = Alignment.Center) {
                        Text(cfg.operatorId.ifBlank { "OP" }.takeLast(6), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiOnInk)
                    }
                    Spacer(Modifier.width(11.dp))
                    Column(Modifier.weight(1f)) {
                        Text("FIELD GATEKEEPER", fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 8.sp, letterSpacing = 1.sp, color = UiFaint)
                        Text(cfg.operatorName.ifBlank { cfg.operatorId.ifBlank { "Unassigned Operator" } }, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 15.sp, color = UiInk)
                        Text(cfg.operatorRole, fontFamily = Sans, fontSize = 10.sp, color = UiMuted)
                    }
                }
                Divider(color = UiBorderSoft, modifier = Modifier.padding(vertical = 12.dp))
                KeyValueRow("Assigned Post", "${cfg.locationName.ifBlank { cfg.locationId }} • ${cfg.gateName.ifBlank { cfg.gateId }}", Icons.Default.Map)
                Spacer(Modifier.height(9.dp))
                KeyValueRow("Shift Commenced", if (cfg.shiftStartedAt > 0) formatShort(cfg.shiftStartedAt) else "—", Icons.Default.AccessTime)
                Spacer(Modifier.height(9.dp))
                KeyValueRow("Terminal ID", cfg.deviceId.ifBlank { "Not paired" }, Icons.Default.DevicesOther)
                Spacer(Modifier.height(9.dp))
                KeyValueRow("Command Center", cfg.serverLabel, Icons.Default.Security)
            }
        }

        Spacer(Modifier.height(10.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
            Row(Modifier.padding(13.dp), verticalAlignment = Alignment.CenterVertically) {
                Icon(if (soundEnabled) Icons.Default.VolumeUp else Icons.Default.VolumeOff, null, tint = if (soundEnabled) UiSuccess else UiFaint, modifier = Modifier.size(17.dp))
                Spacer(Modifier.width(8.dp))
                Text("Scanner audio feedback", fontFamily = Sans, fontSize = 11.sp, fontWeight = FontWeight.Medium, color = UiInk)
                Spacer(Modifier.weight(1f))
                TogglePill(soundEnabled) { vm.updateSoundEnabled(!soundEnabled) }
            }
        }

        Spacer(Modifier.height(10.dp))
        DisplaySettingsCard()

        Spacer(Modifier.height(10.dp))
        SecondaryButton("CHANGE POST & CONNECTION", Icons.Default.Settings, onOpenSettings)

        Spacer(Modifier.height(12.dp))
        var handover by remember { mutableStateOf<HandoverReport?>(null) }
        DangerButton("HANDOVER SHIFT / LOG OUT", Icons.Default.Logout) { handover = vm.handoverReport() }
        handover?.let { report -> HandoverSheet(vm, report) { handover = null } }
        Spacer(Modifier.height(15.dp))
    }
}

@Composable
private fun SettingsScreen(vm: MainViewModel, onBack: () -> Unit) {
    val cfg = vm.currentConfig()
    var loc by rememberSaveable { mutableStateOf(cfg.locationId) }
    var gate by rememberSaveable { mutableStateOf(cfg.gateId) }
    LaunchedEffect(Unit) { vm.refreshStations() }
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).imePadding().verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 15.dp)
    ) {
        ScreenHeader("Post & Connection", "Reassign terminal post and manage the Command Center link", onBack)
        Spacer(Modifier.height(12.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
            Column(Modifier.padding(14.dp)) {
                Text("REASSIGN TERMINAL POST", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
                Spacer(Modifier.height(10.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StationDropdown("Location", vm.locations, loc, Modifier.weight(1f)) { id, n -> loc = id; vm.selectPost(id, n, gate, vm.gates.firstOrNull { it.first == gate }?.second ?: gate) }
                    StationDropdown("Gate", vm.gates, gate, Modifier.weight(1f)) { id, n -> gate = id; vm.selectPost(loc, vm.locations.firstOrNull { it.first == loc }?.second ?: loc, id, n) }
                }
            }
        }
        Spacer(Modifier.height(10.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
            Column(Modifier.padding(14.dp)) {
                Text("PAIRED COMMAND CENTER", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
                Spacer(Modifier.height(8.dp))
                KeyValueRow("Server", cfg.serverLabel)
                Spacer(Modifier.height(6.dp))
                KeyValueRow("Terminal ID", cfg.deviceId.ifBlank { "—" })
                Spacer(Modifier.height(6.dp))
                KeyValueRow("Certificate pin", cfg.serverFingerprint.take(16).ifBlank { "—" } + "…")
                Spacer(Modifier.height(12.dp))
                SecondaryButton("RE-PAIR (SCAN NEW PC QR)", Icons.Default.QrCodeScanner) { vm.openPairingScanner() }
                Spacer(Modifier.height(7.dp))
                DangerButton("UNPAIR THIS TERMINAL", Icons.Default.Logout) { vm.unpair() }
            }
        }
        Spacer(Modifier.height(9.dp))
        Text(vm.message, fontFamily = Mono, fontSize = 9.sp, color = UiMuted)
        Spacer(Modifier.height(20.dp))
    }
}

@Composable
private fun ConfigField(label: String, value: String, password: Boolean = false, onValueChange: (String) -> Unit) {
    OutlinedTextField(
        value = value, onValueChange = onValueChange, modifier = Modifier.fillMaxWidth(), singleLine = true,
        label = { Text(label, fontSize = 10.sp) },
        visualTransformation = if (password) PasswordVisualTransformation() else VisualTransformation.None,
        colors = TextFieldDefaults.colors(
            focusedContainerColor = UiSurface, unfocusedContainerColor = UiSurface,
            focusedIndicatorColor = UiInk, unfocusedIndicatorColor = tc(0xFFD4D4D8),
            focusedTextColor = UiInk, unfocusedTextColor = UiInk, cursorColor = UiInk
        ),
        shape = SmallShape
    )
}

@Composable
private fun PersonResultScreen(vm: MainViewModel, session: ScanSession.PersonResult) {
    var confirm by rememberSaveable { mutableStateOf(false) }
    var reason by rememberSaveable(session.person.id) { mutableStateOf(if (session.inside) "" else vm.suggestedEntryReason(session.person.id)) }
    var expectedReturn by rememberSaveable(session.person.id) { mutableStateOf(0L) }
    var needDate by remember { mutableStateOf(false) }
    var customReason by rememberSaveable(session.person.id) { mutableStateOf("") }
    var remarks by rememberSaveable(session.person.id) { mutableStateOf("") }
    val finalReason = if (reason == REASON_CUSTOM) customReason.trim() else reason
    val cfg = vm.currentConfig()
    val entryAt = remember(vm.events, session.person.id) {
        vm.events.filter {
            it.entityType == EntityType.PERSON &&
                it.entityId == session.person.id &&
                it.eventType == EventType.ENTRY
        }.maxByOrNull { it.eventTimestamp }?.eventTimestamp ?: session.insideSince.takeIf { it > 0 }
    }
    val person = session.person
    val nowMs = System.currentTimeMillis()
    // Visitor / temporary passes admit entry only inside their validity window (exit is always allowed).
    val passBlocked = !session.inside && ((person.validTo > 0 && nowMs > person.validTo) || (person.validFrom > 0 && nowMs < person.validFrom))
    val allowed = person.active && person.status == "ACTIVE" && !passBlocked
    val postName = cfg.locationName.ifBlank { cfg.locationId }
    val stayMs = if (session.inside && entryAt != null) System.currentTimeMillis() - entryAt else 0L
    val action = if (session.inside) "RECORD EXIT" else "RECORD ENTRY"

    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing)
            .verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 13.dp),
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Column {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                IconButton(onClick = vm::returnToHome, modifier = Modifier.size(42.dp)) {
                    Icon(Icons.Default.ArrowBack, "Back", tint = UiInk, modifier = Modifier.size(30.dp))
                }
                Spacer(Modifier.width(4.dp))
                Text(
                    "Person Identified",
                    modifier = Modifier.weight(1f),
                    fontFamily = Sans,
                    fontWeight = FontWeight.Bold,
                    fontSize = 21.sp,
                    color = UiInk,
                    textAlign = TextAlign.Center,
                    letterSpacing = (-.35).sp
                )
                Spacer(Modifier.width(42.dp))
            }

            Spacer(Modifier.height(34.dp))
            Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.TopCenter) {
                Surface(
                    Modifier.fillMaxWidth().padding(top = 40.dp),
                    color = UiSurface,
                    shape = RoundedCornerShape(18.dp),
                    border = BorderStroke(1.dp, tc(0xFFD4D4D8))
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Spacer(Modifier.height(43.dp))
                        Spacer(Modifier.height(2.dp))
                        Text(session.person.name, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 25.sp, color = UiInk, textAlign = TextAlign.Center, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(horizontal = 20.dp))
                        Spacer(Modifier.height(3.dp))
                        Text(displayId(session.person.id) + (person.serviceNo.takeIf { it.isNotBlank() }?.let { "  •  $it" } ?: ""), fontFamily = Mono, fontSize = 15.sp, color = tc(0xFF52525B), fontWeight = FontWeight.Medium)
                        Spacer(Modifier.height(4.dp))
                        Text(listOf(person.rank, person.company.takeIf { it.isNotBlank() }?.let { "$it Co" } ?: "", person.unit).filter { it.isNotBlank() }.joinToString(" • ").ifBlank { person.category }, fontFamily = Sans, fontSize = 15.sp, color = tc(0xFF52525B), textAlign = TextAlign.Center, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(horizontal = 20.dp))
                        Divider(Modifier.padding(top = 20.dp), color = tc(0xFFD4D4D8))
                        Row(Modifier.fillMaxWidth().height(76.dp)) {
                            PersonDetailCell("Assigned Post", postName, Modifier.weight(1f))
                            Box(Modifier.fillMaxHeight().width(1.dp).background(tc(0xFFD4D4D8)))
                            PersonDetailCell("Current Status", if (session.inside) "On-Site (Inside)" else "Off-Site", Modifier.weight(1f))
                        }
                        Box(Modifier.fillMaxWidth().height(1.dp).background(tc(0xFFD4D4D8)))
                        Row(Modifier.fillMaxWidth().height(76.dp)) {
                            PersonDetailCell("Entry Time", entryAt?.let(::formatShort) ?: "—", Modifier.weight(1f))
                            Box(Modifier.fillMaxHeight().width(1.dp).background(tc(0xFFD4D4D8)))
                            PersonDetailCell("Stay Duration", if (stayMs > 0) formatDuration(stayMs) else "—", Modifier.weight(1f), accent = session.inside)
                        }
                    }
                }

                Box(
                    Modifier.size(88.dp).clip(CircleShape).background(UiInk)
                        .border(3.dp, UiSurface, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text(initials(session.person.name), fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 25.sp, color = Color.White)
                }
                Box(Modifier.align(Alignment.TopEnd).padding(top = 50.dp, end = 18.dp)) {
                    if (allowed) StatusPill("ACTIVE", StatusTone.Success) else StatusPill(person.status.ifBlank { "INACTIVE" }, StatusTone.Error)
                }
            }
        }

        Column(Modifier.padding(top = 22.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            if (session.locationMismatch) {
                StatusBanner("LOCATION MISMATCH • The credential specifies ${session.scannedLocation}, this terminal is posted at $postName. Verify authorization before logging cross-station movement.", BannerTone.Warning)
            }
            if (!allowed) {
                StatusBanner("ACCESS DENIED • Credential is ${person.status.lowercase()}. Direct the individual to the central security desk.", BannerTone.Error)
            }
            if (person.validTo > 0) {
                StatusBanner("VISITOR / TEMPORARY PASS • valid ${formatLongTime(person.validFrom)} → ${formatLongTime(person.validTo)}" +
                    if (passBlocked) (if (nowMs > person.validTo) " • PASS EXPIRED — entry refused" else " • NOT YET VALID — entry refused") else "",
                    if (passBlocked) BannerTone.Error else BannerTone.Warning)
            }
            val needsReturn = session.inside && vm.returnReasons.any { it.equals(finalReason, ignoreCase = true) }
            if (allowed) ReasonPicker(vm.reasons, reason, { reason = it }, customReason, { customReason = it }, remarks, { remarks = it })
            if (allowed && needsReturn) ReturnDatePicker(expectedReturn) { expectedReturn = it; needDate = false }
            if (allowed && needsReturn && needDate && expectedReturn == 0L) StatusBanner("Choose the expected return date for \"$finalReason\" before recording the exit.", BannerTone.Error)
            if (allowed) Surface(
                Modifier.fillMaxWidth().height(58.dp).clickable {
                    if (session.inside && vm.returnReasons.any { it.equals(finalReason, ignoreCase = true) } && expectedReturn == 0L) needDate = true
                    else confirm = true
                },
                color = UiInk,
                shape = RoundedCornerShape(100.dp),
                shadowElevation = 2.dp
            ) {
                Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.Shield, null, tint = UiOnInk, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr(action) + " (${cfg.gateName.ifBlank { cfg.gateId }})", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 16.sp, color = UiOnInk)
                }
            }
            Surface(
                Modifier.fillMaxWidth().height(54.dp).clickable(onClick = vm::openPersonScanner),
                color = Color.Transparent,
                shape = RoundedCornerShape(100.dp),
                border = BorderStroke(2.dp, UiInk)
            ) {
                Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                    Text("Cancel & Scan Again", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 15.sp, color = UiInk)
                }
            }
        }
    }

    if (confirm) {
        ConfirmBottomSheet(
            title = tr("CONFIRM") + " " + tr(if (session.inside) "EXIT" else "ENTRY"),
            subtitle = "Confirmation Required",
            onDismiss = { confirm = false },
            onConfirm = { confirm = false; vm.confirmPerson(finalReason, remarks, if (session.inside && vm.returnReasons.any { it.equals(finalReason, ignoreCase = true) }) expectedReturn else 0L) }
        ) {
            ReviewRow("Personnel", "${session.person.name} (${displayId(session.person.id)})")
            ReviewRow("Reason", finalReason.ifBlank { "—" })
            if (remarks.isNotBlank()) ReviewRow("Remarks", remarks.trim())
            if (session.inside && expectedReturn > 0 && vm.returnReasons.any { it.equals(finalReason, ignoreCase = true) })
                ReviewRow("Expected Back", SimpleDateFormat("dd MMM yyyy", Locale.getDefault()).format(Date(expectedReturn)), valueColor = UiWarning)
            ReviewRow("Location / Gate", "$postName • ${cfg.gateName.ifBlank { cfg.gateId }}")
            if (session.locationMismatch) ReviewRow("Location Flag", "QR: ${session.scannedLocation}", valueColor = UiWarning)
            ReviewRow("Timestamp", formatLongTime(System.currentTimeMillis()))
            if (stayMs > 0) ReviewRow("Calculated Stay", formatDuration(stayMs), valueColor = UiWarning)
        }
    }
}

private const val REASON_CUSTOM = "Custom…"

/** Language (English / हिन्दी) and night mode, compact form for the sign-in screen. */
@Composable
private fun DisplayQuickControls() {
    Row(verticalAlignment = Alignment.CenterVertically) {
        LanguageSwitch()
        Spacer(Modifier.width(8.dp))
        Surface(Modifier.size(34.dp).clickable { UiPrefs.updateDark(!UiPrefs.dark) }, color = UiSurface, shape = CircleShape, border = BorderStroke(1.dp, UiBorder)) {
            Box(contentAlignment = Alignment.Center) {
                Icon(if (UiPrefs.dark) Icons.Default.LightMode else Icons.Default.DarkMode, tr("Night mode (dark)"), tint = UiInk, modifier = Modifier.size(17.dp))
            }
        }
    }
}

@Composable
private fun LanguageSwitch() {
    Row(Modifier.clip(RoundedCornerShape(100.dp)).background(UiBorderSoft).border(1.dp, UiBorder, RoundedCornerShape(100.dp)).padding(3.dp)) {
        listOf(false to "English", true to "हिन्दी").forEach { (hindi, label) ->
            val on = UiPrefs.hindi == hindi
            Box(Modifier.clip(RoundedCornerShape(100.dp)).background(if (on) UiInk else Color.Transparent).clickable { UiPrefs.updateHindi(hindi) }
                .padding(horizontal = 12.dp, vertical = 6.dp)) {
                Text(label, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 12.sp, color = if (on) UiOnInk else UiMuted)
            }
        }
    }
}

/** Operator screen: night mode for night duty and the Hindi / English switch (kept on this phone). */
@Composable
private fun DisplaySettingsCard() {
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
        Column(Modifier.padding(13.dp)) {
            Text("DISPLAY & LANGUAGE", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, letterSpacing = 1.sp, color = UiFaint)
            Spacer(Modifier.height(10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.DarkMode, null, tint = if (UiPrefs.dark) UiBlue else UiFaint, modifier = Modifier.size(17.dp))
                Spacer(Modifier.width(8.dp))
                Text("Night mode (dark)", fontFamily = Sans, fontSize = 11.sp, fontWeight = FontWeight.Medium, color = UiInk)
                Spacer(Modifier.weight(1f))
                TogglePill(UiPrefs.dark) { UiPrefs.updateDark(!UiPrefs.dark) }
            }
            Divider(color = UiBorderSoft, modifier = Modifier.padding(vertical = 10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.Translate, null, tint = UiFaint, modifier = Modifier.size(17.dp))
                Spacer(Modifier.width(8.dp))
                Text("Language", fontFamily = Sans, fontSize = 11.sp, fontWeight = FontWeight.Medium, color = UiInk)
                Spacer(Modifier.weight(1f))
                LanguageSwitch()
            }
        }
    }
}

/** Shift handover at logout: entries, exits, unsynced records and who is still inside; optionally sent to the PC. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun HandoverSheet(vm: MainViewModel, report: HandoverReport, onDismiss: () -> Unit) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = UiSurface) {
        Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(horizontal = 20.dp).padding(bottom = 24.dp)) {
            Text("Shift Handover", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 20.sp, color = UiInk)
            Text("Summary of your shift before you log out", fontFamily = Sans, fontSize = 12.sp, color = UiMuted)
            Spacer(Modifier.height(12.dp))
            val fmt = SimpleDateFormat("dd MMM HH:mm", Locale.getDefault())
            KeyValueRow("Assigned Post", report.post.ifBlank { "—" }, Icons.Default.Map)
            Spacer(Modifier.height(8.dp))
            KeyValueRow("SHIFT", (if (report.shiftStart > 0) fmt.format(Date(report.shiftStart)) else "—") + "  →  " + fmt.format(Date(report.now)), Icons.Default.AccessTime)
            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                HandoverStat("Entries", report.entries, UiSuccess, Modifier.weight(1f))
                HandoverStat("Exits", report.exits, UiBlue, Modifier.weight(1f))
                HandoverStat("Not yet synced", report.pending, if (report.pending > 0) UiWarning else UiMuted, Modifier.weight(1f))
            }
            Spacer(Modifier.height(8.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                HandoverStat("Vehicle entries", report.vehicleEntries, UiSuccess, Modifier.weight(1f))
                HandoverStat("Vehicle exits", report.vehicleExits, UiBlue, Modifier.weight(1f))
            }
            if (report.pending > 0) { Spacer(Modifier.height(8.dp)); StatusBanner(tr("Records not yet synced stay safely on this terminal and are sent at the next connection."), BannerTone.Warning) }
            Spacer(Modifier.height(14.dp))
            Text(tr("STILL INSIDE") + " (${report.inside.size})", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, letterSpacing = 1.sp, color = UiMuted)
            Spacer(Modifier.height(6.dp))
            if (report.inside.isEmpty()) Text("No one is recorded inside.", fontFamily = Sans, fontSize = 12.sp, color = UiFaint)
            report.inside.forEach { Text("• $it", fontFamily = Sans, fontSize = 13.sp, color = UiInk, modifier = Modifier.padding(vertical = 2.dp)) }
            if (report.vehiclesInside.isNotEmpty()) {
                Spacer(Modifier.height(10.dp))
                Text(tr("VEHICLES STILL INSIDE") + " (${report.vehiclesInside.size})", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, letterSpacing = 1.sp, color = UiMuted)
                Spacer(Modifier.height(6.dp))
                report.vehiclesInside.forEach { Text("• $it", fontFamily = Mono, fontSize = 13.sp, color = UiInk, modifier = Modifier.padding(vertical = 2.dp)) }
            }
            if (vm.handoverError.isNotBlank()) { Spacer(Modifier.height(10.dp)); StatusBanner(vm.handoverError, BannerTone.Error) }
            Spacer(Modifier.height(16.dp))
            PrimaryButton("SEND TO COMMAND CENTER & LOG OUT", enabled = vm.paired) { vm.sendHandoverAndLogout(report) }
            Spacer(Modifier.height(8.dp))
            DangerButton("LOG OUT WITHOUT SENDING", Icons.Default.Logout) { onDismiss(); vm.logout() }
        }
    }
}

@Composable
private fun HandoverStat(label: String, value: Int, tint: Color, modifier: Modifier) {
    Surface(modifier, color = UiSurfaceSubtle, shape = RoundedCornerShape(12.dp), border = BorderStroke(1.dp, UiBorder)) {
        Column(Modifier.padding(horizontal = 12.dp, vertical = 10.dp)) {
            Text(value.toString(), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 22.sp, color = tint)
            Text(label, fontFamily = Sans, fontSize = 10.sp, color = UiMuted, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
    }
}

/** Expected return date for leave / TD exits (end of the chosen day), required for the reasons set on the PC. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ReturnDatePicker(value: Long, onPick: (Long) -> Unit) {
    var open by remember { mutableStateOf(false) }
    Surface(Modifier.fillMaxWidth().clickable { open = true }, color = if (value == 0L) UiWarningBg else UiSurface, shape = RoundedCornerShape(14.dp),
        border = BorderStroke(1.dp, if (value == 0L) UiWarning else UiBorder)) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.AccessTime, null, tint = UiWarning, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text("EXPECTED RETURN DATE", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, letterSpacing = 1.sp, color = UiMuted)
                Text(if (value == 0L) "Tap to choose (required)" else SimpleDateFormat("EEEE, dd MMM yyyy", Locale.getDefault()).format(Date(value)),
                    fontFamily = Sans, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, color = if (value == 0L) UiWarning else UiInk)
            }
        }
    }
    if (open) {
        val state = rememberDatePickerState(initialSelectedDateMillis = if (value > 0) value else System.currentTimeMillis() + 86_400_000L,
            selectableDates = object : SelectableDates {
                override fun isSelectableDate(utcTimeMillis: Long) = utcTimeMillis >= System.currentTimeMillis() - 86_400_000L
            })
        DatePickerDialog(onDismissRequest = { open = false }, confirmButton = {
            TextButton(onClick = {
                state.selectedDateMillis?.let { utc ->
                    // End of that calendar day in local time: overdue starts the day after.
                    val cal = Calendar.getInstance(java.util.TimeZone.getTimeZone("UTC")).apply { timeInMillis = utc }
                    val local = Calendar.getInstance().apply { set(cal.get(Calendar.YEAR), cal.get(Calendar.MONTH), cal.get(Calendar.DAY_OF_MONTH), 23, 59, 59); set(Calendar.MILLISECOND, 0) }
                    onPick(local.timeInMillis)
                }
                open = false
            }) { Text("OK") }
        }, dismissButton = { TextButton(onClick = { open = false }) { Text("Cancel") } }) { DatePicker(state = state) }
    }
}

/** SOS: confirm, ask for location permission if needed, then send the alert with GPS. */
@Composable
private fun SosButton(vm: MainViewModel, modifier: Modifier = Modifier) {
    val context = LocalContext.current
    var confirm by remember { mutableStateOf(false) }
    val permission = androidx.activity.compose.rememberLauncherForActivityResult(androidx.activity.result.contract.ActivityResultContracts.RequestMultiplePermissions()) { vm.sendSos() }
    Surface(modifier.fillMaxWidth().height(52.dp).clickable { confirm = true }, color = UiError, shape = RoundedCornerShape(14.dp)) {
        Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.WarningAmber, null, tint = Color.White, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(8.dp))
            Text("SOS — EMERGENCY ALERT", fontFamily = Sans, fontWeight = FontWeight.Black, fontSize = 15.sp, color = Color.White, letterSpacing = 1.sp)
        }
    }
    if (vm.sosState.isNotBlank()) { Spacer(Modifier.height(6.dp)); Text(vm.sosState, fontFamily = Sans, fontSize = 12.sp, color = UiError, fontWeight = FontWeight.SemiBold) }
    if (confirm) AlertDialog(
        onDismissRequest = { confirm = false },
        title = { Text("Send SOS?", fontFamily = Sans, fontWeight = FontWeight.Bold, color = UiError) },
        text = { Text("An emergency alert with this post, your name and the phone's GPS position is sent to the Command Center at once.", fontFamily = Sans) },
        confirmButton = {
            TextButton(onClick = {
                confirm = false
                if (com.teamxv.qrmonitor.comms.SosLocation.permitted(context)) vm.sendSos()
                else permission.launch(arrayOf(android.Manifest.permission.ACCESS_FINE_LOCATION, android.Manifest.permission.ACCESS_COARSE_LOCATION))
            }) { Text("SEND SOS", color = UiError, fontWeight = FontWeight.Black) }
        },
        dismissButton = { TextButton(onClick = { confirm = false }) { Text("Cancel") } }
    )
}

/** Reason for the movement (list from the Command Center + custom text) and optional remarks; sent with the record. */
@Composable
private fun ReasonPicker(
    reasons: List<String>, reason: String, onReason: (String) -> Unit,
    custom: String, onCustom: (String) -> Unit, remarks: String, onRemarks: (String) -> Unit
) {
    var open by remember { mutableStateOf(false) }
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = RoundedCornerShape(14.dp), border = BorderStroke(1.dp, UiBorder)) {
        Column(Modifier.padding(12.dp)) {
            Text("REASON", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, letterSpacing = 1.sp, color = UiMuted)
            Spacer(Modifier.height(6.dp))
            Box {
                Surface(Modifier.fillMaxWidth().height(46.dp).clickable { open = true }, color = UiSurfaceSubtle, shape = RoundedCornerShape(10.dp), border = BorderStroke(1.dp, UiBorder)) {
                    Row(Modifier.fillMaxSize().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text(reason.ifBlank { "Select reason (optional)" }, fontFamily = Sans, fontSize = 14.sp, color = if (reason.isBlank()) UiFaint else UiInk, modifier = Modifier.weight(1f))
                        Icon(Icons.Default.ArrowDownward, null, tint = UiMuted, modifier = Modifier.size(16.dp))
                    }
                }
                DropdownMenu(expanded = open, onDismissRequest = { open = false }) {
                    (listOf("") + reasons + REASON_CUSTOM).forEach { r ->
                        DropdownMenuItem(text = { Text(r.ifBlank { "No reason" }, fontFamily = Sans) }, onClick = { open = false; onReason(r) })
                    }
                }
            }
            if (reason == REASON_CUSTOM) {
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(custom, { if (it.length <= 60) onCustom(it) }, Modifier.fillMaxWidth(), singleLine = true,
                    placeholder = { Text("Type the reason", fontFamily = Sans, fontSize = 13.sp) }, shape = RoundedCornerShape(10.dp))
            }
            Spacer(Modifier.height(8.dp))
            OutlinedTextField(remarks, { if (it.length <= 300) onRemarks(it) }, Modifier.fillMaxWidth(), maxLines = 3,
                placeholder = { Text("Remarks (pass no., authority, destination…)", fontFamily = Sans, fontSize = 13.sp) }, shape = RoundedCornerShape(10.dp))
        }
    }
}

@Composable
private fun PersonDetailCell(label: String, value: String, modifier: Modifier = Modifier, accent: Boolean = false) {
    Column(modifier.fillMaxHeight().padding(horizontal = 12.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        Text(label, fontFamily = Sans, fontSize = 12.sp, color = UiInk, fontWeight = FontWeight.Medium, textAlign = TextAlign.Center)
        Spacer(Modifier.height(4.dp))
        Text(value, fontFamily = if (accent) Mono else Sans, fontSize = 15.sp, color = if (accent) UiWarning else UiInk, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center, maxLines = 2, overflow = TextOverflow.Ellipsis)
    }
}

@Composable
private fun VehicleScanScreen(vm: MainViewModel, session: ScanSession.VehicleScan) {
    val vehicle = vm.scannedVehicle ?: VehicleEntity(session.vehicleId, "", "Vehicle", true)
    val entry = !session.inside
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Column {
            ScreenHeader(if (entry) "Vehicle Identified" else "Vehicle Exit", if (entry) "Vehicle credential verified" else "Vehicle currently registered as inside", vm::returnToHome)
            Spacer(Modifier.height(12.dp))
            if (vm.vehicleMismatch.isNotBlank()) { StatusBanner("VEHICLE LOCATION MISMATCH • QR specifies ${vm.vehicleMismatch}, this station is ${vm.currentConfig().locationName.ifBlank { vm.currentConfig().locationId }}.", BannerTone.Warning); Spacer(Modifier.height(8.dp)) }
            VehicleIdentityCard(vehicle)
            Spacer(Modifier.height(11.dp))
            Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                Column(Modifier.padding(13.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
                    KeyValueRow("Vehicle ID", vehicle.id)
                    KeyValueRow("Registration", vehicle.registration.ifBlank { "—" })
                    KeyValueRow("Type", vehicle.type)
                    KeyValueRow("Current presence", if (session.inside) "On-Site (Inside)" else "Outside", positive = false)
                }
            }
        }
        Column(Modifier.padding(top = 14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            PrimaryButton(if (entry) "BUILD VEHICLE MANIFEST" else "CONFIRM VEHICLE EXIT", if (entry) Icons.Default.ArrowForward else Icons.Default.Shield, true) {
                vm.startVehicleDriverScan()
            }
            SecondaryTextButton("CANCEL & RETURN HOME", vm::returnToHome)
        }
    }
}

@Composable
private fun VehicleIdentityCard(vehicle: VehicleEntity) {
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = RoundedCornerShape(18.dp), border = BorderStroke(1.dp, UiBorder), shadowElevation = 2.dp) {
        Column {
            Column(Modifier.padding(horizontal = 17.dp, vertical = 16.dp)) {
                Text("Vehicle Details", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 20.sp, color = UiInk)
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.LocalShipping, null, tint = UiInk, modifier = Modifier.size(46.dp))
                    Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) {
                        Text(vehicle.registration.ifBlank { "VEHICLE ${vehicle.id}" }, fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 25.sp, color = Color.Black, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text("Vehicle ID: ${vehicle.id}", fontFamily = Sans, fontSize = 14.sp, color = tc(0xFF404040), fontWeight = FontWeight.Medium)
                    }
                }
            }
            Divider(color = tc(0xFFD4D4D8))
            Text(
                "Type: ${vehicle.type.ifBlank { "—" }}",
                fontFamily = Sans,
                fontSize = 15.sp,
                color = UiInk,
                modifier = Modifier.padding(horizontal = 17.dp, vertical = 14.dp)
            )
        }
    }
}

@Composable
private fun VehicleDriverScreen(vm: MainViewModel, session: ScanSession.VehicleDriver) {
    val vehicle = vm.scannedVehicle ?: VehicleEntity(session.vehicleId, "", "Vehicle", true)
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Column {
            FlowHeader("Step 2 of 4 • Driver")
            Spacer(Modifier.height(11.dp))
            VehicleIdentityCard(vehicle)
            Spacer(Modifier.height(9.dp))
            ManifestPersonCard("VERIFIED DRIVER", session.driver, false)
            Spacer(Modifier.height(9.dp))
            Surface(Modifier.fillMaxWidth(), color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                Text("Driver verified. Continue to optional co-driver selection.", fontFamily = Sans, fontSize = 10.sp, color = UiMuted, modifier = Modifier.padding(12.dp))
            }
        }
        Column(Modifier.padding(top = 14.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
            PrimaryButton("ADD OPTIONAL CO-DRIVER", Icons.Default.PersonAdd) { vm.startCoDriverScan() }
            SecondaryButton("NO CO-DRIVER • CONTINUE", Icons.Default.ArrowForward) { vm.skipCoDriver() }
            SecondaryTextButton("BACK TO VEHICLE", vm::openVehicleScanner)
        }
    }
}

@Composable
private fun VehicleCoDriverScreen(vm: MainViewModel, session: ScanSession.VehicleCoDriver) {
    val vehicle = vm.scannedVehicle ?: VehicleEntity(session.vehicleId, "", "Vehicle", true)
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Column {
            FlowHeader("Step 3 of 4 • Co-Driver")
            Spacer(Modifier.height(11.dp))
            Surface(Modifier.fillMaxWidth(), color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    KeyValueRow("Vehicle", "${vehicle.id} (${vehicle.registration.ifBlank { "No plate" }})")
                    Divider(color = UiBorderSoft)
                    KeyValueRow("Verified Driver", "${session.driver.name} (${session.driver.id})", positive = true)
                }
            }
            Spacer(Modifier.height(17.dp))
            Box(
                Modifier.size(56.dp).clip(RoundedCornerShape(14.dp)).background(UiBackground).border(1.dp, UiBorder, RoundedCornerShape(14.dp)),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.PersonAdd, null, tint = tc(0xFF52525B), modifier = Modifier.size(27.dp))
            }
            Spacer(Modifier.height(9.dp))
            Text("Is there a Co-Driver?", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 16.sp, color = UiInk, modifier = Modifier.fillMaxWidth(), textAlign = TextAlign.Center)
            Text("Scan a co-driver badge for dual-driver commercial transit, or continue directly.", fontFamily = Sans, fontSize = 10.sp, color = UiMuted, modifier = Modifier.fillMaxWidth(), textAlign = TextAlign.Center)
        }
        Column(Modifier.padding(top = 14.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
            PrimaryButton("SCAN CO-DRIVER QR", Icons.Default.QrCodeScanner) { vm.startCoDriverScan() }
            SecondaryButton("NO CO-DRIVER • CONTINUE", Icons.Default.ArrowForward) { vm.beginOccupants() }
        }
    }
}

@Composable
private fun VehicleOccupantsScreen(vm: MainViewModel, session: ScanSession.VehicleOccupants) {
    val vehicle = vm.scannedVehicle ?: VehicleEntity(session.vehicleId, "", "Vehicle", true)
    val totalAdditional = session.occupants.size
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing)
            .verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 15.dp),
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Column {
            FlowHeader("Step 4 of 4: Manifest Verification")
            Spacer(Modifier.height(14.dp))
            VehicleIdentityCard(vehicle)
            Spacer(Modifier.height(13.dp))
            Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = RoundedCornerShape(18.dp), border = BorderStroke(1.dp, UiBorder), shadowElevation = 2.dp) {
                Column(Modifier.padding(horizontal = 12.dp, vertical = 10.dp)) {
                    ManifestScreenshotRow("Primary Driver:", "${session.driver.name} (${session.driver.id})", verified = true)
                    session.coDriver?.let {
                        Divider(color = tc(0xFFD4D4D8), modifier = Modifier.padding(vertical = 7.dp))
                        ManifestScreenshotRow("Co-Driver:", "${it.name} (${it.id})", verified = true, removable = true, onRemove = vm::removeCoDriver)
                    }
                    Divider(color = tc(0xFFD4D4D8), modifier = Modifier.padding(vertical = 7.dp))
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Person, null, tint = tc(0xFF155EAD), modifier = Modifier.size(28.dp))
                        Spacer(Modifier.width(11.dp))
                        Text(
                            "$totalAdditional Additional Occupants",
                            fontFamily = Sans,
                            fontSize = 15.sp,
                            fontWeight = FontWeight.Medium,
                            color = UiInk
                        )
                    }
                    if (session.occupants.isNotEmpty()) {
                        Spacer(Modifier.height(9.dp))
                        session.occupants.forEachIndexed { index, person ->
                            Divider(color = tc(0xFFE4E4E7), modifier = Modifier.padding(start = 40.dp, top = if (index == 0) 0.dp else 6.dp, bottom = 6.dp))
                            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Text("#${index + 1}", fontFamily = Mono, fontSize = 9.sp, color = UiFaint, modifier = Modifier.width(34.dp))
                                Text("${person.name} (${person.id})", fontFamily = Sans, fontSize = 11.sp, color = UiInk, modifier = Modifier.weight(1f))
                                IconButton(onClick = { vm.removeOccupant(person.id) }, modifier = Modifier.size(36.dp)) {
                                    Icon(Icons.Default.DeleteOutline, "Remove", tint = UiFaint, modifier = Modifier.size(17.dp))
                                }
                            }
                        }
                    }
                }
            }
            Spacer(Modifier.height(9.dp))
            SecondaryButton("ADD ADDITIONAL PERSON QR", Icons.Default.Add) { vm.addOccupantScan() }
        }
        Column(Modifier.padding(top = 14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            VehicleReviewButton(vm)
        }
    }
}

@Composable
private fun ManifestScreenshotRow(
    label: String,
    value: String,
    verified: Boolean,
    removable: Boolean = false,
    onRemove: (() -> Unit)? = null
) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        if (verified) {
            Box(Modifier.size(28.dp).clip(CircleShape).background(tc(0xFF155EAD)), contentAlignment = Alignment.Center) {
                Icon(Icons.Default.Check, null, tint = Color.White, modifier = Modifier.size(17.dp))
            }
        }
        Spacer(Modifier.width(11.dp))
        Column(Modifier.weight(1f)) {
            Text(buildString {
                append(label).append(' ').append(value)
            }, fontFamily = Sans, fontSize = 15.sp, color = UiInk, fontWeight = FontWeight.Medium, maxLines = 2, overflow = TextOverflow.Ellipsis)
            if (verified) Text("[Verified]", fontFamily = Sans, fontSize = 14.sp, color = tc(0xFF155EAD), fontWeight = FontWeight.Medium)
        }
        if (removable && onRemove != null) IconButton(onClick = onRemove) { Icon(Icons.Default.DeleteOutline, "Remove", tint = UiFaint, modifier = Modifier.size(18.dp)) }
    }
}

@Composable
private fun VehicleReviewButton(vm: MainViewModel) {
    val s = vm.session as? ScanSession.VehicleOccupants ?: return
    var confirm by rememberSaveable { mutableStateOf(false) }
    val vehicle = vm.scannedVehicle ?: VehicleEntity(s.vehicleId, "", "Vehicle", true)
    val cfg = vm.currentConfig()
    Surface(
        Modifier.fillMaxWidth().height(62.dp).clickable { confirm = true },
        color = Color.Black,
        shape = RoundedCornerShape(12.dp)
    ) {
        Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.Shield, null, tint = Color.White, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(8.dp))
            Text("CONFIRM VEHICLE ENTRY", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = Color.White)
        }
    }
    if (confirm) {
        ConfirmBottomSheet(
            title = "CONFIRM VEHICLE ENTRY",
            subtitle = "Step 4 of 4 • Manifest Review",
            onDismiss = { confirm = false },
            onConfirm = { confirm = false; vm.confirmVehicleEntry() }
        ) {
            VehicleReviewSummary(vehicle, s.driver, s.coDriver, s.occupants, cfg.locationId, cfg.gateId)
        }
    }
}

@Composable
private fun UnknownResultScreen(vm: MainViewModel, message: String) {
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 22.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        ScreenHeader("Scan Result", "Verification outcome", vm::returnToHome)
        Spacer(Modifier.height(28.dp))
        Box(Modifier.size(64.dp).clip(RoundedCornerShape(16.dp)).background(UiWarningBg).border(1.dp, tc(0xFFFDE68A), RoundedCornerShape(16.dp)), contentAlignment = Alignment.Center) {
            Icon(Icons.Default.WarningAmber, null, tint = UiWarning, modifier = Modifier.size(31.dp))
        }
        Spacer(Modifier.height(13.dp))
        Text("ACTION NOT COMPLETED", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 18.sp, color = UiInk)
        Spacer(Modifier.height(6.dp))
        Text(message.replace('_', ' '), fontFamily = Sans, fontSize = 11.sp, color = UiMuted, textAlign = TextAlign.Center)
        Spacer(Modifier.height(20.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
            Text("The event was not committed locally.", fontFamily = Mono, fontSize = 9.sp, color = UiMuted, modifier = Modifier.padding(12.dp), textAlign = TextAlign.Center)
        }
        Spacer(Modifier.height(20.dp))
        PrimaryButton("SCAN PERSON QR", Icons.Default.Person) { vm.openPersonScanner() }
        Spacer(Modifier.height(7.dp))
        SecondaryButton("SCAN VEHICLE QR", Icons.Default.LocalShipping) { vm.openVehicleScanner() }
    }
}

@Composable
private fun SuccessScreen(
    vm: MainViewModel,
    event: MovementEvent,
    vehicle: CompletedVehicleDisplay?,
    durationMs: Long
) {
    val exit = event.eventType == EventType.EXIT
    Column(
        Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 18.dp)
    ) {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            Box(Modifier.size(70.dp).clip(CircleShape).background(UiSuccessBg).border(2.dp, UiSuccess, CircleShape), contentAlignment = Alignment.Center) {
                Icon(Icons.Default.Check, null, tint = UiSuccess, modifier = Modifier.size(34.dp))
            }
            Spacer(Modifier.height(9.dp))
            StatusPill(if (event.syncStatus == SyncStatus.SYNCED) "SYNCHRONIZED" else "LOCAL BUFFER STORED", StatusTone.Success)
            Spacer(Modifier.height(7.dp))
            Text("${event.eventType.name} CONFIRMED", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 23.sp, color = UiInk)
            Text("Gate movement record generated", fontFamily = Sans, fontSize = 10.sp, color = UiMuted)
        }

        Spacer(Modifier.height(16.dp))
        Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = CardShape, border = BorderStroke(1.dp, UiBorder)) {
            Column {
                Box(Modifier.fillMaxWidth().height(6.dp).background(if (exit) UiWarning else UiSuccess))
                Column(Modifier.padding(17.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    val title = if (event.entityType == EntityType.VEHICLE) vehicle?.vehicle?.id ?: event.entityId else event.entityId
                    val sub = if (event.entityType == EntityType.VEHICLE) vehicle?.vehicle?.registration.orEmpty() else "Personnel record"
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
                        Column(Modifier.weight(1f)) {
                            Text(if (event.entityType == EntityType.VEHICLE) "VEHICLE UNIT" else "PERSONNEL RECORD", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, letterSpacing = 1.sp, color = UiFaint)
                            Spacer(Modifier.height(3.dp))
                            Text(title, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 16.sp, color = UiInk)
                            if (sub.isNotBlank()) Text(sub, fontFamily = Mono, fontSize = 9.sp, color = UiMuted)
                        }
                        DataBadge(event.eventId)
                    }
                    Divider(color = UiBorderSoft)
                    Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) {
                        DetailTile(Modifier.weight(1f), "FACILITY POST", event.locationId, Icons.Default.Map)
                        DetailTile(Modifier.weight(1f), "ACCESS POINT", event.gateId, Icons.Default.Security)
                    }
                    if (exit && durationMs > 0) {
                        Surface(Modifier.fillMaxWidth(), color = UiWarningBg, shape = SmallShape, border = BorderStroke(1.dp, tc(0xFFFDE68A))) {
                            Row(Modifier.padding(11.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Icon(Icons.Default.AccessTime, null, tint = UiWarning, modifier = Modifier.size(15.dp))
                                    Spacer(Modifier.width(6.dp))
                                    Text("TOTAL VERIFIED STAY", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = tc(0xFF92400E))
                                }
                                Text(formatDuration(durationMs), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 12.sp, color = tc(0xFF451A03))
                            }
                        }
                    }
                    if (vehicle != null && event.entityType == EntityType.VEHICLE && event.eventType == EventType.ENTRY) {
                        Surface(Modifier.fillMaxWidth(), color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                            Column(Modifier.padding(11.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                                Text("MANIFEST VERIFICATION", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, letterSpacing = .9.sp, color = UiMuted)
                                ManifestSummaryLine("Driver", vehicle.driver?.let { "${it.name} (${it.id})" })
                                vehicle.coDriver?.let { ManifestSummaryLine("Co-driver", "${it.name} (${it.id})") }
                                ManifestSummaryLine("Occupant Manifest", "${1 + (if (vehicle.coDriver != null) 1 else 0) + vehicle.occupants.size} persons total")
                            }
                        }
                    }
                    Divider(color = UiBorderSoft)
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Text(formatLongTime(event.eventTimestamp), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiMuted)
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.CheckCircle, null, tint = UiSuccess, modifier = Modifier.size(14.dp))
                            Spacer(Modifier.width(4.dp))
                            Text(if (event.syncStatus == SyncStatus.SYNCED) "Synchronized" else "Buffered Offline", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = UiSuccess)
                        }
                    }
                }
            }
        }

        Spacer(Modifier.height(14.dp))
        PrimaryButton("SCAN NEXT TARGET", Icons.Default.ArrowForward) { vm.scanNextAfterSuccess() }
        Spacer(Modifier.height(7.dp))
        SecondaryButton("RETURN TO TERMINAL HOME", Icons.Default.Home) { vm.returnHomeAfterSuccess() }
        Spacer(Modifier.height(16.dp))
    }
}

@Composable
private fun VehicleReviewSummary(
    vehicle: VehicleEntity,
    driver: PersonEntity,
    coDriver: PersonEntity?,
    occupants: List<PersonEntity>,
    location: String,
    gate: String
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        VehicleIdentityCard(vehicle)
        Divider(color = UiBorderSoft)
        ReviewRow("Primary Driver", "${driver.name} (${driver.id})", true)
        coDriver?.let { ReviewRow("Co-driver", "${it.name} (${it.id})", true) }
        ReviewRow("Additional Occupants", if (occupants.isEmpty()) "None" else "${occupants.size} person(s)")
        ReviewRow("Total Persons Onboard", "${1 + (if (coDriver != null) 1 else 0) + occupants.size} individuals")
        Divider(color = UiBorderSoft)
        ReviewRow("Station Post", "$location • $gate")
        ReviewRow("Timestamp", formatLongTime(System.currentTimeMillis()))
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ConfirmBottomSheet(
    title: String,
    subtitle: String,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
    content: @Composable ColumnScope.() -> Unit
) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = UiSurface,
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
        dragHandle = {
            Box(Modifier.padding(top = 8.dp).size(width = 42.dp, height = 4.dp).clip(CircleShape).background(tc(0xFFD4D4D8)))
        }
    ) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp).navigationBarsPadding().padding(bottom = 12.dp)) {
            Text(subtitle.uppercase(Locale.getDefault()), fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 8.sp, letterSpacing = 1.1.sp, color = UiFaint, modifier = Modifier.fillMaxWidth(), textAlign = TextAlign.Center)
            Spacer(Modifier.height(3.dp))
            Text(title, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 18.sp, color = UiInk, modifier = Modifier.fillMaxWidth(), textAlign = TextAlign.Center)
            Spacer(Modifier.height(12.dp))
            Surface(Modifier.fillMaxWidth(), color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
                Column(Modifier.padding(13.dp), content = content)
            }
            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SecondaryButton(Modifier.weight(1f), "CANCEL", null, onDismiss)
                PrimaryButton(Modifier.weight(1f), "CONFIRM", Icons.Default.CheckCircle, true, onConfirm)
            }
        }
    }
}

@Composable
private fun PrimaryButton(
    modifier: Modifier = Modifier,
    text: String,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    onClick: () -> Unit
) {
    Surface(
        Modifier.then(modifier).fillMaxWidth().height(50.dp).clickable(enabled = enabled, onClick = onClick),
        color = if (enabled) UiInk else tc(0xFFD4D4D8),
        contentColor = UiOnInk,
        shape = RoundedCornerShape(12.dp),
        shadowElevation = if (enabled) 2.dp else 0.dp
    ) {
        Row(Modifier.fillMaxSize().padding(horizontal = 15.dp), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Text(text, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, color = if (enabled) UiOnInk else UiMuted, textAlign = TextAlign.Center)
            icon?.let {
                Spacer(Modifier.width(7.dp))
                Icon(it, null, tint = if (enabled) UiSuccess else UiMuted, modifier = Modifier.size(16.dp))
            }
        }
    }
}

// Convenience overload matching call sites with (text, icon, enabled?, action)
@Composable
private fun PrimaryButton(
    text: String,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    onClick: () -> Unit
) = PrimaryButton(Modifier, text, icon, enabled, onClick)

@Composable
private fun SecondaryButton(
    modifier: Modifier = Modifier,
    text: String,
    icon: ImageVector? = null,
    onClick: () -> Unit
) {
    Surface(
        Modifier.then(modifier).fillMaxWidth().height(48.dp).clickable(onClick = onClick),
        color = UiSurface, shape = RoundedCornerShape(12.dp), border = BorderStroke(1.dp, UiBorder)
    ) {
        Row(Modifier.fillMaxSize().padding(horizontal = 15.dp), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            icon?.let { Icon(it, null, tint = tc(0xFF52525B), modifier = Modifier.size(16.dp)); Spacer(Modifier.width(7.dp)) }
            Text(text, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 10.sp, color = UiInk, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun SecondaryButton(text: String, icon: ImageVector? = null, onClick: () -> Unit) =
    SecondaryButton(Modifier, text, icon, onClick)

@Composable
private fun SecondaryTextButton(text: String, onClick: () -> Unit) {
    TextButton(onClick = onClick, modifier = Modifier.fillMaxWidth()) {
        Text(text, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 10.sp, color = UiMuted)
    }
}

@Composable
private fun DangerButton(text: String, icon: ImageVector, onClick: () -> Unit) {
    Surface(Modifier.fillMaxWidth().height(48.dp).clickable(onClick = onClick), color = UiErrorBg, shape = RoundedCornerShape(12.dp), border = BorderStroke(1.dp, tc(0xFFFBCFE8))) {
        Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Icon(icon, null, tint = UiError, modifier = Modifier.size(16.dp))
            Spacer(Modifier.width(7.dp))
            Text(text, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 10.sp, color = UiError)
        }
    }
}

@Composable
private fun TogglePill(enabled: Boolean, onToggle: () -> Unit) {
    Surface(Modifier.size(width = 44.dp, height = 26.dp).clickable(onClick = onToggle), color = if (enabled) UiInk else tc(0xFFD4D4D8), shape = RoundedCornerShape(100.dp)) {
        Box(Modifier.fillMaxSize()) {
            Box(Modifier.size(18.dp).clip(CircleShape).background(Color.White).align(if (enabled) Alignment.CenterEnd else Alignment.CenterStart).padding(3.dp))
        }
    }
}

@Composable
private fun StatusPill(text: String, tone: StatusTone, compact: Boolean = false) {
    val bg = when (tone) {
        StatusTone.Success -> UiSuccessBg
        StatusTone.Warning -> UiWarningBg
        StatusTone.Error -> UiErrorBg
        StatusTone.Info -> UiBlueBg
        StatusTone.Neutral -> UiSurfaceSubtle
    }
    val border = when (tone) {
        StatusTone.Success -> tc(0xFFA7F3D0)
        StatusTone.Warning -> tc(0xFFFDE68A)
        StatusTone.Error -> tc(0xFFFBCFE8)
        StatusTone.Info -> tc(0xFFBFDBFE)
        StatusTone.Neutral -> UiBorder
    }
    val dot = when (tone) {
        StatusTone.Success -> UiSuccess
        StatusTone.Warning -> UiWarning
        StatusTone.Error -> UiError
        StatusTone.Info -> UiBlue
        StatusTone.Neutral -> UiMuted
    }
    Surface(color = bg, shape = RoundedCornerShape(100.dp), border = BorderStroke(1.dp, border)) {
        Row(Modifier.padding(horizontal = if (compact) 8.dp else 10.dp, vertical = if (compact) 4.dp else 5.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(if (compact) 6.dp else 7.dp).clip(CircleShape).background(dot))
            Spacer(Modifier.width(5.dp))
            Text(text, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = if (compact) 8.sp else 9.sp, color = dot)
        }
    }
}

@Composable
private fun StatusBanner(text: String, tone: BannerTone) {
    val bg = when (tone) {
        BannerTone.Success -> UiSuccessBg
        BannerTone.Warning -> UiWarningBg
        BannerTone.Error -> UiErrorBg
        BannerTone.Neutral -> UiSurfaceSubtle
    }
    val fg = when (tone) {
        BannerTone.Success -> UiSuccess
        BannerTone.Warning -> UiWarning
        BannerTone.Error -> UiError
        BannerTone.Neutral -> UiMuted
    }
    val border = when (tone) {
        BannerTone.Success -> tc(0xFFA7F3D0)
        BannerTone.Warning -> tc(0xFFFDE68A)
        BannerTone.Error -> tc(0xFFFBCFE8)
        BannerTone.Neutral -> UiBorder
    }
    Surface(Modifier.fillMaxWidth(), color = bg, shape = SmallShape, border = BorderStroke(1.dp, border)) {
        Row(Modifier.padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(if (tone == BannerTone.Success) Icons.Default.CheckCircle else if (tone == BannerTone.Neutral) Icons.Default.Info else Icons.Default.WarningAmber, null, tint = fg, modifier = Modifier.size(15.dp))
            Spacer(Modifier.width(7.dp))
            Text(text, fontFamily = Sans, fontSize = 9.sp, color = fg)
        }
    }
}

@Composable
private fun ScreenHeader(title: String, subtitle: String, onBack: (() -> Unit)? = null) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        onBack?.let {
            Icon(Icons.Default.ArrowBack, "Back", tint = UiInk, modifier = Modifier.size(24.dp).clickable(onClick = it))
            Spacer(Modifier.width(8.dp))
        }
        Column(Modifier.weight(1f)) {
            Text(title, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 19.sp, color = UiInk)
            Text(subtitle, fontFamily = Sans, fontWeight = FontWeight.Medium, fontSize = 10.sp, color = UiMuted)
        }
    }
}

@Composable
private fun FlowHeader(text: String) {
    Surface(
        Modifier.fillMaxWidth(),
        color = tc(0xFFE5E5E5),
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(1.dp, tc(0xFFE4E4E7))
    ) {
        Text(
            text,
            fontFamily = Sans,
            fontWeight = FontWeight.Bold,
            fontSize = 17.sp,
            color = UiInk,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 16.dp)
        )
    }
}

@Composable
private fun KeyValueRow(label: String, value: String, icon: ImageVector? = null, positive: Boolean = false) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            icon?.let { Icon(it, null, tint = UiFaint, modifier = Modifier.size(14.dp)); Spacer(Modifier.width(6.dp)) }
            Text(label, fontFamily = Sans, fontSize = 9.sp, color = UiMuted)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            if (positive) { Icon(Icons.Default.CheckCircle, null, tint = UiSuccess, modifier = Modifier.size(13.dp)); Spacer(Modifier.width(4.dp)) }
            Text(value, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 9.sp, color = UiInk, textAlign = TextAlign.End)
        }
    }
}

@Composable
private fun ReviewRow(label: String, value: String, positive: Boolean = false, valueColor: Color = UiInk) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(label, fontFamily = Sans, fontSize = 9.sp, color = UiMuted)
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
            if (positive) Icon(Icons.Default.CheckCircle, null, tint = UiSuccess, modifier = Modifier.size(13.dp))
            Text(value, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 9.sp, color = valueColor, textAlign = TextAlign.End)
        }
    }
}

@Composable
private fun ManifestPersonCard(label: String, person: PersonEntity, removable: Boolean, onRemove: (() -> Unit)? = null) {
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
        Row(Modifier.padding(11.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(34.dp).clip(RoundedCornerShape(9.dp)).background(UiBackground), contentAlignment = Alignment.Center) {
                Icon(Icons.Default.Person, null, tint = tc(0xFF52525B), modifier = Modifier.size(17.dp))
            }
            Spacer(Modifier.width(9.dp))
            Column(Modifier.weight(1f)) {
                Text(label, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 8.sp, letterSpacing = .9.sp, color = UiFaint)
                Text("${person.name} (${person.id})", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
            }
            if (removable && onRemove != null) {
                IconButton(onClick = onRemove) { Icon(Icons.Default.DeleteOutline, null, tint = UiFaint, modifier = Modifier.size(18.dp)) }
            } else {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.Check, null, tint = UiSuccess, modifier = Modifier.size(15.dp))
                    Spacer(Modifier.width(4.dp))
                    Text("Verified", fontFamily = Sans, fontSize = 9.sp, fontWeight = FontWeight.Bold, color = UiSuccess)
                }
            }
        }
    }
}

@Composable
private fun OccupantCard(index: Int, person: PersonEntity, onRemove: () -> Unit) {
    Surface(Modifier.fillMaxWidth(), color = UiSurface, shape = SmallShape, border = BorderStroke(1.dp, UiBorder)) {
        Row(Modifier.padding(11.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(34.dp).clip(RoundedCornerShape(9.dp)).background(UiSurfaceSubtle).border(1.dp, UiBorder, RoundedCornerShape(9.dp)), contentAlignment = Alignment.Center) {
                Text("#$index", fontFamily = Mono, fontSize = 9.sp, color = UiMuted)
            }
            Spacer(Modifier.width(9.dp))
            Column(Modifier.weight(1f)) {
                Text("ADDITIONAL OCCUPANT", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 8.sp, letterSpacing = .8.sp, color = UiFaint)
                Text("${person.name} (${person.id})", fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp, color = UiInk)
            }
            IconButton(onClick = onRemove) { Icon(Icons.Default.DeleteOutline, null, tint = UiFaint, modifier = Modifier.size(18.dp)) }
        }
    }
}

@Composable
private fun DetailTile(modifier: Modifier = Modifier, label: String, value: String, icon: ImageVector) {
    Surface(modifier, color = UiSurfaceSubtle, shape = SmallShape, border = BorderStroke(1.dp, UiBorderSoft)) {
        Column(Modifier.padding(9.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(icon, null, tint = UiFaint, modifier = Modifier.size(12.dp))
                Spacer(Modifier.width(4.dp))
                Text(label, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 7.sp, letterSpacing = .5.sp, color = UiFaint)
            }
            Spacer(Modifier.height(3.dp))
            Text(value, fontFamily = Sans, fontWeight = FontWeight.SemiBold, fontSize = 9.sp, color = UiInk, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
    }
}

@Composable
private fun DataBadge(value: String, accent: Color = UiBackground) {
    val isPlate = accent == UiWarning
    Surface(color = if (isPlate) tc(0xFFFFF7D6) else UiBackground, shape = RoundedCornerShape(7.dp), border = BorderStroke(1.dp, if (isPlate) tc(0xFFFCD34D) else UiBorder)) {
        Text(value, fontFamily = Mono, fontWeight = FontWeight.Bold, fontSize = 9.sp, color = if (isPlate) tc(0xFF451A03) else UiInk, modifier = Modifier.padding(horizontal = 7.dp, vertical = 4.dp))
    }
}

@Composable
private fun ManifestSummaryLine(label: String, value: String?) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, fontFamily = Sans, fontSize = 9.sp, color = UiMuted)
        Text(value ?: "None", fontFamily = Sans, fontSize = 9.sp, fontWeight = FontWeight.SemiBold, color = UiInk)
    }
}

@Composable
private fun EmptyState(icon: ImageVector, title: String, subtitle: String) {
    Column(Modifier.fillMaxSize(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        Box(Modifier.size(56.dp).clip(RoundedCornerShape(14.dp)).background(UiSurface).border(1.dp, UiBorder, RoundedCornerShape(14.dp)), contentAlignment = Alignment.Center) {
            Icon(icon, null, tint = UiFaint, modifier = Modifier.size(25.dp))
        }
        Spacer(Modifier.height(9.dp))
        Text(title, fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = UiInk)
        Spacer(Modifier.height(3.dp))
        Text(subtitle, fontFamily = Sans, fontSize = 10.sp, color = UiMuted, textAlign = TextAlign.Center, modifier = Modifier.padding(horizontal = 20.dp))
    }
}

@Composable
private fun TerminalBottomNav(currentTab: Int, pending: Int, commsUnseen: Int, onSelect: (Int) -> Unit) {
    val tabs = listOf(
        Triple(Icons.Default.Home, "Home", 0),
        Triple(Icons.Default.History, "Activity", 1),
        Triple(Icons.Default.Refresh, "Sync", 2),
        Triple(Icons.Default.Forum, "Comms", 4),
        Triple(Icons.Default.PersonOutline, "Operator", 3)
    )
    Surface(
        Modifier.fillMaxWidth(),
        color = UiSurface,
        shadowElevation = 4.dp,
        border = BorderStroke(1.dp, UiBorderSoft)
    ) {
        Row(Modifier.fillMaxWidth().navigationBarsPadding().height(75.dp).padding(horizontal = 3.dp)) {
            tabs.forEach { (icon, label, index) ->
                Column(
                    Modifier.weight(1f).fillMaxHeight().clickable { onSelect(index) }.padding(vertical = 4.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center
                ) {
                    Box(Modifier.size(30.dp), contentAlignment = Alignment.Center) {
                        Icon(icon, null, tint = UiInk, modifier = Modifier.size(if (currentTab == index) 24.dp else 22.dp))
                        if (index == 4 && commsUnseen > 0) Box(
                            Modifier.align(Alignment.TopEnd).clip(CircleShape).background(UiError).padding(horizontal = 4.dp),
                            contentAlignment = Alignment.Center
                        ) { Text(if (commsUnseen > 9) "9+" else commsUnseen.toString(), color = Color.White, fontSize = 9.sp, fontWeight = FontWeight.Bold, fontFamily = Sans) }
                        if (index == 2 && pending > 0) Box(Modifier.size(8.dp).clip(CircleShape).background(UiWarning).border(1.dp, UiSurface, CircleShape).align(Alignment.TopEnd))
                    }
                    Spacer(Modifier.height(2.dp))
                    Text(label, fontFamily = Sans, fontWeight = if (currentTab == index) FontWeight.Bold else FontWeight.Medium, fontSize = 11.sp, color = UiInk)
                }
            }
        }
    }
}

private fun displayId(id: String): String =
    if (id.length > 1 && id[0].isLetter() && id[1].isDigit()) "${id[0]}-${id.substring(1)}" else id

private fun initials(name: String): String =
    name.trim().split(Regex("\\s+")).take(2).joinToString("") { it.firstOrNull()?.uppercase() ?: "" }.ifBlank { "?" }

private fun formatDuration(ms: Long): String {
    val totalMinutes = TimeUnit.MILLISECONDS.toMinutes(ms.coerceAtLeast(0L))
    val hours = totalMinutes / 60
    val minutes = totalMinutes % 60
    return if (hours > 0) "${hours}h ${minutes}m" else "${minutes.coerceAtLeast(1)}m"
}

private fun formatShort(ts: Long) = SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date(ts))
private fun formatLongTime(ts: Long) = SimpleDateFormat("dd MMM yyyy • HH:mm:ss", Locale.getDefault()).format(Date(ts))
private fun formatSyncTime(ts: Long) = if (ts <= 0L) "Never" else SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date(ts))
private fun startOfToday(): Long = Calendar.getInstance().apply {
    set(Calendar.HOUR_OF_DAY, 0); set(Calendar.MINUTE, 0); set(Calendar.SECOND, 0); set(Calendar.MILLISECOND, 0)
}.timeInMillis

@Composable
private fun SessionRouter(vm: MainViewModel) {
    when (val s = vm.session) {
        is ScanSession.PersonResult -> PersonResultScreen(vm, s)
        is ScanSession.VehicleScan -> VehicleScanScreen(vm, s)
        is ScanSession.VehicleDriver -> VehicleDriverScreen(vm, s)
        is ScanSession.VehicleCoDriver -> VehicleCoDriverScreen(vm, s)
        is ScanSession.VehicleOccupants -> VehicleOccupantsScreen(vm, s)
        is ScanSession.Unknown -> UnknownResultScreen(vm, s.message)
        ScanSession.Closed -> Unit
    }
}

// ------------------------------------------------------------------ Comms (messages & alerts with the Command Center)

@Composable
private fun CommsScreen(vm: MainViewModel) {
    val context = LocalContext.current
    val messages by vm.commsMessages.collectAsState(initial = emptyList())
    val state by vm.commsState.collectAsState()
    var text by rememberSaveable { mutableStateOf("") }
    var confirmAlert by remember { mutableStateOf(false) }
    val listState = androidx.compose.foundation.lazy.rememberLazyListState()
    LaunchedEffect(messages.size) {
        vm.commsSeen()
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.size - 1)
    }

    Column(Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).imePadding().padding(horizontal = 16.dp, vertical = 14.dp)) {
        ScreenHeader("Comms", "Messages, alerts & calls with ${vm.currentConfig().serverName.ifBlank { "the Command Center" }}")
        Spacer(Modifier.height(10.dp))
        SosButton(vm)
        Spacer(Modifier.height(8.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = { vm.startCall(false) }, enabled = state.online, modifier = Modifier.weight(1f), shape = RoundedCornerShape(12.dp)) {
                Icon(Icons.Default.Call, null, tint = UiSuccess, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp))
                Text("Voice call", fontFamily = Sans, fontWeight = FontWeight.SemiBold, color = UiInk)
            }
            OutlinedButton(onClick = { vm.startCall(true) }, enabled = state.online, modifier = Modifier.weight(1f), shape = RoundedCornerShape(12.dp)) {
                Icon(Icons.Default.Videocam, null, tint = UiBlue, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp))
                Text("Video call", fontFamily = Sans, fontWeight = FontWeight.SemiBold, color = UiInk)
            }
        }
        Spacer(Modifier.height(8.dp))
        StatusBanner(
            when (state.status) {
                "ONLINE" -> "Comms engine connected • ${state.detail} • end-to-end encrypted"
                "CONNECTING" -> "Connecting to the Comms engine…"
                else -> (state.detail.ifBlank { "Offline" }) + ". Messages are kept and sent automatically when the link returns."
            },
            if (state.online) BannerTone.Success else BannerTone.Warning
        )
        if (!state.online && android.os.Build.VERSION.SDK_INT >= 23) {
            val pm = context.getSystemService(android.os.PowerManager::class.java)
            if (pm != null && !pm.isIgnoringBatteryOptimizations(context.packageName)) {
                Spacer(Modifier.height(6.dp))
                TextButton(onClick = {
                    runCatching {
                        context.startActivity(android.content.Intent(android.provider.Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, android.net.Uri.parse("package:" + context.packageName)))
                    }
                }) { Text("Allow alerts in the background (battery settings)", fontFamily = Sans, fontSize = 12.sp, color = UiBlue) }
            }
        }
        Spacer(Modifier.height(10.dp))
        Box(Modifier.weight(1f).fillMaxWidth()) {
            if (messages.isEmpty()) {
                Text(
                    "No messages yet. Messages and alerts from the Command Center appear here and ring even when the app is closed.",
                    fontFamily = Sans, fontSize = 13.sp, color = UiMuted, textAlign = TextAlign.Center,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp)
                )
            } else {
                LazyColumn(state = listState, modifier = Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(messages, key = { it.id }) { m -> CommsBubble(m) }
                }
            }
        }
        if (vm.commsError.isNotBlank()) { Spacer(Modifier.height(6.dp)); StatusBanner(vm.commsError, BannerTone.Error) }
        Spacer(Modifier.height(8.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = text, onValueChange = { if (it.length <= com.teamxv.qrmonitor.comms.CommsEngine.MAX_BODY) text = it },
                modifier = Modifier.weight(1f), placeholder = { Text("Message to Command Center", fontFamily = Sans, fontSize = 13.sp) },
                maxLines = 4, shape = RoundedCornerShape(12.dp)
            )
            Spacer(Modifier.width(8.dp))
            FilledIconButton(onClick = { vm.sendComms("MESSAGE", text) { text = "" } }, enabled = text.isNotBlank(),
                colors = IconButtonDefaults.filledIconButtonColors(containerColor = UiInk)) {
                Icon(Icons.Default.Send, "Send", tint = UiOnInk)
            }
        }
        Spacer(Modifier.height(6.dp))
        OutlinedButton(
            onClick = { confirmAlert = true }, enabled = text.isNotBlank(), modifier = Modifier.fillMaxWidth(),
            border = BorderStroke(1.dp, UiError), shape = RoundedCornerShape(12.dp)
        ) {
            Icon(Icons.Default.NotificationsActive, null, tint = UiError, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(6.dp))
            Text("Send as ALERT", fontFamily = Sans, fontWeight = FontWeight.Bold, color = UiError)
        }
    }
    if (confirmAlert) AlertDialog(
        onDismissRequest = { confirmAlert = false },
        title = { Text("Send ALERT?", fontFamily = Sans, fontWeight = FontWeight.Bold) },
        text = { Text("The alert pops up on the Command Center screen with a sound. Use it for urgent situations.", fontFamily = Sans) },
        confirmButton = { TextButton(onClick = { confirmAlert = false; vm.sendComms("ALERT", text) { text = "" } }) { Text("Send alert", color = UiError, fontWeight = FontWeight.Bold) } },
        dismissButton = { TextButton(onClick = { confirmAlert = false }) { Text("Cancel") } }
    )
}

@Composable
private fun CommsBubble(m: com.teamxv.qrmonitor.comms.CommsMessageEntity) {
    if (m.kind == "CALL") {
        val missed = m.body.startsWith("Missed")
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
            Surface(color = UiSurfaceSubtle, shape = RoundedCornerShape(12.dp), border = BorderStroke(1.dp, if (missed) UiError else UiBorder)) {
                Row(Modifier.padding(horizontal = 12.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(if (m.body.contains("video")) Icons.Default.Videocam else Icons.Default.Call, null, tint = if (missed) UiError else UiMuted, modifier = Modifier.size(14.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("${m.body} • ${SimpleDateFormat("dd MMM HH:mm", Locale.getDefault()).format(Date(m.createdAt))}", fontFamily = Sans, fontSize = 11.sp, color = if (missed) UiError else UiMuted)
                }
            }
        }
        return
    }
    val mine = m.direction == "OUT"
    val alert = m.kind == "ALERT"
    Row(Modifier.fillMaxWidth(), horizontalArrangement = if (mine) Arrangement.End else Arrangement.Start) {
        Surface(
            color = when { alert -> UiErrorBg; mine -> UiInk; else -> UiSurface },
            shape = RoundedCornerShape(14.dp),
            border = BorderStroke(1.dp, when { alert -> UiError; mine -> UiInk; else -> UiBorder }),
            modifier = Modifier.fillMaxWidth(0.82f)
        ) {
            Column(Modifier.padding(horizontal = 12.dp, vertical = 9.dp)) {
                Text(
                    (if (alert) "⚠ ALERT • " else "") + (if (mine) "You" else m.sender),
                    fontFamily = Sans, fontWeight = FontWeight.Bold, fontSize = 11.sp,
                    color = when { alert -> UiError; mine -> tc(0xFFFCD34D); else -> UiBlue }
                )
                Spacer(Modifier.height(3.dp))
                Text(m.body, fontFamily = Sans, fontSize = 14.sp, color = if (mine && !alert) UiOnInk else UiInk)
                Spacer(Modifier.height(4.dp))
                val tick = if (!mine) "" else when (m.state) { "READ" -> " • read"; "DELIVERED" -> " • delivered"; "SENT" -> " • sent"; else -> " • waiting for link" }
                Text(
                    SimpleDateFormat("dd MMM HH:mm", Locale.getDefault()).format(Date(m.createdAt)) + tick,
                    fontFamily = Mono, fontSize = 10.sp, color = if (mine && !alert) tc(0xFFA1A1AA) else UiMuted
                )
            }
        }
    }
}
