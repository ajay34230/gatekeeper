using System.IO;
using System.Media;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// Call engine (voice / video) between the Command Center and one terminal at a time.
/// Signalling travels as {"t":"call", "op", "callId", …} frames over the encrypted Comms engine;
/// media is direct peer-to-peer WebRTC (DTLS-SRTP) handled by the shared call page in WebView2.
/// </summary>
public static class Calls
{
    const int RingSeconds = 45;
    static Active? _call;
    /// <summary>Raised when a call starts or ends (the Comms Center refreshes its call buttons).</summary>
    public static event Action? StateChanged;
    static Task<CoreWebView2Environment>? _env;

    sealed class Active
    {
        public required string Id, Device, Direction, Peer;
        public required bool Video;
        public CallWindow? Window;
        public IncomingCallWindow? Ringing;
        public DispatcherTimer? Timeout;
        public bool Answered;
    }

    public static void Init()
    {
        App.Comms.Signal += (dev, f) => Application.Current.Dispatcher.BeginInvoke(() => OnSignal(dev, f));
        App.ExtendCommsHeader = (panel, dev, online) =>
        {
            var voice = Btn("Voice call", (_, _) => Start(dev, false), "BtnEmerald");
            var video = Btn("Video call", (_, _) => Start(dev, true), "BtnBlue");
            voice.IsEnabled = video.IsEnabled = online && _call == null;
            voice.ToolTip = video.ToolTip = online ? "Direct encrypted call (same network or VPN)" : "The terminal is offline";
            panel.Children.Add(voice); panel.Children.Add(video.M(8));
        };
    }

    static string PeerName(string dev)
    {
        var d = App.Store.Devices().FirstOrDefault(x => S(x["device_id"]) == dev);
        if (d == null) return dev;
        var op = S(d["operator_id"]);
        var name = S(d["name"]).Length > 0 ? S(d["name"]) : dev;
        return op.Length > 0 ? $"{op} • {name}" : name;
    }

    static void Send(string dev, string op, string callId, JsonObject? extra = null)
    {
        var f = new JsonObject { ["t"] = "call", ["op"] = op, ["callId"] = callId };
        if (extra != null) foreach (var kv in extra) f[kv.Key] = kv.Value?.DeepClone();
        App.Comms.SendSignal(dev, f);
    }

    public static void Start(string dev, bool video)
    {
        if (_call != null) { MessageBox.Show("A call is already in progress.", "Call"); return; }
        if (!App.Comms.IsOnline(dev)) { MessageBox.Show("The terminal is offline. Calls need the terminal connected to the Comms engine.", "Call"); return; }
        var c = new Active { Id = Guid.NewGuid().ToString("N"), Device = dev, Direction = "OUT", Video = video, Peer = PeerName(dev) };
        _call = c;
        StateChanged?.Invoke();
        App.Comms.Store.LogCall(c.Id, dev, "OUT", video, Store.NowMs);
        c.Window = new CallWindow(c.Peer, "caller", video, OnPage, () => Hangup("Call ended", notify: true));
        c.Window.Show();
        Send(dev, "invite", c.Id, new JsonObject { ["video"] = video, ["from"] = App.Settings.ServerName });
        c.Timeout = Timer(RingSeconds, () => { if (_call == c && !c.Answered) { Send(dev, "cancel", c.Id); Finish("No answer", "NO_ANSWER"); } });
    }

