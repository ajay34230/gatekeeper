using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace XV.Core;

/// <summary>What to include in an export. Empty PersonIds = everyone matching Company.</summary>
public sealed record ReportRequest(string Company, IReadOnlyCollection<string> PersonIds, DateTime From, DateTime To, bool IncludeRecords = true)
{
    public long FromMs => new DateTimeOffset(From.Date).ToUnixTimeMilliseconds();
    public long ToMs => new DateTimeOffset(To.Date.AddDays(1)).ToUnixTimeMilliseconds() - 1;
}

/// <summary>Personnel &amp; gate-record reports in Excel (company-wise sheets), PDF and CSV.</summary>
public static class Reports
{
    public static readonly string[] Companies = ["Alpha", "Bravo", "Charlie", "Delta", "SP", "HQ"];

    /// <summary>Signature colour per company, used for sheet tabs, headings and summary cells.</summary>
    public static string CompanyColor(string company) => company.ToUpperInvariant() switch
    {
        "ALPHA" => "#1D4ED8", "BRAVO" => "#047857", "CHARLIE" => "#B45309",
        "DELTA" => "#6D28D9", "SP" => "#B91C1C", "HQ" => "#0F172A", _ => "#52525B",
    };

    const string Ink = "#18181B", HeadFill = "#27272A", Band = "#F4F4F5", Amber = "#F59E0B";

