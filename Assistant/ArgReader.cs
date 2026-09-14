using System.Globalization;
using System.Text.Json;

namespace TaskAssistant.Mvc.Assistant;

/// <summary>Defensive readers for the loosely-typed arguments Gemini sends back for a function call.</summary>
public static class ArgReader
{
    public static string? GetString(Dictionary<string, JsonElement> args, string key)
    {
        if (args.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        return null;
    }

    public static int? GetInt(Dictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Number => (int)el.GetDouble(),
            JsonValueKind.String when int.TryParse(el.GetString(), out var i) => i,
            _ => null,
        };
    }

    public static bool? GetBool(Dictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
            _ => null,
        };
    }

    /// <summary>Returns false only when the key is present but its value cannot be parsed as a date.</summary>
    public static bool TryGetDate(Dictionary<string, JsonElement> args, string key, out DateTime? value)
    {
        value = null;
        if (!args.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String) return true;

        var s = el.GetString();
        if (string.IsNullOrWhiteSpace(s)) return true;

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }
}
