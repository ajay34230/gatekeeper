using XV.Comms;
using XV.Core;

// Headless host of the same server (no UI). Used for automated tests and for running on a machine without a desktop.
// Usage: XV.Server [--create-operator NAME ID PASSWORD] [--import-persons file.csv] [--import-vehicles file.csv] [--add-location ID NAME] [--add-gate ID NAME] [--pair]
var settings = Settings.Load();
using var store = new Store(settings);
var cert = CertManager.LoadOrCreate(settings);
await using var server = new ApiServer(store, cert);
server.Log += Console.WriteLine;
var ci = Array.IndexOf(args, "--create-operator");
if (ci >= 0 && ci + 3 < args.Length) Console.WriteLine("Created " + store.CreateAccount(args[ci + 1], args[ci + 2], args[ci + 3], "ADMIN").id);
for (var i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--import-persons") Console.WriteLine("Imported persons: " + RegistryImport.Persons(store, File.ReadAllText(args[i + 1])).ok);
    if (args[i] == "--import-vehicles") Console.WriteLine("Imported vehicles: " + RegistryImport.Vehicles(store, File.ReadAllText(args[i + 1])).ok);
    if (args[i] == "--add-location" && i + 2 < args.Length) store.UpsertLocation(args[i + 1], args[i + 2]);
    if (args[i] == "--add-gate" && i + 2 < args.Length) store.UpsertGate(args[i + 1], args[i + 2]);
}
for (var i = 0; i + 3 < args.Length; i++)
    if (args[i] == "--add-record")
        store.AddManualRecord(args[i + 1], args[i + 2], Store.NowMs - (i * 3_600_000L), "LOC07", "G02", args[i + 3]);
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
var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
await done.Task;
