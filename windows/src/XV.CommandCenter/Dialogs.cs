using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>Dark dialog shell matching the reference modals: title bar text, scrollable body, footer buttons.</summary>
public class DarkWindow : Window
{
    protected readonly StackPanel Body = new();
    protected readonly StackPanel Footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

    public DarkWindow(string title, string subtitle, double width = 620, double height = 720)
    {
        Title = title; Width = width; Height = Math.Min(height, SystemParameters.WorkArea.Height - 20); Background = B("#FFFFFF");
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResizeWithGrip;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        var dock = new DockPanel();
        var head = new Border { Padding = new Thickness(22, 16, 22, 14), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(0, 0, 0, 1), Child = Col(T(title, 16, "#0F172A", bold: true), new TextBlock { Text = subtitle, Foreground = B("#64748B"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }) };
        DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head);
        var foot = new Border { Padding = new Thickness(22, 12, 22, 12), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(0, 1, 0, 0), Child = Footer };
        DockPanel.SetDock(foot, Dock.Bottom); dock.Children.Add(foot);
        dock.Children.Add(new ScrollViewer { Content = Body, Padding = new Thickness(22, 16, 22, 16) });
        Content = dock;
    }

    protected Button AddButton(string text, Action onClick, string style = "BtnBase")
    {
        var b = new Button { Content = text, Style = (Style)Application.Current.Resources[style], Margin = new Thickness(8, 0, 0, 0), MinWidth = 90 };
        b.Click += (_, _) => onClick();
        Footer.Children.Add(b);
        return b;
    }

    protected static TextBlock Label(string text) => new() { Text = text.ToUpperInvariant(), Foreground = B("#64748B"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) };
    protected static TextBlock Para(string text, string color = "#64748B") => new() { Text = text, Foreground = B(color), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), LineHeight = 18 };

    protected TextBox Field(string label, string value = "", bool readOnly = false, bool mono = false)
    {
        Body.Children.Add(Label(label));
        var tb = new TextBox { Text = value, IsReadOnly = readOnly };
        if (mono) tb.FontFamily = Mono;
        Body.Children.Add(tb);
        return tb;
    }

    protected ComboBox Choice(string label, IEnumerable<string> options, string value, bool editable = false)
    {
        Body.Children.Add(Label(label));
        var cb = new ComboBox { IsEditable = editable, Text = value };
        foreach (var o in options) cb.Items.Add(o);
        cb.SelectedItem = options.FirstOrDefault(o => o == value);
        if (cb.SelectedItem == null) cb.Text = value;
        Body.Children.Add(cb);
        return cb;
    }

    protected static void Fail(Exception ex) => MessageBox.Show(ex.Message, "XV Command Center", MessageBoxButton.OK, MessageBoxImage.Warning);
}

public static class Dialogs
{
    static readonly string[] Companies = ["Alpha", "Bravo", "Charlie", "Delta", "SP", "HQ"];
    static readonly string[] Statuses = ["ACTIVE", "SUSPENDED", "EXPIRED", "FLAGGED"];

    // ------------------------------------------------------------------ personnel & vehicles

