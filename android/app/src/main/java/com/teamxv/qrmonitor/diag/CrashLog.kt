package com.teamxv.qrmonitor.diag

import android.content.Context
import android.content.Intent
import android.os.Build
import android.util.Log
import androidx.core.content.FileProvider
import com.teamxv.qrmonitor.BuildConfig
import java.io.File
import java.io.PrintWriter
import java.io.StringWriter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Crash and log collector of the terminal. Crashes are saved as files (files/logs/crash-*.txt) before the app closes;
 * errors and warnings go to a rotating app log (1 MB). Settings → Diagnostics exports everything, plus the app's own
 * device log, as one text file to share. Technical messages only: no registry data, keys or passwords.
 */
object CrashLog {
    private const val MAX_LOG = 1_048_576L
    private const val MAX_CRASH_FILES = 20
    @Volatile private var dir: File? = null
    private val stamp get() = SimpleDateFormat("yyyy-MM-dd HH:mm:ss.SSS", Locale.US).format(Date())

    fun install(context: Context) {
        dir = File(context.applicationContext.filesDir, "logs").apply { mkdirs() }
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, error ->
            runCatching {
                val d = dir ?: return@runCatching
                d.listFiles { f -> f.name.startsWith("crash-") }?.sortedBy { it.name }?.dropLast(MAX_CRASH_FILES - 1)?.forEach { it.delete() }
                File(d, "crash-" + SimpleDateFormat("yyyyMMdd-HHmmss", Locale.US).format(Date()) + ".txt")
                    .writeText("CRASH $stamp on thread '${thread.name}'\n${deviceInfo()}\n\n${trace(error)}")
                write("FATAL", "Crash on thread ${thread.name}: $error")
            }
            previous?.uncaughtException(thread, error)
        }
        write("INFO ", "App started ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE}) on Android ${Build.VERSION.RELEASE}")
    }

    fun e(tag: String, message: String, error: Throwable? = null) {
        Log.e(tag, message, error)
        write("ERROR", "$tag: $message" + (error?.let { "\n" + trace(it) } ?: ""))
    }

    fun w(tag: String, message: String, error: Throwable? = null) {
        Log.w(tag, message, error)
        write("WARN ", "$tag: $message" + (error?.let { " ($it)" } ?: ""))
    }

    fun i(tag: String, message: String) { Log.i(tag, message); write("INFO ", "$tag: $message") }

    @Synchronized private fun write(level: String, message: String) {
        runCatching {
            val d = dir ?: return
            val f = File(d, "app.log")
            if (f.exists() && f.length() > MAX_LOG) { File(d, "app.1.log").delete(); f.renameTo(File(d, "app.1.log")) }
            f.appendText("$stamp $level $message\n")
        }
    }

    fun crashCount(): Int = dir?.listFiles { f -> f.name.startsWith("crash-") }?.size ?: 0

    fun sizeKb(): Long = (dir?.listFiles()?.sumOf { it.length() } ?: 0L) / 1024

    fun clear() { dir?.listFiles()?.forEach { it.delete() } }

    /** One text file with the system summary, every crash report, the app log and the app's own device log. */
    fun export(context: Context, summary: String): File {
        val out = File(context.cacheDir, "diag").apply { mkdirs(); listFiles()?.forEach { it.delete() } }
        val file = File(out, "XV-Terminal-logs-" + SimpleDateFormat("yyyyMMdd-HHmm", Locale.US).format(Date()) + ".txt")
        file.bufferedWriter().use { w ->
            w.write("XV DIGITAL ACCESS CONTROL — terminal diagnostics\nExported $stamp\n${deviceInfo()}\n$summary\n")
            val d = dir
            d?.listFiles { f -> f.name.startsWith("crash-") }?.sortedBy { it.name }?.forEach { w.write("\n==================== ${it.name}\n"); w.write(it.readText()) }
            listOf("app.1.log", "app.log").map { File(d, it) }.filter { it.exists() }.forEach { w.write("\n==================== ${it.name}\n"); w.write(it.readText()) }
            w.write("\n==================== device log (this app only)\n")
            runCatching {
                val p = Runtime.getRuntime().exec(arrayOf("logcat", "-d", "-v", "time", "-t", "3000", "--pid=${android.os.Process.myPid()}"))
                p.inputStream.bufferedReader().use { r -> r.lineSequence().forEach { line -> w.write(line); w.write("\n") } }
            }.onFailure { w.write("(device log unavailable: $it)\n") }
        }
        return file
    }

    /** Opens the share sheet (e-mail, messaging, Files…) for an exported log file. */
    fun share(context: Context, file: File) {
        val uri = FileProvider.getUriForFile(context, context.packageName + ".diag", file)
        val send = Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_STREAM, uri)
            .putExtra(Intent.EXTRA_SUBJECT, file.name).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        context.startActivity(Intent.createChooser(send, "Share diagnostic logs").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    private fun deviceInfo() = "App ${BuildConfig.APPLICATION_ID} ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})\n" +
        "Device ${Build.MANUFACTURER} ${Build.MODEL}, Android ${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})"

    private fun trace(t: Throwable) = StringWriter().also { t.printStackTrace(PrintWriter(it)) }.toString()
}
