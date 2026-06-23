using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Analytics;

/// <summary>
/// Tenant info with metrics availability (GET /api/ops/analytics/tenants)
/// </summary>
public sealed class TenantMetricsInfoDto
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool HasAutomationData { get; set; }
    public bool HasWaData { get; set; }
    public string? LatestMetricDate { get; set; }
}

/// <summary>
/// Automation summary for a tenant in date range (GET /api/ops/analytics/automation/summary)
/// </summary>
public sealed class AutomationSummaryDto
{
    public int TenantId { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public int TotalReplies { get; set; }
    public int DeflectedCount { get; set; }
    public int HandoffCount { get; set; }
    public double DeflectionRate { get; set; }
    public double HandoffRate { get; set; }
    public double AvgProcessingTimeMs { get; set; }
    public double AvgConfidence { get; set; }
    public Dictionary<string, int> ReplyTypeBreakdown { get; set; } = new();
    public Dictionary<string, int> SessionStatusBreakdown { get; set; } = new();
}

/// <summary>
/// Daily automation trend data point (GET /api/ops/analytics/automation/trends)
/// </summary>
public sealed class DailyMetricDto
{
    public string Date { get; set; } = string.Empty;
    public int TotalReplies { get; set; }
    public int DeflectedCount { get; set; }
    public int HandoffCount { get; set; }
    public double DeflectionRate { get; set; }
    public double AvgProcessingTimeMs { get; set; }
}

/// <summary>
/// Intent performance metric (GET /api/ops/analytics/automation/intents)
/// </summary>
public sealed class IntentMetricDto
{
    public string Intent { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int HandoffCount { get; set; }
    public double HandoffRate { get; set; }
    public double AvgConfidence { get; set; }
    public double AvgProcessingTimeMs { get; set; }
}

/// <summary>
/// WA analysis list item (GET /api/ops/analytics/wa/analyses)
/// </summary>
public sealed class WaAnalysisInfoDto
{
    public int AnalysisId { get; set; }
    public string? SourceFileName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int TotalConversations { get; set; }
    public string? CompletedAt { get; set; }
}

/// <summary>
/// WA analysis summary (GET /api/ops/analytics/wa/summary)
/// </summary>
public sealed class WaSummaryDto
{
    public int AnalysisId { get; set; }
    public int TotalMessages { get; set; }
    public int TotalConversations { get; set; }
    public Dictionary<string, int> OutcomeBreakdown { get; set; } = new();
    public double AvgFirstResponseMinutes { get; set; }
    public double AvgDurationMinutes { get; set; }
}

/// <summary>
/// WA agent performance (GET /api/ops/analytics/wa/agents)
/// </summary>
public sealed class WaAgentMetricDto
{
    public string AgentName { get; set; } = string.Empty;
    public int TotalConversations { get; set; }
    public int SaleCount { get; set; }
    public int OfferedCount { get; set; }
    public int NoSaleCount { get; set; }
    public double ConversionRate { get; set; }
    public double AvgFirstResponseMinutes { get; set; }
}

/// <summary>
/// WA daily trend data point (GET /api/ops/analytics/wa/trends)
/// </summary>
public sealed class WaTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int ConversationCount { get; set; }
    public int SaleCount { get; set; }
    public int OfferedCount { get; set; }
}

/// <summary>
/// GR-3.18: Campaign stats from outbound_campaigns. GET /api/ops/analytics/campaigns
/// </summary>
public sealed class CampaignStatDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("stats_json")]
    public string StatsJson { get; set; } = "{}";

    [JsonPropertyName("template_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TemplateName { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}
