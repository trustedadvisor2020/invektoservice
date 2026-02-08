using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Middleware;

/// <summary>
/// JWT authentication middleware for webhook endpoints.
/// GR-1.9: Validates Bearer token, extracts TenantContext, stores in HttpContext.Items.
/// Only applies to paths that require auth (configured via path prefixes).
/// </summary>
public sealed class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtValidator _jwtValidator;
    private readonly JsonLinesLogger _logger;
    private readonly HashSet<string> _authRequiredPrefixes;

    public JwtAuthMiddleware(
        RequestDelegate next,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        IEnumerable<string> authRequiredPrefixes)
    {
        _next = next;
        _jwtValidator = jwtValidator;
        _logger = logger;
        _authRequiredPrefixes = new HashSet<string>(authRequiredPrefixes, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only check auth for configured prefixes
        if (!RequiresAuth(path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.SystemWarn($"[{ErrorCodes.AuthUnauthorized}] Missing or invalid Authorization header: path={path}");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"));
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var (tenantContext, error) = _jwtValidator.ValidateToken(token);

        if (tenantContext == null)
        {
            var errorCode = error?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true
                ? ErrorCodes.AuthTokenExpired
                : ErrorCodes.AuthTokenInvalid;

            _logger.SystemWarn($"[{errorCode}] JWT validation failed: path={path}, error={error}");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(errorCode, error ?? "Token validation failed", "-"));
            return;
        }

        // Store TenantContext for downstream handlers
        context.Items["TenantContext"] = tenantContext;

        // Also set correlation headers for logging
        if (string.IsNullOrEmpty(context.Request.Headers[HeaderNames.TenantId].FirstOrDefault()))
        {
            context.Request.Headers[HeaderNames.TenantId] = tenantContext.TenantId.ToString();
        }

        await _next(context);
    }

    private bool RequiresAuth(string path)
    {
        foreach (var prefix in _authRequiredPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Extension methods for registering JWT auth middleware.
/// </summary>
public static class JwtAuthMiddlewareExtensions
{
    /// <summary>
    /// Add JWT auth middleware that protects paths starting with specified prefixes.
    /// Existing endpoints (health, ops, chat/analyze) are NOT affected.
    /// </summary>
    public static IApplicationBuilder UseJwtAuth(
        this IApplicationBuilder app,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        params string[] authRequiredPrefixes)
    {
        return app.UseMiddleware<JwtAuthMiddleware>(jwtValidator, logger, (IEnumerable<string>)authRequiredPrefixes);
    }
}
