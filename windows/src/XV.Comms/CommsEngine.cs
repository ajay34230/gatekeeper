using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using XV.Core;

namespace XV.Comms;

/// <summary>
/// Comms engine: live messages, alerts and call signalling between the Command Center and paired terminals.
/// Runs beside the gate server but is independent of it — its own TLS listener (port 8444), its own encrypted
/// database and its own key per terminal (derived from the terminal's device key), so the gate data path is never
/// touched by communications traffic. See docs/PROTOCOL.md → "Comms engine".
///
///   wss://host:8444/comms/v1/ws?d={deviceId}&amp;t={ts}&amp;n={nonce}&amp;m={hmac}
///   commsKey = HMAC-SHA256(deviceKey, "XV-COMMS-1");  m = hex HMAC-SHA256(commsKey, "XVCM1|hello|d|t|n")
///   every frame = {"iv","ct"} AES-256-GCM with AAD "XVCM1|c2s|d|n|seq" (phone→PC) or "XVCM1|s2c|d|n|seq" (PC→phone)
/// </summary>
public sealed class CommsEngine : IAsyncDisposable
{
    public const int MaxBody = 2000;
    readonly Settings _settings;
    readonly X509Certificate2 _cert;
    readonly Func<string, byte[]?> _deviceKey;
    readonly ConcurrentDictionary<string, Link> _links = new();
    static readonly ConcurrentDictionary<string, long> Nonces = new();
    WebApplication? _app;

    public CommsStore Store { get; }
    public string? LastError { get; private set; }
    public bool Running => _app != null;

    /// <summary>A message or alert arrived from a terminal (raised on a background thread).</summary>
    public event Action<CommsMessage>? MessageReceived;
    /// <summary>Delivery / read state or terminal presence changed (raised on a background thread).</summary>
    public event Action? Changed;
    /// <summary>Call signalling frame from a terminal: (deviceId, frame). Used by the call engine.</summary>
    public event Action<string, JsonObject>? Signal;
    public event Action<string>? Log;

    public CommsEngine(Settings settings, X509Certificate2 cert, Func<string, byte[]?> deviceKey, CommsStore? store = null)
    {
        _settings = settings; _cert = cert; _deviceKey = deviceKey;
        Store = store ?? new CommsStore();
    }

    public static byte[] CommsKey(byte[] deviceKey) => HMACSHA256.HashData(deviceKey, Encoding.UTF8.GetBytes("XV-COMMS-1"));

    public bool IsOnline(string deviceId) => _links.ContainsKey(deviceId);
    public IReadOnlyCollection<string> OnlineDevices => _links.Keys.ToList();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.AddServerHeader = false;
            k.ListenAnyIP(_settings.CommsPort, o =>
            {
                o.Protocols = HttpProtocols.Http1;
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
            o.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "", _ =>
                    new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
        });
        var app = builder.Build();
        app.UseRateLimiter();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.Use(async (ctx, next) =>
        {
            if (!_settings.InternetEnabled && !NetUtil.IsPrivate(ctx.Connection.RemoteIpAddress)) { ctx.Response.StatusCode = 403; return; }
            await next();
        });
        app.MapGet("/comms/v1/ping", () => Results.Json(new { app = "XVCM", serverId = _settings.ServerId, serverTime = Core.Store.NowMs }));
        app.Map("/comms/v1/ws", Accept);
        try
        {
            await app.StartAsync();
            _app = app;
            LastError = null;
            Log?.Invoke($"Comms engine listening on port {_settings.CommsPort}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            await app.DisposeAsync();
            Log?.Invoke("Comms engine could not start: " + ex.Message);
        }
    }

