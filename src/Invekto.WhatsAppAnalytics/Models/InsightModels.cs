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

// ── Rescue Candidate DTOs (RI-3.6) ──

public sealed class RescueComputeResult
{
    public int TotalOutcomes { get; set; }
    public int EligibleOutcomes { get; set; }
    public int RescueCandidates { get; set; }
    public int Excluded30Days { get; set; }
    public int Errors { get; set; }
    public long DurationMs { get; set; }
}

public sealed class RescueInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalCandidates { get; set; }
    public List<RescueCandidateEntry> Candidates { get; set; } = new();
}

public sealed class RescueCandidateEntry
{
    public string ConversationId { get; set; } = "";
    public int? InstanceId { get; set; }
    public string OutcomeLabel { get; set; } = "";
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessageFrom { get; set; }
    public int DaysSince { get; set; }
    public double RescuePriorityScore { get; set; }
    public string RescueStatus { get; set; } = "pending";
}

// ── Rescue Candidate DB Record (RI-3.6) ──

public sealed class RescueCandidateRecord
{
    public int TenantId { get; set; }
    public string ConversationId { get; set; } = "";
    public int? InstanceId { get; set; }
    public string OutcomeLabel { get; set; } = "";
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessageFrom { get; set; }
    public int DaysSince { get; set; }
    public double RescuePriorityScore { get; set; }
}

// ── Last Message from MSSQL (internal, RI-3.6) ──

internal sealed class ConversationLastMessage
{
    public string ConversationId { get; set; } = "";
    public int? InstanceId { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool LastMessageFromCustomer { get; set; }
}

// ── Rescue Priority Scoring (RI-3.6) ──

public static class InsightRescueScoring
{
    // Outcome weights: offered closest to sale, no_response lowest
    private static readonly Dictionary<string, double> OutcomeWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["offered"] = 100.0,
        ["offer_lost"] = 70.0,
        ["no_response"] = 40.0
    };

    private const int MaxDays = 30;

    /// <summary>
    /// rescue_priority_score = recency(50%) + outcome_weight(50%).
    /// Recency: 0 days = 100, 30 days = 0, linear interpolation.
    /// Outcome weight: offered=100, offer_lost=70, no_response=40.
    /// </summary>
    public static double CalculatePriorityScore(int daysSince, string outcomeLabel)
    {
        // Recency: 0d=100, 30d=0
        double recencyScore;
        if (daysSince <= 0)
            recencyScore = 100.0;
        else if (daysSince >= MaxDays)
            recencyScore = 0.0;
        else
            recencyScore = 100.0 * (MaxDays - daysSince) / MaxDays;

        // Outcome weight
        var outcomeScore = OutcomeWeights.TryGetValue(outcomeLabel, out var w) ? w : 40.0;

        var score = (recencyScore * 0.50) + (outcomeScore * 0.50);
        return Math.Round(score, 1);
    }

    /// <summary>
    /// Rescue-eligible outcome labels.
    /// </summary>
    public static readonly HashSet<string> EligibleOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "no_response", "offered", "offer_lost"
    };
}

// ── Demand Heatmap DTOs (RI-3.2) ──

public sealed class DemandHeatmapComputeResult
{
    public int TotalOutcomes { get; set; }
    public int TotalMessages { get; set; }
    public int CellsWritten { get; set; }
    public int Errors { get; set; }
    public long DurationMs { get; set; }
}

public sealed class DemandHeatmapInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalConversations { get; set; }
    public List<DemandHeatmapCell> Cells { get; set; } = new();
}

public sealed class DemandHeatmapCell
{
    public int DayOfWeek { get; set; }
    public string DayLabel { get; set; } = "";
    public int HourOfDay { get; set; }
    public int TotalConversations { get; set; }
    public int SaleCount { get; set; }
    public double ConversionRate { get; set; }
    public long? AvgResponseTimeMs { get; set; }
}

// ── Demand Heatmap DB Record (RI-3.2) ──

public sealed class DemandHeatmapRecord
{
    public int TenantId { get; set; }
    public int InstanceId { get; set; }  // 0 = unknown/aggregate, NOT NULL (PG UNIQUE constraint safety)
    public int DayOfWeek { get; set; }
    public int HourOfDay { get; set; }
    public int TotalConversations { get; set; }
    public int SaleCount { get; set; }
    public double ConversionRate { get; set; }
    public long? AvgResponseTimeMs { get; set; }
}

// ── Demand Heatmap MSSQL row (internal, RI-3.2) ──

internal sealed class ConversationTimestamp
{
    public string ConversationId { get; set; } = "";
    public int? InstanceId { get; set; }
    public DateTime FirstMessageAt { get; set; }
}

// ── Day-of-week labels (RI-3.2) ──

