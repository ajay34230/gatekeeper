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
import androidx.lifecycle.Observer
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.work.WorkInfo
import androidx.work.WorkManager
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
import com.teamxv.qrmonitor.data.model.PresenceStatus
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
import kotlinx.coroutines.withContext
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
    /** [offerManualVehicleEntry]: this was a vehicle scan that couldn't be matched locally while offline -- the
     * guard can still type the registration plate by eye and record an entry, synced for the PC to reconcile once
     * connectivity returns, instead of being unable to record the vehicle at all. */
    data class Unknown(val message: String, val offerManualVehicleEntry: Boolean = false) : ScanSession
}

enum class ScannerTarget { PERSON, VEHICLE, DRIVER, CO_DRIVER, OCCUPANT, PAIRING }

data class SyncUiState(
    val lastSuccessfulSyncAt: Long = 0L,
    val lastError: String = "",
    val lastUploadedCount: Int = 0
)

/** Real, observed state behind any button that talks to the Command Center (sync, test connection, refresh
 * stations, pairing): queued/in-progress/succeeded/failed, driven off the actual outcome -- WorkManager's own
 * WorkInfo for background sync, or the coroutine's own result for a direct call -- never a guess. */
enum class ActivityState { IDLE, IN_PROGRESS, SUCCEEDED, FAILED }

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

    /** Background work (sign-in, pairing, sync, Comms): an unexpected error is shown and logged, never a crash. */
    private val appErrors = kotlinx.coroutines.CoroutineExceptionHandler { _, e ->
        com.teamxv.qrmonitor.diag.CrashLog.e("XV-APP", "Background action failed", e)
        busy = false
        message = "Action failed: ${e.message ?: e.javaClass.simpleName}"
    }

    /**
     * Gate actions (scan, identify, record) never close the app on an unexpected error: the guard sees what failed and
     * can scan again; the full error goes to the device log.
     */
    private val gateErrors = kotlinx.coroutines.CoroutineExceptionHandler { _, e ->
        com.teamxv.qrmonitor.diag.CrashLog.e("XV-GATE", "Gate action failed", e)
        showSuccess = false
        session = ScanSession.Unknown("This action could not be completed (${e.javaClass.simpleName}: ${e.message ?: "no details"}). Scan again; if it repeats, sync with the Command Center.")
    }

    var events by mutableStateOf(listOf<MovementEvent>())
        private set
    var pending by mutableIntStateOf(0)
        private set
    var attention by mutableIntStateOf(0)
        private set
    val attentionEvents = repo.observeAttentionEvents()
    var personnel by mutableStateOf(listOf<PersonPresence>())
        private set
    var networkStatus by mutableStateOf(
        NetworkStatus(server = "${config.serverHost}:${config.serverPort}")
    )
        private set
    var syncUi by mutableStateOf(SyncUiState())
        private set
    var syncActivity by mutableStateOf(ActivityState.IDLE)
        private set
    var testConnectionActivity by mutableStateOf(ActivityState.IDLE)
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
    var stationsActivity by mutableStateOf(ActivityState.IDLE)
        private set
    var stationsMessage by mutableStateOf("")
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

    /** Live, observed status of the "sync now" work item -- the actual source of truth for the sync/force-sync
     * buttons' progress indicator, not a fire-and-forget guess. */
    private val syncWorkLiveData = WorkManager.getInstance(app).getWorkInfosForUniqueWorkLiveData(SyncScheduler.UNIQUE_NOW)
    private val syncWorkObserver = Observer<List<WorkInfo>> { infos ->
        val info = infos.maxByOrNull { it.generation }
        val finished = info?.state?.isFinished == true
        syncActivity = when (info?.state) {
            WorkInfo.State.RUNNING, WorkInfo.State.ENQUEUED, WorkInfo.State.BLOCKED -> ActivityState.IN_PROGRESS
            WorkInfo.State.SUCCEEDED -> ActivityState.SUCCEEDED
            WorkInfo.State.FAILED, WorkInfo.State.CANCELLED -> ActivityState.FAILED
            else -> ActivityState.IDLE
        }
        if (finished) syncStateRefresh()
    }

    init {
        api.operatorToken = config.operatorToken
        loggedIn = config.operatorLoggedIn && config.canContinueOffline()
        if (config.operatorLoggedIn && !loggedIn) config.clearSession()

        syncStateRefresh()
        SyncScheduler.ensurePeriodic(app)
        syncWorkLiveData.observeForever(syncWorkObserver)

        viewModelScope.launch(appErrors) { repo.observePersonnel().collectLatest { personnel = it } }
        viewModelScope.launch(appErrors) { repo.observeVehicles().collectLatest { vehicles = it } }
        viewModelScope.launch(appErrors) { repo.observeVehiclesInside().collectLatest { vehiclesInside = it } }
        if (config.paired) refreshStations()
        viewModelScope.launch(appErrors) { repo.observeEvents().collectLatest { events = it } }
        viewModelScope.launch(appErrors) { repo.observePendingCount().collectLatest { pending = it } }
        viewModelScope.launch(appErrors) { repo.observeAttentionCount().collectLatest { attention = it } }
    }

    // ------------------------------------------------------------------ vehicles on the way (transit)

    /** Vehicles that left another location for this terminal's location and have not been recorded as arrived. */
    var incomingTrips by mutableStateOf(listOf<com.teamxv.qrmonitor.network.TransitTrip>())
        private set
    var transitListOpen by mutableStateOf(false)
    var transitError by mutableStateOf("")
        private set
    private var transitSkew by mutableLongStateOf(0L)
    private var transitClock by mutableLongStateOf(System.currentTimeMillis())
    /** Trip id -> server time until which this terminal's pop-up stays hidden after the RP closed it. */
    private val transitHidden = androidx.compose.runtime.mutableStateMapOf<String, Long>()

    /** Server clock, so late/on-time does not depend on this phone's clock being exact. */
    fun serverNow(): Long = transitClock + transitSkew

    fun isLate(t: com.teamxv.qrmonitor.network.TransitTrip): Boolean = t.overdue || (t.dueAt > 0 && serverNow() > t.dueAt)

    /** Late trips whose pop-up is not hidden: shown with a cross, and back 15 minutes after it is closed. */
    val alertTrips: List<com.teamxv.qrmonitor.network.TransitTrip>
        get() = incomingTrips.filter { isLate(it) && (transitHidden[it.transitId] ?: 0L) <= serverNow() }

    fun refreshIncoming() {
        transitClock = System.currentTimeMillis()
        if (!config.paired || !config.hasValidOnlineToken() || config.locationId.isBlank()) { if (incomingTrips.isNotEmpty()) incomingTrips = emptyList(); return }
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            api.openTransits(config.locationId).onSuccess { r ->
                transitSkew = r.serverTime - System.currentTimeMillis()
                transitClock = System.currentTimeMillis()
                incomingTrips = r.trips
                transitHidden.keys.retainAll(r.trips.map { it.transitId }.toSet())
            }
        }
    }

    /** The RP closed the pop-up: hide it for 15 minutes here and tell the server, which asks again after the same time. */
    fun snoozeAlerts(trips: List<com.teamxv.qrmonitor.network.TransitTrip>) {
        val until = serverNow() + 15 * 60_000L
        trips.forEach { transitHidden[it.transitId] = until }
        viewModelScope.launch(Dispatchers.IO + appErrors) { trips.forEach { api.snoozeTransit(it.transitId, config.locationId) } }
    }

    /** Records what happened to a trip. Nothing is assumed: the trip stays open until this succeeds. */
    fun resolveTrip(trip: com.teamxv.qrmonitor.network.TransitTrip, kind: String, minutes: Int, placeName: String, placeId: String, onDone: (Boolean) -> Unit) {
        transitError = ""
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            val r = api.resolveTransit(trip.transitId, kind, minutes, placeName, placeId, config.locationId)
            if (r.isSuccess) {
                incomingTrips = incomingTrips.filter { it.transitId != trip.transitId }
                refreshIncoming()
                withContext(Dispatchers.Main) { onDone(true) }
            } else {
                transitError = r.exceptionOrNull()?.message ?: "Could not save. Check the connection and try again."
                withContext(Dispatchers.Main) { onDone(false) }
            }
        }
    }

    fun login(username: String, password: String) {
        val user = username.trim()
        if (user.isBlank() || password.isBlank()) {
            authMessage = "Enter username and password."
            return
        }
        viewModelScope.launch(Dispatchers.IO + appErrors) {
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

    /** Signs the operator out only when the 24-hour sign-in has ended (a manual logout is handled by [logout]). */
    fun enforceSession() {
        if (!config.operatorLoggedIn) {
            if (loggedIn) authMessage = "Signed out by the Command Center (account disabled or password reset). Sign in again."
            loggedIn = false
            return
        }
        if (!config.canContinueOffline()) {
            config.clearSession()
            api.operatorToken = ""
            loggedIn = false
            authMessage = "Your 24-hour sign-in has ended. Please sign in again."
        }
    }

    fun logout() {
        viewModelScope.launch(Dispatchers.IO + appErrors) {
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

    /** Shift handover: what happened at this terminal since sign-in and who is still inside. */
    fun handoverReport(): HandoverReport {
        val from = config.shiftStartedAt
        val shift = events.filter { from == 0L || it.eventTimestamp >= from }
        fun count(type: EntityType, ev: EventType) = shift.count { it.entityType == type && it.eventType == ev }
        val regs = vehicles.associate { it.id to it.registration.ifBlank { it.id } }
        return HandoverReport(
            shiftStart = from, now = System.currentTimeMillis(),
            post = listOf(config.locationName.ifBlank { config.locationId }, config.gateName.ifBlank { config.gateId }).filter { it.isNotBlank() }.joinToString(" / "),
            operator = config.operatorName.ifBlank { config.operatorId },
            entries = count(EntityType.PERSON, EventType.ENTRY), exits = count(EntityType.PERSON, EventType.EXIT),
            vehicleEntries = count(EntityType.VEHICLE, EventType.ENTRY), vehicleExits = count(EntityType.VEHICLE, EventType.EXIT),
            pending = pending,
            inside = personnel.filter { it.currentStatus == PresenceStatus.INSIDE }.map { if (it.name.isBlank()) it.id else "${it.name} (${it.id})" }.sorted(),
            vehiclesInside = vehiclesInside.map { regs[it] ?: it }.sorted()
        )
    }

    var handoverError by mutableStateOf("")
        private set

    /** Sends the handover summary to the Command Center (queued if the link is down), then signs out. */
    fun sendHandoverAndLogout(report: HandoverReport) {
        val who = config.operatorName.ifBlank { config.operatorId }.ifBlank { config.deviceId }
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            runCatching { com.teamxv.qrmonitor.comms.CommsEngine.queue(getApplication(), "MESSAGE", report.toMessage(), if (report.post.isBlank()) who else "$who • ${report.post}") }
                .onSuccess { handoverError = ""; logout() }
                .onFailure { handoverError = "Handover summary could not be queued." + (it.message?.let { m -> " $m" } ?: "") }
        }
    }

    // ------------------------------------------------------------------ phone-to-phone handover (Bluetooth / Wi-Fi)

    private val handoverService = com.teamxv.qrmonitor.handover.NearbyHandoverService(app)

    var handoverPhase by mutableStateOf<com.teamxv.qrmonitor.handover.HandoverPhase>(com.teamxv.qrmonitor.handover.HandoverPhase.Idle)
        private set

    init {
        viewModelScope.launch { handoverService.phase.collectLatest { handoverPhase = it } }
    }

    /** How this operator identifies themself to the other phone during the code-confirmation step. */
    private fun handoverLabel(): String {
        val who = config.operatorName.ifBlank { config.operatorId }
        val post = listOf(config.locationName.ifBlank { config.locationId }, config.gateName.ifBlank { config.gateId }).filter { it.isNotBlank() }.joinToString("/")
        return listOf(who, post).filter { it.isNotBlank() }.joinToString(" • ")
    }

    /** Outgoing operator: builds a fresh snapshot (registry, who is inside, anything not yet confirmed synced) and
     * starts advertising this phone so the next operator's phone can find it -- no server or internet involved. */
    fun startHandoverSend() {
        viewModelScope.launch(appErrors) {
            val payload = withContext(Dispatchers.IO) {
                com.teamxv.qrmonitor.handover.HandoverPayload.build(config, AppDatabase.get(getApplication()))
            }
            handoverService.startSending(payload, handoverLabel())
        }
    }

    /** Incoming operator: searches for the outgoing operator's phone and, once the code is confirmed and the data
     * arrives, merges it straight into this terminal's own local database. */
    fun startHandoverReceive() {
        handoverService.startReceiving(handoverLabel()) { payload ->
            try {
                val summary = withContext(Dispatchers.IO) { payload.applyTo(config, AppDatabase.get(getApplication())) }
                locations = config.cachedLocations; gates = config.cachedGates
                handoverService.markReceiveApplied(summary)
            } catch (e: Exception) {
                handoverService.markReceiveFailed(e.message ?: "Could not apply the received data (${e.javaClass.simpleName}).")
            }
        }
    }

    /** Called once both operators have visually compared the code shown on both screens. */
    fun confirmHandoverCode(endpointId: String) = handoverService.confirmCode(endpointId)
    fun rejectHandoverCode(endpointId: String) = handoverService.rejectCode(endpointId)
    fun cancelHandoverTransfer() = handoverService.cancel()

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
        viewModelScope.launch(gateErrors) {
            val expected = when (target) {
                ScannerTarget.PERSON,
                ScannerTarget.DRIVER,
                ScannerTarget.CO_DRIVER,
                ScannerTarget.OCCUPANT -> EntityType.PERSON
                ScannerTarget.VEHICLE -> EntityType.VEHICLE
                ScannerTarget.PAIRING -> { pairWithQr(raw); return@launch }
            }
            when (target) {
                ScannerTarget.PERSON -> handlePerson(identify(raw, expected, config.locationId))
                ScannerTarget.VEHICLE -> handleVehicle(identify(raw, expected, config.locationId))
                ScannerTarget.DRIVER -> handleDriver(identify(raw, expected))
                ScannerTarget.CO_DRIVER -> handleCoDriver(identify(raw, expected))
                ScannerTarget.OCCUPANT -> handleOccupant(identify(raw, expected))
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
            // Not in the local registry and no connection to verify it online either: offer a manual, typed-plate
            // entry instead of turning the vehicle away outright -- it syncs to the PC to reconcile once this
            // terminal reaches the Command Center again.
            v == null && !network.isNetworkAvailable() -> ScanSession.Unknown(
                (result.error ?: "Unknown vehicle") + " No connection to verify it online either.",
                offerManualVehicleEntry = true
            )
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

    /** Entry/exit reasons set on the Command Center (the operator may also type a custom one). */
    var reasons by mutableStateOf(config.movementReasons)
    /** Exit reasons that need an expected return date (leave, TD …). */
    var returnReasons by mutableStateOf(config.returnReasons)

    /** Reason to pre-select when this person enters: coming back from leave recorded at this terminal. */
    fun suggestedEntryReason(personId: String): String {
        val lastExit = events.filter { it.entityId == personId && it.eventType == com.teamxv.qrmonitor.data.model.EventType.EXIT }.maxByOrNull { it.eventTimestamp }
        return if (lastExit?.reason?.contains("leave", ignoreCase = true) == true && reasons.any { it.equals("Rejoining from Leave", true) }) "Rejoining from Leave" else ""
    }

    fun confirmPerson(reason: String = "", remarks: String = "", expectedReturn: Long = 0L, forcedType: EventType? = null, comingFrom: String = "") {
        val s = session as? ScanSession.PersonResult ?: return
        viewModelScope.launch(gateErrors) {
            when (
                val result = repo.createPersonEntryOrExit(
                    s.person.id,
                    config.locationId,
                    config.gateId,
                    config.deviceId,
                    config.operatorId,
                    s.locationMismatch,
                    s.scannedLocation,
                    reason,
                    remarks,
                    expectedReturn,
                    forcedType,
                    comingFrom
                )
            ) {
                is OperationResult.Success -> {
                    val e = result.value
                    completedEvent = e
                    // On exit, the time between the matching entry and this exit — shown as "Total Verified Stay".
                    completedDurationMs = if (e.eventType == EventType.EXIT && s.insideSince > 0) (e.eventTimestamp - s.insideSince).coerceAtLeast(0L) else 0L
                    message = "${e.eventType.name} RECORDED • ${s.person.name} • ${e.eventId}"
                    session = ScanSession.Closed
                    showSuccess = true
                    trySync()
                }
                is OperationResult.Rejected -> {
                    session = ScanSession.Unknown(explainRejection(result.reason))
                    message = result.reason
                }
            }
        }
    }

    /** Plain words for the guard instead of internal codes. */
    private fun explainRejection(code: String): String = when (code) {
        "PASS_EXPIRED" -> "Entry refused: this visitor / temporary pass has expired. Direct the visitor to the issuing office."
        "PASS_NOT_YET_VALID" -> "Entry refused: this visitor / temporary pass is not valid yet."
        "ALREADY_INSIDE" -> "This person is already recorded inside. Choose Exit instead — or sync if that looks wrong."
        "NOT_INSIDE" -> "This person is not recorded inside. Choose Entry instead — or sync if that looks wrong."
        "PERSON_NOT_FOUND" -> "This person is no longer in the registry on this terminal. Sync with the Command Center and scan again."
        "INACTIVE_PERSON" -> "Access denied: this credential is not active."
        "PRESENCE_STATE_CHANGED", "MANIFEST_STATE_CHANGED" -> "The record changed while you were confirming (another scan or a sync). Scan again."
        "VEHICLE_NOT_FOUND" -> "This vehicle is no longer in the registry on this terminal. Sync and scan again."
        "INACTIVE_VEHICLE" -> "Access denied: this vehicle credential is not active."
        "VEHICLE_ALREADY_INSIDE" -> "This vehicle is already recorded inside."
        "VEHICLE_NOT_INSIDE" -> "This vehicle is not recorded inside."
        "INACTIVE_MANIFEST_MEMBER" -> "One of the people on board has an inactive credential."
        "PERSON_ALREADY_INSIDE" -> "One of the people on board is already recorded inside. Record their exit first."
        else -> code
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

    /** Records a manually-typed vehicle entry when the plate couldn't be matched locally and there is no
     * connection to verify it online -- see ScanSession.Unknown.offerManualVehicleEntry. */
    fun recordManualVehicleEntry(registration: String, remarks: String = "") {
        viewModelScope.launch(gateErrors) {
            when (
                val result = repo.createManualVehicleEntry(registration, config.locationId, config.gateId, config.deviceId, config.operatorId, remarks)
            ) {
                is OperationResult.Success -> {
                    val e = result.value
                    val plate = registration.trim().uppercase()
                    completedEvent = e
                    completedDurationMs = 0L
                    completedVehicle = CompletedVehicleDisplay(VehicleEntity(e.entityId, plate, "Vehicle (unverified)", true), null, null, emptyList())
                    message = "VEHICLE ENTRY RECORDED (MANUAL) • $plate • will be reconciled on sync"
                    session = ScanSession.Closed
                    showSuccess = true
                    trySync()
                }
                is OperationResult.Rejected -> {
                    message = if (result.reason == "REGISTRATION_REQUIRED") "Enter the vehicle's registration number." else result.reason
                }
            }
        }
    }

    fun confirmVehicleEntry() {
        val s = session as? ScanSession.VehicleOccupants ?: return
        viewModelScope.launch(gateErrors) {
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
                    session = ScanSession.Unknown(explainRejection(result.reason))
                    message = result.reason
                }
            }
        }
    }

    fun confirmVehicleExit(destinationId: String = "", destinationName: String = "", transitMinutes: Int = 0) {
        val s = session as? ScanSession.VehicleScan ?: return
        viewModelScope.launch(gateErrors) {
            when (
                val result = repo.createVehicleExit(
                    s.vehicleId,
                    config.locationId,
                    config.gateId,
                    config.deviceId,
                    config.operatorId,
                    vehicleMismatch.isNotBlank(),
                    vehicleMismatch,
                    destinationId, destinationName, transitMinutes
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
                    session = ScanSession.Unknown(explainRejection(result.reason))
                    message = result.reason
                }
            }
        }
    }

    /**
     * Resolves a scanned code. In Receive-only mode the Command Center verifies it online (nothing personal is stored
     * on the phone); in Full / Minimal mode the encrypted local registry is used, so scanning also works offline.
     */
    private suspend fun identify(raw: String, expected: EntityType, location: String = ""): com.teamxv.qrmonitor.data.IdentityResult {
        if (config.sharingMode == "FULL") return repo.lookupIdentity(raw, expected, location)
        if (config.sharingMode != "RECEIVE_ONLY") {
            // Minimal mode: the badge is checked offline against its hash; names are fetched for this screen only and never stored.
            val local = repo.lookupIdentity(raw, expected, location)
            val id = local.person?.id ?: local.vehicle?.id ?: return local
            val v = kotlinx.coroutines.withTimeoutOrNull(3_500) {
                kotlinx.coroutines.withContext(Dispatchers.IO) { api.verify(id, if (local.person != null) "PERSON" else "VEHICLE").getOrNull() }
            } ?: return local
            return when {
                local.person != null && v.type == "PERSON" -> local.copy(person = local.person.copy(
                    name = v.name.ifBlank { local.person.name }, rank = v.rank, serviceNo = v.serviceNo, unit = v.unit, company = v.company))
                local.vehicle != null && v.type == "VEHICLE" -> local.copy(vehicle = local.vehicle.copy(
                    registration = v.registration.ifBlank { local.vehicle.registration }, type = v.vehicleType.ifBlank { local.vehicle.type }))
                else -> local
            }
        }
        val parsed = com.teamxv.qrmonitor.scanner.QrPayloadParser.parse(raw)
        val res = kotlinx.coroutines.withContext(Dispatchers.IO) { api.verify(parsed.code, expected.name) }
        return res.fold(
            onSuccess = { v ->
                val r = repo.applyOnlineVerification(v)
                val mismatch = parsed.location.isNotBlank() && location.isNotBlank() &&
                    com.teamxv.qrmonitor.scanner.QrPayloadParser.normalizeLocation(parsed.location) != com.teamxv.qrmonitor.scanner.QrPayloadParser.normalizeLocation(location)
                r.copy(scannedLocation = parsed.location, locationMismatch = mismatch)
            },
            onFailure = { e ->
                com.teamxv.qrmonitor.data.IdentityResult(error = (e as? HttpFailure)?.message
                    ?: "Receive-only mode: this terminal must be connected to the Command Center to verify credentials.")
            }
        )
    }

    /** Data Sharing mode set by the Command Center administrator (FULL / MINIMAL / RECEIVE_ONLY). */
    val sharingMode: String get() = config.sharingMode

    /** Display name for a record: person name or vehicle plate from the local registry. */
    fun titleFor(event: MovementEvent): String = when (event.entityType) {
        EntityType.PERSON -> personnel.firstOrNull { it.id == event.entityId }?.name ?: ""
        EntityType.VEHICLE -> vehicles.firstOrNull { it.id == event.entityId }?.registration ?: ""
    }

    /** Opens the identification screen for a person picked from the home roster (same checks as a scan). */
    fun selectPerson(id: String) {
        viewModelScope.launch(gateErrors) { handlePerson(repo.lookupIdentity(id, EntityType.PERSON, config.locationId)) }
    }

    /** Starts the vehicle flow for a vehicle picked from the home fleet list. */
    fun selectVehicle(id: String) {
        showSuccess = false; completedEvent = null; completedVehicle = null
        viewModelScope.launch(gateErrors) { handleVehicle(repo.lookupIdentity(id, EntityType.VEHICLE, config.locationId)) }
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
        testConnectionActivity = ActivityState.IN_PROGRESS
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            val (transport, localIp) = network.current()
            if (transport == "Disconnected") {
                networkStatus = NetworkStatus(
                    transport = transport,
                    server = "${config.serverHost}:${config.serverPort}",
                    localIp = localIp,
                    message = "No network available"
                )
                testConnectionActivity = ActivityState.FAILED
                return@launch
            }
            val start = System.currentTimeMillis()
            val result = api.health(config.baseUrl)
            val latency = System.currentTimeMillis() - start
            networkStatus = if (result.isSuccess) {
                if (config.hasValidOnlineToken()) api.heartbeat(config.baseUrl, config.deviceId, config.locationId, config.gateId, config.operatorId, pending)
                if (pending > 0 && config.hasValidOnlineToken()) SyncScheduler.enqueueNow(getApplication())
                refreshCommsInfo()
                testConnectionActivity = ActivityState.SUCCEEDED
                NetworkStatus(
                    transport, true, true, true,
                    api.lastRoute, localIp, latency, "Encrypted link verified with ${result.getOrNull()?.serverName ?: "Command Center"}"
                )
            } else {
                testConnectionActivity = ActivityState.FAILED
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

    /** PC address typed by the guard (from the PC's pairing window); tried before the addresses in the QR. */
    var manualPcHost by mutableStateOf("")

    fun pairWithQr(raw: String) {
        val typed = manualPcHost.trim().removePrefix("https://").substringBefore('/').substringBefore(':')
        val parsed = com.teamxv.qrmonitor.network.PairingInfo.parse(raw)?.let { p ->
            if (typed.matches(Regex("^[0-9]{1,3}(\\.[0-9]{1,3}){3}$|^[A-Za-z0-9.-]+$"))) p.copy(h = listOf(typed) + p.h.filter { it != typed }) else p
        }
        // If the QR carries no internet address but one was entered on this phone, try it too.
        val info = parsed?.let { if (it.u.isBlank() && config.publicUrl.isNotBlank()) it.copy(u = config.publicUrl, pc = config.publicUsesCaCertificate) else it }
        if (info == null) { pairingMessage = "That is not a Command Center pairing QR. On the PC click 'Local Wi-Fi & Pair Device'."; return }
        busy = true
        pairingMessage = "Pairing with ${info.n.ifBlank { info.id }}…"
        viewModelScope.launch(Dispatchers.IO + appErrors) {
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
                refreshCommsInfo()
            }.onFailure { e ->
                pairingMessage = if (e is HttpFailure) (e.message ?: "Pairing refused") else (e.message ?: "Pairing failed")
            }
        }
    }

    fun unpair() {
        com.teamxv.qrmonitor.comms.CommsService.stop(getApplication())
        viewModelScope.launch(Dispatchers.IO + appErrors) { com.teamxv.qrmonitor.comms.CommsDatabase.get(getApplication()).dao().clear() }
        config.unpair(); paired = false; loggedIn = false; api.operatorToken = ""
        pairingMessage = "Terminal unpaired. Scan a new pairing QR to connect."
    }

    // ------------------------------------------------------------------ comms engine (messages & alerts)

    private val commsDao by lazy { com.teamxv.qrmonitor.comms.CommsDatabase.get(getApplication()).dao() }
    val commsMessages by lazy { commsDao.observeAll() }
    val commsUnseen by lazy { commsDao.observeUnseen() }
    val commsState get() = com.teamxv.qrmonitor.comms.CommsEngine.state
    var commsError by mutableStateOf("")

    /** Learns where the PC's Comms engine listens and (re)starts the engine service. */
    fun refreshCommsInfo() {
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            api.commsInfo().onSuccess { ci ->
                val changed = ci.port != config.commsPort || ci.publicUrl.trimEnd('/') != config.commsPublicUrl || ci.publicUsesCaCertificate != config.commsPublicUsesCa
                config.commsPort = ci.port; config.commsPublicUrl = ci.publicUrl; config.commsPublicUsesCa = ci.publicUsesCaCertificate
                if (changed) com.teamxv.qrmonitor.comms.CommsEngine.nudge()
            }
            kotlinx.coroutines.withContext(Dispatchers.Main) { com.teamxv.qrmonitor.comms.CommsService.start(getApplication()) }
        }
    }

    fun sendComms(kind: String, body: String, onSent: () -> Unit) {
        val who = config.operatorName.ifBlank { config.operatorId }.ifBlank { config.deviceId }
        val post = listOf(config.locationName.ifBlank { config.locationId }, config.gateName.ifBlank { config.gateId }).filter { it.isNotBlank() }.joinToString(" / ")
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            runCatching { com.teamxv.qrmonitor.comms.CommsEngine.queue(getApplication(), kind, body, if (post.isBlank()) who else "$who • $post") }
                .onSuccess { commsError = ""; kotlinx.coroutines.withContext(Dispatchers.Main) { onSent() } }
                .onFailure { commsError = it.message ?: "Could not queue the message" }
        }
    }

    fun startCall(video: Boolean) {
        commsError = com.teamxv.qrmonitor.comms.CallManager.startOutgoing(getApplication(), video) ?: ""
    }

    var sosState by mutableStateOf("")

    /** SOS: one alert to the Command Center with post, operator and GPS position (queued if the link is down). */
    fun sendSos() {
        sosState = "Getting location…"
        viewModelScope.launch(appErrors) {
            val loc = com.teamxv.qrmonitor.comms.SosLocation.current(getApplication())
            val who = config.operatorName.ifBlank { config.operatorId }.ifBlank { "Unknown operator" }
            val post = listOf(config.locationName.ifBlank { config.locationId }, config.gateName.ifBlank { config.gateId }).filter { it.isNotBlank() }.joinToString(" / ").ifBlank { "post not set" }
            val body = "SOS — EMERGENCY at $post • operator $who (${config.operatorId}) • terminal ${config.deviceId} • " +
                com.teamxv.qrmonitor.comms.SosLocation.describe(loc) + " • " + java.text.SimpleDateFormat("dd MMM HH:mm:ss", java.util.Locale.getDefault()).format(java.util.Date())
            runCatching { com.teamxv.qrmonitor.comms.CommsEngine.queue(getApplication(), "ALERT", body, "$who • $post") }
                .onSuccess { sosState = if (com.teamxv.qrmonitor.comms.CommsEngine.state.value.online) "SOS sent to the Command Center" else "SOS queued — it is sent the moment the link returns" }
                .onFailure { sosState = "SOS could not be queued: ${it.message}" }
        }
    }

    fun commsSeen() {
        viewModelScope.launch(Dispatchers.IO + appErrors) { com.teamxv.qrmonitor.comms.CommsEngine.markSeen(getApplication()) }
    }

    /** Locations and gates configured on the PC (public list, no sign-in needed) -- callable from the login screen
     * (before or without ever signing in) so a stale or empty station list can be refreshed on the spot, over the
     * LAN or the internet, whichever reaches the Command Center. */
    fun refreshStations() {
        stationsActivity = ActivityState.IN_PROGRESS
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            api.stations()
                .onSuccess { (locs, gts) ->
                    api.reasonsFromServer?.let { config.movementReasons = it; reasons = it }
                    api.returnReasonsFromServer?.let { config.returnReasons = it; returnReasons = it }
                    config.cachedLocations = locs; config.cachedGates = gts
                    locations = locs; gates = gts
                    stationsMessage = "Locations & gates updated (${locs.size} location(s), ${gts.size} gate(s))"
                    stationsActivity = ActivityState.SUCCEEDED
                }
                .onFailure {
                    stationsMessage = it.message ?: "Could not reach the Command Center"
                    stationsActivity = ActivityState.FAILED
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
        viewModelScope.launch(Dispatchers.IO + appErrors) {
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

    /** Settings → Diagnostics: exports crash reports and logs (with this summary) to one shareable text file. */
    fun exportDiagnostics(onReady: (java.io.File) -> Unit) {
        viewModelScope.launch(Dispatchers.IO + appErrors) {
            val summary = listOf(
                "Paired: ${config.paired} • server ${config.serverName} • terminal ${config.deviceId}",
                "Post: ${config.locationId}/${config.gateId} • operator ${config.operatorId} • signed in $loggedIn",
                "Data sharing: ${config.sharingMode} • pending uploads $pending • needing attention $attention",
                "Network: ${networkStatus.transport} • ${networkStatus.message} • route ${networkStatus.server}",
                "Comms: ${com.teamxv.qrmonitor.comms.CommsEngine.state.value.status} ${com.teamxv.qrmonitor.comms.CommsEngine.state.value.detail}",
                "Display: dark ${UiPrefs.dark} • Hindi ${UiPrefs.hindi}"
            ).joinToString("\n")
            val f = com.teamxv.qrmonitor.diag.CrashLog.export(getApplication(), summary)
            kotlinx.coroutines.withContext(Dispatchers.Main) { onReady(f) }
        }
    }

    fun currentConfig(): AppConfig = config

    private fun formatDuration(ms: Long): String {
        val sec = ms.coerceAtLeast(0L) / 1000L
        return "${sec / 3600}h ${(sec % 3600) / 60}m ${sec % 60}s"
    }

    override fun onCleared() {
        syncWorkLiveData.removeObserver(syncWorkObserver)
        handoverService.dispose()
        super.onCleared()
    }
}

data class HandoverReport(
    val shiftStart: Long, val now: Long, val post: String, val operator: String,
    val entries: Int, val exits: Int, val vehicleEntries: Int, val vehicleExits: Int, val pending: Int,
    val inside: List<String>, val vehiclesInside: List<String>
) {
    /** Plain-text summary for the Command Center (within the 2000-character Comms limit). */
    fun toMessage(): String {
        val fmt = java.text.SimpleDateFormat("dd MMM HH:mm", java.util.Locale.ENGLISH)
        fun list(items: List<String>, max: Int): String =
            if (items.isEmpty()) "none" else items.take(max).joinToString(", ") + if (items.size > max) " +${items.size - max} more" else ""
        val text = "SHIFT HANDOVER — ${post.ifBlank { "post not set" }} • operator ${operator.ifBlank { "—" }} • " +
            "${if (shiftStart > 0) fmt.format(java.util.Date(shiftStart)) else "—"} to ${fmt.format(java.util.Date(now))}\n" +
            "Personnel: $entries entries, $exits exits • Vehicles: $vehicleEntries entries, $vehicleExits exits • Not yet synced: $pending\n" +
            "Still inside (${inside.size}): ${list(inside, 40)}\n" +
            "Vehicles inside (${vehiclesInside.size}): ${list(vehiclesInside, 20)}"
        return if (text.length <= 2000) text else text.take(1990) + "…"
    }
}
