using Invekto.Marketing.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Marketing.Services.Jobs;

/// <summary>
/// FEAT-EFS Hangfire job handler — fires once per scheduled
/// <c>event_followup_runs</c> row at <c>scheduled_at</c>. Responsibilities:
///
///   1. Look up the run row (NULL → INV-MK-051 audit + return; sequence may have been deleted).
///   2. Re-resolve sequence enabled flag (sequence may have flipped to disabled mid-flight; INV-MK-054).
///   3. Re-check lead opt-out via <see cref="FollowupSequenceRepository.GetLeadFollowupStateAsync"/>.
///      Opt-out → run.status='skipped_optout' + INV-MK-052 + audit log + return without send.
///   4. SEND (MVP stub): emit structured <c>[FEAT-EFS][SEND-INTENT]</c> log line that operators
///      can grep, then mark run.status='sent' + executed_at=now. Actual chatoperation
///      delivery is deferred to a follow-up paket per
///      arch/plans/20260425-feat-efs-drip-sequence.json spec_architectural_decisions[8].
///
/// Job is invoked by Hangfire's worker process via reflection over the public
/// <see cref="ExecuteAsync(long, CancellationToken)"/> entry point. The DI container
/// resolves this class as a transient — Hangfire's
/// <see cref="JobActivator"/> creates a scope per job. Job arg is the run row id only;
/// all other state read fresh from DB on entry (no stale captured state).
///
/// Hangfire automatic retry is INTENTIONALLY DISABLED at registration time
/// (<see cref="Hangfire.AutomaticRetryAttribute"/> with Attempts=0) — the orchestrator
/// schedules each stage exactly once; transient failure → run.status='failed' + audit
/// row, operator inspects + re-triggers manually if needed. This avoids duplicate
/// outbound message risk under retry storms.
/// </summary>
[Hangfire.AutomaticRetry(Attempts = 0, OnAttemptsExceeded = Hangfire.AttemptsExceededAction.Fail)]
public sealed class FollowupStageJob
{
    private readonly FollowupSequenceRepository _repo;
    private readonly JsonLinesLogger _log;

    // Note: FollowupSequenceCache is NOT injected here. The slug-keyed cache lives on
    // the orchestrator hot path (EnqueueAsync); this job resolves sequences by id via
    // a focused repo SELECT, which doesn't fit the slug-keyed cache shape. Promoting
    // the cache to an id-keyed secondary store is deferred — profiling data would
    // justify it (Codex iter 2 CQ5 feedback: avoid unused DI dependencies).
    public FollowupStageJob(
        FollowupSequenceRepository repo,
        JsonLinesLogger log)
    {
        _repo = repo;
        _log = log;
    }

    /// <summary>
    /// Hangfire entry point. <paramref name="runId"/> is the only state passed across
    /// the queue boundary so retries / replays read canonical DB state, not stale args.
    /// </summary>
    public async Task ExecuteAsync(long runId, CancellationToken ct)
    {
        // (1) Run row lookup. Missing → sequence cascade or operator delete; nothing to do.
        // A DB transport failure here is surfaced via a terminal mark on a best-effort
        // basis (we still have the runId job argument). If the terminal mark ALSO fails
        // TerminalMarkAsync logs + swallows the secondary error (see method body) so the
        // job does not retry-loop under a genuine DB outage.
        FollowupRunRow? run;
        try
        {
            run = await _repo.GetRunAsync(runId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Hangfire shutdown — let it propagate, no terminal mark (job will requeue).
            throw;
        }
        catch (NpgsqlException ex)
        {
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStorageUnavailable,
                $"[{ErrorCodes.FollowupStorageUnavailable}] FollowupStageJob: DB error reading run {runId}: {ex.Message}",
                ct).ConfigureAwait(false);
            return;
        }

        if (run is null)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupStageJob: run {runId} missing (sequence deleted?), skipping.");
            return;
        }

        // Idempotency: if the run was already marked terminal (status != 'scheduled') we
        // skip silently. This guards against Hangfire mis-replay or operator re-trigger.
        if (!string.Equals(run.Status, FollowupRunStatusValues.Scheduled, StringComparison.Ordinal))
        {
            _log.SystemInfo(
                $"FollowupStageJob: run {runId} already terminal (status={run.Status}), skipping.");
            return;
        }

        // (2) Sequence enabled re-check. Cache returns NULL if sequence was deleted.
        FollowupSequenceConfig? sequence;
        try
        {
            // Re-resolve via slug? No — the run row carries sequence_id but not slug. We
            // bypass cache here and read by id directly via a focused repo query. Cache is
            // primarily for the orchestrator's enqueue hot path (slug-keyed).
            sequence = await ResolveSequenceByIdAsync(run.SequenceId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            // DB transport failure reading the sequence row — distinct failure class
            // from "sequence not found" (which is a logical absence, INV-MK-051). This
            // is transient storage unavailable → INV-MK-056 so operator alerting can
            // distinguish outage from data-loss.
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStorageUnavailable,
                $"[{ErrorCodes.FollowupStorageUnavailable}] FollowupStageJob: DB fail resolving sequence {run.SequenceId}: {ex.Message}",
                ct).ConfigureAwait(false);
            return;
        }

