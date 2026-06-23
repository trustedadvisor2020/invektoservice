using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Photos;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.DTOs.Integration;
using Chatinbox.Shared.Integration;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Automation.Services.Photos;

/// <summary>
/// FEAT-PHOTO Automation dispatch yardimcisi. INMA chatoperation tek kopru
/// (/api/v1/callback/wapcrm Backend bridge) uzerinden mesaj gonderir +
/// leads.photo_status state-machine guncellemesi yapar.
///
/// Bridge limit pilot:
///   * /api/v1/callback/wapcrm Backend bridge messageType=1 (text-only)
///     destekliyor; gercek media attachment expansion gerektiriyor. Pilotta
///     dispatch text-only + variant A icindeki sablon image PUBLIC URL
///     referansiyla cozuldu (DentAdavista/seeds/photo-request-templates.json).
///   * Bu service template body'sini DB'den (template_catalog) cekmez —
///     pilotta seed JSON dogrudan handler tarafinda runtime resolve edilir.
///     Post-pilot: template_catalog uzerinden lookup; bridge expansion
///     ayri paket.
///
/// INMA chatoperation API tek-kopru disiplini: outbound asla baska bir
/// endpoint'e gitmez (lessons-learned 2026-04-14: yeni outbound parametre
/// her zaman Backend bridge'inde +10 satirla eklenir, paralel hop yok).
/// </summary>
public sealed class PhotoRequestService : IPhotoRequestService
{
    private readonly PostgresConnectionFactory _db;
    private readonly MainAppCallbackClient _callbackClient;
    private readonly JsonLinesLogger _log;

    /// <summary>
    /// Pilot: tek-tenant scope = 18173130 (Dent). Template body'leri inline
    /// constant olarak tutuldu (seed JSON'in runtime mirror'i). Multi-tenant
    /// expansion + DB-driven template lookup post-pilot — bkz. tracking.md
    /// non-goals.
    ///
    /// Variant A: sablon image URL referansi (5 ikon: on/sag/sol/ust/alt
    /// + ok). Pilotta image public host'lanir (DentAdavista CDN veya
    /// Invekto static), URL link mesaja gomulu.
    /// Variant B: yazili 5 aci (sablon yok); DocX 'Mesaj 2' tonu.
    /// 24h reminder: nazik hatirlatma; DocX 5 varyant icinden 1.
    /// 48h escalation: koordinator devri tonu; DocX 5 varyant icinden 1.
    /// </summary>
    private static readonly Dictionary<string, string> PilotTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["photo_request_a"] =
            "Merhaba! Dr. Ozge sizin icin ozel bir tedavi plani hazirlasin diye 5 fotoya ihtiyacimiz var. " +
            "Asagidaki kilavuza gore 5 acidan (on, sag, sol, ust, alt) cekebilir misiniz? " +
            "Tek seferlik ve kisisel olarak inceleyecegiz: https://invekto.com/static/dental-angles.png " +
            "Sorun yasarsaniz buradan yazin, yardimci olalim.",

        ["photo_request_b"] =
            "Merhaba! Gorusmemiz icin 5 fotoya ihtiyacimiz var. Sirasiyla:\n" +
            "1) On disler (gulumseyerek, agziniz hafif acik)\n" +
            "2) Sag yan dis profili\n" +
            "3) Sol yan dis profili\n" +
            "4) Alttan ust dis kemeri (basinizi geriye yatirip)\n" +
            "5) Ustten alt dis kemeri (cenenizi gogsunuze yaklastirip)\n" +
            "Telefonunuzun on kamerasi yeterli — net olsun yeter. Tesekkurler!",

        ["photo_reminder_24h"] =
            "Selam! Dunku istegimizi unutmus olabilirsiniz — Dr. Ozge size en uygun planu hazirlamak icin " +
            "5 fotoyu bekliyor. 1 dakikalik bir is :) Sorun varsa yazin, baska turlu de cozum bulalim.",

