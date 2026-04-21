using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Followup;

/// <summary>
/// FEAT-EFS Drip Sequence trigger reason — the originating domain event that asks the
/// FollowupOrchestrator to enqueue a per-stage drip schedule for a given lead.
///
/// MVP auto-emission scope: only <see cref="NoReplyWelcome"/> is automatically emitted by
/// Automation's NoReplyCheckJob (per-lead delayed check). The other three reasons are
/// accepted at the HTTP entry point (POST /api/internal/followup/trigger) as
/// forward-compat ops-manual hooks — Dent pilot has no offer state machine today, so
/// no scanner job emits these yet. See arch/plans/20260425-feat-efs-drip-sequence.json
/// spec_architectural_decisions[3] for the rationale.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FollowupTriggerReason
{
    /// <summary>
    /// Lead received the welcome flow but has not replied within the tenant's configured
    /// no-reply threshold (default 3 days). Automation's NoReplyCheckJob detects this and
    /// auto-triggers. This is the primary MVP path used by Dent pilot.
    /// </summary>
    NoReplyWelcome = 1,

    /// <summary>
    /// Lead explicitly declined the offer (operator-marked). Forward-compat — no auto-emission
    /// in MVP because Dent pilot does not have an offer state machine yet.
    /// </summary>
    OfferDeclined = 2,

    /// <summary>
    /// Offer expired without a decision (operator-marked or scheduled offer expiry hit).
    /// Forward-compat — no auto-emission in MVP.
    /// </summary>
    OfferTimeout = 3,

    /// <summary>
    /// Lead placed on hold for a configured period (operator-marked). Forward-compat — no
    /// auto-emission in MVP.
    /// </summary>
    OnHold = 4
}