public static class DemandDayLabels
{
    private static readonly string[] Labels =
        ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];

    /// <summary>
    /// Get Turkish day label for DayOfWeek (0=Monday, 6=Sunday).
    /// </summary>
    public static string GetLabel(int dayOfWeek) =>
        dayOfWeek >= 0 && dayOfWeek < Labels.Length ? Labels[dayOfWeek] : $"Day{dayOfWeek}";
}

// ── Revenue Attribution DTOs (RI-3.4) ──

public sealed class RevenueComputeResult
{
    public int TotalOutcomes { get; set; }
    public int TotalRecords { get; set; }
    public decimal TotalRevenue { get; set; }
    public long DurationMs { get; set; }
}

public sealed class RevenueAttributionInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalConversations { get; set; }
    public List<RevenueAttributionEntry> Entries { get; set; } = new();
}

public sealed class RevenueAttributionEntry
{
    public string Dimension { get; set; } = "";
    public string DimensionKey { get; set; } = "";
    public string? DimensionLabel { get; set; }
    public int TotalConversations { get; set; }
    public decimal AttributedRevenue { get; set; }
    public decimal AvgRevenue { get; set; }
    public object? Breakdown { get; set; }
}

// ── Revenue Attribution DB Record (RI-3.4) ──

public sealed class RevenueAttributionRecord
{
    public int TenantId { get; set; }
    public int InstanceId { get; set; }  // NOT NULL DEFAULT 0
    public string Dimension { get; set; } = "";
    public string DimensionKey { get; set; } = "";
    public string? DimensionLabel { get; set; }
    public int TotalConversations { get; set; }
    public decimal AttributedRevenue { get; set; }
    public decimal AvgRevenue { get; set; }
    public string? BreakdownJson { get; set; }
}

// ── Revenue PG query helper models (RI-3.4) ──

public sealed class OutcomeCountRow
{
    public string OutcomeLabel { get; set; } = "";
    public int InstanceId { get; set; }
    public int Count { get; set; }
}

public sealed class HourlyOutcomeRow
{
    public int Hour { get; set; }
    public string OutcomeLabel { get; set; } = "";
    public int Count { get; set; }
}

// ── Objection Map DTOs (RI-3.5) ──

public sealed class ObjectionMapComputeResult
{
    public int TotalOfferLost { get; set; }
    public int TotalObjections { get; set; }
    public int NoEvidence { get; set; }
    public long DurationMs { get; set; }
}

public sealed class ObjectionMapInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalObjections { get; set; }
    public List<ObjectionTypeEntry> ObjectionTypes { get; set; } = new();
}

