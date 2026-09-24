using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>Shows the one-time pairing QR the Android terminal scans ("Local Wi-Fi &amp; Native").</summary>
public sealed class PairWindow : DarkWindow
{
    readonly Image _qr = new() { Width = 300, Height = 300, Margin = new Thickness(0, 6, 0, 6) };
    readonly TextBlock _code = T("", 22, "#FBBF24", bold: true, mono: true);
    readonly TextBlock _expires = T("", 11, "#A1A1AA", mono: true);
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    DateTime _expiry;
    string _payload = "";

    public PairWindow() : base("Pair a Terminal (Local Wi-Fi & Internet)",
        "On the phone: open XV Access Control → 'PC Server Connection' → 'Scan PC Pairing QR'. The QR is valid for 10 minutes and works once.", 640, 860)
    {
        RenderOptions.SetBitmapScalingMode(_qr, BitmapScalingMode.NearestNeighbor);
        var s = App.Settings;
        Body.Children.Add(new Border { Background = B("#FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, Child = _qr });
        Body.Children.Add(Row(T("Pairing code  ", 12, "#A1A1AA"), _code).M(0, 10, 0, 0));
        Body.Children.Add(_expires);
        Body.Children.Add(Label("This PC's addresses on the local network"));
        var lan = NetUtil.LanAddresses();
        Body.Children.Add(Para(lan.Count == 0 ? "No network adapter is connected. Connect this PC to the same Wi-Fi/router as the terminals." : string.Join("\n", lan.Select(a => $"https://{a}:{s.Port}")), "#34D399"));
        Body.Children.Add(Label("Internet address included in the QR"));
        Body.Children.Add(Para(s.InternetEnabled && s.PublicUrl.Length > 0 ? s.PublicUrl : "Not configured — terminals will connect on the local network only. Set it up in Cloud Link.", s.InternetEnabled ? "#93C5FD" : "#71717A"));
        Body.Children.Add(Label("Certificate fingerprint the phone pins (SHA-256)"));
        Body.Children.Add(Para(App.Server.Fingerprint, "#D4D4D8"));
        Body.Children.Add(Label("If the phone cannot connect"));
        Body.Children.Add(Para($"• Both devices must be on the same network (guest Wi-Fi often blocks devices from seeing each other).\n• Windows Firewall must allow TCP {s.Port} and UDP {s.DiscoveryPort} — the installer adds these rules.\n• Check the phone's Sync Hub → 'Test Connection'."));
        AddButton("Copy pairing text", () =>
        {
            Clipboard.SetText(_payload);
            MessageBox.Show("Pairing text copied. Send it to the operator (valid 10 minutes, works once); they paste it under 'PC Server Connection' in the app.\n\nFor phones outside the base network, set up Cloud Link first so the text contains the internet address.", "Pairing text copied");
        });
        AddButton("New QR", NewCode);
        AddButton("Done", Close, "BtnAmber");
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
        NewCode();
        _timer.Start();
    }

    void NewCode()
    {
        var code = App.Store.NewPairCode();
        _expiry = DateTime.Now.AddMinutes(10);
        _code.Text = code;
        _payload = Pairing.Payload(App.Settings, App.Server.Fingerprint, code);
        _qr.Source = Dialogs.Png(Pairing.QrPng(_payload, 6));
        Tick();
    }

    void Tick()
    {
        var left = _expiry - DateTime.Now;
        _expires.Text = left > TimeSpan.Zero ? $"Expires in {left:mm\\:ss} • single use" : "Expired — click 'New QR'";
    }
}

/// <summary>Everything needed to link terminals to this PC over the internet / cloud, now or later.</summary>
public sealed class CloudLinkWindow : DarkWindow
{
    public CloudLinkWindow() : base("Cloud Link — Internet Connection Setup",
        "Terminals always try the local network first and fall back to the internet address below. Every request stays end-to-end encrypted (AES-256-GCM with each terminal's own key) even when a tunnel or relay handles TLS.", 720, 900)
    {
        var s = App.Settings;
        var enabled = new CheckBox { Content = "Allow terminals to connect over the internet", IsChecked = s.InternetEnabled, Margin = new Thickness(0, 6, 0, 2), Foreground = B("#F4F4F5"), FontWeight = FontWeights.SemiBold };
        Body.Children.Add(enabled);
        Body.Children.Add(Para("When off, this PC refuses every connection that does not come from a private (LAN / VPN) address.", "#71717A"));

        var mode = Choice("Connection method", ["PORT_FORWARD", "VPN", "TUNNEL", "RELAY"], s.CloudMode);
        var help = Para("");
        void Explain() => help.Text = (mode.SelectedItem as string ?? mode.Text) switch
        {
            "VPN" => "VPN (recommended, simplest and most private): install Tailscale or ZeroTier on this PC and on each phone, sign in to the same network, then enter this PC's VPN address (e.g. 100.x.y.z) as the public host. No router changes needed.",
            "TUNNEL" => "Tunnel (e.g. Cloudflare Tunnel / ngrok): run the tunnel on this PC pointing to https://localhost:" + s.Port + " (with 'no TLS verify' for the origin) and paste the public https URL it gives you. Tick 'uses a public certificate'.",
            "RELAY" => "Hosted relay / cloud server: a cloud VM forwards TCP " + s.Port + " to this PC (e.g. SSH reverse tunnel or WireGuard). Enter the relay's public hostname and port. Data stays encrypted end-to-end.",
            _ => "Port forwarding: on your router forward TCP " + s.PublicPort + " → this PC's LAN address port " + s.Port + ". Use a static public IP or a free DDNS name (e.g. DuckDNS) as the public host.",
        };
        mode.SelectionChanged += (_, _) => Explain(); Explain();
        Body.Children.Add(help);

        var host = Field("Public host (DDNS name, public IP or VPN IP)", s.PublicHost, mono: true);
        var port = Field("Public port", s.PublicPort.ToString(), mono: true);
        var url = Field("…or full public URL from a tunnel / relay (overrides host + port)", s.CloudUrl, mono: true);
        var pubCert = new CheckBox { Content = "The tunnel presents its own public HTTPS certificate (disable pinning for the internet address only)", IsChecked = s.CloudUsesPublicCertificate, Margin = new Thickness(0, 10, 0, 0) };
        Body.Children.Add(pubCert);

        Body.Children.Add(Label("Details terminals and cloud services need"));
        var details = Field("", "", readOnly: true, mono: true);
        details.TextWrapping = TextWrapping.Wrap; details.AcceptsReturn = true; details.MinHeight = 190;
        void Refresh() => details.Text =
            $"Server ID ............ {s.ServerId}\nServer name .......... {s.ServerName}\nLocal HTTPS port ..... {s.Port} (TCP)\nDiscovery port ....... {s.DiscoveryPort} (UDP, LAN only)\n" +
            $"LAN addresses ........ {string.Join(", ", NetUtil.LanAddresses())}\nInternet URL ......... {(s.PublicUrl.Length > 0 ? s.PublicUrl : "(not set)")}\n" +
            $"TLS fingerprint ...... {App.Server.Fingerprint}\nEncryption ........... TLS 1.2/1.3 + AES-256-GCM per terminal, replay-protected\nAPI endpoints ........ GET /api/v1/ping, POST /api/v1/pair/enroll, POST /api/v1/rpc";
        Refresh();

        Body.Children.Add(Label("After saving"));
        Body.Children.Add(Para("Open 'Local Wi-Fi & Pair Device' and pair (or re-pair) terminals — the QR then carries the internet address, so phones switch automatically when away from the base network. Already-paired phones can also enter the URL manually in Sync Hub → Cloud Server."));

        AddButton("Cancel", Close);
        AddButton("Save", async () =>
        {
            if (!int.TryParse(port.Text, out var pp) || pp is < 1 or > 65535) { MessageBox.Show("Public port must be 1-65535"); return; }
            var cleanUrl = url.Text.Trim();
            if (cleanUrl.Length > 0 && !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("The public URL must start with https://"); return; }
            s.InternetEnabled = enabled.IsChecked == true; s.CloudMode = mode.SelectedItem as string ?? mode.Text; s.PublicHost = host.Text.Trim(); s.PublicPort = pp;
            s.CloudUrl = cleanUrl; s.CloudUsesPublicCertificate = pubCert.IsChecked == true;
            s.Save();
            Refresh();
            await App.StartServerAsync();
            MessageBox.Show(s.InternetEnabled ? "Internet access is ON. Pair terminals again to hand them the internet address." : "Internet access is OFF. Only local network connections are accepted.", "Cloud Link saved");
        }, "BtnAmber");
    }
}

/// <summary>Locations, gates and general server settings.</summary>
public sealed class StationsWindow : DarkWindow
{
    readonly StackPanel _locs = new(), _gates = new();

    public StationsWindow() : base("Stations & Settings", "Locations and gates appear in the terminal's sign-in screen. Nothing is pre-filled — add the real posts of your base.", 700, 880)
    {
        var s = App.Settings;
        Body.Children.Add(Label("Locations"));
        Body.Children.Add(_locs);
        var lid = new TextBox { Width = 110, FontFamily = Mono, ToolTip = "e.g. LOC07" }; var lname = new TextBox { Width = 330, ToolTip = "e.g. Location 07 (Main Hub)" };
        Body.Children.Add(Row(lid, lname.M(8), Btn("Add / Update", (_, _) => { try { App.Store.UpsertLocation(lid.Text, lname.Text); lid.Text = lname.Text = ""; Fill(); } catch (Exception ex) { Fail(ex); } }, "BtnAmber").M(8)).M(0, 4));
        Body.Children.Add(Label("Gates"));
        Body.Children.Add(_gates);
        var gid = new TextBox { Width = 110, FontFamily = Mono, ToolTip = "e.g. G02" }; var gname = new TextBox { Width = 330, ToolTip = "e.g. Gate 02 (Primary)" };
        Body.Children.Add(Row(gid, gname.M(8), Btn("Add / Update", (_, _) => { try { App.Store.UpsertGate(gid.Text, gname.Text); gid.Text = gname.Text = ""; Fill(); } catch (Exception ex) { Fail(ex); } }, "BtnAmber").M(8)).M(0, 4));

        Body.Children.Add(Label("Server"));
        var name = Field("Command Center name (shown on terminals)", s.ServerName);
        var port = Field("HTTPS port (terminals must be re-paired after a change)", s.Port.ToString(), mono: true);
        var approval = new CheckBox { Content = "Operators who self-register must be approved here", IsChecked = s.RequireApproval, Margin = new Thickness(0, 10, 0, 0) };
        var autostart = new CheckBox { Content = "Start XV Command Center when Windows starts", IsChecked = s.StartWithWindows, Margin = new Thickness(0, 8, 0, 0) };
        Body.Children.Add(approval); Body.Children.Add(autostart);
        Body.Children.Add(Label("Customisation"));
        var fields = Field("Custom personnel fields (one per line, e.g. Blood Group, Weapon No, Next of Kin)", string.Join(Environment.NewLine, s.CustomFields));
        fields.AcceptsReturn = true; fields.MinHeight = 80; fields.TextWrapping = TextWrapping.Wrap;
        var types = Field("History record types besides ENTRY / EXIT (one per line)", string.Join(Environment.NewLine, s.EventTypes));
        types.AcceptsReturn = true; types.MinHeight = 110; types.TextWrapping = TextWrapping.Wrap;
        var tokenH = Field("Operator session length (hours)", s.TokenHours.ToString(), mono: true);
        var graceH = Field("Offline grace period after session expiry (hours)", s.OfflineGraceHours.ToString(), mono: true);

        Body.Children.Add(Label("Maintenance"));
        Body.Children.Add(Row(Btn("Wipe gate records", (_, _) =>
        {
            if (MessageBox.Show("Permanently delete ALL entry/exit records and presence? Registry, accounts and terminals are kept.", "Wipe records", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) App.Store.PurgeRecords();
        }, "BtnDanger")));

        AddButton("Cancel", Close);
        AddButton("Save Settings", async () =>
        {
            if (!int.TryParse(port.Text, out var p) || p is < 1 or > 65535 || !int.TryParse(tokenH.Text, out var th) || th < 1 || !int.TryParse(graceH.Text, out var gh) || gh < 0)
            { MessageBox.Show("Check the numeric fields."); return; }
            var restart = p != s.Port;
            s.ServerName = name.Text.Trim().Length > 0 ? name.Text.Trim() : Environment.MachineName; s.Port = p; s.RequireApproval = approval.IsChecked == true;
            s.StartWithWindows = autostart.IsChecked == true; s.TokenHours = th; s.OfflineGraceHours = gh;
            static List<string> Lines(string t) => t.Split('\n', '\r', ',').Select(x => x.Trim()).Where(x => x.Length > 0 && x.Length <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            s.CustomFields = Lines(fields.Text);
            s.EventTypes = Lines(types.Text).Where(t => !t.Equals("ENTRY", StringComparison.OrdinalIgnoreCase) && !t.Equals("EXIT", StringComparison.OrdinalIgnoreCase)).ToList();
            s.Save();
            if (Owner is MainWindow mw) mw.Refresh();
            ApplyAutostart(s.StartWithWindows);
            if (restart) await App.StartServerAsync();
            Close();
        }, "BtnAmber");
        Fill();
    }

    public static void ApplyAutostart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)!;
            if (on) key.SetValue("XVCommandCenter", $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue("XVCommandCenter", false);
        }
        catch { /* policy may forbid it */ }
    }

    void Fill()
    {
        _locs.Children.Clear(); _gates.Children.Clear();
        foreach (var l in App.Store.Locations()) { var id = S(l["id"]); _locs.Children.Add(Spread(T($"{id}   {S(l["name"])}", 12, "#E4E4E7", mono: true), Btn("Remove", (_, _) => { App.Store.DeleteLocation(id); Fill(); }, "BtnDanger")).M(0, 2, 0, 2)); }
        foreach (var g in App.Store.Gates()) { var id = S(g["id"]); _gates.Children.Add(Spread(T($"{id}   {S(g["name"])}", 12, "#E4E4E7", mono: true), Btn("Remove", (_, _) => { App.Store.DeleteGate(id); Fill(); }, "BtnDanger")).M(0, 2, 0, 2)); }
        if (_locs.Children.Count == 0) _locs.Children.Add(Para("No locations yet.", "#71717A"));
        if (_gates.Children.Count == 0) _gates.Children.Add(Para("No gates yet.", "#71717A"));
    }
}
