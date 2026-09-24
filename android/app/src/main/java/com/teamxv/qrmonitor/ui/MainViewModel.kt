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
    data class PersonResult(val person: PersonEntity, val inside: Boolean) : ScanSession
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

enum class ScannerTarget { PERSON, VEHICLE, DRIVER, CO_DRIVER, OCCUPANT }

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
    private val api = ApiClient()
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
        api.deviceKey = config.deviceKey
        api.operatorToken = config.operatorToken
        api.deviceId = config.deviceId
        loggedIn = config.operatorLoggedIn && config.canContinueOffline()
        if (config.operatorLoggedIn && !loggedIn) config.clearSession()

        syncStateRefresh()
        SyncScheduler.ensurePeriodic(app)

        viewModelScope.launch { repo.observePersonnel().collectLatest { personnel = it } }
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
            val result = api.login(config.baseUrl, user, password)
            if (result.isSuccess) {
                val value = result.getOrThrow()
                val now = System.currentTimeMillis()
                if (value.accessToken.isBlank() || value.expiresAt <= now) {
                    authMessage = "Server returned an invalid session."
                    return@launch
                }
                config.saveLogin(
                    value.username,
                    value.role,
                    value.accessToken,
                    value.expiresAt,
                    value.offlineGraceSeconds
                )
                api.operatorToken = config.operatorToken
                api.deviceKey = config.deviceKey
                api.deviceId = config.deviceId
                loggedIn = true
                authMessage = "Signed in as ${value.username}"
                syncStateRefresh()
                SyncScheduler.enqueueNow(getApplication())
            } else {
                authMessage = when (val failure = result.exceptionOrNull()) {
                    is HttpFailure -> when (failure.code) {
                        401 -> "Invalid username or password."
                        403 -> "This account is not permitted for mobile operations."
                        else -> "Login failed (HTTP ${failure.code})."
                    }
                    else -> "Login failed. Check the server connection."
                }
            }
        }
    }

    fun continueOffline() {
        val now = System.currentTimeMillis()
        if (config.canContinueOffline(now)) {
            api.operatorToken = config.operatorToken
            api.deviceKey = config.deviceKey
            api.deviceId = config.deviceId
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
            }
            when (target) {
                ScannerTarget.PERSON -> handlePerson(repo.lookupIdentity(raw, expected))
                ScannerTarget.VEHICLE -> handleVehicle(repo.lookupIdentity(raw, expected))
                ScannerTarget.DRIVER -> handleDriver(repo.lookupIdentity(raw, expected))
                ScannerTarget.CO_DRIVER -> handleCoDriver(repo.lookupIdentity(raw, expected))
                ScannerTarget.OCCUPANT -> handleOccupant(repo.lookupIdentity(raw, expected))
            }
        }
    }

    private suspend fun handlePerson(result: IdentityResult) {
        val p = result.person
        session = when {
            p == null -> ScanSession.Unknown(result.error ?: "Unknown person")
            !p.active -> ScanSession.Unknown("This credential is inactive.")
            else -> ScanSession.PersonResult(p, repo.isPersonInside(p.id))
        }
    }

    private suspend fun handleVehicle(result: IdentityResult) {
        val v = result.vehicle
        if (v != null) scannedVehicle = v
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
                    config.operatorId
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
                    config.operatorId
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
                    config.operatorId
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
                api.heartbeat(config.baseUrl, config.deviceId, config.locationId, config.gateId)
                NetworkStatus(
                    transport, true, true, true,
                    "${config.serverHost}:${config.serverPort}", localIp, latency, "API healthy"
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

    fun updateDeviceKey(value: String) {
        config.deviceKey = value
        api.deviceKey = config.deviceKey
        message = if (value.isBlank()) "Device key cleared" else "Device key saved"
    }

    fun updateConfig(host: String, port: Int, location: String, gate: String, device: String, operator: String) {
        config.serverHost = host
        config.serverPort = port
        config.locationId = location
        config.gateId = gate
        config.deviceId = device
        config.operatorId = operator
        api.deviceId = config.deviceId
        networkStatus = networkStatus.copy(server = "${config.serverHost}:${config.serverPort}")
        message = "Configuration saved"
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

