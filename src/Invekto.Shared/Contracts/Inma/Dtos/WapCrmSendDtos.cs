using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

// ============================================================================
// FEAT-PROJELER / cxapi send engine — PR-2 (WapCrmSendClient)
// Typed request/result/options for plain-text sends to cxapi /api/chatoperation.
// PR-2 is DEV-ONLY: nothing in production routes through this client (PR-3 wires
// the sender route). No new error codes here — the client returns a typed
// outcome enum; ErrorCodes/INV-* mapping lands in PR-3.
// ============================================================================

/// <summary>
/// The terminal classification of a single <see cref="WapCrmSendClient"/> send.
/// The split mirrors the idempotency doctrine (Codex P0 #2): only a provably
/// not-sent rate-limit (301/302) is safe to delay-retry; a timeout is ambiguous
/// (the request may have reached the server) and must never be auto-retried.
/// </summary>
public enum WapCrmSendOutcome
{
    /// <summary>HTTP 2xx + envelope status=true. The provider accepted the message.</summary>
    Submitted,

    /// <summary>HTTP 2xx + envelope status!=true (a non-rate-limit provider statusCode). The provider rejected the message.</summary>
    ProviderFailed,

    /// <summary>Rate limited (HTTP 301/302, or envelope statusCode '301'/'302' while status!=true) and the bounded internal retries were exhausted. NOT a final failure — the caller may retry after <see cref="WapCrmSendResult.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The request timed out (linked-CTS) after bytes may have reached the server. Outcome is unknown — DO NOT auto-retry (duplicate-send risk).</summary>
    Ambiguous,

    /// <summary>A transport failure (connection error), an unexpected non-2xx/non-3xx HTTP status, or an unparseable body. NOT assumed to be retry-safe — it can also fail in the response phase; the caller (PR-3) decides conservatively.</summary>
    TransportError
}

/// <summary>
/// A single plain-text send request for cxapi POST /api/chatoperation.
/// Credentials are passed PER CALL (never stored on the client / never in
/// DefaultRequestHeaders) so one pooled HttpClient can serve many tenants
/// without cross-tenant leakage. <see cref="SecretKey"/> is never logged.
/// </summary>
public sealed class WapCrmSendRequest
{
    /// <summary>InvektoServis tenant id. Used only for keying/logging — NOT sent on the wire.</summary>
    public int TenantId { get; init; }

    /// <summary>cxapi <c>instanceID</c> (WapCRM channel). The caller (PR-3) must have verified this instance belongs to the tenant.</summary>
    public int InstanceId { get; init; }

    /// <summary>cxapi <c>userID</c> — from <see cref="WapCrmSettings.UserId"/>.</summary>
    public int UserId { get; init; }

    /// <summary>Per-request <c>X-CIB-SecretKey</c> (tenant API key). NEVER logged, NEVER on DefaultRequestHeaders.</summary>
    public required string SecretKey { get; init; }

    /// <summary>Destination phone in cxapi format (e.g. 905XXXXXXXXX).</summary>
    public required string ChatPhoneNumber { get; init; }

    /// <summary>Plain-text body. cxapi DMP/dynamic content is intentionally NOT supported on this route (approved-template params land in PR-4).</summary>
    public required string MessageText { get; init; }
}

/// <summary>
/// The typed outcome of a send. Field names mirror the PR-1 outbound_messages
/// columns (provider_status_code / provider_status / provider_request_id /
/// provider_error_message / attempt_count) so PR-3 can persist them directly.
/// </summary>
public sealed class WapCrmSendResult
{
    public required WapCrmSendOutcome Outcome { get; init; }

    /// <summary>True only when the provider accepted the message.</summary>
    public bool IsSubmitted => Outcome == WapCrmSendOutcome.Submitted;

    /// <summary>Raw HTTP status of the final attempt (0 when no response was received — transport error/timeout).</summary>
    public int HttpStatusCode { get; init; }

    /// <summary>Envelope <c>statusCode</c> (provider application code, e.g. 622/911/301).</summary>
    public string? ProviderStatusCode { get; init; }

    /// <summary>Envelope <c>status</c> boolean.</summary>
    public bool ProviderStatus { get; init; }

    /// <summary>Envelope <c>requestID</c> — provider correlation id (maps to provider_request_id; ext_id matching is gated on G12, PR-3).</summary>
    public string? ProviderRequestId { get; init; }

    /// <summary>Envelope <c>message</c> or a short transport diagnostic. NEVER contains the secret.</summary>
    public string? ProviderErrorMessage { get; init; }

    /// <summary>For <see cref="WapCrmSendOutcome.RateLimited"/>: the suggested cooldown (Retry-After header, delta or date), if the provider supplied one.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Number of HTTP attempts actually made (1 + rate-limit retries).</summary>
    public int AttemptCount { get; init; }

    /// <summary>Echoes the request tenant — so a PR-3 (tenant_id, instance_id)-keyed rate limiter can cool down the right scope.</summary>
    public int TenantId { get; init; }

    /// <summary>Echoes the request instance — see <see cref="TenantId"/>.</summary>
    public int InstanceId { get; init; }
}

/// <summary>
/// Tuning for <see cref="WapCrmSendClient"/>. Bound from the optional
/// "WapCrmSend" config section; safe defaults apply when absent. Carries NO
/// secrets (the secret lives only in tenant_registry.settings_json).
/// </summary>
public sealed class WapCrmSendOptions
{
    public const string SectionName = "WapCrmSend";

    /// <summary>cxapi base URL. The client posts to <c>api/chatoperation</c> relative to this.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net/";

    /// <summary>Per-attempt timeout (ms). Enforced by a linked CTS — the underlying HttpClient timeout is Infinite.</summary>
    public int TimeoutMs { get; set; } = 10_000;

    /// <summary>Max delayed retries on a 301/302 rate limit (total attempts = this + 1). Set 0 to delegate all backoff to a PR-3 limiter.</summary>
    public int MaxRateLimitRetries { get; set; } = 3;

    /// <summary>Base backoff (ms) used when the provider gives no Retry-After. Doubles per attempt up to <see cref="MaxBackoffMs"/>.</summary>
    public int BaseBackoffMs { get; set; } = 500;

    /// <summary>Ceiling for the computed backoff (ms).</summary>
    public int MaxBackoffMs { get; set; } = 8_000;

    /// <summary>Upper bound (ms) of the random jitter added to a computed backoff.</summary>
    public int MaxJitterMs { get; set; } = 250;
}
