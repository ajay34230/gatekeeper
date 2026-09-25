using System.Text;
using System.Text.Json.Nodes;
using QRCoder;

namespace XV.Core;

public static class Pairing
{
    /// <summary>
    /// Text encoded in the pairing QR: "XVGK1:" + base64url(JSON). Carries addresses, the certificate
    /// fingerprint the phone must pin, and a one-time enrollment code. Contains no long-lived secret.
    /// </summary>
    public static string Payload(Settings s, string fingerprint, string code) =>
        "XVGK1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["id"] = s.ServerId,
            ["n"] = s.ServerName,
            ["h"] = new JsonArray(NetUtil.LanAddresses().Select(a => (JsonNode)a).ToArray()),
            ["p"] = s.Port,
            ["u"] = s.InternetEnabled ? s.PublicUrl : "",
            ["pc"] = s.CloudUsesPublicCertificate,
            ["fp"] = fingerprint,
            ["c"] = code,
        }.ToJsonString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] QrPng(string text, int pixelsPerModule = 10) =>
        new PngByteQRCode(new QRCodeGenerator().CreateQrCode(text, QRCodeGenerator.ECCLevel.M)).GetGraphic(pixelsPerModule);
}

public static class Csv
{
    public static string Build(IEnumerable<Dictionary<string, object?>> rows, params (string header, string key)[] cols)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => Esc(c.header))));
        foreach (var r in rows) sb.AppendLine(string.Join(",", cols.Select(c => Esc(r.TryGetValue(c.key, out var v) ? v?.ToString() ?? "" : ""))));
        return sb.ToString();
    }

    static string Esc(string v)
    {
        v = Neutralize(v);
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    /// <summary>
    /// Spreadsheet formula-injection guard (CWE-1236): a cell starting with = + - @ or a tab/CR is prefixed with an apostrophe,
    /// so Excel shows it as text instead of running it. Values typed on terminals (remarks, reasons) are untrusted.
    /// </summary>
    public static string Neutralize(string v) => v.Length > 0 && "=+-@\t\r".Contains(v[0]) ? "'" + v : v;

    /// <summary>Reverses <see cref="Neutralize"/> when a file exported here is imported again.</summary>
    public static string Restore(string v) => v.Length > 1 && v[0] == '\'' && "=+-@\t\r".Contains(v[1]) ? v[1..] : v;

    /// <summary>Minimal RFC-4180 reader used by the registry import.</summary>
    public static List<Dictionary<string, string>> Parse(string text)
    {
        var rows = ParseRows(text);
        if (rows.Count == 0) return [];
        var head = rows[0].Select(h => h.Trim()).ToList();
        return rows.Skip(1).Where(r => r.Any(c => c.Trim().Length > 0))
            .Select(r => head.Select((h, i) => (h, v: i < r.Count ? Restore(r[i].Trim()) : "")).Where(x => x.h.Length > 0).DistinctBy(x => x.h, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.h, x => x.v, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Raw CSV rows (RFC 4180 quoting); the first row is the header.</summary>
    public static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var q = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (q) { if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; } else cell.Append(ch); }
            else if (ch == '"') q = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; }
            else if (ch != '\r') cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}

public static class RegistryImport
{
    static string Pick(Dictionary<string, string> r, params string[] keys)
    {
        foreach (var k in keys) if (r.TryGetValue(k, out var v) && v.Length > 0) return v;
        return "";
    }

    /// <summary>Imports personnel from CSV (headers: id,name,rank,service_no,unit,company,role,category,status,mobile,id_card,blood_group,access_locations). Returns (ok, errors).</summary>
    public static (int ok, List<string> errors) Persons(Store store, string csv)
    {
        // Same importer as the soldier register: columns missing from the file keep their current values, and rows
        // without an ID are matched by army number (no duplicate soldiers on re-import).
        var headers = Csv.ParseRows(csv).FirstOrDefault()?.Select(h => h.Trim()).Where(h => h.Length > 0).ToList() ?? [];
        var (added, updated, errors) = SoldierFile.Import(store, headers, Csv.Parse(csv), store.Settings.CustomFields);
        return (added + updated, errors);
    }

    /// <summary>Imports vehicles from CSV (headers: id,plate,mil_reg,type,model,company,status).</summary>
    public static (int ok, List<string> errors) Vehicles(Store store, string csv)
    {
        int ok = 0; var errors = new List<string>(); var line = 1;
        foreach (var r in Csv.Parse(csv))
        {
            line++;
            try
            {
                var id = Pick(r, "id", "vehicle id", "vehicle_id");
                var plate = Pick(r, "plate", "registration", "plate number");
                string Norm(string p) => new string(p.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
                // No ID: the same plate is the same vehicle (re-importing does not duplicate the fleet).
                if (id.Length == 0 && plate.Length > 0)
                    id = store.Vehicles().FirstOrDefault(v => Norm(v["plate"]?.ToString() ?? "") == Norm(plate))?["id"]?.ToString() ?? "";
                if (id.Length == 0) id = store.NextId("V", "vehicles");
                var cur = store.Vehicles().FirstOrDefault(v => v["id"]?.ToString() == Store.CanonId(id));
                // Columns missing from the file (or left empty) keep the vehicle's current values.
                string Keep(string value, string column) => value.Length > 0 || cur == null ? value : cur[column]?.ToString() ?? "";
                store.UpsertVehicle(new System.Text.Json.Nodes.JsonObject
                {
                    ["id"] = id, ["plate"] = Keep(plate, "plate"), ["milReg"] = Keep(Pick(r, "mil_reg", "military reg"), "mil_reg"),
                    ["type"] = Keep(Pick(r, "type"), "type"), ["model"] = Keep(Pick(r, "model"), "model"), ["company"] = Keep(Pick(r, "company"), "company"),
                    ["status"] = Keep(Pick(r, "status"), "status"),
                }, "PC-IMPORT");
                ok++;
            }
            catch (Exception ex) { errors.Add($"Row {line}: {ex.Message}"); }
        }
        return (ok, errors);
    }
}
