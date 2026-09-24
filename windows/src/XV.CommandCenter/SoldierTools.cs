using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using XV.Core;
using static XV.CommandCenter.Ui;

namespace XV.CommandCenter;

/// <summary>
/// Chooses which soldiers an action covers: individual / multiple (tick list with search), company, platoon, section or everyone.
/// Company, platoon and section lists come from the real register.
/// </summary>
public sealed class ScopePicker : StackPanel
{
    readonly RadioButton _all = R("Entire register"), _company = R("Company-wise"), _platoon = R("Platoon-wise"), _section = R("Section-wise"), _selected = R("Individual / multiple soldiers");
    readonly ComboBox _cCompany = new() { MinWidth = 150 }, _cPlatoon = new() { MinWidth = 150 }, _cSection = new() { MinWidth = 150 };
    readonly TextBox _search = new() { ToolTip = "Search by ID, army no, name, rank, platoon…" };
    readonly StackPanel _list = new();
    readonly TextBlock _count = new() { Foreground = B("#FBBF24"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
    readonly HashSet<string> _ticked = [];
    readonly List<Dictionary<string, object?>> _people;
    public event Action? Changed;

    static RadioButton R(string t) => new() { Content = t, GroupName = "scope" + Guid.NewGuid(), Foreground = B("#E4E4E7"), Margin = new Thickness(0, 0, 18, 6), FontSize = 12.5 };

    public ScopePicker(IEnumerable<string>? preselected = null)
    {
        _people = App.Store.Persons();
        var group = "scope-" + Guid.NewGuid();
        foreach (var rb in new[] { _all, _company, _platoon, _section, _selected }) { rb.GroupName = group; rb.Checked += (_, _) => Update(); }
        var modes = new WrapPanel();
        foreach (var rb in new[] { _all, _company, _platoon, _section, _selected }) modes.Children.Add(rb);
        Children.Add(modes);
        var selects = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        selects.Children.Add(Labeled("Company", _cCompany)); selects.Children.Add(Labeled("Platoon", _cPlatoon)); selects.Children.Add(Labeled("Section", _cSection));
        Children.Add(selects);
        Children.Add(_search.M(0, 8, 0, 4));
        Children.Add(new Border { BorderBrush = B("#27272A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = B("#0C0C0E"),
            Child = new ScrollViewer { Content = _list, MaxHeight = 220, Padding = new Thickness(8, 6, 8, 6) } });
        var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        tools.Children.Add(Btn("Tick all shown", (_, _) => { foreach (var p in Shown()) _ticked.Add(S(p["id"])); FillList(); Update(); }));
        tools.Children.Add(Btn("Clear ticks", (_, _) => { _ticked.Clear(); FillList(); Update(); }).M(6));
        Children.Add(tools);
        Children.Add(_count);

        foreach (var c in SoldierFile.Companies.Concat(_people.Select(p => S(p["company"])).Where(c => c.Length > 0)).Distinct(StringComparer.OrdinalIgnoreCase)) _cCompany.Items.Add(c);
        _cCompany.SelectionChanged += (_, _) => { FillPlatoons(); Update(); };
        _cPlatoon.SelectionChanged += (_, _) => { FillSections(); Update(); };
        _cSection.SelectionChanged += (_, _) => Update();
        _search.TextChanged += (_, _) => FillList();

        if (preselected?.Any() == true) { foreach (var id in preselected) _ticked.Add(id); _selected.IsChecked = true; }
        else _all.IsChecked = true;
        if (_cCompany.Items.Count > 0) _cCompany.SelectedIndex = 0;
        FillList();
        Update();
    }

    static StackPanel Labeled(string label, Control c) => new StackPanel { Margin = new Thickness(0, 0, 12, 0), Children = { T(label.ToUpperInvariant(), 10, "#A1A1AA", bold: true).M(0, 0, 0, 3), c } };

    string Company => _cCompany.SelectedItem as string ?? "";
    string Platoon => _cPlatoon.SelectedItem as string ?? "";
    string Section => _cSection.SelectedItem as string ?? "";

    void FillPlatoons()
    {
        _cPlatoon.Items.Clear();
        foreach (var p in _people.Where(p => S(p["company"]).Equals(Company, StringComparison.OrdinalIgnoreCase)).Select(p => S(p["platoon"])).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            _cPlatoon.Items.Add(p);
        if (_cPlatoon.Items.Count > 0) _cPlatoon.SelectedIndex = 0;
        FillSections();
    }

    void FillSections()
    {
        _cSection.Items.Clear();
        foreach (var s in _people.Where(p => S(p["company"]).Equals(Company, StringComparison.OrdinalIgnoreCase) && S(p["platoon"]).Equals(Platoon, StringComparison.OrdinalIgnoreCase))
                     .Select(p => S(p["section"])).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            _cSection.Items.Add(s);
        if (_cSection.Items.Count > 0) _cSection.SelectedIndex = 0;
    }

    IEnumerable<Dictionary<string, object?>> Shown()
    {
        var q = _search.Text.Trim();
        return _people.Where(p => q.Length == 0 || string.Join(" ", S(p["id"]), S(p["service_no"]), S(p["name"]), S(p["rank"]), S(p["company"]), S(p["platoon"]), S(p["section"]))
            .Contains(q, StringComparison.OrdinalIgnoreCase)).Take(500);
    }

    void FillList()
    {
        _list.Children.Clear();
        foreach (var p in Shown())
        {
            var id = S(p["id"]);
            var cb = new CheckBox
            {
                IsChecked = _ticked.Contains(id), Margin = new Thickness(0, 2, 0, 2),
                Content = $"{DisplayId(id)}   {S(p["rank"])} {S(p["name"])}   •   {S(p["company"])} {S(p["platoon"])} {S(p["section"])}".Trim(),
            };
            cb.Checked += (_, _) => { _ticked.Add(id); _selected.IsChecked = true; Update(); };
            cb.Unchecked += (_, _) => { _ticked.Remove(id); Update(); };
            _list.Children.Add(cb);
        }
        if (_list.Children.Count == 0) _list.Children.Add(T(_people.Count == 0 ? "The register is empty — add soldiers or import a file first." : "No soldier matches the search.", 12, "#71717A"));
    }

    void Update()
    {
        _cCompany.IsEnabled = _company.IsChecked == true || _platoon.IsChecked == true || _section.IsChecked == true;
        _cPlatoon.IsEnabled = _platoon.IsChecked == true || _section.IsChecked == true;
        _cSection.IsEnabled = _section.IsChecked == true;
        var n = Scope.Apply(_people).Count;
        _count.Text = $"{Scope.Describe()} — {n} soldier(s)";
        Changed?.Invoke();
    }

    public SoldierScope Scope =>
        _company.IsChecked == true ? new SoldierScope("COMPANY", Company) :
        _platoon.IsChecked == true ? new SoldierScope("PLATOON", Company, Platoon) :
        _section.IsChecked == true ? new SoldierScope("SECTION", Company, Platoon, Section) :
        _selected.IsChecked == true ? new SoldierScope("SELECTED", Ids: _ticked.ToList()) : SoldierScope.All;

    /// <summary>The chosen soldiers, in register order.</summary>
    public List<Dictionary<string, object?>> Soldiers => Scope.Apply(App.Store.Persons());
}

/// <summary>Soldier register: export (CSV / Excel, chosen soldiers), blank templates, import with new-column handling, photos from a folder.</summary>
public static class SoldierRegister
{
    sealed class ExportDialog : DarkWindow
    {
        public ExportDialog(IEnumerable<string>? preselected) : base("Export Soldier Details",
            "All soldier details (including your custom fields) in the same layout the import accepts. Choose who to include.", 760, 820)
        {
            var picker = new ScopePicker(preselected);
            Body.Children.Add(Label("Soldiers"));
            Body.Children.Add(picker);
            void Go(string ext)
            {
                var list = picker.Soldiers;
                if (list.Count == 0) { MessageBox.Show("No soldier matches this selection."); return; }
                if (!AdminGate.Require(this, $"Export soldier details ({picker.Scope.Describe()}, {list.Count})")) return;
                var dlg = new SaveFileDialog { FileName = $"XV-Soldiers-{Safe(picker.Scope.Describe())}-{DateTime.Now:yyyyMMdd}{ext}", Filter = ext == ".xlsx" ? "Excel workbook|*.xlsx" : "CSV|*.csv" };
                if (dlg.ShowDialog() != true) return;
                try
                {
                    SoldierFile.Export(list, App.Settings.CustomFields, dlg.FileName, picker.Scope.Describe());
                    if (MessageBox.Show($"Exported {list.Count} soldier(s). Open the file now?", "Export finished", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
                catch (Exception ex) { Fail(ex); }
            }
            AddButton("Close", Close);
            AddButton("Export CSV", () => Go(".csv"));
            AddButton("Export Excel", () => Go(".xlsx"), "BtnEmerald");
        }
    }

    static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    public static void Export(Window owner, IEnumerable<string>? preselected = null) => new ExportDialog(preselected) { Owner = owner }.ShowDialog();

    public static void Template(Window owner)
    {
        var dlg = new SaveFileDialog { FileName = "XV-Soldiers-Template.xlsx", Filter = "Excel workbook|*.xlsx|CSV|*.csv" };
        if (dlg.ShowDialog() != true) return;
        try { SoldierFile.Export([], App.Settings.CustomFields, dlg.FileName, "Blank template"); MessageBox.Show("Template saved. Fill one soldier per row, then use Import.", "Template"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Template"); }
    }

    public static void Import(Window owner)
    {
        var dlg = new OpenFileDialog { Filter = "Soldier register (Excel or CSV)|*.xlsx;*.csv" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var (headers, rows) = SoldierFile.Read(dlg.FileName);
            if (rows.Count == 0) { MessageBox.Show("The file has no soldier rows.", "Import"); return; }
            var unknown = SoldierFile.UnknownHeaders(headers, App.Settings.CustomFields);
            if (unknown.Count > 0)
            {
                var ans = MessageBox.Show($"These columns are not soldier details yet:\n\n  {string.Join("\n  ", unknown)}\n\nAdd them as new custom fields (shown in forms, exports and reports)?\nChoose No to import without them.",
                    "New columns", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (ans == MessageBoxResult.Cancel) return;
                if (ans == MessageBoxResult.Yes) { App.Settings.CustomFields.AddRange(unknown.Where(u => u.Length <= 40)); App.Settings.Save(); }
            }
            var (added, updated, errors) = SoldierFile.Import(App.Store, headers, rows, App.Settings.CustomFields);
            MessageBox.Show($"Added {added} and updated {updated} soldier(s)." + (errors.Count > 0 ? $"\n\n{errors.Count} row(s) skipped:\n" + string.Join("\n", errors.Take(15)) : ""), "Import finished");
        }
        catch (IOException) { MessageBox.Show("The file is open in another program (e.g. Excel). Close it and try again.", "Import"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Import"); }
    }

    /// <summary>Loads photos named after the soldier's Personnel ID or Army No (e.g. P001.jpg, JC-784912X.png).</summary>
    public static void ImportPhotos(Window owner)
    {
        var dlg = new OpenFolderDialog { Title = "Folder with soldier photos (file name = Personnel ID or Army No)" };
        if (dlg.ShowDialog() != true) return;
        var people = App.Store.Persons();
        int ok = 0; var skipped = new List<string>();
        foreach (var f in Directory.EnumerateFiles(dlg.FolderName).Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLowerInvariant())))
        {
            var key = Path.GetFileNameWithoutExtension(f).Trim();
            var p = people.FirstOrDefault(x => S(x["id"]).Equals(Store.CanonId(key), StringComparison.OrdinalIgnoreCase) ||
                                                (S(x["service_no"]).Length > 0 && Store.CanonId(S(x["service_no"])) == Store.CanonId(key)));
            if (p == null) { skipped.Add(Path.GetFileName(f)); continue; }
            try { App.Store.SetPersonPhoto(S(p["id"]), Photo.Prepare(File.ReadAllBytes(f))); ok++; }
            catch { skipped.Add(Path.GetFileName(f) + " (not a readable image)"); }
        }
        MessageBox.Show($"Loaded {ok} photo(s)." + (skipped.Count > 0 ? $"\n\nNot matched ({skipped.Count}):\n" + string.Join("\n", skipped.Take(15)) : ""), "Photos");
    }
}

/// <summary>Soldier photos: any image is cropped to a 4:5 passport frame and stored as a 480×600 JPEG.</summary>
public static class Photo
{
    public static byte[] Prepare(byte[] input)
    {
        var src = new BitmapImage();
        using (var ms = new MemoryStream(input)) { src.BeginInit(); src.CacheOption = BitmapCacheOption.OnLoad; src.StreamSource = ms; src.EndInit(); }
        src.Freeze();
        double w = src.PixelWidth, h = src.PixelHeight, target = 4.0 / 5.0;
        var crop = w / h > target ? new Int32Rect((int)((w - h * target) / 2), 0, (int)(h * target), (int)h) : new Int32Rect(0, (int)((h - w / target) / 5), (int)w, (int)(w / target));
        var cropped = new CroppedBitmap(src, crop);
        var scaled = new TransformedBitmap(cropped, new System.Windows.Media.ScaleTransform(480.0 / crop.Width, 600.0 / crop.Height));
        var enc = new JpegBitmapEncoder { QualityLevel = 88 };
        enc.Frames.Add(BitmapFrame.Create(scaled));
        using var outMs = new MemoryStream();
        enc.Save(outMs);
        return outMs.ToArray();
    }

    public static BitmapImage? Image(byte[]? bytes) => bytes == null ? null : Dialogs.Png(bytes);
}
