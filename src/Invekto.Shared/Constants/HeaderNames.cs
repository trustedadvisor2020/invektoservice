namespace Invekto.Shared.Constants;

/// <summary>
/// Standard header names for request correlation and auth
/// </summary>
public static class HeaderNames
{
    public const string RequestId = "X-Request-Id";
    public const string TenantId = "X-Tenant-Id";
    public const string ChatId = "X-Chat-Id";

    // GR-1.9: Integration headers
    public const string Authorization = "Authorization";
    public const string ProcessingTimeMs = "X-Processing-Time-Ms";
}
