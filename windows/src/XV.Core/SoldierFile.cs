using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace XV.Core;

/// <summary>Which soldiers an export / ID-card run covers.</summary>
/// <param name="Kind">ALL | COMPANY | PLATOON | SECTION | SELECTED</param>
public sealed record SoldierScope(string Kind, string Company = "", string Platoon = "", string Section = "", IReadOnlyCollection<string>? Ids = null)
{
    public static readonly SoldierScope All = new("ALL");

    public string Describe() => Kind switch
    {
        "COMPANY" => $"{Company} Company",
        "PLATOON" => $"{Company} Company • {Platoon}",
        "SECTION" => $"{Company} Company • {Platoon} • {Section}",
        "SELECTED" => Ids?.Count == 1 ? $"Soldier {Ids.First()}" : $"{Ids?.Count ?? 0} selected soldiers",
        _ => "All soldiers",
    };

    public List<Dictionary<string, object?>> Apply(IEnumerable<Dictionary<string, object?>> persons)
    {
        static bool Eq(object? a, string b) => string.Equals((a?.ToString() ?? "").Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        return persons.Where(p => Kind switch
        {
            "COMPANY" => Eq(p["company"], Company),
            "PLATOON" => Eq(p["company"], Company) && Eq(p["platoon"], Platoon),
            "SECTION" => Eq(p["company"], Company) && Eq(p["platoon"], Platoon) && Eq(p["section"], Section),
            "SELECTED" => Ids != null && Ids.Contains(p["id"]?.ToString() ?? ""),
            _ => true,
        }).ToList();
    }
}

/// <summary>
/// Soldier register files (CSV and Excel .xlsx) with every built-in soldier detail plus the administrator's custom fields.
/// The same layout is used for export, blank templates and import, so an exported file can be edited and imported back.
/// </summary>
public static class SoldierFile
{
    public static readonly string[] Companies = ["Alpha", "Bravo", "Charlie", "Delta", "SP", "HQ"];

    /// <summary>(header, database column, upsert key, extra header spellings accepted on import)</summary>
    public static readonly (string Header, string Column, string Key, string[] Aliases)[] BuiltIn =
    [
        ("Personnel ID", "id", "id", ["id", "person id", "personnel id", "person_id"]),
        ("Army No", "service_no", "serviceNo", ["army number", "service no", "service number", "service_no", "army no."]),
        ("Rank", "rank", "rank", []),
        ("Name", "name", "name", ["full name", "soldier name"]),
        ("Appointment", "role", "role", ["designation", "role"]),
        ("Unit", "unit", "unit", []),
        ("Company", "company", "company", ["coy"]),
        ("Platoon", "platoon", "platoon", ["pl"]),
        ("Section", "section", "section", ["sec"]),
        ("Category", "category", "category", []),
        ("Status", "status", "status", []),
        ("Blood Group", "blood_group", "bloodGroup", ["blood_group", "blood gp"]),
        ("Mobile", "mobile", "mobile", ["mobile no", "phone", "mobile number"]),
        ("Address", "address", "address", ["permanent address"]),
        ("Date of Birth", "dob", "dob", ["dob"]),
        ("Date of Enrolment", "enrol_date", "enrolDate", ["enrolment date", "date of enrollment", "enrolment"]),
        ("Card Expiry", "expiry_date", "expiryDate", ["expiry", "expiry date", "valid till"]),
        ("Identification Mark", "id_mark", "idMark", ["identification marks", "id mark"]),
        ("NOK Name", "nok_name", "nokName", ["next of kin", "nok"]),
        ("NOK Relation", "nok_relation", "nokRelation", ["relation"]),
        ("NOK Phone", "nok_phone", "nokPhone", ["nok mobile", "nok contact"]),
        ("I-Card No", "id_card", "idCard", ["id card", "id_card", "icard no"]),
        ("Card Serial", "card_serial", "cardSerial", ["card serial no", "card ref no"]),
        ("Authorized Locations", "access_locations", "accessLocations", ["access locations", "access_locations"]),
        ("Notes", "notes", "notes", []),
    ];

