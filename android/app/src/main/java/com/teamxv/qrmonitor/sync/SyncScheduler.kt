package com.teamxv.qrmonitor.sync

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.OutOfQuotaPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import java.util.concurrent.TimeUnit

object SyncScheduler {
    private const val UNIQUE_NOW = "teamxv-sync-now"
    private const val UNIQUE_PERIODIC = "teamxv-sync-periodic"

    // No network constraint here on purpose: WorkManager's own NetworkType.CONNECTED requires Android's
    // system-level *validated internet* capability, which a phone joined only to the Command Center's local
    // Wi-Fi (no internet behind it) never gets -- the job would sit queued forever and never even run. The
    // Command Center is often reached over the LAN with no internet at all, so SyncWorker itself checks
    // connectivity (NetworkMonitor, which does treat a local-only link as connected) and backs off cleanly
    // when there is truly no network at all; internet is only actually needed when the terminal can only
    // reach the PC through its cloud/internet address, which is a real reachability fact, not a constraint
    // we need to impose ourselves.
    private fun constraints() = Constraints.Builder().build()

    fun enqueueNow(context: Context) {
        // Expedited: asks Android to run this now, ahead of normal battery-saving deferral, the same way a tap-to-
        // sync or a just-completed gate scan expects to reach the Command Center right away rather than whenever
        // the OS next feels like scheduling background work. REPLACE (not KEEP) so a fresh request -- e.g. right
        // after a scan -- isn't left waiting behind an older one still sitting in exponential backoff.
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(constraints())
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 10, TimeUnit.SECONDS)
            .setExpedited(OutOfQuotaPolicy.RUN_AS_NON_EXPEDITED_WORK_REQUEST)
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork(UNIQUE_NOW, ExistingWorkPolicy.REPLACE, request)
    }

    fun ensurePeriodic(context: Context) {
        val request = PeriodicWorkRequestBuilder<SyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(constraints())
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 15, TimeUnit.MINUTES)
            .build()
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
            UNIQUE_PERIODIC, ExistingPeriodicWorkPolicy.KEEP, request
        )
    }
}
