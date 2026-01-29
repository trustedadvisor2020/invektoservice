namespace Invekto.Contracts.Constants;

/// <summary>
/// Standard HTTP header names for Invekto services.
/// </summary>
public static class HeaderNames
{
    #region Identity

    /// <summary>Tenant identifier</summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>Chat identifier</summary>
    public const string ChatId = "X-Chat-Id";

    /// <summary>User identifier</summary>
    public const string UserId = "X-User-Id";

    #endregion

    #region Tracing

    /// <summary>Unique request identifier</summary>
    public const string RequestId = "X-Request-Id";

    /// <summary>Distributed trace identifier</summary>
    public const string TraceId = "X-Trace-Id";

    /// <summary>Current span identifier</summary>
    public const string SpanId = "X-Span-Id";

    /// <summary>Parent span identifier</summary>
    public const string ParentSpanId = "X-Parent-Span-Id";

    #endregion

    #region Idempotency

    /// <summary>Idempotency key for deduplication</summary>
    public const string IdempotencyKey = "X-Idempotency-Key";

    #endregion

    #region Metadata

    /// <summary>API schema version</summary>
    public const string SchemaVersion = "X-Schema-Version";

    /// <summary>Client version</summary>
    public const string ClientVersion = "X-Client-Version";

    #endregion
}
