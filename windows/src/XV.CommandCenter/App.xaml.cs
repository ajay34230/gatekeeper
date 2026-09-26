using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using XV.Comms;
using XV.Core;

namespace XV.CommandCenter;

public partial class App : Application
{
    public static Store Store { get; private set; } = null!;
    public static ApiServer Server { get; private set; } = null!;
    public static Settings Settings => Store.Settings;
    /// <summary>Separate Comms engine (messages, alerts, calls) with its own listener and encrypted database.</summary>
    public static CommsEngine Comms { get; private set; } = null!;
    public static string CommsStatus => Comms.Running ? $"Listening on port {Settings.CommsPort} • end-to-end encrypted" : "Not running: " + (Comms.LastError ?? "stopped");
    /// <summary>Adds per-terminal actions (e.g. calls) to the Comms Center header: (panel, deviceId, online).</summary>
    public static Action<StackPanel, string, bool>? ExtendCommsHeader;
    static DiscoveryResponder? _discovery;
    static Mutex? _single;
    static readonly System.Windows.Threading.DispatcherTimer _autoBackupTimer = new() { Interval = TimeSpan.FromHours(24) };

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Crash collector: every unexpected error goes to the diagnostic log (Stations & Settings → Diagnostics → Export).
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            Diag.Error("FATAL unhandled exception" + (ex.IsTerminating ? " (program closing)" : ""), ex.ExceptionObject as Exception);
            try { File.AppendAllText(Paths.File("error.log"), $"{DateTime.Now:u} FATAL {ex.ExceptionObject}\n"); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, ex) => { Diag.Error("Unobserved background task error", ex.Exception); ex.SetObserved(); };
        Diag.Info($"Command Center {ApiServer.Version} starting on {Environment.OSVersion} (.NET {Environment.Version})");
        DispatcherUnhandledException += (_, ex) =>
        {
            Diag.Error("UI error", ex.Exception);
            File.AppendAllText(Paths.File("error.log"), $"{DateTime.Now:u} {ex.Exception}\n");
            MessageBox.Show(ex.Exception.Message, "XV Command Center", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        var screenshotDir = e.Args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
        if (screenshotDir == null)
        {
            _single = new Mutex(true, "Global\\XVCommandCenterSingleInstance", out var first);
            // After a restore the previous instance is still exiting: wait for it.
            for (var i = 0; !first && e.Args.Contains("--after-restore") && i < 40; i++)
            {
                await Task.Delay(250);
                try { first = _single.WaitOne(0); } catch (AbandonedMutexException) { first = true; }
            }
            if (!first) { MessageBox.Show("XV Command Center is already running.", "XV Command Center"); Shutdown(); return; }
        }

        string? recovered = null;
        try
        {
            if (screenshotDir == null) DataFolderSecurity.Apply();
            recovered = AutoBackup.RestoreIfMissing();
            if (recovered != null) Diag.Info($"Data folder had no database on start -- restored automatic backup {recovered}");
            var settings = Settings.Load();
            try { Store = new Store(settings); }
            catch (Exception ex)
            {
                // The database file was there but would not open (corruption). Move it aside and try the newest
                // automatic backup once before giving up -- this is the "server corrupted" recovery path.
                Diag.Error("Database failed to open; attempting automatic recovery", ex);
                AutoBackup.Quarantine();
                recovered = AutoBackup.ForceRestoreLatest();
                if (recovered == null) throw;
                settings = Settings.Load();
                Store = new Store(settings);
                Diag.Info($"Recovered from a corrupted database using automatic backup {recovered}");
            }
            var cert = CertManager.LoadOrCreate(settings);
            Server = new ApiServer(Store, cert);
            Comms = new CommsEngine(settings, cert, Store.DeviceKey);
            Comms.Store.Checkpoint();
            AutoBackup.Snapshot(Store);
            _autoBackupTimer.Tick += (_, _) => { Comms.Store.Checkpoint(); AutoBackup.Snapshot(Store); };
            _autoBackupTimer.Start();
            Comms.MessageReceived += m =>
            {
                if (m.Kind == "ALERT") Current.Dispatcher.BeginInvoke(() => { if (Current.MainWindow is Window w && screenshotDir == null) CommsWindow.ShowIncomingAlert(w, m); });
            };
            Server.Log += Diag.Info;
            Comms.Log += Diag.Info;
            Calls.Init();
            await StartServerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex is UnauthorizedAccessException
                ? "This Windows account is not allowed to open the XV Command Center data.\n\nThe encrypted data folder is restricted to the Windows user who set up the Command Center (and Administrators). Sign in with that account, or ask an administrator."
                : "The secure server could not start:\n\n" + ex.Message, "XV Command Center", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (screenshotDir == null && Settings.StartWithWindows) StationsWindow.ApplyAutostart(true);
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
        if (screenshotDir != null) { await CaptureScreenshotsAsync(main, screenshotDir); return; }
        if (e.Args.Contains("--after-restore"))
        {
            Store.AdminAudit("RESTORED_FROM_BACKUP");
            MessageBox.Show(main, "The backup was restored. Paired terminals keep working (same server identity and certificate).", "Restore finished");
        }
        if (recovered != null)
        {
            Store.AdminAudit("AUTO_RECOVERED", recovered);
            var when = DateTime.TryParseExact(recovered, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var t)
                ? t.ToLocalTime().ToString("dd MMM yyyy HH:mm") : recovered;
            MessageBox.Show(main, $"The database was not found (or would not open) at startup and was automatically recovered from the backup taken {when}.\n\nAny record added after that backup was made is lost. Check Stations & Settings → Diagnostics for details.",
                "Data recovered automatically", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        OverdueMonitor.Start();
        DueSoonMonitor.Start();
        AutoLock.Start();
        // First start: protect the Command Center before it is used (auto-lock, restore, exports need this password).
        if (!Settings.HasAdminPassword && Current.MainWindow is Window mw)
            mw.Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(mw, "Set the administrator password now. It protects exports, backups, restore, lost-card reports and settings changes, and unlocks the Command Center after auto-lock.\n\nKeep it safe: it cannot be recovered.", "Secure this Command Center", MessageBoxButton.OK, MessageBoxImage.Information);
                AdminGate.ChangePassword(mw);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Stops everything, replaces the data with the backup and starts a fresh instance.</summary>
    public static async void RestoreAndRestart(string file, string password)
    {
        try
        {
            _discovery?.Dispose();
            await Server.StopAsync();
            await Comms.DisposeAsync();
            Store.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await Task.Run(() => Backup.Restore(file, password));
        }
        catch (Exception ex)
        {
            MessageBox.Show("The restore failed: " + ex.Message + "\n\nThe Command Center will restart with the data it had.", "Restore");
        }
        _single?.ReleaseMutex();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--after-restore") { UseShellExecute = false });
        Current.Shutdown();
    }

    public static async Task StartServerAsync()
    {
        _discovery?.Dispose();
        await Server.StopAsync();
        await Server.StartAsync();
        await Comms.StopAsync();
        await Comms.StartAsync();   // failures are reported in the Comms Center, the gate server keeps running
        try { _discovery = new DiscoveryResponder(Store.Settings, Server.Fingerprint); } catch { _discovery = null; }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _discovery?.Dispose();
        try { Server?.StopAsync().Wait(3000); } catch { }
        try { Comms?.DisposeAsync().AsTask().Wait(3000); } catch { }
        Store?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Used by CI: renders each screen of the running app to PNG, then exits.</summary>
    static async Task CaptureScreenshotsAsync(MainWindow w, string dir)
    {
        Directory.CreateDirectory(dir);
        async Task Snap(Window win, string name)
        {
            await Task.Delay(1200);
            win.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(win);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            await using var fs = File.Create(Path.Combine(dir, name + ".png"));
            enc.Save(fs);
        }
        foreach (var (tab, name) in w.ScreenshotTabs()) { tab(); await Snap(w, name); }
        foreach (var (make, name) in Dialogs.ScreenshotWindows(w))
        {
            var win = make(); win.Show(); await Snap(win, name); win.Close();
        }
        await CardStudioWindow.CaptureForCiAsync(w, Path.Combine(dir, "17-id-card-studio.png"));
        Current.Shutdown();
    }
}
