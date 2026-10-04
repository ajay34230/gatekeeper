using System.Text.Json.Nodes;

namespace XV.Core;

/// <summary>
/// Vehicle transit tracking: when a vehicle leaves a location for another one, a "trip" is opened. A trip is never
/// assumed to have finished. It only closes when a gate scan is recorded at a location, or when the destination RP or the
/// server enters what happened (reached after N minutes, stopped, or moved to another location). Trips that run past
/// their approximate time raise alerts that repeat every <see cref="TransitSnoozeMinutes"/> minutes until closed.
/// </summary>
public sealed partial class Store
{
    public const int TransitSnoozeMinutes = 15;
    public const int MaxTransitMinutes = 2880;

    public const string TransitEnRoute = "EN_ROUTE", TransitReached = "REACHED", TransitStopped = "STOPPED", TransitDiverted = "DIVERTED";

    void MigrateTransit() => Exec("""
        CREATE TABLE IF NOT EXISTS transit_routes(from_loc TEXT NOT NULL, to_loc TEXT NOT NULL, minutes INTEGER NOT NULL,
          source TEXT NOT NULL DEFAULT 'SERVER', updated_at INTEGER NOT NULL, PRIMARY KEY(from_loc, to_loc));
        CREATE TABLE IF NOT EXISTS transits(transit_id TEXT PRIMARY KEY, vehicle_id TEXT NOT NULL, exit_event_id TEXT,
          from_loc TEXT NOT NULL, from_name TEXT NOT NULL DEFAULT '', dest_loc TEXT NOT NULL DEFAULT '', dest_name TEXT NOT NULL DEFAULT '',
          left_at INTEGER NOT NULL, expected_min INTEGER NOT NULL DEFAULT 0, due_at INTEGER NOT NULL DEFAULT 0,
          state TEXT NOT NULL DEFAULT 'EN_ROUTE', end_at INTEGER NOT NULL DEFAULT 0, end_loc TEXT NOT NULL DEFAULT '', end_name TEXT NOT NULL DEFAULT '',
          actual_min INTEGER, resolved_by TEXT NOT NULL DEFAULT '', resolved_via TEXT NOT NULL DEFAULT '', note TEXT NOT NULL DEFAULT '',
          pc_snooze_until INTEGER NOT NULL DEFAULT 0, rp_snooze_until INTEGER NOT NULL DEFAULT 0,
          created_at INTEGER NOT NULL, created_by TEXT NOT NULL DEFAULT '');
        CREATE INDEX IF NOT EXISTS ix_transits_state ON transits(state, due_at);
        CREATE INDEX IF NOT EXISTS ix_transits_vehicle ON transits(vehicle_id, left_at);
        """);

    string LocationName(string id) => S(Scalar("SELECT name FROM locations WHERE id=$1", id)) is { Length: > 0 } n ? n : id;

    // ------------------------------------------------------------------ standard times between locations (the Transit Times tab)

    public List<Dictionary<string, object?>> TransitRoutes() => Query("""
        SELECT r.from_loc, r.to_loc, r.minutes, r.source, r.updated_at,
               COALESCE(f.name, r.from_loc) AS from_name, COALESCE(t.name, r.to_loc) AS to_name
        FROM transit_routes r LEFT JOIN locations f ON f.id=r.from_loc LEFT JOIN locations t ON t.id=r.to_loc
        ORDER BY from_name, to_name
        """);

    public JsonArray TransitRoutesJson() => new(TransitRoutes().Select(r => (JsonNode)new JsonObject
    {
        ["from"] = S(r["from_loc"]), ["to"] = S(r["to_loc"]), ["minutes"] = Convert.ToInt64(r["minutes"]),
    }).ToArray());

    long RouteMinutes(string from, string to) =>
        from.Length == 0 || to.Length == 0 ? 0 : Convert.ToInt64(Scalar("SELECT minutes FROM transit_routes WHERE from_loc=$1 AND to_loc=$2", from, to) ?? 0L);

