using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// Administrator password gate for everything that lets data leave this PC (exports, printed credentials,
/// connection sheets) and for data-protection settings. The password is set on first use, stored only as a
/// PBKDF2 hash, and an unlock is remembered for five minutes.
/// </summary>
public static class AdminGate
{
    static DateTime _unlockedUntil = DateTime.MinValue;
    static int _failures;
    static DateTime _lockedUntil = DateTime.MinValue;

    public static bool Require(Window? owner, string purpose)
    {
        owner ??= Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        var s = App.Settings;
        if (s.HasAdminPassword && DateTime.UtcNow < _unlockedUntil) { App.Store.AdminAudit("ADMIN_ACTION", purpose); return true; }
        if (DateTime.UtcNow < _lockedUntil)
        {
            MessageBox.Show($"Too many wrong passwords. Try again in {(int)(_lockedUntil - DateTime.UtcNow).TotalSeconds + 1} seconds.", "Administrator password", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var dlg = new PasswordPrompt(purpose, !s.HasAdminPassword) { Owner = owner };
        if (dlg.ShowDialog() != true) return false;
        if (!s.HasAdminPassword)
        {
            s.SetAdminPassword(dlg.Password);
            s.Save();
            App.Store.AdminAudit("ADMIN_PASSWORD_SET");
        }
        else if (!s.CheckAdminPassword(dlg.Password))
        {
            App.Store.AdminAudit("ADMIN_PASSWORD_FAILED", purpose);
            if (++_failures >= 5) { _failures = 0; _lockedUntil = DateTime.UtcNow.AddMinutes(1); }
            MessageBox.Show("Wrong administrator password.", "Administrator password", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _failures = 0;
        _unlockedUntil = DateTime.UtcNow.AddMinutes(5);
        App.Store.AdminAudit("ADMIN_ACTION", purpose);
        return true;
    }

    /// <summary>Forget the current unlock (e.g. after the password changes).</summary>
    public static void Lock() => _unlockedUntil = DateTime.MinValue;

    public static void ChangePassword(Window? owner)
    {
        if (App.Settings.HasAdminPassword && !Require(owner, "Change administrator password")) return;
        var dlg = new PasswordPrompt("Set a new administrator password", create: true) { Owner = owner };
        if (dlg.ShowDialog() != true) return;
        App.Settings.SetAdminPassword(dlg.Password);
        App.Settings.Save();
        App.Store.AdminAudit("ADMIN_PASSWORD_CHANGED");
        Lock();
        MessageBox.Show("Administrator password updated.", "Administrator password");
    }

    sealed class PasswordPrompt : DarkWindow
    {
        readonly PasswordBox _pw = new() { Margin = new Thickness(0, 0, 0, 4) };
        readonly PasswordBox _confirm = new();
        public string Password => _pw.Password;

        public PasswordPrompt(string purpose, bool create) : base(create ? "Create Administrator Password" : "Administrator Password",
            create ? "Exports, printed credentials and data-protection settings are protected by an administrator password. Choose one now (at least 8 characters). It is stored only as a salted PBKDF2 hash and cannot be recovered — keep it safe."
                   : "This action lets data leave the Command Center or changes how data is shared. Enter the administrator password.", 480, 380)
        {
            Body.Children.Add(Para("Action: " + purpose, "#FBBF24"));
            Body.Children.Add(Label(create ? "New password" : "Password"));
            Body.Children.Add(_pw);
            if (create) { Body.Children.Add(Label("Confirm password")); Body.Children.Add(_confirm); }
            Loaded += (_, _) => _pw.Focus();
            AddButton("Cancel", Close);
            var ok = AddButton(create ? "Set Password" : "Unlock", () =>
            {
                if (create && _pw.Password.Length < 8) { MessageBox.Show("Use at least 8 characters."); return; }
                if (create && _pw.Password != _confirm.Password) { MessageBox.Show("The passwords do not match."); return; }
                if (_pw.Password.Length == 0) return;
                DialogResult = true;
            }, "BtnAmber");
            ok.IsDefault = true;
        }
    }
}

/// <summary>Windows Firewall rule that stops this program from opening connections to public internet addresses.</summary>
public static class OutboundGuard
{
    public const string RuleName = "XV Command Center - Block Internet Outbound";

    // Every public IPv4 range: everything except 10/8, 100.64/10 (VPN CGNAT), 127/8, 169.254/16, 172.16/12, 192.168/16 and multicast/reserved.
    const string PublicV4 = "0.0.0.0-9.255.255.255,11.0.0.0-100.63.255.255,100.128.0.0-126.255.255.255,128.0.0.0-169.253.255.255,169.255.0.0-172.15.255.255,172.32.0.0-192.167.255.255,192.169.0.0-223.255.255.255";
    // Global unicast IPv6 (link-local fe80::/10 and unique-local fc00::/7, used by VPNs, stay allowed).
    const string PublicV6 = "2000::-3fff:ffff:ffff:ffff:ffff:ffff:ffff:ffff";

    public static string AddCommand(string exe) =>
        $"netsh advfirewall firewall add rule name=\"{RuleName}\" dir=out action=block program=\"{exe}\" remoteip={PublicV4},{PublicV6} enable=yes";

    public static string DeleteCommand => $"netsh advfirewall firewall delete rule name=\"{RuleName}\"";

    /// <summary>Applies or removes the rule. Needs administrator rights, so Windows shows a UAC prompt. Returns false when cancelled or failed.</summary>
    public static bool Apply(bool block)
    {
        var exe = Environment.ProcessPath ?? "";
        var cmd = block ? DeleteCommand + " & " + AddCommand(exe) : DeleteCommand + " & exit /b 0";
        try
        {
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            if (p == null) return false;
            p.WaitForExit(30_000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; } // UAC cancelled
    }
}
