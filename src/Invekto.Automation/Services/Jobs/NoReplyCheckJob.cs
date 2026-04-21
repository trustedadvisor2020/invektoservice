using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-EFS Hangfire delayed handler — when fired, calls
/// <see cref="MarketingFollowupClient.TriggerAsync"/> with reason=no_reply_welcome.
///
/// MVP scope: this class is the HANDLER only. Scheduling (the "did the welcome
/// enqueue point queue this job?" side) and the concrete "did the lead reply already?"
/// pre-check are both DEFERRED to a follow-up paket per
/// arch/plans/20260425-feat-efs-drip-sequence.json spec_architectural_decisions[3].
/// The follow-up paket will pair the scheduling site with a canonical inbound source
/// (either a new leads column updated by ChatAnalysis or a dedicated `chat_inbound_log`
/// table) — references to speculative schemas in this class were removed per Codex
/// iter 1 CQ9/CQ11 feedback to avoid schema-drift risk.
///
/// Pilot smoke path (P9 roadmap): operator curls
/// /api/internal/followup/trigger directly with the test lead id; this job is NOT on
/// the smoke path. The class is shipped + DI-registered so the follow-up paket can
/// wire the scheduling site with a single BackgroundJob.Schedule line.
///
/// Idempotency: Marketing's orchestrator carries the (tenant, lead) collision guard
/// (INV-MK-055 + partial unique index), so duplicate NoReplyCheckJob fires for the
/// same lead are rejected at the Marketing side — this job does NOT need a pre-flight
/// dedupe of its own.
///
/// Hangfire automatic retry is INTENTIONALLY DISABLED — a transient Marketing outage
/// during this check should NOT cause duplicate triggers (which would bypass the
/// orchestrator collision guard if the previous attempt actually succeeded but the
/// response was lost). Operator inspects <c>event_followup_runs</c> + Hangfire
/// failed-jobs list and re-schedules manually if needed.
/// </summary>
[Hangfire.AutomaticRetry(Attempts = 0, OnAttemptsExceeded = Hangfire.AttemptsExceededAction.Fail)]
public sealed class NoReplyCheckJob
{
    private readonly MarketingFollowupClient _marketing;
    private readonly JsonLinesLogger _log;

    public NoReplyCheckJob(
        MarketingFollowupClient marketing,
        JsonLinesLogger log)
    {
        _marketing = marketing;
        _log = log;
    }

    /// <summary>
    /// Hangfire entry point. <paramref name="requestId"/> is the original LIW intake
    /// request id, so the trigger trace ties back to the lead's first inbound. The
    /// <paramref name="welcomeEnqueuedUtc"/> parameter is retained in the signature for
    /// forward-compat with the deferred scheduling paket (operators / future code can
    /// pass the welcome enqueue timestamp for audit context); the handler currently
    /// logs it but does not gate on it.
    /// </summary>
    public async Task ExecuteAsync(
        int tenantId, long leadId, DateTime welcomeEnqueuedUtc, string requestId, CancellationToken ct)
    {
        _log.SystemInfo(
            $"[FEAT-EFS] NoReplyCheckJob: fired (tenant={tenantId}, lead={leadId}, " +
            $"welcome_enqueued={welcomeEnqueuedUtc:O}, request={requestId}); " +
            "inbound pre-check deferred to follow-up paket — relying on Marketing " +
            "orchestrator collision guard (INV-MK-055) for idempotency.");

        var outcome = await _marketing.TriggerAsync(
            tenantId, leadId, FollowupTriggerReason.NoReplyWelcome,
            sequenceSlug: null, requestId, ct).ConfigureAwait(false);

        if (outcome.Accepted)
        {
            _log.SystemInfo(
                $"[FEAT-EFS] NoReplyCheckJob: triggered Marketing follow-up " +
                $"(tenant={tenantId}, lead={leadId}, group={outcome.AbGroup ?? "-"}, " +
                $"runs={outcome.ScheduledRuns}, sequence={outcome.SequenceId?.ToString() ?? "-"}, " +
                $"request={requestId}).");
        }
        else if (string.Equals(outcome.ErrorCode, ErrorCodes.FollowupRunCollision, StringComparison.Ordinal))
        {
            // Expected when the lead already has a scheduled sequence — not an error.
            _log.SystemInfo(
                $"[FEAT-EFS] NoReplyCheckJob: lead {leadId} already has an active scheduled " +
                $"sequence (tenant={tenantId}, request={requestId}); no new trigger emitted.");
        }
        else
        {
            _log.SystemWarn(
                $"[{outcome.ErrorCode ?? ErrorCodes.FollowupUpstreamUnavailable}] NoReplyCheckJob: " +
                $"Marketing rejected trigger (tenant={tenantId}, lead={leadId}, request={requestId}): " +
                $"{outcome.ErrorMessage ?? "unknown"}.");
        }
    }
}
