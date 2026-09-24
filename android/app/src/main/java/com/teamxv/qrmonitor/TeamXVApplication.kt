package com.teamxv.qrmonitor

import android.app.Application
import com.teamxv.qrmonitor.sync.SyncScheduler

class TeamXVApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        SyncScheduler.ensurePeriodic(this)
    }
}
