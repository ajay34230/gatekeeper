package com.teamxv.qrmonitor.ui

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.TextUnit
import androidx.compose.material3.LocalTextStyle

/**
 * Terminal display preferences: night (dark) mode and the Hindi / English switch. Kept on the phone only; both are
 * Compose state, so changing them re-draws every screen at once.
 */
object UiPrefs {
    private const val FILE = "xv_ui"
    var dark by mutableStateOf(false)
        private set
    var hindi by mutableStateOf(false)
        private set
    private var appContext: Context? = null

    fun init(context: Context) {
        appContext = context.applicationContext
        val p = context.getSharedPreferences(FILE, Context.MODE_PRIVATE)
        dark = p.getBoolean("dark", false)
        hindi = p.getBoolean("hindi", false)
    }

    fun updateDark(value: Boolean) { dark = value; save() }
    fun updateHindi(value: Boolean) { hindi = value; save() }

    private fun save() {
        appContext?.getSharedPreferences(FILE, Context.MODE_PRIVATE)?.edit()?.putBoolean("dark", dark)?.putBoolean("hindi", hindi)?.apply()
    }
}

/** The on-screen text in the chosen language (English is the source; anything without a translation stays as it is). */
fun tr(text: String): String = if (UiPrefs.hindi) Hindi[text] ?: text else text

/**
 * Every [Text] in this package goes through [tr], so labels, buttons and messages follow the language switch.
 * Same parameters as the Material 3 Text it forwards to; names and data that have no translation are shown unchanged.
 */
@Composable
internal fun Text(
    text: String,
    modifier: Modifier = Modifier,
    color: Color = Color.Unspecified,
    fontSize: TextUnit = TextUnit.Unspecified,
    fontStyle: FontStyle? = null,
    fontWeight: FontWeight? = null,
    fontFamily: FontFamily? = null,
    letterSpacing: TextUnit = TextUnit.Unspecified,
    textDecoration: TextDecoration? = null,
    textAlign: TextAlign? = null,
    lineHeight: TextUnit = TextUnit.Unspecified,
    overflow: TextOverflow = TextOverflow.Clip,
    softWrap: Boolean = true,
    maxLines: Int = Int.MAX_VALUE,
    minLines: Int = 1,
    onTextLayout: ((TextLayoutResult) -> Unit)? = null,
    style: TextStyle = LocalTextStyle.current
) = androidx.compose.material3.Text(
    text = tr(text), modifier = modifier, color = color, fontSize = fontSize, fontStyle = fontStyle, fontWeight = fontWeight,
    fontFamily = fontFamily, letterSpacing = letterSpacing, textDecoration = textDecoration, textAlign = textAlign,
    lineHeight = lineHeight, overflow = overflow, softWrap = softWrap, maxLines = maxLines, minLines = minLines,
    onTextLayout = onTextLayout, style = style
)

