using System.IO;
using System.Media;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

// ====================================================================== visitor / temporary passes

public sealed class VisitorsWindow : DarkWindow
{
    static VisitorsWindow? _open;
    readonly StackPanel _list = new();
    string _filter = "ALL";

    public static void ShowWindow(Window owner)
    {
        if (_open != null) { _open.Activate(); return; }
        _open = new VisitorsWindow { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    internal VisitorsWindow() : base("Visitors & Temporary Passes",
        "Day / temporary passes with a QR that works only between 'valid from' and 'valid to'. Exits are always allowed, so nobody is trapped inside. Visitors still inside after the pass ends are flagged as overstay.", 1000, 860)
    {
        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (k, t) in new[] { ("ALL", "All passes"), ("INSIDE", "Inside now"), ("VALID", "Valid now"), ("EXPIRED", "Ended / expired") })
        {
            var b = Btn(t, (_, _) => { _filter = k; Fill(); }, "BtnSmall"); b.Margin = new Thickness(0, 0, 6, 6); chips.Children.Add(b);
        }
        Body.Children.Add(chips);
        Body.Children.Add(_list);
        AddButton("Close", Close);
        AddButton("Export visitors report…", ExportReport, "BtnEmerald");
        AddButton("+ New visitor pass", () => { var d = new VisitorPassDialog { Owner = this }; if (d.ShowDialog() == true && d.CreatedId != null) { Fill(); VisitorPassCard.Show(this, d.CreatedId); } }, "BtnAmber");
        void OnChange() => Dispatcher.BeginInvoke(Fill);
        App.Store.Changed += OnChange;
        Closed += (_, _) => App.Store.Changed -= OnChange;
        Fill();
    }

    static (string text, string fg, string bg, string border) State(Dictionary<string, object?> v)
    {
        var now = Store.NowMs;
        long from = L(v["valid_from"]), to = L(v["valid_to"]);
        var inside = v["inside_since"] != null;
        if (inside && to > 0 && now > to) return ("OVERSTAY", "#FDA4AF", "#2A0F14", "#9F1239");
        if (inside) return ("INSIDE", "#6EE7B7", "#062F23", "#047857");
        if (to > 0 && now > to) return ("ENDED", "#A1A1AA", "#18181B", "#3F3F46");
        if (from > 0 && now < from) return ("NOT YET VALID", "#93C5FD", "#0B1B33", "#1D4ED8");
        return ("VALID", "#FCD34D", "#2A1F08", "#92400E");
    }

    void Fill()
    {
        _list.Children.Clear();
        var now = Store.NowMs;
        var rows = App.Store.Visitors().Where(v => _filter switch
        {
            "INSIDE" => v["inside_since"] != null,
            "VALID" => L(v["valid_from"]) <= now && (L(v["valid_to"]) == 0 || now <= L(v["valid_to"])),
            "EXPIRED" => L(v["valid_to"]) > 0 && now > L(v["valid_to"]),
            _ => true,
        }).ToList();
        if (rows.Count == 0) { _list.Children.Add(Empty("No visitor passes here. Use '+ New visitor pass'.")); return; }
        foreach (var v in rows)
        {
            var id = S(v["id"]);
            var (text, fg, bg, border) = State(v);
            var info = Col(
                Row(T(S(v["name"]), 14, "#0F172A", bold: true), Pill(text, fg, bg, border, 9).M(10)),
                T(Parts(id, S(v["unit"]), Labeled("ID proof", S(v["id_proof"])), Labeled("Mobile", S(v["mobile"]))), 11, "#64748B", mono: true).Wrap().M(0, 3),
                T(Parts(Labeled("Purpose", S(v["pass_purpose"])), Labeled("Visiting", S(v["pass_host"]))), 11.5, "#475569").Wrap().M(0, 3),
                T($"Valid {Time(L(v["valid_from"]), "dd MMM yyyy HH:mm")} → {Time(L(v["valid_to"]), "dd MMM yyyy HH:mm")}" +
                  (v["inside_since"] != null ? $"   •   inside since {Time(L(v["inside_since"]), "HH:mm")}" : ""), 11, "#B45309", mono: true).M(0, 3));
            var actions = Row(
                Btn("Print pass", (_, _) => VisitorPassCard.Show(this, id), "BtnGold"),
                Btn("History", (_, _) => Dialogs.History(this, "PERSON", id)).M(6),
                Btn("End pass now", (_, _) =>
                {
                    if (MessageBox.Show($"End the pass of {S(v["name"])} now? Entry will be refused from this moment; exit stays possible.", "End pass", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                    App.Store.EndVisitorPass(id); App.Comms.RequestSyncAll("visitor pass ended");
                }, "BtnDanger").M(6));
            _list.Children.Add(Card(SpreadWrap(info, actions), pad: 12).M(0, 0, 0, 8));
        }
    }

    void ExportReport()
    {
        if (!AdminGate.Require(this, "Export visitors report")) return;
        var dlg = new SaveFileDialog { FileName = $"XV-Visitors-{DateTime.Now:yyyyMMdd-HHmm}.csv", Filter = "CSV (Excel)|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var rows = App.Store.Visitors().Select(v =>
        {
            var d = new Dictionary<string, object?>(v)
            {
                ["state"] = State(v).text, ["from"] = Time(L(v["valid_from"]), "yyyy-MM-dd HH:mm"), ["to"] = Time(L(v["valid_to"]), "yyyy-MM-dd HH:mm"),
                ["inside"] = v["inside_since"] != null ? Time(L(v["inside_since"]), "yyyy-MM-dd HH:mm") : "", ["exit"] = v["last_exit"] != null ? Time(L(v["last_exit"]), "yyyy-MM-dd HH:mm") : "",
            };
            return d;
        });
        File.WriteAllText(dlg.FileName, Csv.Build(rows, ("Pass", "id"), ("Name", "name"), ("Organisation", "unit"), ("Mobile", "mobile"), ("ID proof", "id_proof"),
            ("Purpose", "pass_purpose"), ("Visiting", "pass_host"), ("Valid from", "from"), ("Valid to", "to"), ("State", "state"), ("Inside since", "inside"), ("Last exit", "exit")), new System.Text.UTF8Encoding(true));
        MessageBox.Show("Visitors report saved.", "Visitors");
    }
}

public sealed class VisitorPassDialog : DarkWindow
{
    public string? CreatedId { get; private set; }

    public VisitorPassDialog() : base("New Visitor Pass", "The QR on the pass works at every gate only inside the validity window.", 560, 820)
    {
        var name = Field("Visitor's full name");
        var mobile = Field("Mobile number", "", mono: true);
        var org = Field("Organisation / address");
        var idp = Field("ID proof (type and number, e.g. Aadhaar 1234 5678 9012)");
        var purpose = Choice("Purpose of visit", ["Official meeting", "Delivery", "Contractor work", "Family visit", "Interview", "Maintenance"], "", editable: true);
        var soldiers = App.Store.Soldiers().Select(p => $"{DisplayId(S(p["id"]))} {S(p["rank"])} {S(p["name"])}".Trim()).ToList();
        var host = Choice("Visiting (person / office)", soldiers, "", editable: true);
        Body.Children.Add(Label("Valid from"));
        var fromDate = new DatePicker { SelectedDate = DateTime.Today }; var fromTime = new TextBox { Text = DateTime.Now.ToString("HH:mm"), Width = 80, FontFamily = Mono };
        Body.Children.Add(Row(fromDate, fromTime.M(8)));
        Body.Children.Add(Label("Valid to"));
        var toDate = new DatePicker { SelectedDate = DateTime.Today }; var toTime = new TextBox { Text = "18:00", Width = 80, FontFamily = Mono };
        Body.Children.Add(Row(toDate, toTime.M(8)));
        Body.Children.Add(Row(
            Btn("Today until 18:00", (_, _) => { fromDate.SelectedDate = toDate.SelectedDate = DateTime.Today; fromTime.Text = DateTime.Now.ToString("HH:mm"); toTime.Text = "18:00"; }),
            Btn("Next 24 hours", (_, _) => { var n = DateTime.Now; fromDate.SelectedDate = n.Date; fromTime.Text = n.ToString("HH:mm"); toDate.SelectedDate = n.AddDays(1).Date; toTime.Text = n.ToString("HH:mm"); }).M(6),
            Btn("7 days", (_, _) => { var n = DateTime.Now; fromDate.SelectedDate = n.Date; fromTime.Text = n.ToString("HH:mm"); toDate.SelectedDate = n.AddDays(7).Date; toTime.Text = "18:00"; }).M(6)).M(0, 8));
        var access = Field("Allowed locations (IDs, comma separated; blank = all)", "", mono: true);

        static long At(DatePicker d, TextBox t)
        {
            if (d.SelectedDate == null || !TimeSpan.TryParse(t.Text.Trim(), out var tod)) throw new ArgumentException("Enter dates and times as HH:mm.");
            return new DateTimeOffset(d.SelectedDate.Value.Date + tod).ToUnixTimeMilliseconds();
        }
        AddButton("Cancel", Close);
        AddButton("Create pass", () =>
        {
            try
            {
                CreatedId = App.Store.CreateVisitorPass(new JsonObject
                {
                    ["name"] = name.Text.Trim(), ["mobile"] = mobile.Text.Trim(), ["organisation"] = org.Text.Trim(), ["idProof"] = idp.Text.Trim(),
                    ["purpose"] = purpose.Text.Trim(), ["host"] = host.Text.Trim(), ["validFrom"] = At(fromDate, fromTime), ["validTo"] = At(toDate, toTime),
                    ["accessLocations"] = access.Text.Trim(),
                });
                App.Comms.RequestSyncAll("new visitor pass");
                DialogResult = true;
            }
            catch (Exception ex) { Fail(ex); }
        }, "BtnAmber");
    }
}

/// <summary>Printable visitor pass (CR-80 portrait) with the pass QR.</summary>
public sealed class VisitorPassCard : DarkWindow
{
    public static void Show(Window owner, string id) => new VisitorPassCard(id) { Owner = owner }.ShowDialog();

    VisitorPassCard(string id) : base("Visitor Pass " + id, "Hand this pass to the visitor. It works at the gates only inside the validity window.", 460, 820)
    {
        var v = App.Store.Visitors().First(x => S(x["id"]) == id);
        var p = App.Store.Person(id)!;
        var qr = new Image { Source = Dialogs.Png(Pairing.QrPng(S(p["secret_code"]), 8)), Width = 200, Height = 200, Margin = new Thickness(0, 10, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
        var card = new Border
        {
            Width = 340, Background = Brushes.White, CornerRadius = new CornerRadius(14), Padding = new Thickness(18), BorderBrush = B("#B45309"), BorderThickness = new Thickness(3),
            Child = Col(
                new Border { Background = B("#B45309"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6, 8, 6), Child = T("VISITOR PASS", 18, "#FFFFFF", bold: true).Center() },
                T(App.Settings.ServerName, 11, "#52525B").Center().M(0, 6),
                T(S(v["name"]), 18, "#18181B", bold: true).Center().M(0, 8),
                T(S(v["unit"]), 12, "#3F3F46").Center(),
                qr,
                T($"PASS {id}", 13, "#18181B", bold: true, mono: true).Center(),
                T($"Purpose: {S(v["pass_purpose"])}", 11.5, "#27272A").M(0, 8),
                T($"Visiting: {S(v["pass_host"])}", 11.5, "#27272A").M(0, 2),
                T($"ID proof: {S(v["id_proof"])}", 11.5, "#27272A").M(0, 2),
                new Border { Background = B("#FEF3C7"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 8, 0, 0),
                    Child = T($"VALID {Time(L(v["valid_from"]), "dd MMM HH:mm")} → {Time(L(v["valid_to"]), "dd MMM yyyy HH:mm")}", 12, "#92400E", bold: true, mono: true).Center() },
                T("Wear visibly • Return at exit gate", 10, "#71717A").Center().M(0, 8)),
        };
        Body.Children.Add(card);
        AddButton("Close", Close);
        AddButton("Save PNG", () =>
        {
            if (!AdminGate.Require(this, $"Save visitor pass {id}")) return;
            var dlg = new SaveFileDialog { FileName = $"{id}-visitor-pass.png", Filter = "PNG image|*.png" };
            if (dlg.ShowDialog() != true) return;
            card.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)(card.ActualWidth * 3), (int)(card.ActualHeight * 3), 288, 288, PixelFormats.Pbgra32);
            rtb.Render(card);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(dlg.FileName)) enc.Save(fs);
            App.Store.CardEvent(id, "EXPORTED", "Visitor pass PNG", "PC-ADMIN");
        });
        AddButton("Print", () =>
        {
            if (!AdminGate.Require(this, $"Print visitor pass {id}")) return;
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true) { pd.PrintVisual(card, "XV visitor pass " + id); App.Store.CardEvent(id, "PRINTED", "Visitor pass", "PC-ADMIN"); }
        }, "BtnAmber");
    }
}

// ====================================================================== leave / overdue

public sealed class LeaveWindow : DarkWindow
{
    static LeaveWindow? _open;
    readonly StackPanel _list = new();
    bool _overdueOnly;

