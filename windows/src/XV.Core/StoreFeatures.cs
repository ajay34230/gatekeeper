using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace XV.Core;

/// <summary>
/// Operational features on top of the gate records: tamper-evident audit trail, card lifecycle, visitor passes,
/// leave / overdue tracking and remote wipe of revoked terminals.
/// </summary>
public sealed partial class Store
{
    void MigrateFeatures(Action<string, string, string> ensure)
    {
        ensure("audit", "prev_hash", "TEXT NOT NULL DEFAULT ''");
        ensure("audit", "hash", "TEXT NOT NULL DEFAULT ''");
        ensure("events", "expected_return", "INTEGER NOT NULL DEFAULT 0");
        ensure("persons", "valid_from", "INTEGER NOT NULL DEFAULT 0");
        ensure("persons", "valid_to", "INTEGER NOT NULL DEFAULT 0");
        ensure("devices", "wipe_ordered_at", "INTEGER NOT NULL DEFAULT 0");
        Exec("""
            CREATE TABLE IF NOT EXISTS card_events(id INTEGER PRIMARY KEY AUTOINCREMENT, person_id TEXT NOT NULL, event TEXT NOT NULL,
              detail TEXT NOT NULL DEFAULT '', actor TEXT NOT NULL, created_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_card_events_person ON card_events(person_id, created_at);
            CREATE TABLE IF NOT EXISTS removed_devices(device_id TEXT PRIMARY KEY, removed_at INTEGER NOT NULL, wipe_ordered_at INTEGER NOT NULL DEFAULT 0);
            """);
        BackfillAuditChain();
        // No anchor yet (new install or just restored): anchor at the current end so later truncation is detected.
        if (!File.Exists(Paths.File(AuditHeadFile)) && One("SELECT id, hash FROM audit ORDER BY id DESC LIMIT 1") is { } last)
            WriteAuditHead(Convert.ToInt64(last["id"]), S(last["hash"]));
    }

    /// <summary>Soldiers only (visitor passes, IDs G…, are kept out of the soldier register, ID cards and exports).</summary>
    public List<Dictionary<string, object?>> Soldiers(string q = "") => Persons(q).Where(p => S(p["id"]).StartsWith('P')).ToList();

    /// <summary>Encrypted copy of the live database for a backup (see <see cref="Backup"/>).</summary>
    public void ExportEncrypted(string path, string passphrase) { lock (_lock) Backup.ExportDatabase(_db, path, passphrase); }

    // ================================================================== tamper-evident audit trail

    const string AuditHeadFile = "audit-head.bin";

    static string AuditHash(string prev, long id, string actor, string action, string? type, string? entity, string? detail, long at) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{prev}|{id}|{actor}|{action}|{type}|{entity}|{detail}|{at}"))).ToLowerInvariant();