private val Hindi: Map<String, String> = mapOf(
    // Sign-in & pairing
    "XV DIGITAL ACCESS CONTROL" to "XV डिजिटल प्रवेश नियंत्रण",
    "FIELD ACCESS CONTROL SYSTEM" to "फील्ड प्रवेश नियंत्रण प्रणाली",
    "SIGN IN" to "साइन इन",
    "SIGN IN TO TERMINAL" to "टर्मिनल में साइन इन करें",
    "SIGNING IN…" to "साइन इन हो रहा है…",
    "CREATE ACCOUNT" to "खाता बनाएँ",
    "CREATING…" to "बनाया जा रहा है…",
    "RP ID" to "आरपी आईडी",
    "RP ID (OPTIONAL)" to "आरपी आईडी (वैकल्पिक)",
    "Enter your RP ID" to "अपनी आरपी आईडी दर्ज करें",
    "Leave blank to be assigned one" to "खाली छोड़ें, आईडी स्वतः मिलेगी",
    "PASSWORD" to "पासवर्ड",
    "CONFIRM PASSWORD" to "पासवर्ड की पुष्टि",
    "Your password" to "आपका पासवर्ड",
    "Repeat password" to "पासवर्ड दोबारा लिखें",
    "At least 6 characters" to "कम से कम 6 अक्षर",
    "FULL NAME" to "पूरा नाम",
    "Rank and name" to "रैंक और नाम",
    "SHOW" to "दिखाएँ",
    "HIDE" to "छिपाएँ",
    "PC Server Connection" to "पीसी सर्वर कनेक्शन",
    "System Status: Awaiting Pairing" to "स्थिति: पेयरिंग की प्रतीक्षा",
    "System Status: Paired • Encrypted Cache Ready" to "स्थिति: पेयर्ड • एन्क्रिप्टेड कैश तैयार",
    "SCAN PC PAIRING QR" to "पीसी पेयरिंग QR स्कैन करें",
    "Scan PC Pairing QR" to "पीसी पेयरिंग QR स्कैन करें",
    "RE-PAIR WITH PC (SCAN QR)" to "पीसी से दोबारा पेयर करें (QR स्कैन)",
    "RE-PAIR (SCAN NEW PC QR)" to "दोबारा पेयर करें (नया पीसी QR)",
    "OR PASTE PAIRING TEXT FROM THE PC" to "या पीसी से पेयरिंग टेक्स्ट पेस्ट करें",
    "PAIR WITH PASTED TEXT" to "पेस्ट किए टेक्स्ट से पेयर करें",
    "PAIRING…" to "पेयर हो रहा है…",
    "PAIRED COMMAND CENTER" to "पेयर्ड कमांड सेंटर",
    "UNPAIR THIS TERMINAL" to "इस टर्मिनल को अनपेयर करें",
    "On the PC: Local Wi-Fi & Pair Device" to "पीसी पर: लोकल वाई-फाई और डिवाइस पेयर",
    "On the PC open XV Command Center → 'Local Wi-Fi & Pair Device', then scan the QR shown there. Both devices must be on the same network (or Cloud Link must be set up on the PC)." to
        "पीसी पर XV कमांड सेंटर → 'Local Wi-Fi & Pair Device' खोलें और वहाँ दिखा QR स्कैन करें। दोनों डिवाइस एक ही नेटवर्क पर हों (या पीसी पर क्लाउड लिंक सेट हो)।",
    "CONTINUE OFFLINE SESSION" to "ऑफ़लाइन सत्र जारी रखें",
    "Pair first" to "पहले पेयर करें",

    // Navigation & home
    "Home" to "होम",
    "Activity" to "गतिविधि",
    "Sync" to "सिंक",
    "Operator" to "ऑपरेटर",
    "Comms" to "संचार",
    "SCAN PERSON" to "व्यक्ति स्कैन करें",
    "SCAN VEHICLE" to "वाहन स्कैन करें",
    "Staff, contractor & visitor badges" to "स्टाफ, ठेकेदार और आगंतुक बैज",
    "Trucks, cargo & multi-passenger manifest" to "ट्रक, माल और बहु-यात्री सूची",
    "Personnel On-Site Presence" to "परिसर में कार्मिक उपस्थिति",
    "Vehicle Fleet Presence" to "वाहन उपस्थिति",
    "Personnel registry is empty. Add personnel on the PC Command Center; this terminal receives them on the next sync." to
        "कार्मिक रजिस्टर खाली है। पीसी कमांड सेंटर पर कार्मिक जोड़ें; अगले सिंक पर यह टर्मिनल उन्हें प्राप्त करेगा।",
    "INSIDE" to "अंदर",
    "OUTSIDE" to "बाहर",
    "Inside" to "अंदर",
    "Outside" to "बाहर",
    "IN YARD" to "यार्ड में",
    "on site" to "परिसर में",
    "off site" to "परिसर से बाहर",
    "On site" to "परिसर में",
    "On-Site (Inside)" to "परिसर में (अंदर)",
    "Off-Site" to "परिसर से बाहर",
    "Back Online • Transmitting pending records..." to "फिर से ऑनलाइन • लंबित रिकॉर्ड भेजे जा रहे हैं...",
    "ONLINE" to "ऑनलाइन",
    "Online" to "ऑनलाइन",
    "Offline" to "ऑफ़लाइन",
    "● Online" to "● ऑनलाइन",
    "○ Offline" to "○ ऑफ़लाइन",
    "CONNECTING" to "कनेक्ट हो रहा है",

    // Activity
    "Recent Activity" to "हाल की गतिविधि",
    "Logged events for current operational shift" to "वर्तमान शिफ्ट में दर्ज घटनाएँ",
    "Search by personnel, vehicle ID, or gate..." to "कार्मिक, वाहन आईडी या गेट से खोजें...",
    "ALL" to "सभी",
    "All" to "सभी",
    "ENTRY" to "प्रवेश",
    "EXIT" to "निकास",
    "PERSON" to "व्यक्ति",
    "VEHICLE" to "वाहन",
    "Person" to "व्यक्ति",
    "Vehicle" to "वाहन",
    "No events recorded yet." to "अभी कोई घटना दर्ज नहीं है।",
    "No events matched" to "कोई घटना नहीं मिली",
    "Events recorded during your shift will appear here." to "आपकी शिफ्ट में दर्ज घटनाएँ यहाँ दिखेंगी।",
    "No activity" to "कोई गतिविधि नहीं",
    "Event ID" to "घटना आईडी",
    "Timestamp" to "समय",
    "Type" to "प्रकार",
    "Gate" to "गेट",
    "Location" to "स्थान",
    "Location / Gate" to "स्थान / गेट",
    "Location Flag" to "स्थान चेतावनी",
    "Device" to "डिवाइस",
    "Source" to "स्रोत",
    "Sync State" to "सिंक स्थिति",
    "Reason" to "कारण",
    "Remarks" to "टिप्पणी",
    "Expected Back" to "वापसी की तिथि",
    "Synchronized" to "सिंक हो गया",
    "SYNCHRONIZED" to "सिंक हो गया",
    "LOCAL BUFFER STORED" to "स्थानीय बफ़र में सुरक्षित",
    "Buffered Offline" to "ऑफ़लाइन सुरक्षित",
    "ATTENTION" to "ध्यान दें",

    // Sync
    "Sync & Network Hub" to "सिंक और नेटवर्क",
    "Terminal offline buffering & encrypted synchronization" to "टर्मिनल ऑफ़लाइन बफ़रिंग और एन्क्रिप्टेड सिंक",
    "Connected to Central Hub" to "केंद्रीय हब से जुड़ा",
    "Field Offline Mode" to "फील्ड ऑफ़लाइन मोड",
    "LOCAL DB" to "स्थानीय डेटाबेस",
    "Local secure cache & queue" to "स्थानीय सुरक्षित कैश और कतार",
    "PENDING BUFFER" to "लंबित बफ़र",
    "LAST SYNC" to "अंतिम सिंक",
    "UPLOADED LAST" to "पिछली बार भेजे",
    "Never" to "कभी नहीं",
    "All Synced" to "सब सिंक हो गया",
    "BATCH UPLOAD" to "एक साथ अपलोड",
    "Simultaneous buffer transmission" to "पूरा बफ़र एक साथ भेजें",
    "No pending events in local queue." to "स्थानीय कतार में कोई लंबित घटना नहीं।",
    "Connection verified. Ready to trigger batch upload of all offline events." to "कनेक्शन सत्यापित। सभी ऑफ़लाइन घटनाएँ भेजने के लिए तैयार।",
    "Internet connection required to transmit batch buffer." to "बफ़र भेजने के लिए कनेक्शन आवश्यक है।",
    "Records remain in the secure local buffer and synchronization resumes when network becomes available." to
        "रिकॉर्ड सुरक्षित स्थानीय बफ़र में रहते हैं और नेटवर्क मिलते ही सिंक फिर शुरू होता है।",
    "FORCE SYNC / DIAGNOSTIC PING" to "तुरंत सिंक / जाँच",
    "SYNC TARGET ARCHITECTURE" to "सिंक लक्ष्य",
    "Select where this handheld pushes gate records" to "चुनें कि यह टर्मिनल गेट रिकॉर्ड कहाँ भेजे",
    "Auto" to "स्वचालित",
    "LAN / Local" to "LAN / लोकल",
    "Cloud Server" to "क्लाउड सर्वर",
    "Local PC Wi-Fi" to "लोकल पीसी वाई-फाई",
    "Auto: local Wi-Fi first, internet address when away from base. Recommended." to "स्वचालित: पहले लोकल वाई-फाई, बेस से दूर होने पर इंटरनेट पता। अनुशंसित।",
    "Cloud Server: records travel over the internet to the Command Center's public address. Still end-to-end encrypted." to
        "क्लाउड सर्वर: रिकॉर्ड इंटरनेट से कमांड सेंटर के सार्वजनिक पते पर जाते हैं। फिर भी पूरी तरह एन्क्रिप्टेड।",
    "Data sharing: Full — the registry (names, ranks, units) is kept encrypted on this terminal for offline scanning." to
        "डेटा साझा: पूर्ण — ऑफ़लाइन स्कैन के लिए रजिस्टर (नाम, रैंक, यूनिट) इस टर्मिनल पर एन्क्रिप्टेड रहता है।",
    "Data sharing: Minimal — this terminal keeps only IDs and hashed badge codes; names are shown after an online check and never stored." to
        "डेटा साझा: न्यूनतम — यह टर्मिनल केवल आईडी और हैश कोड रखता है; नाम ऑनलाइन जाँच के बाद दिखते हैं और सहेजे नहीं जाते।",
    "Data sharing: Receive-only — no registry is stored on this terminal; every scan is verified online with the Command Center." to
        "डेटा साझा: केवल प्राप्त — इस टर्मिनल पर कोई रजिस्टर नहीं; हर स्कैन कमांड सेंटर से ऑनलाइन सत्यापित होता है।",

    // Operator & settings
    "RP Profile" to "आरपी प्रोफ़ाइल",
    "Active terminal operator credentials" to "वर्तमान टर्मिनल ऑपरेटर",
    "FIELD RP" to "फील्ड आरपी",
    "Unassigned Operator" to "अनिर्धारित ऑपरेटर",
    "Assigned Post" to "नियुक्त पोस्ट",
    "Shift Commenced" to "शिफ्ट शुरू",
    "Terminal ID" to "टर्मिनल आईडी",
    "Command Center" to "कमांड सेंटर",
    "Not paired" to "पेयर नहीं है",
    "Scanner audio feedback" to "स्कैनर ध्वनि",
    "CHANGE POST & CONNECTION" to "पोस्ट और कनेक्शन बदलें",
    "HANDOVER SHIFT / LOG OUT" to "शिफ्ट सौंपें / लॉग आउट",
    "Post & Connection" to "पोस्ट और कनेक्शन",
    "Reassign terminal post and manage the Command Center link" to "टर्मिनल पोस्ट बदलें और कमांड सेंटर लिंक प्रबंधित करें",
    "REASSIGN TERMINAL POST" to "टर्मिनल पोस्ट बदलें",
    "Station Location" to "स्टेशन स्थान",
    "Station Post" to "स्टेशन पोस्ट",
    "FACILITY POST" to "सुविधा पोस्ट",
    "ACCESS POINT" to "प्रवेश बिंदु",
    "Active Gate" to "सक्रिय गेट",
    "SERVER LINK" to "सर्वर लिंक",
    "CONNECT VIA CLOUD / INTERNET" to "क्लाउड / इंटरनेट से जुड़ें",
    "Internet address of the Command Center (https://…)" to "कमांड सेंटर का इंटरनेट पता (https://…)",
    "Filled automatically when you pair after 'Cloud Link' is set up on the PC." to "पीसी पर 'Cloud Link' सेट होने के बाद पेयर करने पर स्वतः भरता है।",
    "Address is a tunnel with a public certificate" to "पता सार्वजनिक प्रमाणपत्र वाली टनल है",
    "Certificate pin" to "प्रमाणपत्र पिन",
    "SAVE & TEST LINK" to "सहेजें और लिंक जाँचें",
    "TEST CONNECTION" to "कनेक्शन जाँचें",
    "Not tested" to "जाँचा नहीं गया",
    "Paired" to "पेयर्ड",
    "Server" to "सर्वर",
    "Allow alerts in the background (battery settings)" to "पृष्ठभूमि में अलर्ट की अनुमति दें (बैटरी सेटिंग)",

    // Person scan & result
    "SCAN PERSON QR" to "व्यक्ति का QR स्कैन करें",
    "Scan Person QR" to "व्यक्ति का QR स्कैन करें",
    "Align identity badge QR inside the reticle" to "पहचान बैज का QR फ्रेम के अंदर रखें",
    "Person Identified" to "व्यक्ति की पहचान हुई",
    "PERSONNEL RECORD" to "कार्मिक रिकॉर्ड",
    "Personnel record" to "कार्मिक रिकॉर्ड",
    "Personnel" to "कार्मिक",
    "ACTIVE" to "सक्रिय",
    "INACTIVE" to "निष्क्रिय",
    "Current Status" to "वर्तमान स्थिति",
    "Current presence" to "वर्तमान उपस्थिति",
    "Entry Time" to "प्रवेश समय",
    "Stay Duration" to "ठहराव अवधि",
    "Calculated Stay" to "गणना की गई अवधि",
    "Recorded Time" to "दर्ज समय",
    "QUICK VERIFICATION & LAST SEEN" to "त्वरित सत्यापन और अंतिम बार देखा",
    "RECORD ENTRY" to "प्रवेश दर्ज करें",
    "RECORD EXIT" to "निकास दर्ज करें",
    "LOG ENTRY" to "प्रवेश दर्ज करें",
    "REASON" to "कारण",
    "Select reason (optional)" to "कारण चुनें (वैकल्पिक)",
    "No reason" to "कोई कारण नहीं",
    "Custom…" to "अन्य…",
    "Type the reason" to "कारण लिखें",
    "Remarks (pass no., authority, destination…)" to "टिप्पणी (पास नं., प्राधिकारी, गंतव्य…)",
    "EXPECTED RETURN DATE" to "वापसी की अपेक्षित तिथि",
    "Tap to choose (required)" to "चुनने के लिए टैप करें (आवश्यक)",
    "Confirmation Required" to "पुष्टि आवश्यक",
    "CONFIRM" to "पुष्टि करें",
    "CANCEL" to "रद्द करें",
    "Cancel" to "रद्द करें",
    "Cancel & Scan Again" to "रद्द करें और फिर स्कैन करें",
    "CANCEL & RETURN HOME" to "रद्द करें और होम पर जाएँ",
    "OK" to "ठीक है",
    "CLOSE" to "बंद करें",
    "Back" to "वापस",
    "Select" to "चुनें",
    "Remove" to "हटाएँ",
    "None" to "कोई नहीं",
    "Scan Result" to "स्कैन परिणाम",
    "Verification outcome" to "सत्यापन परिणाम",
    "Verified" to "सत्यापित",
    "[Verified]" to "[सत्यापित]",
    "ACTION NOT COMPLETED" to "कार्य पूरा नहीं हुआ",
    "The event was not committed locally." to "घटना स्थानीय रूप से दर्ज नहीं हुई।",
    "Gate movement record generated" to "गेट आवाजाही रिकॉर्ड बन गया",
    "TOTAL VERIFIED STAY" to "कुल सत्यापित ठहराव",
    "SCAN NEXT TARGET" to "अगला स्कैन करें",
    "RETURN TO TERMINAL HOME" to "टर्मिनल होम पर लौटें",

    // Vehicle flow
    "SCAN VEHICLE QR" to "वाहन का QR स्कैन करें",
    "Scan Vehicle QR" to "वाहन का QR स्कैन करें",
    "Align vehicle windshield or registration QR in frame" to "वाहन के शीशे या पंजीकरण का QR फ्रेम में रखें",
    "Vehicle Identified" to "वाहन की पहचान हुई",
    "Vehicle credential verified" to "वाहन प्रमाण सत्यापित",
    "Vehicle currently registered as inside" to "वाहन वर्तमान में अंदर दर्ज है",
    "Vehicle Details" to "वाहन विवरण",
    "Vehicle ID" to "वाहन आईडी",
    "VEHICLE UNIT" to "वाहन",
    "Registration" to "पंजीकरण",
    "Vehicles" to "वाहन",
    "Vehicle Exit" to "वाहन निकास",
    "BUILD VEHICLE MANIFEST" to "वाहन सूची बनाएँ",
    "Scan Driver QR" to "चालक का QR स्कैन करें",
    "Scan authorized driver identity badge" to "अधिकृत चालक का पहचान बैज स्कैन करें",
    "Step 2 of 4 • Driver" to "चरण 2/4 • चालक",
    "Step 3 of 4 • Co-Driver" to "चरण 3/4 • सह-चालक",
    "Step 4 of 4 • Manifest Review" to "चरण 4/4 • सूची की जाँच",
    "Step 4 of 4: Manifest Verification" to "चरण 4/4: सूची सत्यापन",
    "Driver" to "चालक",
    "Co-driver" to "सह-चालक",
    "Co-Driver:" to "सह-चालक:",
    "Primary Driver" to "मुख्य चालक",
    "Primary Driver:" to "मुख्य चालक:",
    "Verified Driver" to "सत्यापित चालक",
    "VERIFIED DRIVER" to "सत्यापित चालक",
    "Driver verified. Continue to optional co-driver selection." to "चालक सत्यापित। वैकल्पिक सह-चालक चुनें।",
    "Is there a Co-Driver?" to "क्या सह-चालक है?",
    "Scan a co-driver badge for dual-driver commercial transit, or continue directly." to "दो चालकों वाले वाहन के लिए सह-चालक का बैज स्कैन करें, या सीधे जारी रखें।",
    "ADD OPTIONAL CO-DRIVER" to "सह-चालक जोड़ें (वैकल्पिक)",
    "SCAN CO-DRIVER QR" to "सह-चालक का QR स्कैन करें",
    "Scan Co-Driver QR" to "सह-चालक का QR स्कैन करें",
    "Align co-driver identity badge inside frame" to "सह-चालक का पहचान बैज फ्रेम में रखें",
    "NO CO-DRIVER • CONTINUE" to "सह-चालक नहीं • जारी रखें",
    "Occupant Manifest" to "सवारी सूची",
    "Additional Occupants" to "अतिरिक्त सवारी",
    "ADDITIONAL OCCUPANT" to "अतिरिक्त सवारी",
    "ADD ADDITIONAL PERSON QR" to "अतिरिक्त व्यक्ति का QR जोड़ें",
    "Scan Occupant QR" to "सवारी का QR स्कैन करें",
    "Align passenger identity badge inside frame" to "यात्री का पहचान बैज फ्रेम में रखें",
    "Total Persons Onboard" to "वाहन में कुल व्यक्ति",
    "MANIFEST VERIFICATION" to "सूची सत्यापन",
    "BACK TO VEHICLE" to "वाहन पर वापस",
    "CONFIRM VEHICLE ENTRY" to "वाहन प्रवेश की पुष्टि करें",
    "CONFIRM VEHICLE EXIT" to "वाहन निकास की पुष्टि करें",

    // Comms & SOS
    "Message to Command Center" to "कमांड सेंटर को संदेश",
    "Send" to "भेजें",
    "Send alert" to "अलर्ट भेजें",
    "Send as ALERT" to "अलर्ट के रूप में भेजें",
    "Send ALERT?" to "अलर्ट भेजें?",
    "The alert pops up on the Command Center screen with a sound. Use it for urgent situations." to "अलर्ट कमांड सेंटर की स्क्रीन पर ध्वनि के साथ दिखता है। केवल आपात स्थिति में उपयोग करें।",
    "Voice call" to "वॉयस कॉल",
    "Video call" to "वीडियो कॉल",
    "Connecting to the Comms engine…" to "संचार से जुड़ रहा है…",
    "No messages yet. Messages and alerts from the Command Center appear here and ring even when the app is closed." to
        "अभी कोई संदेश नहीं। कमांड सेंटर के संदेश और अलर्ट यहाँ दिखते हैं और ऐप बंद होने पर भी बजते हैं।",
    "You" to "आप",
    "Missed" to "छूटी हुई",
    "SENT" to "भेजा गया",
    "DELIVERED" to "पहुँच गया",
    "READ" to "पढ़ा गया",
    "SOS — EMERGENCY ALERT" to "SOS — आपातकालीन अलर्ट",
    "Send SOS?" to "SOS भेजें?",
    "SEND SOS" to "SOS भेजें",
    "An emergency alert with this post, your name and the phone's GPS position is sent to the Command Center at once." to
        "इस पोस्ट, आपके नाम और फ़ोन की GPS स्थिति के साथ आपातकालीन अलर्ट तुरंत कमांड सेंटर को भेजा जाएगा।",
    "Getting location…" to "स्थान प्राप्त किया जा रहा है…",
    "SOS sent to the Command Center" to "SOS कमांड सेंटर को भेज दिया गया",
    "SOS queued — it is sent the moment the link returns" to "SOS कतार में — लिंक लौटते ही भेजा जाएगा",

    // Display, language & shift handover
    "DISPLAY & LANGUAGE" to "प्रदर्शन और भाषा",
    "Night mode (dark)" to "रात्रि मोड (डार्क)",
    "Language" to "भाषा",
    "Shift Handover" to "शिफ्ट हस्तांतरण",
    "Summary of your shift before you log out" to "लॉग आउट से पहले आपकी शिफ्ट का सारांश",
    "SHIFT" to "शिफ्ट",
    "Entries" to "प्रवेश",
    "Exits" to "निकास",
    "Vehicle entries" to "वाहन प्रवेश",
    "Vehicle exits" to "वाहन निकास",
    "Not yet synced" to "अभी सिंक नहीं हुए",
    "STILL INSIDE" to "अभी भी अंदर",
    "VEHICLES STILL INSIDE" to "अभी भी अंदर वाहन",
    "No one is recorded inside." to "कोई भी अंदर दर्ज नहीं है।",
    "Records not yet synced stay safely on this terminal and are sent at the next connection." to "जो रिकॉर्ड सिंक नहीं हुए वे इस टर्मिनल पर सुरक्षित रहते हैं और अगले कनेक्शन पर भेजे जाते हैं।",
    "SEND TO COMMAND CENTER & LOG OUT" to "कमांड सेंटर को भेजें और लॉग आउट करें",
    "LOG OUT WITHOUT SENDING" to "भेजे बिना लॉग आउट करें",
    "Handover summary could not be queued." to "हस्तांतरण सारांश कतार में नहीं जुड़ सका।",
    "CAMERA ACCESS REQUIRED" to "कैमरा अनुमति आवश्यक",
    "Camera access is required to verify QR credentials on this terminal." to "इस टर्मिनल पर QR सत्यापन के लिए कैमरा अनुमति आवश्यक है।",
    "ALLOW CAMERA" to "कैमरा की अनुमति दें",
    "ENTER CODE MANUALLY" to "कोड स्वयं दर्ज करें",
    "Enter the QR payload exactly as printed on the credential." to "कार्ड पर छपा QR कोड हूबहू दर्ज करें।",
    "VERIFY CODE" to "कोड सत्यापित करें",
    "DIAGNOSTICS" to "निदान",
    "Crash reports" to "क्रैश रिपोर्ट",
    "Log size" to "लॉग आकार",
    "EXPORT LOGS" to "लॉग निर्यात करें",
    "PREPARING…" to "तैयार हो रहा है…",
    "CLEAR" to "साफ़ करें",
    "Export and send this file to support when the app misbehaves. Technical messages only — no personnel data or passwords." to "ऐप में समस्या होने पर यह फ़ाइल निर्यात कर सहायता को भेजें। केवल तकनीकी संदेश — कोई कार्मिक डेटा या पासवर्ड नहीं।",
    "Wipe notice" to "डेटा मिटाने की सूचना",
    "Visitor pass" to "आगंतुक पास",
)