    static string S(object? o) => o?.ToString() ?? "";
    static long L(object? o) => o == null ? 0 : Convert.ToInt64(o, CultureInfo.InvariantCulture);
    static DateTime Local(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
    static string Id(string id) => id.Length > 1 && char.IsLetter(id[0]) && char.IsDigit(id[1]) ? id[0] + "-" + id[1..] : id;

    public static string Duration(long ms)
    {
        if (ms <= 0) return "";
        var t = TimeSpan.FromMilliseconds(ms);
        return (t.Days > 0 ? $"{t.Days}d " : "") + $"{t.Hours}h {t.Minutes}m";
    }

    static string CompanyOf(Dictionary<string, object?> p) =>
        Companies.FirstOrDefault(c => string.Equals(c, S(p["company"]), StringComparison.OrdinalIgnoreCase)) ?? (S(p["company"]).Length > 0 ? S(p["company"]) : "Unassigned");

    static Dictionary<string, string> Custom(Dictionary<string, object?> p)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(S(p["custom_json"]).Length > 1 ? S(p["custom_json"]) : "{}") ?? []; }
        catch { return []; }
    }

    sealed record Group(string Company, List<Dictionary<string, object?>> Persons, List<Dictionary<string, object?>> Records);

    static List<Group> Build(Store store, ReportRequest req, out List<Dictionary<string, object?>> allPersons)
    {
        var (persons, records) = store.ReportData(req.PersonIds, req.Company, req.FromMs, req.ToMs);
        allPersons = persons;
        var byId = persons.ToDictionary(p => S(p["id"]));
        return persons.GroupBy(CompanyOf)
            .OrderBy(g => Array.FindIndex(Companies, c => c == g.Key) is var i && i >= 0 ? i : 99).ThenBy(g => g.Key)
            .Select(g => new Group(g.Key, g.OrderBy(p => S(p["id"])).ToList(),
                req.IncludeRecords ? records.Where(r => byId.TryGetValue(S(r["entity_id"]), out var p) && CompanyOf(p) == g.Key).ToList() : []))
            .ToList();
    }

    static string Scope(ReportRequest req) =>
        (req.PersonIds.Count > 0 ? $"{req.PersonIds.Count} selected person(s)" : req.Company is "" or "ALL" ? "All companies" : req.Company + " Company")
        + $" • {req.From:dd MMM yyyy} – {req.To:dd MMM yyyy}";

    static string Presence(Dictionary<string, object?> p) => L(p["inside_since"]) > 0 ? "INSIDE since " + Local(L(p["inside_since"])).ToString("dd MMM HH:mm") : "OUTSIDE";

    // ------------------------------------------------------------------ Excel

    public static void Excel(Store store, ReportRequest req, string path)
    {
        var groups = Build(store, req, out var persons);
        var custom = store.Settings.CustomFields;
        using var wb = new XLWorkbook();
        wb.Properties.Title = "XV Digital Access Control Report";
        wb.Properties.Author = "XV Command Center";

        // Summary sheet
        var sum = wb.Worksheets.Add("Summary");
        sum.TabColor = XLColor.FromHtml(Amber);
        Title(sum, 1, 8, "XV DIGITAL ACCESS CONTROL — PERSONNEL & GATE RECORDS", Ink);
        sum.Cell(2, 1).Value = $"{Scope(req)}   •   Generated {DateTime.Now:dd MMM yyyy HH:mm}   •   {store.Settings.ServerName}";
        sum.Range(2, 1, 2, 8).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#52525B")).Font.SetItalic();
        string[] sh = ["Company", "Personnel", "Inside now", "Entries", "Exits", "Other records", "Location flags", "Total records"];
        Header(sum, 4, sh);
        var row = 5;
        foreach (var g in groups)
        {
            var c = sum.Cell(row, 1); c.Value = g.Company;
            c.Style.Fill.SetBackgroundColor(XLColor.FromHtml(CompanyColor(g.Company))).Font.SetFontColor(XLColor.White).Font.SetBold();
            sum.Cell(row, 2).Value = g.Persons.Count;
            sum.Cell(row, 3).Value = g.Persons.Count(p => L(p["inside_since"]) > 0);
            sum.Cell(row, 4).Value = g.Records.Count(r => S(r["event_type"]) == "ENTRY");
            sum.Cell(row, 5).Value = g.Records.Count(r => S(r["event_type"]) == "EXIT");
            sum.Cell(row, 6).Value = g.Records.Count(r => S(r["event_type"]) is not ("ENTRY" or "EXIT"));
            sum.Cell(row, 7).Value = g.Records.Count(r => L(r["loc_mismatch"]) == 1);
            sum.Cell(row, 8).Value = g.Records.Count;
            if (row % 2 == 0) sum.Range(row, 2, row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            row++;
        }
        sum.Cell(row, 1).Value = "TOTAL";
        for (var col = 2; col <= 8; col++) sum.Cell(row, col).FormulaA1 = $"SUM({sum.Cell(5, col).Address}:{sum.Cell(row - 1, col).Address})";
        sum.Range(row, 1, row, 8).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#FEF3C7")).Border.SetTopBorder(XLBorderStyleValues.Medium);
        Grid(sum.Range(4, 1, row, 8));
        sum.Columns(1, 8).Width = 16; sum.Column(1).Width = 20;
        sum.SheetView.FreezeRows(4);

        // One sheet per company: roster, then records
        foreach (var g in groups)
        {
            var color = CompanyColor(g.Company);
            var ws = wb.Worksheets.Add(SafeSheet(g.Company));
            ws.TabColor = XLColor.FromHtml(color);
            string[] rosterHead = ["ID", "Name", "Rank", "Service No", "Unit", "Role", "Category", "Status", "Presence", "Last seen", .. custom];
            var width = Math.Max(rosterHead.Length, 13);
            Title(ws, 1, width, $"{g.Company.ToUpperInvariant()} COMPANY", color);
            ws.Cell(2, 1).Value = $"{g.Persons.Count} personnel • {g.Records.Count} records • {Scope(req)}";
            ws.Range(2, 1, 2, width).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#52525B")).Font.SetItalic();

            Section(ws, 4, width, "PERSONNEL ROSTER", color);
            Header(ws, 5, rosterHead);
            var r = 6;
            foreach (var p in g.Persons)
            {
                var cf = Custom(p);
                object?[] vals = [Id(S(p["id"])), S(p["name"]), S(p["rank"]), S(p["service_no"]), S(p["unit"]), S(p["role"]), S(p["category"]), S(p["status"]), Presence(p),
                    L(p["last_seen"]) > 0 ? Local(L(p["last_seen"])).ToString("dd MMM yyyy HH:mm") : "No gate activity", .. custom.Select(f => cf.GetValueOrDefault(f, ""))];
                for (var i = 0; i < vals.Length; i++) ws.Cell(r, i + 1).Value = S(vals[i]);
                if (r % 2 == 1) ws.Range(r, 1, r, rosterHead.Length).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
                StatusCell(ws.Cell(r, 8), S(p["status"]));
                ws.Cell(r, 9).Style.Font.SetFontColor(XLColor.FromHtml(L(p["inside_since"]) > 0 ? "#047857" : "#71717A")).Font.SetBold();
                ws.Cell(r, 1).Style.Font.SetBold().Font.SetFontName("Consolas");
                r++;
            }
            if (g.Persons.Count == 0) ws.Cell(r++, 1).Value = "No personnel";
            Grid(ws.Range(5, 1, r - 1, rosterHead.Length));

            if (req.IncludeRecords)
            {
                r += 2;
                Section(ws, r, width, "GATE & HISTORY RECORDS", color);
                string[] recHead = ["Date", "Time", "ID", "Name", "Rank", "Record", "Location", "Gate", "Stay", "Location flag", "Operator", "Source", "Remarks"];
                Header(ws, r + 1, recHead);
                var start = r + 1;
                r += 2;
                var names = g.Persons.ToDictionary(p => S(p["id"]));
                foreach (var e in g.Records)
                {
                    var p = names[S(e["entity_id"])];
                    var t = Local(L(e["event_ts"]));
                    object[] vals = [t.ToString("dd MMM yyyy"), t.ToString("HH:mm:ss"), Id(S(e["entity_id"])), S(p["name"]), S(p["rank"]), S(e["event_type"]),
                        S(e["location_name"]), S(e["gate_name"]), Duration(L(e["stay_ms"])), L(e["loc_mismatch"]) == 1 ? "⚠ QR: " + S(e["scanned_loc"]) : "",
                        S(e["operator_id"]), S(e["source"]) == "PC" ? "Command Center" : "Gate terminal " + S(e["device_id"]), S(e["remarks"])];
                    for (var i = 0; i < vals.Length; i++) ws.Cell(r, i + 1).Value = S(vals[i]);
                    if (r % 2 == 1) ws.Range(r, 1, r, recHead.Length).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
                    RecordCell(ws.Cell(r, 6), S(e["event_type"]));
                    if (L(e["loc_mismatch"]) == 1) ws.Cell(r, 10).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEF3C7")).Font.SetFontColor(XLColor.FromHtml("#92400E")).Font.SetBold();
                    r++;
                }
                if (g.Records.Count == 0) ws.Cell(r++, 1).Value = "No records in this period";
                Grid(ws.Range(start, 1, r - 1, recHead.Length));
                if (g.Records.Count > 0) ws.Range(start, 1, r - 1, recHead.Length).SetAutoFilter();
            }
            ws.Columns(1, width).AdjustToContents(4, 60);
            foreach (var col in ws.Columns(1, width)) col.Width = Math.Min(Math.Max(col.Width + 2, 10), 45);
            ws.SheetView.FreezeRows(2);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
        }
        wb.SaveAs(path);
    }

    static string SafeSheet(string name) => new string(name.Where(ch => !"[]*/\\?:".Contains(ch)).ToArray()) is { Length: > 0 } n ? (n.Length > 31 ? n[..31] : n) : "Sheet";

    static void Title(IXLWorksheet ws, int row, int width, string text, string color)
    {
        var r = ws.Range(row, 1, row, width);
        r.Merge();
        ws.Cell(row, 1).Value = text;
        r.Style.Fill.SetBackgroundColor(XLColor.FromHtml(color)).Font.SetFontColor(XLColor.White).Font.SetBold().Font.SetFontSize(15)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetIndent(1);
        ws.Row(row).Height = 30;
    }

    static void Section(IXLWorksheet ws, int row, int width, string text, string color)
    {
        var r = ws.Range(row, 1, row, width);
        r.Merge();
        ws.Cell(row, 1).Value = text;
        r.Style.Font.SetBold().Font.SetFontSize(11.5).Font.SetFontColor(XLColor.FromHtml(color))
            .Border.SetBottomBorder(XLBorderStyleValues.Medium).Border.SetBottomBorderColor(XLColor.FromHtml(color));
        ws.Row(row).Height = 20;
    }

    static void Header(IXLWorksheet ws, int row, string[] titles)
    {
        for (var i = 0; i < titles.Length; i++) ws.Cell(row, i + 1).Value = titles[i];
        ws.Range(row, 1, row, titles.Length).Style.Fill.SetBackgroundColor(XLColor.FromHtml(HeadFill)).Font.SetFontColor(XLColor.White).Font.SetBold()
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center).Border.SetBottomBorder(XLBorderStyleValues.Medium).Border.SetBottomBorderColor(XLColor.FromHtml(Amber));
        ws.Row(row).Height = 22;
    }

    static void Grid(IXLRange range)
    {
        range.Style.Border.SetInsideBorder(XLBorderStyleValues.Hair).Border.SetInsideBorderColor(XLColor.FromHtml("#D4D4D8"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(XLColor.FromHtml("#A1A1AA"));
    }

    static void StatusCell(IXLCell c, string status)
    {
        var (bg, fg) = status switch { "ACTIVE" => ("#D1FAE5", "#065F46"), "SUSPENDED" or "FLAGGED" => ("#FFE4E6", "#9F1239"), _ => ("#E4E4E7", "#3F3F46") };
        c.Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
    }

    static void RecordCell(IXLCell c, string type)
    {
        var (bg, fg) = type switch { "ENTRY" => ("#D1FAE5", "#065F46"), "EXIT" => ("#FEF3C7", "#92400E"), _ => ("#DBEAFE", "#1E40AF") };
        c.Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
    }

    // ------------------------------------------------------------------ PDF

    /// <summary>Loads a regular and bold sans font from the system (Segoe UI / Arial on Windows, Liberation Sans elsewhere).</summary>
    public sealed class SystemFontResolverPublic : SystemFontResolver { }

    public class SystemFontResolver : IFontResolver
    {
        static readonly (string regular, string bold)[] Candidates =
        [
            (@"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\segoeuib.ttf"),
            (@"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\arialbd.ttf"),
            ("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"),
            ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
        ];

        public FontResolverInfo ResolveTypeface(string family, bool bold, bool italic) => new(bold ? "XV-Bold" : "XV-Regular");

        public byte[] GetFont(string face)
        {
            foreach (var (regular, bold) in Candidates)
            {
                var f = face == "XV-Bold" ? bold : regular;
                if (File.Exists(f)) return File.ReadAllBytes(f);
            }
            throw new FileNotFoundException("No system font available for PDF export");
        }
    }

    static bool _fontsReady;
    static void EnsureFonts()
    {
        if (_fontsReady) return;
        GlobalFontSettings.FontResolver = new SystemFontResolver();
        _fontsReady = true;
    }

    static Color C(string hex) => Color.Parse("#FF" + hex.TrimStart('#'));

    public static void Pdf(Store store, ReportRequest req, string path)
    {
        EnsureFonts();
        var groups = Build(store, req, out var persons);
        var doc = new Document();
        doc.Info.Title = "XV Digital Access Control Report";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = "XV Sans";
        normal.Font.Size = 8;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.Orientation = Orientation.Landscape;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.3);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);

        var footer = sec.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7; footer.Format.Font.Color = C("#71717A");
        footer.AddText($"XV Digital Access Control • {store.Settings.ServerName} • Confidential • Page ");
        footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();

        // Title band
        var band = sec.AddTable();
        band.AddColumn(Unit.FromCentimeter(27));
        var br = band.AddRow(); br.Shading.Color = C(Ink); br.TopPadding = br.BottomPadding = Unit.FromPoint(8);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(8);
        var t1 = bp.AddFormattedText("XV DIGITAL ACCESS CONTROL", TextFormat.Bold); t1.Font.Size = 16; t1.Font.Color = Colors.White;
        bp.AddLineBreak();
        var t2 = bp.AddFormattedText($"Personnel & Gate Records Report  •  {Scope(req)}  •  Generated {DateTime.Now:dd MMM yyyy HH:mm}"); t2.Font.Size = 8.5; t2.Font.Color = C("#FCD34D");
        sec.AddParagraph().Format.SpaceAfter = Unit.FromPoint(6);

        // Summary table
        var sum = sec.AddTable();
        foreach (var w in new[] { 4.5, 3, 3, 3, 3, 3.5, 3.5, 3.5 }) sum.AddColumn(Unit.FromCentimeter(w));
        PdfHeader(sum, "Company", "Personnel", "Inside now", "Entries", "Exits", "Other records", "Location flags", "Total records");
        foreach (var g in groups)
        {
            var r = sum.AddRow();
            Cell(r, 0, g.Company, bold: true, color: Colors.White).Shading.Color = C(CompanyColor(g.Company));
            Cell(r, 1, g.Persons.Count.ToString()); Cell(r, 2, g.Persons.Count(p => L(p["inside_since"]) > 0).ToString());
            Cell(r, 3, g.Records.Count(x => S(x["event_type"]) == "ENTRY").ToString()); Cell(r, 4, g.Records.Count(x => S(x["event_type"]) == "EXIT").ToString());
            Cell(r, 5, g.Records.Count(x => S(x["event_type"]) is not ("ENTRY" or "EXIT")).ToString());
            Cell(r, 6, g.Records.Count(x => L(x["loc_mismatch"]) == 1).ToString()); Cell(r, 7, g.Records.Count.ToString());
        }
        PdfGrid(sum);

        foreach (var g in groups)
        {
            var color = C(CompanyColor(g.Company));
            var h = sec.AddParagraph();
            h.Format.SpaceBefore = Unit.FromPoint(16); h.Format.SpaceAfter = Unit.FromPoint(5); h.Format.KeepWithNext = true;
            h.Format.Borders.Bottom.Width = 1.5; h.Format.Borders.Bottom.Color = color;
            var ht = h.AddFormattedText($"{g.Company.ToUpperInvariant()} COMPANY", TextFormat.Bold); ht.Font.Size = 13; ht.Font.Color = color;
            var hs = h.AddFormattedText($"    {g.Persons.Count} personnel • {g.Records.Count} records"); hs.Font.Size = 8; hs.Font.Color = C("#71717A");

            var roster = sec.AddTable();
            foreach (var w in new[] { 2.0, 5, 3, 3, 4.5, 3.5, 2.5, 3.5 }) roster.AddColumn(Unit.FromCentimeter(w));
            PdfHeader(roster, "ID", "Name", "Rank", "Service No", "Unit", "Role", "Status", "Presence");
            var i = 0;
            foreach (var p in g.Persons)
            {
                var r = roster.AddRow(); if (i++ % 2 == 1) r.Shading.Color = C(Band);
                Cell(r, 0, Id(S(p["id"])), bold: true); Cell(r, 1, S(p["name"]), bold: true); Cell(r, 2, S(p["rank"])); Cell(r, 3, S(p["service_no"]));
                Cell(r, 4, S(p["unit"])); Cell(r, 5, S(p["role"]));
                Cell(r, 6, S(p["status"]), bold: true, color: S(p["status"]) == "ACTIVE" ? C("#047857") : C("#BE123C"));
                Cell(r, 7, Presence(p), color: L(p["inside_since"]) > 0 ? C("#047857") : C("#71717A"));
            }
            PdfGrid(roster);

            if (req.IncludeRecords && g.Records.Count > 0)
            {
                var sp = sec.AddParagraph("Gate & history records"); sp.Format.Font.Bold = true; sp.Format.Font.Color = color;
                sp.Format.SpaceBefore = Unit.FromPoint(8); sp.Format.SpaceAfter = Unit.FromPoint(3); sp.Format.KeepWithNext = true;
                var rec = sec.AddTable();
                foreach (var w in new[] { 3.2, 1.8, 4.2, 2.4, 3.8, 2.2, 2.4, 2.6, 4.4 }) rec.AddColumn(Unit.FromCentimeter(w));
                PdfHeader(rec, "Date & time", "ID", "Name", "Record", "Location • Gate", "Stay", "Flag", "Operator", "Remarks");
                var names = g.Persons.ToDictionary(p => S(p["id"]));
                var j = 0;
                foreach (var e in g.Records)
                {
                    var r = rec.AddRow(); if (j++ % 2 == 1) r.Shading.Color = C(Band);
                    Cell(r, 0, Local(L(e["event_ts"])).ToString("dd MMM yyyy HH:mm")); Cell(r, 1, Id(S(e["entity_id"]))); Cell(r, 2, S(names[S(e["entity_id"])]["name"]));
                    var type = S(e["event_type"]);
                    var tc = Cell(r, 3, type, bold: true, color: type == "ENTRY" ? C("#065F46") : type == "EXIT" ? C("#92400E") : C("#1E40AF"));
                    tc.Shading.Color = type == "ENTRY" ? C("#D1FAE5") : type == "EXIT" ? C("#FEF3C7") : C("#DBEAFE");
                    Cell(r, 4, $"{S(e["location_name"])} • {S(e["gate_name"])}"); Cell(r, 5, Duration(L(e["stay_ms"])));
                    Cell(r, 6, L(e["loc_mismatch"]) == 1 ? "QR " + S(e["scanned_loc"]) : "", bold: true, color: C("#B45309"));
                    Cell(r, 7, S(e["operator_id"])); Cell(r, 8, S(e["remarks"]));
                }
                PdfGrid(rec);
            }
        }
        if (groups.Count == 0) sec.AddParagraph("No personnel match the selected filters.");

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    static void PdfHeader(Table t, params string[] titles)
    {
        var r = t.AddRow(); r.HeadingFormat = true; r.Shading.Color = C(HeadFill);
        r.TopPadding = r.BottomPadding = Unit.FromPoint(3);
        r.Borders.Bottom.Width = 1.5; r.Borders.Bottom.Color = C(Amber);
        for (var i = 0; i < titles.Length; i++) Cell(r, i, titles[i], bold: true, color: Colors.White);
    }

    static Cell Cell(Row r, int i, string text, bool bold = false, Color? color = null)
    {
        var c = r.Cells[i];
        var p = c.AddParagraph(text);
        p.Format.Font.Bold = bold;
        if (color.HasValue) p.Format.Font.Color = color.Value;
        c.VerticalAlignment = VerticalAlignment.Center;
        return c;
    }

    static void PdfGrid(Table t)
    {
        t.Borders.Width = 0.25; t.Borders.Color = C("#D4D4D8");
        t.LeftPadding = t.RightPadding = Unit.FromPoint(4);
        t.TopPadding = t.BottomPadding = Unit.FromPoint(2.5);
    }

    // ------------------------------------------------------------------ CSV

    public static void Csv(Store store, ReportRequest req, string path)
    {
        var groups = Build(store, req, out _);
        var custom = store.Settings.CustomFields;
        var rows = new List<Dictionary<string, object?>>();
        foreach (var g in groups)
        {
            var names = g.Persons.ToDictionary(p => S(p["id"]));
            foreach (var e in g.Records)
            {
                var p = names[S(e["entity_id"])];
                var cf = Custom(p);
                var row = new Dictionary<string, object?>
                {
                    ["company"] = g.Company, ["date"] = Local(L(e["event_ts"])).ToString("yyyy-MM-dd"), ["time"] = Local(L(e["event_ts"])).ToString("HH:mm:ss"),
                    ["id"] = Id(S(e["entity_id"])), ["name"] = S(p["name"]), ["rank"] = S(p["rank"]), ["service_no"] = S(p["service_no"]), ["record"] = S(e["event_type"]),
                    ["location"] = S(e["location_name"]), ["gate"] = S(e["gate_name"]), ["stay"] = Duration(L(e["stay_ms"])),
                    ["flag"] = L(e["loc_mismatch"]) == 1 ? "QR " + S(e["scanned_loc"]) : "", ["operator"] = S(e["operator_id"]), ["remarks"] = S(e["remarks"]),
                };
                foreach (var f in custom) row["cf_" + f] = cf.GetValueOrDefault(f, "");
                rows.Add(row);
            }
        }
        (string, string)[] cols = [("Company", "company"), ("Date", "date"), ("Time", "time"), ("ID", "id"), ("Name", "name"), ("Rank", "rank"), ("Service No", "service_no"),
            ("Record", "record"), ("Location", "location"), ("Gate", "gate"), ("Stay", "stay"), ("Location flag", "flag"), ("Operator", "operator"), ("Remarks", "remarks"),
            .. custom.Select(f => (f, "cf_" + f))];
        File.WriteAllText(path, XV.Core.Csv.Build(rows, cols), new UTF8Encoding(true));
    }
}

