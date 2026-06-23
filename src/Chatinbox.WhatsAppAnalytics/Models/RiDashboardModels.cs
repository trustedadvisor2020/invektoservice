namespace Chatinbox.WhatsAppAnalytics.Models;

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

// ── RI Faz 7: Onboarding Insight Response ──

public sealed class RiOnboardingResponse
{
    public int TenantId { get; set; }
    public string? Sector { get; set; }
    public string? SectorDisplayName { get; set; }

    /// <summary>Sector-specific onboarding checklist steps.</summary>
    public List<OnboardingChecklistItem> Checklist { get; set; } = new();

    /// <summary>High-impact quick start recommendations.</summary>
    public List<QuickStartItem> QuickStart { get; set; } = new();

    /// <summary>Sector overview stats (template counts, benchmarks).</summary>
    public SectorOverview? Overview { get; set; }

    /// <summary>Tenant vs sector benchmark comparison (null if no tenant data yet).</summary>
    public TenantBenchmarkComparison? Comparison { get; set; }
}

public sealed class OnboardingChecklistItem
{
    public int StepNumber { get; set; }
    public string Action { get; set; } = "";
    public string? Description { get; set; }
    public string? ExpectedImpact { get; set; }
    public string? DayRange { get; set; }
    public bool IsCompleted { get; set; }
}

public sealed class QuickStartItem
{
    public string Type { get; set; } = "";       // "flow", "intent", "faq"
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? ActionUrl { get; set; }
    public string? ImpactLabel { get; set; }      // e.g. "Conversion +15%"
}

public sealed class SectorOverview
{
    public int IntentCount { get; set; }
    public int FaqCount { get; set; }
    public int FlowCount { get; set; }
    public int ObjectionCount { get; set; }
    public int FollowupCount { get; set; }
    public int TotalTemplates { get; set; }
    public float? BenchmarkF1 { get; set; }
}

public sealed class TenantBenchmarkComparison
{
    public float? TenantAvgResponseMin { get; set; }
    public float? SectorAvgResponseMin { get; set; }
    public float? TenantConversionRate { get; set; }
    public float? SectorConversionRate { get; set; }
    public int? TenantActiveAgents { get; set; }
    public float? TenantAvgQualityScore { get; set; }
    public string? Recommendation { get; set; }
}
