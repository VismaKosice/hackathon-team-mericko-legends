using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PensionCalculationEngine.Api.Services;

/// <summary>
/// High-performance property extraction utility for mutation properties.
/// Uses aggressive inlining and optimized type checking.
/// </summary>
public static class PropertyExtractor
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return string.Empty;

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetString(Dictionary<string, object> props, string key, [NotNullWhen(true)] out string? result)
    {
        if (props.TryGetValue(key, out var value))
        {
            result = value switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
                _ => value.ToString()
            };
            return result != null;
        }
        result = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateOnly GetDate(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return DateOnly.MinValue;

        return value switch
        {
            DateOnly d => d,
            JsonElement { ValueKind: JsonValueKind.String } je => ParseDateFast(je.GetString()!),
            string s => ParseDateFast(s),
            _ => DateOnly.MinValue
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetDate(Dictionary<string, object> props, string key, out DateOnly result)
    {
        if (props.TryGetValue(key, out var value))
        {
            switch (value)
            {
                case DateOnly d:
                    result = d;
                    return true;
                case JsonElement { ValueKind: JsonValueKind.String } je:
                    return TryParseDateFast(je.GetString()!, out result);
                case string s:
                    return TryParseDateFast(s, out result);
            }
        }
        result = DateOnly.MinValue;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal GetDecimal(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return 0m;

        return value switch
        {
            decimal d => d,
            JsonElement je => GetDecimalFromJsonElement(je),
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetInt(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return 0;

        return value switch
        {
            int i => i,
            JsonElement je => GetIntFromJsonElement(je),
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static decimal GetDecimalFromJsonElement(JsonElement je)
    {
        return je.ValueKind switch
        {
            JsonValueKind.Number => je.TryGetDecimal(out var d) ? d : (decimal)je.GetDouble(),
            JsonValueKind.String => decimal.TryParse(je.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => 0m
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetIntFromJsonElement(JsonElement je)
    {
        return je.ValueKind switch
        {
            JsonValueKind.Number => je.TryGetInt32(out var i) ? i : (int)je.GetDouble(),
            JsonValueKind.String => int.TryParse(je.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0,
            _ => 0
        };
    }

    // Fast date parsing for "YYYY-MM-DD" format (ISO 8601)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateOnly ParseDateFast(string dateStr)
    {
        // Fast path for ISO format: YYYY-MM-DD
        if (dateStr.Length == 10 && dateStr[4] == '-' && dateStr[7] == '-')
        {
            var year = int.Parse(dateStr.AsSpan(0, 4));
            var month = int.Parse(dateStr.AsSpan(5, 2));
            var day = int.Parse(dateStr.AsSpan(8, 2));
            return new DateOnly(year, month, day);
        }
        
        // Fallback to standard parsing
        return DateOnly.Parse(dateStr, CultureInfo.InvariantCulture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseDateFast(string dateStr, out DateOnly result)
    {
        try
        {
            result = ParseDateFast(dateStr);
            return true;
        }
        catch
        {
            result = DateOnly.MinValue;
            return false;
        }
    }
}
