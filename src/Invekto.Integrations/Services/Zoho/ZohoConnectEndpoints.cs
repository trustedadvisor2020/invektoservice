// Adim 2 Paket B: HTTP endpoints for Zoho OAuth handshake.
//   GET /api/v1/zoho/connect-url   -> JWT-protected (uses /api/v1/ JwtAuth filter), returns authorize URL.
//   GET /integrations/zoho/callback -> public, processes Zoho redirect (code+state).
using System;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoConnectEndpoints
{
    public static IEndpointRouteBuilder MapZohoConnectEndpoints(this IEndpointRouteBuilder app)
    {
        // Authenticated: tenant comes from JwtAuth middleware via TenantContext.
        app.MapGet("/api/v1/zoho/connect-url", (
            HttpContext ctx,
            ZohoConnectionService service,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
            {
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);
            }

            try
            {
                var url = service.BuildAuthorizeUrl(tenantContext.TenantId);
                return Results.Ok(new ZohoConnectUrlResponse(url));
            }
            catch (InvalidOperationException ex)
            {
                // INV-INT-116 (region not configured) is the only expected path here; surface 503 + actionable message.
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.RegionNotConfigured, ex.Message, requestId),
                    statusCode: 503);
            }
        });

        // Public callback (Zoho redirects the browser here; no JWT possible).
        app.MapGet("/integrations/zoho/callback", async (
            HttpContext ctx,
            ZohoConnectionService service,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var query     = ctx.Request.Query;
            var code      = query["code"].ToString();
            var state     = query["state"].ToString();
            var accSrv    = query["accounts-server"].ToString();
            string? userEmail = null;  // Zoho callback never returns an email; reserved for future hint param.

            try
            {
                var tenantId = await service.HandleCallbackAsync(code, state, accSrv, userEmail, ct).ConfigureAwait(false);
                // Render a small HTML success page — browser is the client, JSON would be confusing for end users.
                return Results.Content(
                    BuildSuccessHtml(tenantId),
                    contentType: "text/html; charset=utf-8");
            }
            catch (InvalidOperationException ex)
            {
                var (errorCode, statusCode) = MapException(ex.Message);
                return Results.Json(
                    ErrorResponse.Create(errorCode, ex.Message, requestId),
                    statusCode: statusCode);
            }
        });

        return app;
    }

    private static (string Code, int StatusCode) MapException(string message)
    {
        // Exception messages start with "INV-INT-NNN: ..." — surface that as the code.
        // Full coverage of ZohoErrorCodes (INV-INT-110..119); fallback only for genuinely unexpected paths.
        if (message.StartsWith(ZohoErrorCodes.UnknownRegion, StringComparison.Ordinal))
            return (ZohoErrorCodes.UnknownRegion, StatusCodes.Status400BadRequest);
        if (message.StartsWith(ZohoErrorCodes.OAuthStateInvalid, StringComparison.Ordinal))
            return (ZohoErrorCodes.OAuthStateInvalid, StatusCodes.Status400BadRequest);
        if (message.StartsWith(ZohoErrorCodes.OAuthStateTenantMismatch, StringComparison.Ordinal))
            return (ZohoErrorCodes.OAuthStateTenantMismatch, StatusCodes.Status400BadRequest);
        if (message.StartsWith(ZohoErrorCodes.TokenExchangeFailed, StringComparison.Ordinal))
            return (ZohoErrorCodes.TokenExchangeFailed, StatusCodes.Status502BadGateway);
        if (message.StartsWith(ZohoErrorCodes.TokenRefreshFailed, StringComparison.Ordinal))
            return (ZohoErrorCodes.TokenRefreshFailed, StatusCodes.Status502BadGateway);
        if (message.StartsWith(ZohoErrorCodes.ConnectionNotFound, StringComparison.Ordinal))
            return (ZohoErrorCodes.ConnectionNotFound, StatusCodes.Status404NotFound);
        if (message.StartsWith(ZohoErrorCodes.RegionNotConfigured, StringComparison.Ordinal))
            return (ZohoErrorCodes.RegionNotConfigured, StatusCodes.Status503ServiceUnavailable);
        if (message.StartsWith(ZohoErrorCodes.DecryptionFailed, StringComparison.Ordinal))
            return (ZohoErrorCodes.DecryptionFailed, StatusCodes.Status500InternalServerError);
        if (message.StartsWith(ZohoErrorCodes.Disconnected, StringComparison.Ordinal))
            return (ZohoErrorCodes.Disconnected, StatusCodes.Status409Conflict);
        if (message.StartsWith(ZohoErrorCodes.RateLimitReached, StringComparison.Ordinal))
            return (ZohoErrorCodes.RateLimitReached, StatusCodes.Status429TooManyRequests);
        return (ErrorCodes.GeneralUnknown, StatusCodes.Status500InternalServerError);
    }

    private static string ResolveRequestId(HttpContext ctx) =>
        ctx.Request.Headers["X-Request-Id"].ToString() is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N");

    private static string BuildSuccessHtml(int tenantId) => $@"<!doctype html>
<html lang=""tr""><head><meta charset=""utf-8""><title>Zoho Bağlandı</title>
<style>
body{{font-family:Inter,system-ui,sans-serif;background:#f8fafc;color:#0f172a;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}}
.box{{background:#fff;border:1px solid #e2e8f0;border-left:5px solid #134AA7;border-radius:12px;padding:32px 40px;box-shadow:0 4px 12px rgba(15,23,42,0.08);max-width:480px;text-align:center}}
h1{{margin:0 0 8px;color:#134AA7;font-size:22px}}
p{{margin:6px 0;color:#475569;font-size:14px;line-height:1.5}}
.t{{color:#64748b;font-size:12px;margin-top:18px}}
</style></head>
<body><div class=""box"">
<h1>✅ Zoho bağlantısı kuruldu</h1>
<p>Hesabınız başarıyla bağlandı. Bu pencereyi kapatabilirsiniz.</p>
<p class=""t"">Tenant #{tenantId} · Invekto Integrations</p>
</div></body></html>";

    public sealed record ZohoConnectUrlResponse(string AuthorizeUrl);
}