    sealed class PersonDialog : DarkWindow
    {
        public PersonDialog(string? id) : base(id == null ? "+ Add Soldier Details" : "Edit Personnel " + DisplayId(id),
            "Stored only in this PC's encrypted database. A unique secret QR code is generated automatically; terminals receive the record on their next sync.")
        {
            var p = id == null ? null : App.Store.Persons().FirstOrDefault(x => S(x["id"]) == id);
            string V(string k) => p == null ? "" : S(p[k]);
            var fId = Field("Personnel ID", id ?? App.Store.NextId("P", "persons"), readOnly: id != null, mono: true);
            var fName = Field("Full name", V("name"));
            var fRank = Choice("Rank", ["Sepoy", "Lance Naik", "Naik", "Havildar", "Naib Subedar", "Subedar", "Subedar Major", "Lieutenant", "Captain", "Major", "Lieutenant Colonel", "Colonel", "Brigadier", "Civilian"], V("rank"), editable: true);
            var fService = Field("Army / service number", V("service_no"), mono: true);
            var fCompany = Choice("Company", Companies, V("company"), editable: true);
            var fUnit = Field("Unit", V("unit"));
            var fRole = Field("Appointment / designation", V("role"));
            var fPlatoon = Field("Platoon", V("platoon"));
            var fSection = Field("Section", V("section"));
            var fCat = Choice("Category", ["PERSONNEL", "CIVILIAN", "CONTRACTOR", "VISITOR"], p == null ? "PERSONNEL" : V("category"));
            var fStatus = Choice("Credential status", Statuses, p == null ? "ACTIVE" : V("status"));
            var fCard = Field("I-Card number", V("id_card"), mono: true);
            var fMobile = Field("Mobile number", V("mobile"), mono: true);
            var fBlood = Choice("Blood group", ["", "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"], V("blood_group"));
            var fAccess = Field("Authorized locations (comma separated IDs, blank = all)", V("access_locations"), mono: true);
            var fAddress = Field("Permanent address", V("address"));
            var fDob = Field("Date of birth (DD-MM-YYYY)", V("dob"), mono: true);
            var fEnrol = Field("Date of enrolment (DD-MM-YYYY)", V("enrol_date"), mono: true);
            var fExpiry = Field("ID card expiry (DD-MM-YYYY)", V("expiry_date"), mono: true);
            var fMark = Field("Identification mark", V("id_mark"));
            var fNokName = Field("Next of kin — name", V("nok_name"));
            var fNokRel = Field("Next of kin — relation", V("nok_relation"));
            var fNokPhone = Field("Next of kin — phone", V("nok_phone"), mono: true);
            var fSerial = Field("ID card serial / reference no.", V("card_serial"), mono: true);
            var fNotes = Field("Notes", V("notes"));
            var fInitialLoc = Choice("Initial location (mark as inside when added)", ["", "MAIN_GATE", "SIDE_GATE", "OFFICE", "BARRACKS", "MESS", "HOSPITAL", "TRAINING_AREA"], V("initial_location"), editable: true);
            Body.Children.Add(Label("Initial status"));
            Body.Children.Add(fInitialLoc);
            Body.Children.Add(Label("Photo (uniform passport portrait)"));
            byte[]? photo = id == null ? null : App.Store.PersonPhoto(id);
            var photoChanged = false;
            var img = new Image { Width = 96, Height = 120, Stretch = System.Windows.Media.Stretch.UniformToFill, Source = Photo.Image(photo) };
            var photoBox = new Border { Width = 100, Height = 124, BorderBrush = B("#D97706"), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(6), Child = img, Background = B("#FFFFFF") };
            Body.Children.Add(Row(photoBox,
                Btn("Choose photo…", (_, _) =>
                {
                    var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
                    if (dlg.ShowDialog() != true) return;
                    try { photo = Photo.Prepare(File.ReadAllBytes(dlg.FileName)); img.Source = Photo.Image(photo); photoChanged = true; }
                    catch { MessageBox.Show("That file is not a readable image."); }
                }, "BtnGold").M(12),
                Btn("Remove", (_, _) => { photo = null; img.Source = null; photoChanged = true; }, "BtnDanger").M(8)));
            var existingCustom = new Dictionary<string, string>();
            try { existingCustom = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(p == null || V("custom_json").Length < 2 ? "{}" : V("custom_json")) ?? []; } catch { }
            var customBoxes = new Dictionary<string, TextBox>();
            if (App.Settings.CustomFields.Count > 0) Body.Children.Add(Label("— Custom fields (Stations & Settings) —"));
            foreach (var f in App.Settings.CustomFields) customBoxes[f] = Field(f, existingCustom.GetValueOrDefault(f, ""));
            AddButton("Cancel", Close);
            AddButton(id == null ? "Add to Registry" : "Save Changes", () =>
            {
                try
                {
                    App.Store.UpsertPerson(new JsonObject
                    {
                        ["id"] = fId.Text, ["name"] = fName.Text, ["rank"] = fRank.Text, ["serviceNo"] = fService.Text, ["company"] = fCompany.Text, ["unit"] = fUnit.Text,
                        ["role"] = fRole.Text, ["category"] = fCat.Text, ["status"] = fStatus.Text, ["idCard"] = fCard.Text, ["mobile"] = fMobile.Text,
                        ["bloodGroup"] = fBlood.Text, ["accessLocations"] = fAccess.Text, ["notes"] = fNotes.Text,
                        ["platoon"] = fPlatoon.Text, ["section"] = fSection.Text, ["address"] = fAddress.Text, ["dob"] = fDob.Text, ["enrolDate"] = fEnrol.Text,
                        ["expiryDate"] = fExpiry.Text, ["idMark"] = fMark.Text, ["nokName"] = fNokName.Text, ["nokRelation"] = fNokRel.Text,
                        ["nokPhone"] = fNokPhone.Text, ["cardSerial"] = fSerial.Text,
                        ["initialLocation"] = fInitialLoc.Text,
                        ["custom"] = new JsonObject(customBoxes.Select(kv => new KeyValuePair<string, System.Text.Json.Nodes.JsonNode?>(kv.Key, kv.Value.Text.Trim()))),
                    });
                    if (photoChanged) App.Store.SetPersonPhoto(Store.CanonId(fId.Text), photo);
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    sealed class VehicleDialog : DarkWindow
    {
        public VehicleDialog(string? id) : base(id == null ? "+ Register Vehicle" : "Edit Vehicle " + DisplayId(id), "A secret windshield QR code is generated automatically for every vehicle.", 560, 600)
        {
            var v = id == null ? null : App.Store.Vehicles().FirstOrDefault(x => S(x["id"]) == id);
            string V(string k) => v == null ? "" : S(v[k]);
            var fId = Field("Vehicle ID", id ?? App.Store.NextId("V", "vehicles"), readOnly: id != null, mono: true);
            var fPlate = Field("Registration plate", V("plate"), mono: true);
            var fMil = Field("Military registration number", V("mil_reg"), mono: true);
            var fType = Choice("Type", ["Tactical Supply Truck", "Light Utility Vehicle", "Armoured Carrier", "Staff Car", "Ambulance", "Water Tanker", "Fuel Bowser", "Motorcycle", "Civilian Vehicle"], V("type"), editable: true);
            var fModel = Field("Make / model", V("model"));
            var fCompany = Choice("Assigned company", Companies, V("company"), editable: true);
            var fStatus = Choice("Credential status", Statuses, v == null ? "ACTIVE" : V("status"));
            AddButton("Cancel", Close);
            AddButton(id == null ? "Register Vehicle" : "Save Changes", () =>
            {
                try
                {
                    App.Store.UpsertVehicle(new JsonObject { ["id"] = fId.Text, ["plate"] = fPlate.Text, ["milReg"] = fMil.Text, ["type"] = fType.Text, ["model"] = fModel.Text, ["company"] = fCompany.Text, ["status"] = fStatus.Text });
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void EditPerson(Window owner, string? id) => new PersonDialog(id) { Owner = owner }.ShowDialog();
    public static void EditVehicle(Window owner, string? id) => new VehicleDialog(id) { Owner = owner }.ShowDialog();

    public static void SetPersonStatus(Window owner, string id, string status)
    {
        var p = App.Store.Persons().First(x => S(x["id"]) == id);
        var o = new JsonObject { ["id"] = id, ["name"] = S(p["name"]), ["rank"] = S(p["rank"]), ["serviceNo"] = S(p["service_no"]), ["unit"] = S(p["unit"]), ["company"] = S(p["company"]), ["role"] = S(p["role"]), ["category"] = S(p["category"]), ["status"] = status, ["mobile"] = S(p["mobile"]), ["idCard"] = S(p["id_card"]), ["bloodGroup"] = S(p["blood_group"]), ["accessLocations"] = S(p["access_locations"]), ["notes"] = S(p["notes"]) };
        App.Store.UpsertPerson(o); // custom fields are left unchanged because "custom" is not sent
    }

    public static void SetVehicleStatus(Window owner, string id, string status)
    {
        var v = App.Store.Vehicles().First(x => S(x["id"]) == id);
        App.Store.UpsertVehicle(new JsonObject { ["id"] = id, ["plate"] = S(v["plate"]), ["milReg"] = S(v["mil_reg"]), ["type"] = S(v["type"]), ["model"] = S(v["model"]), ["company"] = S(v["company"]), ["status"] = status });
    }

    static bool Confirm(string text) => MessageBox.Show(text, "XV Command Center", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public static void DeletePerson(Window o, string id, string name) { if (Confirm($"Delete {name} ({DisplayId(id)}) from the registry?\nTheir printed QR card will stop working. Movement history is kept.")) App.Store.DeletePerson(id); }
    public static void DeleteVehicle(Window o, string id, string plate) { if (Confirm($"Delete vehicle {plate} ({DisplayId(id)})?")) App.Store.DeleteVehicle(id); }
    public static void RevokeDevice(Window o, string id) { if (Confirm($"Revoke terminal {id}?\nIt is disconnected immediately and, the next time it contacts this PC, it erases all its data (registry, gate records not yet uploaded, messages) and must be paired again.")) { App.Store.RevokeDevice(id); App.Comms.Disconnect(id); } }
    public static void DeleteAccount(Window o, string id) { if (Confirm($"Delete operator account {id}?")) App.Store.DeleteAccount(id); }

    public static void ResetPassword(Window o, string id)
    {
        if (!Confirm($"Generate a new password for {id}? The operator is signed out on all terminals.")) return;
        ShowSecret(o, "New password for " + id, App.Store.ResetPassword(id));
    }

    static void ShowSecret(Window owner, string title, string secret)
    {
        var w = new SecretWindow(title, secret) { Owner = owner };
        w.ShowDialog();
    }

    sealed class SecretWindow : DarkWindow
    {
        public SecretWindow(string title, string secret) : base(title, "Give this to the operator now. It is not shown again.", 460, 280)
        {
            var tb = Field("Password", secret, readOnly: true, mono: true); tb.FontSize = 20;
            AddButton("Copy", () => Clipboard.SetText(secret));
            AddButton("Done", Close, "BtnAmber");
        }
    }

    sealed class AccountDialog : DarkWindow
    {
        public AccountDialog() : base("+ Create Operator Account", "Operators sign in on the Android terminal with this RP ID and password.", 480, 470)
        {
            var fName = Field("Rank and name");
            var fId = Field("RP ID (blank = assign automatically, e.g. GK-101)", "", mono: true);
            var fRole = Choice("Role", ["RP", "Gate Supervisor", "Duty Officer"], "RP", editable: true);
            var fPw = Field("Password (blank = generate a secure one)", "", mono: true);
            AddButton("Cancel", Close);
            AddButton("Create Account", () =>
            {
                try
                {
                    var (id, gen) = App.Store.CreateAccount(fName.Text, fId.Text, fPw.Text, "ADMIN", fRole.Text);
                    Close();
                    ShowSecret(Owner, $"Account {id} created", gen ?? fPw.Text);
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void CreateAccount(Window owner) => new AccountDialog { Owner = owner }.ShowDialog();

    // ------------------------------------------------------------------ credentials (ID card + QR)

    sealed class CredentialWindow : DarkWindow
    {
        public CredentialWindow(string type, string id) : base(type == "PERSON" ? "Official ID Card & QR" : "Vehicle Windshield QR",
            "The QR holds a random secret code (not the ID), so a card cannot be forged by typing an ID. Use 'Re-issue QR' if a card is lost.", 520, 760)
        {
            var person = type == "PERSON";
            var r = person ? App.Store.Persons().First(x => S(x["id"]) == id) : App.Store.Vehicles().First(x => S(x["id"]) == id);
            var qr = new Image { Source = Png(Pairing.QrPng(S(r["secret_code"]), 8)), Width = 220, Height = 220, Margin = new Thickness(0, 12, 0, 8) };
            RenderOptions.SetBitmapScalingMode(qr, BitmapScalingMode.NearestNeighbor);
            var card = new Border
            {
                Width = 380, Background = B("#FFFFFF"), CornerRadius = new CornerRadius(14), BorderBrush = B("#334155"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center,
                Child = Col(
                    new Border { Background = B("#0F172A"), CornerRadius = new CornerRadius(13, 13, 0, 0), Padding = new Thickness(16, 12, 16, 12), Child = Spread(Col(T("XV DIGITAL ACCESS CONTROL", 12, "#FFFFFF", bold: true), T(person ? "MILITARY IDENTITY CREDENTIAL" : "VEHICLE ACCESS CREDENTIAL", 9.5, "#34D399", bold: true, mono: true)), T(DisplayId(id), 14, "#FBBF24", bold: true, mono: true)) },
                    new Border { Padding = new Thickness(18), Child = Col(
                        T(person ? S(r["name"]) : S(r["plate"]), 18, "#0F172A", bold: true).Center(),
                        T(person ? string.Join(" • ", new[] { S(r["rank"]), S(r["company"]) is { Length: > 0 } c ? c + " Co" : "", S(r["unit"]) }.Where(x => x.Length > 0)) : string.Join(" • ", new[] { S(r["type"]), S(r["model"]), S(r["company"]) }.Where(x => x.Length > 0)), 11.5, "#64748B").Center().M(0, 4),
                        qr,
                        T(person ? $"Service No: {S(r["service_no"])}   I-Card: {S(r["id_card"])}" : $"Mil Reg: {S(r["mil_reg"])}", 10.5, "#CBD5E1", mono: true).Center(),
                        T("Present this code at any XV gate terminal", 9.5, "#64748B").Center().M(0, 8)) }),
            };
            Body.Children.Add(card);
            AddButton("Re-issue QR", () =>
            {
                var d = new ReissueDialog(person ? $"{S(r["rank"])} {S(r["name"])}".Trim() : S(r["plate"])) { Owner = this };
                if (d.ShowDialog() != true) return;
                App.Store.RotateSecret(person ? "persons" : "vehicles", id, d.Remarks.Length > 0 ? d.Remarks : "Re-issued");
                App.Comms.RequestSyncAll("QR re-issued");
                Close(); Credential(Owner, type, id);
            }, "BtnDanger");
            AddButton("Save PNG", () =>
            {
                if (!AdminGate.Require(this, $"Save credential image {DisplayId(id)}")) return;
                var dlg = new SaveFileDialog { FileName = $"{DisplayId(id)}-credential.png", Filter = "PNG image|*.png" };
                if (dlg.ShowDialog() != true) return;
                card.UpdateLayout();
                var rtb = new RenderTargetBitmap((int)(card.ActualWidth * 2), (int)(card.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
                rtb.Render(card);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(dlg.FileName)) enc.Save(fs);
                if (person) App.Store.CardEvent(id, "EXPORTED", "Credential PNG", "PC-ADMIN");
            });
            AddButton("Print", () => { if (!AdminGate.Require(this, $"Print credential {DisplayId(id)}")) return; var pd = new PrintDialog(); if (pd.ShowDialog() == true) { pd.PrintVisual(card, "XV credential " + id); if (person) App.Store.CardEvent(id, "PRINTED", "Credential window", "PC-ADMIN"); } }, "BtnAmber");
        }
    }

    sealed class ReissueDialog : DarkWindow
    {
        public string Remarks => _box.Text.Trim();
        readonly TextBox _box;
        public ReissueDialog(string who) : base("Re-issue QR", $"A new secret code is issued for {who}. The old printed card stops working at every gate once terminals sync. Recorded in the ID Card Register.", 520, 340)
        {
            _box = Field("Remarks (reason for re-issue — optional)");
            AddButton("Cancel", Close);
            AddButton("Re-issue", () => DialogResult = true, "BtnDanger");
        }
    }

    public static void Credential(Window owner, string type, string id) => new CredentialWindow(type, id) { Owner = owner }.ShowDialog();

    public static BitmapImage Png(byte[] bytes)
    {
        var bi = new BitmapImage();
        bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(bytes); bi.EndInit(); bi.Freeze();
        return bi;
    }

    // ------------------------------------------------------------------ history

    sealed class HistoryWindow : DarkWindow
    {
        public HistoryWindow(string type, string id) : base("Movement History • " + DisplayId(id), "Every entry and exit recorded for this credential, newest first.", 900, 800)
        {
            var rows = App.Store.EventsForEntity(type, id);
            if (rows.Count == 0 && !(type == "VEHICLE" && App.Store.Transits("", id).Count > 0)) { Body.Children.Add(Para("No gate activity has been recorded yet.")); goto buttons; }

            var topIndex = 0;
            if (type == "VEHICLE" && App.Store.Vehicles().FirstOrDefault(x => S(x["id"]) == id) is { } vehicleRow)
            {
                Body.Children.Add(VehicleDetailsCard(TransitText.VehicleDetails(vehicleRow)));
                topIndex = 1;
            }
            var manualTrips = type == "VEHICLE" ? App.Store.Transits("", id).Where(t => S(t["exit_event_id"]).Length == 0).ToList() : [];
            var routePanel = BuildRouteChart(rows, manualTrips);
            if (routePanel != null) Body.Children.Add(routePanel);

            Body.Children.Add(Label("DETAILED RECORDS"));
            long? total = 0;
            foreach (var r in rows)
            {
                var entry = S(r["event_type"]) == "ENTRY";
                total += L(r["stay_ms"]);
                Body.Children.Add(Card(Spread(
                    Row(Pill(S(r["event_type"]), entry ? "#059669" : "#B45309", entry ? "#ECFDF5" : "#FFFBEB", entry ? "#047857" : "#92400E"),
                        T("  " + Time(L(r["event_ts"]), "ddd dd MMM yyyy  HH:mm:ss"), 12, "#0F172A", mono: true),
                        T($"   {S(r["location_name"])} • {S(r["gate_name"])}", 11.5, "#64748B")),
                    Row(L(r["loc_mismatch"]) == 1 ? T("⚠ LOC FLAG  ", 10.5, "#B45309", bold: true, mono: true) : new TextBlock(),
                        T(Note(r, "  •  ") + (L(r["stay_ms"]) > 0 ? "Stayed " + Duration(L(r["stay_ms"])) : "Op " + S(r["operator_id"])), 11, "#334155", mono: true))), "#FFFFFF", pad: 10).M(0, 0, 0, 6));
                if (TransitText.ServerStamp(r) is { Length: > 0 } stamp) Body.Children.Add(T("✓ " + stamp, 10, "#047857", bold: true, mono: true).Wrap().M(10, 0, 0, 6));
                if (TransitText.Line(r, "tr_") is { Length: > 0 } tripLine)
                {
                    var overdue = TransitText.IsOverdue(r, "tr_");
                    var closed = S(r["tr_state"]) != Store.TransitEnRoute;
                    Body.Children.Add(Card(T("🚚  " + tripLine, 11.5, overdue ? "#9F1239" : closed ? "#065F46" : "#92400E", bold: true).Wrap(), overdue ? "#FFF1F2" : closed ? "#ECFDF5" : "#FFFBEB", overdue ? "#FDA4AF" : closed ? "#A7F3D0" : "#FDE68A", 8).M(24, -2, 0, 8));
                }
            }
            if (rows.Count > 0) Body.Children.Insert(topIndex, Para($"{rows.Count} records • total recorded time on site: {Duration(total ?? 0)}", "#B45309"));

            buttons:
            if (type == "PERSON")
            {
                AddButton("+ Add Record", () => { Close(); AddRecord(Owner, id); History(Owner, type, id); }, "BtnBlue");
                AddButton("Export Route…", () => ExportRoute(this, type, id, rows), "BtnEmerald");
                AddButton("Export…", () => Report(this, "ALL", [id]), "BtnGold");
            }
            else if (type == "VEHICLE")
            {
                AddButton("Export Route…", () => ExportRoute(this, type, id, rows), "BtnEmerald");
            }
            AddButton("Close", Close, "BtnAmber");
        }

        static Border VehicleDetailsCard(IReadOnlyList<(string Label, string Value)> details)
        {
            var wrap = new WrapPanel();
            foreach (var (label, value) in details)
                wrap.Children.Add(Col(T(label.ToUpperInvariant(), 9.5, "#64748B", bold: true), T(value, 12.5, "#0F172A", bold: true, mono: true)).M(0, 0, 24, 8));
            return Card(Col(T("VEHICLE DETAILS", 11, "#4F46E5", bold: true).M(0, 0, 0, 8), wrap), "#EEF2FF", "#C7D2FE", 12).M(0, 0, 0, 10);
        }

        static Border? BuildRouteChart(List<Dictionary<string, object?>> newestFirst, List<Dictionary<string, object?>> manualTrips)
        {
            var steps = RouteModel.Build(newestFirst, manualTrips);
            if (steps.Count < 1) return null;
            var timeline = Col();
            foreach (var st in steps)
            {
                var (bg, fg) = XV.Core.Reports.RouteColors(st);
                var travel = st.Kind is "TRAVEL" or "AWAY";
                var head = Row(Pill(st.Title, fg, bg, bg, 10), T("   " + Time(st.Ts, "ddd dd MMM  HH:mm"), 11, "#334155", mono: true));
                if (st.Status.Length > 0) head.Children.Add(T("   " + st.Status, 10.5, fg, bold: true, mono: true));
                var block = Col(head);
                if (st.Place.Length > 0) block.Children.Add(T(travel ? "→ " + st.Place : st.Place, 12, "#0F172A", bold: true).M(0, 4, 0, 0));
                if (st.Detail.Length > 0) block.Children.Add(T(st.Detail, 10.5, "#64748B").Wrap().M(0, 2, 0, 0));
                if (st.Crew.Length > 0) block.Children.Add(T(st.Crew, 10.5, "#3730A3", bold: true).Wrap().M(0, 3, 0, 0));
                timeline.Children.Add(new Border
                {
                    BorderBrush = B(fg), BorderThickness = new Thickness(travel ? 1 : 0, 0, 0, 0), Padding = new Thickness(travel ? 24 : 12, 7, 12, 7),
                    Background = B(travel ? "#FAFAFA" : bg), Margin = new Thickness(travel ? 14 : 0, 0, 0, 5), CornerRadius = new CornerRadius(6), Child = block,
                });
            }
            return new Border { Child = Col(T("ROUTE CHART", 11, "#64748B", bold: true).M(0, 4, 0, 8), Card(timeline, "#FFFFFF", pad: 12)), Padding = new Thickness(0, 0, 0, 16) };
        }
    }

    public static void History(Window owner, string type, string id) => new HistoryWindow(type, id) { Owner = owner }.ShowDialog();

    sealed class ExportRouteDialog : DarkWindow
    {
        public ExportRouteDialog(string type, string id, List<Dictionary<string, object?>> events) : base("Export Route • " + DisplayId(id),
            "Download route chart as PDF (print-friendly, single page) or Excel (professional, color-coded).", 500, 280)
        {
            var entity = type == "PERSON"
                ? App.Store.Persons().FirstOrDefault(x => S(x["id"]) == id)
                : App.Store.Vehicles().FirstOrDefault(x => S(x["id"]) == id);

            if (entity == null) { Body.Children.Add(Para("Entity not found.")); AddButton("Close", Close); return; }

            var title = type == "PERSON"
                ? $"{S(entity["name"])} • {S(entity["rank"])} • {S(entity["company"])}"
                : $"{S(entity["plate"])} • {S(entity["type"])}";

            Body.Children.Add(Para($"Route Chart: {events.Count} movements", "#B45309"));
            Body.Children.Add(Para($"Title: {title}", "#64748B"));

            var format = Choice("Format", ["Excel (Professional, color-coded)", "PDF (Print-friendly, single page)"], "Excel (Professional, color-coded)");

            AddButton("Cancel", Close);
            AddButton("Export", () =>
            {
                try
                {
                    if (!AdminGate.Require(this, "Export route chart")) return;
                    var manual = type == "VEHICLE" ? App.Store.Transits("", id).Where(t => S(t["exit_event_id"]).Length == 0).ToList() : new List<Dictionary<string, object?>>();
                    var details = type == "VEHICLE" ? TransitText.VehicleDetails(entity) : null;
                    var fmt = format.SelectedIndex == 0 ? "xlsx" : "pdf";
                    var ext = fmt == "xlsx" ? ".xlsx" : ".pdf";
                    var name = $"XV-Route-{DisplayId(id)}-{DateTime.Now:yyyyMMdd-HHmm}{ext}";
                    var dlg = new SaveFileDialog
                    {
                        FileName = name,
                        Filter = fmt == "xlsx" ? "Excel workbook|*.xlsx" : "PDF document|*.pdf"
                    };
                    if (dlg.ShowDialog() != true) return;

                    if (fmt == "xlsx")
                    {
                        XV.Core.Reports.RouteChartExcel(events, title, "Movement history", dlg.FileName, manual, details);
                    }
                    else
                    {
                        XV.Core.Reports.RouteChartPdf(events, title, "Movement history", dlg.FileName, manual, details);
                    }

                    if (MessageBox.Show("Route exported. Open it now?", "Export finished", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                    Close();
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnEmerald");
        }
    }

    static void ExportRoute(Window owner, string type, string id, List<Dictionary<string, object?>> events) =>
        new ExportRouteDialog(type, id, events) { Owner = owner }.ShowDialog();

    sealed class AddRecordDialog : DarkWindow
    {
        public AddRecordDialog(string personId) : base("+ Add Record to History • " + DisplayId(personId),
            "ENTRY / EXIT update who is inside, exactly like a gate scan. Other types (Leave, Duty, Course…) are dated history entries. Types are configured in Stations & Settings.", 560, 640)
        {
            var person = App.Store.Persons().First(x => S(x["id"]) == personId);
            Body.Children.Add(Para($"{S(person["name"])} • {S(person["rank"])} • {S(person["company"])} Co • currently {(L(person["inside_since"]) > 0 ? "INSIDE" : "OUTSIDE")}", "#B45309"));
            var type = Choice("Record type", new[] { "ENTRY", "EXIT" }.Concat(App.Settings.EventTypes), L(person["inside_since"]) > 0 ? "EXIT" : "ENTRY");
            Body.Children.Add(Label("Date"));
            var date = new DatePicker { SelectedDate = DateTime.Today, DisplayDateEnd = DateTime.Today };
            Body.Children.Add(date);
            var time = Field("Time (HH:mm)", DateTime.Now.ToString("HH:mm"), mono: true);
            var locs = App.Store.Locations().Select(l => $"{S(l["id"])} — {S(l["name"])}").ToList();
            var gates = App.Store.Gates().Select(g => $"{S(g["id"])} — {S(g["name"])}").ToList();
            var loc = Choice("Location", locs, locs.FirstOrDefault() ?? "");
            var gate = Choice("Gate", gates, gates.FirstOrDefault() ?? "");
            var reasonLabel = Label("Reason (ENTRY / EXIT)");
            var reason = new ComboBox { IsEditable = true };
            reason.Items.Add("");
            foreach (var r in App.Settings.MovementReasons) reason.Items.Add(r);
            Body.Children.Add(reasonLabel);
            Body.Children.Add(reason);
            var remarks = Field("Remarks (authority, pass number…)");
            var returnLabel = Label("Expected return date (optional)");
            var returnDate = new DatePicker { SelectedDate = null, DisplayDateStart = DateTime.Today };
            Body.Children.Add(returnLabel);
            Body.Children.Add(returnDate);
            void SyncReturnVisibility()
            {
                var show = type.Text == "EXIT";
                reasonLabel.Visibility = reason.Visibility = type.Text is "ENTRY" or "EXIT" ? Visibility.Visible : Visibility.Collapsed;
                returnLabel.Visibility = returnDate.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (!show) returnDate.SelectedDate = null;
            }
            type.SelectionChanged += (_, _) => SyncReturnVisibility();
            SyncReturnVisibility();
            AddButton("Cancel", Close);
            AddButton("Save Record", () =>
            {
                try
                {
                    if (!TimeSpan.TryParse(time.Text.Trim(), out var tod)) throw new Exception("Enter the time as HH:mm, e.g. 14:30");
                    var when = (date.SelectedDate ?? DateTime.Today).Date + tod;
                    var expectedReturn = type.Text == "EXIT" && returnDate.SelectedDate is { } rd ? new DateTimeOffset(rd.Date.AddHours(23).AddMinutes(59)).ToUnixTimeMilliseconds() : 0L;
                    App.Store.AddManualRecord(personId, type.Text, new DateTimeOffset(when).ToUnixTimeMilliseconds(),
                        loc.Text.Split(' ')[0], gate.Text.Split(' ')[0], remarks.Text, expectedReturn: expectedReturn, reason: type.Text is "ENTRY" or "EXIT" ? reason.Text : "");
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void AddRecord(Window owner, string personId) => new AddRecordDialog(personId) { Owner = owner }.ShowDialog();

    sealed class TransitReportDialog : DarkWindow
    {
        public TransitReportDialog() : base("Vehicle transit report",
            "Every vehicle trip in the period: where it went, the approximate and actual time, whether it reached, stopped or moved elsewhere, and who recorded it. Excel and PDF also include the average time between locations.", 600, 560)
        {
            Body.Children.Add(Label("From"));
            var from = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30) };
            Body.Children.Add(from);
            Body.Children.Add(Label("To"));
            var to = new DatePicker { SelectedDate = DateTime.Today };
            Body.Children.Add(to);
            void Export(string kind)
            {
                try
                {
                    if (!AdminGate.Require(this, $"Export vehicle transit report ({kind.ToUpperInvariant()})")) return;
                    var f = from.SelectedDate ?? DateTime.Today.AddDays(-30); var t = to.SelectedDate ?? DateTime.Today;
                    if (f > t) throw new Exception("'From' must be before 'To'.");
                    var (filter, ext) = kind switch { "xlsx" => ("Excel workbook|*.xlsx", ".xlsx"), "pdf" => ("PDF document|*.pdf", ".pdf"), _ => ("CSV|*.csv", ".csv") };
                    var dlg = new SaveFileDialog { FileName = $"XV-Vehicle-Transit-{f:yyyyMMdd}-{t:yyyyMMdd}{ext}", Filter = filter };
                    if (dlg.ShowDialog() != true) return;
                    if (kind == "xlsx") XV.Core.Reports.TransitExcel(App.Store, f, t, dlg.FileName);
                    else if (kind == "pdf") XV.Core.Reports.TransitPdf(App.Store, f, t, dlg.FileName);
                    else XV.Core.Reports.TransitCsv(App.Store, f, t, dlg.FileName);
                    if (MessageBox.Show("Report saved. Open it now?", "Export finished", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
                catch (Exception ex) { Fail(ex); }
            }
            AddButton("Close", Close);
            AddButton("CSV", () => Export("csv"));
            AddButton("PDF", () => Export("pdf"), "BtnDanger");
            AddButton("Excel", () => Export("xlsx"), "BtnEmerald");
        }
    }

    public static void TransitReport(Window owner) => new TransitReportDialog { Owner = owner }.ShowDialog();

    sealed class ReportDialog : DarkWindow
    {
        public ReportDialog(string company, List<string> personIds) : base("Reports & Export",
            "Excel: summary sheet plus one colour-coded sheet per company (roster + records). PDF: printable A4 report. CSV: plain records for any software.", 600, 620)
        {
            var scope = personIds.Count > 0 ? $"{personIds.Count} selected: " + string.Join(", ", personIds.Take(8).Select(DisplayId)) + (personIds.Count > 8 ? "…" : "") : "";
            if (scope.Length > 0) Body.Children.Add(Para(scope, "#B45309"));
            var comp = Choice("Company", new[] { "All Companies" }.Concat(Companies.Select(c => c + " Company")), company == "ALL" || personIds.Count > 0 ? "All Companies" : company + " Company");
            comp.IsEnabled = personIds.Count == 0;
            Body.Children.Add(Label("From"));
            var from = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30) };
            Body.Children.Add(from);
            Body.Children.Add(Label("To"));
            var to = new DatePicker { SelectedDate = DateTime.Today };
            Body.Children.Add(to);
            var withRecords = new CheckBox { Content = "Include gate & history records (untick for roster only)", IsChecked = true, Margin = new Thickness(0, 12, 0, 0) };
            Body.Children.Add(withRecords);

            void Export(string kind)
            {
                try
                {
                    if (!AdminGate.Require(this, $"Export {kind.ToUpperInvariant()} report")) return;
                    var c = comp.SelectedIndex <= 0 ? "ALL" : Companies[comp.SelectedIndex - 1];
                    var req = new XV.Core.ReportRequest(c, personIds, from.SelectedDate ?? DateTime.Today.AddDays(-30), to.SelectedDate ?? DateTime.Today, withRecords.IsChecked == true);
                    if (req.From > req.To) throw new Exception("'From' must be before 'To'.");
                    var name = $"XV-{(personIds.Count == 1 ? DisplayId(personIds[0]) : personIds.Count > 1 ? "Selected" : c == "ALL" ? "AllCompanies" : c)}-{req.From:yyyyMMdd}-{req.To:yyyyMMdd}";
                    var (filter, ext) = kind switch { "xlsx" => ("Excel workbook|*.xlsx", ".xlsx"), "pdf" => ("PDF document|*.pdf", ".pdf"), _ => ("CSV|*.csv", ".csv") };
                    var dlg = new SaveFileDialog { FileName = name + ext, Filter = filter };
                    if (dlg.ShowDialog() != true) return;
                    if (kind == "xlsx") XV.Core.Reports.Excel(App.Store, req, dlg.FileName);
                    else if (kind == "pdf") XV.Core.Reports.Pdf(App.Store, req, dlg.FileName);
                    else XV.Core.Reports.Csv(App.Store, req, dlg.FileName);
                    if (MessageBox.Show("Report saved. Open it now?", "Export finished", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
                catch (Exception ex) { Fail(ex); }
            }
            AddButton("Close", Close);
            AddButton("CSV", () => Export("csv"));
            AddButton("PDF", () => Export("pdf"), "BtnDanger");
            AddButton("Excel", () => Export("xlsx"), "BtnEmerald");
        }
    }

    public static void Report(Window owner, string company, List<string> personIds) => new ReportDialog(company, personIds) { Owner = owner }.ShowDialog();

    // ------------------------------------------------------------------ import / export

    static void SaveCsv(string name, string csv, bool containsData = true)
    {
        if (containsData && !AdminGate.Require(null, "Export " + name)) return;
        var dlg = new SaveFileDialog { FileName = name, Filter = "CSV (Excel)|*.csv" };
        if (dlg.ShowDialog() == true) File.WriteAllText(dlg.FileName, csv, new System.Text.UTF8Encoding(true));
    }

    public static void ExportEvents(Window o) => SaveCsv($"xv-gate-records-{DateTime.Now:yyyyMMdd-HHmm}.csv", Csv.Build(
        App.Store.RecentEvents(1_000_000).Select(r => { r["time"] = Time(L(r["event_ts"]), "yyyy-MM-dd HH:mm:ss"); r["recorded_by_server"] = L(r["received_at"]) > 0 ? Time(L(r["received_at"]), "yyyy-MM-dd HH:mm:ss") : ""; r["stay"] = L(r["stay_ms"]) > 0 ? Duration(L(r["stay_ms"])) : "";
            r["occupants"] = S(r["occupants"]).Length > 2 ? string.Join(", ", (System.Text.Json.JsonSerializer.Deserialize<List<string>>(S(r["occupants"])) ?? []).Select(DisplayId)) : "";
            r["loc_mismatch"] = L(r["loc_mismatch"]) == 1 ? "YES" : "";
            var st = S(r["tr_state"]); r["trip_status"] = st.Length == 0 ? "" : TransitText.StateLabel(st, TransitText.IsOverdue(r, "tr_"));
            r["trip_approx"] = L(r["tr_expected_min"]) > 0 ? L(r["tr_expected_min"]).ToString() : ""; r["trip_taken"] = L(r["tr_actual_min"]) > 0 ? L(r["tr_actual_min"]).ToString() : "";
            r["trip_how"] = TransitText.Via(S(r["tr_resolved_via"]), S(r["tr_resolved_by"])); return r; }),
        ("Time", "time"), ("Action", "event_type"), ("Type", "entity_type"), ("ID", "entity_id"), ("Name / Plate", "title"), ("Location", "location_name"), ("Gate", "gate_name"),
        ("Operator", "operator_id"), ("Terminal", "device_id"), ("Stay", "stay"), ("Location flag", "loc_mismatch"), ("QR location", "scanned_loc"), ("Occupants", "occupants"), ("Driver", "driver_label"), ("Co-driver", "co_driver_label"),
        ("Destination", "tr_dest_name"), ("Approx time (min)", "trip_approx"), ("Trip status", "trip_status"), ("Ended at (place)", "tr_end_name"), ("Time taken (min)", "trip_taken"), ("Trip recorded", "trip_how"),
        ("Event ID", "event_id"), ("Server seq", "seq"), ("Recorded by server at", "recorded_by_server")));

    public static void ExportAudit(Window o) => SaveCsv($"xv-audit-{DateTime.Now:yyyyMMdd-HHmm}.csv", Csv.Build(
        App.Store.AuditLog(1_000_000).Select(r => { r["time"] = Time(L(r["created_at"]), "yyyy-MM-dd HH:mm:ss"); return r; }),
        ("Time", "time"), ("Actor", "actor"), ("Action", "action"), ("Entity type", "entity_type"), ("Entity", "entity_id"), ("Detail", "detail")));

    static readonly (string, string)[] PersonCols = [("id", "id"), ("name", "name"), ("rank", "rank"), ("service_no", "service_no"), ("unit", "unit"), ("company", "company"), ("role", "role"), ("category", "category"), ("status", "status"), ("mobile", "mobile"), ("id_card", "id_card"), ("blood_group", "blood_group"), ("access_locations", "access_locations")];
    static readonly (string, string)[] VehicleCols = [("id", "id"), ("plate", "plate"), ("mil_reg", "mil_reg"), ("type", "type"), ("model", "model"), ("company", "company"), ("status", "status")];

    sealed class ImportExportWindow : DarkWindow
    {
        public ImportExportWindow() : base("Import / Export", "Move registry data in and out as CSV files (open directly in Excel). Imports add new records and update existing IDs.", 700, 720)
        {
            Section("SOLDIER REGISTER (EXCEL • CSV)",
                ("Import Excel / CSV", () => SoldierRegister.Import(this), "BtnEmerald"), ("Export…", () => SoldierRegister.Export(this), "BtnBase"),
                ("Blank template", () => SoldierRegister.Template(this), "BtnBase"), ("Import photos from folder", () => SoldierRegister.ImportPhotos(this), "BtnBase"));
            Section("VEHICLE FLEET",
                ("Import CSV", () => Import(false), "BtnEmerald"), ("Export CSV", () => SaveCsv("xv-vehicles.csv", Csv.Build(App.Store.Vehicles(), VehicleCols)), "BtnBase"),
                ("Blank template", () => SaveCsv("xv-vehicles-template.csv", Csv.Build([], VehicleCols), containsData: false), "BtnBase"),
                ("Export JSON", () => ExportVehiclesBackup(this), "BtnBase"), ("Import JSON", () => ImportVehiclesBackup(this), "BtnEmerald"));
            Section("PERSONNEL JSON BACKUP",
                ("Export personnel", () => ExportPersonnelBackup(this), "BtnBase"), ("Import personnel", () => ImportPersonnelBackup(this), "BtnEmerald"));
            Section("ACCOUNTS & DEVICES",
                ("Export accounts", () => ExportAccountsBackup(this), "BtnBase"), ("Import accounts", () => ImportAccountsBackup(this), "BtnEmerald"),
                ("Export devices", () => ExportDevicesBackup(this), "BtnBase"), ("Import devices", () => ImportDevicesBackup(this), "BtnEmerald"));
            Section("SERVER SETTINGS & CONFIG",
                ("Export settings backup", () => ExportSettingsBackup(this), "BtnBase"), ("Import settings backup", () => ImportSettingsBackup(this), "BtnEmerald"));
            Section("REPORTS (EXCEL • PDF • CSV)", ("Company-wise report…", () => Report(this, "ALL", []), "BtnAmber"));
            Section("GATE RECORDS & AUDIT", ("Export gate records", () => ExportEvents(this), "BtnBase"), ("Export audit trail", () => ExportAudit(this), "BtnBase"));
            Section("ENCRYPTED BACKUP & RESTORE", ("Create encrypted backup…", () => BackupUi.Create(this), "BtnAmber"), ("Restore from backup…", () => BackupUi.Restore(this), "BtnDanger"),
                ("Open data folder", () => System.Diagnostics.Process.Start("explorer.exe", Paths.DataDir), "BtnBase"));
            Body.Children.Add(Para("The database key is protected by Windows for this PC only. A password-protected backup (.xvbackup) is the way to recover everything — registry, records, photos, accounts, paired terminals, Comms and settings — on a new PC. Use granular backups to selectively export/import specific data types."));
            AddButton("Close", Close, "BtnAmber");
        }

        void Section(string title, params (string text, Action act, string style)[] buttons)
        {
            Body.Children.Add(Label(title));
            var w = new WrapPanel();
            foreach (var (text, act, style) in buttons) { var b = new Button { Content = text, Style = (Style)Application.Current.Resources[style], Margin = new Thickness(0, 0, 8, 8) }; b.Click += (_, _) => act(); w.Children.Add(b); }
            Body.Children.Add(w);
        }

        void Import(bool persons)
        {
            var dlg = new OpenFileDialog { Filter = "CSV|*.csv" };
            if (dlg.ShowDialog() != true) return;
            var text = File.ReadAllText(dlg.FileName);
            var (ok, errors) = persons ? RegistryImport.Persons(App.Store, text) : RegistryImport.Vehicles(App.Store, text);
            MessageBox.Show($"Imported {ok} record(s)." + (errors.Count > 0 ? $"\n\n{errors.Count} row(s) skipped:\n" + string.Join("\n", errors.Take(15)) : ""), "Import finished");
        }

        void ExportPersonnelBackup(Window owner) => SaveJson($"xv-personnel-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", GranularBackup.ExportPersonnelAsJson(App.Store));

        void ImportPersonnelBackup(Window owner)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var imported = GranularBackup.ImportPersonnelFromJson(App.Store, File.ReadAllText(dlg.FileName));
                MessageBox.Show(owner, $"Imported {imported} personnel record(s).", "Import successful");
            }
            catch (Exception ex) { MessageBox.Show(owner, $"Error: {ex.Message}", "Import failed"); }
        }

        void ExportVehiclesBackup(Window owner) => SaveJson($"xv-vehicles-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", GranularBackup.ExportVehiclesAsJson(App.Store));

        void ImportVehiclesBackup(Window owner)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var imported = GranularBackup.ImportVehiclesFromJson(App.Store, File.ReadAllText(dlg.FileName));
                MessageBox.Show(owner, $"Imported {imported} vehicle(s).", "Import successful");
            }
            catch (Exception ex) { MessageBox.Show(owner, $"Error: {ex.Message}", "Import failed"); }
        }

        void ExportAccountsBackup(Window owner) => SaveJson($"xv-accounts-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", GranularBackup.ExportAccountsAsJson(App.Store));

        void ImportAccountsBackup(Window owner)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var imported = GranularBackup.ImportAccountsFromJson(App.Store, File.ReadAllText(dlg.FileName));
                MessageBox.Show(owner, $"Imported {imported} account(s).", "Import successful");
            }
            catch (Exception ex) { MessageBox.Show(owner, $"Error: {ex.Message}", "Import failed"); }
        }

        void ExportDevicesBackup(Window owner) => SaveJson($"xv-devices-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", GranularBackup.ExportDevicesAsJson(App.Store));

        void ImportDevicesBackup(Window owner)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var imported = GranularBackup.ImportDevicesFromJson(App.Store, File.ReadAllText(dlg.FileName));
                MessageBox.Show(owner, $"Imported {imported} device(s).", "Import successful");
            }
            catch (Exception ex) { MessageBox.Show(owner, $"Error: {ex.Message}", "Import failed"); }
        }

        void ExportSettingsBackup(Window owner) => SaveJson($"xv-settings-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", GranularBackup.ExportSettingsAsJson());

        void ImportSettingsBackup(Window owner)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                GranularBackup.ImportSettingsFromJson(File.ReadAllText(dlg.FileName));
                MessageBox.Show(owner, "Settings imported. Please restart the Command Center for changes to take effect.", "Import successful");
            }
            catch (Exception ex) { MessageBox.Show(owner, $"Error: {ex.Message}", "Import failed"); }
        }

        void SaveJson(string filename, string json)
        {
            var dlg = new SaveFileDialog { FileName = filename, Filter = "JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, json);
            MessageBox.Show($"Backup saved:\n{dlg.FileName}", "Export successful");
        }
    }

    public static void ImportExport(Window owner) => new ImportExportWindow { Owner = owner }.ShowDialog();

    public static IEnumerable<(Func<Window> make, string name)> ScreenshotWindows(Window owner)
    {
        yield return (() => new PairWindow(), "06-pair-device");
        yield return (() => new CloudLinkWindow(), "07-cloud-link");
        yield return (() => new StationsWindow(), "08-stations-settings");
        yield return (() => new PersonDialog(null), "09-add-soldier");
        yield return (() => new ReportDialog("ALL", []), "10-reports-export");
        yield return (() => new CommsWindow(), "12-comms-center");
        yield return (() => new VisitorsWindow(), "13-visitors");
        yield return (() => new VisitorPassDialog(), "14-new-visitor-pass");
        yield return (() => new LeaveWindow(false), "15-leave-overdue");
        yield return (() => new CardRegisterWindow(), "16-card-register");
        var first = App.Store.Persons().FirstOrDefault();
        if (first != null) yield return (() => new AddRecordDialog(S(first["id"])), "11-add-history-record");
        var veh = App.Store.Vehicles().FirstOrDefault();
        if (veh != null) yield return (() => new HistoryWindow("VEHICLE", S(veh["id"])), "17-vehicle-history-route");
        var onWay = App.Store.Transits(Store.TransitEnRoute).FirstOrDefault();
        if (onWay != null)
        {
            yield return (() => TransitDialogs.ResolveWindow(onWay, Store.TransitReached), "18-close-trip");
            yield return (() => new TransitAlertWindow(App.Store.Transits(Store.TransitEnRoute).Where(t => t["overdue"] is true).ToList()), "19-transit-alert");
        }
        yield return (() => TransitDialogs.ManualTripWindow(), "20-add-trip");
        var overdueList = App.Store.Absences().Where(a => a["overdue"] is true).ToList();
        if (overdueList.Count > 0) yield return (() => new AbsenceAlertWindow(overdueList, true, owner), "21-not-returned-alert");
    }
}
