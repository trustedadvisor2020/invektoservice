namespace Invekto.Contracts.DTOs.Responses;

/// <summary>
/// Error details for failed responses.
/// </summary>
public sealed class ErrorInfo
{
    /// <summary>
    /// Error code in SVC-CAT-NNN format.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// User-friendly error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Technical details for debugging (null in production).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public required string TraceId { get; init; }
}