    public static void ShowWindow(Window owner, bool overdueOnly = false)
    {
        if (_open != null) { _open._overdueOnly = overdueOnly; _open.Fill(); _open.Activate(); return; }
        _open = new LeaveWindow(overdueOnly) { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    internal LeaveWindow(bool overdueOnly) : base("Leave, TD & Overdue",
        "People who left with an expected return date (the terminal asks for it for the reasons set in Stations & Settings) and visitors still inside after their pass ended. Any entry — e.g. 'Rejoining from Leave' — closes the absence automatically.", 1000, 860)
    {
        _overdueOnly = overdueOnly;
        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        chips.Children.Add(Btn("Everyone out", (_, _) => { _overdueOnly = false; Fill(); }).M(0, 0, 6, 6));
        chips.Children.Add(Btn("Overdue only", (_, _) => { _overdueOnly = true; Fill(); }, "BtnDanger").M(0, 0, 6, 6));
        Body.Children.Add(chips);
        Body.Children.Add(_list);
        AddButton("Close", Close);
        AddButton("Export list…", Export, "BtnEmerald");
        void OnChange() => Dispatcher.BeginInvoke(Fill);
        App.Store.Changed += OnChange;
        Closed += (_, _) => App.Store.Changed -= OnChange;
        Fill();
    }

    static string Late(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays} d {t.Hours} h" : $"{t.Hours} h {t.Minutes} min";
    }

    void Fill()
    {
        _list.Children.Clear();
        var now = Store.NowMs;
        var rows = App.Store.Absences().Where(a => !_overdueOnly || a["overdue"] is true).ToList();
        if (rows.Count == 0) { _list.Children.Add(Empty(_overdueOnly ? "Nobody is overdue." : "Nobody is out with an expected return date.")); return; }
        foreach (var a in rows)
        {
            var overdue = a["overdue"] is true;
            var due = L(a["expected_return"]);
            var dueToday = !overdue && DateTimeOffset.FromUnixTimeMilliseconds(due).LocalDateTime.Date == DateTime.Today;
            var pill = overdue ? Pill("OVERDUE " + Late(now - due), "#FDA4AF", "#2A0F14", "#9F1239", 9)
                     : dueToday ? Pill("DUE TODAY", "#FCD34D", "#2A1F08", "#92400E", 9) : Pill("OUT", "#6EE7B7", "#062F23", "#047857", 9);
            var info = Col(
                Row(T($"{S(a["rank"])} {S(a["name"])}".Trim(), 14, "#0F172A", bold: true), pill.M(10)),
                T($"{DisplayId(S(a["person_id"]))}  •  {S(a["service_no"])}  •  {S(a["company"])} {S(a["platoon"])} {S(a["section"])}  •  Mobile {S(a["mobile"])}", 11, "#64748B", mono: true).M(0, 3),
                T($"{S(a["reason"])}{(S(a["remarks"]).Length > 0 ? " — " + S(a["remarks"]) : "")}", 12, "#475569").M(0, 3),
                T($"{(S(a["kind"]) == "VISITOR_OVERSTAY" ? "Inside since" : "Left")} {Time(L(a["left_at"]), "dd MMM yyyy HH:mm")}   →   {(S(a["kind"]) == "VISITOR_OVERSTAY" ? "pass ended" : "expected back")} {Time(due, "dd MMM yyyy HH:mm")}",
                  11, overdue ? "#BE123C" : "#B45309", mono: true).M(0, 3));
            var id = S(a["person_id"]);
            _list.Children.Add(Card(Spread(info, Row(Btn("History", (_, _) => Dialogs.History(this, "PERSON", id)))), pad: 12, border: overdue ? "#9F1239" : "#E2E8F0").M(0, 0, 0, 8));
        }
    }

    void Export()
    {
        if (!AdminGate.Require(this, "Export leave / overdue list")) return;
        var dlg = new SaveFileDialog { FileName = $"XV-Leave-Overdue-{DateTime.Now:yyyyMMdd-HHmm}.csv", Filter = "CSV (Excel)|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var rows = App.Store.Absences().Select(a => new Dictionary<string, object?>(a)
        {
            ["left"] = Time(L(a["left_at"]), "yyyy-MM-dd HH:mm"), ["due"] = Time(L(a["expected_return"]), "yyyy-MM-dd HH:mm"), ["state"] = a["overdue"] is true ? "OVERDUE" : "OUT",
        });
        File.WriteAllText(dlg.FileName, Csv.Build(rows, ("ID", "person_id"), ("Army No", "service_no"), ("Rank", "rank"), ("Name", "name"), ("Company", "company"), ("Platoon", "platoon"),
            ("Section", "section"), ("Reason", "reason"), ("Remarks", "remarks"), ("Left", "left"), ("Expected back", "due"), ("State", "state"), ("Mobile", "mobile")), new System.Text.UTF8Encoding(true));
        MessageBox.Show("List saved.", "Leave & Overdue");
    }
}

/// <summary>Checks every two minutes for people who became overdue and alerts once per absence.</summary>
public static class OverdueMonitor
{
    static readonly HashSet<string> Alerted = [];
    static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMinutes(2) };
    public static int OverdueCount { get; private set; }
    public static event Action? Changed;