/// <summary>Connection details for whoever sets up the internet link (VPN / router / tunnel). Contains no secrets.</summary>
public static class ConnectionSheet
{
    public static List<(string key, string value)> Details(Settings s, string fingerprint) =>
    [
        ("Server name", s.ServerName),
        ("Server ID", s.ServerId),
        ("Internet access", s.InternetEnabled ? "Enabled" : "Disabled (LAN only)"),
        ("Connection method", s.CloudMode switch { "VPN" => "VPN (Tailscale / ZeroTier)", "TUNNEL" => "Tunnel (Cloudflare Tunnel / ngrok)", "RELAY" => "Cloud relay", _ => "Port forwarding + DDNS" }),
        ("Internet URL for terminals", s.PublicUrl.Length > 0 ? s.PublicUrl : "(not set)"),
        ("Public host / port", s.PublicHost.Length > 0 ? $"{s.PublicHost}:{s.PublicPort}" : "(not set)"),
        ("Tunnel presents public certificate", s.CloudUsesPublicCertificate ? "Yes" : "No"),
        ("LAN addresses", string.Join(", ", NetUtil.LanAddresses().Select(a => $"https://{a}:{s.Port}"))),
        ("HTTPS port on this PC", $"{s.Port}/TCP"),
        ("LAN discovery port", $"{s.DiscoveryPort}/UDP (local network only)"),
        ("TLS certificate SHA-256", fingerprint),
        ("Protocol", "HTTPS (TLS 1.2/1.3) + AES-256-GCM per-terminal envelope, replay protected"),
        ("Endpoints", "GET /api/v1/ping • POST /api/v1/pair/enroll • POST /api/v1/rpc"),
        ("Generated", DateTime.Now.ToString("dd MMM yyyy HH:mm")),
    ];

