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
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/** Background synchronization: pull registry, push queued gate records, report terminal status. */
class SyncWorker(appContext: Context, workerParams: WorkerParameters) : CoroutineWorker(appContext, workerParams) {
    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val config = AppConfig(applicationContext)
        val state = SyncStateStore(applicationContext)
        if (!config.paired) return@withContext Result.success()
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

        return@withContext try {
            val count = repo.syncPending()
            repo.applyMasterBootstrap(m)
            api.heartbeat(config.baseUrl, config.deviceId, config.locationId, config.gateId, config.operatorId, repo.pendingCount(), BuildConfig.VERSION_NAME)
            state.markSuccess(count, api.lastRoute)
            Result.success()
        } catch (e: HttpFailure) {
            state.markError(e.message ?: "Synchronization failed")
            if (e.code in 400..499 && e.code != 429) Result.failure() else Result.retry()
        } catch (e: Exception) {
            state.markError(e.message ?: "Synchronization failed")
            Result.retry()
        }
    }

    private fun classifyFailure(failure: Throwable?, state: SyncStateStore): Result {
        state.markError(failure?.message ?: "Synchronization failed")
        return if (failure is HttpFailure && failure.code in 400..499 && failure.code != 429) Result.failure() else Result.retry()
    }
}
