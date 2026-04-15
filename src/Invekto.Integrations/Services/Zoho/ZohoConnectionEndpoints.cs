// Adim 3 Paket 3-B1: Dashboard UI icin tenant-scoped Zoho connection management endpoints.
//   GET    /api/v1/zoho/connection   -> tenant JWT, bagli mi?
//   DELETE /api/v1/zoho/connection   -> tenant JWT, soft-disconnect + best-effort revoke.
// /api/v1/ prefix altinda oldugu icin standart UseJwtAuth middleware tenant_id'yi TenantContext'e yerlestirir.
using System;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoConnectionEndpoints
{
    public static IEndpointRouteBuilder MapZohoConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/zoho/connection", async (
            HttpContext ctx,
            ZohoConnectionService service,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var status = await service.GetStatusAsync(tenantContext.TenantId, ct).ConfigureAwait(false);
            return Results.Ok(status);
        });

        app.MapDelete("/api/v1/zoho/connection", async (
            HttpContext ctx,
            ZohoConnectionService service,
            JsonLinesLogger logger,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var (disconnected, revokeOk) = await service.DisconnectAsync(tenantContext.TenantId, logger, ct).ConfigureAwait(false);
            // Idempotent: no-active-connection still returns 200 with connected=false (client intent satisfied).
            return Results.Ok(new ZohoDisconnectResponse
            {
                AlreadyDisconnected = !disconnected,
                TokenRevoked        = revokeOk,
            });
        });

        return app;
    }

    private static string ResolveRequestId(HttpContext ctx) =>
        ctx.Items["RequestId"] as string ?? ctx.TraceIdentifier;

    public sealed class ZohoDisconnectResponse
    {
        public required bool AlreadyDisconnected { get; init; }
        public required bool TokenRevoked { get; init; }
    }
}
