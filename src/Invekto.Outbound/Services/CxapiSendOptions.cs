namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-PROJELER / cxapi send engine — PR-3a gating config.
/// Bound from appsettings "CxapiSend". Production-safe default: DISABLED.
///
/// When OFF or a tenant is not allowlisted, NO row is ever stamped
/// send_route='wapcrm_cxapi' (BroadcastOrchestrator), so the entire bridge path is a
/// byte-for-byte no-op. The flag enables a gradual, reversible cutover (BulkSend precedent).
///
/// LIVE-ENABLEMENT GATE (Codex plan-review P0-3): do NOT allowlist a tenant for live
/// cxapi sends until PR-3b (tenant-scoped delivery-status lookup) lands OR cxapi/WapCRM
/// delivery callbacks are proven NOT routed to /api/v1/webhook/delivery-status.
/// </summary>
public sealed class CxapiSendOptions
{
    public const string SectionName = "CxapiSend";

    /// <summary>Master kill-switch. Default false = no cxapi routing, pure bridge no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>Pilot tenant allowlist. Empty = no tenant routes to cxapi.</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>
    /// Max times a RateLimited (301/302, provably not sent) message is requeued before it
    /// becomes 'failed'. attempt_count is compared against this. Only the rate-limit path is
    /// ever retried — timeout/transport are 'ambiguous' and never auto-retried.
    /// </summary>
    public int MaxSendAttempts { get; set; } = 5;

    /// <summary>Cooldown applied to the (tenant, instance) limiter on a RateLimited result when the provider gives no Retry-After (ms).</summary>
    public int DefaultCooldownMs { get; set; } = 30_000;

    /// <summary>
    /// A row stuck in 'posting' (worker crashed mid-POST) older than this many minutes is swept to
    /// 'ambiguous' on startup. Must be well above the per-attempt POST timeout (WapCrmSendOptions.TimeoutMs)
    /// so a genuinely in-flight POST is never swept.
    /// </summary>
    public int StalePostingMinutes { get; set; } = 5;

    public bool IsTenantAllowed(int tenantId) => Enabled && AllowedTenantIds.Contains(tenantId);
}
