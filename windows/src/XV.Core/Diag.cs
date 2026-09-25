using System.IO.Compression;
using System.Text;

namespace XV.Core;

/// <summary>
/// Diagnostic log of the Command Center (server, Comms, UI errors and crashes) in the data folder: logs/app.log,
/// rotated at 2 MB (one previous file kept). Holds technical messages only — no registry data, secrets or passwords.
/// Exported as a zip from Stations &amp; Settings → Diagnostics.
/// </summary>
public static class Diag
{
    const long MaxBytes = 2 * 1024 * 1024;
    static readonly object Gate = new();

    static string Dir => Directory.CreateDirectory(Paths.File("logs")).FullName;
    static string Current => Path.Combine(Dir, "app.log");

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : $"{message}\n{ex}");

    static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var path = Current;
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    var old = Path.Combine(Dir, "app.1.log");
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}\n", Encoding.UTF8);
            }
        }
        catch { /* logging must never break the program */ }
    }

    /// <summary>Writes a zip with the logs, the error log and a system summary; returns the number of files included.</summary>
    public static int Export(string zipPath, string systemSummary)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        var n = 0;
        lock (Gate)
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            void Add(string file, string name)
            {
                if (!File.Exists(file)) return;
                using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var dst = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                src.CopyTo(dst); n++;
            }
            Add(Path.Combine(Dir, "app.1.log"), "app.1.log");
            Add(Current, "app.log");
            Add(Paths.File("error.log"), "error.log");
            using (var w = new StreamWriter(zip.CreateEntry("system.txt").Open(), Encoding.UTF8)) w.Write(systemSummary);
            n++;
        }
        return n;
    }

    public static void Clear()
    {
        lock (Gate)
        {
            foreach (var f in new[] { Current, Path.Combine(Dir, "app.1.log"), Paths.File("error.log") })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    public static long SizeBytes()
    {
        long total = 0;
        foreach (var f in new[] { Current, Path.Combine(Dir, "app.1.log"), Paths.File("error.log") })
            try { if (File.Exists(f)) total += new FileInfo(f).Length; } catch { }
        return total;
    }
}
