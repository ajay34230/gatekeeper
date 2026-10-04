using XV.Comms;
using XV.Core;

// Headless host of the same server (no UI). Used for automated tests and for running on a machine without a desktop.
// Usage: XV.Server [--create-operator NAME ID PASSWORD] [--import-persons file.csv] [--import-vehicles file.csv] [--add-location ID NAME] [--add-gate ID NAME] [--pair]
var ri0 = Array.IndexOf(args, "--restore");
if (ri0 >= 0 && ri0 + 2 < args.Length)
{
    Backup.Restore(args[ri0 + 1], args[ri0 + 2]);
    Console.WriteLine("Restored from " + args[ri0 + 1]);
}
var settings = Settings.Load();
// Cloud Link through a tunnel with a public certificate (e.g. a Cloudflare tunnel): --cloud-url URL [--comms-cloud-url URL]
var cu = Array.IndexOf(args, "--cloud-url");
if (cu >= 0 && cu + 1 < args.Length)
{
    settings.InternetEnabled = true; settings.CloudMode = "TUNNEL"; settings.CloudUrl = args[cu + 1]; settings.CloudUsesPublicCertificate = true;
    var ccu = Array.IndexOf(args, "--comms-cloud-url");
    if (ccu >= 0 && ccu + 1 < args.Length) settings.CommsCloudUrl = args[ccu + 1];
    settings.Save();
    Console.WriteLine($"CLOUD {settings.PublicUrl} comms {settings.CommsPublicUrl}");
}
using var store = new Store(settings);
var cert = CertManager.LoadOrCreate(settings);
await using var server = new ApiServer(store, cert);
server.Log += Console.WriteLine;
var ci = Array.IndexOf(args, "--create-operator");
if (ci >= 0 && ci + 3 < args.Length) Console.WriteLine("Created " + store.CreateAccount(args[ci + 1], args[ci + 2], args[ci + 3], "ADMIN").id);
for (var i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--import-persons" || args[i] == "--import-soldiers")
    {
        var (headers, rows) = SoldierFile.Read(args[i + 1]);
        var (added, updated, errors) = SoldierFile.Import(store, headers, rows, settings.CustomFields);
        Console.WriteLine($"Imported soldiers: {added} added, {updated} updated" + (errors.Count > 0 ? "; " + string.Join("; ", errors) : ""));
    }
    if (args[i] == "--export-soldiers")
    {
        SoldierFile.Export(store.Persons(), settings.CustomFields, args[i + 1], "All soldiers");
        Console.WriteLine("Exported soldiers to " + args[i + 1]);
    }
    if (args[i] == "--import-vehicles") Console.WriteLine("Imported vehicles: " + RegistryImport.Vehicles(store, File.ReadAllText(args[i + 1])).ok);
    if (args[i] == "--add-location" && i + 2 < args.Length) store.UpsertLocation(args[i + 1], args[i + 2]);
    if (args[i] == "--add-gate" && i + 2 < args.Length) store.UpsertGate(args[i + 1], args[i + 2]);
}
for (var i = 0; i + 3 < args.Length; i++)
    if (args[i] == "--add-record")
        store.AddManualRecord(args[i + 1], args[i + 2], Store.NowMs - (i * 3_600_000L), "LOC07", "G02", args[i + 3]);
// --add-leave PERSON REASON DAYS_AGO RETURN_DAYS_AGO : entry a day before, then an exit with an expected return date (CI test data)
for (var i = 0; i + 4 < args.Length; i++)
    if (args[i] == "--add-leave")
    {
        var left = Store.NowMs - long.Parse(args[i + 3]) * 86_400_000L;
        store.AddManualRecord(args[i + 1], "ENTRY", left - 86_400_000L, "LOC07", "G02", "CI test entry");
        store.AddManualRecord(args[i + 1], "EXIT", left, "LOC07", "G02", "CI test leave", expectedReturn: Store.NowMs - long.Parse(args[i + 4]) * 86_400_000L, reason: args[i + 2]);
    }
// --add-trip VEHICLE FROM_LOC DEST MINUTES_AGO APPROX_MIN TAKEN_MIN : a vehicle trip entered by the server operator (CI test data); TAKEN 0 = still on the way
for (var i = 0; i + 6 < args.Length; i++)
    if (args[i] == "--add-trip")
    {
        // optional 8th/9th values: KIND (STOPPED / DIVERTED) and the place, closing the trip with the TAKEN minutes
        var kind = i + 7 < args.Length && !args[i + 7].StartsWith("--") ? args[i + 7] : "";
        var place = kind.Length > 0 && i + 8 < args.Length && !args[i + 8].StartsWith("--") ? args[i + 8] : "";
        var taken = int.Parse(args[i + 6]);
        var tid = store.AddManualTransit(args[i + 1], args[i + 2], args[i + 3], Store.NowMs - long.Parse(args[i + 4]) * 60_000L, int.Parse(args[i + 5]), kind.Length > 0 ? 0 : taken);
        if (kind.Length > 0) store.ResolveTransit(tid, kind, taken, place, "", "PC-ADMIN", "SERVER");
    }
