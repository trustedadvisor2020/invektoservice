namespace Invekto.Shared.DTOs;

/// <summary>
/// Context passed with every request for correlation and logging
/// Stage-0: requestId ile korelasyon yapılır (trace yok)
/// </summary>
public sealed class RequestContext
{
    public required string RequestId { get; init; }
    public required string TenantId { get; init; }
    public required string ChatId { get; init; }

    /// <summary>
    /// Create context with auto-generated RequestId
    /// </summary>
    public static RequestContext Create(string tenantId, string chatId)
    {
        return new RequestContext
        {
            RequestId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            ChatId = chatId
        };
    }

    /// <summary>
    /// Create context with pass-through RequestId (if provided) or generate new
    /// Stage-0: Pass-through X-Request-Id from upstream if available
    /// </summary>
    public static RequestContext CreateWithPassThrough(
        string? incomingRequestId,
        string tenantId,
        string chatId)
    {
        return new RequestContext
        {
            RequestId = string.IsNullOrWhiteSpace(incomingRequestId)
                ? Guid.NewGuid().ToString("N")
                : incomingRequestId,
            TenantId = tenantId,
            ChatId = chatId
        };
    }
}
