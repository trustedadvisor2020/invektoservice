using Hangfire;
using Invekto.Automation.Services.Photos;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Photos;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-PHOTO Stage 3: 48 saat sessizlik sonrasi koordinator devri job.
/// PhotoRequestReminderJob basariyla tamamlaninca BackgroundJob.Schedule
/// (..., 24h) ile 'photo-request-reminders' queue'sunda enqueue edilir
/// (toplam 48h: Dispatch + 24h Reminder + 24h Escalation).
///
/// Davranis:
///   * Pre-check: photo_status terminal ('received'/'escalated'/'rejected')
///     ise erken cikis + INV-AT-076 log (idempotent re-run safe).
///   * Aksi halde leads.photo_status='escalated' set + nazik koordinator
///     devri mesaji gonderim. Dashboard rozet otomatik gosterir (rozet
///     RiAgentLeaderboard / lead detay panelinde photo_status='escalated'
///     filter ile).
///   * Mesaj gonderim de bu job tarafinda — ayri bir koordinator notification
///     surface'i yok (plan intentional_exclusions).
///
/// Hangfire AutomaticRetry=0 (transient fail = manuel re-trigger). Reminder
/// pattern aynen.
/// </summary>
// FEAT-PHOTO wire-up patch (2026-04-28): queue rename 'photo-request-reminders' ->
// 'automation' (G7 single-queue-per-service topology; PhotoEscalationJob ayni
// queue'yu PhotoRequestReminderJob ile paylasir cunku ayni servis dinler). Plan
// arch/plans/20260428-feat-photo-wireup-patch.json AC1.
[Queue("automation")]
[AutomaticRetry(Attempts = 0)]
public sealed class PhotoEscalationJob
{
    private readonly IPhotoRequestService _service;
    private readonly JsonLinesLogger _log;

    public PhotoEscalationJob(IPhotoRequestService service, JsonLinesLogger log)
    {
        _service = service;
        _log = log;
    }

    public async Task ExecuteAsync(int tenantId, int leadId, CancellationToken ct = default)
    {
        PhotoLeadState? state;
        try
        {
            state = await _service.GetLeadStateAsync(tenantId, leadId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoEscalationAlreadyTerminal}] PhotoEscalationJob: lead state DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoEscalationJob: cancelled during pre-check tenant={tenantId} lead={leadId} (AutomaticRetry=0 — manual re-trigger required).");
            return;
        }

        if (state is null)
        {
            _log.SystemInfo(
                $"PhotoEscalationJob: lead missing tenant={tenantId} lead={leadId} (deleted), skipping.");
            return;
        }

        var currentStatus = PhotoStatusExtensions.FromDbValue(state.PhotoStatus);
        if (currentStatus.IsTerminalForPhotoRequest())
        {
            _log.SystemInfo(
                $"[{ErrorCodes.PhotoEscalationAlreadyTerminal}] PhotoEscalationJob: already-terminal tenant={tenantId} lead={leadId} status={state.PhotoStatus} count={state.PhotoCount} — early return.");
            return;
        }

        // §1. Status flip 'escalated' — yalnizca 'requested' state'inden
        // (Codex iter 0 CQ9 fix: 'received' geldi ise escalation flip
        // YAPILMAZ, idempotent re-run safe). PhotoLifecycleStage.
        // EscalationFromRequested race-guard'i.
        int rowsFlipped;
        try
        {
            rowsFlipped = await _service.MarkPhotoStatusAsync(
                tenantId, leadId,
                PhotoStatusExtensions.DbValueEscalated,
                PhotoLifecycleStage.EscalationFromRequested,
                ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoEscalationAlreadyTerminal}] PhotoEscalationJob: status DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoEscalationJob: cancelled during status flip tenant={tenantId} lead={leadId}.");
            return;
        }

        if (rowsFlipped == 0)
        {
            // Race condition: pre-check ile UPDATE arasinda PhotoInboundHandler
            // 'received' yapti (foto geldi). Mesaj gonderme — koordinator
            // devri gerekmiyor, hasta foto verdi.
            _log.SystemInfo(
                $"[{ErrorCodes.PhotoEscalationAlreadyTerminal}] PhotoEscalationJob: race-skip tenant={tenantId} lead={leadId} (concurrent receive after pre-check) — no escalation message sent.");
            return;
        }

        // §2. Hastaya koordinator devri mesaji. Status zaten flipped; mesaj
        // gonderim faili koordinator devrini gormezden gelmez (Dashboard
        // rozet zaten goruntuluyor). updateStatusToRequested=false oldugu
        // icin PhotoSendOutcome.RaceTerminalDetected sinyali bu yolda devre
        // disi; outcome discard edilir (status flip §1'de zaten yapildi).
        try
        {
            _ = await _service.SendPhotoMessageAsync(
                tenantId, leadId, state.Phone,
                templateSlug: "photo_escalation",
                updateStatusToRequested: false,
                ct: ct).ConfigureAwait(false);
            _log.SystemInfo(
                $"PhotoEscalationJob: escalated tenant={tenantId} lead={leadId} (status=escalated, koordinator devri sent)");
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoEscalationJob: escalation message INMA fail tenant={tenantId} lead={leadId}: {ex.Message} (status flipped, message lost — koordinator panel notification still active)");
        }
        catch (NpgsqlException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoEscalationJob: escalation message DB fail tenant={tenantId} lead={leadId}: {ex.Message} (status flipped, message lost)");
        }
        catch (InvalidOperationException ex)
        {
            // PhotoRequestService unknown template_slug throws this — config bug.
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoEscalationJob: escalation template config fail tenant={tenantId} lead={leadId}: {ex.Message} (status flipped, message lost — operator must verify template_slug='photo_escalation' is registered)");
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown mid-escalation; status zaten flipped, panel
            // bildirimi aktif. INV-AT-082 ile sinyal yukseltilir (Codex
            // iter 1 CQ12 silent OCE fix).
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoEscalationJob: cancelled mid-escalation-message tenant={tenantId} lead={leadId} (status flipped, message lost — panel rozet aktif).");
        }
    }
}