    /// <summary>Ready-to-use setup instructions / config for the selected connection method.</summary>
    public static string Snippet(Settings s) => s.CloudMode switch
    {
        "VPN" => $"""
            Tailscale (recommended):
              1. Install Tailscale on this PC and on every phone; sign in to the same account.
              2. On this PC run:  tailscale ip -4   → e.g. 100.x.y.z
              3. In Cloud Link set Public host = that 100.x.y.z address, Public port = {s.Port}.
              4. Re-pair phones (or enter https://100.x.y.z:{s.Port} in the phone's Cloud settings).
            """,
        "TUNNEL" => $"""
            Cloudflare Tunnel (config.yml on this PC):
              tunnel: <TUNNEL-ID>
              credentials-file: C:\Users\<you>\.cloudflared\<TUNNEL-ID>.json
              ingress:
                - hostname: gate.example.org
                  service: https://localhost:{s.Port}
                  originRequest:
                    noTLSVerify: true
                - service: http_status:404
            Then set Public URL = https://gate.example.org and tick "public certificate".
            """,
        "RELAY" => $"""
            Reverse SSH relay from this PC to a cloud VM (vm.example.org):
              ssh -N -R 0.0.0.0:{s.PublicPort}:localhost:{s.Port} relay@vm.example.org
            (VM sshd_config: GatewayPorts yes; open TCP {s.PublicPort} in the VM firewall.)
            Then set Public host = vm.example.org, Public port = {s.PublicPort}.
            """,
        _ => $"""
            Router port forwarding:
              External TCP {s.PublicPort}  →  {NetUtil.LanAddresses().FirstOrDefault() ?? "<this PC's LAN IP>"} : {s.Port}
            Give this PC a fixed LAN IP (DHCP reservation) and use a static public IP or a DDNS name
            (e.g. DuckDNS / No-IP) as Public host.
            """,
    };

