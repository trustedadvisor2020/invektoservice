using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Campaigns.Dtos;

/// <summary>
/// FEAT-MCC: top-level shape of <c>tenant_settings.campaign_config JSONB</c>.
///
/// Wraps the campaigns array under a <c>campaigns</c> key (rather than serializing
/// the array directly) for forward-compat: future global flags (default_campaign_slug,
/// substitution_rendering_mode, etc.) land as siblings without a JSONB shape break.
///
/// Empty config (`{ "campaigns": [] }`) is the safe default — every existing tenant
/// observes zero campaigns until an operator edits via Dashboard, so the window guard
/// and `{{campaign.*}}` substitution become no-ops, preserving backward-compat.
/// </summary>
public sealed class CampaignConfig
{
    [JsonPropertyName("campaigns")]
    public List<CampaignEntry> Campaigns { get; set; } = new();

    /// <summary>Convenience factory used by resolvers when DB row is missing or column is empty.</summary>
    public static CampaignConfig Empty() => new() { Campaigns = new List<CampaignEntry>() };
}