public sealed class ObjectionTypeEntry
{
    public string ObjectionType { get; set; } = "";
    public string ObjectionLabel { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public sealed class ObjectionMapRecord
{
    public int TenantId { get; set; }
    public int InstanceId { get; set; }
    public string ConversationId { get; set; } = "";
    public string ObjectionType { get; set; } = "";
    public string? Detail { get; set; }
    public string? OutcomeLabel { get; set; }
}

// ── Objection Type Classification (RI-3.5) ──

public static class ObjectionTypes
{
    public const string PriceHigh = "price_high";
    public const string ChoseCompetitor = "chose_competitor";
    public const string NotReady = "not_ready";
    public const string WrongService = "wrong_service";
    public const string NoTrust = "no_trust";
    public const string Timing = "timing";
    public const string MedicalConcern = "medical_concern";
    public const string OutOfStock = "out_of_stock";
    public const string Location = "location";
    public const string Unknown = "unknown";

    public static readonly Dictionary<string, string> Labels = new()
    {
        [PriceHigh] = "Fiyat Yuksek",
        [ChoseCompetitor] = "Rakip Tercih",
        [NotReady] = "Hazir Degil",
        [WrongService] = "Yanlis Hizmet",
        [NoTrust] = "Guven Eksikligi",
        [Timing] = "Zamanlama",
        [MedicalConcern] = "Tibbi Endise",
        [OutOfStock] = "Stok Yok",
        [Location] = "Konum Uygunsuz",
        [Unknown] = "Bilinmeyen"
    };

    /// <summary>
    /// Keyword-based objection classification from evidence text.
    /// Processes Turkish and English keywords.
    /// </summary>
    private static readonly (string Type, string[] Keywords)[] KeywordMap =
    [
        (PriceHigh, ["fiyat", "pahal", "ucuz", "bütçe", "butce", "expensive", "cost", "price", "ücret", "ucret", "indirim", "taksit"]),
        (ChoseCompetitor, ["rakip", "başka", "baska", "competitor", "alternatif", "another", "diğer", "diger", "farklı klinik", "farkli klinik"]),
        (NotReady, ["düşün", "dusun", "karar", "henüz", "henuz", "think", "decide", "not ready", "emin değil", "emin degil", "ilerde"]),
        (Timing, ["zaman", "tarih", "müsait", "musait", "timing", "schedule", "uygun değil", "uygun degil", "sonra", "ertel"]),
        (MedicalConcern, ["tıbbi", "tibbi", "medical", "risk", "doktor", "sağlık", "saglik", "uygun değil", "ameliyat", "komplikasyon"]),
        (OutOfStock, ["stok", "tüken", "tuken", "kalmadı", "kalmadi", "stock", "mevcut değil", "mevcut degil", "beden"]),
        (Location, ["uzak", "konum", "lokasyon", "location", "distance", "şehir", "sehir", "yurt dışı", "yurt disi"]),
        (NoTrust, ["güven", "guven", "emin", "trust", "referans", "yorum", "şüphe", "suphe"]),
        (WrongService, ["yanlış", "yanlis", "farklı", "farkli", "ihtiyaç", "ihtiyac", "wrong", "different", "istemediğ", "istemedig"])
    ];

    public static string ClassifyFromEvidence(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return Unknown;

        var lower = evidence.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        foreach (var (type, keywords) in KeywordMap)
        {
            foreach (var keyword in keywords)
            {
                if (lower.Contains(keyword))
                    return type;
            }
        }

        return Unknown;
    }

    public static string GetLabel(string objectionType) =>
        Labels.TryGetValue(objectionType, out var label) ? label : objectionType;
}

// ── Quality Score DTOs (RI-3.7) ──

public sealed class QualityComputeResult
{
    public int TotalOutcomes { get; set; }
    public int TotalScored { get; set; }
    public int NoResponseTime { get; set; }
    public double AvgOverallScore { get; set; }
    public long DurationMs { get; set; }
}

public sealed class QualityInsight
{
    public int TenantId { get; set; }
    public int? InstanceId { get; set; }
    public int TotalScored { get; set; }
    public double AvgOverallScore { get; set; }
    public List<QualityEntry> Scores { get; set; } = new();
}

public sealed class QualityEntry
{
    public string ConversationId { get; set; } = "";
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
    public double ResponseSpeedScore { get; set; }
    public double EngagementScore { get; set; }
    public double ResolutionScore { get; set; }
    public double SentimentScore { get; set; }
    public double OverallScore { get; set; }
}

public sealed class QualityScoreRecord
{
    public int TenantId { get; set; }
    public int InstanceId { get; set; }
    public string ConversationId { get; set; } = "";
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
    public double ResponseSpeedScore { get; set; }
    public double EngagementScore { get; set; }
    public double ResolutionScore { get; set; }
    public double SentimentScore { get; set; }
    public double OverallScore { get; set; }
}

// ── Quality Score Constants (RI-3.7) ──

public static class QualityScoring
{
    /// <summary>
    /// Map response time bucket to speed score (0-100).
    /// Faster response = higher score.
    /// </summary>
    public static double BucketToSpeedScore(string bucket) => bucket switch
    {
        ResponseTimeBuckets.Instant => 100.0,
        ResponseTimeBuckets.Fast => 80.0,
        ResponseTimeBuckets.Moderate => 60.0,
        ResponseTimeBuckets.Slow => 30.0,
        ResponseTimeBuckets.VerySlow => 10.0,
        ResponseTimeBuckets.NoResponse => 0.0,
        _ => 0.0
    };

    /// <summary>
    /// Map outcome label to resolution score (0-100).
    /// Successful outcomes score higher.
    /// </summary>
    public static double OutcomeToResolutionScore(string outcomeLabel) => outcomeLabel.ToLowerInvariant() switch
    {
        "sale" => 100.0,
        "appointment_booked" => 90.0,
        "offered" => 60.0,
        "return_or_complaint" => 30.0,
        "offer_lost" => 20.0,
        "no_response" => 10.0,
        "abandoned" => 0.0,
        _ => 0.0
    };

    /// <summary>
    /// Map sentiment score (-1.0 to 1.0) to 0-100 scale.
    /// </summary>
    public static double SentimentToScore(double sentimentScore) =>
        Math.Clamp((sentimentScore + 1.0) * 50.0, 0.0, 100.0);

    /// <summary>
    /// Compute overall quality score.
    /// Weights: speed(25%) + engagement(25%) + resolution(30%) + sentiment(20%)
    /// When engagement is 0 (no MSSQL data), redistribute: speed(35%) + resolution(40%) + sentiment(25%)
    /// </summary>
    public static double ComputeOverall(double speed, double engagement, double resolution, double sentiment)
    {
        if (engagement <= 0)
        {
            // No engagement data: redistribute weights
            return Math.Round((speed * 0.35) + (resolution * 0.40) + (sentiment * 0.25), 1);
        }

        return Math.Round(
            (speed * 0.25) + (engagement * 0.25) + (resolution * 0.30) + (sentiment * 0.20), 1);
    }
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
