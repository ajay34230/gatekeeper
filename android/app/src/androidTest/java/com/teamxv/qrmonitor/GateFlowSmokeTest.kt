package com.teamxv.qrmonitor

import android.Manifest
import android.content.Context
import android.os.Build
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createEmptyComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.teamxv.qrmonitor.config.AppConfig
import com.teamxv.qrmonitor.data.local.AppDatabase
import com.teamxv.qrmonitor.data.local.PersonEntity
import com.teamxv.qrmonitor.data.model.EventType
import com.teamxv.qrmonitor.ui.MainViewModel
import com.teamxv.qrmonitor.ui.UiPrefs
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Runs the real app on a device/emulator with the real encrypted database: a signed-in operator scans a soldier's
 * card, records entry, then exit on leave (expected return date through the date picker); an expired visitor pass
 * is refused; night mode, Hindi and the shift handover are rendered. Any crash fails the test with its stack trace.
 */
@RunWith(AndroidJUnit4::class)
class GateFlowSmokeTest {
    @get:Rule val compose = createEmptyComposeRule()

    private val ctx: Context get() = ApplicationProvider.getApplicationContext()
    private lateinit var scenario: ActivityScenario<MainActivity>
    private lateinit var vm: MainViewModel

    @Before
    fun seed() {
        val ui = InstrumentationRegistry.getInstrumentation().uiAutomation
        if (Build.VERSION.SDK_INT >= 33) ui.grantRuntimePermission(ctx.packageName, Manifest.permission.POST_NOTIFICATIONS)
        ui.grantRuntimePermission(ctx.packageName, Manifest.permission.CAMERA)
        val cfg = AppConfig(ctx)
        cfg.saveLogin("OP-TEST", "Test Operator", "RP", "", System.currentTimeMillis() + 3_600_000, 3_600)
        cfg.locationId = "LOC01"; cfg.locationName = "Main Gate Post"; cfg.gateId = "G1"; cfg.gateName = "Gate 1"
        runBlocking {
            val persons = AppDatabase.get(ctx).personDao()
            persons.upsert(PersonEntity("P001", "Test Soldier", "PERSONNEL", secretCode = "XVPTESTCODE01", rank = "Sub", serviceNo = "JC-0001", company = "Alpha", unit = "Test Unit"))
            val now = System.currentTimeMillis()
            persons.upsert(PersonEntity("G0001", "Expired Visitor", "VISITOR", secretCode = "XVPVISITOR001", validFrom = now - 7_200_000, validTo = now - 3_600_000))
        }
        UiPrefs.init(ctx); UiPrefs.updateDark(false); UiPrefs.updateHindi(false)
        scenario = ActivityScenario.launch(MainActivity::class.java)
        scenario.onActivity { vm = ViewModelProvider(it)[MainViewModel::class.java] }
        compose.waitUntil(15_000) { compose.onAllNodesWithText("SCAN PERSON", substring = true).fetchSemanticsNodes().isNotEmpty() }
    }

    @After
    fun tearDown() { UiPrefs.updateDark(false); UiPrefs.updateHindi(false); if (::scenario.isInitialized) scenario.close() }

    /** Opens the real camera scanner, lets frames flow for a moment, then delivers the code as the scanner does. */
    private fun scan(code: String) {
        scenario.onActivity { vm.openPersonScanner() }
        Thread.sleep(2_500)
        scenario.onActivity { vm.onQr(code) }
        compose.waitUntil(15_000) { compose.onAllNodesWithText("Person Identified").fetchSemanticsNodes().isNotEmpty() || compose.onAllNodesWithText("व्यक्ति की पहचान हुई").fetchSemanticsNodes().isNotEmpty() }
    }

    private fun waitText(text: String, ms: Long = 15_000) = compose.waitUntil(ms) { compose.onAllNodes(hasText(text, substring = true)).fetchSemanticsNodes().isNotEmpty() }