    public async Task StopAsync()
    {
        foreach (var l in _links.Values) l.Abort();
        _links.Clear();
        if (_app == null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    public async ValueTask DisposeAsync() { await StopAsync(); Store.Dispose(); }

    // ------------------------------------------------------------------ sending

    /// <summary>Queues a message or alert for a terminal; delivered now if it is connected, otherwise when it next connects.</summary>
    public CommsMessage Send(string deviceId, string kind, string body, string sender)
    {
        body = (body ?? "").Trim();
        if (body.Length == 0) throw new ArgumentException("Message is empty.");
        if (body.Length > MaxBody) throw new ArgumentException($"Messages are limited to {MaxBody} characters.");
        var m = new CommsMessage(Guid.NewGuid().ToString("N"), deviceId, "OUT", kind == "ALERT" ? "ALERT" : "MESSAGE", body, sender, Core.Store.NowMs, 0, 0);
        Store.Insert(m);
        if (_links.TryGetValue(deviceId, out var link)) _ = link.SendAsync(MessageFrame(m));
        Changed?.Invoke();
        return m;
    }

    /// <summary>Marks every message from this terminal as read and tells the terminal.</summary>
    public void MarkRead(string deviceId)
    {
        var ids = Store.MarkIncomingRead(deviceId);
        if (ids.Count > 0 && _links.TryGetValue(deviceId, out var link))
            foreach (var id in ids) _ = link.SendAsync(new JsonObject { ["t"] = "ack", ["id"] = id, ["state"] = "READ" });
        if (ids.Count > 0) Changed?.Invoke();
    }

    /// <summary>Sends a call-signalling frame to a terminal. Returns false when it is not connected.</summary>
    public bool SendSignal(string deviceId, JsonObject frame)
    {
        if (!_links.TryGetValue(deviceId, out var link)) return false;
        _ = link.SendAsync(frame);
        return true;
    }

    /// <summary>Drops a terminal's live connection at once (used when it is revoked or removed; it cannot reconnect without a valid key).</summary>
    public void Disconnect(string deviceId)
    {
        if (_links.TryRemove(deviceId, out var link)) { link.Abort(); Changed?.Invoke(); }
    }

    static JsonObject MessageFrame(CommsMessage m) => new()
    {
        ["t"] = "msg", ["id"] = m.Id, ["kind"] = m.Kind, ["body"] = m.Body, ["sender"] = m.Sender, ["ts"] = m.CreatedAt,
    };

    // ------------------------------------------------------------------ connections

    async Task Accept(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
        var q = ctx.Request.Query;
        string dev = q["d"].ToString(), nonce = q["n"].ToString(), mac = q["m"].ToString();
        _ = long.TryParse(q["t"].ToString(), out var ts);
        var dk = _deviceKey(dev);
        if (dk == null) { ctx.Response.StatusCode = 401; return; }
        var key = CommsKey(dk);
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"XVCM1|hello|{dev}|{ts}|{nonce}"));
        byte[] given;
        try { given = Convert.FromHexString(mac); } catch { ctx.Response.StatusCode = 401; return; }
        if (!CryptographicOperations.FixedTimeEquals(expected, given) || !Fresh(dev, ts, nonce)) { ctx.Response.StatusCode = 401; return; }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var link = new Link(ws, key, dev, nonce);
        if (_links.TryGetValue(dev, out var old)) old.Abort();
        _links[dev] = link;
        Log?.Invoke($"Comms: {dev} connected from {ctx.Connection.RemoteIpAddress?.MapToIPv4()}");
        Changed?.Invoke();
        try
        {
            await link.SendAsync(new JsonObject { ["t"] = "hello", ["serverName"] = _settings.ServerName, ["serverTime"] = Core.Store.NowMs });
            foreach (var m in Store.Undelivered(dev)) await link.SendAsync(MessageFrame(m));
            while (ws.State == WebSocketState.Open)
            {
                var frame = await link.ReceiveAsync(ctx.RequestAborted);
                if (frame == null) break;
                if (_deviceKey(dev) == null) break; // revoked while connected
                await Handle(link, frame);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or CryptographicException or System.Text.Json.JsonException or InvalidDataException)
        {
            Log?.Invoke($"Comms: {dev} disconnected ({ex.GetType().Name})");
        }
        finally
        {
            _links.TryRemove(new KeyValuePair<string, Link>(dev, link));
            Changed?.Invoke();
        }
    }

    async Task Handle(Link link, JsonObject f)
    {
        switch (f["t"]?.ToString())
        {
            case "msg":
                if (!link.Allow(f["kind"]?.ToString() == "ALERT")) return; // flood protection; the terminal re-sends later
                var id = f["id"]?.ToString() ?? "";
                var body = (f["body"]?.ToString() ?? "").Trim();
                if (id.Length is < 8 or > 64 || body.Length == 0 || body.Length > MaxBody) return;
                var m = new CommsMessage(id, link.DeviceId, "IN", f["kind"]?.ToString() == "ALERT" ? "ALERT" : "MESSAGE", body,
                    (f["sender"]?.ToString() ?? "").Trim() is var s && s.Length > 0 ? s[..Math.Min(s.Length, 80)] : link.DeviceId, Core.Store.NowMs, Core.Store.NowMs, 0);
                var isNew = Store.Insert(m);
                await link.SendAsync(new JsonObject { ["t"] = "ack", ["id"] = id, ["state"] = "DELIVERED" });
                if (isNew) { MessageReceived?.Invoke(m); Changed?.Invoke(); }
                break;
            case "ack":
                if (Store.Ack(link.DeviceId, f["id"]?.ToString() ?? "", f["state"]?.ToString() == "READ")) Changed?.Invoke();
                break;
            case "ping":
                await link.SendAsync(new JsonObject { ["t"] = "pong" });
                break;
            case "call":
                if (!link.AllowSignal()) return;
                Signal?.Invoke(link.DeviceId, f);
                break;
        }
    }

    static bool Fresh(string dev, long ts, string nonce)
    {
        var now = Core.Store.NowMs;
        if (Math.Abs(now - ts) > Envelope.MaxSkewMs || nonce.Length is < 16 or > 64) return false;
        if (Nonces.Count > 20_000) foreach (var kv in Nonces) if (now - kv.Value > 2 * Envelope.MaxSkewMs) Nonces.TryRemove(kv.Key, out _);
        return Nonces.TryAdd(dev + "|" + nonce, now);
    }

    /// <summary>One authenticated terminal connection with its per-direction frame counters.</summary>
    sealed class Link(WebSocket ws, byte[] key, string deviceId, string nonce)
    {
        readonly SemaphoreSlim _send = new(1, 1);
        long _outSeq, _inSeq;
        readonly Queue<long> _recent = new(), _recentAlerts = new();
        public string DeviceId => deviceId;

        readonly Queue<long> _recentSignals = new();
        /// <summary>Call signalling (offer / answer / ICE): at most 400 frames per minute.</summary>
        public bool AllowSignal()
        {
            var now = Environment.TickCount64;
            while (_recentSignals.Count > 0 && now - _recentSignals.Peek() > 60_000) _recentSignals.Dequeue();
            if (_recentSignals.Count >= 400) return false;
            _recentSignals.Enqueue(now); return true;
        }

        /// <summary>At most 60 messages and 10 alerts per terminal per minute.</summary>
        public bool Allow(bool alert)
        {
            var now = Environment.TickCount64;
            static bool Take(Queue<long> q, int max, long now)
            {
                while (q.Count > 0 && now - q.Peek() > 60_000) q.Dequeue();
                if (q.Count >= max) return false;
                q.Enqueue(now); return true;
            }
            return Take(_recent, 60, now) && (!alert || Take(_recentAlerts, 10, now));
        }

        public async Task SendAsync(JsonObject frame)
        {
            await _send.WaitAsync();
            try
            {
                if (ws.State != WebSocketState.Open) return;
                var seq = ++_outSeq;
                var (iv, ct) = Envelope.Seal(key, frame.ToJsonString(), $"XVCM1|s2c|{deviceId}|{nonce}|{seq}");
                var bytes = Encoding.UTF8.GetBytes(new JsonObject { ["iv"] = iv, ["ct"] = ct }.ToJsonString());
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException) { }
            finally { _send.Release(); }
        }

        public async Task<JsonObject?> ReceiveAsync(CancellationToken ct)
        {
            var buf = new byte[16 * 1024];
            using var ms = new MemoryStream();
            while (true)
            {
                var r = await ws.ReceiveAsync(buf, ct);
                if (r.MessageType == WebSocketMessageType.Close) return null;
                ms.Write(buf, 0, r.Count);
                if (ms.Length > 256 * 1024) throw new InvalidDataException("Frame too large");
                if (r.EndOfMessage) break;
            }
            var env = JsonNode.Parse(ms.ToArray()) as JsonObject ?? throw new InvalidDataException("Bad frame");
            var seq = ++_inSeq;
            var plain = Envelope.Open(key, env["iv"]?.ToString() ?? "", env["ct"]?.ToString() ?? "", $"XVCM1|c2s|{deviceId}|{nonce}|{seq}");
            return JsonNode.Parse(plain) as JsonObject ?? throw new InvalidDataException("Bad frame");
        }

        public void Abort() { try { ws.Abort(); } catch { } }
    }
}
