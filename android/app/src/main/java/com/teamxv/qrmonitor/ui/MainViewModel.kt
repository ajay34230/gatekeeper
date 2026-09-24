package com.teamxv.qrmonitor.ui

import android.app.Application
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.Icon
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.EventRepository
import com.teamxv.qrmonitor.data.IdentityResult
import com.teamxv.qrmonitor.data.OperationResult
import com.teamxv.qrmonitor.data.VehicleManifestDraft
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.data.local.PersonEntity
import com.teamxv.qrmonitor.data.local.VehicleEntity
import com.teamxv.qrmonitor.data.model.EntityType
import com.teamxv.qrmonitor.data.model.EventType
import com.teamxv.qrmonitor.data.model.MovementEvent
import com.teamxv.qrmonitor.data.model.NetworkStatus
import com.teamxv.qrmonitor.data.model.PersonPresence
import com.teamxv.qrmonitor.data.model.PresenceSource
import com.teamxv.qrmonitor.network.ApiClient
import com.teamxv.qrmonitor.network.HttpFailure
import com.teamxv.qrmonitor.network.NetworkMonitor
import com.teamxv.qrmonitor.scanner.QrScannerView
import com.teamxv.qrmonitor.security.SessionPolicy
import com.teamxv.qrmonitor.sync.SyncScheduler
import com.teamxv.qrmonitor.sync.SyncStateStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

sealed interface ScanSession {
    data object Closed : ScanSession
    data class PersonResult(val person: PersonEntity, val inside: Boolean, val insideSince: Long = 0L, val scannedLocation: String = "", val locationMismatch: Boolean = false) : ScanSession
    data class VehicleScan(val vehicleId: String, val inside: Boolean) : ScanSession
    data class VehicleDriver(val vehicleId: String, val driver: PersonEntity) : ScanSession
    data class VehicleCoDriver(val vehicleId: String, val driver: PersonEntity, val coDriver: PersonEntity?) : ScanSession
    data class VehicleOccupants(
        val vehicleId: String,
        val driver: PersonEntity,
        val coDriver: PersonEntity?,
        val occupants: List<PersonEntity>
    ) : ScanSession
    data class Unknown(val message: String) : ScanSession
}

enum class ScannerTarget { PERSON, VEHICLE, DRIVER, CO_DRIVER, OCCUPANT, PAIRING }

data class SyncUiState(
    val lastSuccessfulSyncAt: Long = 0L,
    val lastError: String = "",
    val lastUploadedCount: Int = 0
)

data class CompletedVehicleDisplay(
    val vehicle: VehicleEntity,
    val driver: PersonEntity?,
    val coDriver: PersonEntity?,
    val occupants: List<PersonEntity>
)


class MainViewModel(app: Application) : AndroidViewModel(app) {
    private val config = AppConfig(app)
    private val network = NetworkMonitor(app)
    private val api = ApiClient(config)
    private val syncState = SyncStateStore(app)
    private val repo = EventRepository(
        AppDatabase.get(app),
        api
    ) { config.baseUrl }

    var events by mutableStateOf(listOf<MovementEvent>())
        private set
    var pending by mutableIntStateOf(0)
        private set
    var attention by mutableIntStateOf(0)
        private set
    var personnel by mutableStateOf(listOf<PersonPresence>())
        private set
    var networkStatus by mutableStateOf(
        NetworkStatus(server = "${config.serverHost}:${config.serverPort}")
    )
        private set
    var syncUi by mutableStateOf(SyncUiState())
        private set
    var message by mutableStateOf("Ready")
        private set
    var session by mutableStateOf<ScanSession>(ScanSession.Closed)
        private set
    var scannerTarget by mutableStateOf<ScannerTarget?>(null)
        private set
    var loggedIn by mutableStateOf(false)
        private set
    var authMessage by mutableStateOf("")
        private set

    var soundEnabled by mutableStateOf(config.soundEnabled)
        private set

    var paired by mutableStateOf(config.paired)
        private set
    var pairingMessage by mutableStateOf("")
        private set
    var busy by mutableStateOf(false)
        private set
    var locations by mutableStateOf(config.cachedLocations)
        private set
    var gates by mutableStateOf(config.cachedGates)
        private set
    var vehicles by mutableStateOf(listOf<VehicleEntity>())
        private set
    var vehiclesInside by mutableStateOf(setOf<String>())
        private set
    var vehicleMismatch by mutableStateOf("")
        private set