// --add-vehicle-trip VEHICLE DRIVER FROM DEST ENTRY_MIN_AGO EXIT_MIN_AGO APPROX_MIN : a real gate ENTRY then EXIT with a destination,
// sent through the same vehicle transaction a terminal uses (CI test data)
for (var i = 0; i + 7 < args.Length; i++)
    if (args[i] == "--add-vehicle-trip")
    {
        var vid = Store.CanonId(args[i + 1]); var driver = Store.CanonId(args[i + 2]); var from = Store.CanonId(args[i + 3]);
        var entryTs = Store.NowMs - long.Parse(args[i + 5]) * 60_000L; var exitTs = Store.NowMs - long.Parse(args[i + 6]) * 60_000L;
        System.Text.Json.Nodes.JsonObject GateEvent(string id, string type, long ts, System.Text.Json.Nodes.JsonObject? extra = null)
        {
            var e = new System.Text.Json.Nodes.JsonObject
            {
                ["eventId"] = id, ["entityType"] = "VEHICLE", ["entityId"] = vid, ["eventType"] = type, ["locationId"] = from, ["gateId"] = "G02",
                ["deviceId"] = "CI-TERMINAL", ["operatorId"] = "GK-01", ["eventTimestamp"] = ts, ["createdAt"] = ts, ["sourceType"] = "DIRECT",
            };
            if (extra != null) foreach (var kv in extra) e[kv.Key] = kv.Value?.DeepClone();
            return e;
        }
        System.Text.Json.Nodes.JsonObject Manifest(string entryId, string state, long exitAt) => new()
        {
            ["manifestId"] = "MNF-" + entryId, ["vehicleId"] = vid, ["entryEventId"] = entryId, ["locationId"] = from, ["gateId"] = "G02", ["driverId"] = driver,
            ["coDriverId"] = null, ["occupants"] = new System.Text.Json.Nodes.JsonArray(driver), ["createdAt"] = entryTs, ["state"] = state,
        };
        var entryId = "CI-ENT-" + Store.RandomCode(6);
        store.VehicleTransaction(new System.Text.Json.Nodes.JsonObject { ["event"] = GateEvent(entryId, "ENTRY", entryTs), ["manifest"] = Manifest(entryId, "ACTIVE", 0) }, "CI-TERMINAL", "GK-01");
        var exitId = "CI-EXT-" + Store.RandomCode(6);
        var dest = new System.Text.Json.Nodes.JsonObject { ["destinationId"] = Store.CanonId(args[i + 4]), ["transitMinutes"] = int.Parse(args[i + 7]) };
        store.VehicleTransaction(new System.Text.Json.Nodes.JsonObject { ["event"] = GateEvent(exitId, "EXIT", exitTs, dest), ["manifest"] = Manifest(entryId, "EXITED", exitTs) }, "CI-TERMINAL", "GK-01");
        Console.WriteLine($"VEHICLE-TRIP {vid} {from}>{args[i + 4]}");
    }
// --alert-targets LOCATIONS OPERATORS (comma separated): the terminals the leave alert would also be sent to
var ati = Array.IndexOf(args, "--alert-targets");
if (ati >= 0 && ati + 2 < args.Length)
    foreach (var d in store.TerminalsForAlert(args[ati + 1].Split(',', StringSplitOptions.RemoveEmptyEntries), args[ati + 2].Split(',', StringSplitOptions.RemoveEmptyEntries)))
        Console.WriteLine("ALERT-TARGET " + d);
// --add-visitor NAME FROM_MINUTES TO_MINUTES : visitor pass valid from now+FROM to now+TO (CI test data)
for (var i = 0; i + 3 < args.Length; i++)
    if (args[i] == "--add-visitor")
    {
        var vid = store.CreateVisitorPass(new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = args[i + 1], ["purpose"] = "CI test visit", ["host"] = "P001", ["idProof"] = "CI test ID",
            ["validFrom"] = Store.NowMs + long.Parse(args[i + 2]) * 60_000, ["validTo"] = Store.NowMs + long.Parse(args[i + 3]) * 60_000,
        });
        Console.WriteLine($"VISITOR {vid}");
    }
