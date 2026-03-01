namespace Invekto.WhatsAppAnalytics.Models;

// ── RI Faz 6: Dashboard aggregate response ──

public sealed class RiDashboardResponse
{
    public int TenantId { get; set; }
    public string? Sector { get; set; }
    public ResponseTimeInsight? ResponseTime { get; set; }
    public AgentLeaderboardInsight? AgentLeaderboard { get; set; }
    public ObjectionMapInsight? ObjectionMap { get; set; }
    public RescueInsight? RescueAlerts { get; set; }
    public QualityInsight? QualityScore { get; set; }
    public DemandHeatmapInsight? DemandHeatmap { get; set; }
    public RevenueAttributionInsight? Revenue { get; set; }
    public SectorTemplatesResponse? Templates { get; set; }
    public SectorBenchmarks? Benchmarks { get; set; }
}

// ── Sector Benchmarks (RI-6.28) ──

public sealed class SectorBenchmarks
{
    public string Sector { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float? BenchmarkF1 { get; set; }
    public int TotalTemplates { get; set; }
    public int IntentCount { get; set; }
    public int FaqCount { get; set; }
    public int FlowCount { get; set; }
}

// ── Feedback (RI-6.14~6.16) ──

public sealed class FeedbackRequest
{
    public string ConversationId { get; set; } = "";
    public string OriginalLabel { get; set; } = "";
    public bool IsAgree { get; set; }
    public string? CorrectedLabel { get; set; }
}

public sealed class FeedbackRecord
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public string ConversationId { get; set; } = "";
    public string OriginalLabel { get; set; } = "";
    public bool IsAgree { get; set; }
    public string? CorrectedLabel { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class FeedbackSummary
{
    public int TotalFeedback { get; set; }
    public int AgreeCount { get; set; }
    public int DisagreeCount { get; set; }
    public float AgreeRate { get; set; }
    public List<LabelFeedbackBreakdown> PerLabel { get; set; } = new();
}

public sealed class LabelFeedbackBreakdown
{
    public string Label { get; set; } = "";
    public int Total { get; set; }
    public int Agree { get; set; }
    public int Disagree { get; set; }
    public float AgreeRate { get; set; }
}