    public void UpsertTransitRoute(string from, string to, int minutes, string source = "SERVER", string actor = "PC-ADMIN")
    {
        from = CanonId(from); to = CanonId(to);
        if (from.Length == 0 || to.Length == 0) throw new StoreException("INVALID_ROUTE", "Choose both locations", 400);
        if (from == to) throw new StoreException("INVALID_ROUTE", "From and To must be different locations", 400);
        if (minutes < 1 || minutes > MaxTransitMinutes) throw new StoreException("INVALID_MINUTES", $"Time must be between 1 and {MaxTransitMinutes} minutes", 400);
        if (Count("SELECT COUNT(*) FROM locations WHERE id IN ($1,$2)", from, to) != 2) throw new StoreException("UNKNOWN_LOCATION", "Both locations must exist in Stations & Settings", 400);
        Exec("""
            INSERT INTO transit_routes(from_loc,to_loc,minutes,source,updated_at) VALUES($1,$2,$3,$4,$5)
            ON CONFLICT(from_loc,to_loc) DO UPDATE SET minutes=$3, source=$4, updated_at=$5
            """, from, to, minutes, source, NowMs);
        Audit(actor, "TRANSIT_ROUTE_SET", "ROUTE", $"{from}>{to}", $"{minutes} min");
        Notify();
    }

    public void DeleteTransitRoute(string from, string to, string actor = "PC-ADMIN")
    {
        Exec("DELETE FROM transit_routes WHERE from_loc=$1 AND to_loc=$2", CanonId(from), CanonId(to));
        Audit(actor, "TRANSIT_ROUTE_DELETED", "ROUTE", $"{CanonId(from)}>{CanonId(to)}");
        Notify();
    }

    // ------------------------------------------------------------------ opening a trip

    /// <summary>Called inside the vehicle EXIT transaction. Does nothing when the guard did not say where the vehicle is going.</summary>
    void OpenTransitFromExit(JsonObject e, string vehicleId, long ts, string actor)
    {
        var typed = Clip(T(e, "destinationName"), 60);
        var destId = CanonId(T(e, "destinationId"));
        long approx = e["transitMinutes"] is JsonValue mv && mv.TryGetValue<long>(out var mm) ? mm : 0;
        OpenTransit(vehicleId, T(e, "eventId"), Upper(T(e, "locationId")), destId, typed, ts, approx, actor);
    }

    string OpenTransit(string vehicleId, string? exitEventId, string fromLoc, string destId, string destTyped, long leftAt, long approx, string actor)
    {
        var known = destId.Length > 0 && Count("SELECT COUNT(*) FROM locations WHERE id=$1", destId) > 0;
        if (!known) destId = "";
        if (!known && destTyped.Length == 0) return "";
        var destName = known ? LocationName(destId) : destTyped;
        if (known && destId == fromLoc) return "";
        // A typed name that is really a known location is treated as that location, so it is monitored and reaches the right RP.
        if (!known)
        {
            var match = One("SELECT id, name FROM locations WHERE UPPER(name)=UPPER($1) OR id=$2", destTyped, CanonId(destTyped));
            if (match != null && S(match["id"]) != fromLoc) { destId = S(match["id"]); destName = S(match["name"]); known = true; }
        }
        if (approx < 0 || approx > MaxTransitMinutes) approx = 0;
        var standard = RouteMinutes(fromLoc, destId);
        var minutes = approx > 0 ? approx : standard;
        if (known && approx > 0 && standard == 0 && fromLoc.Length > 0)
            Exec("INSERT OR IGNORE INTO transit_routes(from_loc,to_loc,minutes,source,updated_at) VALUES($1,$2,$3,'RP',$4)", fromLoc, destId, approx, NowMs);
        var id = "TR-" + RandomCode(10);
        Exec("""
            INSERT INTO transits(transit_id,vehicle_id,exit_event_id,from_loc,from_name,dest_loc,dest_name,left_at,expected_min,due_at,created_at,created_by)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            """, id, vehicleId, exitEventId, fromLoc, LocationName(fromLoc), destId, destName, leftAt, minutes,
            minutes > 0 ? leftAt + minutes * 60_000 : 0L, NowMs, actor);
        Audit(actor, "TRANSIT_OPENED", "VEHICLE", vehicleId, $"{fromLoc} -> {destName} ({(minutes > 0 ? minutes + " min" : "time not set")})");
        return id;
    }

