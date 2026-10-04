using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace XV.Core;

/// <summary>Route chart and vehicle transit reports: Excel, PDF and CSV.</summary>
public static partial class Reports
{
    const string RouteBlue = "#4F46E5";

    /// <summary>Fill / text colours of a route step, by kind and trip status. The on-screen chart uses the same colours.</summary>
    public static (string bg, string fg) RouteColors(RouteStep s) => s.Kind switch
    {
        "ARRIVED" => ("#D1FAE5", "#065F46"),
        "LEFT" => ("#FEF3C7", "#92400E"),
        "TRAVEL" => s.Status switch
        {
            "REACHED" => ("#DCFCE7", "#166534"),
            "NOT REACHED" => ("#FFE4E6", "#9F1239"),
            "STOPPED" => ("#FFE4E6", "#9F1239"),
            "ON THE WAY" => ("#E0E7FF", "#3730A3"),
            _ => ("#DBEAFE", "#1E40AF"),
        },
        "AWAY" => ("#F4F4F5", "#52525B"),
        _ => ("#EFF6FF", "#1D4ED8"),
    };

    static string StepWhen(RouteStep s, string fmt) => Local(s.Ts).ToString(fmt, CultureInfo.InvariantCulture);

    static (int arrivals, int trips, long onSiteMs) RouteTotals(List<Dictionary<string, object?>> newestFirst) =>
        (newestFirst.Count(r => S(r["event_type"]) == "ENTRY"), newestFirst.Count(r => S(r.GetValueOrDefault("tr_dest_name")).Length > 0), newestFirst.Sum(r => L(r["stay_ms"])));

    // ------------------------------------------------------------------ route chart: Excel

