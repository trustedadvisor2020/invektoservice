using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW: POST /api/v1/leads/intake/{source_slug} payload.
/// Free-form tenant-landing submission; Chatinbox resolves canonical fields via
/// tenant_landing_settings.landing_field_map. Keys in <c>Fields</c> are the
/// tenant's own field names (e.g. "ad_soyad", "telefon"); values are resolved
/// to the canonical set (name, phone, consent, email, custom_1..10).
/// </summary>
public sealed class LeadIntakeRequest
{
    /// <summary>Source field name -> value. Strings, numbers, bools, or null allowed.</summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, object?> Fields { get; set; } = new();

    /// <summary>UTM passthrough (stored in intake_metadata.utm).</summary>
    [JsonPropertyName("utm")]
    public LeadIntakeUtm? Utm { get; set; }

    /// <summary>Page URL at time of submission (stored in intake_metadata.referer).</summary>
    [JsonPropertyName("referer")]
    public string? Referer { get; set; }

    /// <summary>Tenant-side submission timestamp. Server NOW() used if omitted.</summary>
    [JsonPropertyName("submitted_at")]
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>UTM fields captured at intake.</summary>
public sealed class LeadIntakeUtm
{
    [JsonPropertyName("utm_source")]   public string? UtmSource { get; set; }
    [JsonPropertyName("utm_medium")]   public string? UtmMedium { get; set; }
    [JsonPropertyName("utm_campaign")] public string? UtmCampaign { get; set; }
    [JsonPropertyName("utm_term")]     public string? UtmTerm { get; set; }
    [JsonPropertyName("utm_content")]  public string? UtmContent { get; set; }
}
