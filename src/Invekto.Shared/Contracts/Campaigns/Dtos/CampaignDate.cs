using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Campaigns.Dtos;

/// <summary>
/// FEAT-MCC: a single per-city event date inside a campaign. Persisted inside
/// <c>tenant_settings.campaign_config -> campaigns[].dates[]</c>.
///
/// Shape: { "city" (slug), "date" (YYYY-MM-DD), "hours" (free text e.g. "09:00-18:00") }.
/// Validator enforces:
///   * <see cref="City"/> MUST reference an existing <see cref="CampaignCity.Slug"/> in the
///     same campaign (no orphan dates → INV-BE-118).
///   * <see cref="Date"/> MUST parse as ISO-8601 calendar date (no timezone offset).
///   * <see cref="Hours"/> is free-form display text (not parsed); used only in customer
///     message rendering. Bounded to 64 chars at the validator layer to keep templates safe.
/// </summary>
public sealed class CampaignDate
{
    /// <summary>City slug — must match a <see cref="CampaignCity.Slug"/> in the same campaign.</summary>
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    /// <summary>ISO-8601 calendar date (YYYY-MM-DD). Stored as string to preserve operator intent.</summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    /// <summary>Display hours range (e.g. "09:00-18:00"). Free text, max 64 chars.</summary>
    [JsonPropertyName("hours")]
    public string? Hours { get; set; }
}
