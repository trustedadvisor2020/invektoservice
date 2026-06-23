using System.Text.Json;
using Chatinbox.Backend.Services;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Followup;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Backend.Endpoints;

/// <summary>
/// FEAT-EFS Backend SPA-facing endpoints — proxy in front of Marketing's
/// /api/v1/followup/* routes. Tenants edit/view sequences here without needing direct
/// network access to Marketing (:7112).
///
///   GET /api/v1/tenant-settings/followup-sequence  → list sequences
///   PUT /api/v1/tenant-settings/followup-sequence  → upsert sequence
///   GET /api/v1/tenant/followup/runs               → list recent runs
///
/// Auth: covered by jwtRequiredPrefixes ("/api/v1/tenant-settings/" + "/api/v1/tenant/").
/// Handler reads ctx.Items["TenantContext"] cast to TenantContext (NEVER scalar
/// "TenantId" — lessons 2026-04-21).
/// NO route-level .RequireAuthorization() — Backend uses custom JWT middleware, not
/// .NET native AddAuthentication scheme; calling RequireAuthorization() throws
/// "No authenticationScheme registered" at request time → 500 instead of 401.
/// </summary>
public static class TenantFollowupSequenceEndpoints
{
    private static readonly JsonSerializerOptions WireJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static void MapTenantFollowupSequenceEndpoints(this WebApplication app)
    {
        // GET — list sequences for the tenant
        app.MapGet("/api/v1/tenant-settings/followup-sequence", async (
            HttpContext ctx,
            MarketingFollowupProxyClient proxy,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            var result = await proxy.ListSequencesAsync(tc.TenantId, requestId, ctx.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsOk)
            {
                // Same forward semantics as PUT (lessons 2026-04-21 Codex iter 2 CQ12):
                // 4xx → forward Marketing's body verbatim (preserves upstream INV-MK-*),
                // 5xx → INV-MK-057 + 502.
                if (result.StatusCode is >= 400 and < 500)
                {
                    jsonLog.StepWarn(
                        $"[forward] GET /api/v1/tenant-settings/followup-sequence upstream {result.StatusCode} forwarded (tenant={tc.TenantId})",
                        requestId);
                    return Results.Content(
                        result.ErrorBody ?? string.Empty,
                        contentType: "application/json",
                        statusCode: result.StatusCode);
                }
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] GET /api/v1/tenant-settings/followup-sequence upstream HTTP {result.StatusCode} (tenant={tc.TenantId})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupUpstreamUnavailable,
                        $"Marketing servisi gecici kullanilamiyor (HTTP {result.StatusCode}). Birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 502);
            }

            // Forward envelope shape verbatim so SPA reads test_mode + no_reply_threshold_days
            // (Codex iter 0 CQ10 fix — frontend was hardcoding testMode=false).
            return Results.Json(new
            {
                data = result.Value!.Data,
                test_mode = result.Value.TestMode,
                no_reply_threshold_days = result.Value.NoReplyThresholdDays
            }, WireJson);
        });

        // PUT — upsert sequence for the tenant
        app.MapPut("/api/v1/tenant-settings/followup-sequence", async (
            HttpContext ctx,
            MarketingFollowupProxyClient proxy,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            FollowupSequenceConfig? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<FollowupSequenceConfig>(WireJson, ctx.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"PUT /api/v1/tenant-settings/followup-sequence cancelled during body read (tenant={tc.TenantId})", requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupSequenceConfigInvalid}] PUT /api/v1/tenant-settings/followup-sequence body parse fail (tenant={tc.TenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "Istek govdesi gecersiz JSON.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "Istek govdesi bos. { slug, stages, ab_split_percent, enabled } sema ile gonderin.",
                        requestId),
                    statusCode: 400);

            var result = await proxy.UpsertSequenceAsync(tc.TenantId, body, requestId, ctx.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsOk)
            {
                // 4xx → Marketing's own validation reject (INV-MK-053 cap, INV-MK-050
                // shape, etc.); forward the raw upstream body so the SPA sees Marketing's
                // canonical error_code rather than a Backend-rewritten code (Codex iter 0
                // CQ12 fix — do NOT mask upstream structured errors).
                // 5xx → upstream transient; map to 502 + INV-MK-057 (distinct from the
                // config-invalid class so ops alerting can distinguish transport
                // problems from tenant configuration errors).
                if (result.StatusCode is >= 400 and < 500)
                {
                    jsonLog.StepWarn(
                        $"[forward] PUT /api/v1/tenant-settings/followup-sequence upstream {result.StatusCode} forwarded (tenant={tc.TenantId})",
                        requestId);
                    return Results.Content(
                        result.ErrorBody ?? string.Empty,
                        contentType: "application/json",
                        statusCode: result.StatusCode);
                }
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] PUT /api/v1/tenant-settings/followup-sequence upstream HTTP {result.StatusCode} (tenant={tc.TenantId})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupUpstreamUnavailable,
                        $"Marketing servisi gecici kullanilamiyor (HTTP {result.StatusCode}). Birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 502);
            }

            jsonLog.StepInfo(
                $"PUT /api/v1/tenant-settings/followup-sequence ok (tenant={tc.TenantId}, slug='{result.Value!.Slug}', enabled={result.Value.Enabled})",
                requestId);
            return Results.Json(new { data = result.Value }, WireJson);
        });

        // GET — list recent runs for the tenant
        app.MapGet("/api/v1/tenant/followup/runs", async (
            HttpContext ctx,
            MarketingFollowupProxyClient proxy,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            var result = await proxy.ListRecentRunsAsync(tc.TenantId, requestId, ctx.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsOk)
            {
                if (result.StatusCode is >= 400 and < 500)
                {
                    jsonLog.StepWarn(
                        $"[forward] GET /api/v1/tenant/followup/runs upstream {result.StatusCode} forwarded (tenant={tc.TenantId})",
                        requestId);
                    return Results.Content(
                        result.ErrorBody ?? string.Empty,
                        contentType: "application/json",
                        statusCode: result.StatusCode);
                }
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] GET /api/v1/tenant/followup/runs upstream HTTP {result.StatusCode} (tenant={tc.TenantId})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupUpstreamUnavailable,
                        $"Marketing servisi gecici kullanilamiyor (HTTP {result.StatusCode}).",
                        requestId),
                    statusCode: 502);
            }
            return Results.Json(new { data = result.Value }, WireJson);
        });
    }
}
