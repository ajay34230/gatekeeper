using System.Text.Json;
using System.Text.Json.Nodes;

namespace XV.Core;

/// <summary>Granular backup/export/import for individual data types (personnel, vehicles, accounts, settings).</summary>
public static class GranularBackup
{
    // ====== PERSONNEL (SOLDIERS) ======

    public static string ExportPersonnelAsJson(Store store)
    {
        var personnel = new JsonArray();
        foreach (var p in store.Persons())
        {
            var person = new JsonObject
            {
                ["id"] = p["id"]?.ToString() ?? "",
                ["name"] = p["name"]?.ToString() ?? "",
                ["rank"] = p["rank"]?.ToString() ?? "",
                ["company"] = p["company"]?.ToString() ?? "",
                ["platoon"] = p["platoon"]?.ToString() ?? "",
                ["section"] = p["section"]?.ToString() ?? "",
                ["mobile"] = p["mobile"]?.ToString() ?? "",
                ["status"] = p["status"]?.ToString() ?? "",
                ["notes"] = p["notes"]?.ToString() ?? "",
            };
            personnel.Add(person);
        }
        var backup = new JsonObject { ["personnel"] = personnel };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int ImportPersonnelFromJson(Store store, string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid JSON format");
        if (obj["personnel"] is not JsonArray personnel) throw new InvalidOperationException("Missing personnel array");

        var count = 0;
        foreach (var p in personnel.OfType<JsonObject>())
        {
            var id = p["id"]?.ToString() ?? throw new InvalidOperationException("Missing personnel id");
            var name = p["name"]?.ToString() ?? "";
            var rank = p["rank"]?.ToString() ?? "";
            var company = p["company"]?.ToString() ?? "";
            var platoon = p["platoon"]?.ToString() ?? "";
            var section = p["section"]?.ToString() ?? "";
            var mobile = p["mobile"]?.ToString() ?? "";
            var status = p["status"]?.ToString() ?? "active";
            var notes = p["notes"]?.ToString() ?? "";

            try
            {
                store.ImportPersonnel(id, name, rank, company, platoon, section, mobile, status, notes);
                count++;
            }
            catch { }
        }
        return count;
    }

    // ====== VEHICLES ======

    public static string ExportVehiclesAsJson(Store store)
    {
        var vehicles = new JsonArray();
        foreach (var v in store.Vehicles())
        {
            var vehicle = new JsonObject
            {
                ["id"] = v["id"]?.ToString() ?? "",
                ["plate"] = v["plate"]?.ToString() ?? "",
                ["mil_reg"] = v["mil_reg"]?.ToString() ?? "",
                ["type"] = v["type"]?.ToString() ?? "",
                ["model"] = v["model"]?.ToString() ?? "",
                ["company"] = v["company"]?.ToString() ?? "",
                ["status"] = v["status"]?.ToString() ?? "",
            };
            vehicles.Add(vehicle);
        }
        var backup = new JsonObject { ["vehicles"] = vehicles };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int ImportVehiclesFromJson(Store store, string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid JSON format");
        if (obj["vehicles"] is not JsonArray vehicles) throw new InvalidOperationException("Missing vehicles array");

        var count = 0;
        foreach (var v in vehicles.OfType<JsonObject>())
        {
            var id = v["id"]?.ToString() ?? throw new InvalidOperationException("Missing vehicle id");
            var plate = v["plate"]?.ToString() ?? "";
            var milReg = v["mil_reg"]?.ToString() ?? "";
            var type = v["type"]?.ToString() ?? "";
            var model = v["model"]?.ToString() ?? "";
            var company = v["company"]?.ToString() ?? "";
            var status = v["status"]?.ToString() ?? "active";

            try
            {
                store.ImportVehicle(id, plate, milReg, type, model, company, status);
                count++;
            }
            catch { }
        }
        return count;
    }

    // ====== ACCOUNTS (OPERATORS) ======

    public static string ExportAccountsAsJson(Store store)
    {
        var accounts = new JsonArray();
        foreach (var acc in store.Accounts())
        {
            accounts.Add(new JsonObject
            {
                ["id"] = acc["id"]?.ToString() ?? "",
                ["name"] = acc["name"]?.ToString() ?? "",
                ["role"] = acc["role"]?.ToString() ?? "",
                ["status"] = acc["status"]?.ToString() ?? "",
                ["created_at"] = acc["created_at"]?.ToString(),
                ["created_by"] = acc["created_by"]?.ToString() ?? "",
                ["last_login"] = acc["last_login"]?.ToString(),
            });
        }
        var backup = new JsonObject { ["accounts"] = accounts };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int ImportAccountsFromJson(Store store, string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid JSON format");
        if (obj["accounts"] is not JsonArray accounts) throw new InvalidOperationException("Missing accounts array");

        var count = 0;
        foreach (var acc in accounts.OfType<JsonObject>())
        {
            var id = acc["id"]?.ToString() ?? throw new InvalidOperationException("Missing account id");
            var name = acc["name"]?.ToString() ?? "";
            var role = acc["role"]?.ToString() ?? "OPERATOR";
            var status = acc["status"]?.ToString() ?? "ACTIVE";

            try
            {
                store.ImportAccount(id, name, role, status);
                count++;
            }
            catch { }
        }
        return count;
    }

    // ====== DEVICES / TERMINALS ======

    public static string ExportDevicesAsJson(Store store)
    {
        var devices = new JsonArray();
        foreach (var dev in store.Devices())
        {
            devices.Add(new JsonObject
            {
                ["device_id"] = dev["device_id"]?.ToString() ?? "",
                ["name"] = dev["name"]?.ToString() ?? "",
                ["model"] = dev["model"]?.ToString() ?? "",
                ["active"] = dev["active"] is true ? true : false,
                ["paired_at"] = dev["paired_at"]?.ToString(),
                ["last_seen"] = dev["last_seen"]?.ToString(),
                ["location_id"] = dev["location_id"]?.ToString() ?? "",
                ["gate_id"] = dev["gate_id"]?.ToString() ?? "",
                ["operator_id"] = dev["operator_id"]?.ToString() ?? "",
            });
        }
        var backup = new JsonObject { ["devices"] = devices };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int ImportDevicesFromJson(Store store, string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid JSON format");
        if (obj["devices"] is not JsonArray devices) throw new InvalidOperationException("Missing devices array");

        var count = 0;
        foreach (var dev in devices.OfType<JsonObject>())
        {
            var deviceId = dev["device_id"]?.ToString() ?? throw new InvalidOperationException("Missing device_id");
            var name = dev["name"]?.ToString() ?? "";
            var model = dev["model"]?.ToString() ?? "";
            var locationId = dev["location_id"]?.ToString() ?? "";
            var gateId = dev["gate_id"]?.ToString() ?? "";
            var operatorId = dev["operator_id"]?.ToString() ?? "";

            try
            {
                store.ImportDevice(deviceId, name, model, locationId, gateId, operatorId);
                count++;
            }
            catch { }
        }
        return count;
    }

    // ====== SETTINGS ======

    public static string ExportSettingsAsJson()
    {
        var settings = File.ReadAllText(Paths.File("settings.json"));
        var obj = JsonNode.Parse(settings) as JsonObject ?? throw new InvalidOperationException("Invalid settings.json");
        var backup = new JsonObject { ["settings"] = obj };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public static void ImportSettingsFromJson(string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid JSON format");
        if (obj["settings"] is not JsonObject settings) throw new InvalidOperationException("Missing settings object");

        var backup = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Paths.File("settings.json.backup"), backup);
        File.WriteAllText(Paths.File("settings.json"), backup);
    }
}