    var scannedVehicle by mutableStateOf<VehicleEntity?>(null)
        private set

    var showSuccess by mutableStateOf(false)
        private set
    var completedEvent by mutableStateOf<MovementEvent?>(null)
        private set
    var completedDurationMs by mutableLongStateOf(0L)
        private set
    var completedVehicle by mutableStateOf<CompletedVehicleDisplay?>(null)
        private set

    init {
        api.operatorToken = config.operatorToken
        loggedIn = config.operatorLoggedIn && config.canContinueOffline()
        if (config.operatorLoggedIn && !loggedIn) config.clearSession()

        syncStateRefresh()
        SyncScheduler.ensurePeriodic(app)

        viewModelScope.launch { repo.observePersonnel().collectLatest { personnel = it } }
        viewModelScope.launch { repo.observeVehicles().collectLatest { vehicles = it } }
        viewModelScope.launch { repo.observeVehiclesInside().collectLatest { vehiclesInside = it } }
        if (config.paired) refreshStations()
        viewModelScope.launch { repo.observeEvents().collectLatest { events = it } }
        viewModelScope.launch { repo.observePendingCount().collectLatest { pending = it } }
        viewModelScope.launch { repo.observeAttentionCount().collectLatest { attention = it } }
    }

    fun login(username: String, password: String) {
        val user = username.trim()
        if (user.isBlank() || password.isBlank()) {
            authMessage = "Enter username and password."
            return
        }
        viewModelScope.launch(Dispatchers.IO) {
            if (!config.paired) { authMessage = "Pair this terminal with the PC Command Center first (PC Server Connection below)."; return@launch }
            if (config.locationId.isBlank() || config.gateId.isBlank()) { authMessage = "Select your station location and gate."; return@launch }
            busy = true
            val result = api.login(config.baseUrl, user, password)
            busy = false
            if (result.isSuccess) {
                val value = result.getOrThrow()
                val now = System.currentTimeMillis()
                if (value.accessToken.isBlank() || value.expiresAt <= now) {
                    authMessage = "Server returned an invalid session."
                    return@launch
                }
                config.saveLogin(
                    value.username,
                    value.name,
                    value.role,
                    value.accessToken,
                    value.expiresAt,
                    value.offlineGraceSeconds
                )
                api.operatorToken = config.operatorToken
                loggedIn = true
                authMessage = "Signed in as ${value.username}"
                syncStateRefresh()
                SyncScheduler.enqueueNow(getApplication())
            } else {
                authMessage = when (val failure = result.exceptionOrNull()) {
                    is HttpFailure -> failure.message ?: "Sign-in failed (HTTP ${failure.code})."
                    else -> "Cannot reach the Command Center. Check that this phone and the PC are on the same network, or configure Cloud Link."
                }
            }
        }
    }

    fun continueOffline() {
        val now = System.currentTimeMillis()
        if (config.canContinueOffline(now)) {
            api.operatorToken = config.operatorToken
            loggedIn = true
            authMessage = "Offline session restored"
        } else {
            config.clearSession()
            loggedIn = false
            authMessage = "Previous offline session has expired. Sign in again."
        }
    }

    fun enforceSession() {
        if (!config.operatorLoggedIn) {
            loggedIn = false
            return
        }
        val now = System.currentTimeMillis()
        val networkAvailable = network.isNetworkAvailable()
        if (networkAvailable && !SessionPolicy.onlineTokenValid(config.operatorTokenExpiresAt, now)) {
            config.clearSession()
            api.operatorToken = ""
            loggedIn = false
            authMessage = "Online session expired. Please sign in again."
        } else if (!networkAvailable && !SessionPolicy.offlineSessionValid(config.offlineSessionUntil, now)) {
            config.clearSession()
            api.operatorToken = ""
            loggedIn = false
            authMessage = "Offline session expired. Please sign in again."
        }
    }

