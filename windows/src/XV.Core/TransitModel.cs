using System.Globalization;

namespace XV.Core;

/// <summary>Plain-language wording for vehicle trips, shared by the screen, the exports and the route chart.</summary>
public static class TransitText
{
    public static string Mins(long m) => m < 60 ? $"{m} min" : $"{m / 60}h {m % 60:00}m";

    static string G(IReadOnlyDictionary<string, object?> r, string key) => r.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
    static long N(IReadOnlyDictionary<string, object?> r, string key) => long.TryParse(G(r, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    static string Clock(long ms, string fmt) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString(fmt);

    public static string StateLabel(string state, bool overdue) => state switch
    {
        Store.TransitEnRoute => overdue ? "NOT REACHED" : "ON THE WAY",
        Store.TransitReached => "REACHED",
        Store.TransitDiverted => "MOVED ELSEWHERE",
        Store.TransitStopped => "STOPPED",
        _ => state,
    };

    public static string Via(string via, string by) => via switch
    {
        "SCAN" => "recorded by gate scan",
        "RP" => "entered by RP " + by,
        "SERVER" => "entered by the server",
        _ => "",
    };

    static string Dash(string id) => id.Length > 1 && char.IsLetter(id[0]) && char.IsDigit(id[1]) ? id[0] + "-" + id[1..] : id;

    static string When(long ms)
    {
        var d = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        return d.Date == DateTime.Today ? d.ToString("HH:mm") : d.ToString("dd MMM HH:mm");
    }

    /// <summary>The notice sent to the destination RP: which vehicle, where and when it left, and that it has not reached its location.</summary>
    public static string RpAlert(IReadOnlyDictionary<string, object?> t, long nowMs)
    {
        var plate = G(t, "plate"); var id = Dash(G(t, "vehicle_id"));
        var vehicle = plate.Length > 0 && !plate.Equals(G(t, "vehicle_id"), StringComparison.OrdinalIgnoreCase) ? $"{plate} ({id})" : id;
        var due = N(t, "due_at");
        var late = due > 0 && nowMs > due ? $" It is {Mins((nowMs - due) / 60_000)} late." : "";
        return $"Vehicle {vehicle} left {G(t, "from_name")} at {When(N(t, "left_at"))} and has not reached {G(t, "dest_name")}.{late}";
    }

    /// <summary>The stamp on every gate record once the server has stored it: when the server recorded it and its server sequence number.</summary>
    public static string ServerStamp(IReadOnlyDictionary<string, object?> e)
    {
        var at = N(e, "received_at");
        if (at == 0) return "";
        var how = G(e, "source") == "PC" ? " (entered at the Command Center)" : "";
        return $"Recorded by server {Clock(at, "dd MMM HH:mm:ss")} • Seq #{N(e, "seq")}{how}";
    }

    /// <summary>What happened to the "not reached" notice sent to the destination RP: sent, in transit, received by the phone, seen by the RP.</summary>
    public static string NoticeStatus(IReadOnlyDictionary<string, object?> t)
    {
        long sent = N(t, "rp_sent_at"), got = N(t, "rp_received_at"), seen = N(t, "rp_seen_at");
        if (G(t, "dest_loc").Length == 0) return "No notice: the destination is not a known location";
        if (sent == 0) return "RP notice: not sent yet";
        if (seen >= sent && seen > 0) return $"RP notice: SEEN {Clock(seen, "HH:mm")} (sent {Clock(sent, "HH:mm")}, received {Clock(got, "HH:mm")})";
        if (got >= sent && got > 0) return $"RP notice: RECEIVED by the phone {Clock(got, "HH:mm")} (sent {Clock(sent, "HH:mm")}) - not seen yet";
        return $"RP notice: SENT {Clock(sent, "HH:mm")} - IN TRANSIT, the phone has not received it yet";
    }

    /// <summary>The vehicle's details for the top of a route chart: registration, id, military registration, type, model, company, status.</summary>
    public static List<(string Label, string Value)> VehicleDetails(IReadOnlyDictionary<string, object?> v)
    {
        var all = new (string, string)[]
        {
            ("Registration", G(v, "plate")), ("Vehicle ID", Dash(G(v, "id"))), ("Military reg", G(v, "mil_reg")), ("Type", G(v, "type")),
            ("Model", G(v, "model")), ("Company", G(v, "company")), ("Status", G(v, "status")),
        };
        return all.Where(x => x.Item2.Length > 0).Select(x => (x.Item1, x.Item2)).ToList();
    }

    /// <summary>True when the trip is still open and past its approximate time.</summary>
    public static bool IsOverdue(IReadOnlyDictionary<string, object?> r, string prefix = "", long? nowMs = null)
    {
        var due = N(r, prefix + "due_at");
        return G(r, prefix + "state") == Store.TransitEnRoute && due > 0 && (nowMs ?? Store.NowMs) > due;
    }

    /// <summary>
    /// One line describing the trip that started with a vehicle exit. <paramref name="prefix"/> is "" for rows of the
    /// transits table and "tr_" for event rows joined with their trip. Empty when the exit had no destination.
    /// </summary>
    public static string Line(IReadOnlyDictionary<string, object?> r, string prefix = "", long? nowMs = null)
    {
        var dest = G(r, prefix + "dest_name");
        if (dest.Length == 0) return "";
        var now = nowMs ?? Store.NowMs;
        var state = G(r, prefix + "state");
        long expected = N(r, prefix + "expected_min"), actual = N(r, prefix + "actual_min"), due = N(r, prefix + "due_at");
        var end = G(r, prefix + "end_name");
        var by = Via(G(r, prefix + "resolved_via"), G(r, prefix + "resolved_by"));
        var approx = expected > 0 ? $"approx {Mins(expected)}" : "approx time not set";
        return state switch
        {
            Store.TransitEnRoute when due > 0 && now > due => $"To {dest} • {approx} • NOT REACHED, {Mins((now - due) / 60_000)} late",
            Store.TransitEnRoute when due > 0 => $"To {dest} • {approx} • ON THE WAY, due {Clock(due, "HH:mm")}",
            Store.TransitEnRoute => $"To {dest} • {approx} • ON THE WAY",
            Store.TransitReached => $"To {dest} • REACHED in {Mins(actual)}" + (expected > 0 ? $" ({approx})" : "") + (by.Length > 0 ? " • " + by : ""),
            Store.TransitDiverted => $"Planned {dest} • MOVED TO {end} in {Mins(actual)}" + (by.Length > 0 ? " • " + by : ""),
            Store.TransitStopped => $"Planned {dest} • STOPPED at {end} after {Mins(actual)}" + (by.Length > 0 ? " • " + by : ""),
            _ => "",
        };
    }
}

/// <summary>One line of a route chart: where someone arrived or left, the time away, and what happened on each trip.</summary>
public sealed record RouteStep(string Kind, long Ts, string Title, string Place, string Detail, string Status, string Crew = "");

public static class RouteModel
{
    static string S(object? o) => o?.ToString() ?? "";
    static long L(object? o) => o == null ? 0 : Convert.ToInt64(o, CultureInfo.InvariantCulture);
    static string Dur(long ms)
    {
        if (ms < 60_000) return "under 1 min";
        var t = TimeSpan.FromMilliseconds(ms);
        return t.Days > 0 ? $"{t.Days}d {t.Hours}h {t.Minutes}m" : t.Hours > 0 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m";
    }

    /// <summary>
    /// Builds the route from the records of one person or vehicle (newest first, as the History window loads them).
    /// <paramref name="manualTrips"/> are trips the server operator entered by hand (no gate record), merged in by time.
    /// </summary>
    public static List<RouteStep> Build(IEnumerable<Dictionary<string, object?>> newestFirst, IEnumerable<Dictionary<string, object?>>? manualTrips = null, long? nowMs = null)
    {
        var now = nowMs ?? Store.NowMs;
        var ev = newestFirst.Reverse().OrderBy(x => L(x["event_ts"])).ToList();
        var steps = new List<RouteStep>();
        long expectedBack = 0;
        var lastDriver = "";
        var lastCrew = "";
        // full = driver, co-driver and everyone else on board; short = driver and co-driver only
        string CrewOf(Dictionary<string, object?> r, bool full = true)
        {
            string d = S(r.GetValueOrDefault("driver_label")), c = S(r.GetValueOrDefault("co_driver_label")), o = S(r.GetValueOrDefault("occupant_labels"));
            return string.Join("  •  ", new[] { d.Length > 0 ? "Driver: " + d : "", c.Length > 0 ? "Co-driver: " + c : "", full && o.Length > 0 ? "Also on board: " + o : "" }.Where(x => x.Length > 0));
        }
        // the full crew is written where it arrives or changes, not repeated on every row
        string CrewIfNew(Dictionary<string, object?> r)
        {
            var crew = CrewOf(r);
            if (crew == lastCrew) return "";
            lastCrew = crew;
            return crew;
        }
        for (var i = 0; i < ev.Count; i++)
        {
            var e = ev[i];
            var type = S(e["event_type"]);
            var ts = L(e["event_ts"]);
            var place = $"{S(e["location_name"])} • {S(e["gate_name"])}";
            var note = string.Join("  •  ", new[]
            {
                S(e.GetValueOrDefault("reason")) is { Length: > 0 } r ? (S(e.GetValueOrDefault("remarks")) is { Length: > 0 } m ? $"{r} — {m}" : r) : S(e.GetValueOrDefault("remarks")),
                S(e.GetValueOrDefault("coming_from")) is { Length: > 0 } f ? "From " + f : "",
                L(e.GetValueOrDefault("loc_mismatch")) == 1 ? "Location flag" : "",
            }.Where(x => x.Length > 0));

            if (type == "ENTRY")
            {
                var exit = ev.Skip(i + 1).FirstOrDefault(x => S(x["event_type"]) == "EXIT");
                var stay = exit == null ? "Still inside" : L(exit["stay_ms"]) > 0 ? "Stayed " + Dur(L(exit["stay_ms"])) : "";
                var back = expectedBack > 0 ? (ts > expectedBack + 3_600_000 ? "Came back " + Dur(ts - expectedBack) + " after the expected date" : "Came back on time") : "";
                expectedBack = 0;
                var driver = S(e.GetValueOrDefault("driver_label"));
                var changed = driver.Length > 0 && lastDriver.Length > 0 && !driver.Equals(lastDriver, StringComparison.OrdinalIgnoreCase) ? "Driver changed (was " + lastDriver + ")" : "";
                if (driver.Length > 0) lastDriver = driver;
                steps.Add(new RouteStep("ARRIVED", ts, "ARRIVED", place, string.Join("  •  ", new[] { stay, back, changed, note }.Where(x => x.Length > 0)), "", CrewIfNew(e)));
            }
            else if (type == "EXIT")
            {
                var back = L(e.GetValueOrDefault("expected_return"));
                expectedBack = back;
                var detail = string.Join("  •  ", new[] { note, back > 0 ? "Expected back " + DateTimeOffset.FromUnixTimeMilliseconds(back).LocalDateTime.ToString("dd MMM yyyy") : "" }.Where(x => x.Length > 0));
                steps.Add(new RouteStep("LEFT", ts, "LEFT", place, detail, "", CrewIfNew(e)));

                var line = TransitText.Line(e, "tr_", now);
                var next = i + 1 < ev.Count ? ev[i + 1] : null;
                if (line.Length > 0)
                {
                    var overdue = TransitText.IsOverdue(e, "tr_", now);
                    var went = S(e["tr_state"]) is Store.TransitDiverted or Store.TransitStopped && S(e["tr_end_name"]).Length > 0 ? S(e["tr_end_name"]) : S(e["tr_dest_name"]);
                    steps.Add(new RouteStep("TRAVEL", ts, "TRAVEL", went, line, TransitText.StateLabel(S(e["tr_state"]), overdue), CrewOf(e, full: false)));
                }
                else if (next != null && S(next["event_type"]) == "ENTRY")
                    steps.Add(new RouteStep("AWAY", ts, "AWAY", "", $"Away for {Dur(L(next["event_ts"]) - ts)} before the next entry" + (S(e.GetValueOrDefault("entity_type")) == "VEHICLE" ? " (no destination was recorded)" : ""), ""));
                else if (next == null)
                    steps.Add(new RouteStep("AWAY", ts, "AWAY", "", $"Outside since this exit ({Dur(now - ts)} ago)", ""));
            }
            else steps.Add(new RouteStep("NOTE", ts, type.ToUpperInvariant(), place, note, ""));
        }
        foreach (var t in manualTrips ?? [])
        {
            var left = L(t["left_at"]);
            steps.Add(new RouteStep("LEFT", left, "LEFT", S(t["from_name"]), "Trip entered by the server operator", ""));
            var state = S(t["state"]);
            var went = state is Store.TransitDiverted or Store.TransitStopped && S(t["end_name"]).Length > 0 ? S(t["end_name"]) : S(t["dest_name"]);
            steps.Add(new RouteStep("TRAVEL", left, "TRAVEL", went, TransitText.Line(t, "", now), TransitText.StateLabel(state, TransitText.IsOverdue(t, "", now))));
        }
        if (manualTrips != null) steps = steps.OrderBy(x => x.Ts).ToList();
        return steps;
    }
}
