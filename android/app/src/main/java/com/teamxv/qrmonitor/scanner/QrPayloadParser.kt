package com.teamxv.qrmonitor.scanner

/** Parses gate QR payloads. See EventRepository.lookupIdentity for how the code part is resolved. */
object QrPayloadParser {
    private val person = Regex("^P[0-9]{3,6}$")
    private val vehicle = Regex("^V[0-9]{3,6}$")

    data class Parsed(val code: String, val location: String)

    fun normalize(raw: String): String = raw.trim().uppercase().replace("-", "")

    fun personId(raw: String): String? = normalize(raw).takeIf { person.matches(it) }
    fun vehicleId(raw: String): String? = normalize(raw).takeIf { vehicle.matches(it) }

    /** "CODE", "CODE|Location 04", "Location 04|CODE" or JSON {"id": "...", "location": "..."}. */
    fun parse(raw: String): Parsed {
        val text = raw.trim()
        if (text.startsWith("XVGK1:")) return Parsed(text, "")
        if (text.startsWith("{")) {
            val id = Regex("\"(?:id|code|secretCode)\"\\s*:\\s*\"([^\"]+)\"").find(text)?.groupValues?.get(1)
            val loc = Regex("\"(?:location|locationId|loc)\"\\s*:\\s*\"([^\"]+)\"").find(text)?.groupValues?.get(1)
            if (id != null) return Parsed(id.trim(), loc?.trim() ?: "")
        }
        val parts = text.split('|', '@', '#', ';').map { it.trim() }.filter { it.isNotEmpty() }
        if (parts.size >= 2) {
            val locFirst = looksLikeLocation(parts[0]) && !looksLikeLocation(parts[1])
            return if (locFirst) Parsed(parts[1], parts[0]) else Parsed(parts[0], parts[1])
        }
        return Parsed(text, "")
    }

    private fun looksLikeLocation(s: String) = Regex("(?i)^(location|loc|post|site|station)[\\s\\-_#:]*\\d+").containsMatchIn(s)

    /** "Location 07", "LOC-07", "loc7", "07" → "LOC_7" so different spellings compare equal. */
    fun normalizeLocation(loc: String): String {
        val t = loc.trim().lowercase()
        Regex("(?:location|loc|post|site|station)[\\s\\-_#:]*0*(\\d+)").find(t)?.let { return "LOC_" + it.groupValues[1].toInt() }
        Regex("^0*(\\d+)$").find(t)?.let { return "LOC_" + it.groupValues[1].toInt() }
        return t.replace(Regex("[^a-z0-9]"), "")
    }
}
