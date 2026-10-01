using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XV.CommandCenter;

/// <summary>Small factory for the reference design's recurring visual pieces (cards, pills, mono labels).</summary>
public static class Ui
{
    public static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];
    public static FontFamily Mono => (FontFamily)Application.Current.Resources["Mono"];

    public static TextBlock T(string text, double size = 12, string color = "#F4F4F5", bool bold = false, bool mono = false, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, FontSize = size, Foreground = B(color), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        if (bold) t.FontWeight = FontWeights.Bold;
        if (weight != null) t.FontWeight = weight.Value;
        if (mono) t.FontFamily = Mono;
        return t;
    }

    public static TextBlock Icon(string glyph, string color = "#A1A1AA", double size = 13) =>
        new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size, Foreground = B(color), VerticalAlignment = VerticalAlignment.Center };

    public static Border Pill(string text, string fg, string bg, string border, double size = 10)
    {
        var tb = T(text, size, fg, bold: true, mono: true);
        return new Border { Child = tb, Background = B(bg), BorderBrush = B(border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 1), VerticalAlignment = VerticalAlignment.Center };
    }

    public static Border Card(UIElement child, string bg = "#141417", string border = "#27272A", double pad = 14) =>
        new() { Child = child, Background = B(bg), BorderBrush = B(border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(pad) };

    public static StackPanel Row(params UIElement[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    public static StackPanel Col(params UIElement[] items)
    {
        var sp = new StackPanel();
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    public static DockPanel Spread(UIElement left, UIElement right)
    {
        var d = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(left, Dock.Left); DockPanel.SetDock(right, Dock.Right);
        d.Children.Add(left); d.Children.Add(right);
        return d;
    }

    /// <summary>Details on the left (wrapping), actions on the right at their natural width.</summary>
    public static Grid SpreadWrap(UIElement left, UIElement right)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (right is FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(12, 0, 0, 0); }
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 1);
        g.Children.Add(left); g.Children.Add(right);
        return g;
    }

    public static TextBlock Wrap(this TextBlock t) { t.TextWrapping = TextWrapping.Wrap; return t; }

    /// <summary>Joins the non-empty parts with " • " (no stray separators for blank fields).</summary>
    public static string Parts(params string[] parts) => string.Join("  •  ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>"Label: value", or empty when the value is blank.</summary>
    public static string Labeled(string label, string value) => string.IsNullOrWhiteSpace(value) ? "" : $"{label}: {value}";

    public static T M<T>(this T e, double l, double t = 0, double r = 0, double b = 0) where T : FrameworkElement { e.Margin = new Thickness(l, t, r, b); return e; }

    public static Button Btn(string text, RoutedEventHandler click, string style = "BtnSmall", string? tip = null)
    {
        var b = new Button { Content = text, Style = (Style)Application.Current.Resources[style], Margin = new Thickness(0, 0, 6, 0) };
        if (tip != null) b.ToolTip = tip;
        b.Click += click;
        return b;
    }

    public static Border Divider() => new() { Height = 1, Background = B("#27272A"), Margin = new Thickness(0, 10, 0, 8) };

    public static string S(object? o) => o?.ToString() ?? "";
    public static long L(object? o) => o == null ? 0 : Convert.ToInt64(o, CultureInfo.InvariantCulture);

    public static string Time(long ms, string fmt = "HH:mm:ss") => ms <= 0 ? "—" : DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString(fmt);

    /// <summary>"Yd Zh Wm" breakdown for stay durations (days only once the stay is long enough); with "Show months in
    /// durations" on in Stations &amp; Settings, 30+ days are folded into "Xmo Yd Zh Wm" instead of piling up days.</summary>
    public static string Duration(long ms)
    {
        if (ms <= 0) return "0m";
        var t = TimeSpan.FromMilliseconds(ms);
        var months = App.Settings.ShowMonthsInDuration ? t.Days / 30 : 0;
        var days = months > 0 ? t.Days % 30 : t.Days;
        var parts = new List<string>();
        if (months > 0) parts.Add($"{months}mo");
        if (months > 0 || days > 0) parts.Add($"{days}d");
        parts.Add($"{t.Hours}h"); parts.Add($"{t.Minutes}m");
        return string.Join(" ", parts);
    }

    public static string Ago(long ms)
    {
        if (ms <= 0) return "never";
        var s = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ms) / 1000;
        return s < 60 ? $"{s}s ago" : s < 3600 ? $"{s / 60}m ago" : s < 86400 ? $"{s / 3600}h ago" : $"{s / 86400}d ago";
    }

    /// <summary>Display form of canonical IDs: P001 → P-001.</summary>
    /// <summary>"Reason — remarks (From: X)" of a record followed by the separator, or empty.</summary>
    public static string Note(Dictionary<string, object?> r, string sep)
    {
        var reason = r.TryGetValue("reason", out var x) ? S(x) : "";
        var remarks = r.TryGetValue("remarks", out var y) ? S(y) : "";
        var comingFrom = r.TryGetValue("coming_from", out var z) ? S(z) : "";
        var text = reason.Length > 0 && remarks.Length > 0 ? $"{reason} — {remarks}" : reason + remarks;
        if (comingFrom.Length > 0) text = (text.Length > 0 ? text + "  " : "") + $"(From: {comingFrom})";
        return text.Length > 0 ? text + sep : "";
    }

    public static string DisplayId(string id) => id.Length > 1 && char.IsLetter(id[0]) && char.IsDigit(id[1]) ? id[0] + "-" + id[1..] : id;

    public static TextBlock Empty(string text) => new() { Text = text, Foreground = B("#71717A"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 60), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
}