    /// <summary>Server operator opens a trip by hand (new destination, or a vehicle left without the guard entering it).
    /// With <paramref name="takenMin"/> the trip is recorded as already finished.</summary>
    public string AddManualTransit(string vehicleId, string fromLoc, string destLocOrName, long leftAt, int approxMin, int takenMin, string actor = "PC-ADMIN")
    {
        vehicleId = CanonId(vehicleId); fromLoc = CanonId(fromLoc);
        if (Count("SELECT COUNT(*) FROM vehicles WHERE id=$1", vehicleId) == 0) throw new StoreException("VEHICLE_NOT_FOUND", "Choose a vehicle from the fleet", 400);
        if (Count("SELECT COUNT(*) FROM locations WHERE id=$1", fromLoc) == 0) throw new StoreException("UNKNOWN_LOCATION", "Choose the location the vehicle left from", 400);
        if (destLocOrName.Trim().Length == 0) throw new StoreException("DESTINATION_REQUIRED", "Enter where the vehicle is going", 400);
        if (leftAt <= 0 || leftAt > NowMs + 60_000) throw new StoreException("INVALID_TIME", "The departure time cannot be in the future", 400);
        if (approxMin < 0 || approxMin > MaxTransitMinutes || takenMin < 0 || takenMin > MaxTransitMinutes) throw new StoreException("INVALID_MINUTES", "Check the minutes entered", 400);
        var created = Tx(() =>
        {
            if (Count("SELECT COUNT(*) FROM transits WHERE vehicle_id=$1 AND state='EN_ROUTE'", vehicleId) > 0)
                throw new StoreException("TRIP_ALREADY_OPEN", "This vehicle already has an open trip. Close it first.", 409);
            var typed = destLocOrName.Trim();
            var id = OpenTransit(vehicleId, null, fromLoc, CanonId(typed), typed, leftAt, approxMin, actor);
            if (id.Length == 0) throw new StoreException("INVALID_DESTINATION", "The destination must differ from the starting location", 400);
            if (takenMin > 0) ResolveTransitCore(id, TransitReached, takenMin, "", "", actor, "SERVER", "Entered by the server operator");
            return id;
        });
        Notify();
        return created;
    }

    // ------------------------------------------------------------------ closing a trip

    /// <summary>A recorded vehicle ENTRY closes an open trip: at the planned destination it is REACHED, anywhere else it is DIVERTED.</summary>
    void CloseTransitOnEntry(string vehicleId, string locationId, long ts, string actor)
    {
        var open = One("SELECT * FROM transits WHERE vehicle_id=$1 AND state='EN_ROUTE' ORDER BY left_at DESC LIMIT 1", vehicleId);
        if (open == null) return;
        var left = Convert.ToInt64(open["left_at"]);
        if (ts < left) return;
        var destLoc = S(open["dest_loc"]);
        var arrivedName = LocationName(locationId);
        var planned = destLoc.Length > 0 ? destLoc == locationId : string.Equals(S(open["dest_name"]), arrivedName, StringComparison.OrdinalIgnoreCase);
        Exec("""
            UPDATE transits SET state=$1, end_at=$2, end_loc=$3, end_name=$4, actual_min=$5, resolved_by=$6, resolved_via='SCAN', note=$7 WHERE transit_id=$8
            """, planned ? TransitReached : TransitDiverted, ts, locationId, arrivedName, Math.Max(1, (ts - left) / 60_000), actor,
            planned ? "Gate entry recorded at the destination" : $"Gate entry recorded at {arrivedName}, not the planned {S(open["dest_name"])}", S(open["transit_id"]));
        Audit(actor, planned ? "TRANSIT_REACHED" : "TRANSIT_DIVERTED", "VEHICLE", vehicleId, $"{S(open["from_name"])} -> {arrivedName} by gate scan");
    }