    static DispatcherTimer Timer(int seconds, Action act)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        t.Tick += (_, _) => { t.Stop(); act(); };
        t.Start();
        return t;
    }

    static void OnSignal(string dev, JsonObject f)
    {
        var op = f["op"]?.ToString() ?? "";
        var id = f["callId"]?.ToString() ?? "";
        if (op == "invite")
        {
            if (_call != null) { Send(dev, "busy", id); return; }
            var video = f["video"]?.GetValue<bool>() == true;
            var c = new Active { Id = id, Device = dev, Direction = "IN", Video = video, Peer = PeerName(dev) };
            _call = c;
            StateChanged?.Invoke();
            App.Comms.Store.LogCall(id, dev, "IN", video, Store.NowMs);
            c.Ringing = new IncomingCallWindow(c.Peer, video, accept: () => Accept(c), decline: () => { Send(dev, "decline", id); Finish(null, "DECLINED"); });
            c.Ringing.Show();
            c.Timeout = Timer(RingSeconds, () => { if (_call == c && !c.Answered) { Send(dev, "decline", id); Finish(null, "MISSED"); } });
            return;
        }
        var call = _call;
        if (call == null || call.Id != id || call.Device != dev) return;
        switch (op)
        {
            case "accept":
                call.Answered = true; call.Timeout?.Stop();
                App.Comms.Store.CallAnswered(id);
                call.Window?.Post(new JsonObject { ["t"] = "accepted" });
                break;
            case "decline": Finish("Call declined", "DECLINED"); break;
            case "busy": Finish("The terminal is busy on another call", "BUSY"); break;
            case "cancel": Finish(null, "MISSED"); break;
            case "hangup": Finish("Call ended by the terminal", "COMPLETED"); break;
            case "offer": case "answer": case "ice":
                var payload = f.DeepClone().AsObject();
                payload.Remove("t"); payload.Remove("callId");
                call.Window?.Post(new JsonObject { ["t"] = "signal", ["payload"] = payload });
                break;
        }
    }

    static void Accept(Active c)
    {
        c.Answered = true; c.Timeout?.Stop();
        c.Ringing?.Close(); c.Ringing = null;
        App.Comms.Store.CallAnswered(c.Id);
        c.Window = new CallWindow(c.Peer, "callee", c.Video, OnPage, () => Hangup("Call ended", notify: true));
        c.Window.Show();
    }

    /// <summary>Messages from the call page.</summary>
    static void OnPage(JsonObject m)
    {
        var c = _call;
        if (c == null) return;
        switch (m["t"]?.ToString())
        {
            case "ready":
                if (c.Direction == "IN") Send(c.Device, "accept", c.Id);
                break;
            case "signal":
                if (m["payload"] is JsonObject p) { var op = p["op"]?.ToString() ?? ""; p.Remove("op"); Send(c.Device, op, c.Id, p); }
                break;
            case "hangup":
                Hangup("Call ended", notify: true);
                break;
            case "state":
                if (m["state"]?.ToString() == "ended") Finish(null, c.Answered ? "COMPLETED" : "FAILED", closeDelayMs: 2500);
                break;
        }
    }

    static void Hangup(string reason, bool notify)
    {
        var c = _call;
        if (c == null) return;
        if (notify) Send(c.Device, c.Answered ? "hangup" : "cancel", c.Id);
        Finish(reason, c.Answered ? "COMPLETED" : "CANCELLED");
    }

    static void Finish(string? message, string outcome, int closeDelayMs = 1800)
    {
        var c = _call;
        if (c == null) return;
        _call = null;
        StateChanged?.Invoke();
        c.Timeout?.Stop();
        c.Ringing?.Close();
        App.Comms.Store.CallEnded(c.Id, outcome);
        App.Store.AdminAudit("CALL_" + outcome, $"{(c.Video ? "video" : "voice")} call {(c.Direction == "OUT" ? "to" : "from")} {c.Device}");
        if (c.Window != null)
        {
            var w = c.Window;
            w.Post(new JsonObject { ["t"] = "end", ["reason"] = message ?? "Call ended" });
            Timer(Math.Max(1, closeDelayMs / 1000), () => w.CloseQuietly());
        }
        else if (message != null) MessageBox.Show(message, "Call");
    }

    public static Task<CoreWebView2Environment> Environment() => _env ??= CoreWebView2Environment.CreateAsync(null, Paths.File("webview2"),
        // No background traffic from the embedded browser: it is used only for the local call page.
        new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update --disable-sync --disable-domain-reliability --no-first-run"));

    // ------------------------------------------------------------------ windows

    public sealed class CallWindow : Window
    {
        readonly WebView2 _web = new();
        readonly Queue<string> _outbox = new();
        readonly Action<JsonObject> _onPage;
        readonly Action _onUserClose;
        bool _ready, _quiet;

        public CallWindow(string peer, string role, bool video, Action<JsonObject> onPage, Action onUserClose)
        {
            _onPage = onPage; _onUserClose = onUserClose;
            Title = (video ? "Video call — " : "Voice call — ") + peer;
            Width = video ? 960 : 440; Height = video ? 640 : 560; Background = B("#09090B");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
            // The call page (call.html) has its own in-page hang-up button, but it depends on WebRTC/WebView2
            // having loaded successfully. This native bar is a guaranteed fallback so there is always a visible,
            // working way to end the call even if the page fails to render.
            var endBtn = new Button
            {
                Content = "End Call", Style = (Style)Application.Current.Resources["BtnDanger"], Padding = new Thickness(22, 10, 22, 10),
                FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
            };
            endBtn.Click += (_, _) => { if (!_quiet) _onUserClose(); CloseQuietly(); };
            var bar = new Border { Background = B("#18181B"), Padding = new Thickness(0, 10, 0, 10), Child = endBtn };
            var dock = new DockPanel();
            DockPanel.SetDock(bar, Dock.Bottom); dock.Children.Add(bar); dock.Children.Add(_web);
            Content = dock;
            Closing += (_, _) => { if (!_quiet) _onUserClose(); };
            Loaded += async (_, _) =>
            {
                try
                {
                    await _web.EnsureCoreWebView2Async(await Environment());
                    var core = _web.CoreWebView2;
                    core.Settings.AreDevToolsEnabled = false;
                    core.Settings.AreDefaultContextMenusEnabled = false;
                    core.Settings.IsStatusBarEnabled = false;
                    core.SetVirtualHostNameToFolderMapping("xv-call.local", Path.Combine(AppContext.BaseDirectory, "call"), CoreWebView2HostResourceAccessKind.Deny);
                    core.PermissionRequested += (_, e) => e.State =
                        e.PermissionKind is CoreWebView2PermissionKind.Camera or CoreWebView2PermissionKind.Microphone ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
                    core.NewWindowRequested += (_, e) => e.Handled = true;
                    core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://xv-call.local/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
                    core.WebMessageReceived += (_, e) =>
                    {
                        try { if (JsonNode.Parse(e.TryGetWebMessageAsString()) is JsonObject m) _onPage(m); } catch (JsonException) { }
                    };
                    core.NavigationCompleted += async (_, _) =>
                    {
                        var start = new JsonObject { ["role"] = role, ["video"] = video, ["peer"] = peer, ["mobile"] = false };
                        await core.ExecuteScriptAsync($"XVCall.start({start.ToJsonString()})");
                        _ready = true;
                        while (_outbox.Count > 0) core.PostWebMessageAsJson(_outbox.Dequeue());
                    };
                    core.Navigate("https://xv-call.local/call.html");
                }
                catch (WebView2RuntimeNotFoundException)
                {
                    MessageBox.Show("Calls need the Microsoft Edge WebView2 Runtime, which is part of Windows 11 and current Windows 10. Install it from Microsoft, then try again.", "Call");
                    Close();
                }
            };
        }

        /// <summary>Sends a message to the call page (queued until the page has started).</summary>
        public void Post(JsonObject m)
        {
            var json = m.ToJsonString();
            if (_ready && _web.CoreWebView2 != null) _web.CoreWebView2.PostWebMessageAsJson(json);
            else _outbox.Enqueue(json);
        }

        public void CloseQuietly() { _quiet = true; try { Close(); } catch (InvalidOperationException) { } }
    }

    public sealed class IncomingCallWindow : Window
    {
        readonly SoundPlayer? _ring;
        bool _handled;

        public IncomingCallWindow(string peer, bool video, Action accept, Action decline)
        {
            Title = "Incoming call"; Width = 420; SizeToContent = SizeToContent.Height; Topmost = true; ResizeMode = ResizeMode.NoResize;
            Background = B("#FFFFFF"); WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
            var acceptBtn = Btn(video ? "Accept video" : "Accept", (_, _) => { _handled = true; accept(); }, "BtnEmerald");
            var declineBtn = Btn("Decline", (_, _) => { _handled = true; decline(); Close(); }, "BtnDanger");
            acceptBtn.Padding = declineBtn.Padding = new Thickness(18, 10, 18, 10);
            Content = new Border
            {
                Padding = new Thickness(24), BorderBrush = B("#059669"), BorderThickness = new Thickness(0, 4, 0, 0),
                Child = Col(T(video ? "INCOMING VIDEO CALL" : "INCOMING VOICE CALL", 11, "#047857", bold: true), T(peer, 18, "#0F172A", bold: true).M(0, 8),
                    T("End-to-end encrypted • direct connection", 11, "#64748B").M(0, 6), Row(acceptBtn, declineBtn.M(10)).M(0, 18)),
            };
            try
            {
                var wav = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows), "Media", "Ring01.wav");
                if (File.Exists(wav)) { _ring = new SoundPlayer(wav); _ring.PlayLooping(); } else SystemSounds.Asterisk.Play();
            }
            catch { /* no audio device */ }
            Closed += (_, _) => { _ring?.Stop(); if (!_handled) { _handled = true; decline(); } };
        }
    }
}
