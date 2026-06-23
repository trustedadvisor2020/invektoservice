using System.Net;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Chatinbox.Shared.Middleware;

/// <summary>
/// JWT authentication middleware for Invekto API endpoints.
/// Supports optional IP whitelist bypass: whitelisted IPs skip JWT and provide
/// tenant identity via ?companyId= query parameter instead.
/// Shared across all Invekto microservices.
/// </summary>
public sealed class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtValidator _jwtValidator;
    private readonly JsonLinesLogger _logger;
    private readonly HashSet<string> _authRequiredPrefixes;
    private readonly HashSet<string> _authExcludedPrefixes;
    private readonly HashSet<string> _allowedIps;

    public JwtAuthMiddleware(
        RequestDelegate next,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        IEnumerable<string> authRequiredPrefixes,
        HashSet<string> allowedIps)
        : this(next, jwtValidator, logger, authRequiredPrefixes, Array.Empty<string>(), allowedIps)
    {
    }

    public JwtAuthMiddleware(
        RequestDelegate next,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        IEnumerable<string> authRequiredPrefixes,
        IEnumerable<string> authExcludedPrefixes,
        HashSet<string> allowedIps)
    {
        _next = next;
        _jwtValidator = jwtValidator;
        _logger = logger;
        _authRequiredPrefixes = new HashSet<string>(authRequiredPrefixes, StringComparer.OrdinalIgnoreCase);
        _authExcludedPrefixes = new HashSet<string>(authExcludedPrefixes, StringComparer.OrdinalIgnoreCase);
        _allowedIps = allowedIps;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!RequiresAuth(path))
        {
            await _next(context);
            return;
        }

        // IP whitelist bypass: trusted IPs authenticate via ?companyId= query param
        if (_allowedIps.Count > 0 && TryIpWhitelistAuth(context, path))
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

        // Defense-in-depth: strip UTF-8 BOM (﻿) that some misbehaving clients prepend after
        // the "Bearer " literal. Trim() does not remove BOM by itself. Empty result falls through
        // to the JwtValidator which returns a malformed-token 401 (not a 500).
        var token = authHeader["Bearer ".Length..].Trim().TrimStart('﻿').Trim();
        var (tenantContext, error) = _jwtValidator.ValidateToken(token);

        if (tenantContext == null)
        {
            var errorCode = error?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true
                ? ErrorCodes.AuthTokenExpired
                : error?.Contains("malformed", StringComparison.OrdinalIgnoreCase) == true
                    ? ErrorCodes.AuthTokenMalformed
                    : ErrorCodes.AuthTokenInvalid;

            _logger.SystemWarn($"[{errorCode}] JWT validation failed: path={path}, error={error}");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(errorCode, error ?? "Token validation failed", "-"));
            return;
        }

        context.Items["TenantContext"] = tenantContext;

        // Validate tenant_id header matches JWT claim (prevent tenant spoofing)
        var existingTenantId = context.Request.Headers[HeaderNames.TenantId].FirstOrDefault();
        if (!string.IsNullOrEmpty(existingTenantId) && existingTenantId != tenantContext.TenantId.ToString())
        {
            _logger.SystemWarn($"[{ErrorCodes.AuthUnauthorized}] Tenant ID mismatch: header={existingTenantId}, jwt={tenantContext.TenantId}, path={path}");
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant ID mismatch between header and JWT token", "-"));
            return;
        }

        if (string.IsNullOrEmpty(existingTenantId))
        {
            context.Request.Headers[HeaderNames.TenantId] = tenantContext.TenantId.ToString();
        }

        await _next(context);
    }

    /// <summary>
    /// Checks if the remote IP is whitelisted and extracts companyId from query string.
    /// Returns true if auth succeeded (TenantContext set), false to fall through to JWT.
    /// On invalid companyId from a whitelisted IP, writes 400 and returns true (request handled).
    /// </summary>
    private bool TryIpWhitelistAuth(HttpContext context, string path)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null) return false;

        // Normalize IPv4-mapped IPv6 (e.g. ::ffff:91.151.84.79 -> 91.151.84.79)
        var ipString = remoteIp.IsIPv4MappedToIPv6
            ? remoteIp.MapToIPv4().ToString()
            : remoteIp.ToString();

        if (!_allowedIps.Contains(ipString)) return false;

        // IP is whitelisted — require companyId query param
        if (!context.Request.Query.TryGetValue("companyId", out var companyIdValues)
            || !int.TryParse(companyIdValues.FirstOrDefault(), out var companyId)
            || companyId <= 0)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationWebhookInvalidPayload}] IP whitelisted but missing/invalid companyId: ip={ipString}, path={path}");
            context.Response.StatusCode = 400;
            context.Response.WriteAsJsonAsync(
                ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "companyId query parameter required (positive integer)", "-")).GetAwaiter().GetResult();
            return true; // handled — do not fall through to JWT
        }

        context.Items["TenantContext"] = new TenantContext
        {
            TenantId = companyId,
            UserId = 0,
            Role = "service"
        };
        context.Request.Headers[HeaderNames.TenantId] = companyId.ToString();

        _logger.SystemInfo($"Webhook IP auth: ip={ipString}, companyId={companyId}, path={path}");
        return true;
    }

    private bool RequiresAuth(string path)
    {
        // Exclusions win over inclusions — lets an endpoint nested under a required
        // prefix (e.g. /api/v1/leads/intake/*) opt out cleanly for its own auth scheme.
        foreach (var prefix in _authExcludedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        foreach (var prefix in _authRequiredPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public static class JwtAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuth(
        this IApplicationBuilder app,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        params string[] authRequiredPrefixes)
    {
        return app.UseMiddleware<JwtAuthMiddleware>(jwtValidator, logger, (IEnumerable<string>)authRequiredPrefixes, new HashSet<string>());
    }

    public static IApplicationBuilder UseJwtAuth(
        this IApplicationBuilder app,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        HashSet<string> webhookAllowedIps,
        params string[] authRequiredPrefixes)
    {
        return app.UseMiddleware<JwtAuthMiddleware>(jwtValidator, logger, (IEnumerable<string>)authRequiredPrefixes, webhookAllowedIps);
    }

    /// <summary>
    /// Registers the JWT middleware with explicit exclusions. Exclusion prefixes win
    /// over inclusions: a path matched by both is treated as unauthenticated, so the
    /// endpoint can run its own auth scheme (e.g. API key). Used by FEAT-LIW for
    /// /api/v1/leads/intake/ while /api/v1/leads stays JWT-protected.
    /// </summary>
    public static IApplicationBuilder UseJwtAuth(
        this IApplicationBuilder app,
        JwtValidator jwtValidator,
        JsonLinesLogger logger,
        HashSet<string> webhookAllowedIps,
        IEnumerable<string> authRequiredPrefixes,
        IEnumerable<string> authExcludedPrefixes)
    {
        return app.UseMiddleware<JwtAuthMiddleware>(
            jwtValidator,
            logger,
            authRequiredPrefixes,
            authExcludedPrefixes,
            webhookAllowedIps);
    }
}
