using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

namespace XV.Core;

/// <summary>
/// HTTPS server the Android terminals talk to (see docs/PROTOCOL.md).
///   GET  /api/v1/ping          – public identity, no data (reachability test)
///   POST /api/v1/pair/enroll   – one-time pairing code → device id + device key (TLS pinned by the QR fingerprint)
///   POST /api/v1/rpc           – every other operation, AES-256-GCM encrypted with the device key
/// When internet access is off, connections from non-private addresses are refused.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    public const string Version = "1.0.0";
    readonly Store _store;
    readonly X509Certificate2 _cert;
    WebApplication? _app;
    public string Fingerprint { get; }
    public string? LastError { get; private set; }
    public bool Running => _app != null;
    public event Action<string>? Log;

    public ApiServer(Store store, X509Certificate2 cert)
    {
        _store = store;
        _cert = cert;
        Fingerprint = CertManager.Fingerprint(cert);
    }

    public async Task StartAsync()
    {
        var s = _store.Settings;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.AddServerHeader = false;
            k.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
            k.ListenAnyIP(s.Port, o =>
            {
                o.Protocols = HttpProtocols.Http1AndHttp2;
                o.UseHttps(new HttpsConnectionAdapterOptions
                {
                    ServerCertificate = _cert,
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                });
            });
        });
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            // Per source address: generous for terminals syncing many records, tight for pairing attempts.
            o.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                ctx.Request.Path.StartsWithSegments("/api/v1/pair")
                    ? System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("pair|" + Ip(ctx), _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) })
                    : System.Threading.RateLimiting.RateLimitPartition.GetTokenBucketLimiter("api|" + Ip(ctx), _ => new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
                        { TokenLimit = 300, TokensPerPeriod = 60, ReplenishmentPeriod = TimeSpan.FromSeconds(1), AutoReplenishment = true }));
        });
        var app = builder.Build();
        app.UseRateLimiter();

        app.Use(async (ctx, next) =>
        {
            if (!_store.Settings.InternetEnabled && !IsLocal(ctx))
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("Internet access is disabled on this Command Center");
                return;
            }
            await next();
        });

        app.MapGet("/api/v1/ping", () => Results.Json(new { app = "XVGK", version = Version, serverId = s.ServerId, serverName = s.ServerName, fingerprint = Fingerprint, serverTime = Store.NowMs }));

        app.MapPost("/api/v1/pair/enroll", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonNode.ParseAsync(ctx.Request.Body) as JsonObject ?? new();
                var res = _store.Enroll(body["code"]?.ToString() ?? "", body["deviceName"]?.ToString() ?? "", body["model"]?.ToString() ?? "", Ip(ctx));
                res["fingerprint"] = Fingerprint;
                Log?.Invoke($"Paired new terminal {res["deviceId"]} from {Ip(ctx)}");
                return Results.Json(res);
            }
            catch (StoreException ex) { return Results.Json(new { status = "rejected", reason = ex.Code, message = ex.Message }, statusCode: ex.Status); }
            catch (Exception) { return Results.Json(new { status = "error", reason = "BAD_REQUEST" }, statusCode: 400); }
        });

        app.MapPost("/api/v1/rpc", HandleRpc);

        await app.StartAsync();
        _app = app;
        Log?.Invoke($"Secure server listening on port {s.Port} (TLS, pinned certificate {Fingerprint[..16]}…)");
    }

    public async Task StopAsync()
    {
        if (_app == null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "";
    static bool IsLocal(HttpContext ctx) => NetUtil.IsPrivate(ctx.Connection.RemoteIpAddress);

    async Task HandleRpc(HttpContext ctx)
    {
        var deviceId = ctx.Request.Headers["X-GK-Device"].ToString();
        var nonce = ctx.Request.Headers["X-GK-Nonce"].ToString();
        _ = long.TryParse(ctx.Request.Headers["X-GK-Ts"].ToString(), out var ts);
        var key = _store.DeviceKey(deviceId);
        if (key == null) { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsJsonAsync(new { reason = "UNKNOWN_DEVICE" }); return; }
        if (!Envelope.InWindow(ts, nonce)) { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsJsonAsync(new { reason = "STALE_OR_REPLAYED" }); return; }

        JsonObject request;
        try
        {
            var env = await JsonNode.ParseAsync(ctx.Request.Body) as JsonObject ?? throw new FormatException();
            request = JsonNode.Parse(Envelope.Open(key, env["iv"]!.ToString(), env["ct"]!.ToString(), Envelope.RequestAad(deviceId, ts, nonce))) as JsonObject
                      ?? throw new FormatException();
        }
        catch
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { reason = "DECRYPT_FAILED" });
            return;
        }
        // Authenticated now (the AAD binds device, timestamp and nonce): reject replays.
        if (!Envelope.AcceptFresh(deviceId, ts, nonce)) { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsJsonAsync(new { reason = "STALE_OR_REPLAYED" }); return; }

        var viaInternet = !IsLocal(ctx);
        _store.Touch(deviceId, Ip(ctx), viaInternet);
        int code; JsonNode body;
        try { (code, body) = Dispatch(request, deviceId, Ip(ctx), viaInternet); }
        catch (StoreException ex) { code = ex.Status; body = new JsonObject { ["status"] = "rejected", ["reason"] = ex.Code, ["message"] = ex.Message }; }
        catch (Exception ex) { code = 500; body = new JsonObject { ["status"] = "error", ["reason"] = "SERVER_ERROR", ["message"] = "The Command Center could not process the request." }; LastError = ex.ToString(); Log?.Invoke("RPC error: " + ex.Message); }

        var plain = new JsonObject { ["code"] = code, ["body"] = body }.ToJsonString();
        var (iv, ct) = Envelope.Seal(key, plain, Envelope.ResponseAad(deviceId, nonce));
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(new JsonObject { ["iv"] = iv, ["ct"] = ct }.ToJsonString(), Encoding.UTF8);
    }

    (int, JsonNode) Dispatch(JsonObject req, string deviceId, string ip, bool viaInternet)
    {
        var op = req["op"]?.ToString() ?? "";
        var data = req["data"] as JsonObject ?? new JsonObject();
        var token = req["token"]?.ToString();
        var s = _store.Settings;

        switch (op)
        {
            case "health":
                return (200, new JsonObject { ["status"] = "ok", ["service"] = "xv-access-control", ["serverId"] = s.ServerId, ["serverName"] = s.ServerName,
                    ["version"] = Version, ["serverTime"] = Store.NowMs, ["publicUrl"] = s.PublicUrl, ["internetEnabled"] = s.InternetEnabled });
            case "auth.login":
                return (200, _store.Login(data["username"]?.ToString() ?? "", data["password"]?.ToString() ?? "", deviceId));
            case "auth.register":
            {
                var (id, _) = _store.CreateAccount(data["name"]?.ToString() ?? "", data["username"]?.ToString(), data["password"]?.ToString() ?? "", "SELF");
                var status = _store.AccountStatus(id);
                Log?.Invoke($"New operator account {id} ({status}) from {deviceId}");
                return (200, new JsonObject { ["status"] = status == "PENDING" ? "pending" : "ok", ["username"] = id });
            }
            case "stations.list":
                return (200, new JsonObject
                {
                    ["locations"] = new JsonArray(_store.Locations().Select(r => (JsonNode)new JsonObject { ["id"] = r["id"]?.ToString(), ["name"] = r["name"]?.ToString() }).ToArray()),
                    ["gates"] = new JsonArray(_store.Gates().Select(r => (JsonNode)new JsonObject { ["id"] = r["id"]?.ToString(), ["name"] = r["name"]?.ToString() }).ToArray()),
                    ["reasons"] = new JsonArray(s.MovementReasons.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
                });
            case "comms.info":
                // Where this terminal reaches the separate Comms engine (messages, alerts, calls).
                return (200, new JsonObject { ["port"] = s.CommsPort, ["publicUrl"] = s.CommsPublicUrl, ["publicUsesCaCertificate"] = s.CommsCloudUrl.Trim().Length > 0 && s.CloudUsesPublicCertificate });
            case "auth.logout":
                if (!string.IsNullOrEmpty(token)) _store.Logout(token);
                return (200, new JsonObject { ["status"] = "ok" });
        }

        var operatorId = _store.OperatorFor(token, deviceId);
        if (operatorId == null) return (401, new JsonObject { ["status"] = "error", ["reason"] = "OPERATOR_AUTH_REQUIRED" });

        switch (op)
        {
            case "master.bootstrap": return (200, _store.Bootstrap());
            case "credential.verify": return (200, _store.Verify(data["code"]?.ToString() ?? "", (data["expected"]?.ToString() ?? "").ToUpperInvariant(), operatorId));
            case "heartbeat": _store.Heartbeat(deviceId, data, ip, viaInternet); return (200, new JsonObject { ["status"] = "ok", ["deviceId"] = deviceId });
            case "events.create": { var r = _store.PersonEvent(data, deviceId, operatorId); return (r["status"]!.ToString() == "duplicate" ? 200 : 201, r); }
            case "vehicle.transaction": { var r = _store.VehicleTransaction(data, deviceId, operatorId); return (r["status"]!.ToString() == "duplicate" ? 200 : 201, r); }
            default: return (404, new JsonObject { ["status"] = "error", ["reason"] = "UNKNOWN_OPERATION" });
        }
    }
}

/// <summary>Answers LAN broadcast probes so terminals can find this PC after its IP address changes.</summary>
public sealed class DiscoveryResponder : IDisposable
{
    readonly UdpClient _udp;
    readonly CancellationTokenSource _cts = new();

    public DiscoveryResponder(Settings settings, string fingerprint)
    {
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, settings.DiscoveryPort));
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var r = await _udp.ReceiveAsync(_cts.Token);
                    // LAN discovery only: never answer internet addresses (no reflection / amplification, no disclosure).
                    if (!NetUtil.IsPrivate(r.RemoteEndPoint.Address) || r.Buffer.Length > 64) continue;
                    if (Encoding.UTF8.GetString(r.Buffer).Trim() != "XVGK_DISCOVER_V1") continue;
                    var reply = JsonSerializer.SerializeToUtf8Bytes(new { app = "XVGK", serverId = settings.ServerId, serverName = settings.ServerName, port = settings.Port, fingerprint });
                    await _udp.SendAsync(reply, r.RemoteEndPoint, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(200); }
            }
        });
    }

    public void Dispose() { _cts.Cancel(); _udp.Dispose(); }
}
