namespace Chatinbox.Automation.Services.Lifecycle;

/// <summary>
/// Paket B-META: Automation → Backend internal HTTP hop for lifecycle events.
/// The concrete caller today is <c>TriggerWelcomeFlowJob</c> which fires
/// <see cref="SendWelcomeSentAsync"/> after the welcome flow emitted ≥1
/// outbound message.
///
/// Contract: fire-and-forget semantics. Failures are logged with INV-AT-072
/// by the implementation and surfaced to the job only as "don't retry,
/// don't throw" — the welcome message already reached the user, so a
/// downstream lifecycle-notification miss is tolerable (next inbound
/// engagement triggers its own lifecycle events). FEAT-INMA-PIPELINE-V2 C1
/// (2026-05-13): Backend handler is currently log-only; V2 C3 INMA-otorite
/// customer_status trigger will consume welcome-sent events when ready.
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