    /// <summary>
    /// Records the outcome of an open trip. <paramref name="kind"/> is REACHED (arrived at the planned destination after
    /// <paramref name="minutes"/>), STOPPED or DIVERTED (stopped at / moved to <paramref name="placeName"/>, reached after <paramref name="minutes"/>).
    /// Nothing is ever recorded as reached unless someone enters it here or a gate scan does.
    /// </summary>
    public void ResolveTransit(string transitId, string kind, int minutes, string placeName, string placeLocationId, string actor, string via, string note = "") =>
        Tx(() => { ResolveTransitCore(transitId, kind, minutes, placeName, placeLocationId, actor, via, note); return 0; });

    void ResolveTransitCore(string transitId, string kind, int minutes, string placeName, string placeLocationId, string actor, string via, string note)
    {
        kind = Upper(kind);
        if (kind is not (TransitReached or TransitStopped or TransitDiverted)) throw new StoreException("INVALID_OUTCOME", "Choose reached, stopped or moved to another location", 400);
        if (minutes < 1 || minutes > MaxTransitMinutes) throw new StoreException("INVALID_MINUTES", "Enter the time taken in minutes (1 or more)", 400);
        var t = One("SELECT * FROM transits WHERE transit_id=$1", transitId) ?? throw new StoreException("TRIP_NOT_FOUND", "Trip not found", 404);
        if (S(t["state"]) != TransitEnRoute) throw new StoreException("TRIP_CLOSED", "This trip is already closed", 409);
        var left = Convert.ToInt64(t["left_at"]);
        var endAt = left + minutes * 60_000L;
        if (endAt > NowMs + 60_000) throw new StoreException("INVALID_MINUTES", "That time taken would end in the future. Check the minutes.", 400);
        string endLoc, endName;
        if (kind == TransitReached)
        {
            endLoc = S(t["dest_loc"]); endName = S(t["dest_name"]);
        }
        else
        {
            var id = CanonId(placeLocationId);
            if (id.Length > 0 && Count("SELECT COUNT(*) FROM locations WHERE id=$1", id) > 0) { endLoc = id; endName = LocationName(id); }
            else
            {
                endName = Clip((placeName ?? "").Trim(), 60);
                if (endName.Length == 0) throw new StoreException("PLACE_REQUIRED", "Enter the location name", 400);
                var match = One("SELECT id, name FROM locations WHERE UPPER(name)=UPPER($1)", endName);
                endLoc = match == null ? "" : S(match["id"]);
                if (match != null) endName = S(match["name"]);
            }
        }
        Exec("""
            UPDATE transits SET state=$1, end_at=$2, end_loc=$3, end_name=$4, actual_min=$5, resolved_by=$6, resolved_via=$7, note=$8 WHERE transit_id=$9
            """, kind, endAt, endLoc, endName, minutes, actor, via, Clip(note ?? "", 200), transitId);
        Audit(actor, "TRANSIT_" + kind, "VEHICLE", S(t["vehicle_id"]), $"{S(t["from_name"])} -> {endName} in {minutes} min ({via})");
        Notify();
    }

    /// <summary>Sets the approximate time on a trip that has none, so it can raise alerts.</summary>
    public void SetTransitApprox(string transitId, int minutes, string actor = "PC-ADMIN")
    {
        if (minutes < 1 || minutes > MaxTransitMinutes) throw new StoreException("INVALID_MINUTES", $"Time must be between 1 and {MaxTransitMinutes} minutes", 400);
        var t = One("SELECT * FROM transits WHERE transit_id=$1 AND state='EN_ROUTE'", transitId) ?? throw new StoreException("TRIP_CLOSED", "This trip is already closed", 409);
        Exec("UPDATE transits SET expected_min=$1, due_at=$2 WHERE transit_id=$3", minutes, Convert.ToInt64(t["left_at"]) + minutes * 60_000L, transitId);
        if (S(t["dest_loc"]).Length > 0 && RouteMinutes(S(t["from_loc"]), S(t["dest_loc"])) == 0)
            Exec("INSERT OR IGNORE INTO transit_routes(from_loc,to_loc,minutes,source,updated_at) VALUES($1,$2,$3,'SERVER',$4)", S(t["from_loc"]), S(t["dest_loc"]), minutes, NowMs);
        Audit(actor, "TRANSIT_TIME_SET", "VEHICLE", S(t["vehicle_id"]), $"{minutes} min");
        Notify();
    }

