using System.Windows;
using System.Windows.Controls;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

public partial class MainWindow
{
    public void ShowTransitTab() => TabTransit.IsChecked = true;

    static string Mins(long minutes) => TransitDialogs.Mins(minutes);

    void RenderTransit()
    {
        var now = Store.NowMs;
        var q = Query.ToLowerInvariant();
        bool Match(Dictionary<string, object?> t) =>
            q.Length == 0 || $"{S(t["plate"])} {S(t["vehicle_id"])} {S(t["from_name"])} {S(t["dest_name"])} {S(t["end_name"])}".ToLowerInvariant().Contains(q);
        var open = App.Store.Transits(Store.TransitEnRoute).Where(Match).ToList();
        var recent = App.Store.Transits().Where(t => t["open"] is not true).Where(Match).Take(60).ToList();
        var routes = App.Store.TransitRoutes();
        var averages = App.Store.TransitAverages();
        var late = open.Count(t => t["overdue"] is true);

        Header("VEHICLE TRANSIT TIMES", late > 0 ? $"{late} NOT REACHED" : $"{open.Count} on the way",
            "How long vehicles take between locations. A vehicle is never marked as reached by itself: only a gate scan, the destination RP or this server can record it.");
        SectionActions.Children.Add(Btn("+ Add Trip", (_, _) => TransitDialogs.AddTrip(this), "BtnAmber"));
        SectionActions.Children.Add(Btn("+ Time Between Locations", (_, _) => TransitDialogs.EditRoute(this), "BtnGold"));
        SectionActions.Children.Add(Btn("Export Transit Report", (_, _) => Dialogs.TransitReport(this), "BtnEmerald"));

        var panel = new StackPanel();

        // ---- vehicles on the way
        panel.Children.Add(T("VEHICLES ON THE WAY", 11.5, "#64748B", bold: true).M(0, 0, 0, 8));
        if (open.Count == 0) panel.Children.Add(Empty("No vehicle is on the way. When a guard records a vehicle exit with a destination, it appears here."));
        var onWay = CardGrid();
        foreach (var t in open) onWay.Children.Add(TripCard(t, now).M(0, 0, 10, 10));
        panel.Children.Add(onWay);

        // ---- standard times
        panel.Children.Add(T("STANDARD TIME BETWEEN LOCATIONS", 11.5, "#64748B", bold: true).M(0, 18, 0, 8));
        panel.Children.Add(T("The vehicle is watched against these times. If the pair is missing, the RP is asked for an approximate time when the vehicle leaves.", 11, "#94A3B8").Wrap().M(0, 0, 0, 8));
        if (routes.Count == 0) panel.Children.Add(Empty("No times saved yet. Use '+ Time Between Locations'."));
        var routeGrid = CardGrid();
        foreach (var r in routes)
        {
            var from = S(r["from_loc"]); var to = S(r["to_loc"]);
            var body = Col(
                Spread(T($"{S(r["from_name"])}  →  {S(r["to_name"])}", 12.5, "#0F172A", bold: true), Pill(Mins(L(r["minutes"])), "#047857", "#ECFDF5", "#A7F3D0", 11)),
                Kv("Set by", S(r["source"]) == "RP" ? "RP at the gate" : "Server"),
                Wrap(Btn("Edit", (_, _) => TransitDialogs.EditRoute(this, from, to, L(r["minutes"]))),
                     Btn("Delete", (_, _) => { if (MessageBox.Show("Delete this time?", "Transit Times", MessageBoxButton.YesNo) == MessageBoxResult.Yes) App.Store.DeleteTransitRoute(from, to); }, "BtnDanger")).M(0, 8, 0, 0));
            routeGrid.Children.Add(Card(body, "#FFFFFF").M(0, 0, 10, 10));
        }
        panel.Children.Add(routeGrid);

        // ---- averages across vehicles
        panel.Children.Add(T("AVERAGE TIME TAKEN (ALL VEHICLES)", 11.5, "#64748B", bold: true).M(0, 18, 0, 8));
        panel.Children.Add(T("Counted only from trips that really arrived: a gate scan, or a time entered by the RP / server. Stopped trips are not counted.", 11, "#94A3B8").Wrap().M(0, 0, 0, 8));
        if (averages.Count == 0) panel.Children.Add(Empty("No completed trips yet."));
        var avgGrid = CardGrid();
        foreach (var a in averages)
        {
            var avg = (long)Math.Round(Convert.ToDouble(a["avg_min"]));
            var std = L(a["standard_min"]);
            var diff = std > 0 ? avg - std : 0;
            var compare = std == 0 ? Pill("no standard set", "#64748B", "#E2E8F0", "#CBD5E1", 9.5)
                : diff > 0 ? Pill($"{Mins(diff)} slower than standard", "#B45309", "#FEF3C7", "#FDE68A", 9.5)
                : diff < 0 ? Pill($"{Mins(-diff)} faster than standard", "#047857", "#ECFDF5", "#A7F3D0", 9.5)
                : Pill("same as standard", "#047857", "#ECFDF5", "#A7F3D0", 9.5);
            var from = S(a["from_loc"]); var toKey = S(a["to_key"]);
            var title = $"{S(a["from_name"])}  →  {S(a["to_name"])}";
            var body = Col(
                Spread(T(title, 12.5, "#0F172A", bold: true), Pill("avg " + Mins(avg), "#1D4ED8", "#EFF6FF", "#BFDBFE", 11)),
                Kv("Trips / vehicles", $"{L(a["trips"])} / {L(a["vehicles"])}"),
                Kv("Fastest / slowest", $"{Mins(L(a["min_min"]))} / {Mins(L(a["max_min"]))}"),
                Kv("Standard time", std > 0 ? Mins(std) : "not set"),
                compare.M(0, 6, 0, 0),
                Wrap(Btn("By vehicle", (_, _) => TransitDialogs.ShowAverage(this, from, toKey, title))).M(0, 8, 0, 0));
            avgGrid.Children.Add(Card(body, "#FFFFFF").M(0, 0, 10, 10));
        }
        panel.Children.Add(avgGrid);

        // ---- finished trips
        panel.Children.Add(T("RECENT TRIPS", 11.5, "#64748B", bold: true).M(0, 18, 0, 8));
        if (recent.Count == 0) panel.Children.Add(Empty("No finished trips yet."));
        foreach (var t in recent) panel.Children.Add(RecentRow(t).M(0, 0, 0, 6));

        ContentHost.Content = panel;
    }

