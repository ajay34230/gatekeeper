using XV.Core;

/// <summary>Fake entries for previewing the route chart exports (nothing here touches a real database).</summary>
static class DemoRoute
{
    static Dictionary<string, object?> Ev(DateTime at, string type, string place, string gate, long stayMin = 0, string reason = "", string remarks = "", string from = "", DateTime? back = null, string op = "GK-01") => new()
    {
        ["event_ts"] = new DateTimeOffset(at).ToUnixTimeMilliseconds(), ["event_type"] = type, ["location_name"] = place, ["gate_name"] = gate,
        ["stay_ms"] = stayMin * 60_000L, ["reason"] = reason, ["remarks"] = remarks, ["coming_from"] = from, ["loc_mismatch"] = 0L,
        ["expected_return"] = back == null ? 0L : new DateTimeOffset(back.Value).ToUnixTimeMilliseconds(), ["operator_id"] = op,
    };

    static Dictionary<string, object?> Trip(this Dictionary<string, object?> ev, DateTime left, string dest, string state, int approx, int taken = 0, string end = "", string via = "", string by = "")
    {
        ev["tr_transit_id"] = "TR-DEMO"; ev["tr_dest_name"] = dest; ev["tr_state"] = state; ev["tr_expected_min"] = (long)approx;
        ev["tr_due_at"] = approx > 0 ? new DateTimeOffset(left.AddMinutes(approx)).ToUnixTimeMilliseconds() : 0L;
        ev["tr_actual_min"] = taken > 0 ? taken : null; ev["tr_end_name"] = end; ev["tr_resolved_via"] = via; ev["tr_resolved_by"] = by; ev["tr_note"] = "";
        ev["tr_left_at"] = new DateTimeOffset(left).ToUnixTimeMilliseconds();
        return ev;
    }

    public static void Write(string dir)
    {
        Directory.CreateDirectory(dir);
        var d = DateTime.Today;

        // ---- a person: duty, TD at the range, annual leave, rejoining, now out on duty
        var person = new List<Dictionary<string, object?>>
        {
            Ev(d.AddDays(-12).AddHours(8).AddMinutes(15), "ENTRY", "Battalion HQ", "Main Gate", from: "Officers' Mess"),
            Ev(d.AddDays(-12).AddHours(17).AddMinutes(40), "EXIT", "Battalion HQ", "Main Gate", stayMin: 565, reason: "Duty over"),
            Ev(d.AddDays(-11).AddHours(8).AddMinutes(5), "ENTRY", "Battalion HQ", "Main Gate", from: "Officers' Mess"),
            Ev(d.AddDays(-11).AddHours(13).AddMinutes(10), "EXIT", "Battalion HQ", "Main Gate", stayMin: 305, reason: "TD", remarks: "Range firing, Firing Range", back: d.AddDays(-9).AddHours(23).AddMinutes(59)),
            Ev(d.AddDays(-9).AddHours(7).AddMinutes(50), "ENTRY", "Firing Range", "Range Gate", from: "Battalion HQ"),
            Ev(d.AddDays(-9).AddHours(16).AddMinutes(30), "EXIT", "Firing Range", "Range Gate", stayMin: 520, reason: "TD complete"),
            Ev(d.AddDays(-9).AddHours(17).AddMinutes(5), "ENTRY", "Battalion HQ", "Main Gate", from: "Firing Range"),
            Ev(d.AddDays(-6).AddHours(9), "EXIT", "Battalion HQ", "Main Gate", stayMin: 4_320, reason: "Proceeding on Leave", remarks: "Annual leave, pass no. 42", back: d.AddDays(-2).AddHours(23).AddMinutes(59)),
            Ev(d.AddDays(-1).AddHours(10).AddMinutes(20), "ENTRY", "Battalion HQ", "Main Gate", reason: "Rejoining from Leave", remarks: "Pass no. 42", from: "Home"),
            Ev(d.AddHours(12).AddMinutes(30) > DateTime.Now ? d.AddHours(1) : d.AddHours(12).AddMinutes(30), "EXIT", "Battalion HQ", "Main Gate", stayMin: 1_600, reason: "Official duty", remarks: "Meeting at Brigade HQ"),
        };
        foreach (var r in person) r["entity_type"] = "PERSON";
        person.Reverse();

        // ---- a vehicle: every kind of outcome, ending with one that is late right now
        var v0 = d.AddDays(-2).AddHours(7).AddMinutes(30);
        var left1 = v0.AddMinutes(40); var left2 = v0.AddMinutes(130); var left3 = v0.AddMinutes(185); var left4 = v0.AddMinutes(290);
        var back = d.AddDays(-1).AddHours(6).AddMinutes(50);
        var left5 = DateTime.Now.AddHours(-3);
        var vehicle = new List<Dictionary<string, object?>>
        {
            Ev(v0, "ENTRY", "Battalion HQ", "Main Gate"),
            Ev(left1, "EXIT", "Battalion HQ", "Main Gate", stayMin: 40).Trip(left1, "Armory", "REACHED", 20, 18, "Armory", "SCAN"),
            Ev(left1.AddMinutes(18), "ENTRY", "Armory", "Armory Gate"),
            Ev(left2, "EXIT", "Armory", "Armory Gate", stayMin: 72).Trip(left2, "Fuel Point", "REACHED", 15, 22, "Fuel Point", "RP", "GK-02"),
            Ev(left2.AddMinutes(22), "ENTRY", "Fuel Point", "Fuel Gate"),
            Ev(left3, "EXIT", "Fuel Point", "Fuel Gate", stayMin: 33).Trip(left3, "Firing Range", "DIVERTED", 40, 25, "Medical Centre", "SCAN"),
            Ev(left3.AddMinutes(25), "ENTRY", "Medical Centre", "Medical Gate"),
            Ev(left4, "EXIT", "Medical Centre", "Medical Gate", stayMin: 80).Trip(left4, "Battalion HQ", "STOPPED", 20, 14, "Roadside near Km 12 (tyre puncture)", "SERVER"),
            Ev(back, "ENTRY", "Battalion HQ", "Main Gate", remarks: "Recovered and towed in"),
            Ev(left5, "EXIT", "Battalion HQ", "Main Gate", stayMin: (long)(left5 - back).TotalMinutes).Trip(left5, "Armory", "EN_ROUTE", 20),
        };
        foreach (var r in vehicle) r["entity_type"] = "VEHICLE";
        vehicle.Reverse();

        Reports.RouteChartExcel(person, "Capt Rajesh Kumar  •  P-014  •  Alpha Company", "Movement history (FAKE DEMO DATA)", Path.Combine(dir, "Route-Chart-PERSON-demo.xlsx"));
        Reports.RouteChartPdf(person, "Capt Rajesh Kumar  •  P-014  •  Alpha Company", "Movement history (FAKE DEMO DATA)", Path.Combine(dir, "Route-Chart-PERSON-demo.pdf"));
        Reports.RouteChartExcel(vehicle, "KA-01-MT-4521  •  V-001  •  Supply Truck", "Movement history (FAKE DEMO DATA)", Path.Combine(dir, "Route-Chart-VEHICLE-demo.xlsx"));
        Reports.RouteChartPdf(vehicle, "KA-01-MT-4521  •  V-001  •  Supply Truck", "Movement history (FAKE DEMO DATA)", Path.Combine(dir, "Route-Chart-VEHICLE-demo.pdf"));
    }
}
