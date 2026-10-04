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
public sealed record RouteStep(string Kind, long Ts, string Title, string Place, string Detail, string Status);

public static class RouteModel
{
    static string S(object? o) => o?.ToString() ?? "";
    static long L(object? o) => o == null ? 0 : Convert.ToInt64(o, CultureInfo.InvariantCulture);
    static string Dur(long ms) => ms < 60_000 ? "under 1 min" : Reports.Duration(ms);

    /// <summary>
    /// Builds the route from the records of one person or vehicle (newest first, as the History window loads them).
    /// <paramref name="manualTrips"/> are trips the server operator entered by hand (no gate record), merged in by time.
    /// </summary>
    public static List<RouteStep> Build(IEnumerable<Dictionary<string, object?>> newestFirst, IEnumerable<Dictionary<string, object?>>? manualTrips = null, long? nowMs = null)
    {
        var now = nowMs ?? Store.NowMs;
        var ev = newestFirst.Reverse().OrderBy(x => L(x["event_ts"])).ToList();
        var steps = new List<RouteStep>();
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
                steps.Add(new RouteStep("ARRIVED", ts, "ARRIVED", place, string.Join("  •  ", new[] { stay, note }.Where(x => x.Length > 0)), ""));
            }
            else if (type == "EXIT")
            {
                var back = L(e.GetValueOrDefault("expected_return"));
                var detail = string.Join("  •  ", new[] { note, back > 0 ? "Expected back " + DateTimeOffset.FromUnixTimeMilliseconds(back).LocalDateTime.ToString("dd MMM yyyy") : "" }.Where(x => x.Length > 0));
                steps.Add(new RouteStep("LEFT", ts, "LEFT", place, detail, ""));

                var line = TransitText.Line(e, "tr_", now);
                var next = i + 1 < ev.Count ? ev[i + 1] : null;
                if (line.Length > 0)
                {
                    var overdue = TransitText.IsOverdue(e, "tr_", now);
                    var went = S(e["tr_state"]) is Store.TransitDiverted or Store.TransitStopped && S(e["tr_end_name"]).Length > 0 ? S(e["tr_end_name"]) : S(e["tr_dest_name"]);
                    steps.Add(new RouteStep("TRAVEL", ts, "TRAVEL", went, line, TransitText.StateLabel(S(e["tr_state"]), overdue)));
                }
                else if (next != null && S(next["event_type"]) == "ENTRY")
                    steps.Add(new RouteStep("AWAY", ts, "AWAY", "", $"Away for {Dur(L(next["event_ts"]) - ts)} before the next entry (no destination was recorded)", ""));
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
