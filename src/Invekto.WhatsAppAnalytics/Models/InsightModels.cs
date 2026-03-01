namespace Invekto.WhatsAppAnalytics.Models;

// ── Request / Response DTOs ──

public sealed class InsightComputeRequest
{
    public int TenantId { get; set; }
    public string Database { get; set; } = "";
    public int? InstanceId { get; set; }
}

public sealed class ResponseTimeComputeResult
{
    public int TotalOutcomes { get; set; }
    public int TotalComputed { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<BucketSummary> Buckets { get; set; } = new();
    public long DurationMs { get; set; }
}

public sealed class BucketSummary
{
    public string Bucket { get; set; } = "";
    public string BucketLabel { get; set; } = "";
    public int Count { get; set; }
}

public sealed class ResponseTimeInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalConversations { get; set; }
    public long? AvgResponseTimeMs { get; set; }
    public List<ResponseTimeBucketCorrelation> Buckets { get; set; } = new();
}

public sealed class ResponseTimeBucketCorrelation
{
    public string Bucket { get; set; } = "";
    public string BucketLabel { get; set; } = "";
    public int ConversationCount { get; set; }
    public double Percentage { get; set; }
    public int SaleCount { get; set; }
    public double ConversionRate { get; set; }
}

// ── DB Record ──

public sealed class ResponseTimeRecord
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public string ConversationId { get; set; } = "";
    public int? InstanceId { get; set; }
    public DateTime? FirstCustomerMsgAt { get; set; }
    public DateTime? FirstAgentResponseAt { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string Bucket { get; set; } = "";
    public string? OutcomeLabel { get; set; }
    public DateTime ComputedAt { get; set; }
}

// ── Agent Leaderboard DTOs (RI-3.3) ──

public sealed class AgentLeaderboardComputeResult
{
    public int TotalOutcomes { get; set; }
    public int TotalAssigned { get; set; }
    public int UnassignedCount { get; set; }
    public int BotFilteredCount { get; set; }
    public int AgentCount { get; set; }
    public long DurationMs { get; set; }
}

public sealed class AgentLeaderboardInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalAgents { get; set; }
    public List<AgentLeaderboardEntry> Agents { get; set; } = new();
}

public sealed class AgentLeaderboardEntry
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public int? InstanceId { get; set; }
    public int TotalConversations { get; set; }
    public int SaleCount { get; set; }
    public int OfferedCount { get; set; }
    public int NoResponseCount { get; set; }
    public int OfferLostCount { get; set; }
    public int OtherCount { get; set; }
    public double ConversionRate { get; set; }
    public long? AvgResponseTimeMs { get; set; }
    public double GhostRate { get; set; }
    public double WeightedScore { get; set; }
}

// ── Agent Metric DB Record (RI-3.3) ──

public sealed class AgentMetricRecord
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public int TotalConversations { get; set; }
    public int SaleCount { get; set; }
    public int OfferedCount { get; set; }
    public int NoResponseCount { get; set; }
    public int OfferLostCount { get; set; }
    public int OtherCount { get; set; }
    public double ConversionRate { get; set; }
    public long? AvgResponseTimeMs { get; set; }
    public double GhostRate { get; set; }
    public double WeightedScore { get; set; }
}

// ── Agent Mapping from MSSQL (internal) ──

internal sealed class ConversationAgentMapping
{
    public string ConversationId { get; set; } = "";
    public int InstanceId { get; set; }
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
}

// ── Bucket Constants ──

public static class ResponseTimeBuckets
{
    public const string Instant = "0-5m";
    public const string Fast = "5-15m";
    public const string Moderate = "15-60m";
    public const string Slow = "1-4h";
    public const string VerySlow = "4h+";
    public const string NoResponse = "no_response";

    public static readonly Dictionary<string, string> Labels = new()
    {
        [Instant] = "Aninda (0-5dk)",
        [Fast] = "Hizli (5-15dk)",
        [Moderate] = "Normal (15-60dk)",
        [Slow] = "Geciken (1-4saat)",
        [VerySlow] = "Cok Gec (4saat+)",
        [NoResponse] = "Cevaplanmadi"
    };

    public static readonly string[] OrderedBuckets = [Instant, Fast, Moderate, Slow, VerySlow, NoResponse];

    public static string Classify(long? responseTimeMs)
    {
        if (responseTimeMs is null) return NoResponse;
        var minutes = responseTimeMs.Value / 60_000.0;
        return minutes switch
        {
            <= 5 => Instant,
            <= 15 => Fast,
            <= 60 => Moderate,
            <= 240 => Slow,
            _ => VerySlow
        };
    }

    public static string GetLabel(string bucket) =>
        Labels.TryGetValue(bucket, out var label) ? label : bucket;
}