    Border TripCard(Dictionary<string, object?> t, long now)
    {
        var overdue = t["overdue"] is true;
        var hasDue = L(t["due_at"]) > 0;
        var vehicleId = S(t["vehicle_id"]);
        var status = overdue ? Pill("NOT REACHED • " + Duration(L(t["late_ms"])) + " late", "#BE123C", "#FFF1F2", "#9F1239", 9.5)
            : hasDue ? Pill("ON THE WAY • due in " + Duration(L(t["due_at"]) - now), "#B45309", "#FEF3C7", "#FDE68A", 9.5)
            : Pill("TIME NOT SET", "#64748B", "#E2E8F0", "#CBD5E1", 9.5);
        var actions = new List<UIElement>
        {
            Btn("Reached…", (_, _) => TransitDialogs.Resolve(this, t, Store.TransitReached), "BtnEmerald"),
            Btn("Stopped / moved…", (_, _) => TransitDialogs.Resolve(this, t, Store.TransitDiverted), "BtnAmber"),
        };
        if (!hasDue) actions.Add(Btn("Set approx time", (_, _) => TransitDialogs.SetApprox(this, t), "BtnBlue"));
        actions.Add(Btn("History", (_, _) => Dialogs.History(this, "VEHICLE", vehicleId)));
        var body = Col(
            Spread(Row(Pill(S(t["plate"]), "#B45309", "#FEF3C7", "#FDE68A", 11), T("  " + DisplayId(vehicleId), 11, "#94A3B8", bold: true, mono: true)), status),
            Divider(),
            Kv("From", S(t["from_name"])),
            Kv("Going to", S(t["dest_name"]) + (S(t["dest_loc"]).Length == 0 ? "  (new place)" : "")),
            Kv("Left at", Time(L(t["left_at"]), "dd MMM HH:mm")),
            Kv("Approx. time", L(t["expected_min"]) > 0 ? Mins(L(t["expected_min"])) : "not set"),
            Kv("Expected by", hasDue ? Time(L(t["due_at"]), "dd MMM HH:mm") : "—"),
            Wrap(actions.ToArray()).M(0, 10, 0, 0));
        return Card(body, overdue ? "#FFF1F2" : "#FFFFFF", overdue ? "#9F1239" : "#E2E8F0");
    }

    Border RecentRow(Dictionary<string, object?> t)
    {
        var state = S(t["state"]);
        var pill = state switch
        {
            Store.TransitReached => Pill("REACHED", "#047857", "#ECFDF5", "#A7F3D0", 9.5),
            Store.TransitDiverted => Pill("MOVED ELSEWHERE", "#1D4ED8", "#EFF6FF", "#BFDBFE", 9.5),
            _ => Pill("STOPPED", "#BE123C", "#FFF1F2", "#FDA4AF", 9.5),
        };
        var how = S(t["resolved_via"]) switch { "SCAN" => "gate scan", "RP" => "entered by RP " + S(t["resolved_by"]), _ => "entered by the server" };
        var where = state == Store.TransitReached ? S(t["dest_name"]) : state == Store.TransitDiverted ? S(t["end_name"]) + $" (planned {S(t["dest_name"])})" : "stopped: " + S(t["end_name"]);
        var vehicleId = S(t["vehicle_id"]);
        return Card(SpreadWrap(
            Col(Row(Pill(S(t["plate"]), "#B45309", "#FEF3C7", "#FDE68A", 10.5), T($"   {S(t["from_name"])}  →  {where}", 12, "#0F172A", bold: true)),
                T($"Left {Time(L(t["left_at"]), "dd MMM HH:mm")}  •  took {Mins(L(t["actual_min"]))} (approx {(L(t["expected_min"]) > 0 ? Mins(L(t["expected_min"])) : "not set")})  •  {how}" + (S(t["note"]).Length > 0 ? "  •  " + S(t["note"]) : ""), 11, "#64748B", mono: true).Wrap().M(0, 3, 0, 0)),
            Row(pill, Btn("History", (_, _) => Dialogs.History(this, "VEHICLE", vehicleId)).M(8, 0, 0, 0))), "#FFFFFF", pad: 10);
    }
}
