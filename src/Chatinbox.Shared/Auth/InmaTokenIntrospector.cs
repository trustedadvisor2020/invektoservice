using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Chatinbox.Shared.Auth;

/// <summary>
/// Validates inma JWT tokens via WapCRM welcome-endpoint introspection
/// (decode-only locally, then GET {ApiBaseUrl}/api/invekto/welcome with Bearer).
///
/// Why no signature verify: inma's signing key is the WapCRM backend's private secret
/// and is NEVER shared with frontends or downstream services. The Angular inma app
/// itself uses jwt-decode (decode-only). Tokens are only useful when sent to WapCRM
/// API endpoints, which verify signatures with their own key. A second verify layer
/// here would be (a) impossible without key sharing and (b) brittle (key rotation /
/// algorithm changes break it).
///
/// Cache: per-token IMemoryCache with 5-minute TTL. Cache key uses SHA256(token)
/// truncated to 64 bits (16 hex chars) so the raw token is never persisted in memory.
/// 5dk windows = revoked tokens are rejected within 5 minutes; cache hit avoids
/// per-request 50-100ms welcome round-trip.
///
/// Defense-in-depth: forged tokens cannot pass because the cache MISS path always
/// calls welcome, which the inma backend rejects with 401 (signature invalid against
/// inma's private key). The cache only serves tokens that previously passed welcome.
///
/// Thread-safe (HttpClient + IMemoryCache are thread-safe). Register as singleton.
/// </summary>
public sealed class InmaTokenIntrospector
{
    private const string NameIdentifierClaim =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InmaTokenIntrospector> _logger;
    private readonly string _apiBaseUrl;

    public InmaTokenIntrospector(
        HttpClient httpClient,
        IMemoryCache cache,
        InmaJwtSettings settings,
        ILogger<InmaTokenIntrospector> logger)
    {
        _httpClient = httpClient;
        _cache      = cache;
        _logger     = logger;
        _apiBaseUrl = settings.ApiBaseUrl?.TrimEnd('/') ?? string.Empty;
    }

    /// <summary>
    /// Structured introspection result. ErrorCode is one of the registered project codes
    /// (INV-AUTH-001 = expired, INV-AUTH-002 = invalid, INV-AUTH-008 = service unavailable)
    /// or null on success. Callers can dispatch on ErrorCode without parsing Detail strings.
    /// </summary>
    public readonly record struct IntrospectResult(
        InmaTokenContext? Context,
        string? ErrorCode,
        string? Detail,
        bool FromCache);

    /// <summary>
    /// Validate an inma JWT by (1) decoding claims locally and (2) calling the inma welcome
    /// endpoint to confirm the token is signed by inma's backend. Returns IntrospectResult
    /// with structured ErrorCode (INV-AUTH-001/002/008) so callers can map status codes
    /// without substring matching.
    /// </summary>
    public async Task<IntrospectResult> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new IntrospectResult(null, "INV-AUTH-002", "Token is empty", false);

        var cacheKey = ComputeCacheKey(token);
        if (_cache.TryGetValue<InmaTokenContext>(cacheKey, out var cached) && cached is not null)
            return new IntrospectResult(cached, null, null, true);

        // Configuration guard: without ApiBaseUrl we cannot introspect — fail closed (unavailable).
        if (string.IsNullOrEmpty(_apiBaseUrl))
            return new IntrospectResult(null, "INV-AUTH-008", "INMA ApiBaseUrl not configured", false);

        // Decode claims locally (signature NOT verified — see class comment).
        InmaTokenContext? context;
        try
        {
            context = ParseClaims(token);
        }
        catch (ArgumentException ex)
        {
            return new IntrospectResult(null, "INV-AUTH-002", ex.Message, false);
        }
        catch (JsonException ex)
        {
            return new IntrospectResult(null, "INV-AUTH-002", $"malformed claims ({ex.Message})", false);
        }

        if (context is null)
            return new IntrospectResult(null, "INV-AUTH-002", "claim parse failed", false);

