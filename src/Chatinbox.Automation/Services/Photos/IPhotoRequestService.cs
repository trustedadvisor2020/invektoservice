namespace Chatinbox.Automation.Services.Photos;

/// <summary>
/// FEAT-PHOTO Automation tarafindaki dispatch yardimcisi: Hangfire job'lari
/// (PhotoRequestDispatchJob / PhotoRequestReminderJob / PhotoEscalationJob)
/// bu interface uzerinden mesaj gonderim + lifecycle update yapar.
///
/// Tek surface'te toplama nedeni:
///   * Test isolation: dispatch + reminder + escalation arasinda paylasilan
///     "INMA chatoperation gonder + leads.photo_status update" iki adimi
///     tek wrapper'da. Mock'lamak icin tek arayuz.
///   * Bridge limit pilot: gercek media attachment yok, text-only gonderim
///     (sablon image referansi text icinde URL link). Bu kisit tek yerde
///     enkapsule edilir; bridge expansion paketinde sadece servis
///     implementasyonu degisir.
///   * INMA wrapper: /api/v1/callback/wapcrm bridge call'u (Backend'in tek
///     authoritative bridge'i — INMA chatoperation tek kopru). Plan
///     scope_discipline gerekligi.
/// </summary>
public interface IPhotoRequestService
{
    /// <summary>
    /// Hastaya foto isteme mesajini gonderir + leads.photo_status='requested'
    /// + photo_count guncellenmez (sadece status flip). Caller (dispatch job)
    /// retry semantigini sarar; bu metod sadece dogrudan bir bridge call +
    /// DB update yapar, exception throw eder veya success doner.
    /// </summary>
    /// <param name="tenantId">Gonderim tenant'i.</param>
    /// <param name="leadId">Hangi lead.</param>
    /// <param name="phone">E.164 normalize phone.</param>
    /// <param name="templateSlug">Template slug (photo_request_a /
    /// photo_request_b / photo_reminder_24h / photo_escalation).
    /// PhotoRequestService template metnini DentAdavista seed'lerinden
    /// resolve eder; pilotta cache yok, her gonderimde fresh fetch.</param>
    /// <param name="updateStatusToRequested">true ise dispatch path; false
    /// ise reminder path (status 'requested'da kalir, photo_status flip
    /// yok).</param>
    /// <returns>
    /// <see cref="PhotoSendOutcome"/> — caller (Hangfire job) bu sonuca
    /// bakarak Reminder/Escalation enqueue zincirini abort edebilir
    /// (Codex iter 1 CQ1/CQ2 fix: flip-skip caller'a gorunmez kaliyordu).
    /// Sent=true + StatusFlipApplied=false durumunda mesaj gonderildi ama
    /// race condition'da terminal state goruldu — Dispatch job zincirini
    /// kurmamali, foto zaten gelmis demektir.
    /// </returns>
    Task<PhotoSendOutcome> SendPhotoMessageAsync(
        int tenantId,
        int leadId,
        string phone,
        string templateSlug,
        bool updateStatusToRequested,
        CancellationToken ct);

    /// <summary>
    /// PhotoEscalationJob 48 saat sonrasi koordinator devri icin status
    /// flip + audit. Mesaj gondermez (escalation icin koordinator panele
    /// bakar; mesaj gonderim varyanti template_slug='photo_escalation' ile
    /// <see cref="SendPhotoMessageAsync"/>'a delege edilir, bu metod sadece
    /// status mutation'ini ayri tutar — Reminder/Escalation race condition
    /// kontrol pre-check'i icin terminal-mark anchor olarak da kullanilir).
    ///
    /// Race-safe semantik: yalnizca <see cref="PhotoLifecycleStage.NonTerminal"/>
    /// durumdaki row'lari mutate eder. Terminal state'ler (received,
    /// escalated, rejected) UPDATE WHERE clause'unda elendigi icin dispatch
    /// 'received'i 'requested'a flip etmez (Codex iter 0 CQ9 fix).
    /// Returns: affected row count (0 = lead missing OR terminal state).
    /// </summary>
    Task<int> MarkPhotoStatusAsync(
        int tenantId,
        int leadId,
        string newStatusDbValue,
        PhotoLifecycleStage allowedFromStage,
        CancellationToken ct);

