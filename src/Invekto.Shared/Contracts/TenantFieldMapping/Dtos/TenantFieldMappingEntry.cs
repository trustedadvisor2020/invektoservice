using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.TenantFieldMapping.Dtos;

/// <summary>
/// FEAT-TFM MVP: per-tenant semantic field mapping entry. Stored in
/// <c>tenant_settings.field_mapping JSONB</c> as a value keyed by semantic name.
///
/// Shape: { "&lt;semantic_name&gt;": { "source": "cf1..cf10", "type": "enum|string|date|bool|int",
///                                       "enum_values": ["..."], "required": false } }
///
/// Validation guards live in <see cref="Services.TenantFieldMappingValidator"/>.
/// </summary>
public sealed class TenantFieldMappingEntry
{
    /// <summary>INMA reserved key (cf1..cf10). Validator enforces range.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Type whitelist: enum | string | date | bool | int.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>Required for type=enum (validator INV-BE-098). Null for other types.</summary>
    [JsonPropertyName("enum_values")]
    public List<string>? EnumValues { get; set; }

    /// <summary>Optional required-flag (UI hint; v1 not enforced at write time).</summary>
    [JsonPropertyName("required")]
    public bool? Required { get; set; }
}
