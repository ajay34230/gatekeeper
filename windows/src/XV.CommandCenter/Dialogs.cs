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
        Title = title; Width = width; Height = Math.Min(height, SystemParameters.WorkArea.Height - 20); Background = B("#101012");
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResizeWithGrip;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        var dock = new DockPanel();
        var head = new Border { Padding = new Thickness(22, 16, 22, 14), BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 0, 0, 1), Child = Col(T(title, 16, "#F4F4F5", bold: true), new TextBlock { Text = subtitle, Foreground = B("#A1A1AA"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }) };
        DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head);
        var foot = new Border { Padding = new Thickness(22, 12, 22, 12), BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 1, 0, 0), Child = Footer };
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

    protected static TextBlock Label(string text) => new() { Text = text.ToUpperInvariant(), Foreground = B("#A1A1AA"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) };
    protected static TextBlock Para(string text, string color = "#A1A1AA") => new() { Text = text, Foreground = B(color), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), LineHeight = 18 };

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
            Body.Children.Add(Label("Photo (uniform passport portrait)"));
            byte[]? photo = id == null ? null : App.Store.PersonPhoto(id);
            var photoChanged = false;
            var img = new Image { Width = 96, Height = 120, Stretch = System.Windows.Media.Stretch.UniformToFill, Source = Photo.Image(photo) };
            var photoBox = new Border { Width = 100, Height = 124, BorderBrush = B("#D97706"), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(6), Child = img, Background = B("#0C0C0E") };
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
                Width = 380, Background = B("#FFFFFF"), CornerRadius = new CornerRadius(14), BorderBrush = B("#D4D4D8"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center,
                Child = Col(
                    new Border { Background = B("#18181B"), CornerRadius = new CornerRadius(13, 13, 0, 0), Padding = new Thickness(16, 12, 16, 12), Child = Spread(Col(T("XV DIGITAL ACCESS CONTROL", 12, "#FFFFFF", bold: true), T(person ? "MILITARY IDENTITY CREDENTIAL" : "VEHICLE ACCESS CREDENTIAL", 9.5, "#34D399", bold: true, mono: true)), T(DisplayId(id), 14, "#FBBF24", bold: true, mono: true)) },
                    new Border { Padding = new Thickness(18), Child = Col(
                        T(person ? S(r["name"]) : S(r["plate"]), 18, "#18181B", bold: true).Center(),
                        T(person ? string.Join(" • ", new[] { S(r["rank"]), S(r["company"]) is { Length: > 0 } c ? c + " Co" : "", S(r["unit"]) }.Where(x => x.Length > 0)) : string.Join(" • ", new[] { S(r["type"]), S(r["model"]), S(r["company"]) }.Where(x => x.Length > 0)), 11.5, "#52525B").Center().M(0, 4),
                        qr,
                        T(person ? $"Service No: {S(r["service_no"])}   I-Card: {S(r["id_card"])}" : $"Mil Reg: {S(r["mil_reg"])}", 10.5, "#3F3F46", mono: true).Center(),
                        T("Present this code at any XV gate terminal", 9.5, "#A1A1AA").Center().M(0, 8)) }),
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
        public HistoryWindow(string type, string id) : base("Movement History • " + DisplayId(id), "Every entry and exit recorded for this credential, newest first.", 760, 640)
        {
            var rows = App.Store.RecentEvents(1000, id, type).Where(r => S(r["entity_id"]) == id).ToList();
            if (rows.Count == 0) Body.Children.Add(Para("No gate activity has been recorded yet."));
            long? total = 0;
            foreach (var r in rows)
            {
                var entry = S(r["event_type"]) == "ENTRY";
                total += L(r["stay_ms"]);
                Body.Children.Add(Card(Spread(
                    Row(Pill(S(r["event_type"]), entry ? "#34D399" : "#FBBF24", entry ? "#0D2A20" : "#2A1F08", entry ? "#047857" : "#92400E"),
                        T("  " + Time(L(r["event_ts"]), "ddd dd MMM yyyy  HH:mm:ss"), 12, "#F4F4F5", mono: true),
                        T($"   {S(r["location_name"])} • {S(r["gate_name"])}", 11.5, "#A1A1AA")),
                    Row(L(r["loc_mismatch"]) == 1 ? T("⚠ LOC FLAG  ", 10.5, "#FCD34D", bold: true, mono: true) : new TextBlock(),
                        T(Note(r, "  •  ") + (L(r["stay_ms"]) > 0 ? "Stayed " + Duration(L(r["stay_ms"])) : "Op " + S(r["operator_id"])), 11, "#D4D4D8", mono: true))), "#131316", pad: 10).M(0, 0, 0, 6));
            }
            if (rows.Count > 0) Body.Children.Insert(0, Para($"{rows.Count} records • total recorded time on site: {Duration(total ?? 0)}", "#FBBF24"));
            if (type == "PERSON")
            {
                AddButton("+ Add Record", () => { Close(); AddRecord(Owner, id); History(Owner, type, id); }, "BtnBlue");
                AddButton("Export…", () => Report(this, "ALL", [id]), "BtnEmerald");
            }
            AddButton("Close", Close, "BtnAmber");
        }
    }

    public static void History(Window owner, string type, string id) => new HistoryWindow(type, id) { Owner = owner }.ShowDialog();

    sealed class AddRecordDialog : DarkWindow
    {
        public AddRecordDialog(string personId) : base("+ Add Record to History • " + DisplayId(personId),
            "ENTRY / EXIT update who is inside, exactly like a gate scan. Other types (Leave, Duty, Course…) are dated history entries. Types are configured in Stations & Settings.", 560, 640)
        {
            var person = App.Store.Persons().First(x => S(x["id"]) == personId);
            Body.Children.Add(Para($"{S(person["name"])} • {S(person["rank"])} • {S(person["company"])} Co • currently {(L(person["inside_since"]) > 0 ? "INSIDE" : "OUTSIDE")}", "#FBBF24"));
            var type = Choice("Record type", new[] { "ENTRY", "EXIT" }.Concat(App.Settings.EventTypes), L(person["inside_since"]) > 0 ? "EXIT" : "ENTRY");
            Body.Children.Add(Label("Date"));
            var date = new DatePicker { SelectedDate = DateTime.Today, DisplayDateEnd = DateTime.Today };
            Body.Children.Add(date);
            var time = Field("Time (HH:mm)", DateTime.Now.ToString("HH:mm"), mono: true);
            var locs = App.Store.Locations().Select(l => $"{S(l["id"])} — {S(l["name"])}").ToList();
            var gates = App.Store.Gates().Select(g => $"{S(g["id"])} — {S(g["name"])}").ToList();
            var loc = Choice("Location", locs, locs.FirstOrDefault() ?? "");
            var gate = Choice("Gate", gates, gates.FirstOrDefault() ?? "");
            var remarks = Field("Remarks (reason, authority, pass number…)");
            AddButton("Cancel", Close);
            AddButton("Save Record", () =>
            {
                try
                {
                    if (!TimeSpan.TryParse(time.Text.Trim(), out var tod)) throw new Exception("Enter the time as HH:mm, e.g. 14:30");
                    var when = (date.SelectedDate ?? DateTime.Today).Date + tod;
                    App.Store.AddManualRecord(personId, type.Text, new DateTimeOffset(when).ToUnixTimeMilliseconds(),
                        loc.Text.Split(' ')[0], gate.Text.Split(' ')[0], remarks.Text);
                    DialogResult = true;
                }
                catch (Exception ex) { Fail(ex); }
            }, "BtnAmber");
        }
    }

    public static void AddRecord(Window owner, string personId) => new AddRecordDialog(personId) { Owner = owner }.ShowDialog();

    sealed class ReportDialog : DarkWindow
    {
        public ReportDialog(string company, List<string> personIds) : base("Reports & Export",
            "Excel: summary sheet plus one colour-coded sheet per company (roster + records). PDF: printable A4 report. CSV: plain records for any software.", 600, 620)
        {
            var scope = personIds.Count > 0 ? $"{personIds.Count} selected: " + string.Join(", ", personIds.Take(8).Select(DisplayId)) + (personIds.Count > 8 ? "…" : "") : "";
            if (scope.Length > 0) Body.Children.Add(Para(scope, "#FBBF24"));
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
        App.Store.RecentEvents(1_000_000).Select(r => { r["time"] = Time(L(r["event_ts"]), "yyyy-MM-dd HH:mm:ss"); r["stay"] = L(r["stay_ms"]) > 0 ? Duration(L(r["stay_ms"])) : "";
            r["occupants"] = S(r["occupants"]).Length > 2 ? string.Join(", ", (System.Text.Json.JsonSerializer.Deserialize<List<string>>(S(r["occupants"])) ?? []).Select(DisplayId)) : "";
            r["loc_mismatch"] = L(r["loc_mismatch"]) == 1 ? "YES" : ""; return r; }),
        ("Time", "time"), ("Action", "event_type"), ("Type", "entity_type"), ("ID", "entity_id"), ("Name / Plate", "title"), ("Location", "location_name"), ("Gate", "gate_name"),
        ("Operator", "operator_id"), ("Terminal", "device_id"), ("Stay", "stay"), ("Location flag", "loc_mismatch"), ("QR location", "scanned_loc"), ("Occupants", "occupants"), ("Event ID", "event_id"), ("Server seq", "seq")));

    public static void ExportAudit(Window o) => SaveCsv($"xv-audit-{DateTime.Now:yyyyMMdd-HHmm}.csv", Csv.Build(
        App.Store.AuditLog(1_000_000).Select(r => { r["time"] = Time(L(r["created_at"]), "yyyy-MM-dd HH:mm:ss"); return r; }),
        ("Time", "time"), ("Actor", "actor"), ("Action", "action"), ("Entity type", "entity_type"), ("Entity", "entity_id"), ("Detail", "detail")));

    static readonly (string, string)[] PersonCols = [("id", "id"), ("name", "name"), ("rank", "rank"), ("service_no", "service_no"), ("unit", "unit"), ("company", "company"), ("role", "role"), ("category", "category"), ("status", "status"), ("mobile", "mobile"), ("id_card", "id_card"), ("blood_group", "blood_group"), ("access_locations", "access_locations")];
    static readonly (string, string)[] VehicleCols = [("id", "id"), ("plate", "plate"), ("mil_reg", "mil_reg"), ("type", "type"), ("model", "model"), ("company", "company"), ("status", "status")];

    sealed class ImportExportWindow : DarkWindow
    {
        public ImportExportWindow() : base("Import / Export", "Move registry data in and out as CSV files (open directly in Excel). Imports add new records and update existing IDs.", 620, 560)
        {
            Section("SOLDIER REGISTER (EXCEL • CSV)",
                ("Import Excel / CSV", () => SoldierRegister.Import(this), "BtnEmerald"), ("Export…", () => SoldierRegister.Export(this), "BtnBase"),
                ("Blank template", () => SoldierRegister.Template(this), "BtnBase"), ("Import photos from folder", () => SoldierRegister.ImportPhotos(this), "BtnBase"));
            Section("VEHICLE FLEET",
                ("Import CSV", () => Import(false), "BtnEmerald"), ("Export CSV", () => SaveCsv("xv-vehicles.csv", Csv.Build(App.Store.Vehicles(), VehicleCols)), "BtnBase"),
                ("Blank template", () => SaveCsv("xv-vehicles-template.csv", Csv.Build([], VehicleCols), containsData: false), "BtnBase"));
            Section("REPORTS (EXCEL • PDF • CSV)", ("Company-wise report…", () => Report(this, "ALL", []), "BtnAmber"));
            Section("GATE RECORDS & AUDIT", ("Export gate records", () => ExportEvents(this), "BtnBase"), ("Export audit trail", () => ExportAudit(this), "BtnBase"));
            Section("ENCRYPTED BACKUP & RESTORE", ("Create encrypted backup…", () => BackupUi.Create(this), "BtnAmber"), ("Restore from backup…", () => BackupUi.Restore(this), "BtnDanger"),
                ("Open data folder", () => System.Diagnostics.Process.Start("explorer.exe", Paths.DataDir), "BtnBase"));
            Body.Children.Add(Para("The database key is protected by Windows for this PC only. A password-protected backup (.xvbackup) is the way to recover everything — registry, records, photos, accounts, paired terminals, Comms and settings — on a new PC."));
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
    }
}
