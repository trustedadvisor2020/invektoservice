using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Campaigns.Dtos;

/// <summary>
/// FEAT-MCC: one campaign entry inside <c>tenant_settings.campaign_config -> campaigns[]</c>.
///
/// Shape (snake_case JSONB persisted form):
///   {
///     "slug":       "roadshow_ireland_2026",
///     "name":       "Ireland Roadshow 2026",
///     "active":     true,
///     "start_date": "2026-03-01",
///     "end_date":   "2026-03-20",
///     "cities":     [ ... CampaignCity ... ],
///     "dates":      [ ... CampaignDate ... ]
///   }
///
/// Validation guards (TenantCampaignConfigValidator) — INV-BE-118 / INV-BE-120:
///   * <see cref="Slug"/> matches <c>^[a-z][a-z0-9_-]{1,63}$</c> AND not in reserved set
///     ('primary','system','default','all'). Tenant-scope unique enforced at the
///     CampaignConfig level (same slug appearing twice in campaigns[] rejected).
///   * <see cref="StartDate"/> &lt;= <see cref="EndDate"/>; both ISO-8601 calendar dates.
///   * <see cref="Cities"/>: 1..20 entries. Empty list rejected — a campaign with no
///     cities is a smoke-test artefact, not a useful tenant config.
///   * <see cref="Dates"/>: 0..20 entries; each <c>city</c> must reference a known
///     <see cref="CampaignCity.Slug"/> in <see cref="Cities"/>.
///
/// Window guard (INV-BE-119) consults <see cref="Active"/>, <see cref="StartDate"/>,
/// <see cref="EndDate"/> per Q's interview answer (inclusive both edges, in
/// tenant_settings.timezone). See <see cref="ITenantCampaignResolver.IsActiveWindow"/>.
/// </summary>
public sealed class CampaignEntry
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Operator kill-switch. When false, the window guard rejects outbound
    /// even if start_date &lt;= NOW &lt;= end_date. Default true keeps a valid-window campaign live.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    /// <summary>Inclusive start of the active window (YYYY-MM-DD, tenant timezone). Required.</summary>
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    /// <summary>Inclusive end of the active window (YYYY-MM-DD, tenant timezone). Required; must be &gt;= start_date.</summary>
    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("cities")]
    public List<CampaignCity> Cities { get; set; } = new();

    [JsonPropertyName("dates")]
    public List<CampaignDate> Dates { get; set; } = new();
}
