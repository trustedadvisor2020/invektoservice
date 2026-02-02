namespace Invekto.Shared.DTOs;

/// <summary>
/// Standard error response following arch/errors.md
/// User-friendly, actionable, localized context
/// </summary>
public sealed class ErrorResponse
{
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; } // Only for ops/debugging
    public required string RequestId { get; init; }
    public required DateTime Timestamp { get; init; }

    public static ErrorResponse Create(string errorCode, string message, string requestId, string? details = null) => new()
    {
        ErrorCode = errorCode,
        Message = message,
        Details = details,
        RequestId = requestId,
        Timestamp = DateTime.UtcNow
    };
}