    // ------------------------------------------------------------------ queries

    static void Decorate(Dictionary<string, object?> r, long now)
    {
        var open = S(r["state"]) == TransitEnRoute;
        var due = Convert.ToInt64(r["due_at"]);
        r["open"] = open;
        r["overdue"] = open && due > 0 && now > due;
        r["late_ms"] = open && due > 0 && now > due ? now - due : 0L;
        r["elapsed_ms"] = (open ? now : Convert.ToInt64(r["end_at"])) - Convert.ToInt64(r["left_at"]);
    }

    /// <summary>Trips newest first, optionally limited by state, vehicle and departure time.</summary>
    public List<Dictionary<string, object?>> Transits(string state = "", string vehicleId = "", long fromMs = 0, long toMs = long.MaxValue, int limit = 2000)
    {
        var rows = Query("""
            SELECT t.*, COALESCE(v.plate, t.vehicle_id) AS plate, COALESCE(v.type,'') AS vehicle_type
            FROM transits t LEFT JOIN vehicles v ON v.id=t.vehicle_id
            WHERE ($1='' OR t.state=$1) AND ($2='' OR t.vehicle_id=$2) AND t.left_at BETWEEN $3 AND $4
            ORDER BY t.left_at DESC LIMIT $5
            """, Upper(state), CanonId(vehicleId), fromMs, toMs, limit);
        var now = NowMs;
        foreach (var r in rows) Decorate(r, now);
        return rows;
    }

    public Dictionary<string, object?>? TransitForExit(string exitEventId)
    {
        var r = One("SELECT * FROM transits WHERE exit_event_id=$1", exitEventId);
        if (r != null) Decorate(r, NowMs);
        return r;
    }

    /// <summary>Open trips heading to one location: what that location's RP must be told about.</summary>
    public List<Dictionary<string, object?>> OpenTransitsTo(string locationId)
    {
        var rows = Query("""
            SELECT t.*, COALESCE(v.plate, t.vehicle_id) AS plate, COALESCE(v.type,'') AS vehicle_type
            FROM transits t LEFT JOIN vehicles v ON v.id=t.vehicle_id
            WHERE t.state='EN_ROUTE' AND t.dest_loc=$1 ORDER BY t.left_at
            """, CanonId(locationId));
        var now = NowMs;
        foreach (var r in rows) Decorate(r, now);
        return rows;
    }

    public JsonObject TransitJson(Dictionary<string, object?> r) => new()
    {
        ["transitId"] = S(r["transit_id"]), ["vehicleId"] = S(r["vehicle_id"]), ["plate"] = S(r["plate"]), ["vehicleType"] = S(r["vehicle_type"]),
        ["fromId"] = S(r["from_loc"]), ["fromName"] = S(r["from_name"]), ["destId"] = S(r["dest_loc"]), ["destName"] = S(r["dest_name"]),
        ["leftAt"] = Convert.ToInt64(r["left_at"]), ["expectedMin"] = Convert.ToInt64(r["expected_min"]), ["dueAt"] = Convert.ToInt64(r["due_at"]),
        ["overdue"] = r["overdue"] is true, ["rpSnoozeUntil"] = Convert.ToInt64(r["rp_snooze_until"]),
    };

    /// <summary>Overdue trips whose server-side alert is not snoozed.</summary>
    public List<Dictionary<string, object?>> TransitsDueForServerAlert() =>
        Transits(TransitEnRoute).Where(r => r["overdue"] is true && Convert.ToInt64(r["pc_snooze_until"]) <= NowMs).OrderBy(r => Convert.ToInt64(r["due_at"])).ToList();

    /// <summary>Overdue trips heading to a known location whose RP alert is due again.</summary>
    public List<Dictionary<string, object?>> TransitsDueForRpAlert() =>
        Transits(TransitEnRoute).Where(r => r["overdue"] is true && S(r["dest_loc"]).Length > 0 && Convert.ToInt64(r["rp_snooze_until"]) <= NowMs).ToList();