    public static void Txt(Settings s, string fp, string path)
    {
        var sb = new StringBuilder("XV DIGITAL ACCESS CONTROL — CONNECTION DETAILS\r\n==============================================\r\n\r\n");
        foreach (var (k, v) in Details(s, fp)) sb.Append($"{k,-36}{v}\r\n");
        sb.Append("\r\nSETUP\r\n-----\r\n").Append(Snippet(s).Replace("\n", "\r\n"))
          .Append("\r\n\r\nPairing: open 'Local Wi-Fi & Pair Device' on the PC and scan the QR, or send its one-time pairing text (valid 10 minutes).\r\nThis sheet contains no passwords or keys.\r\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void Json(Settings s, string fp, string path)
    {
        var o = new System.Text.Json.Nodes.JsonObject
        {
            ["serverName"] = s.ServerName, ["serverId"] = s.ServerId, ["internetEnabled"] = s.InternetEnabled, ["method"] = s.CloudMode,
            ["publicUrl"] = s.PublicUrl, ["publicHost"] = s.PublicHost, ["publicPort"] = s.PublicPort, ["publicCertificate"] = s.CloudUsesPublicCertificate,
            ["lanAddresses"] = new System.Text.Json.Nodes.JsonArray(NetUtil.LanAddresses().Select(a => (System.Text.Json.Nodes.JsonNode)a).ToArray()),
            ["httpsPort"] = s.Port, ["discoveryPort"] = s.DiscoveryPort, ["certificateSha256"] = fp,
            ["endpoints"] = new System.Text.Json.Nodes.JsonObject { ["ping"] = "GET /api/v1/ping", ["enroll"] = "POST /api/v1/pair/enroll", ["rpc"] = "POST /api/v1/rpc" },
            ["encryption"] = "TLS 1.2/1.3 + AES-256-GCM per terminal", ["generated"] = DateTimeOffset.Now.ToString("o"),
        };
        File.WriteAllText(path, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Pdf(Settings s, string fp, string path)
    {
        GlobalFontSettings.FontResolver ??= new Reports.SystemFontResolverPublic();
        var doc = new Document();
        doc.Styles[StyleNames.Normal]!.Font.Name = "XV Sans";
        doc.Styles[StyleNames.Normal]!.Font.Size = 9.5;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        var band = sec.AddTable(); band.AddColumn(Unit.FromCentimeter(17.4));
        var br = band.AddRow(); br.Shading.Color = Color.Parse("#FF18181B"); br.TopPadding = br.BottomPadding = Unit.FromPoint(9);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(8);
        var t = bp.AddFormattedText("XV DIGITAL ACCESS CONTROL", TextFormat.Bold); t.Font.Size = 16; t.Font.Color = Colors.White; bp.AddLineBreak();
        var t2 = bp.AddFormattedText("Command Center connection details — for VPN / router / tunnel setup"); t2.Font.Color = Color.Parse("#FFFCD34D");
        sec.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);
        var tbl = sec.AddTable();
        tbl.AddColumn(Unit.FromCentimeter(5.4)); tbl.AddColumn(Unit.FromCentimeter(12));
        tbl.Borders.Width = 0.25; tbl.Borders.Color = Color.Parse("#FFD4D4D8");
        tbl.LeftPadding = tbl.RightPadding = Unit.FromPoint(5); tbl.TopPadding = tbl.BottomPadding = Unit.FromPoint(4);
        var i = 0;
        foreach (var (k, v) in Details(s, fp))
        {
            var r = tbl.AddRow(); if (i++ % 2 == 1) r.Shading.Color = Color.Parse("#FFF4F4F5");
            var kp = r.Cells[0].AddParagraph(k); kp.Format.Font.Bold = true; kp.Format.Font.Color = Color.Parse("#FF3F3F46");
            r.Cells[1].AddParagraph(v);
        }
        var h = sec.AddParagraph("Setup for the selected method"); h.Format.Font.Bold = true; h.Format.Font.Size = 12;
        h.Format.SpaceBefore = Unit.FromPoint(14); h.Format.SpaceAfter = Unit.FromPoint(4); h.Format.Font.Color = Color.Parse("#FF047857");
        var code = sec.AddParagraph(Snippet(s));
        code.Format.Font.Name = "XV Sans"; code.Format.Font.Size = 8.5;
        code.Format.Shading.Color = Color.Parse("#FFF4F4F5"); code.Format.Borders.Width = 0.5; code.Format.Borders.Color = Color.Parse("#FFD4D4D8");
        code.Format.Borders.Distance = Unit.FromPoint(6);
        var n = sec.AddParagraph("Pairing: open 'Local Wi-Fi & Pair Device' on the PC and scan the QR, or send its one-time pairing text (valid 10 minutes). This sheet contains no passwords or keys.");
        n.Format.SpaceBefore = Unit.FromPoint(12); n.Format.Font.Size = 8; n.Format.Font.Color = Color.Parse("#FF71717A");
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }
}
