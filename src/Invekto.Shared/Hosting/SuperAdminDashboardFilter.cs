using Hangfire.Dashboard;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Invekto.Shared.Hosting;

/// <summary>
/// G7: Hangfire dashboard authorization filter.
/// Allows access only when the caller's JWT carries <c>tenant_id</c> claim == 0 (superadmin),
/// matching the SuperAdmin endpoint convention in arch/auth-architecture.md.
/// The JWT middleware (UseJwtAuth) populates HttpContext.User before this filter runs.
/// </summary>
public sealed class SuperAdminDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;
        var allowed = false;
        string denyReason = "no-claim";

        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            denyReason = "unauthenticated";
        }
        else
        {
            var tenantClaim = user.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantClaim))
                denyReason = "missing-tenant-claim";
            else if (!int.TryParse(tenantClaim, out var tenantId))
                denyReason = "invalid-tenant-claim";
            else if (tenantId != 0)
                denyReason = $"non-superadmin-tenant={tenantId}";
            else
                allowed = true;
        }

        if (!allowed)
        {
            var logger = httpContext.RequestServices.GetService<JsonLinesLogger>();
            logger?.SystemWarn(
                $"[{ErrorCodes.JobDashboardUnauthorized}] /hangfire access denied: {denyReason}, " +
                $"path={httpContext.Request.Path}, ip={httpContext.Connection.RemoteIpAddress}");
        }

        return allowed;
    }
}
