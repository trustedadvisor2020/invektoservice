namespace Invekto.Contracts.DTOs.Responses;

/// <summary>
/// Response metadata for all API responses.
/// </summary>
public sealed class ResponseMetadata
{
    /// <summary>
    /// Unique request identifier.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Server processing time in milliseconds.
    /// </summary>
    public required long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Service version.
    /// </summary>
    public string? ServiceVersion { get; init; }

    /// <summary>
    /// True if serving from fallback/cache due to circuit breaker.
    /// </summary>
    public bool IsDegraded { get; init; }

    /// <summary>
    /// Reason for degraded mode.
    /// </summary>
    public string? DegradedReason { get; init; }

    /// <summary>
    /// Async job ID if request was queued.
    /// </summary>
    public string? JobId { get; init; }
}
