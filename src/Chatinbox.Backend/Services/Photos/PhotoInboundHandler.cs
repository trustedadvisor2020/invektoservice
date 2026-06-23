using System.Security.Cryptography;
using System.Text;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Photos;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Backend.Services.Photos;

/// <summary>
/// FEAT-PHOTO inbound media handler. INMA WhatsApp at-least-once redelivery
/// disiplini icin uc katmanli idempotency:
///
///   1. Tenant guard: leads.tenant_id == caller tenantId pre-check. Mismatch
///      = INV-AT-083 + early-return; idempotency INSERT YAPILMAZ. Bu ilk
///      adim cross-tenant pollution guard'i (Codex iter 0 CQ9 feedback —
///      idempotency tablosu lead_id'ye bagli, tenant validation onceden
///      yapilmali).
///   2. <c>photo_inbound_idempotency</c> UNIQUE (lead_id, media_url_hash)
///      -> INSERT ON CONFLICT DO NOTHING. UNIQUE violation = duplicate
///      redelivery, swallow + INV-AT-073 log + atomic UPDATE skip.
///   3. Atomic UPDATE leads SET photo_status='received',
///      photo_count = photo_count + 1, photo_received_at = COALESCE(...)
///      WHERE id = @id AND tenant_id = @tid AND photo_status &lt;&gt; 'rejected'.
///      RETURNING photo_count. RowsAffected=0 = INV-AT-084 update miss
///      (lead silinmis VEYA 'rejected' state); caller 422 doner.
///
/// Q1 verification path:
///   1. INMA event A geldi -> tenant guard PASS -> idempotency INSERT PASS
///      -> UPDATE WHERE photo_status<>'rejected' rowsAffected=1 -> count=1,
///      FirstReceive=true.
///   2. INMA event A retry geldi (ayni media_url) -> tenant guard PASS ->
///      idempotency INSERT UNIQUE violation -> duplicate path -> UPDATE
///      skip -> count=1, IsDuplicate=true.
///   3. INMA event B (yeni foto, baska URL) -> idempotency PASS -> UPDATE
///      WHERE photo_status<>'rejected' guard'i sayesinde count=2 dogru
///      artar (2. ve sonraki fotolar).
///   4. Cross-tenant attacker leadId guess: tenant guard reddeder, idempotency
///      tablosu kirletilmez (Codex CQ9 fix).
///
/// AC3 atomicity: SQL tek statement; PostgreSQL row-level lock otomatik.
/// Iki concurrent UPDATE serialize olur, count yarisi yasanmaz.
/// </summary>
public sealed class PhotoInboundHandler : IPhotoInboundHandler
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _log;

    public PhotoInboundHandler(PostgresConnectionFactory db, JsonLinesLogger log)
    {
        _db = db;
        _log = log;
    }

    public async Task<PhotoInboundOutcome> HandleInboundMediaAsync(
        int tenantId,
        int leadId,
        string mediaUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            // Caller (InmaInboundMediaEndpoint) zaten payload validation
            // yapiyor; bu defensive guard caller bug + INV-AT-081 sinifina
            // dustugu hali yansitir, INV-AT-078 DB error degil.
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundInvalidPayload}] PhotoInboundHandler: empty mediaUrl tenant={tenantId} lead={leadId} — caller bug, skipping (no idempotency write).");
            return new PhotoInboundOutcome(
                PhotoInboundResultKind.UpdateMiss, false, 0, PhotoStatus.None);
        }

        var hash = ComputeSha256Hex(mediaUrl);

        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);

            // §1. Tenant guard — leads.tenant_id == @tid pre-check.
            // Idempotency tablosu lead_id'ye bagli; cross-tenant attacker
            // baska tenant'in lead_id'sini guess ederse onceden reddet,
            // photo_inbound_idempotency satirini KIRLETME.
            int? leadTenantId = null;
            string? currentStatusStr = null;
            await using (var lookup = conn.CreateCommand())
            {
                lookup.CommandText =
                    "SELECT tenant_id, photo_status FROM leads WHERE id = @lid LIMIT 1";
                lookup.Parameters.AddWithValue("lid", leadId);
                await using var lookupReader = await lookup.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await lookupReader.ReadAsync(ct).ConfigureAwait(false))
                {
                    leadTenantId = lookupReader.GetInt32(0);
                    currentStatusStr = lookupReader.IsDBNull(1) ? null : lookupReader.GetString(1);
                }
            }

            if (leadTenantId is null)
            {
                // Lead missing — caller resolve fail veya cascade silindi.
                // Update miss class altinda topla, idempotency INSERT yok.
                _log.SystemWarn(
                    $"[{ErrorCodes.PhotoInboundUpdateMiss}] PhotoInboundHandler: lead missing tenant={tenantId} lead={leadId} — caller resolve fail or cascade delete.");
                return new PhotoInboundOutcome(
                    PhotoInboundResultKind.UpdateMiss, false, 0, PhotoStatus.None);
            }

            if (leadTenantId.Value != tenantId)
            {
                // Cross-tenant pollution attempt VEYA caller bug.
                _log.SystemWarn(
                    $"[{ErrorCodes.PhotoInboundLeadMismatch}] PhotoInboundHandler: tenant mismatch caller_tenant={tenantId} lead={leadId} lead_tenant={leadTenantId.Value} — idempotency NOT written.");
                return new PhotoInboundOutcome(
                    PhotoInboundResultKind.TenantMismatch, false, 0, PhotoStatus.None);
            }

            // §1b. Drift sinyali: photo_status DB string CHECK constraint
            // disinda bir deger gosterirse log (defensive — migration 034
            // CHECK constraint silinmedikce tetiklenmez).
            if (currentStatusStr is not null)
            {
                _ = PhotoStatusExtensions.FromDbValue(currentStatusStr, out var recognized);
                if (!recognized)
                {
                    _log.SystemWarn(
                        $"[{ErrorCodes.PhotoInboundHandlerDbError}] PhotoInboundHandler: unknown photo_status='{currentStatusStr}' tenant={tenantId} lead={leadId} (schema drift?). Falling back to None semantics.");
                }
            }

            // §2. Idempotency anchor INSERT. UNIQUE violation = duplicate.
            await using (var insIdem = conn.CreateCommand())
            {
                insIdem.CommandText = @"
                    INSERT INTO photo_inbound_idempotency (lead_id, media_url_hash)
                    VALUES (@lid, @hash)
                    ON CONFLICT (lead_id, media_url_hash) DO NOTHING
                    RETURNING id";
                insIdem.Parameters.AddWithValue("lid", leadId);
                insIdem.Parameters.AddWithValue("hash", hash);

                var idemResult = await insIdem.ExecuteScalarAsync(ct).ConfigureAwait(false);
                var inserted = idemResult is long;
                if (!inserted)
                {
                    // Duplicate redelivery — INV-AT-073 swallow + healthy log.
                    var currentCount = await ReadPhotoCountAsync(conn, tenantId, leadId, ct).ConfigureAwait(false);
                    _log.SystemInfo(
                        $"[{ErrorCodes.PhotoInboundIdempotencyDuplicate}] PhotoInboundHandler: " +
                        $"duplicate redelivery dropped tenant={tenantId} lead={leadId} hash={hash[..12]} count={currentCount}");
                    return new PhotoInboundOutcome(
                        PhotoInboundResultKind.Duplicate,
                        FirstReceive: false,
                        PhotoCount: currentCount,
                        Status: PhotoStatus.Received);
                }
            }

            // §3. Atomic UPDATE leads. WHERE clause iki invariant:
            //   * id = @lid AND tenant_id = @tid (defense-in-depth; §1
            //     tenant guard zaten yapildi ama UPDATE'te bir kez daha
            //     sabitlenmesi DB-side row-level lock'in dogru row'a
            //     denk gelmesini garanti eder).
            //   * photo_status NOT IN ('rejected') — rejected ise akisi
            //     yeniden tetiklemiyoruz (post-pilot opt-out). 'received'
            //     uzerine yeni foto count'u artirilir; WHERE photo_status
            //     <> 'rejected' bu nuance'i kapsar.
            // FirstReceive sinyali count=1 ise true.
            await using (var upd = conn.CreateCommand())
            {
                upd.CommandText = @"
                    UPDATE leads
                       SET photo_status      = 'received',
                           photo_count       = photo_count + 1,
                           photo_received_at = COALESCE(photo_received_at, NOW()),
                           updated_at        = NOW()
                     WHERE id        = @lid
                       AND tenant_id = @tid
                       AND photo_status <> 'rejected'
                    RETURNING photo_count";
                upd.Parameters.AddWithValue("lid", leadId);
                upd.Parameters.AddWithValue("tid", tenantId);

                await using var reader = await upd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    // RowsAffected=0: photo_status='rejected' state lock'lu
                    // (yeniden tetiklemiyoruz). Idempotency row INSERT edildi
                    // ama UPDATE etkisiz — caller'a UpdateMiss outcome donulur.
                    // Caller (InmaInboundMediaEndpoint) 422 emit eder, INMA
                    // ops'a fail-loud sinyal verir.
                    _log.SystemWarn(
                        $"[{ErrorCodes.PhotoInboundUpdateMiss}] PhotoInboundHandler: " +
                        $"UPDATE leads matched 0 rows tenant={tenantId} lead={leadId} (rejected state?) — idempotency row written but lifecycle unchanged.");
                    return new PhotoInboundOutcome(
                        PhotoInboundResultKind.UpdateMiss, false, 0, PhotoStatus.None);
                }

                var newCount = reader.GetInt32(0);
                var firstReceive = newCount == 1;

                _log.SystemInfo(
                    $"PhotoInboundHandler: ok tenant={tenantId} lead={leadId} count={newCount} first={firstReceive} hash={hash[..12]}");

                return new PhotoInboundOutcome(
                    PhotoInboundResultKind.Success,
                    FirstReceive: firstReceive,
                    PhotoCount: newCount,
                    Status: PhotoStatus.Received);
            }
        }
        catch (NpgsqlException ex)
        {
            _log.SystemError(
                $"[{ErrorCodes.PhotoInboundHandlerDbError}] PhotoInboundHandler: DB fail tenant={tenantId} lead={leadId}: {ex.Message}");
            // INMA at-least-once: webhook layer 503 doner, bir sonraki
            // redelivery handler'a yeniden girer. Idempotency anchor henuz
            // yazilmadigi icin (tenant guard once veya idempotency INSERT
            // failure) duplicate path tetiklenmez — yeniden gercekten
            // ilk-attempt.
            throw;
        }
        catch (OperationCanceledException)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.PhotoInboundCancelled}] PhotoInboundHandler: cancelled tenant={tenantId} lead={leadId}");
            throw;
        }
    }

    /// <summary>
    /// Tenant-scoped photo_count read. UNIQUE(tenant_id, phone) PK + tenant_id
    /// guard birlikte cross-tenant scan'i engeller (Codex iter 0 CQ9 fix).
    /// </summary>
    private static async Task<int> ReadPhotoCountAsync(
        NpgsqlConnection conn, int tenantId, int leadId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT photo_count FROM leads WHERE id = @lid AND tenant_id = @tid LIMIT 1";
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is int n ? n : 0;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
