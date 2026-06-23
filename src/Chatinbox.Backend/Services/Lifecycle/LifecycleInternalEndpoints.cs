using Chatinbox.Backend.Services.Internal;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Lifecycle;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Backend.Services.Lifecycle;

/// <summary>
/// Paket B-META: receiver for Automation → Backend lifecycle notifications.
/// Currently exposes a single endpoint <c>POST /api/internal/lifecycle/welcome-sent</c>;
/// future lifecycle events (engaged, qualified, etc.) land as sibling handlers
/// in the same class as they come online.
///
/// FEAT-INMA-PIPELINE-V2 C1 (2026-05-13): Zoho Blueprint dispatch removed. Handler
/// now accepts + validates + logs the welcome-sent notification with no downstream
/// sync side effects. The endpoint contract (POST 202 with internal-auth + JWT
/// tenant-match + payload validation) is preserved so Automation TriggerWelcomeFlowJob
/// continues to receive 202 without behavior change on its side. INMA-authoritative
/// customer_status flow (V2 C2-C4) will replace this hop when INMA contract ships;
/// at that point welcome events feed into the new flow trigger channel.
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

            // FEAT-INMA-PIPELINE-V2 C1: dispatch side effect removed; log + 202 preserved
            // for Automation TriggerWelcomeFlowJob contract compatibility. C3 trigger
            // channel BLOCKED pending INMA contract.
            jsonLog.StepInfo(
                $"lifecycle welcome-sent accepted: tenant={tc.TenantId} lead={request.LeadId} triggered_at={request.TriggeredAt:O}",
                rid);
            return Results.Json(new { data = new { accepted = true } }, statusCode: 202);
        });
    }
}
