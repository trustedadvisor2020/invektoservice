// Adim 3 Paket 1: HTTP endpoints for Zoho sync + stage mapping management.
// Auth model:
//   POST /api/internal/zoho/sync            -> internal-service auth (shared secret header).
//                                              Deliberately outside /api/v1/ prefix so UseJwtAuth bypasses it.
//   GET/PUT /api/v1/zoho/stage-mappings     -> tenant JWT (via standard /api/v1/ auth filter).
//   GET /api/v1/zoho/sync-log/failed-count  -> tenant JWT.
// Shared secret config: "InternalServices:SharedSecret" (appsettings / env override). Missing -> 500.
// Tenant-scope trust model: caller (Invekto.Backend lifecycle hook) supplies TenantId in the body;
// shared secret is the sole trust boundary. This is an accepted architectural decision for Adim 3 P1
// (see spec_architectural_decisions in plan JSON). Inbound Zoho path in Adim 4 will introduce
// per-caller service identity if cross-tenant risk surfaces in operation.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoSyncEndpoints
{
    public const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string SharedSecretConfigKey = "InternalServices:SharedSecret";

    public static IEndpointRouteBuilder MapZohoSyncEndpoints(this IEndpointRouteBuilder app)
    {
        // Internal-only: POST /api/v1/zoho/sync (called by Invekto.Backend lifecycle hooks in P2).
        app.MapPost("/api/internal/zoho/sync", async (
            HttpContext ctx,
            IZohoSyncService syncService,
            IConfiguration config,
            ZohoSyncRequest body,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);

            var authResult = ValidateInternalAuth(ctx, config);
            if (!authResult.Ok)
                return Results.Json(
                    ErrorResponse.Create(authResult.ErrorCode, authResult.Reason, requestId),
                    statusCode: authResult.StatusCode);

            ZohoSyncResponse result;
            try
            {
                result = await syncService.SyncAsync(body, ct).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation, ex.Message, requestId),
                    statusCode: 400);
            }

            // Always 200: body.Status indicates success/failed (AC: syncLog row is the source of truth).
            return Results.Ok(result);
        });

        // Tenant-scoped: stage mapping CRUD (consumed by Dashboard UI in P3).
        app.MapGet("/api/v1/zoho/stage-mappings", async (
            HttpContext ctx,
            IZohoStageMappingService service,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var mappings = await service.ListAsync(tenantContext.TenantId, ct).ConfigureAwait(false);
            return Results.Ok(new ZohoStageMappingListResponse { Mappings = mappings });
        });

        app.MapPut("/api/v1/zoho/stage-mappings", async (
            HttpContext ctx,
            IZohoStageMappingService service,
            ZohoStageMappingUpsertRequest body,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            // Tenant comes from JWT only; body carries mappings. ZohoStageMappingUpsertRequest no longer exposes tenant_id.
            var mappings = body?.Mappings ?? Array.Empty<ZohoStageMappingEntry>();
            try
            {
                await service.ReplaceAsync(
                    tenantContext.TenantId,
                    new ZohoStageMappingUpsertRequest { Mappings = mappings },
                    ct).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation, ex.Message, requestId),
                    statusCode: 400);
            }

            var fresh = await service.ListAsync(tenantContext.TenantId, ct).ConfigureAwait(false);
            return Results.Ok(new ZohoStageMappingListResponse { Mappings = fresh });
        });

        // Tenant-scoped: sync log fail count (Dashboard badge).
        app.MapGet("/api/v1/zoho/sync-log/failed-count", async (
            HttpContext ctx,
            ZohoSyncLogRepository logRepo,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var count = await logRepo.CountFailedAsync(tenantContext.TenantId, ct).ConfigureAwait(false);
            return Results.Ok(new ZohoSyncFailedCountResponse { FailedCount = count });
        });

        return app;
    }

    private static (bool Ok, int StatusCode, string ErrorCode, string Reason) ValidateInternalAuth(HttpContext ctx, IConfiguration config)
    {
        var expected = config[SharedSecretConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
            return (false, 500, ZohoErrorCodes.InternalAuthNotConfigured,
                $"Internal service auth not configured (missing {SharedSecretConfigKey}). Operator must set the shared secret before sync endpoint can accept traffic.");

        if (!ctx.Request.Headers.TryGetValue(InternalTokenHeader, out var provided) || provided.Count == 0)
            return (false, 401, ZohoErrorCodes.InternalAuthInvalid,
                $"Missing {InternalTokenHeader} header. Caller must be an internal Invekto service with the shared secret.");

        // Constant-time comparison to avoid timing side-channel leak.
        if (!SlowEquals(provided[0], expected))
            return (false, 403, ZohoErrorCodes.InternalAuthInvalid,
                $"Invalid {InternalTokenHeader}. Rotate the shared secret in appsettings and redeploy callers.");

        return (true, 200, string.Empty, string.Empty);
    }

    private static bool SlowEquals(string? a, string b)
    {
        if (a is null) return false;
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static string ResolveRequestId(HttpContext ctx) =>
        ctx.Items["RequestId"] as string ?? ctx.TraceIdentifier;
}
