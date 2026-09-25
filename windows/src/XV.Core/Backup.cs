using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace XV.Core;

/// <summary>
/// Password-protected full backup of a Command Center, restorable on another PC.
///
/// File (*.xvbackup): "XVBK1\n" + header JSON line + AES-256-GCM(payload), key = PBKDF2-SHA256(password, salt, 600 000).
/// The header (format, server, date, salt) is authenticated as AAD. The payload is a zip with the gate database and
/// the Comms database (each exported by SQLCipher under a random one-off key, so no plaintext copy ever touches the
/// disk), settings.json and the server certificate (so paired terminals keep working after a restore).
/// </summary>
public static class Backup
{
    const string Magic = "XVBK1";
    const int Iterations = 600_000;
    public const int MinPasswordLength = 10;

    /// <summary>Exports an open SQLCipher database into a new file encrypted with <paramref name="passphrase"/>.</summary>
    public static void ExportDatabase(SqliteConnection db, string path, string passphrase)
    {
        if (File.Exists(path)) File.Delete(path);
        using var c = db.CreateCommand();
        c.CommandText = $"ATTACH DATABASE '{path.Replace("'", "''")}' AS xvbackup KEY '{passphrase}'; SELECT sqlcipher_export('xvbackup'); DETACH DATABASE xvbackup;";
        c.ExecuteNonQuery();
    }

    static byte[] Key(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

    /// <summary>Writes the backup. <paramref name="exportGate"/> / <paramref name="exportComms"/> export the live databases.</summary>
    public static void Create(string file, string password, Settings settings, Action<string, string> exportGate, Action<string, string>? exportComms)
    {
        if (password.Length < MinPasswordLength) throw new ArgumentException($"Use a backup password of at least {MinPasswordLength} characters.");
        var work = Directory.CreateTempSubdirectory("xvbk-");
        try
        {
            var gateKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var commsKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var gatePath = Path.Combine(work.FullName, "gate.db");
            var commsPath = Path.Combine(work.FullName, "comms.db");
            exportGate(gatePath, gateKey);
            exportComms?.Invoke(commsPath, commsKey);

            using var zipMs = new MemoryStream();
            using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
            {
                void Add(string name, byte[] data) { using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); s.Write(data); }
                Add("meta.json", JsonSerializer.SerializeToUtf8Bytes(new { gateKey, commsKey = exportComms != null ? commsKey : "" }));
                Add("gate.db", File.ReadAllBytes(gatePath));
                if (exportComms != null) Add("comms.db", File.ReadAllBytes(commsPath));
                Add("settings.json", File.ReadAllBytes(Paths.File("settings.json")));
                var certFile = Paths.File("server-cert.pfx.bin");
                if (File.Exists(certFile)) Add("server-cert.pfx", Protector.Unprotect(File.ReadAllBytes(certFile)));
            }

            var salt = RandomNumberGenerator.GetBytes(16);
            var header = new JsonObject
            {
                ["format"] = Magic, ["serverId"] = settings.ServerId, ["serverName"] = settings.ServerName,
                ["created"] = DateTimeOffset.UtcNow.ToString("o"), ["kdf"] = "PBKDF2-SHA256", ["iterations"] = Iterations, ["salt"] = Convert.ToBase64String(salt),
            }.ToJsonString();
            var plain = zipMs.ToArray();
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length]; var tag = new byte[16];
            using (var gcm = new AesGcm(Key(password, salt), 16)) gcm.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(header));
            CryptographicOperations.ZeroMemory(plain);

