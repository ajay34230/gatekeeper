using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using XV.Core;

namespace XV.Comms;

/// <param name="Direction">IN = from the terminal to this PC, OUT = from this PC to the terminal.</param>
/// <param name="Kind">MESSAGE or ALERT.</param>
public sealed record CommsMessage(string Id, string DeviceId, string Direction, string Kind, string Body, string Sender, long CreatedAt, long DeliveredAt, long ReadAt);

/// <summary>The Comms engine's own SQLCipher database (comms.db) with its own DPAPI-protected key, separate from the gate records.</summary>
public sealed class CommsStore : IDisposable
{
    readonly SqliteConnection _db;
    readonly object _lock = new();

    public CommsStore(string? dbFile = null)
    {
        SQLitePCL.Batteries_V2.Init();
        var key = Protector.LoadOrCreate("comms.key.bin", () => RandomNumberGenerator.GetBytes(32));
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbFile ?? Paths.File("xv-comms.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = Convert.ToHexString(key),
        }.ToString());
        _db.Open();
        Exec("""
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS messages(id TEXT PRIMARY KEY, device_id TEXT NOT NULL, direction TEXT NOT NULL, kind TEXT NOT NULL,
              body TEXT NOT NULL, sender TEXT NOT NULL, created_at INTEGER NOT NULL, delivered_at INTEGER NOT NULL DEFAULT 0, read_at INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_messages_device ON messages(device_id, created_at);
            CREATE TABLE IF NOT EXISTS calls(id TEXT PRIMARY KEY, device_id TEXT NOT NULL, direction TEXT NOT NULL, video INTEGER NOT NULL,
              started_at INTEGER NOT NULL, answered_at INTEGER NOT NULL DEFAULT 0, ended_at INTEGER NOT NULL DEFAULT 0, outcome TEXT NOT NULL DEFAULT '');
            """);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Encrypted copy of the Comms database for a backup.</summary>
    public void ExportEncrypted(string path, string passphrase) { lock (_lock) Backup.ExportDatabase(_db, path, passphrase); }

    int Exec(string sql, params object?[] args)
    {
        lock (_lock)
        {
            using var c = _db.CreateCommand();
            c.CommandText = sql;
            for (var i = 0; i < args.Length; i++) c.Parameters.AddWithValue("$" + (i + 1), args[i] ?? DBNull.Value);
            return c.ExecuteNonQuery();
        }
    }

    List<CommsMessage> Messages(string where, params object?[] args)
    {
        lock (_lock)
        {
            using var c = _db.CreateCommand();
            c.CommandText = "SELECT id,device_id,direction,kind,body,sender,created_at,delivered_at,read_at FROM messages " + where;
            for (var i = 0; i < args.Length; i++) c.Parameters.AddWithValue("$" + (i + 1), args[i] ?? DBNull.Value);
            using var r = c.ExecuteReader();
            var list = new List<CommsMessage>();
            while (r.Read()) list.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt64(6), r.GetInt64(7), r.GetInt64(8)));
            return list;
        }
    }

    /// <summary>Inserts a message; returns false when a message with the same id already exists (terminal re-sent it).</summary>
    public bool Insert(CommsMessage m) =>
        Exec("INSERT OR IGNORE INTO messages(id,device_id,direction,kind,body,sender,created_at,delivered_at,read_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)",
            m.Id, m.DeviceId, m.Direction, m.Kind, m.Body, m.Sender, m.CreatedAt, m.DeliveredAt, m.ReadAt) == 1;

    public List<CommsMessage> Conversation(string deviceId, int limit = 500) =>
        Messages("WHERE device_id=$1 ORDER BY created_at DESC LIMIT $2", deviceId, limit).AsEnumerable().Reverse().ToList();

    public List<CommsMessage> Undelivered(string deviceId) => Messages("WHERE device_id=$1 AND direction='OUT' AND delivered_at=0 ORDER BY created_at", deviceId);

    public List<CommsMessage> UnreadAlerts() => Messages("WHERE direction='IN' AND kind='ALERT' AND read_at=0 ORDER BY created_at");

    public Dictionary<string, int> UnreadCounts()
    {
        lock (_lock)
        {
            using var c = _db.CreateCommand();
            c.CommandText = "SELECT device_id, COUNT(*) FROM messages WHERE direction='IN' AND read_at=0 GROUP BY device_id";
            using var r = c.ExecuteReader();
            var d = new Dictionary<string, int>();
            while (r.Read()) d[r.GetString(0)] = r.GetInt32(1);
            return d;
        }
    }

    /// <summary>Terminal confirmed delivery or reading of a message this PC sent.</summary>
    public bool Ack(string deviceId, string id, bool read)
    {
        var now = Store.NowMs;
        return Exec(read
            ? "UPDATE messages SET read_at=$1, delivered_at=CASE WHEN delivered_at=0 THEN $1 ELSE delivered_at END WHERE id=$2 AND device_id=$3 AND direction='OUT' AND read_at=0"
            : "UPDATE messages SET delivered_at=$1 WHERE id=$2 AND device_id=$3 AND direction='OUT' AND delivered_at=0", now, id, deviceId) > 0;
    }

    public List<string> MarkIncomingRead(string deviceId)
    {
        var ids = Messages("WHERE device_id=$1 AND direction='IN' AND read_at=0", deviceId).Select(m => m.Id).ToList();
        if (ids.Count > 0) Exec("UPDATE messages SET read_at=$1 WHERE device_id=$2 AND direction='IN' AND read_at=0", Store.NowMs, deviceId);
        return ids;
    }

    public void LogCall(string id, string deviceId, string direction, bool video, long startedAt) =>
        Exec("INSERT OR IGNORE INTO calls(id,device_id,direction,video,started_at) VALUES($1,$2,$3,$4,$5)", id, deviceId, direction, video ? 1 : 0, startedAt);

    public void CallAnswered(string id) => Exec("UPDATE calls SET answered_at=$1 WHERE id=$2 AND answered_at=0", Store.NowMs, id);

    public void CallEnded(string id, string outcome) => Exec("UPDATE calls SET ended_at=$1, outcome=$2 WHERE id=$3 AND ended_at=0", Store.NowMs, outcome, id);

    public List<Dictionary<string, object>> Calls(string deviceId, int limit = 50)
    {
        lock (_lock)
        {
            using var c = _db.CreateCommand();
            c.CommandText = "SELECT id,direction,video,started_at,answered_at,ended_at,outcome FROM calls WHERE device_id=$1 ORDER BY started_at DESC LIMIT $2";
            c.Parameters.AddWithValue("$1", deviceId); c.Parameters.AddWithValue("$2", limit);
            using var r = c.ExecuteReader();
            var list = new List<Dictionary<string, object>>();
            while (r.Read()) { var d = new Dictionary<string, object>(); for (var i = 0; i < r.FieldCount; i++) d[r.GetName(i)] = r.GetValue(i); list.Add(d); }
            return list;
        }
    }
}
