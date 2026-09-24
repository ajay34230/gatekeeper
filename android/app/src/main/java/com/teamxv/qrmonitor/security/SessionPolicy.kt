package com.teamxv.qrmonitor.security

object SessionPolicy {
    fun onlineTokenValid(expiresAt: Long, now: Long = System.currentTimeMillis()): Boolean =
        expiresAt > now

    fun offlineSessionValid(offlineUntil: Long, now: Long = System.currentTimeMillis()): Boolean =
        offlineUntil > now
}
