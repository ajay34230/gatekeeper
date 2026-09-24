using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// ID Card Studio: the soldier ID card designer (shared web UI in /idstudio) hosted in WebView2.
/// Soldier data, photos and signatures come only from the encrypted register; the QR on every card is the soldier's
/// genuine secret code read by the gate terminals. Printing and PDF export require the administrator password.
/// </summary>
public sealed class CardStudioWindow : Window
{
    const string HostName = "xv-idstudio.local";
    static CardStudioWindow? _open;
    readonly WebView2 _web = new();
    readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(400) };
    readonly List<string> _preselect;
    string? _pdfPath;
    TaskCompletionSource<JsonObject>? _step;
    int _stepSeq;
    bool _exporting;

    public static void Show(Window owner, IEnumerable<string>? preselect = null)
    {
        if (_open != null) { _open.Activate(); return; }
        _open = new CardStudioWindow(preselect?.ToList() ?? []) { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    CardStudioWindow(List<string> preselect)
    {
        _preselect = preselect;
        Title = "ID Card Studio — Soldier Identity Cards";
        Width = Math.Min(1560, SystemParameters.WorkArea.Width - 40); Height = SystemParameters.WorkArea.Height - 40;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = B("#070B10");
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        Content = _web;
        _refresh.Tick += (_, _) => { _refresh.Stop(); Post(new JsonObject { ["t"] = "soldiers", ["soldiers"] = Soldiers() }); };
        void OnStore() => Dispatcher.BeginInvoke(() => { _refresh.Stop(); _refresh.Start(); });
        App.Store.Changed += OnStore;
        Closed += (_, _) => App.Store.Changed -= OnStore;
        Loaded += async (_, _) => await Init();
    }

    async Task Init()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "idstudio");
        if (!File.Exists(Path.Combine(folder, "index.html")))
        {
            MessageBox.Show("The ID Card Studio files are missing from the installation. Reinstall XV Command Center.", "ID Card Studio");
            Close(); return;
        }
        try
        {
            await _web.EnsureCoreWebView2Async(await Calls.Environment());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("The ID Card Studio needs the Microsoft Edge WebView2 Runtime (part of Windows 11 and current Windows 10).", "ID Card Studio");
            Close(); return;
        }
        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.SetVirtualHostNameToFolderMapping(HostName, folder, CoreWebView2HostResourceAccessKind.Deny);
        core.AddWebResourceRequestedFilter($"https://{HostName}/media/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => e.Response = Media(core, e.Request.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith($"https://{HostName}/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
        core.WebMessageReceived += (_, e) =>
        {
            JsonObject? m = null;
            try { m = JsonNode.Parse(e.TryGetWebMessageAsString()) as JsonObject; } catch (JsonException) { }
            if (m != null) _ = Handle(m);
        };
        core.Navigate($"https://{HostName}/index.html");
    }

    /// <summary>Serves photos and signatures from the encrypted database (never from disk).</summary>
    CoreWebView2WebResourceResponse Media(CoreWebView2 core, string uri)
    {
        var path = new Uri(uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries); // media/photo/{id}
        byte[]? bytes = null; var type = "image/jpeg";
        if (path.Length == 3)
        {
            var id = Uri.UnescapeDataString(path[2]);
            if (path[1] == "photo") bytes = App.Store.PersonPhoto(id);
            else if (path[1] == "signature") { bytes = App.Store.PersonSignature(id); type = "image/png"; }
        }
        if (bytes == null) return core.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
        return core.Environment.CreateWebResourceResponse(new MemoryStream(bytes), 200, "OK", $"Content-Type: {type}\r\nCache-Control: no-store");
    }

    void Post(JsonObject m) { if (_web.CoreWebView2 != null) _web.CoreWebView2.PostWebMessageAsJson(m.ToJsonString()); }
    void Toast(string text, bool error = false) => Post(new JsonObject { ["t"] = error ? "error" : "saved", ["message"] = text });

    static JsonArray Soldiers()
    {
        var media = App.Store.MediaVersions();
        return new JsonArray(App.Store.Persons().Select(p =>
        {
            var id = S(p["id"]);
            var (photoAt, signedAt) = media.GetValueOrDefault(id);
            return (JsonNode)new JsonObject
            {
                ["id"] = id, ["secret"] = S(p["secret_code"]), ["armyNo"] = S(p["service_no"]), ["rank"] = S(p["rank"]), ["name"] = S(p["name"]),
                ["appointment"] = S(p["role"]), ["unit"] = S(p["unit"]), ["company"] = S(p["company"]), ["platoon"] = S(p["platoon"]), ["section"] = S(p["section"]),
                ["category"] = S(p["category"]), ["status"] = S(p["status"]), ["mobile"] = S(p["mobile"]), ["bloodGroup"] = S(p["blood_group"]),
                ["address"] = S(p["address"]), ["dob"] = S(p["dob"]), ["enrolDate"] = S(p["enrol_date"]), ["expiryDate"] = S(p["expiry_date"]),
                ["idMark"] = S(p["id_mark"]), ["nokName"] = S(p["nok_name"]), ["nokRelation"] = S(p["nok_relation"]), ["nokPhone"] = S(p["nok_phone"]),
                ["cardSerial"] = S(p["card_serial"]), ["photoVer"] = photoAt, ["signatureVer"] = signedAt,
                ["signedAt"] = signedAt > 0 ? Time(signedAt, "dd-MM-yyyy") : "",
            };
        }).ToArray());
    }

    static byte[] FromDataUrl(string dataUrl, int maxBytes)
    {
        var comma = dataUrl.IndexOf(',');
        if (!dataUrl.StartsWith("data:image/") || comma < 0) throw new InvalidDataException("Not an image.");
        var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
        if (bytes.Length > maxBytes) throw new InvalidDataException($"Image is larger than {maxBytes / 1024 / 1024} MB.");
        return bytes;
    }

    /// <summary>File name for a soldier's card: the Army Number (Personnel ID when none), made safe for Windows.</summary>
    static string CardFileName(Dictionary<string, object?> p)
    {
        var raw = S(p["service_no"]).Trim();
        if (raw.Length == 0) raw = S(p["id"]);
        var bad = Path.GetInvalidFileNameChars();
        var name = new string(raw.Select(c => bad.Contains(c) ? '-' : c).ToArray()).Trim(' ', '.');
        return name.Length == 0 ? S(p["id"]) : name;
    }

    /// <summary>Renders one soldier at a time in the Studio page and waits for it to report back.</summary>
    async Task<JsonObject> Step(string id, string format)
    {
        _step = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seq = ++_stepSeq;
        Post(new JsonObject { ["t"] = "prepareExport", ["id"] = id, ["format"] = format, ["seq"] = seq });
        var done = await Task.WhenAny(_step.Task, Task.Delay(TimeSpan.FromSeconds(60)));
        if (done != _step.Task) throw new TimeoutException($"The card for {id} did not finish rendering.");
        return _step.Task.Result;
    }

    async Task ExportCards(List<string> ids, string format)
    {
        if (ids.Count == 0 || _exporting) return;
        if (!AdminGate.Require(this, $"Export {ids.Count} ID card file(s)")) return;
        var dlg = new OpenFolderDialog { Title = "Folder for the ID card files (one file per soldier, named by Army Number)" };
        if (dlg.ShowDialog(this) != true) return;
        var folder = dlg.FolderName;
        var people = App.Store.Persons().ToDictionary(p => S(p["id"]));
        var wantPdf = format is "pdf" or "both";
        var wantPng = format is "png" or "both";
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var written = new List<string>();
        var failed = new List<string>();
        _exporting = true;
        try
        {
            foreach (var id in ids)
            {
                if (!people.TryGetValue(id, out var p)) continue;
                var name = CardFileName(p);
                if (!used.Add(name)) { name = $"{name}-{DisplayId(id)}"; used.Add(name); } // two soldiers with the same army no.
                try
                {
                    if (wantPdf)
                    {
                        var r = await Step(id, "pdf");
                        if (r["t"]?.ToString() == "exportFailed") throw new InvalidOperationException(r["message"]?.ToString());
                        var ps = _web.CoreWebView2.Environment.CreatePrintSettings();
                        ps.ShouldPrintBackgrounds = true; ps.ShouldPrintHeaderAndFooter = false;
                        ps.PageWidth = 85.6 / 25.4; ps.PageHeight = 53.98 / 25.4; // ID-1 card
                        ps.MarginTop = ps.MarginBottom = ps.MarginLeft = ps.MarginRight = 0;
                        var path = Path.Combine(folder, name + ".pdf");
                        if (!await _web.CoreWebView2.PrintToPdfAsync(path, ps)) throw new IOException("PDF could not be written");
                        written.Add(Path.GetFileName(path));
                    }
                    if (wantPng)
                    {
                        var r = await Step(id, "png");
                        if (r["t"]?.ToString() == "exportFailed") throw new InvalidOperationException(r["message"]?.ToString());
                        foreach (var side in new[] { "front", "back" })
                        {
                            var path = Path.Combine(folder, $"{name}-{side}.png");
                            await File.WriteAllBytesAsync(path, FromDataUrl(r[side]?.ToString() ?? "", 40_000_000));
                            written.Add(Path.GetFileName(path));
                        }
                    }
                }
                catch (Exception ex) { failed.Add($"{DisplayId(id)}: {ex.Message}"); }
            }
        }
        finally
        {
            _exporting = false;
            Post(new JsonObject { ["t"] = "exportDone" });
        }
        App.Store.AdminAudit("ID_CARDS_EXPORT", $"{format} → {folder}: {written.Count} file(s) for {ids.Count} soldier(s)");
        var msg = $"Exported {written.Count} file(s) to\n{folder}" + (failed.Count > 0 ? $"\n\nNot exported ({failed.Count}):\n" + string.Join("\n", failed.Take(10)) : "") + "\n\nOpen the folder now?";
        if (MessageBox.Show(this, msg, "Export cards", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            System.Diagnostics.Process.Start("explorer.exe", folder);
    }

    async Task Handle(JsonObject m)
    {
        try
        {
            switch (m["t"]?.ToString())
            {
                case "ready":
                    var design = App.Settings.CardDesignJson.Length > 2 ? JsonNode.Parse(App.Settings.CardDesignJson) : null;
                    Post(new JsonObject { ["t"] = "init", ["soldiers"] = Soldiers(), ["design"] = design, ["preselect"] = new JsonArray(_preselect.Select(i => (JsonNode)i).ToArray()) });
                    break;

                case "saveDesign":
                    var json = m["design"]?.ToJsonString() ?? "";
                    if (json.Length > 6_000_000) { Toast("Design images are too large — use smaller emblem images.", true); break; }
                    App.Settings.CardDesignJson = json; App.Settings.Save();
                    App.Store.AdminAudit("CARD_DESIGN_SAVED");
                    Post(new JsonObject { ["t"] = "designSaved" }); Toast("Card design saved.");
                    break;

                case "saveSoldier":
                    if (m["soldier"] is not JsonObject d) break;
                    var id = Store.CanonId(d["id"]?.ToString());
                    var cur = App.Store.Person(id) ?? throw new InvalidOperationException("This soldier is no longer in the register.");
                    // Start from the full current record so fields the Studio does not edit are kept.
                    var o = new JsonObject();
                    foreach (var c in SoldierFile.BuiltIn) o[c.Key] = S(cur[c.Column]);
                    string V(string k) => d[k]?.ToString()?.Trim() ?? "";
                    o["id"] = id; o["serviceNo"] = V("armyNo"); o["rank"] = V("rank"); o["name"] = V("name"); o["role"] = V("appointment"); o["unit"] = V("unit");
                    o["company"] = SoldierFile.CanonCompany(V("company")); o["platoon"] = V("platoon"); o["section"] = V("section"); o["mobile"] = V("mobile");
                    o["bloodGroup"] = V("bloodGroup"); o["address"] = V("address"); o["dob"] = V("dob"); o["enrolDate"] = V("enrolDate"); o["expiryDate"] = V("expiryDate");
                    o["idMark"] = V("idMark"); o["nokName"] = V("nokName"); o["nokRelation"] = V("nokRelation"); o["nokPhone"] = V("nokPhone"); o["cardSerial"] = V("cardSerial");
                    App.Store.UpsertPerson(o, "PC-CARD-STUDIO");
                    Toast($"{DisplayId(id)} saved to the register.");
                    break;

                case "savePhoto":
                    var pid = Store.CanonId(m["id"]?.ToString());
                    var url = m["dataUrl"]?.ToString() ?? "";
                    App.Store.SetPersonPhoto(pid, url.Length == 0 ? null : Photo.Prepare(FromDataUrl(url, 15_000_000)), "PC-CARD-STUDIO");
                    Toast(url.Length == 0 ? "Photo removed." : "Photo saved (cropped to passport size).");
                    break;

                case "saveSignature":
                    var sid = Store.CanonId(m["id"]?.ToString());
                    var sig = m["dataUrl"]?.ToString() ?? "";
                    App.Store.SetPersonSignature(sid, sig.Length == 0 ? null : FromDataUrl(sig, 2_000_000), "PC-CARD-STUDIO");
                    Toast(sig.Length == 0 ? "Signature cleared." : "Signature saved.");
                    break;

                case "exportData":
                    SoldierRegister.Export(this, (m["ids"] as JsonArray)?.Select(x => x!.ToString()).ToList());
                    break;

                case "print":
                case "pdf":
                    var ids = (m["ids"] as JsonArray)?.Select(x => x!.ToString()).ToList() ?? [];
                    if (ids.Count == 0) break;
                    var pdf = m["t"]!.ToString() == "pdf";
                    if (!AdminGate.Require(this, $"{(pdf ? "Save" : "Print")} {ids.Count} ID card(s)")) break;
                    if (pdf)
                    {
                        var dlg = new SaveFileDialog { FileName = $"XV-ID-Cards-{DateTime.Now:yyyyMMdd-HHmm}.pdf", Filter = "PDF document|*.pdf" };
                        if (dlg.ShowDialog(this) != true) break;
                        _pdfPath = dlg.FileName;
                    }
                    App.Store.AdminAudit(pdf ? "ID_CARDS_PDF" : "ID_CARDS_PRINT", string.Join(",", ids.Take(50)) + (ids.Count > 50 ? $" (+{ids.Count - 50})" : ""));
                    Post(new JsonObject { ["t"] = "preparePrint", ["purpose"] = pdf ? "pdf" : "print", ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) });
                    break;

                case "exportCards":
                    await ExportCards((m["ids"] as JsonArray)?.Select(x => x!.ToString()).ToList() ?? [], m["format"]?.ToString() ?? "pdf");
                    break;

                case "exportReady":
                case "exportImages":
                case "exportFailed":
                    if (m["seq"]?.GetValue<int>() == _stepSeq) _step?.TrySetResult(m);
                    break;

                case "printReady":
                    if (_pdfPath == null) break;
                    var path = _pdfPath; _pdfPath = null;
                    var settings = _web.CoreWebView2.Environment.CreatePrintSettings();
                    settings.ShouldPrintBackgrounds = true;
                    settings.Orientation = CoreWebView2PrintOrientation.Portrait;
                    settings.PageWidth = 8.27; settings.PageHeight = 11.69;
                    settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = 0.39;
                    settings.ShouldPrintHeaderAndFooter = false;
                    var ok = await _web.CoreWebView2.PrintToPdfAsync(path, settings);
                    Post(new JsonObject { ["t"] = "printDone" });
                    if (!ok) { Toast("The PDF could not be written.", true); break; }
                    if (MessageBox.Show(this, "ID cards saved as PDF (true card size, with cut marks). Open it now?", "ID Card Studio", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    break;
            }
        }
        catch (Exception ex) { Toast(ex.Message, true); }
    }
}
