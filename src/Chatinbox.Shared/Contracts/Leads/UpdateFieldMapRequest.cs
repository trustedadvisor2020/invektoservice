using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: PUT /api/v1/tenant/landing/fieldmap request body.
/// <see cref="FieldMap"/> holds { source_field_name -> canonical_field_name }
/// (inverse of the stored LandingFieldMap direction — the UI is more natural
/// when authored as "source -> canonical" since tenants think in terms of
/// their own form field names first). The endpoint inverts the dictionary and
/// validates via FieldMapValidator before persisting.
/// <see cref="ExpectedRowVersion"/> is the optimistic-concurrency guard; if
/// absent, the endpoint falls back to the If-Match header.
/// <see cref="PhoneCountryHint"/> is surfaced as a top-level field rather than
/// a synthetic 'phone.country_hint' row so the UI dropdown allowlist stays clean;
/// persisted back into the JSONB under the reserved key by the service.
/// </summary>
public sealed class UpdateFieldMapRequest
{
    [JsonPropertyName("field_map")]
    public Dictionary<string, string> FieldMap { get; set; } = new();

    [JsonPropertyName("phone_country_hint")]
    public string? PhoneCountryHint { get; set; }

    [JsonPropertyName("expected_row_version")]
    public DateTime? ExpectedRowVersion { get; set; }
}

/// <summary>
/// FEAT-LIW Chunk C: PUT /api/v1/tenant/landing/fieldmap 200 response.
/// Returns the normalized persisted map (canonical direction, same shape as
/// TenantLandingSettingsDto.FieldMap) plus the new row_version.
/// </summary>
public sealed class UpdateFieldMapResponse
{
    [JsonPropertyName("field_map")]
    public Dictionary<string, string> FieldMap { get; set; } = new();

    [JsonPropertyName("phone_country_hint")]
    public string? PhoneCountryHint { get; set; }

    [JsonPropertyName("row_version")]
    public DateTime RowVersion { get; set; }
}
