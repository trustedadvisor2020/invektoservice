using Hangfire;
using Invekto.Marketing.Data;
using Invekto.Marketing.Services.Jobs;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Marketing.Services;

/// <summary>
/// FEAT-EFS entry point invoked by /api/internal/followup/trigger handler. Translates a
/// <see cref="FollowupTriggerRequest"/> into N-1 Hangfire scheduled jobs (one per stage)
/// + 1 deterministic A/B group decision recorded on the lead.
///
/// Responsibilities:
///   - Resolve sequence by tenant + slug (pulls from <see cref="FollowupSequenceCache"/>).
///   - Reject if sequence missing (INV-MK-051) or disabled (INV-MK-054).
///   - Reject if lead already has an active scheduled run (INV-MK-055 idempotency).
///   - Compute deterministic A/B group via
///     <see cref="FollowupAbGroupAssigner.Assign(int, long, long, int)"/>.
///   - Control group: persist leads.followup_ab_group='control' + zero rows scheduled.
///   - Drip group: per stage, INSERT event_followup_runs + BackgroundJob.Schedule(..delay..).
///   - Stage delay unit: minutes when tenant_settings.efs_test_mode=TRUE, days otherwise.
///
/// Returns a <see cref="FollowupOrchestratorOutcome"/> that the endpoint handler maps to
/// the wire <see cref="FollowupTriggerResponse"/>.
/// </summary>
public sealed class FollowupOrchestrator
{
    private readonly FollowupSequenceRepository _repo;
    private readonly FollowupSequenceCache _cache;
    private readonly IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _log;

    public FollowupOrchestrator(
        FollowupSequenceRepository repo,
        FollowupSequenceCache cache,
        IBackgroundJobClient jobs,
        JsonLinesLogger log)
    {
        _repo = repo;
        _cache = cache;
        _jobs = jobs;
        _log = log;
    }

    /// <summary>
    /// Enqueue a sequence for the lead. Idempotent at the (tenant, lead) level — second
    /// call while the first sequence is in flight returns INV-MK-055.
    /// </summary>
    public async Task<FollowupOrchestratorOutcome> EnqueueAsync(
        int tenantId,
        long leadId,
        string sequenceSlug,
        string? requestId,
        CancellationToken ct)
    {
        var rid = requestId ?? Guid.NewGuid().ToString("N");

        // (1) Resolve sequence.
        var sequence = await _cache.GetOrFetchAsync(tenantId, sequenceSlug, ct).ConfigureAwait(false);
        if (sequence is null || sequence.Id is null)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupOrchestrator: sequence '{sequenceSlug}' not found for tenant={tenantId} (request={rid}).");
            return FollowupOrchestratorOutcome.Reject(
                ErrorCodes.FollowupStageNotFound,
                $"Sequence '{sequenceSlug}' bulunamadi (tenant={tenantId}). Once Dashboard'tan sequence olusturun.");
        }