    fun logout() {
        viewModelScope.launch(Dispatchers.IO) {
            if (config.operatorToken.isNotBlank()) {
                runCatching { api.logout(config.baseUrl) }
            }
            config.clearSession()
            api.operatorToken = ""
            loggedIn = false
            session = ScanSession.Closed
            scannerTarget = null
            authMessage = "Signed out"
            scannedVehicle = null
            showSuccess = false
            completedEvent = null
            completedVehicle = null
            completedDurationMs = 0L
        }
    }

    fun openPersonScanner() {
        showSuccess = false
        completedEvent = null
        completedVehicle = null
        completedDurationMs = 0L
        scannerTarget = ScannerTarget.PERSON
    }
    fun openVehicleScanner() {
        showSuccess = false
        completedEvent = null
        completedVehicle = null
        completedDurationMs = 0L
        scannedVehicle = null
        scannerTarget = ScannerTarget.VEHICLE
    }
    fun closeScanner() { scannerTarget = null }

    fun onQr(raw: String) {
        val target = scannerTarget ?: return
        closeScanner()
        viewModelScope.launch {
            val expected = when (target) {
                ScannerTarget.PERSON,
                ScannerTarget.DRIVER,
                ScannerTarget.CO_DRIVER,
                ScannerTarget.OCCUPANT -> EntityType.PERSON
                ScannerTarget.VEHICLE -> EntityType.VEHICLE
                ScannerTarget.PAIRING -> { pairWithQr(raw); return@launch }
            }
            when (target) {
                ScannerTarget.PERSON -> handlePerson(repo.lookupIdentity(raw, expected, config.locationId))
                ScannerTarget.VEHICLE -> handleVehicle(repo.lookupIdentity(raw, expected, config.locationId))
                ScannerTarget.DRIVER -> handleDriver(repo.lookupIdentity(raw, expected))
                ScannerTarget.CO_DRIVER -> handleCoDriver(repo.lookupIdentity(raw, expected))
                ScannerTarget.OCCUPANT -> handleOccupant(repo.lookupIdentity(raw, expected))
                ScannerTarget.PAIRING -> Unit
            }
        }
    }

    private suspend fun handlePerson(result: IdentityResult) {
        val p = result.person
        session = when {
            p == null -> ScanSession.Unknown(result.error ?: "Unknown person")
            else -> ScanSession.PersonResult(p, repo.isPersonInside(p.id), repo.insideSince(p.id), result.scannedLocation, result.locationMismatch)
        }
    }

    private suspend fun handleVehicle(result: IdentityResult) {
        val v = result.vehicle
        if (v != null) scannedVehicle = v
        vehicleMismatch = if (result.locationMismatch) result.scannedLocation else ""
        session = when {
            v == null -> ScanSession.Unknown(result.error ?: "Unknown vehicle")
            !v.active -> ScanSession.Unknown("This vehicle credential is inactive.")
            else -> ScanSession.VehicleScan(v.id, repo.isVehicleInside(v.id))
        }
    }

    private suspend fun handleDriver(result: IdentityResult) {
        val p = result.person
        val s = session as? ScanSession.VehicleScan
        session = when {
            p == null -> ScanSession.Unknown(result.error ?: "Driver not found")
            !p.active -> ScanSession.Unknown("Driver credential is inactive.")
            s == null -> ScanSession.Unknown("Vehicle session expired. Scan vehicle again.")
            else -> ScanSession.VehicleDriver(s.vehicleId, p)
        }
    }

    private suspend fun handleCoDriver(result: IdentityResult) {
        val p = result.person
        val s = session as? ScanSession.VehicleDriver
        session = when {
            p == null -> ScanSession.Unknown(result.error ?: "Co-driver not found")
            !p.active -> ScanSession.Unknown("Co-driver credential is inactive.")
            s == null -> ScanSession.Unknown("Vehicle session expired. Scan vehicle again.")
            p.id == s.driver.id -> ScanSession.Unknown("Driver and co-driver cannot be the same person.")
            else -> ScanSession.VehicleCoDriver(s.vehicleId, s.driver, p)
        }
    }

