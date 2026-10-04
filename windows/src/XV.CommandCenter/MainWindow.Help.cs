using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

public partial class MainWindow
{
    static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0";

    /// <summary>Writes the PDF guide (with the bundled sample screenshots) and returns its path, or null when it could not be written.</summary>
    static string? WriteGuide(string path, Window owner)
    {
        try
        {
            var shots = Path.Combine(AppContext.BaseDirectory, "guide-shots");
            Reports.GuidePdf(path, Directory.Exists(shots) ? shots : null, AppVersion, App.Settings.ServerName);
            return path;
        }
        catch (Exception ex) { MessageBox.Show(owner, "The guide could not be created: " + ex.Message, "User guide"); return null; }
    }

    void RenderHelp()
    {
        Header("HELP • HOW TO USE THE SOFTWARE", "", "Every function of the server and the gate phones, with the steps to follow. The full guide with pictures is a PDF.");
        SectionActions.Children.Add(Btn("Open full guide (PDF)", (_, _) =>
        {
            var path = WriteGuide(Path.Combine(Path.GetTempPath(), "XV-User-Guide.pdf"), this);
            if (path != null) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }, "BtnAmber"));
        SectionActions.Children.Add(Btn("Save guide as…", (_, _) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "XV-User-Guide.pdf", Filter = "PDF|*.pdf" };
            if (dlg.ShowDialog() == true && WriteGuide(dlg.FileName, this) != null) MessageBox.Show(this, "Guide saved.", "User guide");
        }, "BtnEmerald"));

        var q = Query.ToLowerInvariant();
        var panel = new StackPanel();
        foreach (var s in GuideContent.Sections)
        {
            if (q.Length > 0 && !(s.Title + " " + s.Intro + " " + string.Join(" ", s.Steps)).ToLowerInvariant().Contains(q)) continue;
            var body = new StackPanel();
            body.Children.Add(T(s.Title, 14, "#0F172A", bold: true));
            body.Children.Add(T(s.Intro, 12, "#475569").Wrap().M(0, 4, 0, 6));
            for (var i = 0; i < s.Steps.Length; i++)
                body.Children.Add(T($"{i + 1}.  {s.Steps[i]}", 11.5, "#0F172A").Wrap().M(0, 0, 0, 3));
            foreach (var tip in s.Tips)
                body.Children.Add(Card(T("Note:  " + tip, 11, "#92400E").Wrap(), "#FEF3C7", "#FDE68A", 8).M(0, 6, 0, 0));
            panel.Children.Add(Card(body, "#FFFFFF").M(0, 0, 0, 10));
        }
        if (panel.Children.Count == 0) panel.Children.Add(Empty("No help topic matches the search."));
        ContentHost.Content = panel;
    }

    void RenderAbout()
    {
        Header("ABOUT", "", "What this software is, what it does and who made it.");
        var panel = new StackPanel();
        panel.Children.Add(Card(Col(
            T(GuideContent.Product, 20, "#0F172A", bold: true),
            T("Command Center (Windows) • Gatekeeper (Android) • Encrypted local server", 12, "#475569").M(0, 2, 0, 8),
            Kv("Version", AppVersion),
            Kv("Created by", GuideContent.Maker),
            Kv("This server", App.Settings.ServerName)), "#FFFFFF").M(0, 0, 0, 10));

        void Block(string title, params string[] lines)
        {
            var sp = new StackPanel();
            sp.Children.Add(T(title, 13, "#0F172A", bold: true).M(0, 0, 0, 6));
            foreach (var l in lines) sp.Children.Add(T("•  " + l, 11.5, "#334155").Wrap().M(0, 0, 0, 3));
            panel.Children.Add(Card(sp, "#FFFFFF").M(0, 0, 0, 10));
        }
        Block("What it is",
            "A gate and base access-control system. Guards scan ID-card and vehicle QR codes on an Android phone; this Command Center is the server that receives, stores and shows every record.",
            "It works on the base network without the internet, and can optionally reach phones over a secure internet tunnel (Cloud Link).");
        Block("What it does",
            "Records every entry and exit of people and vehicles, with the reason, remarks, who recorded it and where.",
            "Keeps a registry of soldiers and vehicles with photos, import and export of Excel and CSV files.",
            "Designs and prints ID cards with a genuine QR code, and keeps a register of issued, re-issued and lost cards.",
            "Issues visitor and temporary passes that work only inside a validity window.",
            "Tracks leave and TD and alerts when someone has not returned.",
            "Watches vehicles between locations: standard and average times, not-reached alerts for the server and the destination RP, with 15-minute repeat until the trip is resolved.",
            "Draws route charts of people and vehicles, with driver and co-driver, and exports them as one-page PDF or Excel.",
            "Messages, alerts, calls and SOS between the server and the phones.",
            "Encrypted backup and restore, and a tamper-evident audit trail.");
        Block("Security",
            "The database is encrypted (SQLCipher). Phones talk to the server over TLS, and every request is also sealed with AES-256-GCM.",
            "Each phone is paired with a QR code, has its own key, and can be revoked and remotely wiped.",
            "Replayed or forged requests are refused. Pairing attempts are rate limited.",
            "The audit trail is chained: any edit or removal is detected by Verify integrity.",
            "Outbound internet access of the server is blocked unless you turn it on.");
        Block("Made by",
            $"{GuideContent.Maker} designed and built this software: the Windows Command Center, the Android Gatekeeper app and the encrypted server.",
            "For how to use it open the Help tab.");
        ContentHost.Content = panel;
    }
}
