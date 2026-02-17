using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Attribution;

/// <summary>
/// Lead attribution response item. GET /api/v1/attribution/leads
/// </summary>
public sealed class LeadAttributionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("chat_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ChatId { get; set; }

    [JsonPropertyName("utm_source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UtmSource { get; set; }

    [JsonPropertyName("utm_medium")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UtmMedium { get; set; }

    [JsonPropertyName("utm_campaign")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UtmCampaign { get; set; }

    [JsonPropertyName("utm_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UtmContent { get; set; }

    [JsonPropertyName("utm_term")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UtmTerm { get; set; }

    [JsonPropertyName("meta_click_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MetaClickId { get; set; }

    [JsonPropertyName("lead_source")]
    public string LeadSource { get; set; } = "direct";

    [JsonPropertyName("lead_labels")]
    public List<string> LeadLabels { get; set; } = [];

    [JsonPropertyName("conversion_status")]
    public string ConversionStatus { get; set; } = "new";

    [JsonPropertyName("conversion_value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ConversionValue { get; set; }

    [JsonPropertyName("converted_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ConvertedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Track attribution from webhook. Internal use (conversation_started event).
/// </summary>
public sealed class AttributionTrackRequest
{
    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("chat_id")]
    public int? ChatId { get; set; }

    [JsonPropertyName("utm_source")]
    public string? UtmSource { get; set; }

    [JsonPropertyName("utm_medium")]
    public string? UtmMedium { get; set; }

    [JsonPropertyName("utm_campaign")]
    public string? UtmCampaign { get; set; }

    [JsonPropertyName("utm_content")]
    public string? UtmContent { get; set; }

    [JsonPropertyName("utm_term")]
    public string? UtmTerm { get; set; }

    [JsonPropertyName("meta_click_id")]
    public string? MetaClickId { get; set; }
}

/// <summary>
/// Update lead conversion status. PUT /api/v1/attribution/leads/{id}/status
/// </summary>
public sealed class LeadStatusUpdateRequest
{
    /// <summary>new, contacted, qualified, converted, lost</summary>
    [JsonPropertyName("conversion_status")]
    public string ConversionStatus { get; set; } = "";

    [JsonPropertyName("conversion_value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ConversionValue { get; set; }
}

/// <summary>
/// Manual ad cost entry. POST /api/v1/attribution/costs
/// </summary>
public sealed class AdCostCreateRequest
{
    /// <summary>meta, google, tiktok, linkedin, other</summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("campaign_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CampaignName { get; set; }

    [JsonPropertyName("cost_amount")]
    public decimal CostAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "TRY";

    /// <summary>yyyy-MM-dd</summary>
    [JsonPropertyName("period_start")]
    public string PeriodStart { get; set; } = "";

    /// <summary>yyyy-MM-dd</summary>
    [JsonPropertyName("period_end")]
    public string PeriodEnd { get; set; } = "";
}

/// <summary>
/// Ad cost response. GET /api/v1/attribution/costs
/// </summary>
public sealed class AdCostDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("campaign_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CampaignName { get; set; }

    [JsonPropertyName("cost_amount")]
    public decimal CostAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "TRY";

    [JsonPropertyName("period_start")]
    public string PeriodStart { get; set; } = "";

    [JsonPropertyName("period_end")]
    public string PeriodEnd { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Attribution summary for dashboard. GET /api/v1/attribution/summary
/// </summary>
public sealed class AttributionSummaryDto
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";

    [JsonPropertyName("total_leads")]
    public int TotalLeads { get; set; }

    [JsonPropertyName("converted_leads")]
    public int ConvertedLeads { get; set; }

    [JsonPropertyName("conversion_rate")]
    public double ConversionRate { get; set; }

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("by_source")]
    public List<SourceBreakdownItem> BySource { get; set; } = [];

    [JsonPropertyName("by_campaign")]
    public List<CampaignBreakdownItem> ByCampaign { get; set; } = [];
}

public sealed class SourceBreakdownItem
{
    [JsonPropertyName("lead_source")]
    public string LeadSource { get; set; } = "";

    [JsonPropertyName("lead_count")]
    public int LeadCount { get; set; }

    [JsonPropertyName("converted_count")]
    public int ConvertedCount { get; set; }

    [JsonPropertyName("conversion_rate")]
    public double ConversionRate { get; set; }

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }
}

public sealed class CampaignBreakdownItem
{
    [JsonPropertyName("utm_campaign")]
    public string UtmCampaign { get; set; } = "";

    [JsonPropertyName("lead_source")]
    public string LeadSource { get; set; } = "";

    [JsonPropertyName("lead_count")]
    public int LeadCount { get; set; }

    [JsonPropertyName("converted_count")]
    public int ConvertedCount { get; set; }

    [JsonPropertyName("conversion_rate")]
    public double ConversionRate { get; set; }

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }
}

/// <summary>
/// Cost-per-lead by platform. GET /api/v1/attribution/cost-per-lead
/// </summary>
public sealed class CostPerLeadDto
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("lead_count")]
    public int LeadCount { get; set; }

    [JsonPropertyName("cost_per_lead")]
    public decimal CostPerLead { get; set; }

    [JsonPropertyName("converted_count")]
    public int ConvertedCount { get; set; }

    [JsonPropertyName("cost_per_conversion")]
    public decimal CostPerConversion { get; set; }
}
