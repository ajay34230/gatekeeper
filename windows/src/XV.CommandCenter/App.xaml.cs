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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            File.AppendAllText(Paths.File("error.log"), $"{DateTime.Now:u} {ex.Exception}\n");
            MessageBox.Show(ex.Exception.Message, "XV Command Center", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        var screenshotDir = e.Args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
        if (screenshotDir == null)
        {
            _single = new Mutex(true, "Global\\XVCommandCenterSingleInstance", out var first);
            if (!first) { MessageBox.Show("XV Command Center is already running.", "XV Command Center"); Shutdown(); return; }
        }

        try
        {
            var settings = Settings.Load();
            Store = new Store(settings);
            var cert = CertManager.LoadOrCreate(settings);
            Server = new ApiServer(Store, cert);
            Comms = new CommsEngine(settings, cert, Store.DeviceKey);
            Comms.MessageReceived += m =>
            {
                if (m.Kind == "ALERT") Current.Dispatcher.BeginInvoke(() => { if (Current.MainWindow is Window w && screenshotDir == null) CommsWindow.ShowIncomingAlert(w, m); });
            };
            await StartServerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("The secure server could not start:\n\n" + ex.Message, "XV Command Center", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (screenshotDir == null && Settings.StartWithWindows) StationsWindow.ApplyAutostart(true);
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
        if (screenshotDir != null) await CaptureScreenshotsAsync(main, screenshotDir);
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
        Current.Shutdown();
    }
}