            using var fs = File.Create(file);
            fs.Write(Encoding.UTF8.GetBytes(Magic + "\n" + header + "\n"));
            fs.Write(nonce); fs.Write(tag); fs.Write(cipher);
        }
        finally { try { work.Delete(true); } catch { } }
    }

    /// <summary>Reads the header without the password (to show what the file contains).</summary>
    public static JsonObject Describe(string file)
    {
        using var r = new StreamReader(File.OpenRead(file));
        if (r.ReadLine() != Magic) throw new InvalidDataException("This is not an XV Command Center backup file.");
        return JsonNode.Parse(r.ReadLine() ?? "") as JsonObject ?? throw new InvalidDataException("The backup header is damaged.");
    }

    /// <summary>
    /// Restores into the data folder. The Command Center must not have its databases open. The restored databases are
    /// re-keyed to this PC's own (DPAPI-protected) keys; the certificate is re-protected for this PC.
    /// </summary>
    /// <summary>Checks the password (and that the file is intact) without changing anything.</summary>
    public static void CheckPassword(string file, string password) => CryptographicOperations.ZeroMemory(Decrypt(file, password));

    static byte[] Decrypt(string file, string password)
    {
        var all = File.ReadAllBytes(file);
        var nl1 = Array.IndexOf(all, (byte)'\n');
        var nl2 = nl1 < 0 ? -1 : Array.IndexOf(all, (byte)'\n', nl1 + 1);
        if (nl1 < 0 || nl2 < 0 || Encoding.UTF8.GetString(all, 0, nl1) != Magic) throw new InvalidDataException("This is not an XV Command Center backup file.");
        var headerBytes = all.AsSpan(nl1 + 1, nl2 - nl1 - 1).ToArray();
        var header = JsonNode.Parse(headerBytes) as JsonObject ?? throw new InvalidDataException("The backup header is damaged.");
        var salt = Convert.FromBase64String(header["salt"]!.ToString());
        var body = all.AsSpan(nl2 + 1);
        if (body.Length < 28) throw new InvalidDataException("The backup file is incomplete.");
        var nonce = body[..12].ToArray(); var tag = body[12..28].ToArray(); var cipher = body[28..].ToArray();
        var plain = new byte[cipher.Length];
        try { using var gcm = new AesGcm(Key(password, salt), 16); gcm.Decrypt(nonce, cipher, tag, plain, headerBytes); }
        catch (CryptographicException) { throw new UnauthorizedAccessException("Wrong backup password, or the file was modified."); }
        return plain;
    }

    public static void Restore(string file, string password)
    {
        var plain = Decrypt(file, password);
        var work = Directory.CreateTempSubdirectory("xvrs-");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(plain), ZipArchiveMode.Read);
            byte[] Read(string name) { using var s = zip.GetEntry(name)?.Open() ?? throw new InvalidDataException($"The backup has no {name}."); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); }
            var meta = JsonNode.Parse(Read("meta.json"))!.AsObject();

            void RestoreDb(string entry, string tempKey, string targetName, string keyFile)
            {
                var tmp = Path.Combine(work.FullName, entry);
                File.WriteAllBytes(tmp, Read(entry));
                var machineKey = Convert.ToHexString(Protector.LoadOrCreate(keyFile, () => RandomNumberGenerator.GetBytes(32)));
                SQLitePCL.Batteries_V2.Init();
                using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = tmp, Mode = SqliteOpenMode.ReadWrite, Password = tempKey, Pooling = false }.ToString()))
                {
                    c.Open();
                    using var cmd = c.CreateCommand();
                    cmd.CommandText = "SELECT count(*) FROM sqlite_master"; cmd.ExecuteScalar(); // proves the key
                    cmd.CommandText = $"PRAGMA rekey = '{machineKey}';"; cmd.ExecuteNonQuery();
                }
                var target = Paths.File(targetName);
                foreach (var f in new[] { target, target + "-wal", target + "-shm" }) if (File.Exists(f)) File.Delete(f);
                File.Move(tmp, target);
            }

            RestoreDb("gate.db", meta["gateKey"]!.ToString(), "xv-access-control.db", "database.key.bin");
            if (zip.GetEntry("comms.db") != null && meta["commsKey"]?.ToString() is { Length: > 0 } ck) RestoreDb("comms.db", ck, "xv-comms.db", "comms.key.bin");
            File.WriteAllBytes(Paths.File("settings.json"), Read("settings.json"));
            if (zip.GetEntry("server-cert.pfx") != null) File.WriteAllBytes(Paths.File("server-cert.pfx.bin"), Protector.Protect(Read("server-cert.pfx")));
            var head = Paths.File("audit-head.bin");
            if (File.Exists(head)) File.Delete(head); // re-anchored from the restored chain on next start
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            try { work.Delete(true); } catch { }
        }
    }
}
