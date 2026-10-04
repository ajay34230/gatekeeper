using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>Dialogs of the Transit Times tab: standard times between locations, manual trips, closing a trip, per-vehicle averages.</summary>
public static class TransitDialogs
{
    public static string Mins(long minutes) => minutes < 60 ? $"{minutes} min" : $"{minutes / 60}h {minutes % 60:00}m";

    static List<string> LocationLabels() => App.Store.Locations().Select(l => $"{S(l["id"])} — {S(l["name"])}").ToList();
    static string IdOf(string label) => label.Split('—')[0].Trim();

    /// <summary>A location picked from the list becomes its ID; anything else typed is a new place name.</summary>
    static (string id, string name) PlaceFrom(string text, List<string> labels)
    {
        text = text.Trim();
        var hit = labels.FirstOrDefault(l => string.Equals(l, text, StringComparison.OrdinalIgnoreCase));
        return hit != null ? (IdOf(hit), "") : ("", text);
    }

    // ------------------------------------------------------------------ standard time between two locations

    sealed class RouteDialog : DarkWindow
    {
        public RouteDialog(string? from, string? to, long minutes) : base("Time between two locations",
            "The approximate time a vehicle should take. A vehicle that has not reached the destination after this time raises an alert on this server and at the destination RP.", 560, 470)
        {
            var labels = LocationLabels();
            string Pick(string? id) => id == null ? "" : labels.FirstOrDefault(l => IdOf(l) == id) ?? "";
            var f = Choice("From location", labels, Pick(from));
            var t = Choice("To location", labels, Pick(to));
            f.IsEnabled = t.IsEnabled = from == null;
            var m = Field("Approximate time (minutes)", minutes > 0 ? minutes.ToString() : "", mono: true);
            Body.Children.Add(Para("Example: 25 means a vehicle leaving the first location should reach the second within 25 minutes."));
            AddButton("Cancel", Close);
            AddButton("Save", () =>
            {
                try
                {
                    if (!int.TryParse(m.Text.Trim(), out var min)) throw new Exception("Enter the time in minutes, for example 25");
                    App.Store.UpsertTransitRoute(IdOf(f.Text), IdOf(t.Text), min, "SERVER");
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void EditRoute(Window owner, string? from = null, string? to = null, long minutes = 0) =>
        new RouteDialog(from, to, minutes) { Owner = owner }.ShowDialog();

    // ------------------------------------------------------------------ trip added by hand

    sealed class ManualTripDialog : DarkWindow
    {
        public ManualTripDialog() : base("Add a vehicle trip manually",
            "For a vehicle going to a new place, or one whose exit was not entered with a destination. The server watches the trip and alerts if the vehicle does not arrive in time.", 620, 790)
        {
            var labels = LocationLabels();
            var vehicles = App.Store.Vehicles().Where(v => S(v["status"]) == "ACTIVE").Select(v => $"{S(v["plate"])} — {DisplayId(S(v["id"]))}").ToList();
            var veh = Choice("Vehicle", vehicles, vehicles.FirstOrDefault() ?? "");
            var from = Choice("Left from (location)", labels, labels.FirstOrDefault() ?? "");
            var dest = Choice("Going to (pick a location, or type a new place)", labels, "", editable: true);
            Body.Children.Add(Label("Departure date"));
            var date = new DatePicker { SelectedDate = DateTime.Today, DisplayDateEnd = DateTime.Today };
            Body.Children.Add(date);
            var time = Field("Departure time (HH:mm)", DateTime.Now.ToString("HH:mm"), mono: true);
            var approx = Field("Approximate time to reach (minutes)", "", mono: true);
            var hint = Para("");
            Body.Children.Add(hint);
            var taken = Field("Already reached? Time taken (minutes). Leave blank if the vehicle is still on the way.", "", mono: true);

            void SyncHint()
            {
                var (destId, _) = PlaceFrom(dest.Text, labels);
                var std = destId.Length == 0 ? null : App.Store.TransitRoutes().FirstOrDefault(r => S(r["from_loc"]) == IdOf(from.Text) && S(r["to_loc"]) == destId);
                if (std != null)
                {
                    hint.Text = $"Standard time for this route: {Mins(L(std["minutes"]))}. Leave the field above blank to use it.";
                    return;
                }
                hint.Text = destId.Length == 0
                    ? "A new place is not in the Transit Times list, so enter the approximate time yourself to get an alert if the vehicle is late."
                    : "No standard time is saved for this route yet. The time you enter is saved for next time.";
            }
            dest.SelectionChanged += (_, _) => SyncHint();
            dest.LostFocus += (_, _) => SyncHint();
            from.SelectionChanged += (_, _) => SyncHint();
            SyncHint();

            AddButton("Cancel", Close);
            AddButton("Save Trip", () =>
            {
                try
                {
                    if (vehicles.Count == 0) throw new Exception("There are no active vehicles in the fleet.");
                    if (!TimeSpan.TryParse(time.Text.Trim(), out var tod)) throw new Exception("Enter the departure time as HH:mm, e.g. 14:30");
                    var left = new DateTimeOffset((date.SelectedDate ?? DateTime.Today).Date + tod).ToUnixTimeMilliseconds();
                    int Num(TextBox b, string what) => b.Text.Trim().Length == 0 ? 0 : int.TryParse(b.Text.Trim(), out var n) ? n : throw new Exception($"{what} must be a whole number of minutes");
                    var (destId, destName) = PlaceFrom(dest.Text, labels);
                    var vehicleId = veh.Text.Split('—').Last().Trim();
                    App.Store.AddManualTransit(vehicleId, IdOf(from.Text), destId.Length > 0 ? destId : destName, left, Num(approx, "Approximate time"), Num(taken, "Time taken"));
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void AddTrip(Window owner) => new ManualTripDialog { Owner = owner }.ShowDialog();

    // ------------------------------------------------------------------ closing a trip: reached / stopped / moved elsewhere

    sealed class ResolveTripDialog : DarkWindow
    {
        public ResolveTripDialog(Dictionary<string, object?> t, string kind) : base("Close trip • " + S(t["plate"]),
            $"{S(t["from_name"])} → {S(t["dest_name"])}  •  left {Time(L(t["left_at"]), "dd MMM HH:mm")}. The vehicle is only recorded as reached when you enter it here.", 620, 720)
        {
            var labels = LocationLabels();
            var elapsedMin = Math.Max(1, (Store.NowMs - L(t["left_at"])) / 60_000);
            Body.Children.Add(Label("What happened to the vehicle?"));
            RadioButton Radio(string text, bool on) { var r = new RadioButton { Content = text, GroupName = "outcome", IsChecked = on, Margin = new Thickness(0, 4, 0, 4), FontSize = 13 }; Body.Children.Add(r); return r; }
            var reached = Radio($"It reached {S(t["dest_name"])}", kind == Store.TransitReached);
            var stopped = Radio("It stopped on the way", kind == Store.TransitStopped);
            var moved = Radio("It moved to another location instead", kind == Store.TransitDiverted);
            var minLabel = Label("Time taken (minutes)");
            Body.Children.Add(minLabel);
            var minutes = new TextBox { Text = elapsedMin.ToString(), FontFamily = Mono };
            Body.Children.Add(minutes);
            var minHint = Para("");
            Body.Children.Add(minHint);
            var placeLabel = Label("Location name");
            var place = new ComboBox { IsEditable = true };
            foreach (var l in labels) place.Items.Add(l);
            Body.Children.Add(placeLabel);
            Body.Children.Add(place);
            var note = Field("Remarks (optional)");

            void Sync()
            {
                var showPlace = reached.IsChecked != true;
                placeLabel.Visibility = place.Visibility = showPlace ? Visibility.Visible : Visibility.Collapsed;
                placeLabel.Text = (stopped.IsChecked == true ? "Where did it stop? (location or place name)" : "Which location did it go to? (pick one, or type a new name)").ToUpperInvariant();
                minLabel.Text = (reached.IsChecked == true ? "Time taken to reach the destination (minutes)" : stopped.IsChecked == true ? "Time taken until it stopped (minutes)" : "Time taken to reach that location (minutes)").ToUpperInvariant();
                minHint.Text = $"It left at {Time(L(t["left_at"]), "HH:mm")}; {elapsedMin} minutes have passed since then. Enter the minutes from departure until the event, not from now.";
            }
            reached.Checked += (_, _) => Sync(); stopped.Checked += (_, _) => Sync(); moved.Checked += (_, _) => Sync();
            Sync();

            AddButton("Cancel", Close);
            AddButton("Save", () =>
            {
                try
                {
                    var k = reached.IsChecked == true ? Store.TransitReached : stopped.IsChecked == true ? Store.TransitStopped : Store.TransitDiverted;
                    if (!int.TryParse(minutes.Text.Trim(), out var m)) throw new Exception("Enter the time taken in minutes, for example 45");
                    var (pid, pname) = PlaceFrom(place.Text, labels);
                    App.Store.ResolveTransit(S(t["transit_id"]), k, m, pname, pid, "PC-ADMIN", "SERVER", note.Text);
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnEmerald");
        }
    }

    public static bool Resolve(Window owner, Dictionary<string, object?> trip, string kind) =>
        new ResolveTripDialog(trip, kind) { Owner = owner }.ShowDialog() == true;

    // ------------------------------------------------------------------ approximate time for a trip that has none

    sealed class ApproxDialog : DarkWindow
    {
        public ApproxDialog(Dictionary<string, object?> t) : base("Approximate time • " + S(t["plate"]),
            $"{S(t["from_name"])} → {S(t["dest_name"])}. Once set, the server alerts if the vehicle has not arrived in time.", 520, 360)
        {
            var m = Field("Approximate time to reach (minutes)", "", mono: true);
            AddButton("Cancel", Close);
            AddButton("Save", () =>
            {
                try
                {
                    if (!int.TryParse(m.Text.Trim(), out var min)) throw new Exception("Enter the time in minutes, for example 25");
                    App.Store.SetTransitApprox(S(t["transit_id"]), min);
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void SetApprox(Window owner, Dictionary<string, object?> trip) => new ApproxDialog(trip) { Owner = owner }.ShowDialog();

    // ------------------------------------------------------------------ average per vehicle

    sealed class AverageDialog : DarkWindow
    {
        public AverageDialog(string fromLoc, string toKey, string title) : base("Average time by vehicle", title, 700, 640)
        {
            var rows = App.Store.TransitAveragesByVehicle().Where(r => S(r["from_loc"]) == fromLoc && S(r["to_key"]) == toKey).ToList();
            if (rows.Count == 0) Body.Children.Add(Para("No completed trips yet."));
            foreach (var r in rows)
                Body.Children.Add(Card(Spread(
                    Row(Pill(S(r["plate"]), "#B45309", "#FEF3C7", "#FDE68A", 11), T($"   {L(r["trips"])} trip(s)", 11.5, "#64748B")),
                    T($"avg {Mins((long)Math.Round(Convert.ToDouble(r["avg_min"])))}   fastest {Mins(L(r["min_min"]))}   slowest {Mins(L(r["max_min"]))}", 11.5, "#334155", mono: true)),
                    "#FFFFFF", pad: 10).M(0, 0, 0, 6));
            AddButton("Close", Close, "BtnAmber");
        }
    }

    public static void ShowAverage(Window owner, string fromLoc, string toKey, string title) => new AverageDialog(fromLoc, toKey, title) { Owner = owner }.ShowDialog();
}

/// <summary>
/// Checks every 30 seconds for vehicles that have not reached their destination in time. The destination RP is alerted
/// through the Comms engine (rings even in the background) and this server shows a pop-up. Closing a pop-up only hides
/// it: it returns, with sound, after 15 minutes, until someone records what happened to the vehicle.
/// </summary>
public static class TransitMonitor
{
    static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(30) };
    static TransitAlertWindow? _window;
    public static int OverdueCount { get; private set; }
    public static int OpenCount { get; private set; }
    public static event Action? Changed;

    public static void Start()
    {
        Timer.Tick += (_, _) => Check();
        Timer.Start();
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Check);
    }

    public static void Check()
    {
        try
        {
            var open = App.Store.Transits(Store.TransitEnRoute);
            OpenCount = open.Count;
            OverdueCount = open.Count(t => t["overdue"] is true);
            Changed?.Invoke();
            RingDestinationTerminals();
            ShowServerAlert();
        }
        catch { /* the next tick tries again */ }
    }

    static void RingDestinationTerminals()
    {
        foreach (var t in App.Store.TransitsDueForRpAlert())
        {
            var text = $"VEHICLE NOT REACHED: {S(t["plate"])} left {S(t["from_name"])} at {Time(L(t["left_at"]), "HH:mm")} for {S(t["dest_name"])}. " +
                       $"It was expected by {Time(L(t["due_at"]), "HH:mm")} and is {Duration(L(t["late_ms"]))} late. " +
                       "Open 'Vehicles on the way' in this app and record when it arrived, that it stopped, or that it went to another location.";
            foreach (var dev in App.Store.TerminalsAt(S(t["dest_loc"])))
            {
                try { App.Comms.Send(dev, "ALERT", text, "Command Center"); } catch { /* terminal offline: the next round tries again */ }
            }
            App.Store.SnoozeTransitRp(S(t["transit_id"]));
        }
    }

    static void ShowServerAlert()
    {
        var due = App.Store.TransitsDueForServerAlert();
        if (due.Count == 0) return;
        if (_window != null) { if (_window.Update(due)) SystemSounds.Exclamation.Play(); return; }
        SystemSounds.Exclamation.Play();
        _window = new TransitAlertWindow(due);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }
}

public sealed class TransitAlertWindow : Window
{
    readonly StackPanel _list = new();
    readonly TextBlock _title = T("", 18, "#9F1239", bold: true);
    List<string> _shown = [];
    bool _clean;

    public TransitAlertWindow(List<Dictionary<string, object?>> due)
    {
        Title = "Vehicles not reached"; Width = 640; Height = Math.Min(620, SystemParameters.WorkArea.Height - 40); Topmost = true;
        Background = B("#FFF1F2"); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        var head = new Border
        {
            Padding = new Thickness(20, 16, 20, 12), BorderBrush = B("#9F1239"), BorderThickness = new Thickness(0, 5, 0, 0),
            Child = Col(_title, T("Closing this window with ✕ only hides it. It returns, with a sound, after 15 minutes until each vehicle is closed below.", 11.5, "#9F1239").Wrap().M(0, 4, 0, 0)),
        };
        var dock = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);
        dock.Children.Add(new ScrollViewer { Content = _list, Padding = new Thickness(20, 6, 20, 16) });
        Content = dock;
        Closed += (_, _) =>
        {
            if (!_clean) { try { App.Store.SnoozeTransitServer(_shown); } catch { } }
        };
        Update(due);
    }

    /// <summary>Redraws the list; returns true when a vehicle appeared that was not shown before.</summary>
    public bool Update(List<Dictionary<string, object?>> due)
    {
        var ids = due.Select(t => S(t["transit_id"])).ToList();
        var added = ids.Any(i => !_shown.Contains(i));
        _shown = ids;
        _title.Text = $"⚠  {due.Count} VEHICLE{(due.Count == 1 ? "" : "S")} NOT REACHED";
        _list.Children.Clear();
        foreach (var t in due) _list.Children.Add(TripRow(t).M(0, 8, 0, 0));
        return added;
    }

    UIElement TripRow(Dictionary<string, object?> t)
    {
        var dest = S(t["dest_name"]) + (S(t["dest_loc"]).Length == 0 ? "  (new place)" : "");
        var body = Col(
            Spread(Row(Pill(S(t["plate"]), "#B45309", "#FEF3C7", "#FDE68A", 12), T("  " + S(t["vehicle_type"]), 11.5, "#64748B")),
                   Pill("LATE " + Duration(L(t["late_ms"])), "#BE123C", "#FFF1F2", "#9F1239", 10)),
            T($"{S(t["from_name"])}  →  {dest}", 14, "#0F172A", bold: true).M(0, 8, 0, 2),
            T($"Left {Time(L(t["left_at"]), "dd MMM HH:mm")}  •  expected by {Time(L(t["due_at"]), "HH:mm")} ({TransitDialogs.Mins(L(t["expected_min"]))})", 11.5, "#64748B", mono: true),
            Row(Btn("Reached…", (_, _) => Act(t, Store.TransitReached), "BtnEmerald"),
                Btn("Stopped / moved to another location…", (_, _) => Act(t, Store.TransitDiverted), "BtnAmber"),
                Btn("Open Transit Times", (_, _) => OpenTab(), "BtnSmall")).M(0, 10, 0, 0));
        return Card(body, "#FFFFFF", "#FDA4AF");
    }

    void Act(Dictionary<string, object?> t, string kind)
    {
        TransitDialogs.Resolve(this, t, kind);
        TransitMonitor.Check();
        var due = App.Store.TransitsDueForServerAlert();
        if (due.Count == 0) { _clean = true; Close(); return; }
        Update(due);
    }

    void OpenTab()
    {
        if (Application.Current.MainWindow is MainWindow main) { main.ShowTransitTab(); main.Activate(); }
        Close();
    }
}
