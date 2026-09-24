package com.teamxv.qrmonitor.scanner

object QrPayloadParser {
    private val person = Regex("^P[0-9]{3,6}$")
    private val vehicle = Regex("^V[0-9]{3,6}$")

    fun normalize(raw: String): String = raw.trim().uppercase().replace("-", "")

    fun personId(raw: String): String? = normalize(raw).takeIf { person.matches(it) }
    fun vehicleId(raw: String): String? = normalize(raw).takeIf { vehicle.matches(it) }
}
