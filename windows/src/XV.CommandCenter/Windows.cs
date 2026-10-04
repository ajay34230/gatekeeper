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
    readonly TextBlock _code = T("", 22, "#B45309", bold: true, mono: true);
    readonly TextBlock _expires = T("", 11, "#64748B", mono: true);
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    DateTime _expiry;
    string _payload = "";

    public PairWindow() : base("Pair a Terminal (Local Wi-Fi & Internet)",
        "On the phone: open XV Access Control → 'PC Server Connection' → 'Scan PC Pairing QR'. The QR is valid for 10 minutes and works once.", 640, 860)
    {
        RenderOptions.SetBitmapScalingMode(_qr, BitmapScalingMode.NearestNeighbor);
        var s = App.Settings;
        Body.Children.Add(new Border { Background = B("#FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, Child = _qr });
        Body.Children.Add(Row(T("Pairing code  ", 12, "#64748B"), _code).M(0, 10, 0, 0));
        Body.Children.Add(_expires);
        Body.Children.Add(Label("This PC's addresses on the local network"));
        var lan = NetUtil.LanAddresses();
        Body.Children.Add(Para(lan.Count == 0 ? "No network adapter is connected. Connect this PC to the same Wi-Fi/router as the terminals." : string.Join("\n", lan.Select(a => $"https://{a}:{s.Port}")), "#059669"));
        Body.Children.Add(Label("Internet address included in the QR"));
        Body.Children.Add(Para(s.InternetEnabled && s.PublicUrl.Length > 0 ? s.PublicUrl : "Not configured — terminals will connect on the local network only. Set it up in Cloud Link.", s.InternetEnabled ? "#1D4ED8" : "#94A3B8"));
        Body.Children.Add(Label("Certificate fingerprint the phone pins (SHA-256)"));
        Body.Children.Add(Para(App.Server.Fingerprint, "#334155"));
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
        var enabled = new CheckBox { Content = "Allow terminals to connect over the internet", IsChecked = s.InternetEnabled, Margin = new Thickness(0, 6, 0, 2), Foreground = B("#0F172A"), FontWeight = FontWeights.SemiBold };
        Body.Children.Add(enabled);
        Body.Children.Add(Para("When off, this PC refuses every connection that does not come from a private (LAN / VPN) address.", "#94A3B8"));

        var mode = Choice("Connection method", ["PORT_FORWARD", "VPN", "TUNNEL", "RELAY"], s.CloudMode);
        var help = Para("");
        void Explain() => help.Text = (mode.SelectedItem as string ?? mode.Text) switch
        {
            "VPN" => "VPN (recommended, simplest and most private): install Tailscale or ZeroTier on this PC and on each phone, sign in to the same network, then enter this PC's VPN address (e.g. 100.x.y.z) as the public host. No router changes needed.",
            "TUNNEL" => "Tunnel (e.g. Cloudflare Tunnel / ngrok): run the tunnel on this PC pointing to https://localhost:" + s.Port + " (with 'no TLS verify' for the origin) and paste the public https URL it gives you. Tick 'uses a public certificate'.",
            "RELAY" => "Hosted relay / cloud server: a cloud VM forwards TCP " + s.Port + " to this PC (e.g. SSH reverse tunnel or WireGuard). Enter the relay's public hostname and port. Data stays encrypted end-to-end.",
            _ => "Port forwarding: on your router forward TCP " + s.PublicPort + " → this PC's LAN address port " + s.Port + ". Use a static public IP or a free DDNS name (e.g. DuckDNS) as the public host.\n\n⚠ Least private option — this opens a port directly to the whole internet (still TLS + AES-256-GCM protected, but scannable and reachable by anyone). VPN or Tunnel expose nothing at all to the open internet and are safer where they're an option.",
        };
        mode.SelectionChanged += (_, _) => Explain(); Explain();
        Body.Children.Add(help);

        Body.Children.Add(Label("Restrict internet connections to known networks (optional)"));
        var cidrs = Field("Allowed internet address ranges, one per line — e.g. 203.0.113.0/24 (blank = allow any address)",
            string.Join(Environment.NewLine, s.AllowedInternetCidrs));
        cidrs.AcceptsReturn = true; cidrs.MinHeight = 60; cidrs.TextWrapping = TextWrapping.Wrap;
        Body.Children.Add(Para("Only affects connections from outside the local network / VPN — LAN terminals are never blocked by this. Leave blank unless you know the exact networks your remote terminals connect from.", "#94A3B8"));

        var host = Field("Public host (DDNS name, public IP or VPN IP)", s.PublicHost, mono: true);
        var port = Field("Public port", s.PublicPort.ToString(), mono: true);
        var url = Field("…or full public URL from a tunnel / relay (overrides host + port)", s.CloudUrl, mono: true);
        var cport = Field("Comms engine public port (messages, alerts, calls)", s.CommsPublicPort.ToString(), mono: true);
        var curl = Field("…or Comms URL from a tunnel (second hostname pointing to https://localhost:" + s.CommsPort + ")", s.CommsCloudUrl, mono: true);
        var pubCert = new CheckBox { Content = "The tunnel presents its own public HTTPS certificate (disable pinning for the internet address only)", IsChecked = s.CloudUsesPublicCertificate, Margin = new Thickness(0, 10, 0, 0) };
        Body.Children.Add(pubCert);

        Body.Children.Add(Label("Details terminals and cloud services need"));
        var details = Field("", "", readOnly: true, mono: true);
        details.TextWrapping = TextWrapping.Wrap; details.AcceptsReturn = true; details.MinHeight = 190;
        void Refresh() => details.Text =
            $"Server ID ............ {s.ServerId}\nServer name .......... {s.ServerName}\nLocal HTTPS port ..... {s.Port} (TCP)\nDiscovery port ....... {s.DiscoveryPort} (UDP, LAN only)\n" +
            $"LAN addresses ........ {string.Join(", ", NetUtil.LanAddresses())}\nInternet URL ......... {(s.PublicUrl.Length > 0 ? s.PublicUrl : "(not set)")}\n" +
            $"Comms engine ......... port {s.CommsPort} (TCP) • internet {(s.CommsPublicUrl.Length > 0 ? s.CommsPublicUrl : "(not set)")}\n" +
            $"TLS fingerprint ...... {App.Server.Fingerprint}\nEncryption ........... TLS 1.2/1.3 + AES-256-GCM per terminal, replay-protected\nAPI endpoints ........ GET /api/v1/ping, POST /api/v1/pair/enroll, POST /api/v1/rpc";
        Refresh();

        Body.Children.Add(Label("After saving"));
        Body.Children.Add(Para("Open 'Local Wi-Fi & Pair Device' and pair (or re-pair) terminals — the QR then carries the internet address, so phones switch automatically when away from the base network. Already-paired phones can also enter the URL manually in Sync Hub → Cloud Server."));

        AddButton("Export details…", () =>
        {
            if (!AdminGate.Require(this, "Export connection details")) return;
            var dlg = new SaveFileDialog { FileName = $"XV-Connection-{s.ServerId}", Filter = "PDF connection sheet|*.pdf|Text|*.txt|JSON|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                switch (System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant())
                {
                    case ".txt": ConnectionSheet.Txt(s, App.Server.Fingerprint, dlg.FileName); break;
                    case ".json": ConnectionSheet.Json(s, App.Server.Fingerprint, dlg.FileName); break;
                    default: ConnectionSheet.Pdf(s, App.Server.Fingerprint, dlg.FileName); break;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { Fail(ex); }
        }, "BtnEmerald");
        AddButton("Cancel", Close);
        AddButton("Save", async () =>
        {
            if (!int.TryParse(port.Text, out var pp) || pp is < 1 or > 65535) { MessageBox.Show("Public port must be 1-65535"); return; }
            var cleanUrl = url.Text.Trim();
            if (cleanUrl.Length > 0 && !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("The public URL must start with https://"); return; }
            if (!int.TryParse(cport.Text, out var cp) || cp is < 1 or > 65535) { MessageBox.Show("Comms public port must be 1-65535"); return; }
            var cleanCommsUrl = curl.Text.Trim();
            if (cleanCommsUrl.Length > 0 && !cleanCommsUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("The Comms URL must start with https://"); return; }
            s.CommsPublicPort = cp; s.CommsCloudUrl = cleanCommsUrl;
            s.InternetEnabled = enabled.IsChecked == true; s.CloudMode = mode.SelectedItem as string ?? mode.Text; s.PublicHost = host.Text.Trim(); s.PublicPort = pp;
            s.CloudUrl = cleanUrl; s.CloudUsesPublicCertificate = pubCert.IsChecked == true;
            s.AllowedInternetCidrs = cidrs.Text.Split('\n', '\r', ',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    /// <summary>What the exported diagnostics describe about this PC (no registry data, keys or passwords).</summary>
    static string DiagnosticSummary()
    {
        var st = App.Settings;
        var (entries, problems) = App.Store.VerifyAudit();
        var devices = App.Store.Devices();
        return string.Join("\n", [
            $"XV Command Center {ApiServer.Version}",
            $"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC{DateTimeOffset.Now:zzz})",
            $"Windows: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit), .NET {Environment.Version}",
            $"Uptime of this program: {DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime:d\\.hh\\:mm\\:ss}",
            $"Gate server: port {st.Port}, running {App.Server?.Running}, last error: {(App.Server?.LastError ?? "none").Split('\n')[0]}",
            $"Comms: {App.CommsStatus}",
            $"Internet access: {st.InternetEnabled}, data sharing: {st.DataSharing}, auto-lock: {st.AutoLockMinutes} min",
            $"Terminals: {devices.Count} ({devices.Count(d => Convert.ToInt64(d["active"]) == 1)} active)",
            $"Audit trail: {entries} entries, {problems.Count} problem(s)",
            ..problems.Select(p => "  " + p),
            ""]);
    }

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
        var returnReasons = Field("Exit reasons that ask for an expected return date (one per line) — used for the Leave & Overdue list and alerts", string.Join(Environment.NewLine, s.ReturnDateReasons));
        returnReasons.AcceptsReturn = true; returnReasons.MinHeight = 70; returnReasons.TextWrapping = TextWrapping.Wrap;
        var reasons = Field("Entry / exit reasons offered on terminals (one per line; operators can also type a custom reason and remarks)", string.Join(Environment.NewLine, s.MovementReasons));
        reasons.AcceptsReturn = true; reasons.MinHeight = 110; reasons.TextWrapping = TextWrapping.Wrap;
        var tokenH = Field("Operator sign-in length on terminals (hours, at least 24)", s.TokenHours.ToString(), mono: true);
        var graceH = Field("Offline grace period after session expiry (hours)", s.OfflineGraceHours.ToString(), mono: true);
        var showMonths = new CheckBox { Content = "Show months in stay durations once they pass 30 days (e.g. \"1mo 5d 2h 10m\") — off shows plain days/hours/mins", IsChecked = s.ShowMonthsInDuration, Margin = new Thickness(0, 8, 0, 0) };
        Body.Children.Add(showMonths);

        Body.Children.Add(Label("Data protection"));
        Body.Children.Add(Para("Terminals only ever receive data when they ask for it. Choose how much of the registry they are given. Gate records always flow from the terminals to this PC.", "#94A3B8"));
        string[] modes = ["FULL", "MINIMAL", "RECEIVE_ONLY"];
        var sharing = Choice("Data sharing with terminals", ["Full — names, ranks and units are copied to terminals (works fully offline)", "Minimal — terminals get IDs, secret-code hashes and status only; details shown after an online check (recommended)", "Receive-only — terminals hold nothing; every scan is verified online against this PC"], "");
        sharing.SelectedIndex = Math.Max(0, Array.IndexOf(modes, s.DataSharing));
        var lockMin = Field("Lock the Command Center after this many idle minutes (0 = never; needs the administrator password)", s.AutoLockMinutes.ToString(), mono: true);
        var block = new CheckBox { Content = "Block this program from opening connections to the internet (Windows Firewall, needs administrator approval)", IsChecked = s.BlockOutbound, Margin = new Thickness(0, 10, 0, 0) };
        Body.Children.Add(block);
        Body.Children.Add(Para("Incoming connections from paired terminals, the local network and VPN addresses keep working. The Command Center itself never uploads data anywhere.", "#94A3B8"));
        Body.Children.Add(Row(Btn(s.HasAdminPassword ? "Change administrator password" : "Set administrator password", (_, _) => AdminGate.ChangePassword(this), "BtnGold")).M(0, 6));

        Body.Children.Add(Label("Diagnostics"));
        Body.Children.Add(Para("Errors and crashes of this Command Center are written to a diagnostic log (technical messages only — no registry data or passwords). Export it and send the file to support when something goes wrong.", "#94A3B8"));
        var diagInfo = T($"Log size: {Diag.SizeBytes() / 1024.0:0.#} KB", 11, "#64748B", mono: true);
        Body.Children.Add(Row(
            Btn("Export diagnostic logs…", (_, _) =>
            {
                if (App.Settings.HasAdminPassword && !AdminGate.Require(this, "Export diagnostic logs")) return;
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"XV-CommandCenter-logs-{DateTime.Now:yyyyMMdd-HHmm}.zip", Filter = "Zip archive|*.zip" };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    var files = Diag.Export(dlg.FileName, DiagnosticSummary());
                    App.Store.AdminAudit("DIAGNOSTICS_EXPORTED", System.IO.Path.GetFileName(dlg.FileName));
                    MessageBox.Show(this, $"Diagnostic logs saved ({files} files):\n{dlg.FileName}", "Diagnostics");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Diagnostics"); }
            }, "BtnGold"),
            Btn("Clear logs", (_, _) =>
            {
                if (MessageBox.Show(this, "Delete the diagnostic log files?", "Diagnostics", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Diag.Clear(); diagInfo.Text = "Log size: 0 KB";
            }).M(8), diagInfo.M(12)).M(0, 4));

        Body.Children.Add(Label("Maintenance"));
        Body.Children.Add(Row(Btn("Wipe gate records", (_, _) =>
        {
            if (!AdminGate.Require(this, "Wipe gate records")) return;
            if (MessageBox.Show("Permanently delete ALL entry/exit records and presence? Registry, accounts and terminals are kept.", "Wipe records", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) App.Store.PurgeRecords();
        }, "BtnDanger")));

        AddButton("Cancel", Close);
        AddButton("Save Settings", async () =>
        {
            if (!int.TryParse(port.Text, out var p) || p is < 1 or > 65535 || !int.TryParse(tokenH.Text, out var th) || th < 1 || !int.TryParse(graceH.Text, out var gh) || gh < 0)
            { MessageBox.Show("Check the numeric fields."); return; }
            if (!int.TryParse(lockMin.Text, out var lm) || lm < 0 || lm > 720) { MessageBox.Show("Auto-lock minutes must be 0-720."); return; }
            // Weakening the lock (longer idle time or off) needs the administrator password.
            if (lm != s.AutoLockMinutes && (lm == 0 || (s.AutoLockMinutes != 0 && lm > s.AutoLockMinutes)) && !AdminGate.Require(this, lm == 0 ? "Turn auto-lock off" : "Lengthen auto-lock time")) return;
            s.AutoLockMinutes = lm;
            var newMode = modes[Math.Max(0, sharing.SelectedIndex)];
            var protectionChanged = newMode != s.DataSharing || (block.IsChecked == true) != s.BlockOutbound;
            if (protectionChanged && !AdminGate.Require(this, $"Change data protection (sharing {s.DataSharing} → {newMode}, outbound block {(block.IsChecked == true ? "ON" : "OFF")})")) return;
            if ((block.IsChecked == true) != s.BlockOutbound)
            {
                if (!OutboundGuard.Apply(block.IsChecked == true)) { MessageBox.Show("Windows Firewall was not changed (administrator approval was cancelled or failed). Other settings were not saved.", "Data protection"); return; }
                App.Store.AdminAudit("OUTBOUND_BLOCK", block.IsChecked == true ? "ON" : "OFF");
            }
            if (newMode != s.DataSharing) App.Store.AdminAudit("DATA_SHARING_MODE", $"{s.DataSharing} -> {newMode}");
            s.DataSharing = newMode; s.BlockOutbound = block.IsChecked == true;
            var restart = p != s.Port;
            s.ServerName = name.Text.Trim().Length > 0 ? name.Text.Trim() : Environment.MachineName; s.Port = p; s.RequireApproval = approval.IsChecked == true;
            s.StartWithWindows = autostart.IsChecked == true; s.TokenHours = th; s.OfflineGraceHours = gh;
            s.ShowMonthsInDuration = showMonths.IsChecked == true;
            static List<string> Lines(string t) => t.Split('\n', '\r', ',').Select(x => x.Trim()).Where(x => x.Length > 0 && x.Length <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            s.CustomFields = Lines(fields.Text);
            s.ReturnDateReasons = Lines(returnReasons.Text);
            s.MovementReasons = Lines(reasons.Text).Where(r => !r.Equals("Custom", StringComparison.OrdinalIgnoreCase)).ToList();
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
        foreach (var l in App.Store.Locations()) { var id = S(l["id"]); _locs.Children.Add(Spread(T($"{id}   {S(l["name"])}", 12, "#1E293B", mono: true), Btn("Remove", (_, _) => { App.Store.DeleteLocation(id); Fill(); }, "BtnDanger")).M(0, 2, 0, 2)); }
        foreach (var g in App.Store.Gates()) { var id = S(g["id"]); _gates.Children.Add(Spread(T($"{id}   {S(g["name"])}", 12, "#1E293B", mono: true), Btn("Remove", (_, _) => { App.Store.DeleteGate(id); Fill(); }, "BtnDanger")).M(0, 2, 0, 2)); }
        if (_locs.Children.Count == 0) _locs.Children.Add(Para("No locations yet.", "#94A3B8"));
        if (_gates.Children.Count == 0) _gates.Children.Add(Para("No gates yet.", "#94A3B8"));
    }
}
