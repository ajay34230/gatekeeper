using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

public partial class MainWindow : Window
{
    string _feedFilter = "ALL", _company = "ALL", _personView = "CARDS", _personSort = "ID";
    readonly HashSet<string> _selected = [];
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    bool _refreshQueued;

    public MainWindow()
    {
        InitializeComponent();
        App.Store.Changed += () => Dispatcher.BeginInvoke(QueueRefresh);
        App.Comms.Changed += () => Dispatcher.BeginInvoke(UpdateCommsBadge);
        OverdueMonitor.Changed += () => Dispatcher.BeginInvoke(UpdateOverdueBadge);
        Loaded += (_, _) => UpdateCommsBadge();
        App.Server.Log += msg => Dispatcher.BeginInvoke(() => StatusRight.Text = $"{DateTime.Now:HH:mm:ss}  {msg}");
        _timer.Tick += (_, _) => Refresh(full: false);
        _timer.Start();
        SizeChanged += (_, _) => QueueRefresh();
        Loaded += (_, _) => Refresh();
    }

    void QueueRefresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { _refreshQueued = false; Refresh(); });
    }

    int Columns => Math.Max(1, (int)((MainScroll.ActualWidth - 50) / 380));
    int IconColumns => Math.Max(4, (int)((MainScroll.ActualWidth - 50) / 118));
    string Query => Search.Text.Trim();

    // ------------------------------------------------------------------ refresh

    public void Refresh() => Refresh(true);

    public void Refresh(bool full)
    {
        var s = App.Store;
        var st = s.Stats();
        var lan = NetUtil.LanAddresses();
        HdrEndpoint.Text = $"{(lan.FirstOrDefault() ?? "localhost")}:{App.Settings.Port}";
        ServerBadgeText.Text = App.Server.Running ? "PC SERVER ACTIVE" : "SERVER STOPPED";
        ServerDot.Fill = App.Server.Running ? Res("Emerald") : Res("Rose");
        StatInside.Text = st.inside.ToString();
        StatInsideSub.Text = $"{st.outside} currently outside";
        StatFleet.Text = st.fleetIn.ToString();
        StatFleetSub.Text = $"{st.fleet} tactical units registered";
        StatFlags.Text = st.flags.ToString();
        StatTotal.Text = st.total.ToString();
        StatTotalSub.Text = $"Today: {st.entriesToday} in • {st.exitsToday} out";
        StatusLeft.Text = $"Server {App.Settings.ServerId} • TLS fingerprint {App.Server.Fingerprint[..16]}… • Internet access {(App.Settings.InternetEnabled ? "ON" : "OFF (LAN only)")} • Data: {Paths.DataDir}";

        TabFeed.Content = $"Live Feed ({st.total})";
        TabPersons.Content = $"Personnel Registry ({st.inside + st.outside})";
        TabVehicles.Content = $"Vehicle Fleet ({st.fleet})";
        var pending = s.Accounts().Count(a => S(a["status"]) == "PENDING");
        TabAccounts.Content = pending > 0 ? $"Accounts & Devices ({pending} pending)" : "Accounts & Devices";

        RenderGates();
        RenderDevices();
        if (full) RenderTab();
    }

    void RenderGates()
    {
        GateList.Children.Clear();
        var gates = App.Store.GateStations();
        var online = gates.Count(g => L(g["last_seen"]) > Store.NowMs - 120_000);
        GateOnline.Text = $"{online}/{gates.Count} ONLINE";
        if (gates.Count == 0)
        {
            GateList.Children.Add(Card(Col(T("No gates configured yet", 12, "#D4D4D8", bold: true),
                T("Open Stations & Settings to add your locations and gates.", 11, "#71717A").M(0, 4)), "#111113"));
            return;
        }
        foreach (var g in gates)
        {
            var isOnline = L(g["last_seen"]) > Store.NowMs - 120_000;
            var flags = L(g["flags"]);
            var body = Col(
                Spread(Row(T(S(g["id"]), 12.5, "#F4F4F5", bold: true, mono: true), T("  " + S(g["name"]), 12.5, "#E4E4E7", bold: true)),
                       isOnline ? Pill("ONLINE", "#34D399", "#0D2A20", "#047857", 9) : Pill("STANDBY", "#A1A1AA", "#27272A", "#3F3F46", 9)),
                Spread(T(S(g["location_id"]) is { Length: > 0 } loc ? loc : "—", 11, "#A1A1AA", mono: true),
                       T("Op: " + (S(g["operator_id"]) is { Length: > 0 } op ? op : "Unassigned"), 11, "#A1A1AA", mono: true)).M(0, 8),
                Divider(),
                Spread(Row(T($"↓ {L(g["entries"])} In", 11, "#34D399", mono: true), T($"      ↑ {L(g["exits"])} Out", 11, "#FBBF24", mono: true)),
                       flags > 0 ? T($"⚠ {flags} Flag", 11, "#FB7185", bold: true, mono: true) : T("0 Flags", 11, "#52525B", mono: true)));
            GateList.Children.Add(Card(body, "#111113").M(0, 0, 0, 10));
        }
    }

    void RenderDevices()
    {
        DeviceList.Children.Clear();
        var devs = App.Store.Devices();
        DeviceCount.Text = $"{devs.Count(d => L(d["active"]) == 1)} active";
        if (devs.Count == 0)
        {
            DeviceList.Children.Add(Card(Col(T("No terminals paired", 12, "#D4D4D8", bold: true),
                T("Click 'Local Wi-Fi & Pair Device' and scan the QR with the XV app.", 11, "#71717A").M(0, 4)), "#111113"));
            return;
        }
        foreach (var d in devs.Take(8))
        {
            var active = L(d["active"]) == 1;
            var online = active && L(d["last_seen"]) > Store.NowMs - 120_000;
            var body = Col(
                Spread(T(S(d["name"]), 12, "#F4F4F5", bold: true),
                    !active ? Pill("REVOKED", "#FDA4AF", "#2A0F14", "#9F1239", 9) : online ? Pill(S(d["via"]) == "INTERNET" ? "ONLINE • WAN" : "ONLINE • LAN", "#34D399", "#0D2A20", "#047857", 9) : Pill("OFFLINE", "#A1A1AA", "#27272A", "#3F3F46", 9)),
                T($"{S(d["device_id"])} • {S(d["last_ip"])} • seen {Ago(L(d["last_seen"]))}", 10.5, "#71717A", mono: true).M(0, 5),
                T($"Post: {S(d["location_id"])} {S(d["gate_id"])}  Op: {S(d["operator_id"])}  Queue: {L(d["pending"])}", 10.5, "#A1A1AA", mono: true).M(0, 3));
            DeviceList.Children.Add(Card(body, "#111113", pad: 11).M(0, 0, 0, 8));
        }
    }

    // ------------------------------------------------------------------ tabs

    void Tab_Checked(object sender, RoutedEventArgs e) { if (IsLoaded) { Search.Text = ""; RenderTab(); } }
    void Search_Changed(object sender, TextChangedEventArgs e) { SearchHint.Visibility = Search.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible; if (IsLoaded) RenderTab(); }

    void RenderTab()
    {
        SectionActions.Children.Clear();
        FilterChips.Children.Clear();
        if (TabFeed.IsChecked == true) RenderFeed();
        else if (TabPersons.IsChecked == true) RenderPersons();
        else if (TabVehicles.IsChecked == true) RenderVehicles();
        else if (TabAccounts.IsChecked == true) RenderAccounts();
        else if (TabSettings.IsChecked == true) RenderSettings();
        else if (TabExport.IsChecked == true) RenderExport();
        else RenderAudit();
    }

    void Chip(string label, bool active, Action onClick)
    {
        var b = new Button
        {
            Content = label, Style = (Style)FindResource("BtnSmall"), Margin = new Thickness(0, 0, 6, 6),
            Background = active ? B("#F59E0B") : B("#18181B"), Foreground = active ? B("#18181B") : B("#D4D4D8"),
            BorderBrush = active ? B("#FBBF24") : B("#3F3F46"),
        };
        b.Click += (_, _) => onClick();
        FilterChips.Children.Add(b);
    }

    UniformGrid CardGrid() => new() { Columns = Columns };

    void Header(string title, string count, string sub)
    {
        SectionTitle.Text = title; SectionCount.Text = count; SectionSub.Text = sub;
        SectionCountBorder.Visibility = count.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void RenderFeed()
    {
        var rows = App.Store.RecentEvents(400, Query, _feedFilter);
        Header("REAL-TIME GATE AUDIT STREAM", $"{rows.Count} events", $"Refreshed: {DateTime.Now:HH:mm:ss} • every record below was received from a paired terminal and acknowledged by this server");
        foreach (var f in new[] { "ALL", "PERSON", "VEHICLE", "ENTRY", "EXIT", "FLAGS" })
            Chip(f switch { "ALL" => "All Events", "PERSON" => "Personnel", "VEHICLE" => "Vehicles", "ENTRY" => "Entries", "EXIT" => "Exits", _ => "Location Flags" },
                _feedFilter == f, () => { _feedFilter = f; RenderTab(); });
        SectionActions.Children.Add(Btn("Export CSV", (_, _) => Dialogs.ExportEvents(this), "BtnEmerald"));
        if (rows.Count == 0) { ContentHost.Content = Empty("No matching activity events recorded for current filters."); return; }
        var grid = CardGrid();
        foreach (var r in rows)
        {
            var entry = S(r["event_type"]) == "ENTRY";
            var other = S(r["event_type"]) is not ("ENTRY" or "EXIT");
            var flag = L(r["loc_mismatch"]) == 1;
            var arrow = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(8), Background = B(other ? "#0B1B33" : entry ? "#0D2A20" : "#2A1F08"), BorderBrush = B(other ? "#1D4ED8" : entry ? "#065F46" : "#92400E"), BorderThickness = new Thickness(1),
                Child = T(other ? "●" : entry ? "↙" : "↗", 14, other ? "#93C5FD" : entry ? "#34D399" : "#FBBF24", bold: true).Center(),
            };
            var body = Col(
                Spread(Row(arrow, Col(T(DisplayId(S(r["entity_id"])), 11.5, "#E4E4E7", bold: true, mono: true), T(S(r["title"]), 12, "#F4F4F5", weight: FontWeights.SemiBold)).M(8)),
                       Col(T(Time(L(r["event_ts"])), 11, "#A1A1AA", mono: true), T(Time(L(r["event_ts"]), "dd MMM"), 10, "#52525B", mono: true))),
                Spread(T($"⌖ {S(r["location_name"])} • {S(r["gate_name"])}", 10.5, "#A1A1AA", mono: true),
                       L(r["stay_ms"]) > 0 ? T("Stayed: " + Duration(L(r["stay_ms"])), 10.5, "#D4D4D8", mono: true) : T(S(r["entity_type"]) == "VEHICLE" ? "VEHICLE" : "PERSON", 10, "#52525B", mono: true)).M(0, 10),
                T((other ? S(r["event_type"]).ToUpperInvariant() + " • " : "") + Note(r, " • ") + $"Op {S(r["operator_id"])} • {(S(r["source"]) == "PC" ? "Command Center" : "Terminal " + S(r["device_id"]))} • Seq #{L(r["seq"])}", 10, other ? "#93C5FD" : "#52525B", mono: true).M(0, 5));
            if (flag)
                body.Children.Add(Card(Spread(T("⚠ LOCATION MISMATCH", 10, "#FCD34D", bold: true, mono: true), T("QR: " + (S(r["scanned_loc"]) is { Length: > 0 } q ? q : "Diff Loc"), 10, "#FCD34D", mono: true)), "#3A2A0A", "#B45309", 7).M(0, 8));
            if (S(r["occupants"]).Length > 2)
            {
                var occ = System.Text.Json.JsonSerializer.Deserialize<List<string>>(S(r["occupants"])) ?? [];
                body.Children.Add(Divider());
                body.Children.Add(Spread(T("Manifest: " + string.Join(", ", occ.Select(DisplayId)), 10, "#A1A1AA", mono: true), T($"{occ.Count} aboard", 10, "#A1A1AA", mono: true)));
            }
            var card = Card(body, flag ? "#1C1508" : "#131316", flag ? "#B45309" : "#27272A").M(0, 0, 10, 10);
            if (S(r["entity_type"]) == "PERSON") { card.Cursor = System.Windows.Input.Cursors.Hand; var id = S(r["entity_id"]); card.MouseLeftButtonUp += (_, _) => Dialogs.History(this, "PERSON", id); }
            grid.Children.Add(card);
        }
        ContentHost.Content = grid;
    }

    static readonly string[] Companies = ["Alpha", "Bravo", "Charlie", "Delta", "SP", "HQ"];

    static readonly (string Key, string Label)[] PersonViews =
    [
        ("CARDS", "Detailed Cards"), ("COMPACT", "Compact Cards"), ("LIST", "List"), ("ICONS", "Icon Grid"), ("TABLE", "Table"),
    ];
    static readonly (string Key, string Label)[] PersonSorts =
    [
        ("ID", "ID (default)"), ("NAME", "Name"), ("RANK", "Rank (seniority)"), ("COMPANY", "Company"),
        ("PLATOON", "Platoon"), ("SERVICE", "Service No"), ("LASTSEEN", "Last Seen"),
    ];
    // Highest seniority first; a rank not in this list (or blank) sorts after every known rank.
    static readonly string[] RankSeniority =
    [
        "Brigadier", "Colonel", "Lieutenant Colonel", "Major", "Captain", "Lieutenant",
        "Subedar Major", "Subedar", "Naib Subedar", "Havildar", "Naik", "Lance Naik", "Sepoy",
    ];
    static int RankIndex(string rank) { var i = Array.IndexOf(RankSeniority, rank); return i < 0 ? RankSeniority.Length : i; }

    IEnumerable<Dictionary<string, object?>> SortPersons(IEnumerable<Dictionary<string, object?>> src) => _personSort switch
    {
        "NAME" => src.OrderBy(p => S(p["name"]), StringComparer.OrdinalIgnoreCase),
        "RANK" => src.OrderBy(p => RankIndex(S(p["rank"]))).ThenBy(p => S(p["name"]), StringComparer.OrdinalIgnoreCase),
        "COMPANY" => src.OrderBy(p => S(p["company"]), StringComparer.OrdinalIgnoreCase).ThenBy(p => S(p["name"]), StringComparer.OrdinalIgnoreCase),
        "PLATOON" => src.OrderBy(p => S(p["platoon"]), StringComparer.OrdinalIgnoreCase).ThenBy(p => S(p["name"]), StringComparer.OrdinalIgnoreCase),
        "SERVICE" => src.OrderBy(p => S(p["service_no"]), StringComparer.OrdinalIgnoreCase),
        "LASTSEEN" => src.OrderByDescending(p => L(p["last_seen"])),
        _ => src.OrderBy(p => S(p["id"]), StringComparer.OrdinalIgnoreCase),
    };

    void RenderPersons()
    {
        var all = App.Store.Soldiers(Query);
        var rows = _company == "ALL" ? all : all.Where(p => string.Equals(S(p["company"]), _company, StringComparison.OrdinalIgnoreCase)).ToList();
        _selected.IntersectWith(all.Select(p => S(p["id"])));
        Header("MILITARY & CIVILIAN PERSONNEL DOSSIER", $"{rows.Count} / {all.Count} Personnel", "Company-wise registry with printable QR ID cards, custom fields and full movement history. Tick cards to export selected personnel.");
        SectionActions.Children.Add(Btn("+ Add Soldier Details", AddSoldier_Click, "BtnAmber"));
        var exportSel = Btn(_selected.Count > 0 ? $"Export Selected ({_selected.Count})" : "Export Selected", (_, _) => Dialogs.Report(this, _company, _selected.ToList()), "BtnGold");
        exportSel.IsEnabled = _selected.Count > 0;
        SectionActions.Children.Add(exportSel);
        SectionActions.Children.Add(Btn("Reports", (_, _) => Dialogs.Report(this, _company, []), "BtnEmerald"));
        SectionActions.Children.Add(Btn(_selected.Count > 0 ? $"ID Cards ({_selected.Count})" : "ID Cards", (_, _) => CardStudioWindow.Show(this, _selected.ToList()), "BtnGold"));
        SectionActions.Children.Add(Btn("Card Register", (_, _) => CardRegisterWindow.ShowWindow(this)));
        SectionActions.Children.Add(Btn("Import / Export", ImportExport_Click));

        // Select Company dropdown + chips
        var pick = new ComboBox { Width = 190, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Center };
        pick.Items.Add("All Companies");
        foreach (var c in Companies) pick.Items.Add(c + " Company");
        pick.SelectedIndex = _company == "ALL" ? 0 : Array.IndexOf(Companies, _company) + 1;
        pick.SelectionChanged += (_, _) => { _company = pick.SelectedIndex <= 0 ? "ALL" : Companies[pick.SelectedIndex - 1]; RenderTab(); };
        FilterChips.Children.Add(Row(T("Select Company  ", 11.5, "#A1A1AA", bold: true), pick));

        var viewPick = new ComboBox { Width = 160, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Center };
        foreach (var v in PersonViews) viewPick.Items.Add(v.Label);
        viewPick.SelectedIndex = Math.Max(0, Array.FindIndex(PersonViews, v => v.Key == _personView));
        viewPick.SelectionChanged += (_, _) => { _personView = PersonViews[viewPick.SelectedIndex].Key; RenderTab(); };
        FilterChips.Children.Add(Row(T("View  ", 11.5, "#A1A1AA", bold: true), viewPick));

        var sortPick = new ComboBox { Width = 160, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Center };
        foreach (var v in PersonSorts) sortPick.Items.Add(v.Label);
        sortPick.SelectedIndex = Math.Max(0, Array.FindIndex(PersonSorts, v => v.Key == _personSort));
        sortPick.SelectionChanged += (_, _) => { _personSort = PersonSorts[sortPick.SelectedIndex].Key; RenderTab(); };
        FilterChips.Children.Add(Row(T("Sort by  ", 11.5, "#A1A1AA", bold: true), sortPick));

        Chip($"All ({all.Count})", _company == "ALL", () => { _company = "ALL"; RenderTab(); });
        foreach (var c in Companies)
            Chip($"{c} ({all.Count(p => string.Equals(S(p["company"]), c, StringComparison.OrdinalIgnoreCase))})", _company == c, () => { _company = c; RenderTab(); });
        if (rows.Count > 0) Chip(rows.All(p => _selected.Contains(S(p["id"]))) ? "Clear selection" : "Select all shown", false, () =>
        {
            if (rows.All(p => _selected.Contains(S(p["id"])))) _selected.Clear(); else foreach (var p in rows) _selected.Add(S(p["id"]));
            RenderTab();
        });
        if (rows.Count == 0) { ContentHost.Content = Empty(all.Count == 0 ? "The personnel registry is empty.\nAdd soldiers with '+ Add Soldier Details' or import a CSV file." : "No personnel in this company match the current filter."); return; }

        // List and Table lay every match out flat, sorted by the chosen key; the others stay grouped by company.
        if (_personView is "LIST" or "TABLE")
        {
            var flat = SortPersons(rows).ToList();
            ContentHost.Content = _personView == "TABLE" ? PersonTable(flat) : PersonListPanel(flat);
            return;
        }

        var panel = new StackPanel();
        var groups = rows.GroupBy(p => Companies.FirstOrDefault(c => string.Equals(c, S(p["company"]), StringComparison.OrdinalIgnoreCase)) ?? (S(p["company"]).Length > 0 ? S(p["company"]) : "Unassigned"))
            .OrderBy(g => Array.IndexOf(Companies, g.Key) is var i && i >= 0 ? i : 99);
        foreach (var g in groups)
        {
            var color = XV.Core.Reports.CompanyColor(g.Key);
            var inside = g.Count(p => L(p["inside_since"]) > 0);
            panel.Children.Add(new Border
            {
                Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 14, 10, 10), Padding = new Thickness(14, 8, 14, 8), CornerRadius = new CornerRadius(10),
                Background = B(color), Child = Spread(T($"{g.Key.ToUpperInvariant()} COMPANY", 13.5, "#FFFFFF", bold: true),
                    T($"{g.Count()} personnel • {inside} inside", 11.5, "#F4F4F5", mono: true)),
            });
            var sortedGroup = SortPersons(g).ToList();
            if (_personView == "ICONS")
            {
                var grid = new UniformGrid { Columns = IconColumns };
                foreach (var p in sortedGroup) grid.Children.Add(PersonIcon(p));
                panel.Children.Add(grid);
            }
            else
            {
                var grid = CardGrid();
                foreach (var p in sortedGroup) grid.Children.Add(_personView == "COMPACT" ? PersonCompactCard(p) : PersonCard(p));
                panel.Children.Add(grid);
            }
        }
        ContentHost.Content = panel;
    }

    Border PersonCard(Dictionary<string, object?> p)
    {
        var id = S(p["id"]);
        var inside = L(p["inside_since"]) > 0;
        var status = S(p["status"]);
        var check = new CheckBox { IsChecked = _selected.Contains(id), ToolTip = "Select for export", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        check.Click += (_, _) => { if (check.IsChecked == true) _selected.Add(id); else _selected.Remove(id); RenderTab(); };
        var avatar = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(10), Background = B("#27272A"), Child = T(Initials(S(p["name"])), 14, "#E4E4E7", bold: true).Center() };
        var body = Col(
            Spread(Row(check, avatar, Col(Row(T(DisplayId(id), 11, "#FBBF24", bold: true, mono: true), T("  " + S(p["company"]) + (S(p["company"]).Length > 0 ? " Co" : ""), 10.5, "#71717A", mono: true)),
                                 T(S(p["name"]), 13.5, "#F4F4F5", bold: true), T(string.Join(" • ", new[] { S(p["rank"]), S(p["role"]) }.Where(x => x.Length > 0)), 11, "#A1A1AA")).M(10)),
                   StatusPill(status)),
            Divider(),
            Kv("Service No", S(p["service_no"])), Kv("Unit", S(p["unit"])), Kv("I-Card", S(p["id_card"])), Kv("Category", S(p["category"])));
        try
        {
            var custom = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(S(p["custom_json"]).Length > 1 ? S(p["custom_json"]) : "{}") ?? [];
            foreach (var f in App.Settings.CustomFields) body.Children.Add(Kv(f, custom.GetValueOrDefault(f, "")));
        }
        catch { /* malformed custom data is shown as empty */ }
        body.Children.Add(Spread(T("Presence", 11, "#71717A"), inside ? Pill("INSIDE • " + Duration(Store.NowMs - L(p["inside_since"])), "#34D399", "#0D2A20", "#047857", 9.5) : Pill("OUTSIDE", "#A1A1AA", "#27272A", "#3F3F46", 9.5)).M(0, 3));
        body.Children.Add(Kv("Last seen", L(p["last_seen"]) > 0 ? Time(L(p["last_seen"]), "dd MMM HH:mm") : "No gate activity"));
        body.Children.Add(Wrap(Btn("Edit", (_, _) => Dialogs.EditPerson(this, id)), Btn("ID Card & QR", (_, _) => Dialogs.Credential(this, "PERSON", id), "BtnGold"),
                 Btn("History", (_, _) => Dialogs.History(this, "PERSON", id)), Btn("+ Add Record", (_, _) => Dialogs.AddRecord(this, id), "BtnBlue"),
                 Btn(status == "ACTIVE" ? "Suspend" : "Activate", (_, _) => Dialogs.SetPersonStatus(this, id, status == "ACTIVE" ? "SUSPENDED" : "ACTIVE")),
                 Btn("Delete", (_, _) => Dialogs.DeletePerson(this, id, S(p["name"])), "BtnDanger")).M(0, 10));
        return Card(body, _selected.Contains(id) ? "#1C1508" : "#131316", _selected.Contains(id) ? "#B45309" : "#27272A").M(0, 0, 10, 10);
    }

    /// <summary>A smaller card: avatar, name, rank/company, presence and just the three most-used actions.</summary>
    Border PersonCompactCard(Dictionary<string, object?> p)
    {
        var id = S(p["id"]);
        var inside = L(p["inside_since"]) > 0;
        var status = S(p["status"]);
        var check = new CheckBox { IsChecked = _selected.Contains(id), ToolTip = "Select for export", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        check.Click += (_, _) => { if (check.IsChecked == true) _selected.Add(id); else _selected.Remove(id); RenderTab(); };
        var avatar = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(9), Background = B("#27272A"), Child = T(Initials(S(p["name"])), 12, "#E4E4E7", bold: true).Center() };
        var body = Col(
            Spread(Row(check, avatar, Col(T(S(p["name"]), 12.5, "#F4F4F5", bold: true),
                         T(Parts(DisplayId(id), S(p["rank"]), S(p["company"]).Length > 0 ? S(p["company"]) + " Co" : ""), 10.5, "#A1A1AA", mono: true).M(0, 2)).M(8)),
                   StatusPill(status)),
            Spread(inside ? Pill("INSIDE", "#34D399", "#0D2A20", "#047857", 9.5) : Pill("OUTSIDE", "#A1A1AA", "#27272A", "#3F3F46", 9.5),
                   T(L(p["last_seen"]) > 0 ? Time(L(p["last_seen"]), "dd MMM HH:mm") : "No activity", 10, "#71717A", mono: true)).M(0, 8),
            Wrap(Btn("Edit", (_, _) => Dialogs.EditPerson(this, id)), Btn("ID Card", (_, _) => Dialogs.Credential(this, "PERSON", id), "BtnGold"),
                 Btn("History", (_, _) => Dialogs.History(this, "PERSON", id))).M(0, 8));
        return Card(body, _selected.Contains(id) ? "#1C1508" : "#131316", _selected.Contains(id) ? "#B45309" : "#27272A", pad: 11).M(0, 0, 10, 10);
    }

    /// <summary>A small round-photo tile with just the initials, name and ID — for scanning a whole company at a glance.
    /// A tick box in the corner selects for export; clicking the rest of the tile opens the movement history.</summary>
    Border PersonIcon(Dictionary<string, object?> p)
    {
        var id = S(p["id"]);
        var inside = L(p["inside_since"]) > 0;
        var selected = _selected.Contains(id);
        var check = new CheckBox { IsChecked = selected, ToolTip = "Select for export", HorizontalAlignment = HorizontalAlignment.Right };
        check.Click += (_, _) => { if (check.IsChecked == true) _selected.Add(id); else _selected.Remove(id); RenderTab(); };
        var avatar = new Border
        {
            Width = 50, Height = 50, CornerRadius = new CornerRadius(25), HorizontalAlignment = HorizontalAlignment.Center,
            Background = B(selected ? "#B45309" : "#27272A"), Child = T(Initials(S(p["name"])), 15, selected ? "#18181B" : "#E4E4E7", bold: true).Center(),
        };
        var name = T(S(p["name"]), 10.5, "#F4F4F5", bold: true).Wrap();
        name.TextAlignment = TextAlignment.Center; name.HorizontalAlignment = HorizontalAlignment.Center;
        var sub = T(DisplayId(id) + (inside ? " • IN" : ""), 9, inside ? "#34D399" : "#71717A", mono: true);
        sub.TextAlignment = TextAlignment.Center; sub.HorizontalAlignment = HorizontalAlignment.Center;
        var tile = new Border
        {
            Width = 108, Padding = new Thickness(8, 4, 8, 10), CornerRadius = new CornerRadius(10),
            Background = B(selected ? "#1C1508" : "#131316"), BorderBrush = B(selected ? "#B45309" : "#27272A"), BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand, ToolTip = Parts(S(p["name"]), S(p["rank"]), S(p["company"]).Length > 0 ? S(p["company"]) + " Co" : "", inside ? "Inside" : "Outside"),
            Child = Col(check, avatar.M(0, 2, 0, 0), name.M(0, 8, 0, 2), sub),
        };
        tile.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not CheckBox) Dialogs.History(this, "PERSON", id); };
        return tile;
    }

    /// <summary>One row per person, spanning the full width — quicker to scan than cards when the list is long.</summary>
    StackPanel PersonListPanel(List<Dictionary<string, object?>> rows)
    {
        var panel = new StackPanel();
        foreach (var p in rows)
        {
            var id = S(p["id"]);
            var inside = L(p["inside_since"]) > 0;
            var status = S(p["status"]);
            var check = new CheckBox { IsChecked = _selected.Contains(id), ToolTip = "Select for export", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            check.Click += (_, _) => { if (check.IsChecked == true) _selected.Add(id); else _selected.Remove(id); RenderTab(); };
            var avatar = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = B("#27272A"), Child = T(Initials(S(p["name"])), 11.5, "#E4E4E7", bold: true).Center() };
            var left = Row(check, avatar, Col(
                Row(T(DisplayId(id), 11, "#FBBF24", bold: true, mono: true), T("  " + S(p["name"]), 12.5, "#F4F4F5", bold: true)),
                T(Parts(S(p["rank"]), S(p["company"]).Length > 0 ? S(p["company"]) + " Co" : "", S(p["platoon"]), S(p["service_no"])), 10.5, "#A1A1AA", mono: true).M(0, 2)).M(10));
            var right = Row(
                inside ? Pill("INSIDE", "#34D399", "#0D2A20", "#047857", 9.5) : Pill("OUTSIDE", "#A1A1AA", "#27272A", "#3F3F46", 9.5),
                StatusPill(status).M(8, 0, 0, 0),
                Btn("Edit", (_, _) => Dialogs.EditPerson(this, id)).M(10, 0, 0, 0),
                Btn("ID Card", (_, _) => Dialogs.Credential(this, "PERSON", id), "BtnGold"),
                Btn("History", (_, _) => Dialogs.History(this, "PERSON", id)));
            panel.Children.Add(Card(SpreadWrap(left, right), _selected.Contains(id) ? "#1C1508" : "#131316", _selected.Contains(id) ? "#B45309" : "#27272A", 10).M(0, 0, 0, 8));
        }
        return panel;
    }

    /// <summary>A dense header + row table: ID, Name, Rank, Company, Platoon, Service No, Status, Presence, then actions.</summary>
    StackPanel PersonTable(List<Dictionary<string, object?>> rows)
    {
        double[] widths = [28, 62, 210, 120, 55, 50, 100, 95, 115];
        string[] heads = ["", "ID", "Name", "Rank", "Coy", "Pl", "Service No", "Status", "Presence"];

        Grid TableRow(Func<int, UIElement> cell)
        {
            var g = new Grid();
            for (var i = 0; i < widths.Length; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i <= widths.Length; i++) { var el = cell(i); Grid.SetColumn(el, i); g.Children.Add(el); }
            return g;
        }

        var panel = new StackPanel();
        panel.Children.Add(new Border { Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 4), Background = B("#111113"), CornerRadius = new CornerRadius(6),
            Child = TableRow(i => T(i < heads.Length ? heads[i] : "Actions", 10, "#71717A", bold: true, mono: true)) });
        foreach (var p in rows)
        {
            var id = S(p["id"]);
            var inside = L(p["inside_since"]) > 0;
            var status = S(p["status"]);
            var check = new CheckBox { IsChecked = _selected.Contains(id), ToolTip = "Select for export", VerticalAlignment = VerticalAlignment.Center };
            check.Click += (_, _) => { if (check.IsChecked == true) _selected.Add(id); else _selected.Remove(id); RenderTab(); };
            UIElement Cell(int i) => i switch
            {
                0 => check,
                1 => T(DisplayId(id), 11, "#FBBF24", mono: true),
                2 => T(S(p["name"]), 11.5, "#F4F4F5", bold: true),
                3 => T(S(p["rank"]), 11, "#D4D4D8"),
                4 => T(S(p["company"]), 11, "#A1A1AA", mono: true),
                5 => T(S(p["platoon"]), 11, "#A1A1AA", mono: true),
                6 => T(S(p["service_no"]), 11, "#A1A1AA", mono: true),
                7 => StatusPill(status),
                8 => inside ? Pill("INSIDE", "#34D399", "#0D2A20", "#047857", 9.5) : Pill("OUTSIDE", "#A1A1AA", "#27272A", "#3F3F46", 9.5),
                _ => Wrap(Btn("Edit", (_, _) => Dialogs.EditPerson(this, id)), Btn("ID Card", (_, _) => Dialogs.Credential(this, "PERSON", id), "BtnGold"), Btn("History", (_, _) => Dialogs.History(this, "PERSON", id))),
            };
            panel.Children.Add(new Border { Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 3), CornerRadius = new CornerRadius(6),
                Background = B(_selected.Contains(id) ? "#1C1508" : "#131316"), Child = TableRow(Cell) });
        }
        return panel;
    }

    void RenderVehicles()
    {
        var rows = App.Store.Vehicles(Query);
        Header("TACTICAL & LOGISTICS VEHICLE FLEET", $"{rows.Count} Vehicles", "Registered vehicles with windshield QR credentials. Occupant manifests are captured at the gate.");
        SectionActions.Children.Add(Btn("+ Register Vehicle", AddVehicle_Click, "BtnAmber"));
        SectionActions.Children.Add(Btn("Import / Export", ImportExport_Click, "BtnEmerald"));
        if (rows.Count == 0) { ContentHost.Content = Empty("No vehicles registered.\nUse '+ Register Vehicle' or import a CSV file."); return; }
        var grid = CardGrid();
        foreach (var v in rows)
        {
            var id = S(v["id"]); var inside = L(v["inside_since"]) > 0; var status = S(v["status"]);
            var body = Col(
                Spread(Row(Icon("", "#22D3EE", 22), Col(Row(Pill(S(v["plate"]), "#FDE68A", "#3A2A0A", "#B45309", 11), T("  " + DisplayId(id), 11, "#71717A", bold: true, mono: true)),
                                                             T(string.Join(" • ", new[] { S(v["type"]), S(v["model"]) }.Where(x => x.Length > 0)), 11.5, "#D4D4D8").M(0, 4)).M(10)),
                       StatusPill(status)),
                Divider(),
                Kv("Military Reg", S(v["mil_reg"])), Kv("Assigned Company", S(v["company"])),
                Spread(T("Presence", 11, "#71717A"), inside ? Pill("IN YARD • " + Duration(Store.NowMs - L(v["inside_since"])), "#FCD34D", "#3A2A0A", "#B45309", 9.5) : Pill("DISPATCHED / OUT", "#A1A1AA", "#27272A", "#3F3F46", 9.5)).M(0, 3),
                Wrap(Btn("Edit", (_, _) => Dialogs.EditVehicle(this, id)), Btn("Windshield QR", (_, _) => Dialogs.Credential(this, "VEHICLE", id), "BtnGold"),
                     Btn("History", (_, _) => Dialogs.History(this, "VEHICLE", id)),
                     Btn(status == "ACTIVE" ? "Suspend" : "Activate", (_, _) => Dialogs.SetVehicleStatus(this, id, status == "ACTIVE" ? "SUSPENDED" : "ACTIVE")),
                     Btn("Delete", (_, _) => Dialogs.DeleteVehicle(this, id, S(v["plate"])), "BtnDanger")).M(0, 10));
            grid.Children.Add(Card(body, "#131316").M(0, 0, 10, 10));
        }
        ContentHost.Content = grid;
    }

    void RenderAccounts()
    {
        var accounts = App.Store.Accounts();
        var devices = App.Store.Devices();
        Header("RP ACCOUNTS & PAIRED TERMINALS", $"{accounts.Count} accounts • {devices.Count} terminals",
            App.Settings.RequireApproval ? "Self-registered operators must be approved here before they can sign in." : "Self-registration is auto-approved (change in Stations & Settings).");
        SectionActions.Children.Add(Btn("+ Create Operator", (_, _) => Dialogs.CreateAccount(this), "BtnAmber"));
        SectionActions.Children.Add(Btn("Pair Terminal", Pair_Click, "BtnGold"));
        var panel = new StackPanel();
        panel.Children.Add(T("OPERATOR ACCOUNTS", 11.5, "#A1A1AA", bold: true).M(0, 0, 0, 8));
        if (accounts.Count == 0) panel.Children.Add(Empty("No operator accounts yet. Create one here, or operators can use 'Create Account' in the app."));
        var g1 = CardGrid();
        foreach (var a in accounts)
        {
            var id = S(a["id"]); var status = S(a["status"]);
            var actions = Wrap();
            if (status == "PENDING") actions.Children.Add(Btn("Approve", (_, _) => { App.Store.SetAccountStatus(id, "ACTIVE"); }, "BtnEmerald"));
            actions.Children.Add(status == "DISABLED" ? Btn("Enable", (_, _) => App.Store.SetAccountStatus(id, "ACTIVE")) : Btn("Disable", (_, _) => App.Store.SetAccountStatus(id, "DISABLED")));
            actions.Children.Add(Btn("Reset Password", (_, _) => Dialogs.ResetPassword(this, id)));
            actions.Children.Add(Btn("Delete", (_, _) => Dialogs.DeleteAccount(this, id), "BtnDanger"));
            var body = Col(
                Spread(Row(new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(10), Background = B("#F4F4F5"), Child = T(id.Length > 6 ? id[..6] : id, 10.5, "#18181B", bold: true, mono: true).Center() },
                           Col(T(S(a["name"]), 13, "#F4F4F5", bold: true), T(S(a["role"]), 11, "#A1A1AA")).M(10)),
                       status == "ACTIVE" ? Pill("ACTIVE", "#34D399", "#0D2A20", "#047857") : status == "PENDING" ? Pill("PENDING APPROVAL", "#FCD34D", "#3A2A0A", "#B45309") : Pill("DISABLED", "#FDA4AF", "#2A0F14", "#9F1239")),
                Divider(),
                Kv("Created", Time(L(a["created_at"]), "dd MMM yyyy HH:mm") + (S(a["created_by"]) == "SELF" ? " (self sign-up)" : " (by admin)")),
                Kv("Last sign-in", L(a["last_login"]) > 0 ? Time(L(a["last_login"]), "dd MMM HH:mm") : "Never"),
                actions.M(0, 10));
            g1.Children.Add(Card(body, status == "PENDING" ? "#1C1508" : "#131316", status == "PENDING" ? "#B45309" : "#27272A").M(0, 0, 10, 10));
        }
        panel.Children.Add(g1);
        panel.Children.Add(T("PAIRED TERMINALS", 11.5, "#A1A1AA", bold: true).M(0, 18, 0, 8));
        if (devices.Count == 0) panel.Children.Add(Empty("No terminals paired yet."));
        var g2 = CardGrid();
        foreach (var d in devices)
        {
            var id = S(d["device_id"]); var active = L(d["active"]) == 1;
            var body = Col(
                Spread(Col(T(S(d["name"]), 13, "#F4F4F5", bold: true), T($"{id} • {S(d["model"])}", 10.5, "#71717A", mono: true)),
                       active ? Pill("TRUSTED", "#34D399", "#0D2A20", "#047857") : Pill("REVOKED", "#FDA4AF", "#2A0F14", "#9F1239")),
                Divider(),
                Kv("Paired", Time(L(d["paired_at"]), "dd MMM yyyy HH:mm")), Kv("Last contact", $"{Ago(L(d["last_seen"]))} from {S(d["last_ip"])} ({(S(d["via"]).Length > 0 ? S(d["via"]) : "—")})"),
                Kv("Post / Operator", $"{S(d["location_id"])} {S(d["gate_id"])} / {S(d["operator_id"])}"), Kv("App version", S(d["app_version"])),
                Wrap(active ? Btn("Revoke Access", (_, _) => Dialogs.RevokeDevice(this, id), "BtnDanger") : Btn("Remove", (_, _) => { App.Store.DeleteDevice(id); App.Comms.Disconnect(id); }, "BtnDanger")).M(0, 10));
            g2.Children.Add(Card(body, "#131316").M(0, 0, 10, 10));
        }
        panel.Children.Add(g2);
        ContentHost.Content = panel;
    }

    /// <summary>Who/when/format/heading export, replacing the old single "Company-wise report…" button with real
    /// scope choices (individual, platoon, section, company, battalion/unit, everyone), real date ranges (today /
    /// this week / this month / entire history / a custom range), and a per-export customisable heading.</summary>
    void RenderExport()
    {
        Header("EXPORT REPORTS", "", "Choose who and what period to export, then pick a file format. CSV is data only; Excel and PDF print the heading below.");

        var persons = App.Store.Persons();
        string[] scopeLevels = ["All Personnel", "Battalion / Unit", "Company", "Platoon", "Section", "Individual"];
        var scopeBox = new ComboBox { Width = 260 };
        foreach (var lvl in scopeLevels) scopeBox.Items.Add(lvl);
        scopeBox.SelectedIndex = 0;

        var valuePanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        ComboBox? valueBox = null;

        void RebuildValuePanel()
        {
            valuePanel.Children.Clear();
            valueBox = null;
            var level = scopeBox.SelectedItem as string ?? "All Personnel";
            if (level == "All Personnel") return;
            if (level == "Individual")
            {
                var vb = new ComboBox { Width = 420, IsEditable = true, IsTextSearchEnabled = true };
                foreach (var p in persons.OrderBy(p => S(p["name"]))) vb.Items.Add($"{DisplayId(S(p["id"]))} — {S(p["rank"])} {S(p["name"])}".Trim());
                valuePanel.Children.Add(Lbl("Select person"));
                valuePanel.Children.Add(vb);
                valueBox = vb;
                return;
            }
            var field = level switch { "Battalion / Unit" => "unit", "Company" => "company", "Platoon" => "platoon", _ => "section" };
            var values = persons.Select(p => S(p[field])).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList();
            var vb2 = new ComboBox { Width = 300 };
            foreach (var v in values) vb2.Items.Add(v);
            if (values.Count > 0) vb2.SelectedIndex = 0;
            valuePanel.Children.Add(Lbl($"Select {level.ToLowerInvariant()}"));
            valuePanel.Children.Add(vb2);
            if (values.Count == 0) valuePanel.Children.Add(Prg($"No personnel have a {level.ToLowerInvariant()} recorded yet.", "#71717A"));
            valueBox = vb2;
        }
        scopeBox.SelectionChanged += (_, _) => RebuildValuePanel();
        RebuildValuePanel();

        string[] dateRanges = ["Today", "This Week (last 7 days)", "This Month", "Entire History", "Custom Range"];
        var rangeBox = new ComboBox { Width = 260 };
        foreach (var r in dateRanges) rangeBox.Items.Add(r);
        rangeBox.SelectedIndex = 2;

        var fromPick = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30), Width = 160 };
        var toPick = new DatePicker { SelectedDate = DateTime.Today, Width = 160 };
        var customPanel = Col(Lbl("From"), fromPick, Lbl("To"), toPick);
        customPanel.Visibility = Visibility.Collapsed;
        rangeBox.SelectionChanged += (_, _) => customPanel.Visibility = rangeBox.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;

        var withRecords = new CheckBox { Content = "Include gate & history records (untick for roster only)", IsChecked = true, Margin = new Thickness(0, 12, 0, 0) };

        var headingField = new TextBox { Text = App.Settings.ExportHeadingTemplate, FontFamily = Mono };
        var saveHeadingDefault = new CheckBox { Content = "Save as the default heading for future exports", IsChecked = false, Margin = new Thickness(0, 8, 0, 0) };

        (List<string> ids, string company, string scopeLabel) ResolveScope()
        {
            var level = scopeBox.SelectedItem as string ?? "All Personnel";
            if (level == "All Personnel") return ([], "ALL", "All Personnel");
            if (level == "Individual")
            {
                var id = (valueBox?.Text ?? "").Split(['—'], 2, StringSplitOptions.None)[0].Trim();
                var p = persons.FirstOrDefault(x => string.Equals(DisplayId(S(x["id"])), id, StringComparison.OrdinalIgnoreCase) || string.Equals(S(x["id"]), id, StringComparison.OrdinalIgnoreCase));
                if (p == null) return ([], "ALL", "Selected Person");
                var label = $"{S(p["rank"])} {S(p["name"])}".Trim();
                return ([S(p["id"])], "ALL", label.Length > 0 ? label : S(p["name"]));
            }
            var field = level switch { "Battalion / Unit" => "unit", "Company" => "company", "Platoon" => "platoon", _ => "section" };
            var val = valueBox?.SelectedItem as string ?? "";
            var ids = persons.Where(p => string.Equals(S(p[field]), val, StringComparison.OrdinalIgnoreCase)).Select(p => S(p["id"])).ToList();
            var suffix = level is "Platoon" or "Section" or "Company" ? " " + level : "";
            var company = level == "Company" ? val : "ALL";
            return (ids, company, (val + suffix).Trim());
        }

        (DateTime from, DateTime to) ResolveDates()
        {
            var today = DateTime.Today;
            return rangeBox.SelectedIndex switch
            {
                0 => (today, today),
                1 => (today.AddDays(-6), today),
                2 => (new DateTime(today.Year, today.Month, 1), today),
                3 => (new DateTime(2000, 1, 1), today),
                _ => (fromPick.SelectedDate ?? today.AddDays(-30), toPick.SelectedDate ?? today),
            };
        }

        void Export(string kind)
        {
            try
            {
                if (!AdminGate.Require(this, $"Export {kind.ToUpperInvariant()} report")) return;
                var (ids, company, scopeLabel) = ResolveScope();
                if ((scopeBox.SelectedItem as string) != "All Personnel" && ids.Count == 0) { MessageBox.Show(this, "No personnel match that selection.", "Export"); return; }
                var (from, to) = ResolveDates();
                if (from > to) throw new Exception("'From' must be before 'To'.");
                var template = headingField.Text.Trim().Length > 0 ? headingField.Text.Trim() : App.Settings.ExportHeadingTemplate;
                if (saveHeadingDefault.IsChecked == true && template != App.Settings.ExportHeadingTemplate) { App.Settings.ExportHeadingTemplate = template; App.Settings.Save(); }
                var heading = template.Replace("{Scope}", scopeLabel, StringComparison.OrdinalIgnoreCase);
                var req = new XV.Core.ReportRequest(company, ids, from, to, withRecords.IsChecked == true, heading);
                var safeName = new string(scopeLabel.Where(char.IsLetterOrDigit).ToArray());
                var name = $"XV-{(safeName.Length > 0 ? safeName : "Export")}-{from:yyyyMMdd}-{to:yyyyMMdd}";
                var (filter, ext) = kind switch { "xlsx" => ("Excel workbook|*.xlsx", ".xlsx"), "pdf" => ("PDF document|*.pdf", ".pdf"), _ => ("CSV|*.csv", ".csv") };
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = name + ext, Filter = filter };
                if (dlg.ShowDialog(this) != true) return;
                if (kind == "xlsx") XV.Core.Reports.Excel(App.Store, req, dlg.FileName);
                else if (kind == "pdf") XV.Core.Reports.Pdf(App.Store, req, dlg.FileName);
                else XV.Core.Reports.Csv(App.Store, req, dlg.FileName);
                if (MessageBox.Show(this, "Report saved. Open it now?", "Export finished", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export"); }
        }

        var panel = Col(
            Card(Col(T("WHO", 11, "#A1A1AA", bold: true).M(0, 0, 0, 8), scopeBox, valuePanel), "#141417").M(0, 0, 0, 14),
            Card(Col(T("WHEN", 11, "#A1A1AA", bold: true).M(0, 0, 0, 8), rangeBox, customPanel, withRecords), "#141417").M(0, 0, 0, 14),
            Card(Col(
                T("EXPORT HEADING", 11, "#A1A1AA", bold: true).M(0, 0, 0, 8),
                Prg("{Scope} is replaced with who this export covers -- e.g. \"Capt John Doe\", \"Alpha Company\", \"2 Platoon\". Also editable from Settings.", "#71717A"),
                headingField, saveHeadingDefault
            ), "#141417").M(0, 0, 0, 14),
            Card(Col(
                T("FILE FORMAT", 11, "#A1A1AA", bold: true).M(0, 0, 0, 10),
                Wrap(Btn("Export CSV", (_, _) => Export("csv"), "BtnBase"), Btn("Export PDF", (_, _) => Export("pdf"), "BtnDanger"), Btn("Export Excel", (_, _) => Export("xlsx"), "BtnEmerald"))
            ), "#141417")
        );
        ContentHost.Content = panel;
    }

    /// <summary>Settings used to be scattered across header buttons that each opened their own dialog -- this tab
    /// gives them one place with a quick-status summary up top and a clearly labelled card per area below.</summary>
    void RenderSettings()
    {
        var s = App.Settings;
        Header("COMMAND CENTER SETTINGS", "", "Station setup, data protection, connectivity and maintenance for this Command Center.");

        Border SettingsCard(string title, string desc, string actionLabel, RoutedEventHandler onClick, string style = "BtnAmber") =>
            Card(Col(
                T(title, 13.5, "#F4F4F5", bold: true).M(0, 0, 0, 6),
                T(desc, 11.5, "#A1A1AA").Wrap().M(0, 0, 0, 12),
                Btn(actionLabel, onClick, style)
            ), "#141417").M(0, 0, 12, 12);

        var quick = Card(Col(
            T("QUICK STATUS", 11, "#A1A1AA", bold: true).M(0, 0, 0, 10),
            Kv("Data sharing with terminals", s.DataSharing),
            Kv("Internet access", s.InternetEnabled ? "Enabled" : "Disabled (LAN only)"),
            Kv("Auto-lock", s.AutoLockMinutes > 0 ? $"{s.AutoLockMinutes} minute(s) idle" : "Off"),
            Kv("Outbound internet block", s.BlockOutbound ? "ON" : "OFF"),
            Kv("Self-registration approval", s.RequireApproval ? "Required" : "Auto-approved"),
            Kv("Administrator password", s.HasAdminPassword ? "Set" : "Not set"),
            Kv("Export heading template", s.ExportHeadingTemplate)
        ), "#111113").M(0, 0, 0, 16);

        var grid = CardGrid();
        grid.Children.Add(SettingsCard("Station & General",
            "Locations, gates, server name, custom fields, movement reasons, data protection, diagnostics and maintenance.",
            "Open Station & General Settings", (_, _) => new StationsWindow { Owner = this }.ShowDialog()));
        grid.Children.Add(SettingsCard("Cloud Link",
            "Internet connection method, allowed address ranges, and the public URL terminals use away from base.",
            "Open Cloud Link", (_, _) => new CloudLinkWindow { Owner = this }.ShowDialog(), "BtnBlue"));
        grid.Children.Add(SettingsCard("Local Wi-Fi & Pair Device",
            "Pair a new terminal to this Command Center over the local network with a one-time QR code.",
            "Scan / Show Pairing QR", Pair_Click, "BtnGold"));
        grid.Children.Add(SettingsCard("Administrator Password",
            s.HasAdminPassword ? "A password is set and protects sensitive actions (data protection changes, wipes, auto-lock)." : "No administrator password is set yet -- sensitive actions are currently unprotected.",
            s.HasAdminPassword ? "Change Password" : "Set Password", (_, _) => AdminGate.ChangePassword(this), "BtnGold"));
        grid.Children.Add(SettingsCard("Comms Center",
            "Messages, alerts and voice/video calls with paired terminals.",
            "Open Comms Center", Comms_Click, "BtnDanger"));
        grid.Children.Add(SettingsCard("Lock Now",
            "Lock this Command Center immediately. The administrator password is required to unlock it again.",
            "Lock Command Center", Lock_Click, "BtnDanger"));
        grid.Children.Add(SettingsCard("Reports & Export",
            "Export personnel and gate records by individual, platoon, section, company or battalion, for any date range, in CSV, PDF or Excel -- including the heading template above.",
            "Open Export Tab", (_, _) => TabExport.IsChecked = true, "BtnEmerald"));

        ContentHost.Content = Col(quick, grid);
    }

    void RenderAudit()
    {
        var rows = App.Store.AuditLog(500).Where(r => Query.Length == 0 || string.Join(" ", r.Values).Contains(Query, StringComparison.OrdinalIgnoreCase)).ToList();
        Header("SECURITY AUDIT TRAIL", $"{rows.Count} entries", "Every administrative change, sign-in and gate record is written to the encrypted audit log.");
        SectionActions.Children.Add(Btn("Verify integrity", (_, _) =>
        {
            var (n, problems) = App.Store.VerifyAudit();
            App.Store.AdminAudit("AUDIT_VERIFIED", problems.Count == 0 ? $"{n} entries intact" : $"{problems.Count} problem(s)");
            MessageBox.Show(this, problems.Count == 0
                ? $"All {n} audit entries are intact: every entry is chained to the previous one and nothing was edited, removed or truncated."
                : $"TAMPERING DETECTED in the audit trail ({problems.Count}):\n\n" + string.Join("\n", problems.Take(20)), "Audit trail integrity",
                MessageBoxButton.OK, problems.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Error);
        }, "BtnGold"));
        SectionActions.Children.Add(Btn("Export CSV", (_, _) => Dialogs.ExportAudit(this), "BtnEmerald"));
        if (rows.Count == 0) { ContentHost.Content = Empty("The audit trail is empty."); return; }
        var sp = new StackPanel();
        foreach (var r in rows)
        {
            var g = new Grid();
            foreach (var w in new[] { 150.0, 130, 230, 170 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            void Add(UIElement e, int c) { Grid.SetColumn(e, c); g.Children.Add(e); }
            Add(T(Time(L(r["created_at"]), "dd MMM yyyy HH:mm:ss"), 11, "#A1A1AA", mono: true), 0);
            Add(T(S(r["actor"]), 11, "#FBBF24", bold: true, mono: true), 1);
            Add(T(S(r["action"]), 11, "#F4F4F5", mono: true), 2);
            Add(T($"{S(r["entity_type"])} {S(r["entity_id"])}", 11, "#D4D4D8", mono: true), 3);
            Add(T(S(r["detail"]), 11, "#71717A", mono: true), 4);
            sp.Children.Add(new Border { Child = g, Padding = new Thickness(10, 7, 10, 7), BorderBrush = B("#1F1F23"), BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        ContentHost.Content = Card(sp, "#111113", pad: 4);
    }

    // ------------------------------------------------------------------ small builders

    // DarkWindow has its own Label/Para (protected members of that base class) -- MainWindow isn't a DarkWindow,
    // so inline panels built here (RenderExport, RenderSettings) need their own equivalents, named to avoid
    // colliding with System.Windows.Controls.Label.
    static TextBlock Lbl(string text) => new() { Text = text.ToUpperInvariant(), Foreground = B("#A1A1AA"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) };
    static TextBlock Prg(string text, string color = "#A1A1AA") => new() { Text = text, Foreground = B(color), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };

    static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    static Border StatusPill(string status) => status switch
    {
        "ACTIVE" => Pill("● Active", "#34D399", "#0D2A20", "#047857"),
        "SUSPENDED" => Pill("Suspended", "#FDA4AF", "#2A0F14", "#9F1239"),
        "FLAGGED" => Pill("Security Flag", "#FDA4AF", "#2A0F14", "#BE123C"),
        _ => Pill(status.Length > 0 ? status : "—", "#A1A1AA", "#27272A", "#3F3F46"),
    };

    static DockPanel Kv(string k, string v) => Spread(T(k, 11, "#71717A"), T(v.Length > 0 ? v : "—", 11, "#D4D4D8", mono: true)).M(0, 3);

    static WrapPanel Wrap(params UIElement[] items)
    {
        var w = new WrapPanel();
        foreach (var i in items) { if (i is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 6, 6); w.Children.Add(i); }
        return w;
    }

    // ------------------------------------------------------------------ header actions

    void AddSoldier_Click(object s, RoutedEventArgs e) => Dialogs.EditPerson(this, null);
    void AddVehicle_Click(object s, RoutedEventArgs e) => Dialogs.EditVehicle(this, null);
    void ImportExport_Click(object s, RoutedEventArgs e) => Dialogs.ImportExport(this);
    void Pair_Click(object s, RoutedEventArgs e) => new PairWindow { Owner = this }.ShowDialog();
    void Comms_Click(object s, RoutedEventArgs e) => CommsWindow.Show(this);
    void CardStudio_Click(object s, RoutedEventArgs e) => CardStudioWindow.Show(this);
    void Visitors_Click(object s, RoutedEventArgs e) => VisitorsWindow.ShowWindow(this);
    void Leave_Click(object s, RoutedEventArgs e) => LeaveWindow.ShowWindow(this);
    void Lock_Click(object s, RoutedEventArgs e) => AutoLock.LockNow();

    void UpdateOverdueBadge()
    {
        var n = OverdueMonitor.OverdueCount;
        OverdueBadge.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        OverdueBadgeText.Text = n > 99 ? "99+" : n.ToString();
    }

    void UpdateCommsBadge()
    {
        var n = App.Comms.Store.UnreadCounts().Values.Sum();
        CommsBadge.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        CommsBadgeText.Text = n > 99 ? "99+" : n.ToString();
    }

    void Refresh_Click(object s, RoutedEventArgs e) => Refresh();
    void StatInside_Click(object s, System.Windows.Input.MouseButtonEventArgs e) => TabPersons.IsChecked = true;
    void StatFleet_Click(object s, System.Windows.Input.MouseButtonEventArgs e) => TabVehicles.IsChecked = true;
    void StatFlags_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _feedFilter = "FLAGS"; TabFeed.IsChecked = true; RenderTab(); }

    public IEnumerable<(Action select, string name)> ScreenshotTabs()
    {
        yield return (() => { TabFeed.IsChecked = true; Refresh(); }, "01-live-feed");
        yield return (() => { TabPersons.IsChecked = true; Refresh(); }, "02-personnel");
        yield return (() => { TabVehicles.IsChecked = true; Refresh(); }, "03-vehicles");
        yield return (() => { TabAccounts.IsChecked = true; Refresh(); }, "04-accounts-devices");
        yield return (() => { TabAudit.IsChecked = true; Refresh(); }, "05-audit");
        yield return (() => { TabExport.IsChecked = true; Refresh(); }, "06-export");
        yield return (() => { TabSettings.IsChecked = true; Refresh(); }, "07-settings");
    }
}

static class Ext
{
    public static TextBlock Center(this TextBlock t) { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; return t; }
}