    private suspend fun handleOccupant(result: IdentityResult) {
        val p = result.person
        val s = session as? ScanSession.VehicleOccupants
        if (p == null) {
            session = ScanSession.Unknown(result.error ?: "Occupant not found")
            return
        }
        if (!p.active) {
            session = ScanSession.Unknown("Occupant credential is inactive.")
            return
        }
        if (s == null) {
            session = ScanSession.Unknown("Vehicle session expired. Scan vehicle again.")
            return
        }
        val existingIds = (listOf(s.driver) + listOfNotNull(s.coDriver) + s.occupants).map { it.id }
        if (p.id in existingIds) {
            message = "Duplicate occupant prevented: ${p.id}"
            return
        }
        session = s.copy(occupants = s.occupants + p)
    }

    fun confirmPerson() {
        val s = session as? ScanSession.PersonResult ?: return
        viewModelScope.launch {
            when (
                val result = repo.createPersonEntryOrExit(
                    s.person.id,
                    config.locationId,
                    config.gateId,
                    config.deviceId,
                    config.operatorId,
                    s.locationMismatch,
                    s.scannedLocation
                )
            ) {
                is OperationResult.Success -> {
                    val e = result.value
                    completedEvent = e
                    completedDurationMs = 0L
                    message = "${e.eventType.name} RECORDED • ${s.person.name} • ${e.eventId}"
                    session = ScanSession.Closed
                    showSuccess = true
                    trySync()
                }
                is OperationResult.Rejected -> {
                    session = ScanSession.Unknown(result.reason)
                    message = result.reason
                }
            }
        }
    }

    fun startVehicleDriverScan() {
        val s = session as? ScanSession.VehicleScan ?: return
        if (s.inside) {
            confirmVehicleExit()
        } else {
            scannerTarget = ScannerTarget.DRIVER
        }
    }

    fun startCoDriverScan() {
        if (session is ScanSession.VehicleDriver) scannerTarget = ScannerTarget.CO_DRIVER
    }

    fun skipCoDriver() {
        (session as? ScanSession.VehicleDriver)?.let {
            session = ScanSession.VehicleCoDriver(it.vehicleId, it.driver, null)
        }
    }

    fun beginOccupants() {
        (session as? ScanSession.VehicleCoDriver)?.let {
            session = ScanSession.VehicleOccupants(it.vehicleId, it.driver, it.coDriver, emptyList())
        }
    }

    fun addOccupantScan() { scannerTarget = ScannerTarget.OCCUPANT }

    fun removeOccupant(id: String) {
        (session as? ScanSession.VehicleOccupants)?.let {
            session = it.copy(occupants = it.occupants.filterNot { person -> person.id == id })
        }
    }

    fun removeCoDriver() {
        (session as? ScanSession.VehicleOccupants)?.let {
            session = it.copy(coDriver = null)
        }
    }

    fun confirmVehicleEntry() {
        val s = session as? ScanSession.VehicleOccupants ?: return
        viewModelScope.launch {
            val vehicle = repo.lookupIdentity(s.vehicleId, EntityType.VEHICLE).vehicle
            if (vehicle == null) {
                session = ScanSession.Unknown("Vehicle no longer exists locally.")
                return@launch
            }
            when (
                val result = repo.createVehicleEntry(
                    VehicleManifestDraft(vehicle, s.driver, s.coDriver, s.occupants),
                    config.locationId,
                    config.gateId,
                    config.deviceId,
                    config.operatorId,
                    vehicleMismatch.isNotBlank(),
                    vehicleMismatch
                )
            ) {
                is OperationResult.Success -> {
                    val tx = result.value
                    completedEvent = tx.event
                    completedDurationMs = 0L
                    completedVehicle = CompletedVehicleDisplay(vehicle, s.driver, s.coDriver, s.occupants)
                    message = "VEHICLE ENTRY RECORDED • ${s.vehicleId} • Manifest ${tx.manifest.manifestId}"
                    session = ScanSession.Closed
                    showSuccess = true
                    trySync()
                }
                is OperationResult.Rejected -> {
                    session = ScanSession.Unknown(result.reason)
                    message = result.reason
                }
            }
        }
    }

