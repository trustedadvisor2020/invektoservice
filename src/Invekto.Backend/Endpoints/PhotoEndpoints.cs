using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Photos;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Endpoints;

/// <summary>
/// FEAT-PHOTO Dashboard SPA proxy endpoints. Lead detail panelindeki
/// "Fotograflar" sekmesi bu route'lardan beslenir; ayrica koordinator
/// "Tekrar Iste" butonu Photo dispatch job'unu yeniden tetikler.
///
///   GET  /api/v1/leads/{id}/photos          -> durum + thumbnail listesi
///   POST /api/v1/leads/{id}/photos/request  -> manuel re-dispatch
///
/// Auth: Backend custom JWT middleware (jwtRequiredPrefixes "/api/v1/leads/")
/// kapsami; ctx.Items["TenantContext"] uzerinden TenantContext.TenantId
/// alinir. Cross-tenant guard: lead_id PK lookup sonucu leads.tenant_id
/// JWT TenantContext.TenantId ile match olmazsa 403 + INV-AT-080 emit.
///
/// INMA proxy: thumbnail URL'leri INMA'da host edilir, biz persistent
/// storage tutmuyoruz. GET endpoint photo_inbound_idempotency uzerinden
/// "su lead'e gelmis fotolar" timeline'ini doner; URL referansi yerine
/// hash + received_at + lead summary doner. Frontend INMA /api/chats/getimage
/// path'ini kullanir (PhotoTab.tsx tarafinda).
///
/// NOTE: Wiring (app.MapPhotoEndpoints()) Program.cs out of scope per plan
/// allowed_files. Post-pilot 1-line patch separate paket — bkz. tracking.md.
/// </summary>
public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this WebApplication app)
    {
        // GET /api/v1/leads/{id}/photos
        app.MapGet("/api/v1/leads/{leadId:int}/photos", async (
            int leadId,
            HttpContext ctx,
            PostgresConnectionFactory db,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            try
            {
                await using var conn = await db.OpenConnectionAsync(ctx.RequestAborted);

                // §1. Lead lookup + tenant guard (INV-AT-080).
                int leadTenantId;
                string? photoStatusStr;
                int photoCount;
                DateTime? photoReceivedAt;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT tenant_id, photo_status, photo_count, photo_received_at
                          FROM leads
                         WHERE id = @lid";
                    cmd.Parameters.AddWithValue("lid", leadId);
                    await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
                    if (!await reader.ReadAsync(ctx.RequestAborted))
                    {
                        return Results.Json(
                            ErrorResponse.Create(ErrorCodes.LeadNotFound,
                                "Lead bulunamadi.", requestId),
                            statusCode: 404);
                    }
                    leadTenantId   = reader.GetInt32(0);
                    photoStatusStr = reader.IsDBNull(1) ? null : reader.GetString(1);
                    photoCount     = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    photoReceivedAt= reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                }

                if (leadTenantId != tc.TenantId)
                {
                    jsonLog.SystemWarn(
                        $"[{ErrorCodes.PhotoEndpointsCrossTenantBlocked}] PhotoEndpoints GET cross-tenant blocked " +
                        $"requesting_tenant={tc.TenantId} lead={leadId} lead_tenant={leadTenantId} user={tc.UserId}");
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthCrossTenantBlocked,
                            "Bu kayda erisim izniniz yok.", requestId),
                        statusCode: 403);
                }

                // §2. Inbound idempotency rows -> thumbnail timeline.
                // INMA URL'i tutmuyoruz; thumbnail URL'i frontend tarafi INMA
                // proxy ile cozer. Endpoint hash + received_at doner.
                var thumbnails = new List<PhotoThumbnailDto>();
                await using (var listCmd = conn.CreateCommand())
                {
                    listCmd.CommandText = @"
                        SELECT id, media_url_hash, received_at
                          FROM photo_inbound_idempotency
                         WHERE lead_id = @lid
                         ORDER BY received_at DESC
                         LIMIT 50";
                    listCmd.Parameters.AddWithValue("lid", leadId);
                    await using var listReader = await listCmd.ExecuteReaderAsync(ctx.RequestAborted);
                    while (await listReader.ReadAsync(ctx.RequestAborted))
                    {
                        thumbnails.Add(new PhotoThumbnailDto
                        {
                            Id           = listReader.GetInt64(0),
                            MediaUrlHash = listReader.GetString(1),
                            ReceivedAt   = listReader.GetDateTime(2)
                        });
                    }
                }

                return Results.Json(new
                {
                    lead_id        = leadId,
                    tenant_id      = leadTenantId,
                    photo_status   = photoStatusStr ?? PhotoStatusExtensions.DbValueNone,
                    photo_count    = photoCount,
                    photo_received_at = photoReceivedAt,
                    thumbnails
                });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemError(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] PhotoEndpoints GET DB fail tenant={tc.TenantId} lead={leadId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed,
                        "Veritabani gecici kullanilamiyor.", requestId),
                    statusCode: 503);
            }
        });

        // POST /api/v1/leads/{id}/photos/request -> "Tekrar Iste"
        // Bu endpoint Hangfire BackgroundJobClient.Enqueue'yi cagirmaktan
        // kacinmak icin (Backend Hangfire scheduler'a bagli degil; Automation
        // servis ayri Hangfire instance'i isletiyor) Backend->Automation
        // internal HTTP hop yerine basit bir flag UPDATE yapar:
        //   leads.photo_status = 'requested', updated_at = NOW().
        // Automation worker tarafindaki PhotoRequestDispatchJob bu flag'i
        // kullanarak yeni dispatch zinciri kurabilir; alternatif olarak
        // Backend tarafinda /api/internal/photo/dispatch endpoint'i acilip
        // Automation HttpClient ile cagrilabilir (post-pilot wiring).
        //
        // Pilotta basitlik: status'u 'requested'a flip et + log; manual
        // dispatch ileri paketin "operator-trigger" workflow'u.
        app.MapPost("/api/v1/leads/{leadId:int}/photos/request", async (
            int leadId,
            HttpContext ctx,
            PostgresConnectionFactory db,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            try
            {
                await using var conn = await db.OpenConnectionAsync(ctx.RequestAborted);

                int leadTenantId;
                await using (var lookup = conn.CreateCommand())
                {
                    lookup.CommandText = "SELECT tenant_id FROM leads WHERE id = @lid";
                    lookup.Parameters.AddWithValue("lid", leadId);
                    var result = await lookup.ExecuteScalarAsync(ctx.RequestAborted);
                    if (result is not int t)
                    {
                        return Results.Json(
                            ErrorResponse.Create(ErrorCodes.LeadNotFound,
                                "Lead bulunamadi.", requestId),
                            statusCode: 404);
                    }
                    leadTenantId = t;
                }

                if (leadTenantId != tc.TenantId)
                {
                    jsonLog.SystemWarn(
                        $"[{ErrorCodes.PhotoEndpointsCrossTenantBlocked}] PhotoEndpoints POST cross-tenant blocked " +
                        $"requesting_tenant={tc.TenantId} lead={leadId} lead_tenant={leadTenantId} user={tc.UserId}");
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthCrossTenantBlocked,
                            "Bu kayda erisim izniniz yok.", requestId),
                        statusCode: 403);
                }

                // 'received'/'escalated' state'lerinden 'requested'e geri donus
                // istegi bu pilotta destekli — koordinator manual takdir.
                // 'rejected' kalir (post-pilot opt-out, bypass yok).
                await using (var upd = conn.CreateCommand())
                {
                    upd.CommandText = @"
                        UPDATE leads
                           SET photo_status = 'requested',
                               updated_at   = NOW()
                         WHERE id        = @lid
                           AND tenant_id = @tid
                           AND photo_status <> 'rejected'";
                    upd.Parameters.AddWithValue("lid", leadId);
                    upd.Parameters.AddWithValue("tid", tc.TenantId);
                    var rows = await upd.ExecuteNonQueryAsync(ctx.RequestAborted);
                    if (rows == 0)
                    {
                        // 'rejected' state lock'lu — koordinator post-pilot
                        // opt-out karari verirse otomatik flip yok. ErrorResponse
                        // envelope ile (CQ10 fix) standardize et.
                        return Results.Json(
                            ErrorResponse.Create(ErrorCodes.PhotoRequestRejectedLock,
                                "Bu lead reddedilmis durumda; tekrar istek gonderilemez.", requestId),
                            statusCode: 409);
                    }
                }

                jsonLog.SystemInfo(
                    $"PhotoEndpoints: re-request flag set tenant={tc.TenantId} lead={leadId} user={tc.UserId} (Automation dispatch trigger pending — post-pilot)");
                return Results.Json(new
                {
                    status = "requested",
                    lead_id = leadId,
                    tenant_id = tc.TenantId
                });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemError(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] PhotoEndpoints POST DB fail tenant={tc.TenantId} lead={leadId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed,
                        "Veritabani gecici kullanilamiyor.", requestId),
                    statusCode: 503);
            }
        });
    }
}

/// <summary>
/// Dashboard frontend'e dondurulen thumbnail row. Raw URL YOK — sadece hash +
/// timestamp; frontend INMA proxy uzerinden gercek thumbnail'i alir
/// (post-pilot: hash -> URL resolver Backend tarafinda baska bir mekanizma).
/// </summary>
public sealed class PhotoThumbnailDto
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("media_url_hash")]
    public string MediaUrlHash { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("received_at")]
    public DateTime ReceivedAt { get; init; }
}
