using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XV.Comms;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// Comms Center: messages and alerts with paired terminals, served by the separate Comms engine.
/// One window, kept open alongside the dashboard.
/// </summary>
public sealed class CommsWindow : Window
{
    static CommsWindow? _open;
    readonly StackPanel _devices = new();
    readonly StackPanel _thread = new();
    readonly ScrollViewer _threadScroll;
    readonly TextBlock _title = T("Select a terminal", 15, "#F4F4F5", bold: true);
    readonly TextBlock _subtitle = T("", 11, "#A1A1AA");
    readonly TextBlock _engineStatus = new() { FontSize = 10.5, Foreground = B("#71717A"), FontFamily = Mono, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    readonly TextBox _input = new() { AcceptsReturn = false, MaxLength = CommsEngine.MaxBody, MinHeight = 38, VerticalContentAlignment = VerticalAlignment.Center };
    readonly StackPanel _composer;
    readonly StackPanel _headerActions = new() { Orientation = Orientation.Horizontal };
    string? _device;

    public static void Show(Window owner, string? deviceId = null)
    {
        if (_open == null) { _open = new CommsWindow { Owner = owner }; _open.Closed += (_, _) => _open = null; _open.Show(); }
        else { if (_open.WindowState == WindowState.Minimized) _open.WindowState = WindowState.Normal; _open.Activate(); }
        if (deviceId != null) _open.Select(deviceId);
    }

    internal CommsWindow()
    {
        Title = "Comms Center — Messages & Alerts"; Width = 1040; Height = Math.Min(720, SystemParameters.WorkArea.Height - 20);
        Background = B("#101012"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        // left: terminals
        _engineStatus.Text = App.CommsStatus;
        var leftHead = Col(T("COMMS ENGINE", 10, "#A1A1AA", bold: true), T("Paired terminals", 15, "#F4F4F5", bold: true).M(0, 2), _engineStatus);
        var broadcast = Btn("Broadcast alert to all terminals…", (_, _) => Broadcast(), "BtnDanger");
        broadcast.HorizontalAlignment = HorizontalAlignment.Stretch; broadcast.Margin = new Thickness(0, 10, 0, 0);
        leftHead.Children.Add(broadcast);
        var left = new DockPanel();
        var lh = new Border { Padding = new Thickness(16, 14, 16, 12), BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 0, 0, 1), Child = leftHead };
        DockPanel.SetDock(lh, Dock.Top); left.Children.Add(lh);
        left.Children.Add(new ScrollViewer { Content = _devices, Padding = new Thickness(8) });
        var leftBorder = new Border { BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 0, 1, 0), Background = B("#0C0C0E"), Child = left };
        grid.Children.Add(leftBorder);

        // right: conversation
        var right = new DockPanel();
        var head = new Border { Padding = new Thickness(18, 14, 18, 12), BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 0, 0, 1), Child = Spread(Col(_title, _subtitle.M(0, 3)), _headerActions) };
        DockPanel.SetDock(head, Dock.Top); right.Children.Add(head);
        var send = Btn("Send", (_, _) => SendNow("MESSAGE"), "BtnAmber"); send.MinWidth = 80; send.Padding = new Thickness(14, 8, 14, 8);
        var alert = Btn("Send ALERT", (_, _) => SendNow("ALERT"), "BtnDanger"); alert.Padding = new Thickness(14, 8, 14, 8);
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(send, 1); Grid.SetColumn(alert, 2); send.Margin = new Thickness(8, 0, 0, 0); alert.Margin = new Thickness(8, 0, 0, 0);
        g.Children.Add(_input); g.Children.Add(send); g.Children.Add(alert);
        _composer = Col(g, T("Enter sends a message. Alerts ring on the terminal even when the app is in the background.", 10.5, "#71717A").M(0, 6));
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { SendNow("MESSAGE"); e.Handled = true; } };
        var foot = new Border { Padding = new Thickness(18, 12, 18, 12), BorderBrush = B("#27272A"), BorderThickness = new Thickness(0, 1, 0, 0), Child = _composer };
        DockPanel.SetDock(foot, Dock.Bottom); right.Children.Add(foot);
        _threadScroll = new ScrollViewer { Content = _thread, Padding = new Thickness(18, 14, 18, 14) };
        right.Children.Add(_threadScroll);
        Grid.SetColumn(right, 1); grid.Children.Add(right);
        Content = grid;

        _composer.IsEnabled = false;
        _thread.Children.Add(Empty("Choose a terminal on the left to see its messages."));
        App.Comms.Changed += OnChanged;
        App.Comms.MessageReceived += OnMessage;
        Calls.StateChanged += OnCallState;
        Closed += (_, _) => { App.Comms.Changed -= OnChanged; App.Comms.MessageReceived -= OnMessage; Calls.StateChanged -= OnCallState; };
        FillDevices();
    }

    void OnCallState() => Dispatcher.BeginInvoke(() => { if (_device != null) FillThread(); });
    void OnChanged() => Dispatcher.BeginInvoke(() => { _engineStatus.Text = App.CommsStatus; FillDevices(); if (_device != null) FillThread(); });
    void OnMessage(CommsMessage m) => Dispatcher.BeginInvoke(() => { if (m.DeviceId == _device && IsActive) App.Comms.MarkRead(m.DeviceId); });

    void FillDevices()
    {
        _devices.Children.Clear();
        var unread = App.Comms.Store.UnreadCounts();
        var devices = App.Store.Devices().Where(d => L(d["active"]) == 1).ToList();
        if (devices.Count == 0) { _devices.Children.Add(Empty("No paired terminals yet. Pair a phone from 'Local Wi-Fi & Pair Device'.")); return; }
        foreach (var d in devices)
        {
            var id = S(d["device_id"]);
            var online = App.Comms.IsOnline(id);
            var n = unread.GetValueOrDefault(id);
            var name = S(d["name"]).Length > 0 ? S(d["name"]) : id;
            var info = Col(T(name, 12.5, "#F4F4F5", bold: true),
                T($"{id}  •  {(S(d["operator_id"]).Length > 0 ? S(d["operator_id"]) : "no operator")}", 10.5, "#A1A1AA", mono: true).M(0, 2),
                T(S(d["location_id"]).Length > 0 ? $"{S(d["location_id"])} / {S(d["gate_id"])}" : "post not set", 10.5, "#71717A", mono: true).M(0, 1));
            var right = Row(Pill(online ? "● ONLINE" : "○ OFFLINE", online ? "#6EE7B7" : "#A1A1AA", online ? "#062F23" : "#18181B", online ? "#047857" : "#3F3F46", 9));
            if (n > 0) right.Children.Add(Pill(n.ToString(), "#18181B", "#FBBF24", "#F59E0B", 10).M(6));
            var card = new Border
            {
                Background = B(id == _device ? "#1F1F23" : "#141417"), BorderBrush = B(id == _device ? "#F59E0B" : "#27272A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand,
                Child = Spread(info, right),
            };
            card.MouseLeftButtonUp += (_, _) => Select(id);
            _devices.Children.Add(card);
        }
    }

    public void Select(string deviceId)
    {
        _device = deviceId;
        _composer.IsEnabled = true;
        App.Comms.MarkRead(deviceId);
        FillDevices();
        FillThread();
        _input.Focus();
    }

    void FillThread()
    {
        if (_device == null) return;
        var dev = App.Store.Devices().FirstOrDefault(d => S(d["device_id"]) == _device);
        var online = App.Comms.IsOnline(_device);
        _title.Text = dev == null ? _device : (S(dev["name"]).Length > 0 ? S(dev["name"]) : _device);
        _subtitle.Text = $"{_device}  •  {(online ? "online now — messages arrive instantly" : "offline — messages are queued and delivered when it reconnects")}";
        _headerActions.Children.Clear();
        App.ExtendCommsHeader?.Invoke(_headerActions, _device, online);

        var atBottom = _threadScroll.VerticalOffset >= _threadScroll.ScrollableHeight - 4;
        _thread.Children.Clear();
        var msgs = App.Comms.Store.Conversation(_device);
        var calls = App.Comms.Store.Calls(_device, 200);
        // One timeline: messages and calls in time order.
        var items = msgs.Select(m => (ts: m.CreatedAt, el: (Func<FrameworkElement>)(() => Bubble(m))))
            .Concat(calls.Select(c => (ts: L(c["started_at"]), el: (Func<FrameworkElement>)(() => CallLine(c)))))
            .OrderBy(x => x.ts).ToList();
        if (items.Count == 0) _thread.Children.Add(Empty("No messages or calls yet."));
        string? day = null;
        foreach (var (ts, el) in items)
        {
            var d = Time(ts, "dddd, dd MMM yyyy");
            if (d != day) { day = d; var sep = T(d, 10.5, "#71717A", bold: true).M(0, 10, 0, 6); sep.HorizontalAlignment = HorizontalAlignment.Center; _thread.Children.Add(sep); }
            _thread.Children.Add(el());
        }
        if (atBottom || items.Count > 0) _threadScroll.ScrollToEnd();
    }

    static string CallLength(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    static FrameworkElement CallLine(Dictionary<string, object> c)
    {
        var video = L(c["video"]) == 1;
        var outgoing = S(c["direction"]) == "OUT";
        var outcome = S(c["outcome"]);
        long answered = L(c["answered_at"]), ended = L(c["ended_at"]);
        var kind = video ? "video call" : "voice call";
        var text = outcome switch
        {
            "COMPLETED" when answered > 0 && ended > 0 => $"{(outgoing ? "Outgoing" : "Incoming")} {kind} • {CallLength(ended - answered)}",
            "MISSED" => $"Missed {kind}",
            "NO_ANSWER" => $"Outgoing {kind} • no answer",
            "DECLINED" => outgoing ? $"Outgoing {kind} • declined" : $"Declined {kind}",
            "BUSY" => $"Outgoing {kind} • terminal busy",
            "CANCELLED" => $"{(outgoing ? "Outgoing" : "Incoming")} {kind} • cancelled",
            "FAILED" => $"{(outgoing ? "Outgoing" : "Incoming")} {kind} • could not connect",
            "" => $"{(outgoing ? "Outgoing" : "Incoming")} {kind} • in progress",
            _ => $"{(outgoing ? "Outgoing" : "Incoming")} {kind}",
        };
        var missed = outcome is "MISSED" or "FAILED";
        var line = T($"{(video ? "🎥" : "📞")}  {text}  •  {Time(L(c["started_at"]), "HH:mm")}", 11, missed ? "#FB7185" : "#A1A1AA");
        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(12, 5, 12, 5),
            CornerRadius = new CornerRadius(12), Background = B("#141417"), BorderBrush = B(missed ? "#9F1239" : "#27272A"), BorderThickness = new Thickness(1), Child = line,
        };
    }

    static FrameworkElement Bubble(CommsMessage m)
    {
        var mine = m.Direction == "OUT";
        var alert = m.Kind == "ALERT";
        var state = !mine ? "" : m.ReadAt > 0 ? "  ✓✓ read" : m.DeliveredAt > 0 ? "  ✓✓ delivered" : "  ✓ queued";
        var body = new TextBlock { Text = m.Body, TextWrapping = TextWrapping.Wrap, Foreground = B(alert ? "#FECDD3" : "#F4F4F5"), FontSize = 13, LineHeight = 19 };
        var col = Col(
            T((alert ? "⚠ ALERT  •  " : "") + (mine ? "You" : m.Sender), 10.5, alert ? "#FB7185" : mine ? "#FCD34D" : "#93C5FD", bold: true),
            body.M(0, 4),
            T(Time(m.CreatedAt, "HH:mm") + state, 10, "#71717A", mono: true).M(0, 5));
        return new Border
        {
            Background = B(alert ? "#2A0F14" : mine ? "#1C1917" : "#141417"),
            BorderBrush = B(alert ? "#9F1239" : mine ? "#78350F" : "#27272A"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(mine ? 120 : 0, 0, mine ? 0 : 120, 8),
            HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left, MaxWidth = 560, Child = col,
        };
    }

    void SendNow(string kind)
    {
        if (_device == null || _input.Text.Trim().Length == 0) return;
        if (kind == "ALERT" && MessageBox.Show("Send this as an ALERT? It rings on the terminal even when the app is in the background.", "Send alert", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { App.Comms.Send(_device, kind, _input.Text, "Command Center"); _input.Text = ""; FillThread(); _threadScroll.ScrollToEnd(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Comms"); }
    }

    void Broadcast()
    {
        var targets = App.Store.Devices().Where(d => L(d["active"]) == 1).Select(d => S(d["device_id"])).ToList();
        if (targets.Count == 0) { MessageBox.Show("No paired terminals.", "Broadcast alert"); return; }
        var dlg = new BroadcastDialog(targets.Count) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        foreach (var t in targets) App.Comms.Send(t, "ALERT", dlg.Text, "Command Center");
        if (_device != null) FillThread();
        MessageBox.Show($"Alert queued for {targets.Count} terminal(s). Online terminals receive it now; the others as soon as they reconnect.", "Broadcast alert");
    }

    sealed class BroadcastDialog : DarkWindow
    {
        public string Text => _box.Text.Trim();
        readonly TextBox _box;
        public BroadcastDialog(int count) : base("Broadcast Alert", $"Sent as an ALERT to all {count} paired terminal(s). It rings on each phone even when the app is in the background.", 520, 380)
        {
            _box = Field("Alert text");
            _box.AcceptsReturn = true; _box.MinHeight = 90; _box.TextWrapping = TextWrapping.Wrap; _box.MaxLength = CommsEngine.MaxBody;
            AddButton("Cancel", Close);
            AddButton("Send to all", () => { if (Text.Length > 0) DialogResult = true; }, "BtnDanger");
        }
    }

    /// <summary>Pop-up shown for every incoming alert, on top of all windows, with a sound.</summary>
    public static void ShowIncomingAlert(Window owner, CommsMessage m)
    {
        var sos = m.Body.StartsWith("SOS", StringComparison.OrdinalIgnoreCase);
        if (sos) App.Store.AdminAudit("SOS_RECEIVED", $"{m.DeviceId}: {m.Body}");
        SystemSounds.Exclamation.Play();
        if (sos) _ = Task.Run(async () => { for (var i = 0; i < 4; i++) { await Task.Delay(900); SystemSounds.Hand.Play(); } });
        var dev = App.Store.Devices().FirstOrDefault(d => S(d["device_id"]) == m.DeviceId);
        var where = dev == null ? m.DeviceId : $"{(S(dev["name"]).Length > 0 ? S(dev["name"]) : m.DeviceId)}  •  {S(dev["location_id"])} / {S(dev["gate_id"])}";
        var w = new Window
        {
            Title = (sos ? "SOS from " : "ALERT from ") + m.Sender, Width = 460, SizeToContent = SizeToContent.Height, Topmost = true, Background = B("#2A0F14"),
            WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, ShowActivated = true,
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
        };
        var open = Btn("Open conversation", (_, _) => { w.Close(); Show(owner, m.DeviceId); }, "BtnAmber");
        var dismiss = Btn("Dismiss", (_, _) => w.Close(), "BtnBase");
        w.Content = new Border
        {
            Padding = new Thickness(20), BorderBrush = B("#9F1239"), BorderThickness = new Thickness(0, 4, 0, 0),
            Child = Col(T(sos ? "🆘  SOS — EMERGENCY" : "⚠  ALERT", sos ? 22 : 18, "#FB7185", bold: true), T($"{m.Sender}  •  {where}", 11, "#FDA4AF", mono: true).M(0, 6),
                new TextBlock { Text = m.Body, TextWrapping = TextWrapping.Wrap, Foreground = B("#FFF1F2"), FontSize = 15, Margin = new Thickness(0, 12, 0, 12) },
                T(Time(m.CreatedAt, "dd MMM yyyy HH:mm:ss"), 10.5, "#FDA4AF", mono: true),
                Row(open, dismiss.M(8)).M(0, 14)),
        };
        w.Show();
    }
}
