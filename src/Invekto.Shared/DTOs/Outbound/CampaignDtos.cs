using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

/// <summary>
/// Campaign create/update request (GR-3.15).
/// </summary>
public sealed class CampaignCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = "manual"; // manual, scheduled, event_based, recurring

    [JsonPropertyName("target_criteria")]
    public Dictionary<string, object>? TargetCriteria { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("ab_template_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbTemplateId { get; set; }

    [JsonPropertyName("ab_split_pct")]
    public int AbSplitPct { get; set; } = 50;

    [JsonPropertyName("schedule")]
    public CampaignSchedule? Schedule { get; set; }
}

public sealed class CampaignSchedule
{
    [JsonPropertyName("start_at")]
    public DateTime? StartAt { get; set; }

    [JsonPropertyName("recurring_cron")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecurringCron { get; set; }

    [JsonPropertyName("end_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EndAt { get; set; }
}

/// <summary>
/// Campaign response.
/// </summary>
public sealed class CampaignResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = "";

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("ab_template_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbTemplateId { get; set; }

    [JsonPropertyName("ab_split_pct")]
    public int AbSplitPct { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("stats")]
    public CampaignStats? Stats { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class CampaignStats
{
    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("delivered")]
    public int Delivered { get; set; }

    [JsonPropertyName("read")]
    public int Read { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("converted")]
    public int Converted { get; set; }

    [JsonPropertyName("conversion_rate")]
    public double ConversionRate { get; set; }

    [JsonPropertyName("total_value")]
    public decimal TotalValue { get; set; }
}

/// <summary>
/// Conversion recording request.
/// </summary>
public sealed class ConversionRecordRequest
{
    [JsonPropertyName("message_id")]
    public long? MessageId { get; set; }

    [JsonPropertyName("campaign_id")]
    public int? CampaignId { get; set; }

    [JsonPropertyName("conversion_type")]
    public string ConversionType { get; set; } = ""; // reply, purchase, appointment, click, custom

    [JsonPropertyName("value_amount")]
    public decimal? ValueAmount { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Campaign ROI stats response (GR-3.15.6).
/// </summary>
public sealed class CampaignRoiResponse
{
    [JsonPropertyName("campaign_id")]
    public int CampaignId { get; set; }

    [JsonPropertyName("campaign_name")]
    public string CampaignName { get; set; } = "";

    [JsonPropertyName("total_sent")]
    public int TotalSent { get; set; }

    [JsonPropertyName("total_delivered")]
    public int TotalDelivered { get; set; }

    [JsonPropertyName("total_conversions")]
    public int TotalConversions { get; set; }

    [JsonPropertyName("conversion_rate")]
    public double ConversionRate { get; set; }

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("ab_winner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AbWinner { get; set; } // "A", "B", or null if not A/B
}