        ["photo_escalation"] =
            "Merhaba! Sizin icin koordinatorumuz devreye giriyor; ekipten biri kisa bir telefon konusmasi " +
            "icin sizinle iletisime gececek. Foto cekmekte zorlandiysaniz hic problem degil, birlikte halledelim."
    };

    public PhotoRequestService(
        PostgresConnectionFactory db,
        MainAppCallbackClient callbackClient,
        JsonLinesLogger log)
    {
        _db = db;
        _callbackClient = callbackClient;
        _log = log;
    }

    public async Task<PhotoSendOutcome> SendPhotoMessageAsync(
        int tenantId,
        int leadId,
        string phone,
        string templateSlug,
        bool updateStatusToRequested,
        CancellationToken ct)
    {
        if (!PilotTemplates.TryGetValue(templateSlug, out var messageText))
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestService: unknown template_slug='{templateSlug}' tenant={tenantId} lead={leadId} — skipping.");
            throw new InvalidOperationException(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] Unknown photo template_slug: {templateSlug}");
        }

        // INMA chatoperation tek-kopru: Backend /api/v1/callback/wapcrm
        // bridge'i. messageType=1 (text). Image attachment yok (bridge
        // limit; sablon image URL referansi text icinde).
        var callback = new OutgoingCallback
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Action = CallbackActions.SendMessage,
            TenantId = tenantId,
            ChatId = $"{phone}@c.us",
            SequenceId = leadId,
            Data = new CallbackData
            {
                MessageText = messageText,
                Phone = phone,
                // FEAT-J2: foto akisi transactional event sayilir (HSM gerek
                // duymayan customer-care window aktif — slot picker'da hasta
                // cevap verdi). MessageCategory=null leaving INMA opt-out
                // bypass'i hosgorulur (Q4 verification).
                MessageCategory = null
            },
            Timestamp = DateTime.UtcNow
        };

        var sent = await _callbackClient.SendCallbackAsync(callback, callbackUrl: null, ct).ConfigureAwait(false);
        if (!sent)
        {
            // Caller (Hangfire job) retry semantigi karar verir — exception
            // throw etmek INV-AT-075 retry tetikler; success false donusunde
            // throw'la sinyali yukseltmek gerek.
            throw new HttpRequestException(
                $"[{ErrorCodes.PhotoRequestDispatchInmaFailed}] PhotoRequestService: callback delivery failed tenant={tenantId} lead={leadId} template={templateSlug}");
        }

        var statusFlipApplied = true;
        if (updateStatusToRequested)
        {
            // Race-safe: yalnizca non-terminal state'lerden 'requested'a flip.
            // 'received'/'escalated' uzerinde override YOK (Codex CQ9 fix).
            var rows = await MarkPhotoStatusAsync(
                tenantId, leadId, PhotoStatusExtensions.DbValueRequested,
                PhotoLifecycleStage.NonTerminal, ct).ConfigureAwait(false);
            if (rows == 0)
            {
                // Mesaj gonderildi ama status flip atildi (concurrent inbound
                // 'received' yapmis olabilir veya lead silinmis). Caller
                // (Dispatch job) PhotoSendOutcome.RaceTerminalDetected sinyali
                // ile Reminder enqueue zincirini abort eder (Codex iter 1
                // CQ1/CQ2 fix — flip-skip caller'a gorunmez kaliyordu).
                statusFlipApplied = false;
                _log.SystemWarn(
                    $"[{ErrorCodes.PhotoRequestReminderRaceTerminal}] PhotoRequestService: status flip skipped (race terminal or lead missing) tenant={tenantId} lead={leadId} template={templateSlug} — caller will abort downstream chain.");
            }
        }

        _log.SystemInfo(
            $"PhotoRequestService: sent ok tenant={tenantId} lead={leadId} template={templateSlug} status_update={updateStatusToRequested} flip_applied={statusFlipApplied}");

        return new PhotoSendOutcome(Sent: true, StatusFlipApplied: statusFlipApplied);
    }

    public async Task<int> MarkPhotoStatusAsync(
        int tenantId,
        int leadId,
        string newStatusDbValue,
        PhotoLifecycleStage allowedFromStage,
        CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        // Race guard: hangi from-state'den flip izin verildigini parametrize
        // edilmis array ile belirler (Codex iter 1 CQ5 fix — SQL string
        // interpolation YASAK, parametrize edilmis ANY(@allowed_states)
        // pattern'i kullanildi). Enum-controlled set olsa da rule policy
        // bu: SQL CommandText icinde interpolation hicbir kosulda yok.
        //
        // NonTerminal: 'none','requested' (received/escalated/rejected
        // korunur — dispatch flip yapmaz).
        // EscalationFromRequested: 'requested' (received geldi ise
        // escalation flip ATLANIR; idempotent re-run safe).
        var allowedStates = allowedFromStage switch
        {
            PhotoLifecycleStage.NonTerminal              => new[] { "none", "requested" },
            PhotoLifecycleStage.EscalationFromRequested  => new[] { "requested" },
            _ => new[] { "none", "requested" }
        };

        cmd.CommandText = @"
            UPDATE leads
               SET photo_status = @status,
                   updated_at   = NOW()
             WHERE id        = @lid
               AND tenant_id = @tid
               AND photo_status = ANY(@allowed_states)";
        cmd.Parameters.AddWithValue("status", newStatusDbValue);
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.Add(new NpgsqlParameter("allowed_states", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = allowedStates
        });
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<PhotoLeadState?> GetLeadStateAsync(int tenantId, int leadId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        // Codex iter 2 CQ9 fix: updated_at -> PhotoRequestedAt mapping kaldirildi.
        // Reminder/Escalation timing Hangfire BackgroundJob.Schedule delay'i ile
        // garantilenir (scheduled_at sutunundan); DB-side photo_requested_at
        // sutunu post-pilot scope.
        cmd.CommandText = @"
            SELECT photo_status, photo_count, phone
              FROM leads
             WHERE id = @lid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return new PhotoLeadState
        {
            PhotoStatus = reader.GetString(0),
            PhotoCount  = reader.GetInt32(1),
            Phone       = reader.GetString(2)
        };
    }
}
