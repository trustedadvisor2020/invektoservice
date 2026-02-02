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

    public static RequestContext Create(string tenantId, string chatId)
    {
        return new RequestContext
        {
            RequestId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            ChatId = chatId
        };
    }
}
