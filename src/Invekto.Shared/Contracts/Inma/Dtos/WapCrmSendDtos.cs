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
/// A single approved-template (HSM) send request for cxapi POST /api/chatoperation — PR-4.
/// The wire body carries a <c>template</c> object (templateId slug + named parameters +
/// optional headerMedia) INSTEAD of messageType/messageText. There is NO <c>language</c>
/// field on the wire: per INMA (2026-06-08) every language is a separate template slug,
/// so language selection happens at template pick time, never at send time.
/// Credential handling is identical to <see cref="WapCrmSendRequest"/> (per-call secret,
/// never logged, never on DefaultRequestHeaders).
/// </summary>
public sealed class WapCrmTemplateSendRequest
{
    /// <summary>InvektoServis tenant id. Used only for keying/logging — NOT sent on the wire.</summary>
    public int TenantId { get; init; }

    /// <summary>cxapi <c>instanceID</c> (WapCRM channel). The caller must have verified this instance belongs to the tenant.</summary>
    public int InstanceId { get; init; }

    /// <summary>cxapi <c>userID</c> — from <see cref="WapCrmSettings.UserId"/>.</summary>
    public int UserId { get; init; }

    /// <summary>Per-request <c>X-CIB-SecretKey</c> (tenant API key). NEVER logged, NEVER on DefaultRequestHeaders.</summary>
    public required string SecretKey { get; init; }

    /// <summary>Destination phone in cxapi format (e.g. 905XXXXXXXXX).</summary>
    public required string ChatPhoneNumber { get; init; }

    /// <summary>Approved-template slug (<c>template.templateId</c>) from cxapi POST /api/templates. Language is embedded in the slug.</summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Named parameter values for the template's required text inputs
    /// (<c>template.parameters[{paramKey,value}]</c>). Empty/null when the template
    /// has no required text inputs — the wire object then omits <c>parameters</c>.
    /// </summary>
    public IReadOnlyList<WapCrmTemplateParamValue>? Parameters { get; init; }

    /// <summary>Dynamic header media (<c>template.headerMedia</c>) — only for templates whose requiredInputs demand media. Null = omitted on the wire (fixed-media headers are added provider-side).</summary>
    public WapCrmTemplateHeaderMedia? HeaderMedia { get; init; }
}

/// <summary>A single named template parameter value ({paramKey, value}).</summary>
public sealed class WapCrmTemplateParamValue
{
    public required string ParamKey { get; init; }
    public required string Value { get; init; }
}

/// <summary>
/// Dynamic header media for an approved-template send. <see cref="Url"/> must be a public,
/// auth-less HTTPS URL. <see cref="FileName"/> is optional — when absent the client derives
/// a sanitized name from the URL's last path segment (fallback "media").
/// </summary>
public sealed class WapCrmTemplateHeaderMedia
{
    public required string Url { get; init; }
    public string? FileName { get; init; }
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

    /// <summary>Envelope <c>requestID</c> — INMA-internal request/log GUID. Audit only; it is NOT the correlation key (INMA C1, 2026-06-08).</summary>
    public string? ProviderRequestId { get; init; }

    /// <summary>
    /// Envelope <c>data</c> on a Submitted send — the sent message's WhatsApp <c>wamid</c> (PR-3b-1).
    /// Per INMA C1 (2026-06-08) this equals the later ack's <c>InstanceMessageID</c>, so PR-3 persists it
    /// as <c>external_message_id</c> for tenant-scoped delivery-status correlation. Null when the provider
    /// returned no string <c>data</c> (or for any non-Submitted outcome).
    /// </summary>
    public string? ProviderMessageId { get; init; }

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
/// FEAT-PROJELER FEATURE B (status-pull): the typed outcome of a single cxapi
/// <c>POST /api/message-status</c> pull. <see cref="MessageStatus"/> is the raw provider
/// status int (0..5; same semantics as the delivery-ack <c>Status</c> — map via
/// <c>WapCrmAckMapping.MapStatus</c>). It is null when the provider returned no parseable
/// status (best-effort: the caller no-ops it). NO secret is ever carried here.
/// </summary>
public sealed class WapCrmMessageStatusResult
{
    /// <summary>True only when the provider envelope was accepted (status==true) AND a numeric messageStatus was parsed.</summary>
    public bool Ok { get; init; }

    /// <summary>Raw provider message status int (0=Pending,1=Sent,2=Delivered,3=Viewed/read,4=NotSent/failed,5=Deleted); null when absent/unparseable.</summary>
    public int? MessageStatus { get; init; }

    /// <summary>Short diagnostic for a non-Ok outcome (transport/timeout/provider-fail/unparseable). NEVER contains the secret.</summary>
    public string? Error { get; init; }
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

    /// <summary>Per-attempt timeout (ms). PRIMARY control: a per-attempt linked CTS in SendCoreAsync. The
    /// HttpClient.Timeout is set to a FINITE hard backstop (<see cref="HttpClientBackstopMs"/>) — never Infinite —
    /// so a stuck socket that the linked CTS fails to cancel can never freeze the single-threaded sender
    /// (incident 2026-06-12: one hung POST wedged the worker 4h; HttpClient.Timeout was Infinite).</summary>
    public int TimeoutMs { get; set; } = 10_000;

    /// <summary>Buffer (ms) added to <see cref="TimeoutMs"/> to derive the FINITE HttpClient.Timeout backstop.
    /// Clamped to a 1s floor so the backstop is ALWAYS &gt; the per-attempt CTS (which therefore fires first).</summary>
    public int HttpClientBackstopBufferMs { get; set; } = 5_000;

    /// <summary>FINITE HttpClient.Timeout hard backstop (ms) = TimeoutMs + buffer (buffer floored at 1s). Used at
    /// DI registration instead of Timeout.InfiniteTimeSpan so no SendAsync can hang past this ceiling.</summary>
    public int HttpClientBackstopMs => TimeoutMs + Math.Max(HttpClientBackstopBufferMs, 1_000);

    /// <summary>Max delayed retries on a 301/302 rate limit (total attempts = this + 1). Set 0 to delegate all backoff to a PR-3 limiter.</summary>
    public int MaxRateLimitRetries { get; set; } = 3;

    /// <summary>Base backoff (ms) used when the provider gives no Retry-After. Doubles per attempt up to <see cref="MaxBackoffMs"/>.</summary>
    public int BaseBackoffMs { get; set; } = 500;

    /// <summary>Ceiling for the computed backoff (ms).</summary>
    public int MaxBackoffMs { get; set; } = 8_000;

    /// <summary>Upper bound (ms) of the random jitter added to a computed backoff.</summary>
    public int MaxJitterMs { get; set; } = 250;
}