    public static void Start()
    {
        Timer.Tick += (_, _) => Check();
        Timer.Start();
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Check);
    }

    public static void Check()
    {
        List<Dictionary<string, object?>> overdue;
        try { overdue = App.Store.Absences().Where(a => a["overdue"] is true).ToList(); } catch { return; }
        OverdueCount = overdue.Count;
        Changed?.Invoke();
        var fresh = overdue.Where(a => Alerted.Add($"{S(a["person_id"])}|{L(a["expected_return"])}")).ToList();
        if (fresh.Count == 0 || Application.Current.MainWindow is not Window main) return;
        foreach (var a in fresh) App.Store.AdminAudit("OVERDUE_ALERT", $"{S(a["person_id"])} {S(a["reason"])} due {Time(L(a["expected_return"]), "yyyy-MM-dd HH:mm")}");
        SystemSounds.Exclamation.Play();
        new AbsenceAlertWindow(fresh, overdue: true, main).Show();
        SendToChosenTerminals(fresh);
    }

    /// <summary>The PC always shows the alert; phones get it only if the server chose their location or RP in Settings.</summary>
    static void SendToChosenTerminals(List<Dictionary<string, object?>> fresh)
    {
        try
        {
            var s = App.Settings;
            var devices = App.Store.TerminalsForAlert(s.AbsenceAlertLocations, s.AbsenceAlertOperators);
            if (devices.Count == 0) return;
            var unsent = fresh.Where(a => !s.AbsenceAlertsSent.Contains($"{S(a["person_id"])}|{L(a["expected_return"])}")).ToList();
            foreach (var a in unsent) s.AbsenceAlertsSent.Add($"{S(a["person_id"])}|{L(a["expected_return"])}");
            if (s.AbsenceAlertsSent.Count > 500) s.AbsenceAlertsSent.RemoveRange(0, s.AbsenceAlertsSent.Count - 500);
            if (unsent.Count > 0) s.Save();
            foreach (var a in unsent)
            {
                var text = AbsenceText.PhoneAlert(a, Store.NowMs);
                foreach (var d in devices)
                {
                    try { App.Comms.Send(d, "ALERT", text, "Command Center"); } catch { /* terminal unreachable: Comms keeps it for the next connection */ }
                }
            }
            if (unsent.Count > 0) App.Store.AdminAudit("OVERDUE_ALERT_SENT", $"{unsent.Count} alert(s) to {devices.Count} terminal(s)");
        }
        catch { /* the alert on the PC has already been shown */ }
    }
}

