using System.Text.Json;
using Chatinbox.Shared.Contracts.Leads;

namespace Chatinbox.Backend.Services;

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
    /// FEAT-LIW Chunk C: build a <see cref="LandingFieldMap"/> from an in-memory
    /// dictionary (Dashboard's draft editor state — not persisted yet). Input
    /// shape mirrors UpdateFieldMapRequest: { source_field -> canonical }.
    /// Inverts to the internal canonical->source map direction + adds the
    /// optional phone country hint. Unlike <see cref="ParseMap"/> there is no
    /// JSON parsing / exception path — this overload is only called from the
    /// dry-run code path where the UI has already given us strongly-typed
    /// dictionary content.
    /// </summary>
    public LandingFieldMap FromSourceToCanonical(
        IDictionary<string, string>? sourceToCanonical,
        string? phoneCountryHint)
    {
        var result = new LandingFieldMap();
        if (sourceToCanonical != null)
        {
            foreach (var kvp in sourceToCanonical)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                    continue;
                // Internal map direction is canonical -> source_field.
                result.Map[kvp.Value] = kvp.Key;
            }
        }
        if (!string.IsNullOrWhiteSpace(phoneCountryHint))
            result.PhoneCountryHint = phoneCountryHint.Trim().ToUpperInvariant();
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
