using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Invekto.Marketing.Data;
using Invekto.Marketing.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Invekto.Marketing.Endpoints;

/// <summary>
/// FEAT-EFS Marketing endpoints.
///
/// Tenant-scoped (JWT, /api/v1/followup/...):
///   GET  /api/v1/followup/sequences         — list tenant sequences
///   PUT  /api/v1/followup/sequences         — upsert by slug
///   GET  /api/v1/followup/runs              — recent runs (audit)
///
/// Internal (X-Internal-Service-Token + service JWT, /api/internal/followup/...):
///   POST /api/internal/followup/trigger     — Automation → Marketing trigger hop
///
/// Auth notes (lessons 2026-04-21):
///   - Marketing's UseJwtAuth("/api/v1/") prefix only covers /api/v1/. The internal
///     endpoint at /api/internal/ is NOT JWT-gated by middleware here, so the handler
///     enforces both X-Internal-Service-Token (constant-time compare) AND a service-JWT
///     Bearer claim that MUST match the body tenant_id (defense in depth, LIW Chunk B
///     precedent).
///   - Tenant-scoped routes use ctx.Items["TenantContext"] cast to TenantContext (NEVER
///     ctx.Items["TenantId"] scalar — that key is never set by JwtAuthMiddleware).
///   - NO route-level .RequireAuthorization() — Marketing uses custom JWT middleware,
///     not .NET native AddAuthentication scheme; calling RequireAuthorization() throws
///     "No authenticationScheme registered" InvalidOperationException → 500.
/// </summary>
public static class FollowupEndpoints
{
    /// <summary>
    /// Snake-cased serializer for endpoint request/response bodies. Mirrors the repo's
    /// persistence contract so wire JSON and stored JSONB are interchangeable.
    /// </summary>
    private static readonly JsonSerializerOptions WireJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static void MapFollowupEndpoints(this WebApplication app)
    {
        MapListSequences(app);
        MapUpsertSequence(app);
        MapListRuns(app);
        MapInternalTrigger(app);
    }

