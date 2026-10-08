using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace XV.Core;

public sealed class StoreException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

/// <summary>
/// Encrypted SQLite (SQLCipher) store holding the registry, movement records, accounts and paired devices.
/// Starts completely empty: nothing is seeded. All access is serialized through one connection.
/// </summary>
public sealed partial class Store : IDisposable
{
    readonly SqliteConnection _db;
    readonly object _lock = new();
    public Settings Settings { get; }
    public event Action? Changed;

    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public Store(Settings settings, string? dbFile = null)
    {
        Settings = settings;
        SQLitePCL.Batteries_V2.Init();
        var key = Protector.LoadOrCreate("database.key.bin", () => RandomNumberGenerator.GetBytes(32));
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbFile ?? Paths.File("xv-access-control.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = Convert.ToHexString(key),
        }.ToString();
        _db = new SqliteConnection(cs);
        _db.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
        CreateSchema();
        Migrate();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Folds the write-ahead log into the main database file, so a plain file copy of it (for an automatic
    /// backup snapshot) is complete on its own -- nothing is left behind in a separate -wal file.</summary>
    public void Checkpoint() { lock (_lock) Exec("PRAGMA wal_checkpoint(TRUNCATE);"); }

    void CreateSchema() => Exec("""
        CREATE TABLE IF NOT EXISTS persons(id TEXT PRIMARY KEY, secret_code TEXT UNIQUE NOT NULL, name TEXT NOT NULL,
          rank TEXT NOT NULL DEFAULT '', service_no TEXT NOT NULL DEFAULT '', unit TEXT NOT NULL DEFAULT '', company TEXT NOT NULL DEFAULT '',
          role TEXT NOT NULL DEFAULT '', category TEXT NOT NULL DEFAULT 'PERSONNEL', status TEXT NOT NULL DEFAULT 'ACTIVE',
          mobile TEXT NOT NULL DEFAULT '', id_card TEXT NOT NULL DEFAULT '', blood_group TEXT NOT NULL DEFAULT '',
          access_locations TEXT NOT NULL DEFAULT '', notes TEXT NOT NULL DEFAULT '', created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS vehicles(id TEXT PRIMARY KEY, secret_code TEXT UNIQUE NOT NULL, plate TEXT NOT NULL,
          mil_reg TEXT NOT NULL DEFAULT '', type TEXT NOT NULL DEFAULT '', model TEXT NOT NULL DEFAULT '', company TEXT NOT NULL DEFAULT '',
          status TEXT NOT NULL DEFAULT 'ACTIVE', created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS locations(id TEXT PRIMARY KEY, name TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS gates(id TEXT PRIMARY KEY, name TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS events(event_id TEXT PRIMARY KEY, entity_type TEXT NOT NULL, entity_id TEXT NOT NULL, event_type TEXT NOT NULL,
          location_id TEXT NOT NULL, gate_id TEXT NOT NULL, device_id TEXT NOT NULL, operator_id TEXT NOT NULL, event_ts INTEGER NOT NULL,
          created_at INTEGER NOT NULL, received_at INTEGER NOT NULL, seq INTEGER NOT NULL UNIQUE, source_type TEXT NOT NULL DEFAULT 'DIRECT',
          source_id TEXT, loc_mismatch INTEGER NOT NULL DEFAULT 0, scanned_loc TEXT NOT NULL DEFAULT '', stay_ms INTEGER, payload_hash TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_events_ts ON events(event_ts);
        CREATE TABLE IF NOT EXISTS manifests(manifest_id TEXT PRIMARY KEY, vehicle_id TEXT NOT NULL, entry_event_id TEXT NOT NULL UNIQUE,
          location_id TEXT NOT NULL, gate_id TEXT NOT NULL, driver_id TEXT NOT NULL, co_driver_id TEXT, occupants TEXT NOT NULL,
          created_at INTEGER NOT NULL, state TEXT NOT NULL DEFAULT 'ACTIVE', exit_event_id TEXT, exit_at INTEGER);
        CREATE TABLE IF NOT EXISTS presence(session_id TEXT PRIMARY KEY, person_id TEXT NOT NULL, vehicle_id TEXT, source_type TEXT NOT NULL,
          source_id TEXT, entry_event_id TEXT NOT NULL, entry_at INTEGER NOT NULL, location_id TEXT NOT NULL, gate_id TEXT NOT NULL,
          status TEXT NOT NULL DEFAULT 'ACTIVE', exit_event_id TEXT, exit_at INTEGER);
        CREATE INDEX IF NOT EXISTS ix_presence_person ON presence(person_id, status);
        CREATE TABLE IF NOT EXISTS devices(device_id TEXT PRIMARY KEY, name TEXT NOT NULL, model TEXT NOT NULL DEFAULT '', key_b64 TEXT NOT NULL,
          active INTEGER NOT NULL DEFAULT 1, paired_at INTEGER NOT NULL, last_seen INTEGER NOT NULL DEFAULT 0, last_ip TEXT NOT NULL DEFAULT '',
          location_id TEXT NOT NULL DEFAULT '', gate_id TEXT NOT NULL DEFAULT '', operator_id TEXT NOT NULL DEFAULT '',
          pending INTEGER NOT NULL DEFAULT 0, app_version TEXT NOT NULL DEFAULT '', via TEXT NOT NULL DEFAULT '');
        CREATE TABLE IF NOT EXISTS accounts(id TEXT PRIMARY KEY, name TEXT NOT NULL, role TEXT NOT NULL, status TEXT NOT NULL,
          salt TEXT NOT NULL, hash TEXT NOT NULL, created_at INTEGER NOT NULL, created_by TEXT NOT NULL, last_login INTEGER);
        CREATE TABLE IF NOT EXISTS tokens(token_hash TEXT PRIMARY KEY, account_id TEXT NOT NULL, device_id TEXT NOT NULL, expires_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT, actor TEXT NOT NULL, action TEXT NOT NULL,
          entity_type TEXT, entity_id TEXT, detail TEXT, created_at INTEGER NOT NULL);
        """);

    /// <summary>Adds columns introduced after the first release without touching existing data.</summary>
    void Migrate()
    {
        void Ensure(string table, string column, string definition)
        {
            if (!Query($"PRAGMA table_info({table})").Any(r => string.Equals(S(r["name"]), column, StringComparison.OrdinalIgnoreCase)))
                Exec($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
        }
        Ensure("persons", "custom_json", "TEXT NOT NULL DEFAULT '{}'");
        Ensure("events", "remarks", "TEXT NOT NULL DEFAULT ''");
        Ensure("events", "source", "TEXT NOT NULL DEFAULT 'TERMINAL'");
        Ensure("events", "reason", "TEXT NOT NULL DEFAULT ''");
        // Where the person is coming from, set by the guard on ENTRY only -- shown on the gate record so the
        // Command Center knows which post/unit/location the person arrived from, not just that they arrived.
        Ensure("events", "coming_from", "TEXT NOT NULL DEFAULT ''");
        // Flags for duplicate entries, location mismatches, and other issues requiring attention (comma-separated)
        Ensure("events", "flags", "TEXT NOT NULL DEFAULT ''");
        foreach (var (_, col) in ExtraPersonFields) Ensure("persons", col, "TEXT NOT NULL DEFAULT ''");
        MigrateFeatures(Ensure);
        MigrateTransit();
        MigrateTransitNotice();
        // Session binding for tokens (Phase 2 security): bind tokens to IP address and created device
        Ensure("tokens", "bound_ip", "TEXT NOT NULL DEFAULT ''");
        Ensure("tokens", "created_at", "INTEGER NOT NULL DEFAULT 0");
        Exec("CREATE TABLE IF NOT EXISTS person_media(person_id TEXT PRIMARY KEY, photo BLOB, signature BLOB, updated_at INTEGER NOT NULL)");
        Ensure("person_media", "photo_at", "INTEGER NOT NULL DEFAULT 0");
        Ensure("person_media", "signed_at", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>Soldier details used by the ID card and the import/export files (JSON key → column).</summary>
    public static readonly (string key, string column)[] ExtraPersonFields =
    [
        ("platoon", "platoon"), ("section", "section"), ("address", "address"), ("dob", "dob"), ("enrolDate", "enrol_date"),
        ("expiryDate", "expiry_date"), ("idMark", "id_mark"), ("nokName", "nok_name"), ("nokRelation", "nok_relation"),
        ("nokPhone", "nok_phone"), ("cardSerial", "card_serial"),
        // visitor passes
        ("purpose", "pass_purpose"), ("host", "pass_host"), ("idProof", "id_proof"),
    ];

    // ------------------------------------------------------------------ low level helpers

    SqliteCommand Cmd(string sql, object?[] args)
    {
        var c = _db.CreateCommand();
        c.CommandText = sql;
        for (var i = 0; i < args.Length; i++) c.Parameters.AddWithValue("$" + (i + 1), args[i] ?? DBNull.Value);
        return c;
    }

    int Exec(string sql, params object?[] args) { lock (_lock) { using var c = Cmd(sql, args); return c.ExecuteNonQuery(); } }

    object? Scalar(string sql, params object?[] args)
    {
        lock (_lock) { using var c = Cmd(sql, args); var v = c.ExecuteScalar(); return v is DBNull ? null : v; }
    }

    long Count(string sql, params object?[] args) => Convert.ToInt64(Scalar(sql, args) ?? 0L);

    public List<Dictionary<string, object?>> Query(string sql, params object?[] args)
    {
        lock (_lock)
        {
            using var c = Cmd(sql, args);
            using var r = c.ExecuteReader();
            var rows = new List<Dictionary<string, object?>>();
            while (r.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }
    }

    Dictionary<string, object?>? One(string sql, params object?[] args) => Query(sql, args).FirstOrDefault();

    /// <summary>Runs <paramref name="work"/> inside one transaction; rolls back on any exception.</summary>
    T Tx<T>(Func<T> work)
    {
        lock (_lock)
        {
            Exec("BEGIN IMMEDIATE");
            try { var r = work(); Exec("COMMIT"); return r; }
            catch { Exec("ROLLBACK"); throw; }
        }
    }

    /// <summary>Appends a hash-chained audit entry (see StoreFeatures.cs → tamper-evident audit trail).</summary>
    void Audit(string actor, string action, string? type = null, string? id = null, string? detail = null) => AppendAudit(actor, action, type, id, detail);

    /// <summary>Records an administrator action taken in the Command Center (exports, data-protection changes).</summary>
    public void AdminAudit(string action, string? detail = null) => Audit("ADMIN@" + Environment.MachineName, action, null, null, detail);

    void Notify() { try { Changed?.Invoke(); } catch { /* UI listeners must not break the server */ } }

    static string S(object? o) => o?.ToString() ?? "";
    static string Upper(string? s) => (s ?? "").Trim().ToUpperInvariant();

    const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public static string RandomCode(int len)
    {
        var sb = new StringBuilder(len);
        for (var i = 0; i < len; i++) sb.Append(CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]);
        return sb.ToString();
    }

    /// <summary>Canonical ID form used everywhere: letters+digits, no separators (P-001 → P001).</summary>
    public static string CanonId(string? raw) => new string(Upper(raw).Where(char.IsLetterOrDigit).ToArray());

    // ------------------------------------------------------------------ registry

    public List<Dictionary<string, object?>> Persons(string q = "") => Query("""
        SELECT p.*, (SELECT s.entry_at FROM presence s WHERE s.person_id=p.id AND s.status='ACTIVE' ORDER BY s.entry_at DESC LIMIT 1) AS inside_since,
               (SELECT MAX(e.event_ts) FROM events e WHERE e.entity_type='PERSON' AND e.entity_id=p.id) AS last_seen
        FROM persons p WHERE $1='' OR p.id LIKE $2 OR p.name LIKE $2 OR p.service_no LIKE $2 OR p.unit LIKE $2 OR p.company LIKE $2
        ORDER BY p.id
        """, q.Trim(), "%" + q.Trim() + "%");

    public Dictionary<string, object?>? Person(string id) => One("SELECT * FROM persons WHERE id=$1", CanonId(id));

    public List<Dictionary<string, object?>> Vehicles(string q = "") => Query("""
        SELECT v.*, (SELECT m.created_at FROM manifests m WHERE m.vehicle_id=v.id AND m.state='ACTIVE' LIMIT 1) AS inside_since
        FROM vehicles v WHERE $1='' OR v.id LIKE $2 OR v.plate LIKE $2 OR v.type LIKE $2 OR v.company LIKE $2 ORDER BY v.id
        """, q.Trim(), "%" + q.Trim() + "%");

    public string NextId(string prefix, string table)
    {
        if (table is not ("persons" or "vehicles")) throw new ArgumentException("Unknown table", nameof(table));
        var ids = Query($"SELECT id FROM {table}").Select(r => S(r["id"])).Where(i => i.StartsWith(prefix)).Select(i => int.TryParse(i[prefix.Length..], out var n) ? n : 0);
        return prefix + ((ids.DefaultIfEmpty(0).Max()) + 1).ToString("000");
    }

    public void UpsertPerson(JsonObject p, string actor = "PC-ADMIN")
    {
        var id = CanonId(p["id"]?.ToString());
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[PG][0-9]{3,6}$")) throw new StoreException("INVALID_ID", "Personnel ID must look like P001 (visitor passes G0001)", 400);
        var name = (p["name"]?.ToString() ?? "").Trim();
        if (name.Length < 2) throw new StoreException("INVALID_NAME", "Name is required", 400);
        var now = NowMs;
        var existing = One("SELECT secret_code FROM persons WHERE id=$1", id);
        var secret = existing != null ? S(existing["secret_code"]) : "XVP" + RandomCode(10);
        Exec("""
            INSERT INTO persons(id,secret_code,name,rank,service_no,unit,company,role,category,status,mobile,id_card,blood_group,access_locations,notes,created_at,updated_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$16)
            ON CONFLICT(id) DO UPDATE SET name=$3,rank=$4,service_no=$5,unit=$6,company=$7,role=$8,category=$9,status=$10,mobile=$11,
              id_card=$12,blood_group=$13,access_locations=$14,notes=$15,updated_at=$16
            """, id, secret, name, T(p, "rank"), T(p, "serviceNo"), T(p, "unit"), T(p, "company"), T(p, "role"),
            Upper(T(p, "category")) is { Length: > 0 } c ? c : "PERSONNEL", Upper(T(p, "status")) is { Length: > 0 } st ? st : "ACTIVE",
            T(p, "mobile"), T(p, "idCard"), T(p, "bloodGroup"), T(p, "accessLocations"), T(p, "notes"), now);
        foreach (var (key, col) in ExtraPersonFields)
            if (p.ContainsKey(key)) Exec($"UPDATE persons SET {col}=$1 WHERE id=$2", T(p, key), id);
        if (p["custom"] is JsonObject custom)
        {
            // Merge so an import that carries only some custom columns keeps the others.
            var merged = JsonNode.Parse(S(Scalar("SELECT custom_json FROM persons WHERE id=$1", id)) is { Length: > 1 } cj ? cj : "{}") as JsonObject ?? new JsonObject();
            foreach (var kv in custom) merged[kv.Key] = kv.Value?.ToString().Trim() ?? "";
            Exec("UPDATE persons SET custom_json=$1 WHERE id=$2", merged.ToJsonString(), id);
        }
        if (p["validFrom"] != null) Exec("UPDATE persons SET valid_from=$1 WHERE id=$2", p["validFrom"]!.GetValue<long>(), id);
        if (p["validTo"] != null) Exec("UPDATE persons SET valid_to=$1 WHERE id=$2", p["validTo"]!.GetValue<long>(), id);
        if (existing == null) CardEvent(id, "ISSUED", "", actor);

        // Create initial presence session if location is specified
        var initialLoc = T(p, "initialLocation");
        if (!string.IsNullOrWhiteSpace(initialLoc))
        {
            // Close any existing presence session for this person
            Exec("UPDATE presence SET status='CLOSED', exit_at=$1 WHERE person_id=$2 AND status='ACTIVE'", now, id);

            var syntheticEventId = "INIT-" + id + "-" + now;
            var sessionId = "SES-" + syntheticEventId;
            Exec("INSERT INTO presence(session_id,person_id,source_type,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,'DIRECT',$3,$4,$5,$6)",
                sessionId, id, syntheticEventId, now, Upper(initialLoc), "");
        }

        Audit(actor, existing == null ? "ADD_PERSON" : "EDIT_PERSON", "PERSON", id);
        Notify();
    }

    // ------------------------------------------------------------------ photos & signatures (kept out of list queries)

    public byte[]? PersonPhoto(string id) => Scalar("SELECT photo FROM person_media WHERE person_id=$1", id) as byte[];
    public byte[]? PersonSignature(string id) => Scalar("SELECT signature FROM person_media WHERE person_id=$1", id) as byte[];

    public void SetPersonPhoto(string id, byte[]? jpeg, string actor = "PC-ADMIN")
    {
        Exec("INSERT INTO person_media(person_id,photo,updated_at,photo_at) VALUES($1,$2,$3,$4) ON CONFLICT(person_id) DO UPDATE SET photo=$2, updated_at=$3, photo_at=$4",
            id, jpeg, NowMs, jpeg == null ? 0 : NowMs);
        Audit(actor, jpeg == null ? "REMOVE_PHOTO" : "SET_PHOTO", "PERSON", id);
        Notify();
    }

    public void SetPersonSignature(string id, byte[]? png, string actor = "PC-ADMIN")
    {
        Exec("INSERT INTO person_media(person_id,signature,updated_at,signed_at) VALUES($1,$2,$3,$4) ON CONFLICT(person_id) DO UPDATE SET signature=$2, updated_at=$3, signed_at=$4",
            id, png, NowMs, png == null ? 0 : NowMs);
        Audit(actor, png == null ? "REMOVE_SIGNATURE" : "SET_SIGNATURE", "PERSON", id);
        Notify();
    }

    /// <summary>Per person: when the photo and the signature were last set (0 = none).</summary>
    public Dictionary<string, (long photoAt, long signedAt)> MediaVersions() =>
        Query("SELECT person_id, photo_at, signed_at FROM person_media").ToDictionary(r => S(r["person_id"]), r => (Convert.ToInt64(r["photo_at"]), Convert.ToInt64(r["signed_at"])));

    public HashSet<string> PersonsWithPhoto() => Query("SELECT person_id FROM person_media WHERE photo IS NOT NULL").Select(r => S(r["person_id"])).ToHashSet();

    public void UpsertVehicle(JsonObject v, string actor = "PC-ADMIN")
    {
        var id = CanonId(v["id"]?.ToString());
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^V[0-9]{3,6}$")) throw new StoreException("INVALID_ID", "Vehicle ID must look like V001", 400);
        var plate = Upper(T(v, "plate"));
        if (plate.Length < 2) throw new StoreException("INVALID_PLATE", "Registration plate is required", 400);
        var now = NowMs;
        var existing = One("SELECT secret_code FROM vehicles WHERE id=$1", id);
        var secret = existing != null ? S(existing["secret_code"]) : "XVV" + RandomCode(10);
        Exec("""
            INSERT INTO vehicles(id,secret_code,plate,mil_reg,type,model,company,status,created_at,updated_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$9)
            ON CONFLICT(id) DO UPDATE SET plate=$3,mil_reg=$4,type=$5,model=$6,company=$7,status=$8,updated_at=$9
            """, id, secret, plate, T(v, "milReg"), T(v, "type"), T(v, "model"), T(v, "company"),
            Upper(T(v, "status")) is { Length: > 0 } st ? st : "ACTIVE", now);
        Audit(actor, existing == null ? "ADD_VEHICLE" : "EDIT_VEHICLE", "VEHICLE", id);
        Notify();
    }

    static string T(JsonObject o, string k) => (o[k]?.ToString() ?? "").Trim();

    public void DeletePerson(string id) { Exec("DELETE FROM persons WHERE id=$1", id); Exec("DELETE FROM person_media WHERE person_id=$1", id); Audit("PC-ADMIN", "DELETE_PERSON", "PERSON", id); Notify(); }
    public void DeleteVehicle(string id) { Exec("DELETE FROM vehicles WHERE id=$1", id); Audit("PC-ADMIN", "DELETE_VEHICLE", "VEHICLE", id); Notify(); }

    /// <summary>Issues a new QR secret, instantly invalidating the old printed credential.</summary>
    public void RotateSecret(string table, string id, string reason = "Re-issued")
    {
        if (table is not ("persons" or "vehicles")) throw new ArgumentException("Unknown table", nameof(table));
        Exec($"UPDATE {table} SET secret_code=$1, updated_at=$2 WHERE id=$3", (table == "persons" ? "XVP" : "XVV") + RandomCode(10), NowMs, id);
        if (table == "persons") CardEvent(id, "REISSUED", reason, "PC-ADMIN");
        Audit("PC-ADMIN", "ROTATE_QR_SECRET", table, id, reason); Notify();
    }

    public List<Dictionary<string, object?>> Locations() => Query("SELECT id,name FROM locations ORDER BY id");
    public List<Dictionary<string, object?>> Gates() => Query("SELECT id,name FROM gates ORDER BY id");

    public void UpsertLocation(string id, string name) { id = CanonId(id); if (id.Length == 0 || name.Trim().Length == 0) throw new StoreException("INVALID", "ID and name are required", 400); Exec("INSERT INTO locations(id,name) VALUES($1,$2) ON CONFLICT(id) DO UPDATE SET name=$2", id, name.Trim()); Notify(); }
    public void UpsertGate(string id, string name) { id = CanonId(id); if (id.Length == 0 || name.Trim().Length == 0) throw new StoreException("INVALID", "ID and name are required", 400); Exec("INSERT INTO gates(id,name) VALUES($1,$2) ON CONFLICT(id) DO UPDATE SET name=$2", id, name.Trim()); Notify(); }
    public void DeleteLocation(string id) { Exec("DELETE FROM locations WHERE id=$1", id); Notify(); }
    public void DeleteGate(string id) { Exec("DELETE FROM gates WHERE id=$1", id); Notify(); }

    // ------------------------------------------------------------------ operator accounts

    static string HashPassword(string password, string saltHex) =>
        Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(saltHex), 120_000, HashAlgorithmName.SHA256, 32));

    public List<Dictionary<string, object?>> Accounts() => Query("SELECT id,name,role,status,created_at,created_by,last_login FROM accounts ORDER BY status='PENDING' DESC, id");

    /// <summary>Creates an operator account. Returns the generated password when none was supplied.</summary>
    public (string id, string? generated) CreateAccount(string name, string? id, string? password, string createdBy, string role = "RP")
    {
        name = (name ?? "").Trim();
        if (name.Length < 2 || name.Length > 60) throw new StoreException("INVALID_NAME", "Name must be 2 to 60 characters", 400);
        id = Upper(id);
        if (id.Length == 0)
        {
            var nums = Query("SELECT id FROM accounts").Select(r => S(r["id"])).Where(x => x.StartsWith("GK-")).Select(x => int.TryParse(x[3..], out var n) ? n : 0);
            id = "GK-" + (Math.Max(100, nums.DefaultIfEmpty(0).Max()) + 1);
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Z0-9-]{3,20}$"))
            throw new StoreException("INVALID_ID", "ID must be 3-20 letters, digits or dashes", 400);
        if (Count("SELECT COUNT(*) FROM accounts WHERE id=$1", id) > 0) throw new StoreException("ID_TAKEN", $"ID {id} is already registered", 409);
        string? generated = null;
        if (string.IsNullOrEmpty(password)) password = generated = RandomCode(10);
        if (password.Length < 6) throw new StoreException("WEAK_PASSWORD", "Password must be at least 6 characters", 400);
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var status = createdBy == "SELF" && Settings.RequireApproval ? "PENDING" : "ACTIVE";
        Exec("INSERT INTO accounts(id,name,role,status,salt,hash,created_at,created_by) VALUES($1,$2,$3,$4,$5,$6,$7,$8)",
            id, name, role, status, salt, HashPassword(password, salt), NowMs, createdBy);
        Audit(createdBy, "CREATE_ACCOUNT", "ACCOUNT", id, status);
        Notify();
        return (id, generated);
    }

    public string AccountStatus(string id) => S(Scalar("SELECT status FROM accounts WHERE id=$1", Upper(id)));

    public void SetAccountStatus(string id, string status)
    {
        Exec("UPDATE accounts SET status=$1 WHERE id=$2", status, id);
        if (status != "ACTIVE") Exec("DELETE FROM tokens WHERE account_id=$1", id);
        Audit("PC-ADMIN", "ACCOUNT_" + status, "ACCOUNT", id); Notify();
    }

    public string ResetPassword(string id)
    {
        var pw = RandomCode(10); var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Exec("UPDATE accounts SET salt=$1, hash=$2 WHERE id=$3", salt, HashPassword(pw, salt), id);
        Exec("DELETE FROM tokens WHERE account_id=$1", id);
        Audit("PC-ADMIN", "RESET_PASSWORD", "ACCOUNT", id); Notify();
        return pw;
    }

    public void DeleteAccount(string id) { Exec("DELETE FROM accounts WHERE id=$1", id); Exec("DELETE FROM tokens WHERE account_id=$1", id); Audit("PC-ADMIN", "DELETE_ACCOUNT", "ACCOUNT", id); Notify(); }

    readonly Dictionary<string, (int n, long first)> _failures = new();

    public JsonObject Login(string idRaw, string password, string deviceId, string ip = "")
    {
        var id = Upper(idRaw);
        lock (_failures)
            if (_failures.TryGetValue(id + deviceId, out var f) && f.n >= 5 && NowMs - f.first < 600_000)
                throw new StoreException("TOO_MANY_ATTEMPTS", "Too many failed attempts. Try again in 10 minutes.", 429);
        var a = One("SELECT * FROM accounts WHERE id=$1", id);
        var ok = a != null && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashPassword(password ?? "", S(a["salt"]))), Convert.FromHexString(S(a["hash"])));
        if (!ok)
        {
            lock (_failures) { _failures.TryGetValue(id + deviceId, out var f); _failures[id + deviceId] = (NowMs - f.first > 600_000 ? 1 : f.n + 1, f.n == 0 || NowMs - f.first > 600_000 ? NowMs : f.first); }
            throw new StoreException("INVALID_CREDENTIALS", "Invalid RP ID or password", 401);
        }
        lock (_failures) _failures.Remove(id + deviceId);
        var status = S(a!["status"]);
        if (status != "ACTIVE") throw new StoreException("INVALID_CREDENTIALS", "Invalid RP ID or password", 401);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        // Token lifetime set by Settings.TokenHours (default 8 hours)
        var expires = NowMs + Settings.TokenHours * 3_600_000L;
        Exec("INSERT INTO tokens(token_hash,account_id,device_id,expires_at,bound_ip,created_at) VALUES($1,$2,$3,$4,$5,$6)", Sha(token), id, deviceId, expires, ip, NowMs);
        Exec("UPDATE accounts SET last_login=$1 WHERE id=$2", NowMs, id);
        Audit(id, "OPERATOR_LOGIN", "DEVICE", deviceId);
        Notify();
        return new JsonObject
        {
            ["status"] = "ok", ["username"] = id, ["name"] = S(a["name"]), ["role"] = S(a["role"]),
            ["accessToken"] = token, ["expiresAt"] = expires, ["offlineGraceSeconds"] = Settings.OfflineGraceHours * 3600,
        };
    }

    public void Logout(string token) => Exec("DELETE FROM tokens WHERE token_hash=$1", Sha(token));

    /// <summary>Returns the operator id for a valid token issued to this device, else null.</summary>
    public string? OperatorFor(string? token, string deviceId, string ip = "")
    {
        if (string.IsNullOrEmpty(token)) return null;
        var r = One("SELECT t.account_id, t.expires_at, t.bound_ip, a.status FROM tokens t JOIN accounts a ON a.id=t.account_id WHERE t.token_hash=$1 AND t.device_id=$2", Sha(token), deviceId);
        if (r == null || Convert.ToInt64(r["expires_at"]) < NowMs || S(r["status"]) != "ACTIVE") return null;
        // Session binding: token must be used from the same IP address
        var boundIp = S(r["bound_ip"]);
        if (!string.IsNullOrEmpty(boundIp) && boundIp != ip) return null;
        return S(r["account_id"]);
    }

    static string Sha(string v) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v)));

    // ------------------------------------------------------------------ device pairing

    readonly Dictionary<string, long> _pairCodes = new();

    /// <summary>One-time pairing code shown in the pairing QR; valid 10 minutes.</summary>
    public string NewPairCode()
    {
        var code = RandomCode(8);
        lock (_pairCodes) { foreach (var k in _pairCodes.Where(k => k.Value < NowMs).Select(k => k.Key).ToList()) _pairCodes.Remove(k); _pairCodes[code] = NowMs + 600_000; }
        return code;
    }

    public JsonObject Enroll(string code, string name, string model, string ip)
    {
        lock (_pairCodes)
        {
            if (!_pairCodes.TryGetValue(Upper(code), out var exp) || exp < NowMs)
                throw new StoreException("INVALID_PAIR_CODE", "Pairing code is invalid or expired. Show a new QR on the PC.", 403);
            _pairCodes.Remove(Upper(code));
        }
        var deviceId = "DEV-" + RandomCode(6);
        var key = RandomNumberGenerator.GetBytes(32);
        Exec("INSERT INTO devices(device_id,name,model,key_b64,active,paired_at,last_ip) VALUES($1,$2,$3,$4,1,$5,$6)",
            deviceId, string.IsNullOrWhiteSpace(name) ? "Android terminal" : name.Trim()[..Math.Min(40, name.Trim().Length)], model ?? "", Convert.ToBase64String(key), NowMs, ip);
        Audit("PC-ADMIN", "PAIR_DEVICE", "DEVICE", deviceId, ip);
        Notify();
        return new JsonObject { ["deviceId"] = deviceId, ["deviceKey"] = Convert.ToBase64String(key), ["serverId"] = Settings.ServerId, ["serverName"] = Settings.ServerName };
    }

    public byte[]? DeviceKey(string deviceId)
    {
        var k = Scalar("SELECT key_b64 FROM devices WHERE device_id=$1 AND active=1", deviceId);
        return k == null ? null : Convert.FromBase64String(S(k));
    }

    public List<Dictionary<string, object?>> Devices() => Query("SELECT device_id,name,model,active,paired_at,last_seen,last_ip,location_id,gate_id,operator_id,pending,app_version,via FROM devices ORDER BY active DESC, last_seen DESC");

    public void RevokeDevice(string id) { Exec("UPDATE devices SET active=0 WHERE device_id=$1", id); Exec("DELETE FROM tokens WHERE device_id=$1", id); Audit("PC-ADMIN", "REVOKE_DEVICE", "DEVICE", id); Notify(); }
    public void DeleteDevice(string id)
    {
        // A removed terminal is remembered so it is still told to erase itself if it ever connects again.
        Exec("INSERT OR IGNORE INTO removed_devices(device_id,removed_at) VALUES($1,$2)", id, NowMs);
        Exec("DELETE FROM devices WHERE device_id=$1", id); Exec("DELETE FROM tokens WHERE device_id=$1", id);
        Audit("PC-ADMIN", "DELETE_DEVICE", "DEVICE", id); Notify();
    }

    public void Heartbeat(string deviceId, JsonObject p, string ip, bool viaInternet)
    {
        Exec("UPDATE devices SET last_seen=$1,last_ip=$2,location_id=$3,gate_id=$4,operator_id=$5,pending=$6,app_version=$7,via=$8 WHERE device_id=$9",
            NowMs, ip, Upper(T(p, "locationId")), Upper(T(p, "gateId")), Upper(T(p, "operatorId")),
            int.TryParse(T(p, "pending"), out var n) ? n : 0, T(p, "appVersion"), viaInternet ? "INTERNET" : "LAN", deviceId);
        Notify();
    }

    public void Touch(string deviceId, string ip, bool viaInternet) =>
        Exec("UPDATE devices SET last_seen=$1,last_ip=$2,via=$3 WHERE device_id=$4", NowMs, ip, viaInternet ? "INTERNET" : "LAN", deviceId);

    // ------------------------------------------------------------------ master data for terminals

    /// <summary>SHA-256 of a QR secret, so terminals can verify a badge without holding the secret itself (Minimal mode).</summary>
    public static string SecretHash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((code ?? "").Trim().ToUpperInvariant()))).ToLowerInvariant();

    /// <summary>Privacy-safe name for anything sent to a phone: "Ajay Singh" → "A SINGH", "Ajay Singh Shekhawat" → "AS SHEKHAWAT", a single word is unchanged.</summary>
    public static string AbbrevName(string? name)
    {
        var parts = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return (name ?? "").Trim();
        var initials = string.Concat(parts[..^1].Select(p => char.ToUpperInvariant(p[0])));
        return initials + " " + parts[^1].ToUpperInvariant();
    }

    /// <summary>
    /// Registry sent to terminals, limited by the Data Sharing mode chosen by the administrator:
    ///   FULL         – abbreviated name, rank, company, QR secret (fully offline scanning); never the
    ///                  army/service number or unit designation, which stay PC-side regardless of mode
    ///   MINIMAL      – IDs, status and hashed QR secrets only; no names or personal details
    ///   RECEIVE_ONLY – no registry at all; every scan is verified online and nothing is stored on the phone
    /// Active vehicle manifests and presence are IDs only and are shared in FULL and MINIMAL modes.
    /// Sent over the server's TLS-only Kestrel listener (ApiServer), so this is always encrypted in transit.
    /// </summary>
    public JsonObject Bootstrap()
    {
        var mode = Settings.DataSharing;
        var full = mode == "FULL";
        var shareRegistry = mode != "RECEIVE_ONLY";
        return new JsonObject
        {
            ["sharingMode"] = mode,
            ["version"] = S(Scalar("SELECT MAX(updated_at) FROM (SELECT updated_at FROM persons UNION ALL SELECT updated_at FROM vehicles)")),
            ["serverTime"] = NowMs,
            ["persons"] = new JsonArray(!shareRegistry ? [] : Query("SELECT id,secret_code,name,rank,service_no,unit,company,role,category,status,access_locations,valid_from,valid_to FROM persons ORDER BY id").Select(r => (JsonNode)new JsonObject
            {
                ["personId"] = S(r["id"]), ["secretCode"] = full ? S(r["secret_code"]) : "", ["secretHash"] = SecretHash(S(r["secret_code"])),
                ["name"] = full ? AbbrevName(S(r["name"])) : "", ["rank"] = full ? S(r["rank"]) : "", ["serviceNo"] = "",
                ["unit"] = "", ["company"] = full ? S(r["company"]) : "", ["role"] = full ? S(r["role"]) : "",
                ["category"] = S(r["category"]), ["status"] = S(r["status"]), ["active"] = S(r["status"]) == "ACTIVE",
                ["accessLocations"] = S(r["access_locations"]),
                ["validFrom"] = Convert.ToInt64(r["valid_from"]), ["validTo"] = Convert.ToInt64(r["valid_to"]),
            }).ToArray()),
            ["vehicles"] = new JsonArray(!shareRegistry ? [] : Query("SELECT id,secret_code,plate,mil_reg,type,model,company,status FROM vehicles ORDER BY id").Select(r => (JsonNode)new JsonObject
            {
                ["vehicleId"] = S(r["id"]), ["secretCode"] = full ? S(r["secret_code"]) : "", ["secretHash"] = SecretHash(S(r["secret_code"])),
                ["registration"] = full ? S(r["plate"]) : "", ["milReg"] = full ? S(r["mil_reg"]) : "",
                ["type"] = full ? S(r["type"]) : "", ["model"] = full ? S(r["model"]) : "", ["company"] = full ? S(r["company"]) : "", ["status"] = S(r["status"]),
                ["active"] = S(r["status"]) == "ACTIVE",
            }).ToArray()),
            ["locations"] = new JsonArray(Locations().Select(r => (JsonNode)new JsonObject { ["id"] = S(r["id"]), ["name"] = S(r["name"]) }).ToArray()),
            ["gates"] = new JsonArray(Gates().Select(r => (JsonNode)new JsonObject { ["id"] = S(r["id"]), ["name"] = S(r["name"]) }).ToArray()),
            ["presence"] = new JsonArray(!shareRegistry ? [] : Query("SELECT person_id, entry_at FROM presence WHERE status='ACTIVE'").Select(r => (JsonNode)new JsonObject
            { ["personId"] = S(r["person_id"]), ["entryAt"] = Convert.ToInt64(r["entry_at"]) }).ToArray()),
            ["manifests"] = new JsonArray(!shareRegistry ? [] : ActiveManifests().ToArray()),
            ["reasons"] = new JsonArray(Settings.MovementReasons.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
            ["returnReasons"] = new JsonArray(Settings.ReturnDateReasons.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
        };
    }

    IEnumerable<JsonNode> ActiveManifests() => Query("SELECT * FROM manifests WHERE state='ACTIVE'").Select(m => (JsonNode)ManifestJson(m));

    static JsonObject ManifestJson(Dictionary<string, object?> m) => new()
    {
        ["manifestId"] = S(m["manifest_id"]), ["vehicleId"] = S(m["vehicle_id"]), ["entryEventId"] = S(m["entry_event_id"]),
        ["locationId"] = S(m["location_id"]), ["gateId"] = S(m["gate_id"]), ["driverId"] = S(m["driver_id"]), ["coDriverId"] = m["co_driver_id"] == null ? null : S(m["co_driver_id"]),
        ["occupants"] = JsonNode.Parse(S(m["occupants"])), ["createdAt"] = Convert.ToInt64(m["created_at"]),
    };

    /// <summary>
    /// Online badge check used in Receive-only mode: returns only what the gate screen shows for this one scan.
    /// Nothing here is meant to be stored on the terminal.
    /// </summary>
    public JsonObject Verify(string rawCode, string expected, string operatorId)
    {
        var code = (rawCode ?? "").Trim();
        var canon = CanonId(code);
        JsonObject? result = null;
        if (expected != "VEHICLE")
        {
            var p = One("SELECT * FROM persons WHERE UPPER(secret_code)=UPPER($1) OR id=$2 OR (service_no<>'' AND UPPER(service_no)=UPPER($1)) LIMIT 1", code, canon);
            if (p != null)
            {
                var since = Scalar("SELECT entry_at FROM presence WHERE person_id=$1 AND status='ACTIVE' ORDER BY entry_at DESC LIMIT 1", S(p["id"]));
                result = new JsonObject
                {
                    ["type"] = "PERSON", ["id"] = S(p["id"]), ["name"] = S(p["name"]), ["rank"] = S(p["rank"]), ["serviceNo"] = S(p["service_no"]),
                    ["unit"] = S(p["unit"]), ["company"] = S(p["company"]), ["category"] = S(p["category"]), ["status"] = S(p["status"]),
                    ["inside"] = since != null, ["insideSince"] = since == null ? 0 : Convert.ToInt64(since),
                    ["validFrom"] = Convert.ToInt64(p["valid_from"]), ["validTo"] = Convert.ToInt64(p["valid_to"]),
                };
            }
        }
        if (result == null && expected != "PERSON")
        {
            var v = One("SELECT * FROM vehicles WHERE UPPER(secret_code)=UPPER($1) OR id=$2 OR REPLACE(REPLACE(UPPER(plate),'-',''),' ','')=$3 LIMIT 1", code, canon, code.ToUpperInvariant().Replace("-", "").Replace(" ", ""));
            if (v != null)
            {
                var m = One("SELECT * FROM manifests WHERE vehicle_id=$1 AND state='ACTIVE' LIMIT 1", S(v["id"]));
                result = new JsonObject
                {
                    ["type"] = "VEHICLE", ["id"] = S(v["id"]), ["registration"] = S(v["plate"]), ["vehicleType"] = S(v["type"]), ["status"] = S(v["status"]),
                    ["inside"] = m != null, ["manifest"] = m == null ? null : ManifestJson(m),
                };
            }
        }
        Audit(operatorId, "VERIFY_CREDENTIAL", result?["type"]?.ToString(), result?["id"]?.ToString(), result == null ? "NOT_FOUND" : "OK");
        return result ?? throw new StoreException("NOT_REGISTERED", $"Code {code} is not registered in the Command Center", 404);
    }

    // ------------------------------------------------------------------ movement records (ported from the Flask server rules)

    static readonly string[] EventFields = ["eventId", "entityType", "entityId", "eventType", "locationId", "gateId", "deviceId", "operatorId", "eventTimestamp", "createdAt"];

    static string Hash(JsonNode n) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString())));

    long NextSeq() => Count("SELECT COALESCE(MAX(seq),0)+1 FROM events");

    void CheckEvent(JsonObject e, string deviceId, string operatorId)
    {
        foreach (var f in EventFields)
            if (e[f] == null) throw new StoreException("MISSING_" + f.ToUpperInvariant(), $"Missing field {f}", 400);
        if (T(e, "deviceId") != deviceId) throw new StoreException("DEVICE_MISMATCH", "Event was not created by this device", 403);
        // Records captured offline may belong to an operator who signed out before the upload; they are accepted
        // from the same paired terminal as long as that operator account exists.
        var recordedBy = Upper(T(e, "operatorId"));
        if (recordedBy != operatorId && Count("SELECT COUNT(*) FROM accounts WHERE id=$1", recordedBy) == 0)
            throw new StoreException("UNKNOWN_OPERATOR", $"Operator {recordedBy} does not exist on this Command Center", 403);
        if (Upper(T(e, "eventType")) is not ("ENTRY" or "EXIT")) throw new StoreException("INVALID_EVENT_TYPE", "Invalid event type", 400);
    }

    void InsertEvent(JsonObject e, string entity, string id, string type, long seq, string hash, long? stay, string flags = "") =>
        Exec("""
            INSERT INTO events(event_id,entity_type,entity_id,event_type,location_id,gate_id,device_id,operator_id,event_ts,created_at,received_at,seq,
              source_type,source_id,loc_mismatch,scanned_loc,stay_ms,payload_hash,reason,remarks,expected_return,coming_from,flags) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23)
            """, T(e, "eventId"), entity, id, type, Upper(T(e, "locationId")), Upper(T(e, "gateId")), T(e, "deviceId"), Upper(T(e, "operatorId")),
            (long)e["eventTimestamp"]!, (long)e["createdAt"]!, NowMs, seq, Upper(T(e, "sourceType")) is { Length: > 0 } s ? s : "DIRECT",
            e["sourceId"]?.ToString(), e["locationMismatch"]?.GetValue<bool>() == true ? 1 : 0, T(e, "scannedLocation"), stay, hash, Clip(T(e, "reason"), 60), Clip(T(e, "remarks"), 300),
            type == "EXIT" && e["expectedReturn"] is JsonValue er && er.TryGetValue<long>(out var ret) && ret > 0 ? ret : 0L,
            type == "ENTRY" ? Clip(T(e, "comingFrom"), 80) : "", flags);

    static string Clip(string v, int max) => v.Length <= max ? v : v[..max];

    public JsonObject PersonEvent(JsonObject e, string deviceId, string operatorId)
    {
        CheckEvent(e, deviceId, operatorId);
        var result = Tx(() =>
        {
            var hash = Hash(e);
            var existing = One("SELECT payload_hash, seq, received_at FROM events WHERE event_id=$1", T(e, "eventId"));
            if (existing != null)
            {
                if (S(existing["payload_hash"]) != hash) throw new StoreException("EVENT_ID_REUSED", "Event id reused with different content");
                return new JsonObject { ["status"] = "duplicate", ["eventId"] = T(e, "eventId"), ["serverSequence"] = Convert.ToInt64(existing["seq"]), ["recordedAt"] = Convert.ToInt64(existing.GetValueOrDefault("received_at") ?? NowMs) };
            }
            if (Upper(T(e, "entityType")) != "PERSON") throw new StoreException("VEHICLE_TRANSACTION_REQUIRED", "Vehicles use the vehicle transaction", 400);
            var pid = CanonId(T(e, "entityId"));
            var person = One("SELECT status FROM persons WHERE id=$1", pid) ?? throw new StoreException("PERSON_NOT_FOUND", "Person is not in the registry");
            if (S(person["status"]) != "ACTIVE") throw new StoreException("INACTIVE_PERSON", "Person credential is " + S(person["status"]));
            var type = Upper(T(e, "eventType"));
            CheckPassValidity(pid, type, (long)e["eventTimestamp"]!);
            var current = One("SELECT * FROM presence WHERE person_id=$1 AND status='ACTIVE' ORDER BY entry_at DESC LIMIT 1", pid);
            long? stay = null;
            var flags = "";
            // Allow same person to be scanned multiple times by forcing EXIT first if already inside
            // This records multiple entries/exits more naturally and prevents "ALREADY_INSIDE" errors
            if (type == "ENTRY" && current != null)
            {
                // Flag the duplicate entry for administrator review, then auto-close the previous session
                flags = "DUPLICATE_ENTRY";
                Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE session_id=$3",
                    "AUTO-" + T(e, "eventId"), (long)e["eventTimestamp"]!, current["session_id"]);
            }
            if (type == "EXIT")
            {
                if (current == null) throw new StoreException("NOT_INSIDE", "Person is not recorded inside");
                if (S(current["source_type"]) == "VEHICLE" && Upper(T(e, "sourceType")) != "VEHICLE")
                {
                    // A person who arrived in a vehicle may leave on foot: close the vehicle-linked presence directly.
                }
                stay = (long)e["eventTimestamp"]! - Convert.ToInt64(current["entry_at"]);
            }
            var seq = NextSeq();
            InsertEvent(e, "PERSON", pid, type, seq, hash, stay, flags);
            if (type == "ENTRY")
                Exec("INSERT INTO presence(session_id,person_id,source_type,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,'DIRECT',$3,$4,$5,$6)",
                    "SES-" + T(e, "eventId"), pid, T(e, "eventId"), (long)e["eventTimestamp"]!, Upper(T(e, "locationId")), Upper(T(e, "gateId")));
            else
                Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE session_id=$3", T(e, "eventId"), (long)e["eventTimestamp"]!, current!["session_id"]);
            Audit(operatorId, "PERSON_" + type, "PERSON", pid, T(e, "eventId"));
            return new JsonObject { ["status"] = "accepted", ["eventId"] = T(e, "eventId"), ["serverSequence"] = seq, ["recordedAt"] = NowMs };
        });
        Notify();
        return result;
    }

    /// <summary>A vehicle this terminal could not identify offline -- not in its local registry and no connection
    /// to verify it online -- so the guard typed the plate by eye instead of being unable to record it at all.
    /// Logged as a standalone event with no manifest or presence tracking (there is no registry record to attach
    /// either to); an administrator reconciles it from the live feed / history once reviewed.</summary>
    public JsonObject ManualVehicleSighting(JsonObject e, string deviceId, string operatorId)
    {
        CheckEvent(e, deviceId, operatorId);
        var result = Tx(() =>
        {
            var hash = Hash(e);
            var existing = One("SELECT payload_hash, seq, received_at FROM events WHERE event_id=$1", T(e, "eventId"));
            if (existing != null)
            {
                if (S(existing["payload_hash"]) != hash) throw new StoreException("EVENT_ID_REUSED", "Event id reused with different content");
                return new JsonObject { ["status"] = "duplicate", ["eventId"] = T(e, "eventId"), ["serverSequence"] = Convert.ToInt64(existing["seq"]), ["recordedAt"] = Convert.ToInt64(existing.GetValueOrDefault("received_at") ?? NowMs) };
            }
            if (Upper(T(e, "entityType")) != "VEHICLE") throw new StoreException("INVALID_EVENT_TYPE", "Manual sighting must be a vehicle event", 400);
            var vid = CanonId(T(e, "entityId"));
            var seq = NextSeq();
            InsertEvent(e, "VEHICLE", vid, Upper(T(e, "eventType")), seq, hash, null);
            Audit(operatorId, "VEHICLE_MANUAL_SIGHTING", "VEHICLE", vid, T(e, "eventId"));
            return new JsonObject { ["status"] = "accepted", ["eventId"] = T(e, "eventId"), ["serverSequence"] = seq, ["recordedAt"] = NowMs };
        });
        Notify();
        return result;
    }

    public JsonObject VehicleTransaction(JsonObject payload, string deviceId, string operatorId)
    {
        var e = payload["event"] as JsonObject ?? throw new StoreException("INVALID_TRANSACTION", "Missing event", 400);
        var m = payload["manifest"] as JsonObject ?? throw new StoreException("INVALID_TRANSACTION", "Missing manifest", 400);
        CheckEvent(e, deviceId, operatorId);
        var result = Tx(() =>
        {
            var hash = Hash(payload);
            var vid = CanonId(T(e, "entityId"));
            var type = Upper(T(e, "eventType"));
            var manifestId = T(m, "manifestId");
            var existing = One("SELECT payload_hash, seq, received_at FROM events WHERE event_id=$1", T(e, "eventId"));
            if (existing != null)
            {
                if (S(existing["payload_hash"]) != hash) throw new StoreException("EVENT_ID_REUSED", "Event id reused with different content");
                return new JsonObject { ["status"] = "duplicate", ["eventId"] = T(e, "eventId"), ["manifestId"] = manifestId, ["serverSequence"] = Convert.ToInt64(existing["seq"]), ["recordedAt"] = Convert.ToInt64(existing.GetValueOrDefault("received_at") ?? NowMs) };
            }
            if (CanonId(T(m, "vehicleId")) != vid) throw new StoreException("EVENT_MANIFEST_MISMATCH", "Manifest vehicle does not match event");
            var vehicle = One("SELECT status FROM vehicles WHERE id=$1", vid) ?? throw new StoreException("VEHICLE_NOT_FOUND", "Vehicle is not in the fleet registry");
            if (S(vehicle["status"]) != "ACTIVE") throw new StoreException("INACTIVE_VEHICLE", "Vehicle credential is " + S(vehicle["status"]));
            var people = (m["occupants"] as JsonArray ?? []).Select(x => CanonId(x?.ToString())).Where(x => x.Length > 0).Distinct().ToList();
            var driver = CanonId(T(m, "driverId"));
            if (!people.Contains(driver)) throw new StoreException("DRIVER_NOT_IN_MANIFEST", "Driver missing from manifest");
            var active = One("SELECT * FROM manifests WHERE vehicle_id=$1 AND state='ACTIVE' LIMIT 1", vid);
            var ts = (long)e["eventTimestamp"]!;
            var seq = NextSeq();
            if (type == "ENTRY")
            {
                if (active != null) throw new StoreException("VEHICLE_ALREADY_INSIDE", "Vehicle is already recorded inside");
                foreach (var pid in people)
                {
                    var p = One("SELECT status FROM persons WHERE id=$1", pid) ?? throw new StoreException("PERSON_NOT_FOUND", $"{pid} is not in the registry");
                    if (S(p["status"]) != "ACTIVE") throw new StoreException("INACTIVE_PERSON", $"{pid} credential is {S(p["status"])}");
                    CheckPassValidity(pid, "ENTRY", ts); // visitors on board are checked like a walk-in entry
                    if (Count("SELECT COUNT(*) FROM presence WHERE person_id=$1 AND status='ACTIVE'", pid) > 0)
                        throw new StoreException("PERSON_ALREADY_INSIDE", $"{pid} is already recorded inside");
                }
                InsertEvent(e, "VEHICLE", vid, "ENTRY", seq, hash, null);
                Exec("INSERT INTO manifests(manifest_id,vehicle_id,entry_event_id,location_id,gate_id,driver_id,co_driver_id,occupants,created_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)",
                    manifestId, vid, T(e, "eventId"), Upper(T(e, "locationId")), Upper(T(e, "gateId")), driver,
                    string.IsNullOrWhiteSpace(T(m, "coDriverId")) ? null : CanonId(T(m, "coDriverId")), JsonSerializer.Serialize(people), ts);
                var i = 0;
                foreach (var pid in people)
                {
                    Exec("INSERT INTO presence(session_id,person_id,vehicle_id,source_type,source_id,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,$3,'VEHICLE',$4,$5,$6,$7,$8)",
                        $"SES-{manifestId}-{++i}", pid, vid, manifestId, T(e, "eventId"), ts, Upper(T(e, "locationId")), Upper(T(e, "gateId")));
                    // Also generate individual ENTRY event for crew member (shows in their history)
                    var crewEventId = "CREW-" + RandomCode(12);
                    InsertEvent(new JsonObject
                    {
                        ["eventId"] = crewEventId,
                        ["entityType"] = "PERSON",
                        ["entityId"] = pid,
                        ["eventType"] = "ENTRY",
                        ["eventTimestamp"] = ts,
                        ["locationId"] = Upper(T(e, "locationId")),
                        ["gateId"] = Upper(T(e, "gateId")),
                        ["sourceType"] = "VEHICLE",
                        ["sourceId"] = vid,
                        ["remarks"] = $"With vehicle {vid}"
                    }, "PERSON", pid, "ENTRY", NextSeq(), Hash(new JsonObject()), null);
                }
                CloseTransitOnEntry(vid, Upper(T(e, "locationId")), ts, operatorId);
            }
            else
            {
                if (active == null) throw new StoreException("VEHICLE_NOT_INSIDE", "Vehicle is not recorded inside");
                if (S(active["manifest_id"]) != manifestId) throw new StoreException("ACTIVE_MANIFEST_MISMATCH", "Manifest does not match the vehicle's entry");
                InsertEvent(e, "VEHICLE", vid, "EXIT", seq, hash, ts - Convert.ToInt64(active["created_at"]));
                OpenTransitFromExit(e, vid, ts, operatorId);
                Exec("UPDATE manifests SET state='EXITED', exit_event_id=$1, exit_at=$2 WHERE manifest_id=$3", T(e, "eventId"), ts, manifestId);
                Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE source_type='VEHICLE' AND source_id=$3 AND status='ACTIVE'", T(e, "eventId"), ts, manifestId);

                // Generate individual EXIT events for crew members
                var occupants = (active["occupants"] as JsonArray ?? []).Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList();
                foreach (var pid in occupants)
                {
                    var crewEventId = "CREW-" + RandomCode(12);
                    InsertEvent(new JsonObject
                    {
                        ["eventId"] = crewEventId,
                        ["entityType"] = "PERSON",
                        ["entityId"] = pid,
                        ["eventType"] = "EXIT",
                        ["eventTimestamp"] = ts,
                        ["locationId"] = Upper(T(e, "locationId")),
                        ["gateId"] = Upper(T(e, "gateId")),
                        ["sourceType"] = "VEHICLE",
                        ["sourceId"] = vid
                    }, "PERSON", pid, "EXIT", NextSeq(), Hash(new JsonObject()), null);
                }
            }
            Audit(operatorId, "VEHICLE_" + type, "VEHICLE", vid, manifestId);
            return new JsonObject { ["status"] = "accepted", ["eventId"] = T(e, "eventId"), ["manifestId"] = manifestId, ["serverSequence"] = seq, ["recordedAt"] = NowMs };
        });
        Notify();
        return result;
    }

    // ------------------------------------------------------------------ history records added on the PC

    /// <summary>
    /// Adds a record to a person's history from the Command Center. ENTRY / EXIT follow the same presence
    /// rules as a gate scan; any other type (Leave, Duty, Course…) is a dated history note that does not change presence.
    /// </summary>
    public long AddManualRecord(string personId, string type, long ts, string locationId, string gateId, string remarks, string actor = "PC-ADMIN", long expectedReturn = 0, string reason = "")
    {
        personId = CanonId(personId);
        type = (type ?? "").Trim();
        if (type.Length == 0 || type.Length > 40) throw new StoreException("INVALID_TYPE", "Choose a record type", 400);
        if (ts <= 0 || ts > NowMs + 3_600_000) throw new StoreException("INVALID_TIME", "The record time cannot be in the future", 400);
        var eventId = "PC-" + RandomCode(12);
        var upper = type.ToUpperInvariant();
        var result = Tx(() =>
        {
            var person = One("SELECT status FROM persons WHERE id=$1", personId) ?? throw new StoreException("PERSON_NOT_FOUND", "Person is not in the registry");
            long? stay = null;
            if (upper is "ENTRY" or "EXIT")
            {
                var current = One("SELECT * FROM presence WHERE person_id=$1 AND status='ACTIVE' ORDER BY entry_at DESC LIMIT 1", personId);
                if (upper == "ENTRY" && current != null) throw new StoreException("ALREADY_INSIDE", "Person is already recorded inside. Record an EXIT first.");
                if (upper == "EXIT" && current == null) throw new StoreException("NOT_INSIDE", "Person is not recorded inside.");
                if (upper == "EXIT")
                {
                    stay = ts - Convert.ToInt64(current!["entry_at"]);
                    if (stay < 0) throw new StoreException("EXIT_BEFORE_ENTRY", "The exit time is earlier than the recorded entry (" + DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(current["entry_at"])).LocalDateTime.ToString("dd MMM yyyy HH:mm") + ").", 400);
                }
                if (upper == "ENTRY")
                    Exec("INSERT INTO presence(session_id,person_id,source_type,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,'DIRECT',$3,$4,$5,$6)",
                        "SES-" + eventId, personId, eventId, ts, Upper(locationId), Upper(gateId));
                else
                    Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE session_id=$3", eventId, ts, current!["session_id"]);
                type = upper;
            }
            var seq = NextSeq();
            Exec("""
                INSERT INTO events(event_id,entity_type,entity_id,event_type,location_id,gate_id,device_id,operator_id,event_ts,created_at,received_at,seq,
                  source_type,stay_ms,payload_hash,remarks,source,expected_return,reason) VALUES($1,'PERSON',$2,$3,$4,$5,'PC',$6,$7,$8,$8,$9,'DIRECT',$10,'',$11,'PC',$12,$13)
                """, eventId, personId, type, Upper(locationId), Upper(gateId), actor, ts, NowMs, seq, stay, (remarks ?? "").Trim(), upper == "EXIT" && expectedReturn > 0 ? expectedReturn : 0L, Clip((reason ?? "").Trim(), 60));
            Audit(actor, "ADD_HISTORY_RECORD", "PERSON", personId, $"{type} {(remarks ?? "").Trim()}".Trim());
            return seq;
        });
        Notify();
        return result;
    }

    /// <summary>Rows for reports: people filtered by company / explicit IDs, and their records in a date range.</summary>
    public (List<Dictionary<string, object?>> persons, List<Dictionary<string, object?>> records) ReportData(IReadOnlyCollection<string>? personIds, string? company, long fromMs, long toMs)
    {
        var persons = Persons().Where(p =>
            (personIds == null || personIds.Count == 0 || personIds.Contains(S(p["id"]))) &&
            (string.IsNullOrEmpty(company) || company == "ALL" || string.Equals(S(p["company"]), company, StringComparison.OrdinalIgnoreCase))).ToList();
        var ids = persons.Select(p => S(p["id"])).ToHashSet();
        var records = Query("""
            SELECT e.*, COALESCE(l.name, e.location_id) AS location_name, COALESCE(g.name, e.gate_id) AS gate_name
            FROM events e LEFT JOIN locations l ON l.id=e.location_id LEFT JOIN gates g ON g.id=e.gate_id
            WHERE e.entity_type='PERSON' AND e.event_ts BETWEEN $1 AND $2 ORDER BY e.event_ts
            """, fromMs, toMs).Where(r => ids.Contains(S(r["entity_id"]))).ToList();
        return (persons, records);
    }

    // ------------------------------------------------------------------ dashboard queries

    public List<Dictionary<string, object?>> RecentEvents(int limit = 300, string q = "", string type = "ALL")
    {
        var rows = Query("""
        SELECT e.*, COALESCE(p.name, v.plate, e.entity_id) AS title,
               CASE WHEN e.entity_type='PERSON' THEN TRIM(COALESCE(p.rank,'') || ' ' || COALESCE(p.unit,'')) ELSE COALESCE(v.type,'') END AS subtitle,
               COALESCE(l.name, e.location_id) AS location_name, COALESCE(g.name, e.gate_id) AS gate_name, m.occupants,
               m.driver_id AS crew_driver_id, m.co_driver_id AS crew_co_driver_id,
               tr.transit_id AS tr_transit_id, tr.dest_name AS tr_dest_name, tr.state AS tr_state, tr.expected_min AS tr_expected_min, tr.due_at AS tr_due_at,
               tr.actual_min AS tr_actual_min, tr.resolved_via AS tr_resolved_via, tr.resolved_by AS tr_resolved_by, tr.end_name AS tr_end_name, tr.note AS tr_note, tr.left_at AS tr_left_at
        FROM events e
        LEFT JOIN persons p ON e.entity_type='PERSON' AND p.id=e.entity_id
        LEFT JOIN vehicles v ON e.entity_type='VEHICLE' AND v.id=e.entity_id
        LEFT JOIN locations l ON l.id=e.location_id LEFT JOIN gates g ON g.id=e.gate_id
        LEFT JOIN manifests m ON m.entry_event_id=e.event_id OR m.exit_event_id=e.event_id
        LEFT JOIN transits tr ON tr.exit_event_id=e.event_id
        WHERE ($2='ALL' OR e.entity_type=$2 OR e.event_type=$2 OR ($2='FLAGS' AND e.loc_mismatch=1))
          AND ($3='' OR e.entity_id LIKE $4 OR p.name LIKE $4 OR v.plate LIKE $4 OR e.location_id LIKE $4 OR l.name LIKE $4 OR e.operator_id LIKE $4)
        ORDER BY e.seq DESC LIMIT $1
        """, limit, type, q.Trim(), "%" + q.Trim() + "%");
        AttachCrew(rows);
        return rows;
    }

    /// <summary>The complete movement history for one exact person/vehicle -- every record ever made for it, newest
    /// first, with no recency cap. Used by the "History" dossier view, which must show the total record, not just
    /// however many of the *system's* most recent events (across everyone) happen to include this one.</summary>
    public List<Dictionary<string, object?>> EventsForEntity(string type, string id)
    {
        var rows = Query("""
        SELECT e.*, COALESCE(p.name, v.plate, e.entity_id) AS title,
               CASE WHEN e.entity_type='PERSON' THEN TRIM(COALESCE(p.rank,'') || ' ' || COALESCE(p.unit,'')) ELSE COALESCE(v.type,'') END AS subtitle,
               COALESCE(l.name, e.location_id) AS location_name, COALESCE(g.name, e.gate_id) AS gate_name, m.occupants,
               m.driver_id AS crew_driver_id, m.co_driver_id AS crew_co_driver_id,
               tr.transit_id AS tr_transit_id, tr.dest_name AS tr_dest_name, tr.state AS tr_state, tr.expected_min AS tr_expected_min, tr.due_at AS tr_due_at,
               tr.actual_min AS tr_actual_min, tr.resolved_via AS tr_resolved_via, tr.resolved_by AS tr_resolved_by, tr.end_name AS tr_end_name, tr.note AS tr_note, tr.left_at AS tr_left_at
        FROM events e
        LEFT JOIN persons p ON e.entity_type='PERSON' AND p.id=e.entity_id
        LEFT JOIN vehicles v ON e.entity_type='VEHICLE' AND v.id=e.entity_id
        LEFT JOIN locations l ON l.id=e.location_id LEFT JOIN gates g ON g.id=e.gate_id
        LEFT JOIN manifests m ON m.entry_event_id=e.event_id OR m.exit_event_id=e.event_id
        LEFT JOIN transits tr ON tr.exit_event_id=e.event_id
        WHERE e.entity_type=$1 AND e.entity_id=$2
        ORDER BY e.seq DESC
        """, type, id);
        AttachCrew(rows);
        return rows;
    }

    public (long inside, long outside, long fleetIn, long fleet, long flags, long total, long entriesToday, long exitsToday) Stats()
    {
        var start = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();
        var persons = Count("SELECT COUNT(*) FROM persons");
        var inside = Count("SELECT COUNT(DISTINCT person_id) FROM presence WHERE status='ACTIVE'");
        return (inside, Math.Max(0, persons - inside), Count("SELECT COUNT(*) FROM manifests WHERE state='ACTIVE'"), Count("SELECT COUNT(*) FROM vehicles"),
            Count("SELECT COUNT(*) FROM events WHERE loc_mismatch=1"), Count("SELECT COUNT(*) FROM events"),
            Count("SELECT COUNT(*) FROM events WHERE event_type='ENTRY' AND event_ts>=$1", start), Count("SELECT COUNT(*) FROM events WHERE event_type='EXIT' AND event_ts>=$1", start));
    }

    /// <summary>Per-gate counts for today built from real records and device heartbeats.</summary>
    public List<Dictionary<string, object?>> GateStations()
    {
        var start = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();
        return Query("""
            SELECT g.id, g.name,
              (SELECT COUNT(*) FROM events e WHERE e.gate_id=g.id AND e.event_type='ENTRY' AND e.event_ts>=$1) AS entries,
              (SELECT COUNT(*) FROM events e WHERE e.gate_id=g.id AND e.event_type='EXIT' AND e.event_ts>=$1) AS exits,
              (SELECT COUNT(*) FROM events e WHERE e.gate_id=g.id AND e.loc_mismatch=1 AND e.event_ts>=$1) AS flags,
              (SELECT MAX(d.last_seen) FROM devices d WHERE d.gate_id=g.id AND d.active=1) AS last_seen,
              (SELECT d.operator_id FROM devices d WHERE d.gate_id=g.id AND d.active=1 ORDER BY d.last_seen DESC LIMIT 1) AS operator_id,
              (SELECT d.location_id FROM devices d WHERE d.gate_id=g.id AND d.active=1 ORDER BY d.last_seen DESC LIMIT 1) AS location_id
            FROM gates g ORDER BY g.id
            """, start);
    }

    public List<Dictionary<string, object?>> AuditLog(int limit = 300) => Query("SELECT * FROM audit ORDER BY id DESC LIMIT $1", limit);

    /// <summary>Removes all movement records and presence. Registry, accounts and devices are kept.</summary>
    public void PurgeRecords()
    {
        Tx(() => { Exec("DELETE FROM events"); Exec("DELETE FROM manifests"); Exec("DELETE FROM presence"); Exec("DELETE FROM transits"); Audit("PC-ADMIN", "PURGE_RECORDS"); return 0; });
        Notify();
    }
}