    private fun confirmAndFinish() {
        waitText("CONFIRM")
        compose.onNodeWithText("CONFIRM").performClick() // exact match: the button (the sheet title is "CONFIRM ENTRY/EXIT")
        waitText("SCAN NEXT TARGET")
        compose.onNodeWithText("RETURN TO TERMINAL HOME", substring = true).performScrollTo().performClick()
        waitText("SCAN PERSON")
    }

    @Test
    fun personnelEntryThenExitOnLeave() {
        scan("XVPTESTCODE01")
        compose.onNodeWithText("Test Soldier").assertExists()
        compose.onNodeWithText("RECORD ENTRY", substring = true).performScrollTo().performClick()
        confirmAndFinish()

        // Exit on leave: the expected return date is required and picked through the date picker.
        scan("XVPTESTCODE01")
        compose.onNodeWithText("Select reason (optional)").performScrollTo().performClick()
        compose.onNodeWithText("Proceeding on Leave").performClick()
        compose.onNodeWithText("Tap to choose (required)").performScrollTo().performClick()
        waitText("OK")
        compose.onNodeWithText("OK").performClick()
        compose.onNodeWithText("RECORD EXIT", substring = true).performScrollTo().performClick()
        confirmAndFinish()

        val events = runBlocking { AppDatabase.get(ctx).movementEventDao().latestForEntity("PERSON", "P001") }
        assertEquals(EventType.EXIT.name, events?.eventType)
        assertEquals("Proceeding on Leave", events?.reason)
        assertTrue("expected return date stored", (events?.expectedReturn ?: 0L) > System.currentTimeMillis())
    }

    @Test
    fun expiredVisitorPassIsRefused() {
        scan("XVPVISITOR001")
        waitText("PASS EXPIRED")
        assertTrue(compose.onAllNodesWithText("RECORD ENTRY", substring = true).fetchSemanticsNodes().isEmpty())
    }

    @Test
    fun nightModeHindiAndHandoverRender() {
        scenario.onActivity { UiPrefs.updateDark(true); UiPrefs.updateHindi(true) }
        scan("XVPTESTCODE01")
        waitText("प्रवेश दर्ज करें")
        scenario.onActivity { vm.returnToHome() }
        val report = vm.handoverReport()
        assertTrue(report.toMessage().startsWith("SHIFT HANDOVER"))
        compose.onNodeWithText("ऑपरेटर").performClick()
        compose.onNodeWithText("शिफ्ट सौंपें / लॉग आउट").performScrollTo().performClick()
        waitText("शिफ्ट हस्तांतरण")
    }

    @Test
    fun diagnosticsExportContainsLoggedErrors() {
        com.teamxv.qrmonitor.diag.CrashLog.e("XV-TEST", "diagnostics probe 4711")
        val file = com.teamxv.qrmonitor.diag.CrashLog.export(ctx, "test summary")
        val text = file.readText()
        assertTrue(text.contains("terminal diagnostics"))
        assertTrue(text.contains("diagnostics probe 4711"))
        assertTrue(text.contains("test summary"))
    }

    @Test
    fun signInSurvivesRestartAndEndsAfter24Hours() {
        // Closing and reopening the app keeps the operator signed in.
        scenario.close()
        scenario = ActivityScenario.launch(MainActivity::class.java)
        compose.waitUntil(15_000) { compose.onAllNodesWithText("SCAN PERSON", substring = true).fetchSemanticsNodes().isNotEmpty() }
        // A sign-in older than 24 hours ends: the sign-in screen comes back.
        scenario.close()
        AppConfig(ctx).shiftStartedAt = System.currentTimeMillis() - 25L * 3_600_000
        scenario = ActivityScenario.launch(MainActivity::class.java)
        compose.waitUntil(15_000) { compose.onAllNodesWithText("SIGN IN", substring = true).fetchSemanticsNodes().isNotEmpty() }
    }

    private fun until(ms: Long, what: String, ok: () -> Boolean) {
        val end = System.currentTimeMillis() + ms
        while (!ok()) { check(System.currentTimeMillis() < end) { "Timed out: $what (${vm.session} / ${vm.message})" }; Thread.sleep(200) }
    }

