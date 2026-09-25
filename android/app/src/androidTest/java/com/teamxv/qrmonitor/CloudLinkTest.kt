package com.teamxv.qrmonitor

import android.Manifest
import android.os.Build
import android.util.Base64
import androidx.compose.ui.test.junit4.createEmptyComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.ui.MainViewModel
import com.teamxv.qrmonitor.ui.ScanSession
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * The real app against a real Command Center reached ONLY through its Cloud Link (internet) address — CI starts the
 * server behind a free Cloudflare tunnel and passes the pairing QR as the instrumentation argument "pairQr".
 * Pairs, signs in, downloads the registry, identifies a soldier online, records entry and exit, uploads them, and
 * exchanges a Comms message with the PC. Skipped when no pairing QR is given.
 */
@RunWith(AndroidJUnit4::class)
class CloudLinkTest {
    @get:Rule val compose = createEmptyComposeRule()

    @Test
    fun pairSignInScanSyncAndMessageOverTheInternet() {
        val qr = InstrumentationRegistry.getArguments().getString("pairQr").orEmpty()
        assumeTrue("no Cloud Link server given", qr.startsWith("XVGK1:"))
        val ctx = InstrumentationRegistry.getInstrumentation().targetContext
        if (Build.VERSION.SDK_INT >= 33) InstrumentationRegistry.getInstrumentation().uiAutomation.grantRuntimePermission(ctx.packageName, Manifest.permission.POST_NOTIFICATIONS)

        // Drop the LAN addresses from the QR so the only way to the PC is the internet address.
        val json = JSONObject(String(Base64.decode(qr.removePrefix("XVGK1:"), Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)))
        json.put("h", JSONArray())
        val cloudOnly = "XVGK1:" + Base64.encodeToString(json.toString().toByteArray(), Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)

        val scenario = ActivityScenario.launch(MainActivity::class.java)
        lateinit var vm: MainViewModel
        scenario.onActivity { vm = ViewModelProvider(it)[MainViewModel::class.java]; vm.logout() }
        fun until(ms: Long, what: String, ok: () -> Boolean) {
            val end = System.currentTimeMillis() + ms
            while (!ok()) { check(System.currentTimeMillis() < end) { "Timed out: $what (${vm.pairingMessage} / ${vm.authMessage} / ${vm.message})" }; Thread.sleep(300) }
        }

        scenario.onActivity { vm.pairWithQr(cloudOnly) }
        until(60_000, "pairing through the Cloud Link") { vm.paired && vm.pairingMessage.startsWith("Paired") }
        scenario.onActivity { vm.selectPost("LOC07", "Location 07", "G02", "Gate 02"); vm.login("GK-01", "Operator#1") }
        until(60_000, "sign-in through the Cloud Link") { vm.loggedIn }
        until(90_000, "registry download") { vm.personnel.any { it.id == "P001" } }

        // Identify the soldier (Minimal mode: the name comes from the PC for this scan only), then record entry.
        scenario.onActivity { vm.openPersonScanner(); vm.onQr("P-001") }
        until(60_000, "online identification") { (vm.session as? ScanSession.PersonResult)?.person?.name == "CI Test Soldier" }
        scenario.onActivity { vm.confirmPerson() }
        until(30_000, "entry recorded") { vm.showSuccess }
        scenario.onActivity { vm.returnHomeAfterSuccess(); vm.trySync() }
        until(120_000, "entry uploaded to the PC") { vm.pending == 0 && vm.events.any { it.entityId == "P001" } }
        val synced = runBlocking { AppDatabase.get(ctx).movementEventDao().latestForEntity("PERSON", "P001") }
        assertEquals("SYNCED", synced?.syncStatus)

        // Comms through the tunnel: the CI server answers every message with "Echo: …".
        scenario.onActivity { vm.sendComms("MESSAGE", "cloud link check") {} }
        until(120_000, "Comms echo from the PC") { runBlocking { vm.commsMessages.first() }.any { it.direction == "IN" && it.body == "Echo: cloud link check" } }
        compose.waitUntil(5_000) { compose.onAllNodesWithText("SCAN PERSON", substring = true).fetchSemanticsNodes().isNotEmpty() }
        scenario.close()
    }
}
