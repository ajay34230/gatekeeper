using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// The pop-up for people who have not come back from leave / TD (overdue) or are due back soon. Each person gets a card with
/// a headline that follows the reason they left, e.g. "Capt Rajesh Kumar has not returned from leave".
/// </summary>
public sealed class AbsenceAlertWindow : Window
{
    static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public AbsenceAlertWindow(List<Dictionary<string, object?>> items, bool overdue, Window? main)
    {
        Title = overdue ? "Not returned" : "Due back soon";
        Width = 600; SizeToContent = SizeToContent.Height; MaxHeight = Math.Min(760, SystemParameters.WorkArea.Height - 40);
        Topmost = true; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = B(overdue ? "#FFF5F6" : "#FFFBEB");
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        string deep = overdue ? "#9F1239" : "#92400E", bright = overdue ? "#F43F5E" : "#F59E0B", soft = overdue ? "#FFE4E6" : "#FEF3C7", edge = overdue ? "#FDA4AF" : "#FCD34D";

        var close = new Button
        {
            Content = "✕", FontSize = 16, Foreground = Brushes.White, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Close",
        };
        close.Click += (_, _) => Close();
        var count = items.Count;
        var title = overdue
            ? (count == 1 ? "1 PERSON HAS NOT RETURNED" : $"{count} PEOPLE HAVE NOT RETURNED")
            : (count == 1 ? "1 PERSON IS DUE BACK SOON" : $"{count} PEOPLE ARE DUE BACK SOON");
        var sub = overdue ? "They are past the expected return date." : "Expected back within the next 24 hours. Not overdue yet.";
        var headText = Col(T(title, 17, "#FFFFFF", bold: true), T(sub, 11.5, "#FFE4E6").M(0, 3, 0, 0));
        if (!overdue) { headText = Col(T(title, 17, "#FFFFFF", bold: true), T(sub, 11.5, "#FEF3C7").M(0, 3, 0, 0)); }
        var head = new Grid { Margin = new Thickness(0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var glyph = T(overdue ? "⚠" : "⏰", 28, "#FFFFFF", bold: true); glyph.VerticalAlignment = VerticalAlignment.Center; glyph.Margin = new Thickness(0, 0, 14, 0);
        Grid.SetColumn(glyph, 0); Grid.SetColumn(headText, 1); Grid.SetColumn(close, 2);
        headText.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(glyph); head.Children.Add(headText); head.Children.Add(close);
        var headBand = new Border
        {
            Padding = new Thickness(22, 16, 12, 16), Child = head,
            Background = new LinearGradientBrush(Hex(deep), Hex(bright), 0),
        };

        var list = new StackPanel { Margin = new Thickness(0) };
        foreach (var a in items.Take(8)) list.Children.Add(PersonCard(a, overdue, deep, bright, soft, edge).M(0, 0, 0, 10));
        if (items.Count > 8) list.Children.Add(T($"… and {items.Count - 8} more in the Leave & Overdue list", 12, deep, bold: true).M(4, 2, 0, 6));

        var footer = new Border
        {
            Padding = new Thickness(20, 12, 20, 14), BorderBrush = B(edge), BorderThickness = new Thickness(0, 1, 0, 0), Background = B(soft),
            Child = Row(Btn("Open Leave & Overdue list", (_, _) => { Close(); if (main != null) LeaveWindow.ShowWindow(main, overdue); }, "BtnAmber"), Btn("Dismiss", (_, _) => Close())),
        };

        var dock = new DockPanel();
        DockPanel.SetDock(headBand, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(headBand); dock.Children.Add(footer);
        dock.Children.Add(new ScrollViewer { Content = list, Padding = new Thickness(20, 16, 20, 6), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;
    }

    Border PersonCard(Dictionary<string, object?> a, bool overdue, string deep, string bright, string soft, string edge)
    {
        var visitor = S(a["kind"]) == "VISITOR_OVERSTAY";
        var rank = visitor ? "" : S(a["rank"]);
        var name = S(a["name"]);
        var who = (rank + " " + name).Trim();
        var phrase = overdue ? AbsenceText.NotReturned(S(a["reason"]), S(a["remarks"]), S(a["kind"])) : AbsenceText.DueBack(S(a["reason"]), S(a["remarks"]));
        var due = L(a["expected_return"]); var left = L(a["left_at"]); var now = Store.NowMs;
        var id = S(a["person_id"]);

        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        var avatarText = T(initials.Length > 0 ? initials : "?", 15, "#FFFFFF", bold: true);
        avatarText.HorizontalAlignment = HorizontalAlignment.Center; avatarText.VerticalAlignment = VerticalAlignment.Center;
        var avatar = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0),
            Background = new LinearGradientBrush(Hex(deep), Hex(bright), 45), Child = avatarText,
        };

        var headline = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 15.5, Foreground = B("#0F172A"), LineHeight = 22 };
        headline.Inlines.Add(new Run(who) { FontWeight = FontWeights.Bold });
        headline.Inlines.Add(new Run(" " + phrase));

        var pills = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Pill1(UIElement p) { if (p is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 6, 6); pills.Children.Add(p); }
        if (overdue) Pill1(Pill((visitor ? "OVER BY " : "OVERDUE ") + Duration(now - due), deep, soft, edge, 10.5));
        else Pill1(Pill("DUE IN " + Duration(due - now), deep, soft, edge, 10.5));
        if (left > 0) Pill1(Pill((visitor ? "Came in " : "Left ") + Time(left, "dd MMM HH:mm"), "#475569", "#F1F5F9", "#CBD5E1", 10.5));
        if (due > 0) Pill1(Pill((visitor ? "Pass ended " : "Expected back ") + Time(due, "dd MMM yyyy"), "#92400E", "#FEF3C7", "#FDE68A", 10.5));

        var detail = string.Join("  •  ", new[] { visitor ? "Visitor" : S(a["company"]).Length > 0 ? S(a["company"]) + " Company" : "", S(a["platoon"]).Length > 0 ? S(a["platoon"]) + " Platoon" : "", DisplayId(id), S(a["mobile"]) }.Where(x => x.Length > 0));
        var remarks = S(a["remarks"]);

        var text = Col(headline);
        if (detail.Length > 0) text.Children.Add(T(detail, 11.5, "#64748B", mono: true).M(0, 3, 0, 0));
        text.Children.Add(pills);
        if (remarks.Length > 0 && !visitor) text.Children.Add(T("“" + remarks + "”", 11.5, "#64748B").Wrap().M(0, 0, 0, 4));
        if (!visitor) text.Children.Add(Btn("View history", (_, _) => { Dialogs.History(this, "PERSON", id); }).M(0, 4, 0, 0));

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(avatar, 0); Grid.SetColumn(text, 1);
        row.Children.Add(avatar); row.Children.Add(text);
        return new Border
        {
            Background = Brushes.White, BorderBrush = B(edge), BorderThickness = new Thickness(1, 1, 1, 1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 14, 16, 10),
            Child = new Border { BorderBrush = B(bright), BorderThickness = new Thickness(0), Child = row },
        };
    }
}