    static string Norm(string h) => new string(h.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string CanonCompany(string raw)
    {
        var v = (raw ?? "").Trim();
        foreach (var c in Companies)
            if (Norm(v) == Norm(c) || Norm(v) == Norm(c + " company") || Norm(v) == Norm(c + " coy")) return c;
        return v;
    }

    static Dictionary<string, string> CustomOf(Dictionary<string, object?> p)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(p.TryGetValue("custom_json", out var j) && (j?.ToString() ?? "").Length > 1 ? j!.ToString()! : "{}") ?? []; }
        catch { return []; }
    }

    static List<string> Row(Dictionary<string, object?> p, IReadOnlyList<string> custom)
    {
        var cf = CustomOf(p);
        var row = BuiltIn.Select(c => c.Column == "id" ? Store.CanonId(p[c.Column]?.ToString()) : (p.TryGetValue(c.Column, out var v) ? v?.ToString() ?? "" : "")).ToList();
        row.AddRange(custom.Select(f => cf.GetValueOrDefault(f, "")));
        return row;
    }

    // ------------------------------------------------------------------ export

    /// <summary>Writes the soldiers to .csv or .xlsx (chosen by the file extension). An empty list writes a blank template.</summary>
    public static void Export(IReadOnlyList<Dictionary<string, object?>> persons, IReadOnlyList<string> custom, string path, string title)
    {
        var headers = BuiltIn.Select(c => c.Header).Concat(custom).ToList();
        if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) { Xlsx(persons, custom, headers, path, title); return; }
        var sb = new StringBuilder();
        static string Q(string v) => v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        sb.Append(string.Join(",", headers.Select(Q))).Append("\r\n");
        foreach (var p in persons) sb.Append(string.Join(",", Row(p, custom).Select(Q))).Append("\r\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    static void Xlsx(IReadOnlyList<Dictionary<string, object?>> persons, IReadOnlyList<string> custom, List<string> headers, string path, string title)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Soldiers");
        for (var i = 0; i < headers.Count; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = headers[i];
            c.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(i < BuiltIn.Length ? "#0F172A" : "#6D28D9"));
            c.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText();
        }
        ws.Row(1).Height = 30;
        var r = 2;
        foreach (var p in persons)
        {
            var vals = Row(p, custom);
            for (var i = 0; i < vals.Count; i++) ws.Cell(r, i + 1).SetValue(vals[i]); // text cells: IDs and dates keep their exact form
            var company = CanonCompany(p["company"]?.ToString() ?? "");
            var tint = Reports.CompanyColor(company);
            ws.Cell(r, 7).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(tint));
            if (r % 2 == 0) ws.Range(r, 1, r, headers.Count).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
            r++;
        }
        var used = ws.Range(1, 1, Math.Max(1, r - 1), headers.Count);
        used.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorderColor(XLColor.FromHtml("#E2E8F0")).Border.SetOutsideBorderColor(XLColor.FromHtml("#CBD5E1"));
        if (persons.Count > 0) used.SetAutoFilter();
        ws.SheetView.FreezeRows(1); ws.SheetView.FreezeColumns(4);
        ws.Columns(1, headers.Count).AdjustToContents(1, Math.Max(2, r), 10, 45);

        var info = wb.AddWorksheet("About");
        info.Cell(1, 1).Value = "XV Digital Access Control — soldier register"; info.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        info.Cell(2, 1).Value = title;
        info.Cell(3, 1).Value = $"Exported {DateTime.Now:dd MMM yyyy HH:mm} • {persons.Count} soldier(s)";
        info.Cell(5, 1).Value = "Import: keep the header row of the 'Soldiers' sheet. 'Personnel ID' updates an existing soldier (blank = new soldier).";
        info.Cell(6, 1).Value = "Purple headers are custom fields defined in Stations & Settings. Extra columns can be added as new custom fields when importing.";
        info.Cell(7, 1).Value = "Companies: " + string.Join(", ", Companies) + ". Dates as DD-MM-YYYY.";
        info.Column(1).Width = 110;
        wb.SaveAs(path);
    }

    // ------------------------------------------------------------------ import

    /// <summary>Reads header + rows from .csv or .xlsx (first worksheet, header in row 1).</summary>
    public static (List<string> headers, List<Dictionary<string, string>> rows) Read(string path)
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var text = File.ReadAllText(path);
            var head = Csv.ParseRows(text).FirstOrDefault()?.Select(h => h.Trim()).Where(h => h.Length > 0).ToList() ?? [];
            return (head, Csv.Parse(text));
        }
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var last = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var headers = Enumerable.Range(1, last).Select(i => ws.Cell(1, i).GetString().Trim()).ToList();
        var list = new List<Dictionary<string, string>>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                if (headers[i].Length == 0 || d.ContainsKey(headers[i])) continue;
                var cell = row.Cell(i + 1);
                d[headers[i]] = cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("dd-MM-yyyy") : cell.GetFormattedString().Trim();
            }
            if (d.Values.Any(v => v.Length > 0)) list.Add(d);
        }
        return (headers.Where(h => h.Length > 0).ToList(), list);
    }

    /// <summary>Column headers that are neither built-in details nor existing custom fields.</summary>
    public static List<string> UnknownHeaders(IEnumerable<string> headers, IReadOnlyList<string> custom)
    {
        var known = BuiltIn.SelectMany(c => c.Aliases.Append(c.Header)).Select(Norm).Concat(custom.Select(Norm)).ToHashSet();
        return headers.Where(h => h.Trim().Length > 0 && !known.Contains(Norm(h))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Adds or updates soldiers from the file rows. Only columns present in the file are changed.</summary>
    public static (int added, int updated, List<string> errors) Import(Store store, List<string> headers, List<Dictionary<string, string>> rows, IReadOnlyList<string> custom)
    {
        var map = new Dictionary<string, string>(); // file header → upsert key
        foreach (var h in headers)
        {
            var n = Norm(h);
            var b = BuiltIn.FirstOrDefault(c => Norm(c.Header) == n || c.Aliases.Any(a => Norm(a) == n));
            if (b.Header != null) { map.TryAdd(h, b.Key); continue; }
            var cf = custom.FirstOrDefault(c => Norm(c) == n);
            if (cf != null) map.TryAdd(h, "cf:" + cf);
        }
        int added = 0, updated = 0, line = 1;
        var errors = new List<string>();
        var known = store.Persons().Select(p => p["id"]?.ToString() ?? "").ToHashSet();
        foreach (var r in rows)
        {
            line++;
            try
            {
                var o = new JsonObject();
                var cfo = new JsonObject();
                foreach (var (h, key) in map)
                {
                    var v = r.GetValueOrDefault(h, "").Trim();
                    if (key.StartsWith("cf:")) cfo[key[3..]] = v;
                    else o[key] = key == "company" ? CanonCompany(v) : v;
                }
                var id = Store.CanonId(o["id"]?.ToString());
                var isNew = id.Length == 0 || !known.Contains(id);
                if (id.Length == 0) id = store.NextId("P", "persons");
                o["id"] = id;
                // Columns missing from the file keep the soldier's current values.
                if (!isNew && store.Person(id) is { } cur)
                    foreach (var c in BuiltIn.Where(c => c.Key is not "id" && !o.ContainsKey(c.Key) && !ExtraKey(c.Key)))
                        o[c.Key] = cur[c.Column]?.ToString() ?? "";
                if (cfo.Count > 0) o["custom"] = cfo;
                store.UpsertPerson(o, "PC-IMPORT");
                known.Add(id);
                if (isNew) added++; else updated++;
            }
            catch (Exception ex) { errors.Add($"Row {line}: {ex.Message}"); }
        }
        return (added, updated, errors);
    }

    static bool ExtraKey(string key) => Store.ExtraPersonFields.Any(f => f.key == key);
}