        if (sequence is null)
        {
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStageNotFound,
                $"FollowupStageJob: sequence {run.SequenceId} not found at execute time (deleted?), run {runId} → failed.",
                ct).ConfigureAwait(false);
            return;
        }

        if (!sequence.Enabled)
        {
            await TerminalMarkAsync(runId, FollowupRunStatusValues.SkippedDisabled,
                ErrorCodes.FollowupSequenceDisabled,
                $"[{ErrorCodes.FollowupSequenceDisabled}] FollowupStageJob: sequence {run.SequenceId} disabled at execute time, run {runId} skipped.",
                ct).ConfigureAwait(false);
            return;
        }

        if (run.StageIndex < 0 || run.StageIndex >= sequence.Stages.Count)
        {
            // Sequence shrunk after the row was scheduled — log + mark failed for audit.
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStageNotFound,
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupStageJob: stage {run.StageIndex} no longer exists in sequence {run.SequenceId} (count={sequence.Stages.Count}), run {runId} → failed.",
                ct).ConfigureAwait(false);
            return;
        }

        var stage = sequence.Stages[run.StageIndex];

        // (3) Lead state + opt-out gate.
        FollowupLeadState? leadState;
        try
        {
            leadState = await _repo.GetLeadFollowupStateAsync(run.TenantId, run.LeadId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            // Transient storage fail reading lead state — use INV-MK-056 so the audit
            // row distinguishes outage from missing lead (INV-MK-051).
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStorageUnavailable,
                $"[{ErrorCodes.FollowupStorageUnavailable}] FollowupStageJob: DB fail reading lead {run.LeadId} state: {ex.Message}",
                ct).ConfigureAwait(false);
            return;
        }

        if (leadState is null)
        {
            // Lead deleted (or tenant boundary changed) between schedule and execute.
            // Marked status='failed' with INV-MK-051 so the operator audit trail surfaces
            // a distinct cause from "DB error" or "opt-out" — there is nothing to send,
            // no template to attempt, and no eligible retry. (Comment intentionally
            // matches the Failed status; an earlier draft mis-said "skipped".)
            await TerminalMarkAsync(runId, FollowupRunStatusValues.Failed,
                ErrorCodes.FollowupStageNotFound,
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupStageJob: lead {run.LeadId} not found at execute time, run {runId} → failed (audit only, no action).",
                ct).ConfigureAwait(false);
            return;
        }

        if (leadState.OptOut)
        {
            await TerminalMarkAsync(runId, FollowupRunStatusValues.SkippedOptout,
                ErrorCodes.FollowupOptOut,
                $"[{ErrorCodes.FollowupOptOut}] FollowupStageJob: lead {run.LeadId} opted out (inma_optout_outbox latest=opt_out), run {runId} skipped.",
                ct).ConfigureAwait(false);
            return;
        }

        // (4) SEND (MVP stub) — see class XML doc + plan spec_architectural_decisions[8].
        // Emit ops-greppable structured line; downstream paket replaces this with an HTTP
        // hop to Outbound /api/v1/outbound/send (or equivalent template-resolved endpoint).
        _log.SystemInfo(
            $"[FEAT-EFS][SEND-INTENT] tenant={run.TenantId} lead={run.LeadId} sequence={run.SequenceId} " +
            $"stage={run.StageIndex} template={stage.TemplateSlug} group={stage.TemplateGroup ?? "-"} " +
            $"ab={run.AbGroup} run={runId}");

        await TerminalMarkAsync(runId, FollowupRunStatusValues.Sent, errorCode: null,
            logMessage: null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve a sequence by id. The cache is slug-keyed (orchestrator path), so this
    /// goes direct to repo. Could be promoted to a second cache shape if profiling
    /// shows hot-path pressure — MVP keeps it simple.
    /// </summary>
    private async Task<FollowupSequenceConfig?> ResolveSequenceByIdAsync(long sequenceId, CancellationToken ct)
    {
        // Inline minimal SELECT to avoid widening the repo surface for a single use.
        // Mirrors FindBySlugAsync shape so deserialization is identical.
        var conn = await OpenConnectionForRepoAsync(ct).ConfigureAwait(false);
        await using (conn)
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT id, slug, stages::text, ab_split_percent, enabled, created_at, updated_at " +
                "FROM event_followup_sequences WHERE id = @id LIMIT 1", conn);
            cmd.Parameters.AddWithValue("id", sequenceId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new FollowupSequenceConfig
            {
                Id = reader.GetInt64(0),
                Slug = reader.GetString(1),
                Stages = System.Text.Json.JsonSerializer.Deserialize<List<FollowupStageConfig>>(
                    reader.GetString(2), FollowupSequenceRepository.SnakeCaseJson)
                    ?? new List<FollowupStageConfig>(),
                AbSplitPercent = reader.GetInt16(3),
                Enabled = reader.GetBoolean(4),
                CreatedAt = reader.GetFieldValue<DateTime>(5),
                UpdatedAt = reader.GetFieldValue<DateTime>(6)
            };
        }
    }

    private async Task<NpgsqlConnection> OpenConnectionForRepoAsync(CancellationToken ct)
    {
        // The repository owns the factory; expose it via a thin pass-through method to
        // keep the job from grabbing a separate factory. Implementation chooses repo
        // helper so connection lifetime is consistent.
        return await _repo.OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    private async Task TerminalMarkAsync(
        long runId, string status, string? errorCode, string? logMessage, CancellationToken ct)
    {
        if (logMessage is not null)
            _log.SystemWarn(logMessage);

        try
        {
            await _repo.MarkRunTerminalAsync(runId, status, errorCode, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            // Best-effort: if the terminal mark itself fails, surface a structured error
            // so ops can manually fix the row. Do NOT rethrow — that would re-queue the
            // job (we have AutomaticRetry attempts=0 but defensive guard against
            // future-replays from operator action).
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupStageJob: DB fail marking run {runId} terminal " +
                $"(status={status}, code={errorCode ?? "-"}): {ex.Message}");
        }
    }
}