    /// <summary>
    /// Pre-check: Reminder/Escalation job execute time'da DB lookup —
    /// FollowupStageJob terminal-skip pattern. (current_status, current_count)
    /// doner. NULL response = lead silinmis (cascade veya manuel delete).
    /// </summary>
    Task<PhotoLeadState?> GetLeadStateAsync(int tenantId, int leadId, CancellationToken ct);
}

/// <summary>
/// Pre-check snapshot. PhotoRequestedAt KASITLI OLARAK CIKARTILDI (Codex iter 2
/// CQ9 fix): leads.updated_at semantik olarak "request gonderildi" zamanini
/// degil, herhangi bir UPDATE'in zamanini tasidigi icin 24h reminder clock'unu
/// distort ederdi. Reminder/Escalation timing zaten BackgroundJob.Schedule
/// delay'i ile garantilenir (Hangfire scheduled_at sutunundan), DB-side
/// timestamp ihtiyaci yok. Foto akisi icin gercek bir 'photo_requested_at'
/// sutunu eklemek post-pilot scope (reminder_sent_at, escalated_at ile
/// birlikte audit timeline icin).
/// </summary>
public sealed class PhotoLeadState
{
    public required string PhotoStatus { get; init; }
    public required int PhotoCount { get; init; }
    public required string Phone { get; init; }
}

/// <summary>
/// SendPhotoMessageAsync return shape. Caller (Hangfire job) flip-skip
/// race-loss kararini buradan okur: <c>Sent=true</c> + <c>StatusFlipApplied=false</c>
/// durumu Dispatch job'un Reminder enqueue zincirini abort etmesi gerektigi
/// sinyalidir (concurrent inbound 'received' yapmis, foto zaten gelmis;
/// reminder mantiksiz).
/// </summary>
public readonly record struct PhotoSendOutcome(
    bool Sent,
    bool StatusFlipApplied)
{
    public bool RaceTerminalDetected => Sent && !StatusFlipApplied;
}

/// <summary>
/// Race-safe lifecycle stage filter for <see cref="IPhotoRequestService.MarkPhotoStatusAsync"/>.
/// Codex iter 0 CQ9 finding: MarkPhotoStatusAsync must not overwrite terminal
/// state'leri (received/escalated/rejected) cunku PhotoInboundHandler concurrent
/// olarak 'received' set ettiyse Reminder/Dispatch flip etmemeli.
///
/// * <see cref="NonTerminal"/> = sadece 'none','requested' state'leri update edilir
///   (escalated dahil tum terminal state korunur). Dispatch/Reminder bu mode'u
///   kullanir.
/// * <see cref="NonTerminalIncludingRequested"/> = ayni semantik (Reminder de
///   sadece status flip yapmadan reminder mesaji yollar; flip skip).
/// * <see cref="EscalationFromRequested"/> = sadece 'requested' state -> 'escalated'.
///   Escalation job bu mode'u kullanir; 'received' state'i ASLA 'escalated'
///   yapmaz.
/// </summary>
public enum PhotoLifecycleStage
{
    /// <summary>Sadece 'none' veya 'requested' state'inden flip. Tum terminal
    /// state'ler (received, escalated, rejected) korunur. Dispatch path.</summary>
    NonTerminal = 0,

    /// <summary>Yalnizca 'requested' state -> hedef state. Escalation icin
    /// kullanilir; 'received' state'i (foto geldi) escalation'a flip
    /// edilmesini engeller.</summary>
    EscalationFromRequested = 1
}