        // Introspect via welcome endpoint — inma backend verifies signature with its own key.
        var welcomeUrl = _apiBaseUrl + "/api/invekto/welcome";
        using var request = new HttpRequestMessage(HttpMethod.Get, welcomeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[INMA-INTROSPECT] company_code={CompanyCode} result=transport_fail", context.CompanyCode);
            return new IntrospectResult(null, "INV-AUTH-008", "INMA introspection unavailable (network)", false);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            _logger.LogWarning("[INMA-INTROSPECT] company_code={CompanyCode} result=transport_timeout", context.CompanyCode);
            return new IntrospectResult(null, "INV-AUTH-008", "INMA introspection unavailable (timeout)", false);
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("[INMA-INTROSPECT] company_code={CompanyCode} result=welcome_401", context.CompanyCode);
                return new IntrospectResult(null, "INV-AUTH-001", "INMA token expired or invalid", false);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[INMA-INTROSPECT] company_code={CompanyCode} result=welcome_fail status={Status}",
                    context.CompanyCode, (int)response.StatusCode);
                return new IntrospectResult(null, "INV-AUTH-008",
                    $"INMA introspection unavailable (HTTP {(int)response.StatusCode})", false);
            }

            _logger.LogDebug("[INMA-INTROSPECT] company_code={CompanyCode} result=welcome_ok cached_for=5min", context.CompanyCode);
            _cache.Set(cacheKey, context, CacheTtl);
            return new IntrospectResult(context, null, null, false);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static string ComputeCacheKey(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        // First 8 bytes = 64 bits → 16 hex chars. Sufficient to avoid collisions for
        // <<1000 unique tokens per 5-minute window (birthday-paradox safe margin).
        return "inma:tk:" + Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }

    private const int CompanyCodeMaxLength = 100;

    private static InmaTokenContext? ParseClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            throw new ArgumentException("Token format is not a valid JWT");

        var jwt = handler.ReadJwtToken(token);

        // Local expiry pre-check (60s clock skew). Welcome would also catch this with 401,
        // but local check avoids a wasted round-trip for clearly stale tokens.
        if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow.AddSeconds(-60))
            throw new ArgumentException("Token expired (local exp claim)");

        // CompanyCode: inma 'CompanyCode' claim (fallback: 'CompanyId'). OPAQUE STRING —
        // 5050 happens to be numeric ("5050") but DentAdavista is "dentadavista". Do NOT
        // int-parse here; translation to INSE int tenant_id is the caller's responsibility
        // via TenantRegistryRepository.ResolveOrCreateByInmaCodeAsync. Length cap matches
        // tenant_registry.inma_code VARCHAR(100).
        var companyCode = (jwt.Claims.FirstOrDefault(c => c.Type == "CompanyCode")?.Value
                           ?? jwt.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value)?.Trim();
        if (string.IsNullOrEmpty(companyCode))
            throw new ArgumentException("Missing or empty CompanyCode/CompanyId claim");
        if (companyCode.Length > CompanyCodeMaxLength)
            throw new ArgumentException($"CompanyCode exceeds max length {CompanyCodeMaxLength}");

        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == NameIdentifierClaim)?.Value
                          ?? jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new ArgumentException("Missing or invalid nameidentifier claim");

        var role = MapChatRole(jwt.Claims.FirstOrDefault(c => c.Type == "ChatRole")?.Value);
        var fullName = jwt.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? string.Empty;
        var lang = jwt.Claims.FirstOrDefault(c => c.Type == "Lang")?.Value ?? "tr";

        // InseFeatures: malformed JSON → empty array (graceful degradation).
        string[] features;
        try
        {
            var raw = jwt.Claims.FirstOrDefault(c => c.Type == "InseFeatures")?.Value;
            features = string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            features = Array.Empty<string>();
        }

        return new InmaTokenContext
        {
            CompanyCode = companyCode,
            UserId = userId,
            Role = role,
            FullName = fullName,
            Lang = lang,
            InseFeatures = features
        };
    }

    private static string MapChatRole(string? chatRole) => chatRole switch
    {
        "2" => "admin",
        "1" => "agent",
        _   => "agent"
    };
}
