using Hangfire;
using Invekto.Automation.Services.Photos;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Photos;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-PHOTO Stage 1: Foto isteme dispatch job. Slot booking onaylaninca
/// (P11 sonrasi BackgroundJob.Schedule trigger 30 saniye delay ile bu
/// job'i enqueue eder — pilot wiring tarafi bu paket disinda;
/// post-pilot scope-correction patch).
///
/// Sorumluluklar:
///   1. Pre-check: leads.photo_status terminal degil mi (received/escalated
///      /rejected ise erken cikis — koordinator manuel halletmis VEYA
///      hasta zaten gondermis race).
///   2. INMA chatoperation /api/v1/callback/wapcrm uzerinden A varyantini
///      gonder (PhotoRequestService.SendPhotoMessageAsync).
///   3. leads.photo_status='requested' update + 24h sonra
///      PhotoRequestReminderJob enqueue (BackgroundJob.Schedule).
///
/// Retry policy: Hangfire AutomaticRetry=2 (30/120s backoff). INMA bridge
/// 5xx / timeout INV-AT-075 olarak loglanir. Max retry tukendi durumunda
/// INV-AT-077 terminal — Hangfire 'failed' state'e moved, Reminder zinciri
/// kurulmaz, koordinator manuel "Tekrar Iste" butonunu kullanir.
/// </summary>
// FEAT-PHOTO wire-up patch (2026-04-28): queue rename 'photo-request-dispatch' ->
// 'automation' (G7 single-queue-per-service topology). Orphan queue P10 EFS bug
// ikizinden kacinma; AddInvektoHangfire("automation", ...) zaten Automation servisinde
// kayitli, yeni queue eklemek yerine var olani kullaniyoruz. Plan
// arch/plans/20260428-feat-photo-wireup-patch.json AC1.
[Queue("automation")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 120 })]
public sealed class PhotoRequestDispatchJob
{
    /// <summary>24 saat reminder delay — pilot. Daha sonra tenant_settings'e
    /// tasinabilir.</summary>
    private static readonly TimeSpan ReminderDelay = TimeSpan.FromHours(24);

    private readonly IPhotoRequestService _service;
    private readonly IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _log;

    public PhotoRequestDispatchJob(
        IPhotoRequestService service,
        IBackgroundJobClient jobs,
        JsonLinesLogger log)
    {
        _service = service;
        _jobs = jobs;
        _log = log;
    }

    public async Task ExecuteAsync(int tenantId, int leadId, CancellationToken ct = default)
    {
        // §1. Pre-check — FollowupStageJob terminal-skip pattern.
        PhotoLeadState? state;
        try
        {
            state = await _service.GetLeadStateAsync(tenantId, leadId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestDispatchJob: lead state DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            throw; // Hangfire retry
        }
        catch (OperationCanceledException)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoRequestDispatchJob: cancelled during pre-check tenant={tenantId} lead={leadId} — Hangfire will requeue.");
            throw;
        }

        if (state is null)
        {
            // Lead silinmis (cascade veya manuel) — sessiz cikis.
            _log.SystemInfo(
                $"PhotoRequestDispatchJob: lead missing tenant={tenantId} lead={leadId} (deleted), skipping.");
            return;
        }

        var currentStatus = PhotoStatusExtensions.FromDbValue(state.PhotoStatus);
        if (currentStatus.IsTerminalForPhotoRequest())
        {
            _log.SystemInfo(
                $"PhotoRequestDispatchJob: terminal-skip tenant={tenantId} lead={leadId} status={state.PhotoStatus} count={state.PhotoCount} (race or manual). FollowupStageJob pre-check pattern.");
            return;
        }

        if (string.IsNullOrWhiteSpace(state.Phone))
        {
            // E.164 phone yok — leads tablosu corruption veya placeholder.
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestDispatchJob: phone empty tenant={tenantId} lead={leadId} — skipping (cannot dispatch).");
            return;
        }

        // §2. INMA chatoperation A varyanti gonderim. PhotoSendOutcome
        // donusu race-terminal sinyalini caller'a tasir (Codex iter 1
        // CQ1/CQ2 fix).
        PhotoSendOutcome sendOutcome;
        try
        {
            sendOutcome = await _service.SendPhotoMessageAsync(
                tenantId, leadId, state.Phone,
                templateSlug: "photo_request_a",
                updateStatusToRequested: true,
                ct: ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestDispatchJob: INMA bridge fail tenant={tenantId} lead={leadId}: {ex.Message} — Hangfire will retry");
            throw; // Hangfire AutomaticRetry tetikler
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestDispatchJob: status update DB fail tenant={tenantId} lead={leadId}: {ex.Message} — Hangfire will retry");
            throw;
        }
        catch (OperationCanceledException)
        {
            // Hangfire shutdown / job-level cancellation — re-raise so the
            // server can re-queue. INV-AT-082 traces the cancellation point.
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoRequestDispatchJob: cancelled mid-send tenant={tenantId} lead={leadId} — Hangfire will requeue.");
            throw;
        }

        // §2b. Race-terminal abort: SendPhotoMessageAsync mesaji gonderdi
        // ama status flip skip edildi (concurrent PhotoInboundHandler
        // 'received' yapmis). Reminder zincirini kurmamali — foto zaten
        // gelmis (Codex iter 1 CQ1/CQ2 fix).
        if (sendOutcome.RaceTerminalDetected)
        {
            _log.SystemInfo(
                $"[{ErrorCodes.PhotoRequestReminderRaceTerminal}] PhotoRequestDispatchJob: race-terminal detected tenant={tenantId} lead={leadId} — reminder NOT scheduled (foto already received).");
            return;
        }

        // §3. Reminder job enqueue (24h delay).
        // BackgroundJob.Schedule -> 'photo-request-reminders' queue.
        // Typed catch (Codex iter 0 CQ5/CQ12 fix — bare catch(Exception) policy
        // violation). Hangfire client failure modes: BackgroundJobClientException
        // (storage transport / serialization) + NpgsqlException (PG store) +
        // InvalidOperationException (DI misconfiguration).
        try
        {
            _jobs.Schedule<PhotoRequestReminderJob>(
                job => job.ExecuteAsync(tenantId, leadId, CancellationToken.None),
                ReminderDelay);
            _log.SystemInfo(
                $"PhotoRequestDispatchJob: dispatched + reminder scheduled tenant={tenantId} lead={leadId} delay={ReminderDelay.TotalHours}h");
        }
        catch (Hangfire.BackgroundJobClientException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobStorageConnectionFailed}] PhotoRequestDispatchJob: reminder schedule failed (Hangfire client) tenant={tenantId} lead={leadId}: {ex.Message} (dispatch ok, reminder lost — operator may re-arm)");
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobStorageConnectionFailed}] PhotoRequestDispatchJob: reminder schedule failed (Hangfire PG store) tenant={tenantId} lead={leadId}: {ex.Message} (dispatch ok, reminder lost — operator may re-arm)");
        }
        catch (InvalidOperationException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobHandlerUnresolved}] PhotoRequestDispatchJob: reminder schedule failed (DI misconfig) tenant={tenantId} lead={leadId}: {ex.Message} (dispatch ok, reminder lost — operator must inspect DI registration)");
        }
    }
}