    public void SnoozeTransitServer(IEnumerable<string> ids)
    {
        foreach (var id in ids) Exec("UPDATE transits SET pc_snooze_until=$1 WHERE transit_id=$2 AND state='EN_ROUTE'", NowMs + TransitSnoozeMinutes * 60_000L, id);
    }

    public void SnoozeTransitRp(string transitId) =>
        Exec("UPDATE transits SET rp_snooze_until=$1 WHERE transit_id=$2 AND state='EN_ROUTE'", NowMs + TransitSnoozeMinutes * 60_000L, transitId);

    /// <summary>Average real time between two locations across all vehicles (trips that actually arrived at a known place).</summary>
    public List<Dictionary<string, object?>> TransitAverages(long fromMs = 0, long toMs = long.MaxValue)
    {
        var rows = Query("""
            SELECT t.from_loc, MAX(t.from_name) AS from_name,
                   CASE WHEN t.end_loc<>'' THEN t.end_loc ELSE t.end_name END AS to_key, MAX(t.end_name) AS to_name,
                   COUNT(*) AS trips, COUNT(DISTINCT t.vehicle_id) AS vehicles,
                   AVG(t.actual_min) AS avg_min, MIN(t.actual_min) AS min_min, MAX(t.actual_min) AS max_min
            FROM transits t
            WHERE t.state IN ('REACHED','DIVERTED') AND t.actual_min>0 AND (t.state='REACHED' OR t.end_loc<>'') AND t.left_at BETWEEN $1 AND $2
            GROUP BY t.from_loc, to_key ORDER BY from_name, to_name
            """, fromMs, toMs);
        foreach (var r in rows)
        {
            r["standard_min"] = RouteMinutes(S(r["from_loc"]), S(r["to_key"]));
            r["from_name"] = LocationName(S(r["from_loc"]));
        }
        return rows;
    }

    /// <summary>Per-vehicle average for one pair of locations (detail view and exports).</summary>
    public List<Dictionary<string, object?>> TransitAveragesByVehicle(long fromMs = 0, long toMs = long.MaxValue) => Query("""
        SELECT t.from_loc, MAX(t.from_name) AS from_name,
               CASE WHEN t.end_loc<>'' THEN t.end_loc ELSE t.end_name END AS to_key, MAX(t.end_name) AS to_name,
               t.vehicle_id, COALESCE(MAX(v.plate), t.vehicle_id) AS plate, COUNT(*) AS trips,
               AVG(t.actual_min) AS avg_min, MIN(t.actual_min) AS min_min, MAX(t.actual_min) AS max_min
        FROM transits t LEFT JOIN vehicles v ON v.id=t.vehicle_id
        WHERE t.state IN ('REACHED','DIVERTED') AND t.actual_min>0 AND (t.state='REACHED' OR t.end_loc<>'') AND t.left_at BETWEEN $1 AND $2
        GROUP BY t.from_loc, to_key, t.vehicle_id ORDER BY from_name, to_name, plate
        """, fromMs, toMs);

    /// <summary>Terminals (paired, active) whose assigned location is <paramref name="locationId"/>.</summary>
    public List<string> TerminalsAt(string locationId) =>
        Query("SELECT device_id FROM devices WHERE active=1 AND location_id=$1", CanonId(locationId)).Select(r => S(r["device_id"])).ToList();

    /// <summary>The location a terminal reports in its transit calls becomes its current location, so alerts reach the right phone
    /// even before its next heartbeat.</summary>
    public string TerminalLocation(string deviceId, string? reported)
    {
        var loc = CanonId(reported);
        if (loc.Length > 0 && Count("SELECT COUNT(*) FROM locations WHERE id=$1", loc) > 0)
        {
            Exec("UPDATE devices SET location_id=$1 WHERE device_id=$2 AND location_id<>$1", loc, deviceId);
            return loc;
        }
        return DeviceLocation(deviceId);
    }

    public string DeviceLocation(string deviceId) => S(Scalar("SELECT location_id FROM devices WHERE device_id=$1", deviceId));
}
