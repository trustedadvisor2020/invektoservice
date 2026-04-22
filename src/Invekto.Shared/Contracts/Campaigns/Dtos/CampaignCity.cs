using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Campaigns.Dtos;

/// <summary>
/// FEAT-MCC: a single city inside a tenant campaign. Persisted inside
/// <c>tenant_settings.campaign_config -> campaigns[].cities[]</c>.
///
/// Shape: { "slug", "name", "country" (ISO-3166-1 alpha-2), "timezone" (IANA) }.
/// Validator (TenantCampaignConfigValidator) enforces lowercase slug regex,
/// non-empty name + country, and a timezone string format check (IANA shape).
/// </summary>
public sealed class CampaignCity
{
    /// <summary>Lowercase slug, [a-z0-9_-]{1,40}. Cross-referenced from <see cref="CampaignDate.City"/>.</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Display name shown in customer messages and Dashboard editor (e.g. "Dublin").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO-3166-1 alpha-2 country code (e.g. "IE"). Optional but encouraged for locale rendering.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>IANA timezone (e.g. "Europe/Dublin"). Optional; falls back to tenant_settings.timezone for window guard.</summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}
