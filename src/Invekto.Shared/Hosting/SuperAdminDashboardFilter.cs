using Hangfire.Dashboard;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Invekto.Shared.Hosting;

/// <summary>
/// G7: Hangfire dashboard authorization filter.
/// Accepts EITHER a JWT with <c>tenant_id</c>=0 populated by UseJwtAuth (API-style header
/// auth) OR a validated <c>hf_jwt</c> cookie set by the <c>/ops/hangfire-login</c> bridge
/// endpoint (browser navigation, since HTML cannot attach Bearer headers).
/// The cookie carries the same Invekto JWT — validation reuses <see cref="JwtValidator"/>.
/// </summary>
public sealed class SuperAdminDashboardFilter : IDashboardAuthorizationFilter
{
    /// <summary>Cookie name set by /ops/hangfire-login; HttpOnly + Secure + SameSite=Strict.</summary>
    public const string CookieName = "hf_jwt";

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Path 1: JWT tenant_id=0 via Authorization header (populated by UseJwtAuth).
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirst("tenant_id")?.Value;
            if (int.TryParse(tenantClaim, out var tenantId) && tenantId == 0)
                return true;
        }

        // Path 2: hf_jwt cookie — validated on every request via JwtValidator.
        string denyReason = "no-auth";
        if (httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieJwt)
            && !string.IsNullOrEmpty(cookieJwt))
        {
            var validator = httpContext.RequestServices.GetService<JwtValidator>();
            if (validator == null)
            {
                denyReason = "no-jwt-validator";
            }
            else
            {
                var (ctx, error) = validator.ValidateToken(cookieJwt);
                if (ctx == null)
                    denyReason = $"cookie-invalid:{error}";
                else if (ctx.TenantId != 0)
                    denyReason = $"cookie-non-superadmin:tenant={ctx.TenantId}";
                else
                    return true;
            }
        }
        else
        {
            denyReason = "no-cookie";
        }

        httpContext.Response.StatusCode = 401;
        var logger = httpContext.RequestServices.GetService<JsonLinesLogger>();
        logger?.SystemWarn(
            $"[{ErrorCodes.JobDashboardUnauthorized}] /hangfire access denied: {denyReason}, " +
            $"path={httpContext.Request.Path}, ip={httpContext.Connection.RemoteIpAddress}");
        return false;
    }
}
