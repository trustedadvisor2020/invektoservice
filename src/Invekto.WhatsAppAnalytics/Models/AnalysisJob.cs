namespace Invekto.WhatsAppAnalytics.Models;

/// <summary>
/// Represents an analysis job (wa_analyses row).
/// Tracks pipeline progress from upload to completion.
/// </summary>
public sealed class AnalysisJob
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Status { get; set; } = "pending"; // pending, cleaning, threading, stats, intents, faq, sentiment, products, completed, error
    public string SourceFileName { get; set; } = "";
    public string? ConfigJson { get; set; } // {delimiter, encoding, tenant_name, sector}
    public int? TotalMessages { get; set; }
    public int? TotalConversations { get; set; }
    public string? StageProgress { get; set; } // JSON: {stage, percent, message}
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Progress info for the current pipeline stage.
/// </summary>
public sealed class StageProgress
{
    public string Stage { get; set; } = "";
    public int Percent { get; set; }
    public string Message { get; set; } = "";
    public int StageNumber { get; set; }
    public int TotalStages { get; set; } = 3; // Phase A: 3 stages

    public string ToJson() =>
        System.Text.Json.JsonSerializer.Serialize(this);
}

/// <summary>
/// Queued job for background processing.
/// </summary>
public sealed class AnalysisProcessJob
{
    public int AnalysisId { get; set; }
    public int TenantId { get; set; }
    public string FilePath { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public char Delimiter { get; set; } = ';';
}
