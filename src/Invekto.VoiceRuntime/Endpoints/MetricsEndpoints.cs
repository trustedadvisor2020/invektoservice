using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Metrics;

namespace Invekto.VoiceRuntime.Endpoints;

public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        // F0 PoC: rolling window latency stats. F2 will add per-tenant + per-channel breakdown.
        // Auth: JWT-gated, superadmin only (tenant=0). Dev environment with empty JWT secret
        // allows ?dev=1 bypass to mirror the WS endpoint convention.
        app.MapGet("/metrics/latency", (
            HttpContext ctx,
            LatencyTracker tracker,
            JwtValidator jwt,
            IConfiguration config,
            IWebHostEnvironment env,
            JsonLinesLogger logger) =>
        {
            // Metrics is ALWAYS JWT-gated superadmin (tenant=0). No dev=1 bypass — even in Development,
            // operator must mint a tenant=0 token. This prevents accidental anonymous access if
            // ASPNETCORE_ENVIRONMENT or Jwt:SecretKey is misconfigured.
            var token = ctx.Request.Query["token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = ctx.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    token = authHeader["Bearer ".Length..].Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [/metrics/latency] rejected: missing token");
                return Results.Json(new { code = ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed, message = "Bearer token required" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var (tenantContext, error) = jwt.ValidateToken(token);
            if (tenantContext is null)
            {
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [/metrics/latency] rejected: {error ?? "invalid token"}");
                return Results.Json(new { code = ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed, message = error ?? "invalid token" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (tenantContext.TenantId != 0)
            {
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [/metrics/latency] rejected: non-superadmin tenant={tenantContext.TenantId}");
                return Results.Json(new { code = ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed, message = "F0 PoC metrics require superadmin (tenant=0)" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var snapshot = tracker.Snapshot();
            return Results.Ok(new
            {
                service = "Invekto.VoiceRuntime",
                generated_at = DateTimeOffset.UtcNow,
                first_byte = new
                {
                    snapshot.FirstByte.Count,
                    snapshot.FirstByte.P50,
                    snapshot.FirstByte.P95,
                    snapshot.FirstByte.P99,
                    snapshot.FirstByte.Max
                },
                barge_in = new
                {
                    snapshot.BargeIn.Count,
                    snapshot.BargeIn.P50,
                    snapshot.BargeIn.P95,
                    snapshot.BargeIn.P99,
                    snapshot.BargeIn.Max
                }
            });
        });
    }
}
