using Invekto.Backend.Services.Internal;
using Invekto.Backend.Services.Zoho;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Lifecycle;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services.Lifecycle;

/// <summary>
/// Paket B-META: receiver for Automation → Backend lifecycle notifications.
/// Currently exposes a single endpoint <c>POST /api/internal/lifecycle/welcome-sent</c>;
/// future lifecycle events (engaged, qualified, etc.) land as sibling handlers
/// in the same class as they come online.
///
/// The fire-and-forget semantics run on the Backend side: this handler returns
/// 202 immediately after the dispatcher is kicked off on the thread pool, so
/// the Automation-side HTTP call completes fast and retains best-effort shape
/// regardless of Zoho transport latency or Blueprint transition cost.
///
/// Auth is the FEAT-LIW Chunk B pattern:
///   * JWT middleware on <c>/api/internal/</c> establishes TenantContext
///   * <see cref="IntakeInternalAuth"/> proves peer-service identity via
///     <c>X-Internal-Service-Token</c> cross-check
///   * Payload <c>tenant_id</c> MUST equal TenantContext.TenantId (iter-2
///     reinforcement against tenant-id spoofing with a valid JWT)
/// </summary>
public static class LifecycleInternalEndpoints
{
    public static void MapLifecycleInternalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/internal/lifecycle/welcome-sent", (
            HttpContext ctx,
            IConfiguration appConfig,
            JsonLinesLogger jsonLog,
            ZohoLifecycleDispatcher dispatcher,
            WelcomeSentNotification? request) =>
        {
            var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            const string authMsg = "Servisler arasi yetki gecersiz veya yapilandirilmamis.";

            var auth = IntakeInternalAuth.Validate(ctx, appConfig);
            if (!auth.Ok)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] welcome-sent: internal-auth fail " +
                    $"(status={auth.StatusCode}, reason='{auth.Reason}', requestId={rid})");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, authMsg, rid),
                    statusCode: auth.StatusCode);
            }

            if (ctx.Items["TenantContext"] is not TenantContext tc)
            {
                jsonLog.SystemError(
                    $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] welcome-sent: TenantContext absent " +
                    $"despite JWT-required prefix (middleware misconfig; requestId={rid})");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, authMsg, rid),
                    statusCode: 401);
            }

            if (request is null || request.TenantId <= 0 || request.LeadId <= 0)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation,
                        "welcome-sent gövdesi eksik (tenant_id + lead_id zorunlu).", rid),
                    statusCode: 400);

            if (request.TenantId != tc.TenantId)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] welcome-sent: tenant_id mismatch " +
                    $"payload={request.TenantId} jwt_claim={tc.TenantId} requestId={rid}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, authMsg, rid),
                    statusCode: 403);
            }

            // Fire-and-forget: dispatcher kicks Zoho sync onto the thread pool and
            // returns immediately. 202 Accepted signals "received and enqueued,
            // no outcome guarantee" which matches the Automation-side fire-and-
            // forget semantic of the calling TriggerWelcomeFlowJob hook.
            dispatcher.DispatchEvent(tc.TenantId, request.LeadId, "welcome_sent");

            jsonLog.StepInfo(
                $"lifecycle welcome-sent accepted: tenant={tc.TenantId} lead={request.LeadId} triggered_at={request.TriggeredAt:O}",
                rid);
            return Results.Json(new { data = new { accepted = true } }, statusCode: 202);
        });
    }
}
