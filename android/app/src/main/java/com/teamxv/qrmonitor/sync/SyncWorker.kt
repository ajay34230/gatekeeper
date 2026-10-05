package com.teamxv.qrmonitor.sync

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.teamxv.qrmonitor.BuildConfig
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.EventRepository
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.network.ApiClient
import com.teamxv.qrmonitor.network.HttpFailure
import com.teamxv.qrmonitor.network.NetworkMonitor
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/** Background synchronization: pull registry, push queued gate records, report terminal status. No network
 * constraint gates this job (see SyncScheduler) -- it runs on a purely local connection to the Command Center just
 * as well as an internet one; NetworkMonitor treats a local-only link as connected, unlike WorkManager's own
 * NetworkType.CONNECTED check. Internet is only actually required when the PC can only be reached at its
 * cloud/internet address, which shows up as an ordinary connection failure below, not a constraint. */
class SyncWorker(appContext: Context, workerParams: WorkerParameters) : CoroutineWorker(appContext, workerParams) {
    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val config = AppConfig(applicationContext)
        val state = SyncStateStore(applicationContext)
        if (!config.paired) return@withContext Result.success()
        if (!NetworkMonitor(applicationContext).isNetworkAvailable()) {
            state.markError("No network connection")
            return@withContext Result.retry()
        }
        if (!config.hasValidOnlineToken()) {
            state.markError("Operator sign-in required to synchronize")
            return@withContext Result.success()
        }
        val api = ApiClient(config).apply { operatorToken = config.operatorToken }
        val repo = EventRepository(AppDatabase.get(applicationContext), api) { config.baseUrl }

        val master = api.fetchMaster(config.baseUrl)
        if (master.isFailure) return@withContext classifyFailure(master.exceptionOrNull(), state)
        val m = master.getOrThrow()
        config.cachedLocations = m.locations.map { it.id to it.name }
        config.cachedGates = m.gates.map { it.id to it.name }
        config.sharingMode = m.sharingMode
        if (m.reasons.isNotEmpty()) config.movementReasons = m.reasons
        config.returnReasons = m.returnReasons
        api.commsInfo().onSuccess { ci -> config.commsPort = ci.port; config.commsPublicUrl = ci.publicUrl; config.commsPublicUsesCa = ci.publicUsesCaCertificate }

        return@withContext try {
            val count = repo.syncPending()
            repo.applyMasterBootstrap(m)
            val remaining = repo.pendingCount()
            api.heartbeat(config.baseUrl, config.deviceId, config.locationId, config.gateId, config.operatorId, remaining, BuildConfig.VERSION_NAME)
            state.markSuccess(count, api.lastRoute)
            com.teamxv.qrmonitor.diag.CrashLog.i("SyncWorker", "Sync completed: $count synced, $remaining pending (route=${api.lastRoute})")
            Result.success()
        } catch (e: HttpFailure) {
            state.markError(e.message ?: "Synchronization failed")
            com.teamxv.qrmonitor.diag.CrashLog.w("SyncWorker", "Sync HTTP error: ${e.code} ${e.reason} - ${e.message}")
            if (e.code in 400..499 && e.code != 429) Result.failure() else Result.retry()
        } catch (e: Exception) {
            state.markError(e.message ?: "Synchronization failed")
            com.teamxv.qrmonitor.diag.CrashLog.w("SyncWorker", "Sync failed: ${e.message ?: "unknown error"}", e)
            Result.retry()
        }
    }

    private fun classifyFailure(failure: Throwable?, state: SyncStateStore): Result {
        // The Command Center disabled this operator's account or reset the password: sign out on this terminal.
        if (failure is HttpFailure && failure.code == 401 && failure.reason == "OPERATOR_AUTH_REQUIRED") AppConfig(applicationContext).clearSession()
        state.markError(failure?.message ?: "Synchronization failed")
        return if (failure is HttpFailure && failure.code in 400..499 && failure.code != 429) Result.failure() else Result.retry()
    }
}
