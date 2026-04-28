using Hangfire;
using Invekto.Automation.Services.Photos;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Photos;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-PHOTO Stage 2: 24 saat sessizlik hatirlatma job. PhotoRequestDispatch
/// Job basariyla tamamlaninca BackgroundJob.Schedule(..., 24h) ile
/// 'photo-request-reminders' queue'sunda enqueue edilir.
///
/// Pre-check pattern (FollowupStageJob.cs line 35-62):
///   * Lead silinmis -> sessiz cikis.
///   * photo_status terminal (received/escalated/rejected) -> early-return
///     + INV-AT-074 log (race condition: hasta tam bu sirada foto gonderdi).
///   * Aksi halde 'photo_reminder_24h' template ile nazik hatirlatma.
///
/// Hangfire AutomaticRetry=0 (transient fail = manuel re-trigger gerek).
/// FollowupStageJob disiplini: scheduled job'lar duplicate-send riskini
/// yok etmek icin hicbir surette retry tetiklemez.
///
/// 48h escalation chain: Reminder dispatch sonrasi BackgroundJob.Schedule
/// ile +24h gecikmeli PhotoEscalationJob enqueue (toplam 48h: dispatch + 24h
/// reminder + 24h escalation).
/// </summary>
[Queue("photo-request-reminders")]
[AutomaticRetry(Attempts = 0)]
public sealed class PhotoRequestReminderJob
{
    private static readonly TimeSpan EscalationDelay = TimeSpan.FromHours(24);

    private readonly IPhotoRequestService _service;
    private readonly IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _log;

    public PhotoRequestReminderJob(
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
        // §1. Pre-check (race condition guard).
        PhotoLeadState? state;
        try
        {
            state = await _service.GetLeadStateAsync(tenantId, leadId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestReminderRaceTerminal}] PhotoRequestReminderJob: lead state DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            return; // AutomaticRetry=0 — sessiz cikis
        }
        catch (OperationCanceledException)
        {
            // Hangfire shutdown — log + return (AutomaticRetry=0 oldugu icin
            // re-queue olmaz; ops sonraki window'da manuel re-trigger).
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoRequestReminderJob: cancelled during pre-check tenant={tenantId} lead={leadId} — manual re-trigger required.");
            return;
        }

        if (state is null)
        {
            _log.SystemInfo(
                $"PhotoRequestReminderJob: lead missing tenant={tenantId} lead={leadId} (deleted), skipping.");
            return;
        }

        var currentStatus = PhotoStatusExtensions.FromDbValue(state.PhotoStatus);
        if (currentStatus.IsTerminalForPhotoRequest())
        {
            _log.SystemInfo(
                $"[{ErrorCodes.PhotoRequestReminderRaceTerminal}] PhotoRequestReminderJob: terminal-skip tenant={tenantId} lead={leadId} status={state.PhotoStatus} count={state.PhotoCount} — race or manual.");
            return;
        }

        // §2. Reminder mesaji gonder. Status flip yok (zaten 'requested'),
        // sadece text gonderim. updateStatusToRequested=false → outcome
        // PhotoSendOutcome.StatusFlipApplied her zaman true, race sinyali
        // bu yolda devre disi (reminder zaten flip yapmiyor).
        try
        {
            _ = await _service.SendPhotoMessageAsync(
                tenantId, leadId, state.Phone,
                templateSlug: "photo_reminder_24h",
                updateStatusToRequested: false,
                ct: ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestReminderJob: INMA bridge fail tenant={tenantId} lead={leadId}: {ex.Message} (AutomaticRetry=0 — manuel re-trigger needed)");
            return;
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestReminderJob: DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoRequestReminderJob: cancelled mid-send tenant={tenantId} lead={leadId} (AutomaticRetry=0 — Escalation chain not scheduled).");
            return;
        }

        // §3. Escalation chain (24h sonra).
        // Typed catch (Codex iter 0 CQ5/CQ12 fix — bare catch(Exception) policy
        // violation). Hangfire client failure modes: BackgroundJobClientException
        // (storage transport / serialization) + NpgsqlException (PG store) +
        // InvalidOperationException (DI misconfiguration).
        try
        {
            _jobs.Schedule<PhotoEscalationJob>(
                job => job.ExecuteAsync(tenantId, leadId, CancellationToken.None),
                EscalationDelay);
            _log.SystemInfo(
                $"PhotoRequestReminderJob: reminder sent + escalation scheduled tenant={tenantId} lead={leadId} delay={EscalationDelay.TotalHours}h");
        }
        catch (Hangfire.BackgroundJobClientException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobStorageConnectionFailed}] PhotoRequestReminderJob: escalation schedule failed (Hangfire client) tenant={tenantId} lead={leadId}: {ex.Message} (reminder ok, escalation lost — operator may re-arm)");
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobStorageConnectionFailed}] PhotoRequestReminderJob: escalation schedule failed (Hangfire PG store) tenant={tenantId} lead={leadId}: {ex.Message} (reminder ok, escalation lost — operator may re-arm)");
        }
        catch (InvalidOperationException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.JobHandlerUnresolved}] PhotoRequestReminderJob: escalation schedule failed (DI misconfig) tenant={tenantId} lead={leadId}: {ex.Message} (reminder ok, escalation lost — operator must inspect DI registration)");
        }
    }
}
