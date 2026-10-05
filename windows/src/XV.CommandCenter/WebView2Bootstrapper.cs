using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace XV.CommandCenter;

/// <summary>
/// Ensures WebView2 runtime is installed on Windows 10+. On first run without WebView2,
/// automatically downloads and installs the bootstrapper (standalone installer).
/// </summary>
public static class WebView2Bootstrapper
{
    const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    /// <summary>Check if WebView2 runtime is available by attempting to access the registry key.</summary>
    static bool IsRuntimeInstalled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            if (key?.GetValue("pv") is string version && !string.IsNullOrEmpty(version))
                return true;

            using var key2 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            if (key2?.GetValue("pv") is string version2 && !string.IsNullOrEmpty(version2))
                return true;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Ensure WebView2 runtime is installed. If not, download and run the bootstrapper.
    /// Returns true if runtime is available (either was already installed or now installed).
    /// Returns false if user canceled or installation failed.
    /// </summary>
    public static async Task<bool> EnsureInstalledAsync()
    {
        if (IsRuntimeInstalled())
            return true;

        var dlg = new WebView2InstallDialog();
        var result = dlg.ShowDialog();

        if (result != true) // User clicked Cancel
            return false;

        // Download and install
        return await DownloadAndInstallAsync();
    }

    static async Task<bool> DownloadAndInstallAsync()
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebView2RuntimeBootstrapper.exe");

            // Download
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                var response = await client.GetAsync(BootstrapperUrl, HttpCompletionOption.ResponseContentRead);
                response.EnsureSuccessStatusCode();

                await using var fs = File.Create(tempPath);
                await response.Content.CopyToAsync(fs);
            }

            // Execute installer
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath)
            {
                UseShellExecute = true,
                Verb = "runas" // Request admin privileges
            });

            if (proc == null)
                throw new InvalidOperationException("Failed to start installer process.");

            proc.WaitForExit();

            // Clean up
            try { File.Delete(tempPath); } catch { }

            // Check if installation succeeded
            if (proc.ExitCode == 0 && IsRuntimeInstalled())
                return true;

            // If exit code is 3010, system needs reboot but WebView2 should work
            if (proc.ExitCode == 3010)
            {
                MessageBox.Show(
                    "WebView2 runtime installed successfully.\n\nA system restart is recommended. The Command Center will attempt to start now.",
                    "WebView2 Installation", MessageBoxButton.OK, MessageBoxImage.Information);
                return IsRuntimeInstalled();
            }

            throw new InvalidOperationException($"WebView2 installer exited with code {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to install WebView2 runtime:\n\n{ex.Message}\n\nThe Command Center requires WebView2 to function. Please install it manually from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                "WebView2 Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}

/// <summary>Dialog asking user to install WebView2 runtime.</summary>
public partial class WebView2InstallDialog : Window
{
    public WebView2InstallDialog()
    {
        Title = "Install WebView2 Runtime";
        Width = 400;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Install WebView2 Runtime",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        });
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "The ID Card Studio and other features require Microsoft Edge WebView2 Runtime. It will be downloaded and installed automatically.",
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelBtn = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, Margin = new Thickness(10, 0, 0, 0) };
        var installBtn = new System.Windows.Controls.Button { Content = "Install", Width = 80, IsDefault = true };

        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        installBtn.Click += (_, _) => { DialogResult = true; Close(); };

        buttonPanel.Children.Add(cancelBtn);
        buttonPanel.Children.Add(installBtn);

        panel.Children.Add(buttonPanel);
        Content = panel;
    }
}
