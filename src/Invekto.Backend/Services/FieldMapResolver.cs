using System.Text.Json;
using Invekto.Shared.Contracts.Leads;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW: resolves a tenant's landing_field_map JSONB + an incoming intake
/// payload into canonical field values. Enforces Required canonical presence;
/// unmapped Optionals become nulls; unknown canonical keys in the map are kept
/// but ignored (future canonical extension tolerated).
/// </summary>
public sealed class FieldMapResolver
{
    /// <summary>Parse the tenant_landing_settings.landing_field_map JSONB column.</summary>
    public LandingFieldMap ParseMap(string? landingFieldMapJson)
    {
        var result = new LandingFieldMap();
        if (string.IsNullOrWhiteSpace(landingFieldMapJson)) return result;

        using var doc = JsonDocument.Parse(landingFieldMapJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            var value = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (string.Equals(prop.Name, LeadIntakeCanonical.PhoneCountryHintKey, StringComparison.OrdinalIgnoreCase))
                result.PhoneCountryHint = value.Trim().ToUpperInvariant();
            else
                result.Map[prop.Name] = value;
        }

        return result;
    }

    /// <summary>
    /// Try to pull the canonical value from <paramref name="fields"/> using the
    /// resolved source field name. Returns true iff the canonical is mapped AND
    /// the source key exists in the payload (value may be null/empty — caller
    /// decides how to treat that per-canonical).
    /// </summary>
    public bool TryResolve(
        LandingFieldMap map,
        IReadOnlyDictionary<string, object?> fields,
        string canonical,
        out object? value,
        out string? sourceFieldName)
    {
        sourceFieldName = map.GetSourceField(canonical);
        if (sourceFieldName == null)
        {
            value = null;
            return false;
        }
        return fields.TryGetValue(sourceFieldName, out value);
    }

    /// <summary>Shortcut: resolve to string (JsonElement or primitive) or null.</summary>
    public string? ResolveString(LandingFieldMap map, IReadOnlyDictionary<string, object?> fields, string canonical)
    {
        if (!TryResolve(map, fields, canonical, out var value, out _)) return null;
        return ValueToString(value);
    }

    /// <summary>
    /// Resolve a canonical field to bool. Accepts JsonElement (True/False/String
    /// "true"/"false"), CLR bool, or string "true"/"false" (case-insensitive).
    /// Anything else (null, numbers, objects) returns null — caller treats as
    /// "not true" (INV-BE-105).
    /// </summary>
    public bool? ResolveBool(LandingFieldMap map, IReadOnlyDictionary<string, object?> fields, string canonical)
    {
        if (!TryResolve(map, fields, canonical, out var value, out _)) return null;
        return ValueToBool(value);
    }

    private static string? ValueToString(object? value)
    {
        if (value is null) return null;
        if (value is string s) return s;
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => null
            };
        }
        return value.ToString();
    }

    private static bool? ValueToBool(object? value)
    {
        if (value is null) return null;
        if (value is bool b) return b;
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(el.GetString(), out var parsed) => parsed,
                _ => null
            };
        }
        if (value is string s && bool.TryParse(s, out var parsedStr)) return parsedStr;
        return null;
    }
}