    /// <summary>Every entry stores the hash of the previous one; the newest (id, hash) is also kept in a protected file,
    /// so editing, deleting or truncating the log is detected by <see cref="VerifyAudit"/>.</summary>
    void AppendAudit(string actor, string action, string? type, string? id, string? detail)
    {
        lock (_lock)
        {
            var prev = S(Scalar("SELECT hash FROM audit WHERE hash<>'' ORDER BY id DESC LIMIT 1"));
            var at = NowMs;
            Exec("INSERT INTO audit(actor,action,entity_type,entity_id,detail,created_at,prev_hash) VALUES($1,$2,$3,$4,$5,$6,$7)", actor, action, type, id, detail, at, prev);
            var rowId = Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));
            var hash = AuditHash(prev, rowId, actor, action, type, id, detail, at);
            Exec("UPDATE audit SET hash=$1 WHERE id=$2", hash, rowId);
            WriteAuditHead(rowId, hash);
        }
    }

    static void WriteAuditHead(long id, string hash)
    {
        try { File.WriteAllBytes(Paths.File(AuditHeadFile), Protector.Protect(Encoding.UTF8.GetBytes($"{id}|{hash}"))); }
        catch { /* the chain itself still detects edits */ }
    }

    /// <summary>Chains entries written by earlier versions (before hashing existed), oldest first.</summary>
    void BackfillAuditChain()
    {
        lock (_lock)
        {
            var rows = Query("SELECT * FROM audit WHERE hash='' ORDER BY id");
            if (rows.Count == 0) return;
            var prev = S(Scalar("SELECT hash FROM audit WHERE hash<>'' AND id<$1 ORDER BY id DESC LIMIT 1", Convert.ToInt64(rows[0]["id"])));
            long lastId = 0; var lastHash = "";
            foreach (var r in rows)
            {
                var id = Convert.ToInt64(r["id"]);
                var h = AuditHash(prev, id, S(r["actor"]), S(r["action"]), r["entity_type"] as string, r["entity_id"] as string, r["detail"] as string, Convert.ToInt64(r["created_at"]));
                Exec("UPDATE audit SET prev_hash=$1, hash=$2 WHERE id=$3", prev, h, id);
                prev = h; lastId = id; lastHash = h;
            }
            var newest = One("SELECT id, hash FROM audit ORDER BY id DESC LIMIT 1");
            if (newest != null && Convert.ToInt64(newest["id"]) == lastId) WriteAuditHead(lastId, lastHash);
        }
    }

    /// <summary>Re-computes the whole chain. Returns the number of entries checked and every problem found.</summary>
    public (int entries, List<string> problems) VerifyAudit()
    {
        var problems = new List<string>();
        var rows = Query("SELECT * FROM audit ORDER BY id");
        var prev = ""; long lastId = 0;
        foreach (var r in rows)
        {
            var id = Convert.ToInt64(r["id"]);
            if (S(r["prev_hash"]) != prev) problems.Add($"Entry #{id}: link to the previous entry is broken (an entry before it was deleted or changed).");
            var h = AuditHash(S(r["prev_hash"]), id, S(r["actor"]), S(r["action"]), r["entity_type"] as string, r["entity_id"] as string, r["detail"] as string, Convert.ToInt64(r["created_at"]));
            if (h != S(r["hash"])) problems.Add($"Entry #{id} ({S(r["action"])}): content was modified.");
            prev = S(r["hash"]); lastId = id;
        }
        try
        {
            var path = Paths.File(AuditHeadFile);
            if (File.Exists(path))
            {
                var head = Encoding.UTF8.GetString(Protector.Unprotect(File.ReadAllBytes(path))).Split('|');
                if (head.Length == 2 && (long.Parse(head[0]) != lastId || head[1] != prev))
                    problems.Add($"The newest entries were removed: the log should end at entry #{head[0]} but ends at #{lastId}.");
            }
        }
        catch (Exception ex) { problems.Add("The protected audit anchor could not be read: " + ex.Message); }
        return (rows.Count, problems);
    }

    // ================================================================== card lifecycle

    /// <summary>ISSUED, REISSUED, PRINTED, EXPORTED, LOST.</summary>
    public void CardEvent(string personId, string evt, string detail, string actor) =>
        Exec("INSERT INTO card_events(person_id,event,detail,actor,created_at) VALUES($1,$2,$3,$4,$5)", CanonId(personId), evt, detail ?? "", actor, NowMs);

    public List<Dictionary<string, object?>> CardHistory(string personId) =>
        Query("SELECT * FROM card_events WHERE person_id=$1 ORDER BY id DESC", CanonId(personId));

    /// <summary>One row per person: card number (issues so far), first issue, last print/export, lost reports, and
    /// the remarks entered with the most recent issue or re-issue (the operator's own note, e.g. why it was
    /// re-issued) — used for the ID Card Register's company-wise export.</summary>
    public List<Dictionary<string, object?>> CardRegister() => Query("""
        SELECT p.id, p.name, p.rank, p.service_no, p.company, p.platoon, p.section, p.status, p.card_serial, p.created_at,
               (SELECT COUNT(*) FROM card_events c WHERE c.person_id=p.id AND c.event IN ('ISSUED','REISSUED')) AS issues,
               (SELECT MIN(created_at) FROM card_events c WHERE c.person_id=p.id AND c.event='ISSUED') AS issued_at,
               (SELECT MAX(created_at) FROM card_events c WHERE c.person_id=p.id AND c.event='REISSUED') AS reissued_at,
               (SELECT MAX(created_at) FROM card_events c WHERE c.person_id=p.id AND c.event IN ('PRINTED','EXPORTED')) AS printed_at,
               (SELECT COUNT(*) FROM card_events c WHERE c.person_id=p.id AND c.event='LOST') AS lost,
               (SELECT detail FROM card_events c WHERE c.person_id=p.id AND c.event IN ('ISSUED','REISSUED') ORDER BY created_at DESC, id DESC LIMIT 1) AS remarks
        FROM persons p WHERE p.id LIKE 'P%' ORDER BY p.id
        """);

    /// <summary>Lost card: records it and issues a new QR secret at once, so the old card is refused at every gate.</summary>
    public void ReportCardLost(string personId, string remarks, string actor = "PC-ADMIN")
    {
        var id = CanonId(personId);
        if (One("SELECT id FROM persons WHERE id=$1", id) == null) throw new StoreException("PERSON_NOT_FOUND", "Person is not in the register", 404);
        CardEvent(id, "LOST", remarks, actor);
        Audit(actor, "CARD_REPORTED_LOST", "PERSON", id, remarks);
        RotateSecret("persons", id, "Replacement for lost card" + (remarks.Trim().Length > 0 ? " — " + remarks.Trim() : ""));
    }

    /// <summary>Activates or suspends a credential (ACTIVE, SUSPENDED, …); a non-active person is refused at every gate.</summary>
    public void SetPersonStatus(string personId, string status, string actor = "PC-ADMIN")
    {
        var id = CanonId(personId); status = Upper(status);
        if (Exec("UPDATE persons SET status=$1, updated_at=$2 WHERE id=$3", status, NowMs, id) == 0) throw new StoreException("PERSON_NOT_FOUND", "Person is not in the register", 404);
        Audit(actor, "PERSON_STATUS_" + status, "PERSON", id); Notify();
    }

    // ================================================================== visitor / temporary passes

    /// <summary>Creates a visitor pass (ID G0001…): a person record limited to the validity window.</summary>
    public string CreateVisitorPass(JsonObject v, string actor = "PC-ADMIN")
    {
        long from = v["validFrom"]?.GetValue<long>() ?? 0, to = v["validTo"]?.GetValue<long>() ?? 0;
        if (to <= from) throw new StoreException("INVALID_VALIDITY", "'Valid to' must be after 'valid from'.", 400);
        if ((v["name"]?.ToString() ?? "").Trim().Length < 2) throw new StoreException("INVALID_NAME", "Visitor name is required", 400);
        var id = v["id"]?.ToString() is { Length: > 0 } given ? CanonId(given) : NextId("G", "persons");
        var o = new JsonObject
        {
            ["id"] = id, ["name"] = v["name"]?.ToString(), ["rank"] = "VISITOR", ["serviceNo"] = "", ["company"] = "Visitors",
            ["unit"] = v["organisation"]?.ToString() ?? "", ["role"] = "", ["category"] = "VISITOR", ["status"] = "ACTIVE",
            ["mobile"] = v["mobile"]?.ToString() ?? "", ["idCard"] = "", ["bloodGroup"] = "", ["accessLocations"] = v["accessLocations"]?.ToString() ?? "",
            ["notes"] = v["notes"]?.ToString() ?? "", ["purpose"] = v["purpose"]?.ToString() ?? "", ["host"] = v["host"]?.ToString() ?? "",
            ["idProof"] = v["idProof"]?.ToString() ?? "", ["validFrom"] = from, ["validTo"] = to,
        };
        UpsertPerson(o, actor);
        return id;
    }

    /// <summary>Ends a pass now: entry is refused from this moment, exit stays possible.</summary>
    public void EndVisitorPass(string id, string actor = "PC-ADMIN")
    {
        Exec("UPDATE persons SET valid_to=$1, updated_at=$1 WHERE id=$2 AND id LIKE 'G%'", NowMs, CanonId(id));
        Audit(actor, "END_VISITOR_PASS", "PERSON", CanonId(id)); Notify();
    }

    public List<Dictionary<string, object?>> Visitors() => Query("""
        SELECT p.*, (SELECT s.entry_at FROM presence s WHERE s.person_id=p.id AND s.status='ACTIVE' ORDER BY s.entry_at DESC LIMIT 1) AS inside_since,
               (SELECT MAX(e.event_ts) FROM events e WHERE e.entity_type='PERSON' AND e.entity_id=p.id AND e.event_type='EXIT') AS last_exit
        FROM persons p WHERE p.id LIKE 'G%' ORDER BY p.valid_from DESC
        """);

    /// <summary>Rejects an entry outside a pass's validity window (exits are always allowed).</summary>
    void CheckPassValidity(string personId, string type, long ts)
    {
        if (type != "ENTRY") return;
        var p = One("SELECT valid_from, valid_to FROM persons WHERE id=$1", personId);
        if (p == null) return;
        long from = Convert.ToInt64(p["valid_from"]), to = Convert.ToInt64(p["valid_to"]);
        if (to > 0 && ts > to) throw new StoreException("PASS_EXPIRED", "This pass has expired", 403);
        if (from > 0 && ts < from) throw new StoreException("PASS_NOT_YET_VALID", "This pass is not valid yet", 403);
    }

    // ================================================================== leave / overdue

    /// <summary>
    /// People whose last movement is an exit with an expected return date (leave, TD …), and visitors still inside
    /// after their pass ended. <c>overdue</c> = the return date / pass end has passed.
    /// </summary>
    public List<Dictionary<string, object?>> Absences()
    {
        var now = NowMs;
        var list = Query("""
            SELECT e.entity_id AS person_id, e.reason, e.remarks, e.event_ts AS left_at, e.expected_return, e.location_id, e.gate_id, e.operator_id,
                   p.name, p.rank, p.service_no, p.company, p.platoon, p.section, p.mobile
            FROM events e JOIN persons p ON p.id=e.entity_id
            WHERE e.entity_type='PERSON' AND e.event_type='EXIT' AND e.expected_return>0
              AND e.event_ts=(SELECT MAX(x.event_ts) FROM events x WHERE x.entity_type='PERSON' AND x.entity_id=e.entity_id AND x.event_type IN ('ENTRY','EXIT'))
            ORDER BY e.expected_return
            """);
        foreach (var r in list) { r["kind"] = "RETURN"; r["overdue"] = now > Convert.ToInt64(r["expected_return"]); }
        foreach (var v in Visitors().Where(v => v["inside_since"] != null && Convert.ToInt64(v["valid_to"]) > 0 && now > Convert.ToInt64(v["valid_to"])))
            list.Add(new Dictionary<string, object?>
            {
                ["kind"] = "VISITOR_OVERSTAY", ["person_id"] = v["id"], ["name"] = v["name"], ["rank"] = "VISITOR", ["service_no"] = "", ["company"] = "Visitors",
                ["platoon"] = "", ["section"] = "", ["mobile"] = v["mobile"], ["reason"] = "Visitor pass ended", ["remarks"] = S(v["pass_purpose"]),
                ["left_at"] = v["inside_since"], ["expected_return"] = v["valid_to"], ["overdue"] = true,
            });
        return list;
    }

    // ================================================================== remote wipe

    /// <summary>Revoked terminals are told to erase their data the next time they contact this PC.</summary>
    public bool IsRevokedDevice(string deviceId)
    {
        var r = One("SELECT active, wipe_ordered_at FROM devices WHERE device_id=$1", deviceId);
        if (r == null)
        {
            // Deleted from the list before it connected again: it still gets the wipe order.
            var gone = One("SELECT wipe_ordered_at FROM removed_devices WHERE device_id=$1", deviceId);
            if (gone == null) return false;
            if (Convert.ToInt64(gone["wipe_ordered_at"]) == 0)
            {
                Exec("UPDATE removed_devices SET wipe_ordered_at=$1 WHERE device_id=$2", NowMs, deviceId);
                Audit("SYSTEM", "REMOTE_WIPE_ORDERED", "DEVICE", deviceId, "removed terminal");
                Notify();
            }
            return true;
        }
        if (Convert.ToInt64(r["active"]) == 1) return false;
        if (Convert.ToInt64(r["wipe_ordered_at"]) == 0)
        {
            Exec("UPDATE devices SET wipe_ordered_at=$1 WHERE device_id=$2", NowMs, deviceId);
            Audit("SYSTEM", "REMOTE_WIPE_ORDERED", "DEVICE", deviceId);
            Notify();
        }
        return true;
    }
}
