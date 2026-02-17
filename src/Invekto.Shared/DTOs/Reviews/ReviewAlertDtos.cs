namespace Invekto.Shared.DTOs.Reviews;

/// <summary>PKT-6B1 GR-3.16: Review alert webhook payload (external trigger).</summary>
public sealed class ReviewAlertWebhookRequest
{
    public string Provider { get; set; } = "";
    public string? ExternalReviewId { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public string? CustomerPhone { get; set; }
    public string? OrderId { get; set; }
}

/// <summary>Review alert record for API responses.</summary>
public sealed class ReviewAlertResponse
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Provider { get; set; } = "";
    public string? ExternalReviewId { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public string? CustomerPhone { get; set; }
    public string? OrderId { get; set; }
    public string RecoveryStatus { get; set; } = "pending";
    public int RecoveryAttempt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? CustomerResponse { get; set; }
    public bool ReviewUpdated { get; set; }
    public int? NewRating { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Review recovery stats for dashboard.</summary>
public sealed class ReviewRecoveryStatsResponse
{
    public int TotalAlerts { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByProvider { get; set; } = new();
}
