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
public sealed class Store : IDisposable
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
    }

    public void Dispose() => _db.Dispose();

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

    void Audit(string actor, string action, string? type = null, string? id = null, string? detail = null) =>
        Exec("INSERT INTO audit(actor,action,entity_type,entity_id,detail,created_at) VALUES($1,$2,$3,$4,$5,$6)", actor, action, type, id, detail, NowMs);

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

    public List<Dictionary<string, object?>> Vehicles(string q = "") => Query("""
        SELECT v.*, (SELECT m.created_at FROM manifests m WHERE m.vehicle_id=v.id AND m.state='ACTIVE' LIMIT 1) AS inside_since
        FROM vehicles v WHERE $1='' OR v.id LIKE $2 OR v.plate LIKE $2 OR v.type LIKE $2 OR v.company LIKE $2 ORDER BY v.id
        """, q.Trim(), "%" + q.Trim() + "%");

    public string NextId(string prefix, string table)
    {
        var ids = Query($"SELECT id FROM {table}").Select(r => S(r["id"])).Where(i => i.StartsWith(prefix)).Select(i => int.TryParse(i[prefix.Length..], out var n) ? n : 0);
        return prefix + ((ids.DefaultIfEmpty(0).Max()) + 1).ToString("000");
    }

    public void UpsertPerson(JsonObject p, string actor = "PC-ADMIN")
    {
        var id = CanonId(p["id"]?.ToString());
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^P[0-9]{3,6}$")) throw new StoreException("INVALID_ID", "Personnel ID must look like P001", 400);
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
        Audit(actor, existing == null ? "ADD_PERSON" : "EDIT_PERSON", "PERSON", id);
        Notify();
    }

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

    public void DeletePerson(string id) { Exec("DELETE FROM persons WHERE id=$1", id); Audit("PC-ADMIN", "DELETE_PERSON", "PERSON", id); Notify(); }
    public void DeleteVehicle(string id) { Exec("DELETE FROM vehicles WHERE id=$1", id); Audit("PC-ADMIN", "DELETE_VEHICLE", "VEHICLE", id); Notify(); }

    /// <summary>Issues a new QR secret, instantly invalidating the old printed credential.</summary>
    public void RotateSecret(string table, string id)
    {
        Exec($"UPDATE {table} SET secret_code=$1, updated_at=$2 WHERE id=$3", (table == "persons" ? "XVP" : "XVV") + RandomCode(10), NowMs, id);
        Audit("PC-ADMIN", "ROTATE_QR_SECRET", table, id); Notify();
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
    public (string id, string? generated) CreateAccount(string name, string? id, string? password, string createdBy, string role = "Gatekeeper Operator")
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

    public JsonObject Login(string idRaw, string password, string deviceId)
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
            throw new StoreException("INVALID_CREDENTIALS", "Invalid Gatekeeper ID or password", 401);
        }
        lock (_failures) _failures.Remove(id + deviceId);
        var status = S(a!["status"]);
        if (status == "PENDING") throw new StoreException("PENDING_APPROVAL", "Account is waiting for approval on the PC Command Center", 403);
        if (status != "ACTIVE") throw new StoreException("ACCOUNT_DISABLED", "Account is disabled", 403);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = NowMs + Settings.TokenHours * 3_600_000L;
        Exec("INSERT INTO tokens(token_hash,account_id,device_id,expires_at) VALUES($1,$2,$3,$4)", Sha(token), id, deviceId, expires);
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
    public string? OperatorFor(string? token, string deviceId)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var r = One("SELECT t.account_id, t.expires_at, a.status FROM tokens t JOIN accounts a ON a.id=t.account_id WHERE t.token_hash=$1 AND t.device_id=$2", Sha(token), deviceId);
        if (r == null || Convert.ToInt64(r["expires_at"]) < NowMs || S(r["status"]) != "ACTIVE") return null;
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
    public void DeleteDevice(string id) { Exec("DELETE FROM devices WHERE device_id=$1", id); Exec("DELETE FROM tokens WHERE device_id=$1", id); Notify(); }

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

    public JsonObject Bootstrap() => new()
    {
        ["version"] = S(Scalar("SELECT MAX(updated_at) FROM (SELECT updated_at FROM persons UNION ALL SELECT updated_at FROM vehicles)")),
        ["serverTime"] = NowMs,
        ["persons"] = new JsonArray(Query("SELECT id,secret_code,name,rank,service_no,unit,company,role,category,status,access_locations FROM persons ORDER BY id").Select(r => (JsonNode)new JsonObject
        {
            ["personId"] = S(r["id"]), ["secretCode"] = S(r["secret_code"]), ["name"] = S(r["name"]), ["rank"] = S(r["rank"]),
            ["serviceNo"] = S(r["service_no"]), ["unit"] = S(r["unit"]), ["company"] = S(r["company"]), ["role"] = S(r["role"]),
            ["category"] = S(r["category"]), ["status"] = S(r["status"]), ["active"] = S(r["status"]) == "ACTIVE",
            ["accessLocations"] = S(r["access_locations"]),
        }).ToArray()),
        ["vehicles"] = new JsonArray(Query("SELECT id,secret_code,plate,mil_reg,type,model,company,status FROM vehicles ORDER BY id").Select(r => (JsonNode)new JsonObject
        {
            ["vehicleId"] = S(r["id"]), ["secretCode"] = S(r["secret_code"]), ["registration"] = S(r["plate"]), ["milReg"] = S(r["mil_reg"]),
            ["type"] = S(r["type"]), ["model"] = S(r["model"]), ["company"] = S(r["company"]), ["status"] = S(r["status"]),
            ["active"] = S(r["status"]) == "ACTIVE",
        }).ToArray()),
        ["locations"] = new JsonArray(Locations().Select(r => (JsonNode)new JsonObject { ["id"] = S(r["id"]), ["name"] = S(r["name"]) }).ToArray()),
        ["gates"] = new JsonArray(Gates().Select(r => (JsonNode)new JsonObject { ["id"] = S(r["id"]), ["name"] = S(r["name"]) }).ToArray()),
        ["presence"] = new JsonArray(Query("SELECT person_id, entry_at FROM presence WHERE status='ACTIVE'").Select(r => (JsonNode)new JsonObject
        { ["personId"] = S(r["person_id"]), ["entryAt"] = Convert.ToInt64(r["entry_at"]) }).ToArray()),
    };

    // ------------------------------------------------------------------ movement records (ported from the Flask server rules)

    static readonly string[] EventFields = ["eventId", "entityType", "entityId", "eventType", "locationId", "gateId", "deviceId", "operatorId", "eventTimestamp", "createdAt"];

    static string Hash(JsonNode n) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString())));

    long NextSeq() => Count("SELECT COALESCE(MAX(seq),0)+1 FROM events");

    void CheckEvent(JsonObject e, string deviceId, string operatorId)
    {
        foreach (var f in EventFields)
            if (e[f] == null) throw new StoreException("MISSING_" + f.ToUpperInvariant(), $"Missing field {f}", 400);
        if (T(e, "deviceId") != deviceId) throw new StoreException("DEVICE_MISMATCH", "Event was not created by this device", 403);
        if (Upper(T(e, "operatorId")) != operatorId) throw new StoreException("OPERATOR_MISMATCH", "Event operator does not match the signed-in operator", 403);
        if (Upper(T(e, "eventType")) is not ("ENTRY" or "EXIT")) throw new StoreException("INVALID_EVENT_TYPE", "Invalid event type", 400);
    }

    void InsertEvent(JsonObject e, string entity, string id, string type, long seq, string hash, long? stay) =>
        Exec("""
            INSERT INTO events(event_id,entity_type,entity_id,event_type,location_id,gate_id,device_id,operator_id,event_ts,created_at,received_at,seq,
              source_type,source_id,loc_mismatch,scanned_loc,stay_ms,payload_hash) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18)
            """, T(e, "eventId"), entity, id, type, Upper(T(e, "locationId")), Upper(T(e, "gateId")), T(e, "deviceId"), Upper(T(e, "operatorId")),
            (long)e["eventTimestamp"]!, (long)e["createdAt"]!, NowMs, seq, Upper(T(e, "sourceType")) is { Length: > 0 } s ? s : "DIRECT",
            e["sourceId"]?.ToString(), e["locationMismatch"]?.GetValue<bool>() == true ? 1 : 0, T(e, "scannedLocation"), stay, hash);

    public JsonObject PersonEvent(JsonObject e, string deviceId, string operatorId)
    {
        CheckEvent(e, deviceId, operatorId);
        var result = Tx(() =>
        {
            var hash = Hash(e);
            var existing = One("SELECT payload_hash, seq FROM events WHERE event_id=$1", T(e, "eventId"));
            if (existing != null)
            {
                if (S(existing["payload_hash"]) != hash) throw new StoreException("EVENT_ID_REUSED", "Event id reused with different content");
                return new JsonObject { ["status"] = "duplicate", ["eventId"] = T(e, "eventId"), ["serverSequence"] = Convert.ToInt64(existing["seq"]) };
            }
            if (Upper(T(e, "entityType")) != "PERSON") throw new StoreException("VEHICLE_TRANSACTION_REQUIRED", "Vehicles use the vehicle transaction", 400);
            var pid = CanonId(T(e, "entityId"));
            var person = One("SELECT status FROM persons WHERE id=$1", pid) ?? throw new StoreException("PERSON_NOT_FOUND", "Person is not in the registry");
            if (S(person["status"]) != "ACTIVE") throw new StoreException("INACTIVE_PERSON", "Person credential is " + S(person["status"]));
            var type = Upper(T(e, "eventType"));
            var current = One("SELECT * FROM presence WHERE person_id=$1 AND status='ACTIVE' ORDER BY entry_at DESC LIMIT 1", pid);
            long? stay = null;
            if (type == "ENTRY" && current != null) throw new StoreException("ALREADY_INSIDE", "Person is already recorded inside");
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
            InsertEvent(e, "PERSON", pid, type, seq, hash, stay);
            if (type == "ENTRY")
                Exec("INSERT INTO presence(session_id,person_id,source_type,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,'DIRECT',$3,$4,$5,$6)",
                    "SES-" + T(e, "eventId"), pid, T(e, "eventId"), (long)e["eventTimestamp"]!, Upper(T(e, "locationId")), Upper(T(e, "gateId")));
            else
                Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE session_id=$3", T(e, "eventId"), (long)e["eventTimestamp"]!, current!["session_id"]);
            Audit(operatorId, "PERSON_" + type, "PERSON", pid, T(e, "eventId"));
            return new JsonObject { ["status"] = "accepted", ["eventId"] = T(e, "eventId"), ["serverSequence"] = seq };
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
            var existing = One("SELECT payload_hash, seq FROM events WHERE event_id=$1", T(e, "eventId"));
            if (existing != null)
            {
                if (S(existing["payload_hash"]) != hash) throw new StoreException("EVENT_ID_REUSED", "Event id reused with different content");
                return new JsonObject { ["status"] = "duplicate", ["eventId"] = T(e, "eventId"), ["manifestId"] = manifestId, ["serverSequence"] = Convert.ToInt64(existing["seq"]) };
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
                    if (Count("SELECT COUNT(*) FROM presence WHERE person_id=$1 AND status='ACTIVE'", pid) > 0)
                        throw new StoreException("PERSON_ALREADY_INSIDE", $"{pid} is already recorded inside");
                }
                InsertEvent(e, "VEHICLE", vid, "ENTRY", seq, hash, null);
                Exec("INSERT INTO manifests(manifest_id,vehicle_id,entry_event_id,location_id,gate_id,driver_id,co_driver_id,occupants,created_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)",
                    manifestId, vid, T(e, "eventId"), Upper(T(e, "locationId")), Upper(T(e, "gateId")), driver,
                    string.IsNullOrWhiteSpace(T(m, "coDriverId")) ? null : CanonId(T(m, "coDriverId")), JsonSerializer.Serialize(people), ts);
                var i = 0;
                foreach (var pid in people)
                    Exec("INSERT INTO presence(session_id,person_id,vehicle_id,source_type,source_id,entry_event_id,entry_at,location_id,gate_id) VALUES($1,$2,$3,'VEHICLE',$4,$5,$6,$7,$8)",
                        $"SES-{manifestId}-{++i}", pid, vid, manifestId, T(e, "eventId"), ts, Upper(T(e, "locationId")), Upper(T(e, "gateId")));
            }
            else
            {
                if (active == null) throw new StoreException("VEHICLE_NOT_INSIDE", "Vehicle is not recorded inside");
                if (S(active["manifest_id"]) != manifestId) throw new StoreException("ACTIVE_MANIFEST_MISMATCH", "Manifest does not match the vehicle's entry");
                InsertEvent(e, "VEHICLE", vid, "EXIT", seq, hash, ts - Convert.ToInt64(active["created_at"]));
                Exec("UPDATE manifests SET state='EXITED', exit_event_id=$1, exit_at=$2 WHERE manifest_id=$3", T(e, "eventId"), ts, manifestId);
                Exec("UPDATE presence SET status='CLOSED', exit_event_id=$1, exit_at=$2 WHERE source_type='VEHICLE' AND source_id=$3 AND status='ACTIVE'", T(e, "eventId"), ts, manifestId);
            }
            Audit(operatorId, "VEHICLE_" + type, "VEHICLE", vid, manifestId);
            return new JsonObject { ["status"] = "accepted", ["eventId"] = T(e, "eventId"), ["manifestId"] = manifestId, ["serverSequence"] = seq };
        });
        Notify();
        return result;
    }

    // ------------------------------------------------------------------ dashboard queries

    public List<Dictionary<string, object?>> RecentEvents(int limit = 300, string q = "", string type = "ALL") => Query("""
        SELECT e.*, COALESCE(p.name, v.plate, e.entity_id) AS title,
               CASE WHEN e.entity_type='PERSON' THEN TRIM(COALESCE(p.rank,'') || ' ' || COALESCE(p.unit,'')) ELSE COALESCE(v.type,'') END AS subtitle,
               COALESCE(l.name, e.location_id) AS location_name, COALESCE(g.name, e.gate_id) AS gate_name, m.occupants
        FROM events e
        LEFT JOIN persons p ON e.entity_type='PERSON' AND p.id=e.entity_id
        LEFT JOIN vehicles v ON e.entity_type='VEHICLE' AND v.id=e.entity_id
        LEFT JOIN locations l ON l.id=e.location_id LEFT JOIN gates g ON g.id=e.gate_id
        LEFT JOIN manifests m ON m.entry_event_id=e.event_id OR m.exit_event_id=e.event_id
        WHERE ($2='ALL' OR e.entity_type=$2 OR e.event_type=$2 OR ($2='FLAGS' AND e.loc_mismatch=1))
          AND ($3='' OR e.entity_id LIKE $4 OR p.name LIKE $4 OR v.plate LIKE $4 OR e.location_id LIKE $4 OR l.name LIKE $4 OR e.operator_id LIKE $4)
        ORDER BY e.seq DESC LIMIT $1
        """, limit, type, q.Trim(), "%" + q.Trim() + "%");

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
        Tx(() => { Exec("DELETE FROM events"); Exec("DELETE FROM manifests"); Exec("DELETE FROM presence"); Audit("PC-ADMIN", "PURGE_RECORDS"); return 0; });
        Notify();
    }
}
