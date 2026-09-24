package com.teamxv.qrmonitor.sync

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.EventRepository
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.network.ApiClient
import com.teamxv.qrmonitor.network.HttpFailure
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class SyncWorker(appContext: Context, workerParams: WorkerParameters) : CoroutineWorker(appContext, workerParams) {
    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val config = AppConfig(applicationContext)
        val base = config.baseUrl
        val api = ApiClient().apply {
            deviceKey = config.deviceKey
            operatorToken = config.operatorToken
            deviceId = config.deviceId
        }
        val repo = EventRepository(AppDatabase.get(applicationContext), api) { base }
        val state = SyncStateStore(applicationContext)

        val health = api.health(base)
        if (health.isFailure) return@withContext classifyFailure(health.exceptionOrNull(), state)

        val master = api.fetchMaster(base)
        if (master.isFailure) return@withContext classifyFailure(master.exceptionOrNull(), state)
        repo.applyMasterBootstrap(master.getOrThrow())

        return@withContext try {
            val count = repo.syncPending()
            val heartbeat = api.heartbeat(base, config.deviceId, config.locationId, config.gateId)
            if (heartbeat.isFailure) {
                val failure = heartbeat.exceptionOrNull()
                if (failure != null) state.markError(failure.message ?: "Heartbeat failed")
                if (failure is HttpFailure && failure.code in 400..499 && failure.code != 429) {
                    return@withContext Result.failure()
                }
                return@withContext Result.retry()
            }
            state.markSuccess(count)
            Result.success()
        } catch (e: EventRepository.SyncTransientException) {
            state.markError(e.message ?: "Synchronization failed")
            Result.retry()
        } catch (e: Exception) {
            state.markError(e.message ?: "Synchronization failed")
            Result.retry()
        }
    }

    private fun classifyFailure(failure: Throwable?, state: SyncStateStore): Result {
        state.markError(failure?.message ?: "Synchronization failed")
        return if (failure is HttpFailure && failure.code in 400..499 && failure.code != 429) {
            Result.failure()
        } else {
            Result.retry()
        }
    }
}
