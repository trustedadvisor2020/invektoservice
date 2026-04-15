// Adim 4: Stage mapping editor endpoints (tenant JWT).
//   GET  /api/v1/zoho/blueprint/transitions    -> module-level Blueprint transitions for dropdown; ?refresh=true bypasses cache.
//   POST /api/v1/zoho/stage-mappings/test      -> dry-run: validate (zohoEvent, zohoTransitionId) against live Blueprint transitions whitelist.
// PUT/GET /api/v1/zoho/stage-mappings already mapped in ZohoSyncEndpoints (P1).
// Auth: mounted under /api/v1/ -> UseJwtAuth middleware (Integrations Program.cs:165) sets TenantContext.
// Schema source of truth (NO new migration):
//   - zoho_stage_mappings: arch/db/migrations/013-zoho-stage-mappings.sql + 015-rename-gunes-to-zoho.sql
//                          (tenant_id, zoho_event, zoho_transition_id, zoho_transition_name; UNIQUE(tenant_id, zoho_event)).
//   PUT /api/v1/zoho/stage-mappings (P1) via ZohoStageMappingService.ReplaceAsync -> tenant_id from TenantContext
//   (NOT request body); full-upsert semantics: mevcut tenant rows DELETE + new list INSERT.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoStageMappingEditorEndpoints
{
    public static IEndpointRouteBuilder MapZohoStageMappingEditorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/zoho/blueprint/transitions", async (
            HttpContext ctx,
            IZohoBlueprintClient blueprint,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var refresh = ctx.Request.Query["refresh"].ToString();
            var forceRefresh = string.Equals(refresh, "true", StringComparison.OrdinalIgnoreCase);

            try
            {
                var (transitions, fromCache) = await blueprint.GetAllBlueprintTransitionsAsync(
                    tenantContext.TenantId, forceRefresh, ct).ConfigureAwait(false);

                var items = new List<ZohoBlueprintTransitionDto>(transitions.Count);
                foreach (var t in transitions)
                {
                    items.Add(new ZohoBlueprintTransitionDto
                    {
                        TransitionId = t.TransitionId,
                        Name         = t.Name,
                        NextState    = t.NextState,
                    });
                }

                return Results.Ok(new ZohoBlueprintTransitionsResponse { Items = items, FromCache = fromCache });
            }
            catch (InvalidOperationException ex)
            {
                var (code, status) = MapBlueprintException(ex.Message);
                return Results.Json(ErrorResponse.Create(code, ex.Message, requestId), statusCode: status);
            }
        });

        app.MapPost("/api/v1/zoho/stage-mappings/test", async (
            HttpContext ctx,
            IZohoBlueprintClient blueprint,
            ZohoStageMappingTestRequest body,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            if (body is null || string.IsNullOrWhiteSpace(body.ZohoEvent) || string.IsNullOrWhiteSpace(body.ZohoTransitionId))
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsQueryValidation,
                        "ZohoEvent and ZohoTransitionId are required.", requestId),
                    statusCode: 400);

            try
            {
                // Test = whitelist check against live blueprint. No Zoho mutation (spec_architectural_decisions).
                var (transitions, _) = await blueprint.GetAllBlueprintTransitionsAsync(
                    tenantContext.TenantId, forceRefresh: false, ct).ConfigureAwait(false);

                foreach (var t in transitions)
                {
                    if (string.Equals(t.TransitionId, body.ZohoTransitionId, StringComparison.Ordinal))
                    {
                        return Results.Ok(new ZohoStageMappingTestResponse
                        {
                            Valid          = true,
                            TransitionName = t.Name,
                            NextState      = t.NextState,
                        });
                    }
                }

                // Whitelist failure: surface INV-INT-122 in the envelope (200 with {valid:false}) rather than
                // an error status, because the endpoint semantic is "validation check" not a failed action.
                // UI reads ErrorCode + Reason for actionable display.
                return Results.Ok(new ZohoStageMappingTestResponse
                {
                    Valid     = false,
                    ErrorCode = ZohoErrorCodes.BlueprintTransitionNotFound,
                    Reason    = $"{ZohoErrorCodes.BlueprintTransitionNotFound}: Transition id '{body.ZohoTransitionId}' not found in Zoho Blueprint for tenant {tenantContext.TenantId}. Discover ile fresh listeyi tekrar cekin.",
                });
            }
            catch (InvalidOperationException ex)
            {
                var (code, status) = MapBlueprintException(ex.Message);
                return Results.Json(ErrorResponse.Create(code, ex.Message, requestId), statusCode: status);
            }
        });

        return app;
    }

    private static (string Code, int StatusCode) MapBlueprintException(string message)
    {
        if (message.StartsWith(ZohoErrorCodes.BlueprintNotConfigured, StringComparison.Ordinal))
            return (ZohoErrorCodes.BlueprintNotConfigured, StatusCodes.Status409Conflict);
        if (message.StartsWith(ZohoErrorCodes.BlueprintTransitionNotFound, StringComparison.Ordinal))
            return (ZohoErrorCodes.BlueprintTransitionNotFound, StatusCodes.Status404NotFound);
        if (message.StartsWith(ZohoErrorCodes.ConnectionNotFound, StringComparison.Ordinal))
            return (ZohoErrorCodes.ConnectionNotFound, StatusCodes.Status404NotFound);
        if (message.StartsWith(ZohoErrorCodes.RateLimitReached, StringComparison.Ordinal))
            return (ZohoErrorCodes.RateLimitReached, StatusCodes.Status429TooManyRequests);
        if (message.StartsWith(ZohoErrorCodes.SyncInfrastructureError, StringComparison.Ordinal))
            return (ZohoErrorCodes.SyncInfrastructureError, StatusCodes.Status502BadGateway);
        return (ZohoErrorCodes.SyncInfrastructureError, StatusCodes.Status502BadGateway);
    }

    private static string ResolveRequestId(HttpContext ctx) =>
        ctx.Items["RequestId"] as string ?? ctx.TraceIdentifier;
}
