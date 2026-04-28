using System.Text.RegularExpressions;
using Invekto.Backend.Services.Photos;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma.Webhooks;
using Invekto.Shared.Contracts.Photos;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Endpoints;

/// <summary>
/// FEAT-PHOTO: INMA WhatsApp inbound media event ingest -> PhotoInboundHandler
/// adapter. Mevcut /api/v1/webhook/event Backend route'unun (Program.cs'de)
/// hem text hem media event'leri kabul ettigini biliyoruz; bu endpoint o
/// route'a alternatif degil ek bir media-specific surface acmaktan ziyade
/// PhotoInboundHandler'i wire edecek extension method seklinde tasarlandi.
///
/// Wiring strategy (PROGRAM.cs out of scope per plan allowed_files):
///   * Program.cs son satir Map.* zincirine `app.MapInmaInboundMediaEndpoint()`
///     eklenmesi gerek. Bu commit'te uygulamadik (scope discipline);
///     post-pilot 1-line wiring patch separate paket — bkz. tracking.md
///     "Deploy blocker" section.
///
/// Route shape:
///   POST /api/v1/inma/inbound/media
///   Auth: localhost or X-Internal-Api-Key (callback bridge ile ayni gate)
///   Body: { tenant_id, lead_id, image_url } (Backend Program.cs ana webhook
///         route'undan resolve sonrasi forward edilir)
///   Returns 200 (handler dedup PASS / duplicate), 503 (DB error → INMA retry)
///
/// Bu surface'i ana webhook ingest layer'in cagirmasi yoluyla, mevcut
/// IncomingWebhookEvent + chatId -> phone -> tenant_id resolve mantigi
/// degismez; sadece imageUrl varsa handler hop'u eklenir. Test layer
/// endpoint'i HTTP'den dogrudan curl edebilir; ana akis kompozisyondan
/// zarar gormez.
/// </summary>
public static class InmaInboundMediaEndpoint
{
    private const string RoutePath = "/api/v1/inma/inbound/media";

    public static void MapInmaInboundMediaEndpoint(this WebApplication app, string? internalApiKey)
    {
        app.MapPost(RoutePath, async (
            HttpContext ctx,
            IPhotoInboundHandler handler,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            // §1. Auth: localhost veya internal api key (Program.cs'deki
            // /api/v1/callback/wapcrm bridge ile ayni gate semantici).
            var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
            var apiKey = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            var isLocal = remoteIp is "127.0.0.1" or "::1";
            var hasKey = !string.IsNullOrEmpty(internalApiKey) && apiKey == internalApiKey;
            if (!isLocal && !hasKey)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthUnauthorized}] InmaInboundMedia: rejected unauthorized request (ip={remoteIp})");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        "Yetkisiz istek (yalnizca localhost veya internal API key).", requestId),
                    statusCode: 401);
            }

            InmaInboundMediaRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<InmaInboundMediaRequest>(ctx.RequestAborted);
            }
            catch (System.Text.Json.JsonException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.PhotoInboundInvalidPayload}] InmaInboundMedia: invalid JSON body: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.PhotoInboundInvalidPayload,
                        "Foto webhook gövdesi gecerli JSON degil.", requestId),
                    statusCode: 400);
            }

            if (body is null
                || body.TenantId <= 0
                || body.LeadId <= 0
                || string.IsNullOrWhiteSpace(body.ImageUrl))
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.PhotoInboundInvalidPayload}] InmaInboundMedia: invalid payload tenant={body?.TenantId} lead={body?.LeadId} url={(string.IsNullOrEmpty(body?.ImageUrl) ? "(empty)" : "(present)")}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.PhotoInboundInvalidPayload,
                        "tenant_id, lead_id ve image_url alanlari zorunlu.", requestId),
                    statusCode: 400);
            }

            try
            {
                var outcome = await handler.HandleInboundMediaAsync(
                    body.TenantId, body.LeadId, body.ImageUrl, ctx.RequestAborted);

                // Handler outcome -> HTTP response mapping. Success/Duplicate
                // = 200 (INMA at-least-once'in normal jeti); TenantMismatch =
                // 422 INV-AT-083; UpdateMiss = 422 INV-AT-084. Frontend
                // (sadece INMA caller) success-shape'i parse eder.
                if (outcome.IsTenantMismatch)
                {
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.PhotoInboundLeadMismatch,
                            "Foto webhook tenant uyusmuyor.", requestId),
                        statusCode: 422);
                }

                if (outcome.IsUpdateMiss)
                {
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.PhotoInboundUpdateMiss,
                            "Foto kaydedildi ama lead durumu guncel degil.", requestId),
                        statusCode: 422);
                }

                return Results.Json(new
                {
                    status = "ok",
                    first_receive = outcome.FirstReceive,
                    duplicate = outcome.IsDuplicate,
                    photo_count = outcome.PhotoCount,
                    photo_status = outcome.Status.ToDbValue()
                });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemError(
                    $"[{ErrorCodes.PhotoInboundHandlerDbError}] InmaInboundMedia: DB transient tenant={body.TenantId} lead={body.LeadId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.PhotoInboundHandlerDbError,
                        "Foto islenirken veritabani hatasi olustu; INMA tekrar deneyecek.", requestId),
                    statusCode: 503);
            }
            catch (OperationCanceledException)
            {
                // 408 Request Timeout — standard HTTP status for client-cancelled
                // / timed-out requests (Codex iter 1 CQ12: 499 was nginx-only,
                // not in the project status-code set).
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.PhotoInboundCancelled}] InmaInboundMedia: cancelled tenant={body.TenantId} lead={body.LeadId} — INMA at-least-once will redeliver.");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.PhotoInboundCancelled,
                        "Foto webhook isleme iptal edildi; INMA at-least-once tekrar gonderecek.", requestId),
                    statusCode: 408);
            }
        });
    }

    /// <summary>
    /// Helper: mevcut /api/v1/webhook/event route'unda bir mesajin media event
    /// olup olmadigini ayirir. Ana webhook handler'i `WebhookMessage` icin
    /// `imageUrl` doluysa bu helper'i cagirip <see cref="MapInmaInboundMediaEndpoint"/>
    /// surface'inden yuklemek yerine direkt handler'a teslim edebilir
    /// (post-pilot wiring kararina bagli, su an her iki path destekli).
    /// </summary>
    public static bool IsMediaMessage(WebhookMessage message)
    {
        // INMA contract: type=='image' veya imageUrl non-null+non-empty.
        if (string.Equals(message.Type, "image", StringComparison.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrWhiteSpace(message.ImageUrl);
    }

    /// <summary>
    /// chatId "905xxxxxxxxxxx@c.us" -> "905xxxxxxxxxxx" phone normalize.
    /// Program.cs callback/wapcrm bridge'in ayni regex'i (line 7643) — burada
    /// duplicate yapildi cunku PhotoEndpoints crosss tenant guard'i + ana
    /// webhook ingest path'inin ayni helper'a ihtiyaci var. Refactor: shared
    /// utility post-pilot.
    /// </summary>
    public static string? ExtractPhoneFromChatId(string? chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId)) return null;
        var match = Regex.Match(chatId, @"(\d+)@");
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>
/// Inbound media POST body. Caller (mevcut /api/v1/webhook/event ana ingest
/// surface'i veya kanal-disi callback) bu shape'i kullanir.
/// </summary>
public sealed class InmaInboundMediaRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("lead_id")]
    public int LeadId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }
}
