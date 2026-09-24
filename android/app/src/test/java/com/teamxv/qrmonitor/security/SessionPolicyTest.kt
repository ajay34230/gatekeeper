package com.teamxv.qrmonitor.security

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SessionPolicyTest {
    @Test fun onlineTokenExpiresAtBoundary() {
        assertFalse(SessionPolicy.onlineTokenValid(1000L, 1000L))
        assertTrue(SessionPolicy.onlineTokenValid(1001L, 1000L))
    }

    @Test fun offlineSessionExpiresAtBoundary() {
        assertFalse(SessionPolicy.offlineSessionValid(1000L, 1000L))
        assertTrue(SessionPolicy.offlineSessionValid(1001L, 1000L))
    }
}