    // ============================================================
    // GET /api/v1/followup/sequences
    // ============================================================
    private static void MapListSequences(WebApplication app)
    {
        app.MapGet("/api/v1/followup/sequences", async (
            HttpContext ctx,
            FollowupSequenceRepository repo,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                        requestId),
                    statusCode: 401);

            try
            {
                // Parallelizable reads, but sequenced here for simpler error-path mapping.
                var sequences = await repo.ListSequencesAsync(tc.TenantId, ctx.RequestAborted).ConfigureAwait(false);
                var tenantSettings = await repo.GetTenantEfsSettingsAsync(tc.TenantId, ctx.RequestAborted).ConfigureAwait(false);
                // Surface tenant test_mode flag so the Dashboard editor labels units correctly
                // (days vs minutes) — fixes Codex iter 0 CQ10 FAIL (frontend hardcoded testMode=false).
                return Results.Json(new
                {
                    data = sequences,
                    test_mode = tenantSettings.TestMode,
                    no_reply_threshold_days = tenantSettings.NoReplyThresholdDays
                }, WireJson);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"GET /api/v1/followup/sequences cancelled (tenant={tc.TenantId})", requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] GET /api/v1/followup/sequences DB fail (tenant={tc.TenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupStorageUnavailable,
                        "Followup sequence listesi okunamadi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 503);
            }
        });
    }

    // ============================================================
    // PUT /api/v1/followup/sequences
    // ============================================================
    private static void MapUpsertSequence(WebApplication app)
    {
        app.MapPut("/api/v1/followup/sequences", async (
            HttpContext ctx,
            FollowupSequenceRepository repo,
            FollowupSequenceCache cache,
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
                jsonLog.StepInfo($"PUT /api/v1/followup/sequences cancelled during body read (tenant={tc.TenantId})", requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupSequenceConfigInvalid}] PUT /api/v1/followup/sequences body parse fail (tenant={tc.TenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "Istek govdesi gecersiz JSON. Content-Type: application/json gondererek gecerli JSON syntax ile tekrar deneyin.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "Istek govdesi bos. { slug, stages, ab_split_percent, enabled } sema ile gonderin.",
                        requestId),
                    statusCode: 400);

            // Read tenant test_mode for unit-aware validation (same flag the orchestrator uses).
            (bool TestMode, int _ /*ThresholdDays*/) tenantSettings;
            try
            {
                tenantSettings = await repo.GetTenantEfsSettingsAsync(tc.TenantId, ctx.RequestAborted).ConfigureAwait(false);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] PUT /api/v1/followup/sequences DB fail reading tenant settings (tenant={tc.TenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupStorageUnavailable,
                        "Tenant ayarlari okunamadi. Veritabani gecici kullanilamiyor.",
                        requestId),
                    statusCode: 503);
            }

            try
            {
                FollowupSequenceValidator.Validate(body, tenantSettings.TestMode);
            }
            catch (FollowupSequenceValidationException ex)
            {
                jsonLog.StepWarn(
                    $"[{ex.ErrorCode}] PUT /api/v1/followup/sequences validation fail (tenant={tc.TenantId}, slug='{body.Slug}'): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ex.ErrorCode, ex.Message, requestId),
                    statusCode: 400);
            }

            FollowupSequenceConfig saved;
            try
            {
                saved = await repo.UpsertSequenceAsync(tc.TenantId, body, ctx.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"PUT /api/v1/followup/sequences cancelled during upsert (tenant={tc.TenantId})", requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] PUT /api/v1/followup/sequences DB upsert fail (tenant={tc.TenantId}, slug='{body.Slug}'): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupStorageUnavailable,
                        "Followup sequence kaydedilemedi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 503);
            }

            // Local-instance cache invalidate (multi-instance handled by 5dk TTL — BACKLOG B2).
            cache.Invalidate(tc.TenantId, saved.Slug);

            jsonLog.StepInfo(
                $"PUT /api/v1/followup/sequences ok (tenant={tc.TenantId}, slug='{saved.Slug}', stages={saved.Stages.Count}, enabled={saved.Enabled})",
                requestId);

            return Results.Json(new { data = saved }, WireJson);
        });
    }

    // ============================================================
    // GET /api/v1/followup/runs
    // ============================================================
    private static void MapListRuns(WebApplication app)
    {
        app.MapGet("/api/v1/followup/runs", async (
            HttpContext ctx,
            FollowupSequenceRepository repo,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context yok.", requestId),
                    statusCode: 401);

            try
            {
                var rows = await repo.ListRecentRunsForTenantAsync(tc.TenantId, ctx.RequestAborted).ConfigureAwait(false);
                // Project to a wire-friendly anonymous shape so the snake_case serializer
                // emits the conventional field names.
                var data = rows.Select(r => new
                {
                    id = r.Id,
                    sequence_id = r.SequenceId,
                    lead_id = r.LeadId,
                    stage_index = r.StageIndex,
                    ab_group = r.AbGroup,
                    scheduled_at = r.ScheduledAt,
                    status = r.Status
                });
                return Results.Json(new { data }, WireJson);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"GET /api/v1/followup/runs cancelled (tenant={tc.TenantId})", requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] GET /api/v1/followup/runs DB fail (tenant={tc.TenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupStorageUnavailable,
                        "Followup run listesi okunamadi.",
                        requestId),
                    statusCode: 503);
            }
        });
    }

    // ============================================================
    // POST /api/internal/followup/trigger
    // ============================================================
    private static void MapInternalTrigger(WebApplication app)
    {
        app.MapPost("/api/internal/followup/trigger", async (
            HttpContext ctx,
            FollowupOrchestrator orchestrator,
            JwtValidator jwtValidator,
            IConfiguration cfg,
            JsonLinesLogger jsonLog) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            // ---- (a) X-Internal-Service-Token constant-time compare ----
            var configuredSecret = cfg["InternalServices:SharedSecret"] ?? string.Empty;
            if (string.IsNullOrEmpty(configuredSecret))
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthUnauthorized}] /api/internal/followup/trigger: InternalServices:SharedSecret not configured — request rejected (request={requestId}).");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        "Internal endpoint configuration error: shared secret not set on Marketing service. Operator: set InternalServices:SharedSecret in appsettings.Production.json.",
                        requestId),
                    statusCode: 500);
            }

            var presentedToken = ctx.Request.Headers["X-Internal-Service-Token"].FirstOrDefault() ?? string.Empty;
            if (!FixedTimeEquals(configuredSecret, presentedToken))
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthUnauthorized}] /api/internal/followup/trigger: invalid X-Internal-Service-Token (request={requestId}).");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        "Internal service token gecersiz. X-Internal-Service-Token header InternalServices:SharedSecret ile esitlenmeli.",
                        requestId),
                    statusCode: 401);
            }

            // ---- (b) Service JWT Bearer (defense in depth + tenant binding) ----
            var bearer = ctx.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;
            const string bearerPrefix = "Bearer ";
            if (!bearer.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthUnauthorized}] /api/internal/followup/trigger: missing Bearer JWT (request={requestId}).");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        "Bearer service-JWT zorunlu. Caller (Automation) JwtGenerator.GenerateServiceToken ile mint etmeli.",
                        requestId),
                    statusCode: 401);
            }

            var token = bearer[bearerPrefix.Length..].Trim();
            var (validatedContext, validateError) = jwtValidator.ValidateToken(token);
            if (validatedContext is null)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthUnauthorized}] /api/internal/followup/trigger: JWT validation failed (request={requestId}): {validateError ?? "unknown"}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                        $"Service JWT validate edilemedi: {validateError ?? "unknown"}",
                        requestId),
                    statusCode: 401);
            }
            var jwtTenantId = validatedContext.TenantId;

            // ---- (c) Body parse + tenant binding check ----
            FollowupTriggerRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<FollowupTriggerRequest>(WireJson, ctx.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"/api/internal/followup/trigger cancelled during body read (request={requestId})", requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FollowupSequenceConfigInvalid}] /api/internal/followup/trigger body parse fail: {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "Istek govdesi gecersiz JSON.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null || body.TenantId <= 0 || body.LeadId <= 0)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.FollowupSequenceConfigInvalid,
                        "tenant_id ve lead_id zorunlu (>0).",
                        requestId),
                    statusCode: 400);

            if (body.TenantId != jwtTenantId)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.AuthCrossTenantBlocked}] /api/internal/followup/trigger cross-tenant attempt (jwt={jwtTenantId}, body={body.TenantId}, request={requestId}).");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthCrossTenantBlocked,
                        $"Cross-tenant trigger engellendi: JWT tenant={jwtTenantId} body tenant={body.TenantId}.",
                        requestId),
                    statusCode: 403);
            }

            // Default sequence slug per reason (MVP convention — operator names match these).
            var sequenceSlug = body.SequenceSlug ?? DefaultSequenceSlugForReason(body.Reason);

            // ---- (d) Orchestrator dispatch ----
            // Typed catches only (lessons 2026-04-21 Codex iter 0 CQ1/CQ5): NpgsqlException +
            // JsonException + InvalidOperationException cover the orchestrator's declared
            // failure modes (DB transport, malformed stored JSON, invalid UPSERT RETURNING
            // response). Any other exception propagates to the framework's default handler
            // so a genuinely unexpected bug is not silently swallowed.
            FollowupOrchestratorOutcome outcome;
            try
            {
                outcome = await orchestrator.EnqueueAsync(
                    body.TenantId, body.LeadId, sequenceSlug, body.RequestId ?? requestId, ctx.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo($"/api/internal/followup/trigger orchestrator cancelled (request={requestId})", requestId);
                throw;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] /api/internal/followup/trigger DB fail (tenant={body.TenantId}, lead={body.LeadId}, request={requestId}): {ex.Message}");
                return Results.Json(new FollowupTriggerResponse
                {
                    Accepted = false,
                    ErrorCode = ErrorCodes.FollowupStorageUnavailable,
                    ErrorMessage = "Followup veritabani gecici kullanilamiyor. Birkac saniye sonra tekrar deneyin.",
                    RequestId = requestId
                }, WireJson, statusCode: 503);
            }
            catch (JsonException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.FollowupSequenceConfigInvalid}] /api/internal/followup/trigger malformed stored sequence JSONB (tenant={body.TenantId}, request={requestId}): {ex.Message}");
                return Results.Json(new FollowupTriggerResponse
                {
                    Accepted = false,
                    ErrorCode = ErrorCodes.FollowupSequenceConfigInvalid,
                    ErrorMessage = "Followup sequence kaydi bozuk JSON iceriyor. Operator mudahalesi gerekir.",
                    RequestId = requestId
                }, WireJson, statusCode: 500);
            }
            catch (InvalidOperationException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.FollowupStorageUnavailable}] /api/internal/followup/trigger unexpected DB semantic (tenant={body.TenantId}, request={requestId}): {ex.Message}");
                return Results.Json(new FollowupTriggerResponse
                {
                    Accepted = false,
                    ErrorCode = ErrorCodes.FollowupStorageUnavailable,
                    ErrorMessage = "Followup orchestrator beklenmeyen DB durumuyla karsilasti. Log kaydi alindi.",
                    RequestId = requestId
                }, WireJson, statusCode: 500);
            }

            var response = new FollowupTriggerResponse
            {
                Accepted = outcome.Accepted,
                AbGroup = outcome.AbGroup,
                ScheduledRuns = outcome.ScheduledRuns,
                SequenceId = outcome.SequenceId,
                ErrorCode = outcome.ErrorCode,
                ErrorMessage = outcome.ErrorMessage,
                RequestId = requestId
            };

            // Map orchestrator reject code → HTTP status (lifecycle mismatch vs validation
            // vs tenant collision). Accepted always 200.
            var statusCode = outcome.Accepted
                ? 200
                : outcome.ErrorCode switch
                {
                    ErrorCodes.FollowupRunCollision => 409,
                    ErrorCodes.FollowupOptOut => 200, // intentional: opt-out is a "no work" success
                    ErrorCodes.FollowupSequenceDisabled => 409,
                    ErrorCodes.FollowupStageNotFound => 404,
                    ErrorCodes.FollowupSequenceTooLong => 400,
                    _ => 400
                };
            // Opt-out skip is reported as Accepted=false but HTTP 200 so caller does not
            // retry — this is a final terminal state, not a transient.
            return Results.Json(response, WireJson, statusCode: statusCode);
        });
    }

    /// <summary>
    /// MVP convention — operators name their sequence slug after the trigger reason.
    /// Caller may override via <see cref="FollowupTriggerRequest.SequenceSlug"/>.
    /// </summary>
    private static string DefaultSequenceSlugForReason(FollowupTriggerReason reason) => reason switch
    {
        FollowupTriggerReason.NoReplyWelcome => "no-reply-welcome",
        FollowupTriggerReason.OfferDeclined => "offer-declined",
        FollowupTriggerReason.OfferTimeout => "offer-timeout",
        FollowupTriggerReason.OnHold => "on-hold",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, $"Unknown trigger reason: {reason}")
    };

    /// <summary>
    /// Constant-time shared-secret comparison. Both inputs are UTF-8 encoded and handed
    /// to <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>,
    /// which internally returns false on length mismatch. A length-reveal side channel is
    /// accepted because the secret length is a static deploy-time config value (not
    /// user-controlled), and a mismatched-length input cannot be coerced to "equal"
    /// through timing anyway.
    /// </summary>
    private static bool FixedTimeEquals(string expected, string presented)
    {
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