/// <summary>Reminds once, up to 24 hours ahead, that someone on leave is due back soon — so their return is
/// expected in advance instead of only finding out after they're already overdue. Visitor passes aren't included:
/// an overstaying visitor is already overdue the moment their pass ends, so there is no "soon" to warn about.</summary>
public static class DueSoonMonitor
{
    static readonly HashSet<string> Alerted = [];
    static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMinutes(2) };
    const long LeadMs = 24 * 60 * 60 * 1000;

    public static void Start()
    {
        Timer.Tick += (_, _) => Check();
        Timer.Start();
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Check);
    }

    public static void Check()
    {
        List<Dictionary<string, object?>> dueSoon;
        try
        {
            var now = Store.NowMs;
            dueSoon = App.Store.Absences().Where(a => S(a["kind"]) == "RETURN" && a["overdue"] is not true
                && L(a["expected_return"]) - now > 0 && L(a["expected_return"]) - now <= LeadMs).ToList();
        }
        catch { return; }
        var fresh = dueSoon.Where(a => Alerted.Add($"{S(a["person_id"])}|{L(a["expected_return"])}")).ToList();
        if (fresh.Count == 0 || Application.Current.MainWindow is not Window main) return;
        foreach (var a in fresh) App.Store.AdminAudit("RETURN_DUE_SOON_ALERT", $"{S(a["person_id"])} {S(a["reason"])} due {Time(L(a["expected_return"]), "yyyy-MM-dd HH:mm")}");
        SystemSounds.Asterisk.Play();
        new AbsenceAlertWindow(fresh, overdue: false, main).Show();
    }
}