    fun confirmVehicleExit() {
        val s = session as? ScanSession.VehicleScan ?: return
        viewModelScope.launch {
            when (
                val result = repo.createVehicleExit(
                    s.vehicleId,
                    config.locationId,
                    config.gateId,
                    config.deviceId,
                    config.operatorId,
                    vehicleMismatch.isNotBlank(),
                    vehicleMismatch
                )
            ) {
                is OperationResult.Success -> {
                    val duration = result.value.second
                    completedEvent = result.value.first
                    completedDurationMs = duration ?: 0L
                    completedVehicle = CompletedVehicleDisplay(
                        scannedVehicle ?: VehicleEntity(s.vehicleId, "", "Vehicle", true),
                        null,
                        null,
                        emptyList()
                    )
                    message = "VEHICLE EXIT RECORDED • ${s.vehicleId}" +
                        (duration?.let { " • Stay ${formatDuration(it)}" } ?: "")
                    session = ScanSession.Closed
                    showSuccess = true
                    trySync()
                }
                is OperationResult.Rejected -> {
                    session = ScanSession.Unknown(result.reason)
                    message = result.reason
                }
            }
        }
    }

    /** Display name for a record: person name or vehicle plate from the local registry. */
    fun titleFor(event: MovementEvent): String = when (event.entityType) {
        EntityType.PERSON -> personnel.firstOrNull { it.id == event.entityId }?.name ?: ""
        EntityType.VEHICLE -> vehicles.firstOrNull { it.id == event.entityId }?.registration ?: ""
    }

    /** Opens the identification screen for a person picked from the home roster (same checks as a scan). */
    fun selectPerson(id: String) {
        viewModelScope.launch { handlePerson(repo.lookupIdentity(id, EntityType.PERSON, config.locationId)) }
    }

    /** Starts the vehicle flow for a vehicle picked from the home fleet list. */
    fun selectVehicle(id: String) {
        showSuccess = false; completedEvent = null; completedVehicle = null
        viewModelScope.launch { handleVehicle(repo.lookupIdentity(id, EntityType.VEHICLE, config.locationId)) }
    }

    fun trySync() {
        SyncScheduler.enqueueNow(getApplication())
        message = if (network.isNetworkAvailable()) {
            "Synchronization requested"
        } else {
            "Offline: synchronization will resume when network returns"
        }
        syncStateRefresh()
    }

    fun testConnection() {
        viewModelScope.launch(Dispatchers.IO) {
            val (transport, localIp) = network.current()
            if (transport == "Disconnected") {
                networkStatus = NetworkStatus(
                    transport = transport,
                    server = "${config.serverHost}:${config.serverPort}",
                    localIp = localIp,
                    message = "No network available"
                )
                return@launch
            }
            val start = System.currentTimeMillis()
            val result = api.health(config.baseUrl)
            val latency = System.currentTimeMillis() - start
            networkStatus = if (result.isSuccess) {
                if (config.hasValidOnlineToken()) api.heartbeat(config.baseUrl, config.deviceId, config.locationId, config.gateId, config.operatorId, pending)
                if (pending > 0 && config.hasValidOnlineToken()) SyncScheduler.enqueueNow(getApplication())
                NetworkStatus(
                    transport, true, true, true,
                    api.lastRoute, localIp, latency, "Encrypted link verified with ${result.getOrNull()?.serverName ?: "Command Center"}"
                )
            } else {
                NetworkStatus(
                    transport, true, false, false,
                    "${config.serverHost}:${config.serverPort}", localIp, latency,
                    result.exceptionOrNull()?.message ?: "Server unavailable"
                )
            }
        }
    }

    // ------------------------------------------------------------------ pairing, stations, cloud link

    fun openPairingScanner() { pairingMessage = ""; scannerTarget = ScannerTarget.PAIRING }

    fun pairWithQr(raw: String) {
        val parsed = com.teamxv.qrmonitor.network.PairingInfo.parse(raw)
        // If the QR carries no internet address but one was entered on this phone, try it too.
        val info = parsed?.let { if (it.u.isBlank() && config.publicUrl.isNotBlank()) it.copy(u = config.publicUrl, pc = config.publicUsesCaCertificate) else it }
        if (info == null) { pairingMessage = "That is not a Command Center pairing QR. On the PC click 'Local Wi-Fi & Pair Device'."; return }
        busy = true
        pairingMessage = "Pairing with ${info.n.ifBlank { info.id }}…"
        viewModelScope.launch(Dispatchers.IO) {
            val name = android.os.Build.MANUFACTURER.replaceFirstChar { it.uppercase() } + " " + android.os.Build.MODEL
            val res = api.enroll(info, name, "Android ${android.os.Build.VERSION.RELEASE}")
            busy = false
            res.onSuccess { (enr, ep) ->
                val hosts = if (ep.internet) info.h else listOf(ep.baseUrl.substringAfter("https://").substringBeforeLast(":")) + info.h
                config.savePairing(enr.serverId.ifBlank { info.id }, enr.serverName.ifBlank { info.n }, info.fp, hosts, info.p, info.u, info.pc, enr.deviceId, enr.deviceKey)
                api.operatorToken = ""
                paired = true
                loggedIn = false
                pairingMessage = "Paired securely with ${config.serverName} (${enr.deviceId}). Sign in to start your shift."
                refreshStations()
            }.onFailure { e ->
                pairingMessage = if (e is HttpFailure) (e.message ?: "Pairing refused") else (e.message ?: "Pairing failed")
            }
        }
    }

