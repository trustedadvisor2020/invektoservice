using System.Text.Json;
using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: POST /api/v1/tenant/landing/dry-run request body.
/// Diagnostic path — UI sends a sample payload + optionally the UNSAVED draft
/// field_map to preview the resolver's output WITHOUT writing any DB row or
/// enqueueing welcome flow. Reuses the exact FieldMapResolver +
/// PhoneE164Normalizer + consent-check code path as LeadIntakeService.IntakeAsync.
/// When <see cref="FieldMapOverride"/> is null, the tenant's currently-saved
/// field_map is used. When non-null, the override wins (lets the UI preview
/// unsaved edits). <see cref="PhoneCountryHintOverride"/> follows the same
/// override-or-saved-value rule.
/// </summary>
public sealed class DryRunRequest
{
    /// <summary>Source slug to simulate (validated against same regex as landing endpoint). Defaults to 'dryrun' when empty.</summary>
    [JsonPropertyName("source_slug")]
    public string? SourceSlug { get; set; }

    /// <summary>Free-form payload fields (the tenant's landing form would POST this shape).</summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, JsonElement>? Payload { get; set; }

    /// <summary>Draft field_map (source -> canonical). When null, saved map is used.</summary>
    [JsonPropertyName("field_map_override")]
    public Dictionary<string, string>? FieldMapOverride { get; set; }

    /// <summary>Draft phone country hint. When null, saved value is used.</summary>
    [JsonPropertyName("phone_country_hint_override")]
    public string? PhoneCountryHintOverride { get; set; }
}
