using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Chatinbox.Shared.Middleware;

/// <summary>
/// Feature guard middleware: checks that the tenant's plan includes the feature
/// required by the matched path prefix. Must be registered AFTER UseJwtAuth.
///
/// Bypasses: SuperAdmin (TenantId == 0), service calls (Role == "service"), unmatched paths.
/// Fail-open: if TenantPlanCache returns null, request is allowed through.
/// </summary>
public sealed class FeatureGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantPlanCache _planCache;
    private readonly JsonLinesLogger _logger;
    private readonly IReadOnlyList<(string Prefix, string Feature)> _featurePrefixes;

    public FeatureGuardMiddleware(
        RequestDelegate next,
        TenantPlanCache planCache,
        JsonLinesLogger logger,
        IEnumerable<(string Prefix, string Feature)> featurePrefixes)
    {
        _next = next;
        _planCache = planCache;
        _logger = logger;
        _featurePrefixes = featurePrefixes.ToList();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        string? requiredFeature = null;
        foreach (var (prefix, feature) in _featurePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                requiredFeature = feature;
                break;
            }
        }

        if (requiredFeature == null)
        {
            await _next(context);
            return;
        }

        var tenantCtx = context.Items["TenantContext"] as TenantContext;
        if (tenantCtx == null)
        {
            await _next(context);
            return;
        }

        if (tenantCtx.TenantId == 0 || tenantCtx.IsServiceCall)
        {
            await _next(context);
            return;
        }

        var planInfo = await _planCache.GetAsync(tenantCtx.TenantId);

        if (planInfo == null)
        {
            _logger.SystemWarn(
                $"[FeatureGuard] Plan info unavailable for tenant {tenantCtx.TenantId}, " +
                $"allowing request through (fail-open): path={path}");
            await _next(context);
            return;
        }

        if (!planInfo.IsFeatureEnabled(requiredFeature))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AuthFeatureDisabled}] Feature '{requiredFeature}' not in plan " +
                $"'{planInfo.PlanTier}' for tenant {tenantCtx.TenantId}: path={path}");
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(
                    ErrorCodes.AuthFeatureDisabled,
                    $"Bu özellik mevcut planınızda bulunmuyor: {requiredFeature}",
                    context.TraceIdentifier));
            return;
        }

        // Store plan info in context for tier-level checks in endpoint handlers
        context.Items["TenantPlanInfo"] = planInfo;

        await _next(context);
    }
}

public static class FeatureGuardMiddlewareExtensions
{
    /// <summary>
    /// Registers FeatureGuardMiddleware with path→feature mappings.
    /// Must be called AFTER UseJwtAuth.
    ///
    /// Usage:
    ///   app.UseFeatureGuard(planCache, logger,
    ///       ("/api/v1/knowledge/", "Knowledge"),
    ///       ("/api/v1/flows/", "FlowBuilder"));
    /// </summary>
    public static IApplicationBuilder UseFeatureGuard(
        this IApplicationBuilder app,
        TenantPlanCache planCache,
        JsonLinesLogger logger,
        params (string Prefix, string Feature)[] featurePrefixes)
    {
        return app.UseMiddleware<FeatureGuardMiddleware>(
            planCache, logger, (IEnumerable<(string, string)>)featurePrefixes);
    }
}
