package com.teamxv.qrmonitor.scanner

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class QrPayloadParserTest {
    @Test fun validPersonIdIsAccepted() = assertEquals("P001", QrPayloadParser.personId("P-001"))
    @Test fun validVehicleIdIsAccepted() = assertEquals("V010", QrPayloadParser.vehicleId("v-010"))
    @Test fun malformedPersonIsRejected() = assertNull(QrPayloadParser.personId("P01"))
    @Test fun malformedVehicleIsRejected() = assertNull(QrPayloadParser.vehicleId("VABC"))
    @Test fun wrongPrefixIsRejected() = assertNull(QrPayloadParser.personId("V001"))
}
