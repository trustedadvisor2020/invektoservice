namespace Chatinbox.WhatsAppAnalytics.Models;

/// <summary>
/// wa_batch_jobs row — tracks batch classification jobs.
/// </summary>
public sealed class BatchJob
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string DatabaseName { get; set; } = "";
    public int? InstanceId { get; set; }
    public string? Sector { get; set; }
    public string JobType { get; set; } = "manual";
    public string Status { get; set; } = "pending";
    public int LookbackDays { get; set; } = 7;
    public int? TotalCandidates { get; set; }
    public int AlreadyClassified { get; set; }
    public int ClassifiedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? StageProgress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// wa_conversation_outcomes row.
/// </summary>
public sealed class ConversationOutcome
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public string DatabaseName { get; set; } = "";
    public int? InstanceId { get; set; }
    public string ConversationId { get; set; } = "";
    public string? Sector { get; set; }
    public string OutcomeLabel { get; set; } = "";
    public float Confidence { get; set; }
    public bool HasOffer { get; set; }
    public string? Evidence { get; set; }
    public string ModelVersion { get; set; } = "tiered-v0.6";
    public DateTime ClassifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// API request DTO for starting a batch classification.
/// </summary>
public sealed class BatchStartRequest
{
    public int TenantId { get; set; }
    public string Database { get; set; } = "";
    public int? InstanceId { get; set; }
    public string? Sector { get; set; }
    public int LookbackDays { get; set; } = 7;
    public int? MaxThreads { get; set; }
}

/// <summary>
/// Queued batch job for background processing.
/// </summary>
public sealed class BatchProcessJob
{
    public int BatchJobId { get; set; }
    public int TenantId { get; set; }
    public string DatabaseName { get; set; } = "";
    public int? InstanceId { get; set; }
    public string? Sector { get; set; }
    public int LookbackDays { get; set; } = 7;
    public int? MaxThreads { get; set; }
}