    fun unpair() {
        config.unpair(); paired = false; loggedIn = false; api.operatorToken = ""
        pairingMessage = "Terminal unpaired. Scan a new pairing QR to connect."
    }

    /** Locations and gates configured on the PC (public list, no sign-in needed). */
    fun refreshStations() {
        viewModelScope.launch(Dispatchers.IO) {
            api.stations().onSuccess { (locs, gts) ->
                config.cachedLocations = locs; config.cachedGates = gts
                locations = locs; gates = gts
            }
        }
    }

    fun selectPost(locId: String, locName: String, gateId: String, gateName: String) {
        config.locationId = locId; config.locationName = locName; config.gateId = gateId; config.gateName = gateName
        message = "Post set to $locName • $gateName"
    }

    fun register(name: String, id: String, password: String, confirm: String, onRegistered: (String) -> Unit) {
        when {
            !config.paired -> { authMessage = "Pair this terminal with the PC Command Center first."; return }
            name.trim().length < 2 -> { authMessage = "Enter your rank and full name."; return }
            password.length < 6 -> { authMessage = "Password must be at least 6 characters."; return }
            password != confirm -> { authMessage = "Passwords do not match."; return }
        }
        busy = true
        viewModelScope.launch(Dispatchers.IO) {
            val r = api.register(name.trim(), id.trim(), password)
            busy = false
            r.onSuccess {
                authMessage = if (it.status == "pending") "Account ${it.username} created. Sign in after it is approved on the PC Command Center."
                else "Account ${it.username} created. You can sign in now."
                onRegistered(it.username)
            }.onFailure { e -> authMessage = (e as? HttpFailure)?.message ?: "Cannot reach the Command Center." }
        }
    }

    fun saveCloudLink(mode: String, url: String, publicCa: Boolean) {
        val clean = url.trim().trimEnd('/')
        if (clean.isNotEmpty() && !clean.startsWith("https://")) { message = "The internet address must start with https://"; return }
        config.connectionMode = mode; config.publicUrl = clean; config.publicUsesCaCertificate = publicCa
        message = "Connection settings saved"
        testConnection()
    }

    fun syncStateRefresh() {
        syncUi = SyncUiState(
            syncState.lastSuccessfulSyncAt,
            syncState.lastError,
            syncState.lastUploadedCount
        )
    }

    fun scanNextAfterSuccess() {
        val type = completedEvent?.entityType
        showSuccess = false
        completedEvent = null
        completedVehicle = null
        completedDurationMs = 0L
        scannerTarget = if (type == EntityType.VEHICLE) ScannerTarget.VEHICLE else ScannerTarget.PERSON
    }

    fun returnHomeAfterSuccess() {
        showSuccess = false
        completedEvent = null
        completedVehicle = null
        completedDurationMs = 0L
        session = ScanSession.Closed
        scannerTarget = null
        scannedVehicle = null
    }

    fun returnToHome() {
        session = ScanSession.Closed
        scannerTarget = null
        scannedVehicle = null
    }

    fun updateSoundEnabled(enabled: Boolean) {
        soundEnabled = enabled
        config.soundEnabled = enabled
    }

    fun currentConfig(): AppConfig = config

    private fun formatDuration(ms: Long): String {
        val sec = ms.coerceAtLeast(0L) / 1000L
        return "${sec / 3600}h ${(sec % 3600) / 60}m ${sec % 60}s"
    }
}

