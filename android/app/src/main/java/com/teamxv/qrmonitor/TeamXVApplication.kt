package com.teamxv.qrmonitor

import android.app.Application
import com.teamxv.qrmonitor.sync.SyncScheduler

class TeamXVApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        com.teamxv.qrmonitor.network.ApiClient.onDeviceRevoked = { com.teamxv.qrmonitor.security.TerminalWipe.wipe(this) }
        SyncScheduler.ensurePeriodic(this)
    }
}