    @Test
    fun vehicleEntryWithDriverAndPassengerThenExit() {
        runBlocking {
            AppDatabase.get(ctx).vehicleDao().upsert(com.teamxv.qrmonitor.data.local.VehicleEntity(id = "V001", registration = "DL01AB1234", type = "Truck", secretCode = "XVVTESTVEH01"))
            AppDatabase.get(ctx).personDao().upsert(PersonEntity("P005", "Test Passenger", "PERSONNEL", secretCode = "XVPTESTCODE05"))
        }
        scenario.onActivity { vm.openVehicleScanner() }; Thread.sleep(1_500)
        scenario.onActivity { vm.onQr("XVVTESTVEH01") }
        until(15_000, "vehicle identified") { vm.session is com.teamxv.qrmonitor.ui.ScanSession.VehicleScan }
        scenario.onActivity { vm.startVehicleDriverScan() }; Thread.sleep(800)
        scenario.onActivity { vm.onQr("XVPTESTCODE01") }
        until(15_000, "driver verified") { vm.session is com.teamxv.qrmonitor.ui.ScanSession.VehicleDriver }
        scenario.onActivity { vm.skipCoDriver(); vm.beginOccupants(); vm.addOccupantScan() }; Thread.sleep(800)
        scenario.onActivity { vm.onQr("XVPTESTCODE05") }
        until(15_000, "passenger added") { (vm.session as? com.teamxv.qrmonitor.ui.ScanSession.VehicleOccupants)?.occupants?.size == 1 }
        scenario.onActivity { vm.confirmVehicleEntry() }
        until(15_000, "vehicle entry recorded") { vm.showSuccess }
        runBlocking { assertTrue(AppDatabase.get(ctx).vehicleManifestDao().activeForVehicle("V001") != null) }
        assertTrue("driver and passenger inside", runBlocking { AppDatabase.get(ctx).presenceSessionDao().activeForPerson("P005") != null })
        scenario.onActivity { vm.returnHomeAfterSuccess() }
        // Exit: scanning the vehicle again records its exit and closes everyone on board.
        scenario.onActivity { vm.openVehicleScanner() }; Thread.sleep(800)
        scenario.onActivity { vm.onQr("XVVTESTVEH01") }
        until(15_000, "vehicle shown as inside") { (vm.session as? com.teamxv.qrmonitor.ui.ScanSession.VehicleScan)?.inside == true }
        scenario.onActivity { vm.startVehicleDriverScan() }
        until(15_000, "vehicle exit recorded") { vm.showSuccess }
        runBlocking {
            assertTrue(AppDatabase.get(ctx).vehicleManifestDao().activeForVehicle("V001") == null)
            assertTrue(AppDatabase.get(ctx).presenceSessionDao().activeForPerson("P005") == null)
        }
    }

    @Test
    fun everyTabAndMainButtonOpensWithoutError() {
        for ((tab, expected) in listOf("Activity" to "Recent Activity", "Sync" to "Sync & Network Hub", "Comms" to "Message to Command Center", "Operator" to "RP Profile", "Home" to "SCAN PERSON")) {
            compose.onAllNodesWithText(tab)[compose.onAllNodesWithText(tab).fetchSemanticsNodes().size - 1].performClick()
            waitText(expected)
        }
        compose.onNodeWithText("Sync").performClick(); waitText("FORCE SYNC / DIAGNOSTIC PING")
        compose.onNodeWithText("FORCE SYNC / DIAGNOSTIC PING").performScrollTo().performClick(); Thread.sleep(1_000)
        compose.onNodeWithText("Comms").performClick(); waitText("Message to Command Center")
        scenario.onActivity { vm.sendComms("MESSAGE", "queued while offline") {} }
        waitText("queued while offline")
        compose.onNodeWithText("Operator").performClick(); waitText("CHANGE POST & CONNECTION")
        compose.onNodeWithText("CHANGE POST & CONNECTION").performScrollTo().performClick()
        waitText("REASSIGN TERMINAL POST")
    }
}
