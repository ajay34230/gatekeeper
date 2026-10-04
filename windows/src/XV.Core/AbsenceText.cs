namespace XV.Core;

/// <summary>Plain wording for people who have not come back, chosen from the reason they left.</summary>
public static class AbsenceText
{
    static bool Has(string text, params string[] words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>"has not returned from leave", "has not returned from temporary duty", … Reason and remarks are both searched,
    /// because exits typed in on the PC may only mention the leave in the remarks.</summary>
    public static string NotReturned(string reason, string remarks, string kind = "RETURN")
    {
        if (kind == "VISITOR_OVERSTAY") return "has overstayed the visitor pass";
        var text = $"{reason} {remarks}";
        if (Has(text, "leave")) return "has not returned from leave";
        if (reason.Trim().Equals("TD", StringComparison.OrdinalIgnoreCase) || Has(text, "temporary duty", "tdy")) return "has not returned from temporary duty";
        if (Has(text, "local work")) return "has not returned from local work";
        if (Has(text, "course")) return "has not returned from course";
        if (Has(text, "medical", "hospital")) return "has not returned from medical leave";
        return reason.Trim().Length > 0 ? $"has not returned ({reason.Trim()})" : "has not returned";
    }

    /// <summary>"is due back from leave" for the reminder before the return date.</summary>
    public static string DueBack(string reason, string remarks)
    {
        var text = $"{reason} {remarks}";
        if (Has(text, "leave")) return "is due back from leave";
        if (reason.Trim().Equals("TD", StringComparison.OrdinalIgnoreCase) || Has(text, "temporary duty", "tdy")) return "is due back from temporary duty";
        if (Has(text, "local work")) return "is due back from local work";
        if (Has(text, "course")) return "is due back from course";
        return "is due back";
    }
}
