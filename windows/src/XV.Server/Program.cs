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
if (args.Contains("--setup-only")) return;
await server.StartAsync();
using var discovery = new DiscoveryResponder(settings, server.Fingerprint);
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
