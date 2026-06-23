using Chatinbox.Shared.Contracts.Inma.Dtos;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// FEAT-PROJELER / cxapi send engine — PR-3a.
/// The single, pure source of the idempotency doctrine (Codex P0 #2): it maps a
/// <see cref="WapCrmSendOutcome"/> (plus the post-attempt count) to the terminal decision
/// the sender persists. Kept pure + side-effect-free so every branch is unit-tested without
/// a DB or HTTP — the place a bug would be catastrophic (duplicate WhatsApp sends).
///
/// Doctrine:
///   * Submitted        -> 'sent'   (counter: sent)      — provider accepted it.
///   * ProviderFailed   -> 'failed' (counter: failed)    — HTTP 200 + status=false.
///   * RateLimited      -> requeue to 'queued' + cooldown WHILE attempt &lt; MaxSendAttempts;
///                          once exhausted -> 'failed' (counter: failed). 301/302 is the ONLY
///                          retried outcome (provably not sent).
///   * Ambiguous        -> 'ambiguous' (counter: ambiguous) — timeout: delivery unknown.
///   * TransportError   -> 'ambiguous' (counter: ambiguous) — conservative: it can also fail
///                          in the response phase, so it is NOT assumed retry-safe.
/// Only RateLimited is ever retried; everything else is terminal.
/// </summary>
public static class CxapiOutcomeMapper
{
    public const string StatusQueued = "queued";
    public const string StatusSent = "sent";
    public const string StatusFailed = "failed";
    public const string StatusAmbiguous = "ambiguous";

    public const string CounterSent = "sent";
    public const string CounterFailed = "failed";
    public const string CounterAmbiguous = "ambiguous";

    /// <param name="outcome">The typed result from <see cref="WapCrmSendClient"/>.</param>
    /// <param name="attemptCount">attempt_count AFTER this send (= prior count + 1).</param>
    /// <param name="maxSendAttempts">Cap on RateLimited requeues before the message is failed.</param>
    public static CxapiSendDecision Map(WapCrmSendOutcome outcome, int attemptCount, int maxSendAttempts) => outcome switch
    {
        WapCrmSendOutcome.Submitted =>
            new CxapiSendDecision(StatusSent, CounterSent, Requeue: false, ApplyCooldown: false),

        WapCrmSendOutcome.ProviderFailed =>
            new CxapiSendDecision(StatusFailed, CounterFailed, Requeue: false, ApplyCooldown: false),

        // 301/302 is provably-not-sent, so it is the only safe-to-retry outcome.
        WapCrmSendOutcome.RateLimited when attemptCount < maxSendAttempts =>
            new CxapiSendDecision(StatusQueued, CounterColumn: null, Requeue: true, ApplyCooldown: true),

        WapCrmSendOutcome.RateLimited =>
            new CxapiSendDecision(StatusFailed, CounterFailed, Requeue: false, ApplyCooldown: false),

        // Timeout / transport: delivery unknown -> never auto-retried (duplicate-send risk).
        WapCrmSendOutcome.Ambiguous or WapCrmSendOutcome.TransportError =>
            new CxapiSendDecision(StatusAmbiguous, CounterAmbiguous, Requeue: false, ApplyCooldown: false),

        _ => new CxapiSendDecision(StatusAmbiguous, CounterAmbiguous, Requeue: false, ApplyCooldown: false)
    };
}

/// <summary>
/// The terminal decision for a single cxapi send. <see cref="Requeue"/> => status goes back to
/// 'queued' (retried next cycle); otherwise <see cref="Status"/> is terminal. <see cref="CounterColumn"/>
/// (sent|failed|ambiguous) is the outbound_broadcasts counter to increment (null on requeue).
/// </summary>
public readonly record struct CxapiSendDecision(
    string Status,
    string? CounterColumn,
    bool Requeue,
    bool ApplyCooldown);
