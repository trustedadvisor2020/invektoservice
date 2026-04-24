namespace Invekto.Automation.Services.Lifecycle;

/// <summary>
/// Paket B-META: Automation → Backend internal HTTP hop for lifecycle events.
/// The concrete caller today is <c>TriggerWelcomeFlowJob</c> which fires
/// <see cref="SendWelcomeSentAsync"/> after the welcome flow emitted ≥1
/// outbound message.
///
/// Contract: fire-and-forget semantics. Failures are logged with INV-AT-072
/// by the implementation and surfaced to the job only as "don't retry,
/// don't throw" — the welcome message already reached the user, so the
/// Zoho "1. Mesaj Atildi" transition miss is tolerable (next inbound
/// engagement triggers its own lifecycle events).
///
/// Interface kept tight (one method) to preserve unit-test surface for
/// <c>TriggerWelcomeFlowJobTests</c> without paying for unused methods.
/// Future lifecycle hops (engaged / qualified / …) land as sibling methods
/// as those Automation paths come online.
/// </summary>
public interface ILifecycleBackendClient
{
    /// <summary>
    /// POST <c>/api/internal/lifecycle/welcome-sent</c>. Returns false on any
    /// non-2xx / transport failure; caller treats as best-effort silent fail.
    /// Never throws on transport-class exceptions (HttpRequestException,
    /// TaskCanceledException) — implementation logs INV-AT-072 and returns false.
    /// OperationCanceledException from the caller's CancellationToken DOES
    /// propagate so a graceful Hangfire shutdown can unwind the job cleanly.
    /// </summary>
    Task<bool> SendWelcomeSentAsync(int tenantId, int leadId, CancellationToken ct);
}