if (args.Contains("--absences"))
    foreach (var a in store.Absences())
        Console.WriteLine($"ABSENCE {a["person_id"]} {a["kind"]} reason={a["reason"]} overdue={a["overdue"]}");
if (args.Contains("--transits"))
{
    foreach (var t in store.Transits())
        Console.WriteLine($"TRANSIT {t["vehicle_id"]} {t["from_loc"]}>{(t["dest_loc"]?.ToString() is { Length: > 0 } d ? d : t["dest_name"])} {t["state"]} via={t["resolved_via"]} min={t["actual_min"]} end={t["end_loc"]}{t["end_name"]}");
    foreach (var r in store.TransitRoutes()) Console.WriteLine($"ROUTE {r["from_loc"]}>{r["to_loc"]} {r["minutes"]} {r["source"]}");
    foreach (var a in store.TransitAverages()) Console.WriteLine($"AVG {a["from_loc"]}>{a["to_key"]} trips={a["trips"]} avg={Math.Round(Convert.ToDouble(a["avg_min"]))}");
}
if (args.Contains("--card-register"))
    foreach (var c in store.CardRegister())
        Console.WriteLine($"CARD {c["id"]} issues={c["issues"]} lost={c["lost"]}");
for (var i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--set-photo" && i + 2 < args.Length) { store.SetPersonPhoto(Store.CanonId(args[i + 1]), File.ReadAllBytes(args[i + 2]), "CI"); Console.WriteLine("PHOTO " + args[i + 1]); }
    if (args[i] == "--report-lost") { store.ReportCardLost(args[i + 1], "CI test"); Console.WriteLine("LOST " + args[i + 1]); }
}
var bi = Array.IndexOf(args, "--backup");
if (bi >= 0 && bi + 2 < args.Length)
{
    using var commsStore = new CommsStore();
    Backup.Create(args[bi + 1], args[bi + 2], settings, store.ExportEncrypted, commsStore.ExportEncrypted);
    Console.WriteLine("Backup written to " + args[bi + 1]);
}
if (args.Contains("--verify-audit"))
{
    var (n, problems) = store.VerifyAudit();
    Console.WriteLine($"AUDIT {n} entries, {problems.Count} problem(s)" + (problems.Count > 0 ? ": " + string.Join(" | ", problems) : ""));
}
var ci2 = Array.IndexOf(args, "--connection-sheet");
if (ci2 >= 0 && ci2 + 1 < args.Length)
{
    var fp = CertManager.Fingerprint(cert);
    Directory.CreateDirectory(args[ci2 + 1]);
    ConnectionSheet.Pdf(settings, fp, Path.Combine(args[ci2 + 1], "XV-Connection.pdf"));
    ConnectionSheet.Txt(settings, fp, Path.Combine(args[ci2 + 1], "XV-Connection.txt"));
    ConnectionSheet.Json(settings, fp, Path.Combine(args[ci2 + 1], "XV-Connection.json"));
    Console.WriteLine("Connection details written to " + args[ci2 + 1]);
    return;
}
var dri = Array.IndexOf(args, "--demo-route");
if (dri >= 0 && dri + 1 < args.Length) { DemoRoute.Write(args[dri + 1]); Console.WriteLine("Demo route charts written to " + args[dri + 1]); return; }
var tri = Array.IndexOf(args, "--transit-report");
if (tri >= 0 && tri + 1 < args.Length)
{
    Directory.CreateDirectory(args[tri + 1]);
    var d0 = DateTime.Today.AddDays(-30);
    Reports.TransitExcel(store, d0, DateTime.Today, Path.Combine(args[tri + 1], "XV-Transit.xlsx"));
    Reports.TransitPdf(store, d0, DateTime.Today, Path.Combine(args[tri + 1], "XV-Transit.pdf"));
    Reports.TransitCsv(store, d0, DateTime.Today, Path.Combine(args[tri + 1], "XV-Transit.csv"));
    var vh = store.EventsForEntity("VEHICLE", "V014");
    var manual = store.Transits("", "V014").Where(t => (t["exit_event_id"]?.ToString() ?? "").Length == 0).ToList();
    Reports.RouteChartExcel(vh, "V014 • demo vehicle", "Movement history", Path.Combine(args[tri + 1], "XV-Route.xlsx"), manual);
    Reports.RouteChartPdf(vh, "V014 • demo vehicle", "Movement history", Path.Combine(args[tri + 1], "XV-Route.pdf"), manual);
    Console.WriteLine("Transit reports written to " + args[tri + 1]);
    return;
}
var ri = Array.IndexOf(args, "--report");
if (ri >= 0 && ri + 1 < args.Length)
{
    Directory.CreateDirectory(args[ri + 1]);
    var req = new ReportRequest("ALL", [], DateTime.Today.AddDays(-30), DateTime.Today);
    Reports.Excel(store, req, Path.Combine(args[ri + 1], "XV-Report.xlsx"));
    Reports.Pdf(store, req, Path.Combine(args[ri + 1], "XV-Report.pdf"));
    Reports.Csv(store, req, Path.Combine(args[ri + 1], "XV-Report.csv"));
    Console.WriteLine("Reports written to " + args[ri + 1]);
    return;
}
var ei = Array.IndexOf(args, "--export");
if (ei >= 0 && ei + 1 < args.Length)
{
    // Same CSV layouts as the Command Center's Import / Export window.
    Directory.CreateDirectory(args[ei + 1]);
    File.WriteAllText(Path.Combine(args[ei + 1], "persons.csv"), Csv.Build(store.Persons(), ("ID", "id"), ("Name", "name"), ("Rank", "rank"), ("Service No", "service_no"), ("Unit", "unit"), ("Company", "company"), ("Status", "status"), ("Inside since", "inside_since"), ("Last seen", "last_seen")));
    File.WriteAllText(Path.Combine(args[ei + 1], "vehicles.csv"), Csv.Build(store.Vehicles(), ("ID", "id"), ("Plate", "plate"), ("Type", "type"), ("Model", "model"), ("Company", "company"), ("Status", "status"), ("In yard since", "inside_since")));
    File.WriteAllText(Path.Combine(args[ei + 1], "records.csv"), Csv.Build(store.RecentEvents(100000), ("Seq", "seq"), ("Time", "event_ts"), ("Action", "event_type"), ("Type", "entity_type"), ("ID", "entity_id"), ("Name / Plate", "title"), ("Location", "location_name"), ("Gate", "gate_name"), ("Operator", "operator_id"), ("Terminal", "device_id"), ("Stay ms", "stay_ms"), ("Loc flag", "loc_mismatch"), ("QR location", "scanned_loc"), ("Occupants", "occupants")));
    File.WriteAllText(Path.Combine(args[ei + 1], "audit.csv"), Csv.Build(store.AuditLog(100000), ("Time", "created_at"), ("Actor", "actor"), ("Action", "action"), ("Entity", "entity_id"), ("Detail", "detail")));
    Console.WriteLine("Exported to " + args[ei + 1]);
    return;
}
if (args.Contains("--setup-only")) return;
await server.StartAsync();
using var discovery = new DiscoveryResponder(settings, server.Fingerprint);
await using var comms = new CommsEngine(settings, cert, store.DeviceKey);
comms.Log += Console.WriteLine;
comms.MessageReceived += m => Console.WriteLine($"COMMS {m.Kind} from {m.DeviceId}: {m.Body}");
// CI only: answer every terminal message so the protocol test can check PC -> terminal delivery.
if (args.Contains("--comms-test-echo"))
{
    comms.MessageReceived += m => comms.Send(m.DeviceId, "MESSAGE", "Echo: " + m.Body, "Command Center");
    // The headless host has no call UI: answer call invitations with "busy" so the signalling path can be tested.
    comms.Signal += (dev, f) =>
    {
        if (f["op"]?.ToString() == "invite") comms.SendSignal(dev, new System.Text.Json.Nodes.JsonObject { ["t"] = "call", ["op"] = "busy", ["callId"] = f["callId"]?.ToString() });
    };
}
await comms.StartAsync();
Console.WriteLine($"Server {settings.ServerId} fingerprint {server.Fingerprint}");
if (args.Contains("--pair"))
{
    var code = store.NewPairCode();
    Console.WriteLine("PAIRCODE " + code);
    Console.WriteLine("PAIRQR " + Pairing.Payload(settings, server.Fingerprint, code));
}
// CI only: revoke the device named in <data>/ci-revoke so the protocol test can check the remote-wipe order.
if (args.Contains("--ci-control"))
    _ = Task.Run(async () =>
    {
        var f = Paths.File("ci-revoke");
        while (true)
        {
            await Task.Delay(300);
            if (File.Exists(f)) { var id = File.ReadAllText(f).Trim(); File.Delete(f); store.RevokeDevice(id); Console.WriteLine("REVOKED " + id); }
            var sf = Paths.File("ci-suspend");
            if (File.Exists(sf)) { var pid = File.ReadAllText(sf).Trim(); File.Delete(sf); store.SetPersonStatus(pid, "SUSPENDED", "CI"); Console.WriteLine("SUSPENDED " + pid); }
        }
    });
var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
await done.Task;