        if (!sequence.Enabled)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupSequenceDisabled}] FollowupOrchestrator: sequence '{sequenceSlug}' disabled (tenant={tenantId}, request={rid}).");
            return FollowupOrchestratorOutcome.Reject(
                ErrorCodes.FollowupSequenceDisabled,
                $"Sequence '{sequenceSlug}' devre disi. Sequence ayarlarindan enable edip tekrar deneyin.");
        }

        // (2) Read tenant test mode + threshold (single round-trip).
        var tenantSettings = await _repo.GetTenantEfsSettingsAsync(tenantId, ct).ConfigureAwait(false);

        // (3) Re-validate config against runtime test mode (cap reinterpretation as minutes).
        try
        {
            FollowupSequenceValidator.Validate(sequence, tenantSettings.TestMode);
        }
        catch (FollowupSequenceValidationException ex)
        {
            _log.SystemWarn(
                $"[{ex.ErrorCode}] FollowupOrchestrator: validation fail at enqueue (sequence='{sequenceSlug}', tenant={tenantId}, request={rid}): {ex.Message}");
            return FollowupOrchestratorOutcome.Reject(ex.ErrorCode, ex.Message);
        }

        // (4) Lead state + collision detection.
        var leadState = await _repo.GetLeadFollowupStateAsync(tenantId, leadId, ct).ConfigureAwait(false);
        if (leadState is null)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupStageNotFound}] FollowupOrchestrator: lead {leadId} not found (tenant={tenantId}, request={rid}).");
            return FollowupOrchestratorOutcome.Reject(
                ErrorCodes.FollowupStageNotFound,
                $"Lead {leadId} bulunamadi (tenant={tenantId}).");
        }

        if (leadState.OptOut)
        {
            // Pre-emptive opt-out skip — also recorded by stage job, but we save the
            // schedule churn here.
            _log.SystemInfo(
                $"[{ErrorCodes.FollowupOptOut}] FollowupOrchestrator: lead {leadId} already opted out at enqueue, no rows scheduled (tenant={tenantId}, request={rid}).");
            return FollowupOrchestratorOutcome.Reject(
                ErrorCodes.FollowupOptOut,
                $"Lead {leadId} opt-out durumda. Followup baslatilmadi.");
        }

        var existingScheduled = await _repo.CountScheduledRunsForLeadAsync(tenantId, leadId, ct).ConfigureAwait(false);
        if (existingScheduled > 0)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupRunCollision}] FollowupOrchestrator: lead {leadId} already has {existingScheduled} scheduled runs (tenant={tenantId}, request={rid}).");
            return FollowupOrchestratorOutcome.Reject(
                ErrorCodes.FollowupRunCollision,
                $"Lead {leadId} icin zaten aktif scheduled sequence var (mevcut: {existingScheduled} stage). Mevcut tamamlandiginda veya iptal edildiginde tekrar deneyin.");
        }

        // (5) Deterministic A/B assignment.
        var sequenceId = sequence.Id.Value;
        var abGroup = FollowupAbGroupAssigner.Assign(tenantId, leadId, sequenceId, sequence.AbSplitPercent);

        // Persist the group decision regardless of drip/control so cross-tenant reporting
        // can SELECT followup_ab_group on every lead routed through EFS.
        await _repo.SetLeadAbGroupAsync(tenantId, leadId, abGroup, sequenceId, ct).ConfigureAwait(false);

        if (string.Equals(abGroup, FollowupAbGroupAssigner.Control, StringComparison.Ordinal))
        {
            _log.SystemInfo(
                $"[FEAT-EFS] FollowupOrchestrator: lead {leadId} → CONTROL group, zero rows scheduled (tenant={tenantId}, sequence='{sequenceSlug}', request={rid}).");
            return FollowupOrchestratorOutcome.AcceptedControl(sequenceId);
        }

        // (6) Drip group → schedule per-stage Hangfire jobs.
        var now = DateTime.UtcNow;
        var cumulative = TimeSpan.Zero;
        var runIds = new List<long>(sequence.Stages.Count);

        for (var i = 0; i < sequence.Stages.Count; i++)
        {
            var stage = sequence.Stages[i];
            cumulative += DelayFor(stage.DelayDays, tenantSettings.TestMode);
            var scheduledAt = now + cumulative;

            // INSERT before Schedule so a Hangfire failure doesn't leave dangling jobs
            // without a run row. Conversely if Schedule fails after INSERT we have an
            // orphan row with hangfire_job_id NULL — operator can detect via
            // event_followup_runs WHERE hangfire_job_id IS NULL AND created_at < now()-1h.
            //
            // Concurrent-trigger race guard (Migration 029 partial unique index
            // uq_efs_runs_lead_stage_scheduled): a parallel EnqueueAsync that already
            // scheduled stage i for this lead causes the INSERT to throw Npgsql 23505.
            // Translate that into the orchestrator's idempotency reject (INV-MK-055)
            // so the second caller sees the same answer the pre-flight Count check
            // would have given on a non-racing path.
            long runId;
            try
            {
                runId = await _repo.InsertScheduledRunAsync(
                    tenantId, sequenceId, leadId, stageIndex: i, abGroup,
                    scheduledAt, ct).ConfigureAwait(false);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupRunCollision}] FollowupOrchestrator: concurrent enqueue race detected " +
                    $"(tenant={tenantId}, lead={leadId}, stage={i}, request={rid}): {ex.MessageText}");
                return FollowupOrchestratorOutcome.Reject(
                    ErrorCodes.FollowupRunCollision,
                    $"Lead {leadId} icin paralel trigger tespit edildi (stage {i} zaten scheduled). Mevcut sequence tamamlandiginda tekrar deneyin.");
            }
            runIds.Add(runId);

            // Typed catches only (lessons 2026-04-21 Codex iter 1 CQ1/CQ5): Hangfire's
            // BackgroundJob.Schedule can fail with BackgroundJobClientException (storage
            // unreachable / serialization) or NpgsqlException (Hangfire uses PG storage).
            // Any other exception propagates to the framework handler so a genuinely
            // unexpected runtime issue is not silently absorbed.
            string hangfireJobId;
            try
            {
                hangfireJobId = _jobs.Schedule<FollowupStageJob>(
                    job => job.ExecuteAsync(runId, CancellationToken.None),
                    cumulative);
            }
            catch (BackgroundJobClientException ex)
            {
                // Mark the orphan row failed so operator sees the cause without scanning logs.
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] FollowupOrchestrator: Hangfire Schedule rejected run {runId} (tenant={tenantId}, lead={leadId}, stage={i}, request={rid}): {ex.Message}");
                await _repo.MarkRunTerminalAsync(runId, FollowupRunStatusValues.Failed,
                    ErrorCodes.FollowupStorageUnavailable, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (NpgsqlException ex)
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] FollowupOrchestrator: Hangfire storage DB fail run {runId} (tenant={tenantId}, lead={leadId}, stage={i}, request={rid}): {ex.Message}");
                await _repo.MarkRunTerminalAsync(runId, FollowupRunStatusValues.Failed,
                    ErrorCodes.FollowupStorageUnavailable, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            await _repo.UpdateRunHangfireJobIdAsync(runId, hangfireJobId, ct).ConfigureAwait(false);
        }

        _log.SystemInfo(
            $"[FEAT-EFS] FollowupOrchestrator: lead {leadId} → DRIP group, {runIds.Count} stages scheduled (tenant={tenantId}, sequence='{sequenceSlug}', test_mode={tenantSettings.TestMode}, request={rid}).");

        return FollowupOrchestratorOutcome.AcceptedDrip(sequenceId, runIds.Count);
    }

    /// <summary>
    /// Test mode flips the unit interpretation: each unit of stage delay is a minute when
    /// TRUE, a day otherwise. Cap enforcement happens upstream in the validator using the
    /// same TestMode flag, so this method is the only place runtime conversion happens.
    /// </summary>
    private static TimeSpan DelayFor(int delayUnits, bool testMode) =>
        testMode ? TimeSpan.FromMinutes(delayUnits) : TimeSpan.FromDays(delayUnits);
}

/// <summary>Result of <see cref="FollowupOrchestrator.EnqueueAsync"/>.</summary>
public sealed class FollowupOrchestratorOutcome
{
    public bool Accepted { get; private init; }
    public string? AbGroup { get; private init; }
    public int ScheduledRuns { get; private init; }
    public long? SequenceId { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static FollowupOrchestratorOutcome AcceptedDrip(long sequenceId, int scheduledRuns) => new()
    {
        Accepted = true,
        AbGroup = FollowupAbGroupAssigner.Drip,
        ScheduledRuns = scheduledRuns,
        SequenceId = sequenceId
    };

    public static FollowupOrchestratorOutcome AcceptedControl(long sequenceId) => new()
    {
        Accepted = true,
        AbGroup = FollowupAbGroupAssigner.Control,
        ScheduledRuns = 0,
        SequenceId = sequenceId
    };

    public static FollowupOrchestratorOutcome Reject(string errorCode, string errorMessage) => new()
    {
        Accepted = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
