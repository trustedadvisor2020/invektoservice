// Adim 3 Paket 3-B1: Dashboard UI -> Backend proxy endpoints for Zoho management.
//   GET    /api/v1/zoho/connection         -> Integrations GET  /api/v1/zoho/connection
//   GET    /api/v1/zoho/connect-url        -> Integrations GET  /api/v1/zoho/connect-url
//   DELETE /api/v1/zoho/connection         -> Integrations DELETE /api/v1/zoho/connection
//   GET    /api/v1/zoho/stage-mappings     -> Integrations GET  /api/v1/zoho/stage-mappings (read-only in P3-B1; PUT deferred to P4)
//   GET    /api/v1/zoho/sync-log           -> Integrations GET  /api/v1/zoho/sync-log (forwards query string)
//   POST   /api/v1/zoho/sync-log/{id}/retry-> Integrations POST /api/v1/zoho/sync-log/{id}/retry
// Tenant JWT is forwarded verbatim; Integrations re-validates and derives tenant_id.
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invekto.Backend.Services.Zoho;

public static class ZohoProxyEndpoints
{
    public static IEndpointRouteBuilder MapZohoProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/zoho/connection", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/connection", ct));

        app.MapGet("/api/v1/zoho/connect-url", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/connect-url", ct));

        app.MapDelete("/api/v1/zoho/connection", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            Forward(ctx, proxy, HttpMethod.Delete, "/api/v1/zoho/connection", ct));

        app.MapGet("/api/v1/zoho/stage-mappings", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/stage-mappings", ct));

        app.MapGet("/api/v1/zoho/sync-log", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
        {
            // Forward query string verbatim so page/pageSize/status/event/from/to reach Integrations.
            var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            return Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/sync-log" + qs, ct);
        });

        app.MapPost("/api/v1/zoho/sync-log/{id:long}/retry",
            (long id, HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
                Forward(ctx, proxy, HttpMethod.Post, $"/api/v1/zoho/sync-log/{id}/retry", ct));

        // Adim 4: Stage mapping editor
        app.MapPut("/api/v1/zoho/stage-mappings", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            ForwardWithBody(ctx, proxy, HttpMethod.Put, "/api/v1/zoho/stage-mappings", ct));

        app.MapGet("/api/v1/zoho/blueprint/transitions", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
        {
            var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            return Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/blueprint/transitions" + qs, ct);
        });

        app.MapPost("/api/v1/zoho/stage-mappings/test", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
            ForwardWithBody(ctx, proxy, HttpMethod.Post, "/api/v1/zoho/stage-mappings/test", ct));

        // P4.1: Lead_Status picklist proxy (state-bagimsiz enum for stage mapping editor info banner + manuel input UX).
        app.MapGet("/api/v1/zoho/lead-statuses", (HttpContext ctx, IZohoProxyClient proxy, CancellationToken ct) =>
        {
            var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            return Forward(ctx, proxy, HttpMethod.Get, "/api/v1/zoho/lead-statuses" + qs, ct);
        });

        return app;
    }

    private static async Task<IResult> ForwardWithBody(
        HttpContext ctx,
        IZohoProxyClient proxy,
        HttpMethod method,
        string pathAndQuery,
        CancellationToken ct)
    {
        var bearer = ExtractBearer(ctx);
        string body;
        using (var reader = new System.IO.StreamReader(ctx.Request.Body, System.Text.Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        var upstream = await proxy.ForwardAsync(method, pathAndQuery, bearer, body, ct).ConfigureAwait(false);
        return Results.Content(upstream.Body, upstream.ContentType, statusCode: upstream.StatusCode);
    }

    private static async Task<IResult> Forward(
        HttpContext ctx,
        IZohoProxyClient proxy,
        HttpMethod method,
        string pathAndQuery,
        CancellationToken ct)
    {
        var bearer = ExtractBearer(ctx);
        var upstream = await proxy.ForwardAsync(method, pathAndQuery, bearer, ct).ConfigureAwait(false);
        return Results.Content(upstream.Body, upstream.ContentType, statusCode: upstream.StatusCode);
    }

    private static string? ExtractBearer(HttpContext ctx)
    {
        var raw = ctx.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(raw)) return null;
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(prefix.Length)
            : raw;
    }
}