// ====================================================================== card lifecycle

public sealed class CardRegisterWindow : DarkWindow
{
    readonly StackPanel _list = new();
    readonly TextBox _search = new() { ToolTip = "Search ID, army no, name…" };

    public static void ShowWindow(Window owner) => new CardRegisterWindow { Owner = owner }.Show();

    internal CardRegisterWindow() : base("ID Card Register",
        "Every card's issue, re-issue, print and loss history. Personnel QR codes never expire — a code changes only when the card is reported lost or re-issued, and then the old card is refused at every gate.", 1060, 860)
    {
        Body.Children.Add(T("SEARCH  (ID, army no, name, company)", 10, "#64748B", bold: true).M(0, 0, 0, 4));
        Body.Children.Add(_search.M(0, 0, 0, 10));
        Body.Children.Add(_list);
        _search.TextChanged += (_, _) => Fill();
        AddButton("Close", Close);
        AddButton("Export company-wise…", Export, "BtnEmerald");
        Fill();
    }

    void Export()
    {
        if (!AdminGate.Require(this, "Export the ID Card Register")) return;
        var dlg = new SaveFileDialog { FileName = $"XV-ID-Card-Register-{DateTime.Now:yyyyMMdd-HHmm}.csv", Filter = "CSV (Excel)|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var companies = XV.Core.Reports.Companies;
        var rows = App.Store.CardRegister()
            .OrderBy(r => Array.IndexOf(companies, S(r["company"])) is var i && i >= 0 ? i : 99)
            .ThenBy(r => S(r["name"]))
            .Select(r => new Dictionary<string, object?>(r)
            {
                ["issued"] = L(r["issued_at"]) > 0 ? Time(L(r["issued_at"]), "yyyy-MM-dd") : "",
                ["reissued"] = L(r["reissued_at"]) > 0 ? Time(L(r["reissued_at"]), "yyyy-MM-dd") : "",
            });
        File.WriteAllText(dlg.FileName, Csv.Build(rows, ("Army Number", "service_no"), ("Rank", "rank"), ("Name", "name"), ("Company", "company"),
            ("Date of ID Card Issue", "issued"), ("Reissue", "reissued"), ("Remarks", "remarks")), new System.Text.UTF8Encoding(true));
        App.Store.AdminAudit("CARD_REGISTER_EXPORTED", Path.GetFileName(dlg.FileName));
        MessageBox.Show(this, "ID Card Register saved:\n" + dlg.FileName, "ID Card Register");
    }

    void Fill()
    {
        _list.Children.Clear();
        var q = _search.Text.Trim();
        var rows = App.Store.CardRegister().Where(r => q.Length == 0 || string.Join(" ", S(r["id"]), S(r["service_no"]), S(r["name"]), S(r["rank"]), S(r["company"])).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (rows.Count == 0) { _list.Children.Add(Empty("No soldiers in the register.")); return; }
        foreach (var r in rows)
        {
            var id = S(r["id"]);
            var issued = r["issued_at"] != null ? L(r["issued_at"]) : L(r["created_at"]);
            var info = Col(
                Row(T($"{S(r["rank"])} {S(r["name"])}".Trim(), 13.5, "#0F172A", bold: true),
                    Pill($"CARD #{Math.Max(1, L(r["issues"]))}", "#FCD34D", "#2A1F08", "#92400E", 9).M(10),
                    L(r["lost"]) > 0 ? Pill($"LOST ×{L(r["lost"])}", "#FDA4AF", "#2A0F14", "#9F1239", 9).M(6) : new TextBlock(),
                    S(r["status"]) != "ACTIVE" ? Pill(S(r["status"]), "#FDA4AF", "#2A0F14", "#9F1239", 9).M(6) : new TextBlock()),
                T(Parts(DisplayId(id), Labeled("Army No", S(r["service_no"])), $"{S(r["company"])} {S(r["platoon"])} {S(r["section"])}".Trim(), Labeled("Card ref", S(r["card_serial"]))), 11, "#64748B", mono: true).Wrap().M(0, 3),
                T($"Issued {Time(issued, "dd MMM yyyy")}" + (r["reissued_at"] != null ? $"   •   last re-issued {Time(L(r["reissued_at"]), "dd MMM yyyy HH:mm")}" : "") +
                  $"   •   last printed {(r["printed_at"] != null ? Time(L(r["printed_at"]), "dd MMM yyyy HH:mm") : "never")}   •   QR: no expiry", 11, "#475569", mono: true).Wrap().M(0, 3));
            var actions = Row(
                Btn("History", (_, _) => History(id)),
                Btn("Open in ID Card Studio", (_, _) => CardStudioWindow.Show(Owner, [id]), "BtnGold").M(6),
                Btn("Report lost…", (_, _) => ReportLost(id, $"{S(r["rank"])} {S(r["name"])}"), "BtnDanger").M(6));
            _list.Children.Add(Card(SpreadWrap(info, actions), pad: 12).M(0, 0, 0, 8));
        }
    }

    void History(string id)
    {
        var events = App.Store.CardHistory(id);
        var text = events.Count == 0 ? "No card events recorded yet (the card was issued before the card register existed)." :
            string.Join("\n", events.Select(e => $"{Time(L(e["created_at"]), "dd MMM yyyy HH:mm")}   {S(e["event"]),-9}  {S(e["detail"])}   ({S(e["actor"])})"));
        MessageBox.Show(this, text, "Card history " + DisplayId(id));
    }

    void ReportLost(string id, string who)
    {
        var d = new LostDialog(who) { Owner = this };
        if (d.ShowDialog() != true) return;
        if (!AdminGate.Require(this, $"Report card lost {DisplayId(id)}")) return;
        App.Store.ReportCardLost(id, d.Remarks);
        var told = App.Comms.RequestSyncAll("card reported lost");
        Fill();
        if (MessageBox.Show(this, $"The lost card of {who} no longer works. A new QR was issued ({told} connected terminal(s) were told to update now; the others update on their next sync).\n\nOpen the ID Card Studio to print the replacement card?",
                "Card reported lost", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            CardStudioWindow.Show(Owner, [id]);
    }

    sealed class LostDialog : DarkWindow
    {
        public string Remarks => _box.Text.Trim();
        readonly TextBox _box;
        public LostDialog(string who) : base("Report Card Lost", $"The card of {who} will stop working at every gate immediately and a replacement QR is issued.", 520, 360)
        {
            _box = Field("Remarks (where / when lost, FIR or report number…)");
            AddButton("Cancel", Close);
            AddButton("Report lost & re-issue", () => DialogResult = true, "BtnDanger");
        }
    }
}

// ====================================================================== encrypted backup & restore

public static class BackupUi
{
    sealed class PasswordDialog : DarkWindow
    {
        readonly PasswordBox _pw = new(), _confirm = new();
        public string Password => _pw.Password;
        public PasswordDialog(string title, string subtitle, bool confirm) : base(title, subtitle, 520, confirm ? 420 : 340)
        {
            Body.Children.Add(Label("Backup password")); Body.Children.Add(_pw);
            if (confirm) { Body.Children.Add(Label("Confirm password")); Body.Children.Add(_confirm); }
            Loaded += (_, _) => _pw.Focus();
            AddButton("Cancel", Close);
            var ok = AddButton(confirm ? "Create backup" : "Restore", () =>
            {
                if (confirm && _pw.Password.Length < Backup.MinPasswordLength) { MessageBox.Show($"Use at least {Backup.MinPasswordLength} characters."); return; }
                if (confirm && _pw.Password != _confirm.Password) { MessageBox.Show("The passwords do not match."); return; }
                if (_pw.Password.Length == 0) return;
                DialogResult = true;
            }, "BtnAmber");
            ok.IsDefault = true;
        }
    }

    public static async void Create(Window owner)
    {
        if (!AdminGate.Require(owner, "Create encrypted backup")) return;
        var pw = new PasswordDialog("Encrypted Backup", "Everything on this Command Center — registry, records, photos, accounts, terminals, Comms, settings and the server certificate — in one file encrypted with this password (AES-256-GCM). Without the password the file cannot be opened; keep it safe and separate from the backup.", true) { Owner = owner };
        if (pw.ShowDialog() != true) return;
        var dlg = new SaveFileDialog { FileName = $"XV-Backup-{App.Settings.ServerId}-{DateTime.Now:yyyyMMdd-HHmm}.xvbackup", Filter = "XV backup|*.xvbackup" };
        if (dlg.ShowDialog(owner) != true) return;
        try
        {
            owner.Cursor = Cursors.Wait;
            var path = dlg.FileName; var password = pw.Password;
            await Task.Run(() => Backup.Create(path, password, App.Settings, App.Store.ExportEncrypted, App.Comms.Store.ExportEncrypted));
            App.Store.AdminAudit("BACKUP_CREATED", Path.GetFileName(path));
            MessageBox.Show(owner, $"Backup saved ({new FileInfo(path).Length / 1024.0 / 1024.0:0.0} MB):\n{path}\n\nStore it on removable media away from this PC. To move to a new PC: install XV Command Center there, then Import / Export → Restore from backup.", "Encrypted backup");
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Backup failed"); }
        finally { owner.Cursor = null; }
    }

    public static void Restore(Window owner)
    {
        if (App.Settings.HasAdminPassword && !AdminGate.Require(owner, "Restore from backup")) return;
        var dlg = new OpenFileDialog { Filter = "XV backup|*.xvbackup" };
        if (dlg.ShowDialog(owner) != true) return;
        JsonObject head;
        try { head = Backup.Describe(dlg.FileName); } catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Restore"); return; }
        if (MessageBox.Show(owner, $"Backup of \"{head["serverName"]}\" ({head["serverId"]}) made {DateTimeOffset.Parse(head["created"]!.ToString()).LocalDateTime:dd MMM yyyy HH:mm}.\n\nRestoring REPLACES ALL DATA on this PC with the backup and restarts the Command Center. Continue?",
                "Restore from backup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var pw = new PasswordDialog("Restore Backup", "Enter the password the backup was created with.", false) { Owner = owner };
        if (pw.ShowDialog() != true) return;
        try { Backup.CheckPassword(dlg.FileName, pw.Password); }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Restore"); return; }
        App.RestoreAndRestart(dlg.FileName, pw.Password);
    }
}

// ====================================================================== auto-lock

/// <summary>Locks the Command Center after the configured idle time; the administrator password unlocks it. The gate server keeps running.</summary>
public static class AutoLock
{
    static DateTime _lastInput = DateTime.UtcNow;
    static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(15) };
    static Window? _lock;
    public static bool Locked => _lock != null;

    public static void Start()
    {
        InputManager.Current.PreProcessInput += (_, e) => { if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs && !Locked) _lastInput = DateTime.UtcNow; };
        Timer.Tick += (_, _) =>
        {
            var minutes = App.Settings.AutoLockMinutes;
            if (minutes > 0 && !Locked && App.Settings.HasAdminPassword && DateTime.UtcNow - _lastInput > TimeSpan.FromMinutes(minutes)) LockNow();
        };
        Timer.Start();
    }

    public static void LockNow()
    {
        if (Locked || Application.Current.MainWindow is not Window main) return;
        if (!App.Settings.HasAdminPassword) { MessageBox.Show("Set an administrator password first (Stations & Settings → Data protection).", "Lock"); return; }
        var hidden = Application.Current.Windows.OfType<Window>().Where(w => w != main && w.IsVisible).ToList();
        foreach (var w in hidden) w.Hide();
        var pw = new PasswordBox { Width = 280, FontSize = 16, Margin = new Thickness(0, 18, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
        var msg = T("", 12, "#FDA4AF").Center();
        var failures = 0; var until = DateTime.MinValue;
        var win = new Window
        {
            Owner = main, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true,
            Left = main.Left, Top = main.Top, Width = main.ActualWidth, Height = main.ActualHeight, Background = B("#F2090A0C"), AllowsTransparency = true,
        };
        if (main.WindowState == WindowState.Maximized) { win.WindowState = WindowState.Maximized; }
        void TryUnlock()
        {
            if (DateTime.UtcNow < until) { msg.Text = $"Too many attempts — wait {(int)(until - DateTime.UtcNow).TotalSeconds + 1} s"; return; }
            if (App.Settings.CheckAdminPassword(pw.Password))
            {
                App.Store.AdminAudit("UNLOCKED");
                _lock = null; _lastInput = DateTime.UtcNow; win.Close();
                foreach (var w in hidden) w.Show();
                return;
            }
            App.Store.AdminAudit("UNLOCK_FAILED");
            pw.Clear(); msg.Text = "Wrong password";
            if (++failures >= 5) { failures = 0; until = DateTime.UtcNow.AddMinutes(1); }
        }
        pw.KeyDown += (_, e) => { if (e.Key == Key.Enter) TryUnlock(); };
        var unlock = Btn("Unlock", (_, _) => TryUnlock(), "BtnAmber"); unlock.HorizontalAlignment = HorizontalAlignment.Center; unlock.Padding = new Thickness(28, 9, 28, 9);
        win.Content = new Grid
        {
            Children =
            {
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { T("🔒", 48, "#FBBF24").Center(), T("XV COMMAND CENTER LOCKED", 20, "#F4F4F5", bold: true).Center().M(0, 10),
                        T("The gate server keeps running. Enter the administrator password to unlock.", 12, "#A1A1AA").Center().M(0, 6), pw, msg, unlock },
                },
            },
        };
        win.Closing += (_, e) => { if (_lock != null) e.Cancel = true; };
        _lock = win;
        App.Store.AdminAudit("LOCKED", App.Settings.AutoLockMinutes > 0 ? $"after {App.Settings.AutoLockMinutes} min idle or manually" : "manually");
        win.Show(); pw.Focus();
    }
}