    public static void RouteChartExcel(List<Dictionary<string, object?>> newestFirst, string title, string subtitle, string path, IEnumerable<Dictionary<string, object?>>? manualTrips = null)
    {
        var steps = RouteModel.Build(newestFirst, manualTrips);
        var (arrivals, trips, onSite) = RouteTotals(newestFirst);
        using var wb = new XLWorkbook();
        wb.Properties.Title = "Route chart - " + title;
        wb.Properties.Author = "XV Command Center";

        var ws = wb.Worksheets.Add("Route");
        ws.TabColor = XLColor.FromHtml("#06B6D4");
        ws.ShowGridLines = false;
        const int width = 6;
        Title(ws, 1, width, "ROUTE CHART — MOVEMENT & TRAVEL", RouteBlue);
        ws.Cell(2, 1).Value = title; ws.Range(2, 1, 2, width).Merge().Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(XLColor.FromHtml(Ink));
        ws.Row(2).Height = 24;
        ws.Cell(3, 1).Value = $"{subtitle}   •   Generated {DateTime.Now:dd MMM yyyy HH:mm}";
        ws.Range(3, 1, 3, width).Merge().Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#52525B"));

        // at-a-glance figures
        string[] kpiHead = ["Entries", "Trips with a destination", "Total time on site"];
        object[] kpiVal = [arrivals, trips, onSite > 0 ? Duration(onSite) : "—"];
        for (var i = 0; i < 3; i++)
        {
            var c = ws.Range(5, 1 + i * 2, 5, 2 + i * 2); c.Merge(); ws.Cell(5, 1 + i * 2).Value = kpiHead[i];
            c.Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#71717A")).Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F4F4F5")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            var v = ws.Range(6, 1 + i * 2, 6, 2 + i * 2); v.Merge(); ws.Cell(6, 1 + i * 2).Value = XLCellValue.FromObject(kpiVal[i]);
            v.Style.Font.SetFontSize(15).Font.SetBold().Font.SetFontColor(XLColor.FromHtml(RouteBlue)).Fill.SetBackgroundColor(XLColor.FromHtml("#F4F4F5")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }
        ws.Row(6).Height = 26;

        // legend
        ws.Cell(8, 1).Value = "Colour key";
        ws.Cell(8, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#52525B"));
        (string label, RouteStep sample)[] legend =
        [
            ("Arrived", new("ARRIVED", 0, "", "", "", "")), ("Left", new("LEFT", 0, "", "", "", "")), ("Reached", new("TRAVEL", 0, "", "", "", "REACHED")),
            ("On the way", new("TRAVEL", 0, "", "", "", "ON THE WAY")), ("Not reached / stopped", new("TRAVEL", 0, "", "", "", "NOT REACHED")),
        ];
        for (var i = 0; i < legend.Length; i++)
        {
            var (bg, fg) = RouteColors(legend[i].sample);
            var cell = ws.Cell(8, 2 + i); cell.Value = legend[i].label;
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Font.SetFontSize(9).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        Header(ws, 10, ["Date", "Time", "Step", "Location / Destination", "Details", "Status"]);
        var row = 11;
        foreach (var s in steps)
        {
            var (bg, fg) = RouteColors(s);
            ws.Cell(row, 1).Value = StepWhen(s, "dd MMM yyyy");
            ws.Cell(row, 2).Value = StepWhen(s, "HH:mm");
            ws.Cell(row, 3).Value = s.Title;
            ws.Cell(row, 4).Value = s.Kind == "TRAVEL" ? "→ " + s.Place : s.Place;
            ws.Cell(row, 5).Value = s.Detail;
            ws.Cell(row, 6).Value = s.Status;
            ws.Range(row, 1, row, width).Style.Fill.SetBackgroundColor(XLColor.FromHtml(s.Kind is "TRAVEL" or "AWAY" ? "#FAFAFA" : "#FFFFFF"));
            ws.Cell(row, 3).Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            if (s.Status.Length > 0) ws.Cell(row, 6).Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 2).Style.Font.SetFontName("Consolas").Font.SetBold();
            ws.Cell(row, 4).Style.Font.SetBold();
            if (s.Kind is "TRAVEL" or "AWAY") ws.Range(row, 4, row, 5).Style.Font.SetItalic();
            ws.Range(row, 4, row, 5).Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            row++;
        }
        if (steps.Count == 0) ws.Cell(row++, 1).Value = "No records";
        Grid(ws.Range(10, 1, row - 1, width));
        ws.Column(1).Width = 13; ws.Column(2).Width = 8; ws.Column(3).Width = 12; ws.Column(4).Width = 34; ws.Column(5).Width = 62; ws.Column(6).Width = 18;
        ws.SheetView.FreezeRows(10);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 1);
        ws.PageSetup.Margins.Top = 0.5; ws.PageSetup.Margins.Bottom = 0.5; ws.PageSetup.Margins.Left = 0.4; ws.PageSetup.Margins.Right = 0.4;
        ws.PageSetup.CenterHorizontally = true;
        ws.PageSetup.Footer.Center.AddText("XV Digital Access Control • Confidential");

        // every raw record, with the trip columns
        var rec = wb.Worksheets.Add("All records");
        rec.TabColor = XLColor.FromHtml(Amber);
        string[] head = ["Date", "Time", "Record", "Reason", "Location", "Gate", "Stay", "Destination", "Approx time", "Trip status", "Time taken", "How recorded", "Operator", "Remarks"];
        Title(rec, 1, head.Length, "ALL RECORDS — " + title.ToUpperInvariant(), Ink);
        Header(rec, 3, head);
        var r2 = 4;
        foreach (var e in newestFirst.AsEnumerable().Reverse())
        {
            var ts = Local(L(e["event_ts"]));
            var state = S(e.GetValueOrDefault("tr_state"));
            object[] vals =
            [
                ts.ToString("dd MMM yyyy"), ts.ToString("HH:mm:ss"), S(e["event_type"]), S(e["reason"]), S(e["location_name"]), S(e["gate_name"]), Duration(L(e["stay_ms"])),
                S(e.GetValueOrDefault("tr_dest_name")), L(e.GetValueOrDefault("tr_expected_min")) > 0 ? TransitText.Mins(L(e["tr_expected_min"])) : "",
                state.Length == 0 ? "" : TransitText.StateLabel(state, TransitText.IsOverdue(e, "tr_")),
                L(e.GetValueOrDefault("tr_actual_min")) > 0 ? TransitText.Mins(L(e["tr_actual_min"])) : "",
                TransitText.Via(S(e.GetValueOrDefault("tr_resolved_via")), S(e.GetValueOrDefault("tr_resolved_by"))), S(e["operator_id"]), S(e["remarks"]),
            ];
            for (var i = 0; i < vals.Length; i++) rec.Cell(r2, i + 1).Value = S(vals[i]);
            if (r2 % 2 == 1) rec.Range(r2, 1, r2, head.Length).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            RecordCell(rec.Cell(r2, 3), S(e["event_type"]));
            r2++;
        }
        Grid(rec.Range(3, 1, Math.Max(r2 - 1, 3), head.Length));
        if (r2 > 4) rec.Range(3, 1, r2 - 1, head.Length).SetAutoFilter();
        rec.Columns(1, head.Length).AdjustToContents(4, 50);
        rec.SheetView.FreezeRows(3);
        rec.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        rec.PageSetup.FitToPages(1, 0);
        wb.SaveAs(path);
    }

    // ------------------------------------------------------------------ route chart: PDF (one page)

    public static void RouteChartPdf(List<Dictionary<string, object?>> newestFirst, string title, string subtitle, string path, IEnumerable<Dictionary<string, object?>>? manualTrips = null)
    {
        EnsureFonts();
        var all = RouteModel.Build(newestFirst, manualTrips);
        // One printed page: shrink type for longer routes, and keep only the latest steps beyond what a page can hold.
        var size = all.Count <= 22 ? 9.0 : all.Count <= 34 ? 8.0 : all.Count <= 46 ? 7.0 : 6.2;
        var pad = all.Count <= 22 ? 4.0 : all.Count <= 34 ? 3.0 : all.Count <= 46 ? 2.2 : 1.6;
        var maxRows = 62;
        var shown = all.Count > maxRows ? all.Skip(all.Count - maxRows).ToList() : all;

        var doc = new Document();
        doc.Info.Title = "Route chart - " + title;
        doc.Styles[StyleNames.Normal]!.Font.Name = "XV Sans";
        doc.Styles[StyleNames.Normal]!.Font.Size = size;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.Orientation = Orientation.Portrait;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        var footer = sec.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7; footer.Format.Font.Color = C("#71717A");
        footer.AddText($"XV Digital Access Control • Confidential • Generated {DateTime.Now:dd MMM yyyy HH:mm}");

        var band = sec.AddTable(); band.AddColumn(Unit.FromCentimeter(18.6));
        var br = band.AddRow(); br.Shading.Color = C(RouteBlue); br.TopPadding = br.BottomPadding = Unit.FromPoint(8);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(8);
        var t1 = bp.AddFormattedText("ROUTE CHART — MOVEMENT & TRAVEL", TextFormat.Bold); t1.Font.Size = 14; t1.Font.Color = Colors.White;
        bp.AddLineBreak();
        var t2 = bp.AddFormattedText(title); t2.Font.Size = 10; t2.Font.Color = C("#E0E7FF");

        var (arrivals, trips, onSite) = RouteTotals(newestFirst);
        var meta = sec.AddParagraph($"{subtitle}   •   {arrivals} entries   •   {trips} trip(s) with a destination" + (onSite > 0 ? $"   •   time on site {Duration(onSite)}" : ""));
        meta.Format.Font.Size = 8; meta.Format.Font.Color = C("#52525B"); meta.Format.SpaceBefore = Unit.FromPoint(5); meta.Format.SpaceAfter = Unit.FromPoint(5);

        var tbl = sec.AddTable();
        foreach (var w in new[] { 2.4, 1.2, 1.9, 4.4, 5.9, 2.8 }) tbl.AddColumn(Unit.FromCentimeter(w));
        PdfHeader(tbl, "Date", "Time", "Step", "Location / destination", "Details", "Status");
        var n = 0;
        foreach (var s in shown)
        {
            var (bg, fg) = RouteColors(s);
            var r = tbl.AddRow(); if (n++ % 2 == 1) r.Shading.Color = C("#FAFAFA");
            Cell(r, 0, StepWhen(s, "dd MMM yy")); Cell(r, 1, StepWhen(s, "HH:mm"), bold: true);
            var sc = Cell(r, 2, s.Title, bold: true, color: C(fg)); sc.Shading.Color = C(bg);
            var italic = s.Kind is "TRAVEL" or "AWAY";
            Cell(r, 3, s.Kind == "TRAVEL" ? "→ " + s.Place : s.Place, bold: true).Format.Font.Italic = italic;
            Cell(r, 4, s.Detail, color: C("#334155")).Format.Font.Italic = italic;
            if (s.Status.Length > 0) { var st = Cell(r, 5, s.Status, bold: true, color: C(fg)); st.Shading.Color = C(bg); } else Cell(r, 5, "");
        }
        tbl.Borders.Width = 0.25; tbl.Borders.Color = C("#D4D4D8");
        tbl.LeftPadding = tbl.RightPadding = Unit.FromPoint(3);
        tbl.TopPadding = tbl.BottomPadding = Unit.FromPoint(pad);
        if (shown.Count == 0) sec.AddParagraph("No records.");
        if (all.Count > shown.Count)
        {
            var note = sec.AddParagraph($"Showing the latest {shown.Count} of {all.Count} steps so this fits one page. Export to Excel for the full history.");
            note.Format.Font.Size = 7; note.Format.Font.Color = C("#B45309"); note.Format.SpaceBefore = Unit.FromPoint(4);
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    // ------------------------------------------------------------------ vehicle transit report

    static string TripHow(Dictionary<string, object?> t) => TransitText.Via(S(t["resolved_via"]), S(t["resolved_by"]));
    static string TripStatus(Dictionary<string, object?> t) => TransitText.StateLabel(S(t["state"]), t["overdue"] is true);
    static string TripPlace(Dictionary<string, object?> t) => S(t["state"]) == Store.TransitEnRoute ? "" : S(t["end_name"]);

    /// <summary>Colour of a trip status cell, shared by Excel and PDF.</summary>
    static (string bg, string fg) TripColors(string status) => status switch
    {
        "REACHED" => ("#D1FAE5", "#065F46"),
        "NOT REACHED" or "STOPPED" => ("#FFE4E6", "#9F1239"),
        "MOVED ELSEWHERE" => ("#DBEAFE", "#1E40AF"),
        _ => ("#FEF3C7", "#92400E"),
    };

    static string VsApprox(Dictionary<string, object?> t)
    {
        long exp = L(t["expected_min"]), act = L(t["actual_min"]);
        if (t["overdue"] is true) return TransitText.Mins(L(t["late_ms"]) / 60_000) + " late so far";
        if (exp <= 0 || act <= 0 || S(t["state"]) == Store.TransitStopped) return "";
        var d = act - exp;
        return d == 0 ? "on time" : d > 0 ? $"{TransitText.Mins(d)} late" : $"{TransitText.Mins(-d)} early";
    }

    public static void TransitExcel(Store store, DateTime from, DateTime to, string path)
    {
        long fromMs = new DateTimeOffset(from.Date).ToUnixTimeMilliseconds(), toMs = new DateTimeOffset(to.Date.AddDays(1)).ToUnixTimeMilliseconds() - 1;
        var trips = store.Transits("", "", fromMs, toMs, 100_000);
        var averages = store.TransitAverages(fromMs, toMs);
        var byVehicle = store.TransitAveragesByVehicle(fromMs, toMs);
        var routes = store.TransitRoutes();
        var period = $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";
        using var wb = new XLWorkbook();
        wb.Properties.Title = "XV Vehicle Transit Report";
        wb.Properties.Author = "XV Command Center";

        // ---- Summary
        var sum = wb.Worksheets.Add("Summary");
        sum.TabColor = XLColor.FromHtml(Amber); sum.ShowGridLines = false;
        Title(sum, 1, 8, "VEHICLE TRANSIT REPORT", RouteBlue);
        sum.Cell(2, 1).Value = $"{period}   •   Generated {DateTime.Now:dd MMM yyyy HH:mm}   •   {store.Settings.ServerName}";
        sum.Range(2, 1, 2, 8).Merge().Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#52525B"));
        (string label, int count, string color)[] kpi =
        [
            ("Trips", trips.Count, RouteBlue), ("Reached", trips.Count(t => S(t["state"]) == Store.TransitReached), "#047857"),
            ("Moved elsewhere", trips.Count(t => S(t["state"]) == Store.TransitDiverted), "#1D4ED8"), ("Stopped", trips.Count(t => S(t["state"]) == Store.TransitStopped), "#BE123C"),
            ("On the way", trips.Count(t => S(t["state"]) == Store.TransitEnRoute && t["overdue"] is not true), "#B45309"), ("Not reached (late)", trips.Count(t => t["overdue"] is true), "#9F1239"),
        ];
        for (var i = 0; i < kpi.Length; i++)
        {
            var h = sum.Cell(4, 1 + i); h.Value = kpi[i].label;
            h.Style.Font.SetFontSize(9).Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#71717A")).Fill.SetBackgroundColor(XLColor.FromHtml("#F4F4F5")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            var v = sum.Cell(5, 1 + i); v.Value = kpi[i].count;
            v.Style.Font.SetFontSize(18).Font.SetBold().Font.SetFontColor(XLColor.FromHtml(kpi[i].color)).Fill.SetBackgroundColor(XLColor.FromHtml("#F4F4F5")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }
        sum.Row(5).Height = 30;
        Section(sum, 7, 8, "AVERAGE TIME BETWEEN LOCATIONS — ALL VEHICLES", RouteBlue);
        Header(sum, 8, ["From", "To", "Trips", "Vehicles", "Average", "Fastest", "Slowest", "Standard time"]);
        var row = 9;
        foreach (var a in averages)
        {
            var avg = (long)Math.Round(Convert.ToDouble(a["avg_min"], CultureInfo.InvariantCulture));
            var std = L(a["standard_min"]);
            object[] vals = [S(a["from_name"]), S(a["to_name"]), L(a["trips"]), L(a["vehicles"]), TransitText.Mins(avg), TransitText.Mins(L(a["min_min"])), TransitText.Mins(L(a["max_min"])),
                std > 0 ? TransitText.Mins(std) + (avg > std ? $"  (+{TransitText.Mins(avg - std)})" : avg < std ? $"  (−{TransitText.Mins(std - avg)})" : "") : "not set"];
            for (var i = 0; i < vals.Length; i++) sum.Cell(row, i + 1).Value = XLCellValue.FromObject(vals[i]);
            if (row % 2 == 0) sum.Range(row, 1, row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            sum.Cell(row, 5).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#1D4ED8"));
            row++;
        }
        if (averages.Count == 0) sum.Cell(row++, 1).Value = "No completed trips in this period";
        Grid(sum.Range(8, 1, row - 1, 8));
        sum.Cell(row + 1, 1).Value = "Averages count only trips that really arrived at a location (gate scan, or a time entered by the RP / server). Stopped trips are not counted.";
        sum.Range(row + 1, 1, row + 1, 8).Merge().Style.Font.SetItalic().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#71717A"));
        sum.Columns(1, 8).Width = 20; sum.Column(8).Width = 30;
        sum.PageSetup.PageOrientation = XLPageOrientation.Landscape; sum.PageSetup.FitToPages(1, 0);

        // ---- Trips
        var ws = wb.Worksheets.Add("Trips");
        ws.TabColor = XLColor.FromHtml("#06B6D4");
        string[] head = ["Date", "Vehicle", "Vehicle ID", "From", "Planned destination", "Left at", "Approx time", "Expected by", "Status", "Ended at (place)", "Ended at (time)", "Time taken", "Compared with approx", "How recorded", "Remarks"];
        Title(ws, 1, head.Length, "VEHICLE TRIPS — " + period, Ink);
        Header(ws, 3, head);
        row = 4;
        foreach (var t in trips)
        {
            var status = TripStatus(t);
            var left = Local(L(t["left_at"]));
            object[] vals = [left.ToString("dd MMM yyyy"), S(t["plate"]), Id(S(t["vehicle_id"])), S(t["from_name"]), S(t["dest_name"]) + (S(t["dest_loc"]).Length == 0 ? " (new place)" : ""), left.ToString("HH:mm"),
                L(t["expected_min"]) > 0 ? TransitText.Mins(L(t["expected_min"])) : "not set", L(t["due_at"]) > 0 ? Local(L(t["due_at"])).ToString("dd MMM HH:mm") : "",
                status, TripPlace(t), L(t["end_at"]) > 0 ? Local(L(t["end_at"])).ToString("dd MMM HH:mm") : "",
                L(t["actual_min"]) > 0 ? TransitText.Mins(L(t["actual_min"])) : "", VsApprox(t), TripHow(t), S(t["note"])];
            for (var i = 0; i < vals.Length; i++) ws.Cell(row, i + 1).Value = S(vals[i]);
            if (row % 2 == 1) ws.Range(row, 1, row, head.Length).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            var (bg, fg) = TripColors(status);
            ws.Cell(row, 9).Style.Fill.SetBackgroundColor(XLColor.FromHtml(bg)).Font.SetFontColor(XLColor.FromHtml(fg)).Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 2).Style.Font.SetBold();
            if (VsApprox(t).EndsWith("late")) ws.Cell(row, 13).Style.Font.SetFontColor(XLColor.FromHtml("#9F1239")).Font.SetBold();
            else if (VsApprox(t).EndsWith("early") || VsApprox(t) == "on time") ws.Cell(row, 13).Style.Font.SetFontColor(XLColor.FromHtml("#047857")).Font.SetBold();
            row++;
        }
        if (trips.Count == 0) ws.Cell(row++, 1).Value = "No trips in this period";
        Grid(ws.Range(3, 1, row - 1, head.Length));
        if (trips.Count > 0) ws.Range(3, 1, row - 1, head.Length).SetAutoFilter();
        ws.Columns(1, head.Length).AdjustToContents(4, 45);
        ws.SheetView.FreezeRows(3);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape; ws.PageSetup.FitToPages(1, 0);

        // ---- By vehicle
        var bv = wb.Worksheets.Add("Average by vehicle");
        bv.TabColor = XLColor.FromHtml("#059669");
        Title(bv, 1, 7, "AVERAGE TIME BY VEHICLE — " + period, Ink);
        Header(bv, 3, ["From", "To", "Vehicle", "Trips", "Average", "Fastest", "Slowest"]);
        row = 4;
        foreach (var v in byVehicle)
        {
            object[] vals = [S(v["from_name"]), S(v["to_name"]), S(v["plate"]), L(v["trips"]), TransitText.Mins((long)Math.Round(Convert.ToDouble(v["avg_min"], CultureInfo.InvariantCulture))),
                TransitText.Mins(L(v["min_min"])), TransitText.Mins(L(v["max_min"]))];
            for (var i = 0; i < vals.Length; i++) bv.Cell(row, i + 1).Value = XLCellValue.FromObject(vals[i]);
            if (row % 2 == 1) bv.Range(row, 1, row, 7).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            bv.Cell(row, 3).Style.Font.SetBold();
            row++;
        }
        if (byVehicle.Count == 0) bv.Cell(row++, 1).Value = "No completed trips in this period";
        Grid(bv.Range(3, 1, row - 1, 7));
        bv.Columns(1, 7).AdjustToContents(4, 40);
        bv.SheetView.FreezeRows(3);

        // ---- Standard times
        var st = wb.Worksheets.Add("Standard times");
        st.TabColor = XLColor.FromHtml("#B45309");
        Title(st, 1, 4, "STANDARD TIME BETWEEN LOCATIONS", Ink);
        Header(st, 3, ["From", "To", "Approx time", "Set by"]);
        row = 4;
        foreach (var r in routes)
        {
            object[] vals = [S(r["from_name"]), S(r["to_name"]), TransitText.Mins(L(r["minutes"])), S(r["source"]) == "RP" ? "RP at the gate" : "Server"];
            for (var i = 0; i < vals.Length; i++) st.Cell(row, i + 1).Value = S(vals[i]);
            if (row % 2 == 1) st.Range(row, 1, row, 4).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Band));
            row++;
        }
        if (routes.Count == 0) st.Cell(row++, 1).Value = "No standard times saved";
        Grid(st.Range(3, 1, row - 1, 4));
        st.Columns(1, 4).AdjustToContents(4, 40);
        wb.SaveAs(path);
    }

    public static void TransitPdf(Store store, DateTime from, DateTime to, string path)
    {
        EnsureFonts();
        long fromMs = new DateTimeOffset(from.Date).ToUnixTimeMilliseconds(), toMs = new DateTimeOffset(to.Date.AddDays(1)).ToUnixTimeMilliseconds() - 1;
        var trips = store.Transits("", "", fromMs, toMs, 100_000);
        var averages = store.TransitAverages(fromMs, toMs);
        var doc = new Document();
        doc.Info.Title = "XV Vehicle Transit Report";
        doc.Styles[StyleNames.Normal]!.Font.Name = "XV Sans";
        doc.Styles[StyleNames.Normal]!.Font.Size = 8;
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

        var band = sec.AddTable(); band.AddColumn(Unit.FromCentimeter(27));
        var br = band.AddRow(); br.Shading.Color = C(RouteBlue); br.TopPadding = br.BottomPadding = Unit.FromPoint(8);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(8);
        var t1 = bp.AddFormattedText("VEHICLE TRANSIT REPORT", TextFormat.Bold); t1.Font.Size = 16; t1.Font.Color = Colors.White;
        bp.AddLineBreak();
        var t2 = bp.AddFormattedText($"{from:dd MMM yyyy} – {to:dd MMM yyyy}  •  {trips.Count} trip(s)  •  Generated {DateTime.Now:dd MMM yyyy HH:mm}"); t2.Font.Size = 8.5; t2.Font.Color = C("#E0E7FF");

        var h1 = sec.AddParagraph("Average time between locations (all vehicles)");
        h1.Format.Font.Bold = true; h1.Format.Font.Size = 11; h1.Format.Font.Color = C(RouteBlue); h1.Format.SpaceBefore = Unit.FromPoint(12); h1.Format.SpaceAfter = Unit.FromPoint(4); h1.Format.KeepWithNext = true;
        var at = sec.AddTable();
        foreach (var w in new[] { 5.5, 5.5, 2.2, 2.4, 2.6, 2.6, 2.6, 3.6 }) at.AddColumn(Unit.FromCentimeter(w));
        PdfHeader(at, "From", "To", "Trips", "Vehicles", "Average", "Fastest", "Slowest", "Standard time");
        var n = 0;
        foreach (var a in averages)
        {
            var avg = (long)Math.Round(Convert.ToDouble(a["avg_min"], CultureInfo.InvariantCulture)); var std = L(a["standard_min"]);
            var r = at.AddRow(); if (n++ % 2 == 1) r.Shading.Color = C(Band);
            Cell(r, 0, S(a["from_name"]), bold: true); Cell(r, 1, S(a["to_name"]), bold: true); Cell(r, 2, L(a["trips"]).ToString()); Cell(r, 3, L(a["vehicles"]).ToString());
            Cell(r, 4, TransitText.Mins(avg), bold: true, color: C("#1D4ED8")); Cell(r, 5, TransitText.Mins(L(a["min_min"]))); Cell(r, 6, TransitText.Mins(L(a["max_min"])));
            Cell(r, 7, std > 0 ? TransitText.Mins(std) : "not set");
        }
        if (averages.Count == 0) { var r = at.AddRow(); Cell(r, 0, "No completed trips in this period"); }
        PdfGrid(at);
        var note = sec.AddParagraph("Counted only from trips that really arrived at a location (gate scan, or a time entered by the RP / server). Stopped trips are not counted.");
        note.Format.Font.Size = 7; note.Format.Font.Color = C("#71717A"); note.Format.SpaceBefore = Unit.FromPoint(3);

        var h2 = sec.AddParagraph("Trips");
        h2.Format.Font.Bold = true; h2.Format.Font.Size = 11; h2.Format.Font.Color = C(RouteBlue); h2.Format.SpaceBefore = Unit.FromPoint(14); h2.Format.SpaceAfter = Unit.FromPoint(4); h2.Format.KeepWithNext = true;
        var tt = sec.AddTable();
        foreach (var w in new[] { 2.4, 2.6, 3.2, 3.6, 1.6, 1.8, 2.8, 3.6, 1.9, 3.5 }) tt.AddColumn(Unit.FromCentimeter(w));
        PdfHeader(tt, "Left", "Vehicle", "From", "Planned destination", "Approx", "Taken", "Status", "Ended at", "vs approx", "How recorded");
        n = 0;
        foreach (var t in trips)
        {
            var status = TripStatus(t); var (bg, fg) = TripColors(status);
            var r = tt.AddRow(); if (n++ % 2 == 1) r.Shading.Color = C(Band);
            Cell(r, 0, Local(L(t["left_at"])).ToString("dd MMM HH:mm")); Cell(r, 1, S(t["plate"]), bold: true); Cell(r, 2, S(t["from_name"]));
            Cell(r, 3, S(t["dest_name"]) + (S(t["dest_loc"]).Length == 0 ? " (new)" : ""));
            Cell(r, 4, L(t["expected_min"]) > 0 ? TransitText.Mins(L(t["expected_min"])) : "—"); Cell(r, 5, L(t["actual_min"]) > 0 ? TransitText.Mins(L(t["actual_min"])) : "—");
            var sc = Cell(r, 6, status, bold: true, color: C(fg)); sc.Shading.Color = C(bg);
            Cell(r, 7, TripPlace(t)); Cell(r, 8, VsApprox(t)); Cell(r, 9, TripHow(t));
        }
        if (trips.Count == 0) { var r = tt.AddRow(); Cell(r, 0, "No trips in this period"); }
        PdfGrid(tt);

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    public static void TransitCsv(Store store, DateTime from, DateTime to, string path)
    {
        long fromMs = new DateTimeOffset(from.Date).ToUnixTimeMilliseconds(), toMs = new DateTimeOffset(to.Date.AddDays(1)).ToUnixTimeMilliseconds() - 1;
        var rows = store.Transits("", "", fromMs, toMs, 100_000).Select(t => new Dictionary<string, object?>
        {
            ["date"] = Local(L(t["left_at"])).ToString("yyyy-MM-dd"), ["left"] = Local(L(t["left_at"])).ToString("HH:mm"), ["vehicle"] = S(t["plate"]), ["vehicle_id"] = Id(S(t["vehicle_id"])),
            ["from"] = S(t["from_name"]), ["to"] = S(t["dest_name"]), ["approx"] = L(t["expected_min"]) > 0 ? L(t["expected_min"]).ToString() : "",
            ["status"] = TripStatus(t), ["ended_place"] = TripPlace(t), ["ended_at"] = L(t["end_at"]) > 0 ? Local(L(t["end_at"])).ToString("yyyy-MM-dd HH:mm") : "",
            ["taken"] = L(t["actual_min"]) > 0 ? L(t["actual_min"]).ToString() : "", ["vs"] = VsApprox(t), ["how"] = TripHow(t), ["remarks"] = S(t["note"]),
        });
        (string, string)[] cols = [("Date", "date"), ("Left", "left"), ("Vehicle", "vehicle"), ("Vehicle ID", "vehicle_id"), ("From", "from"), ("Planned destination", "to"),
            ("Approx time (min)", "approx"), ("Status", "status"), ("Ended at (place)", "ended_place"), ("Ended at (time)", "ended_at"), ("Time taken (min)", "taken"),
            ("Compared with approx", "vs"), ("How recorded", "how"), ("Remarks", "remarks")];
        File.WriteAllText(path, XV.Core.Csv.Build(rows, cols), new UTF8Encoding(true));
    }
}
