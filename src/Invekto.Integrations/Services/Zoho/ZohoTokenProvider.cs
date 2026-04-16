// Adim 2 Paket B: lazy-refresh + IMemoryCache backed Zoho access token provider.
// Strategy:
//   1. Cache hit -> return access_token immediately (no DB, no HTTP).
//   2. Cache miss -> per-tenant SemaphoreSlim lock (single-flight), re-check cache, refresh from Zoho.
//   3. After successful refresh, cache for (expires_in - 60s) safety buffer.
// Concurrency: ConcurrentDictionary keyed by tenantId for per-tenant lock isolation.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoTokenProvider : IZohoTokenProvider
{
    public const string DataProtectionPurpose = "Invekto.Integrations.Zoho.RefreshToken";
    private const int SafetyBufferSeconds = 60;

    private readonly HttpClient _httpClient;
    private readonly ZohoConnectionRepository _repository;
    private readonly ZohoRegionResolver _regionResolver;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public ZohoTokenProvider(
        HttpClient httpClient,
        ZohoConnectionRepository repository,
        ZohoRegionResolver regionResolver,
        IDataProtectionProvider protectionProvider,
        IMemoryCache cache)
    {
        _httpClient     = httpClient;
        _repository     = repository;
        _regionResolver = regionResolver;
        _protector      = protectionProvider.CreateProtector(DataProtectionPurpose);
        _cache          = cache;
    }

    public async Task<string> GetAccessTokenAsync(int tenantId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(tenantId);
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        var gate = _locks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check inside the lock (another caller may have refreshed during our wait).
            if (_cache.TryGetValue<string>(cacheKey, out cached) && !string.IsNullOrEmpty(cached))
                return cached;

            var connection = await _repository.GetActiveAsync(tenantId, ct).ConfigureAwait(false);
            if (connection is null)
                throw new InvalidOperationException(
                    $"INV-INT-115: No active Zoho connection for tenant {tenantId}. Tenant must connect via /api/v1/zoho/connect-url.");

            var regionConfig = _regionResolver.GetConfig(connection.Region)
                ?? throw new InvalidOperationException(
                    $"INV-INT-116: Zoho region '{connection.Region}' not configured for tenant {tenantId}. " +
                    $"Add Zoho:{connection.Region.ToUpperInvariant().Replace('.', '_')} block to appsettings or reconnect tenant in different DC.");

            string refreshTokenPlain;
            try
            {
                refreshTokenPlain = _protector.Unprotect(connection.EncryptedRefreshToken);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"INV-INT-117: Refresh token decryption failed for tenant {tenantId}. " +
                    "DataProtection key ring may have changed; tenant must reconnect via /api/v1/zoho/connect-url.",
                    ex);
            }

            var tokenResponse = await CallRefreshAsync(regionConfig, refreshTokenPlain, ct).ConfigureAwait(false);

            // Zoho occasionally rotates the refresh_token; persist when present.
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                var newEnc = _protector.Protect(tokenResponse.RefreshToken);
                await _repository.UpdateRefreshTokenAsync(tenantId, newEnc, ct).ConfigureAwait(false);
            }
            else
            {
                await _repository.TouchRefreshedAsync(tenantId, ct).ConfigureAwait(false);
            }

            var ttl = TimeSpan.FromSeconds(Math.Max(60, tokenResponse.ExpiresInSeconds - SafetyBufferSeconds));
            _cache.Set(cacheKey, tokenResponse.AccessToken, ttl);

            return tokenResponse.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateCache(int tenantId) => _cache.Remove(CacheKey(tenantId));

    /// <summary>
    /// Adim 3 Paket 3-B1: Best-effort revoke for a refresh_token at Zoho's revoke endpoint.
    /// Disconnect flow swallows failures (local DB disconnect is authoritative); caller logs INV-INT-130 on error.
    /// Typed catches only — no bare catch. Returns true on Zoho HTTP 2xx, false otherwise.
    /// </summary>
    public async Task<bool> RevokeRefreshTokenAsync(
        ZohoRegionConfig regionConfig,
        string refreshTokenPlain,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshTokenPlain)) return false;

        var url = TrimSlash(regionConfig.AccountsUrl) + "/oauth/v2/token/revoke?token=" + Uri.EscapeDataString(refreshTokenPlain);
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>());
            using var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    /// <summary>
    /// Exchanges an authorization_code for tokens at the region-specific accounts URL.
    /// Used by the connection callback (NOT cached - one-shot during OAuth handshake).
    /// </summary>
    public async Task<ZohoTokenResponse> ExchangeAuthorizationCodeAsync(
        ZohoRegionConfig regionConfig,
        string authorizationCode,
        CancellationToken ct = default)
    {
        var url = TrimSlash(regionConfig.AccountsUrl) + "/oauth/v2/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["client_id"]     = regionConfig.ClientId,
            ["client_secret"] = regionConfig.ClientSecret,
            ["redirect_uri"]  = regionConfig.RedirectUri,
            ["code"]          = authorizationCode,
        };

        return await PostTokenAsync(url, form, ct).ConfigureAwait(false);
    }

    private async Task<ZohoTokenResponse> CallRefreshAsync(
        ZohoRegionConfig regionConfig,
        string refreshTokenPlain,
        CancellationToken ct)
    {
        var url = TrimSlash(regionConfig.AccountsUrl) + "/oauth/v2/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = regionConfig.ClientId,
            ["client_secret"] = regionConfig.ClientSecret,
            ["refresh_token"] = refreshTokenPlain,
        };

        return await PostTokenAsync(url, form, ct).ConfigureAwait(false);
    }

    private async Task<ZohoTokenResponse> PostTokenAsync(
        string url,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "INV-INT-114: Zoho token endpoint network failure. Verify outbound connectivity to " + url + " (firewall, DNS, TLS).",
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                "INV-INT-114: Zoho token endpoint timed out (15s). Retry; if persistent, check Zoho status or raise HttpClient timeout.",
                ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            // Avoid logging the body verbatim (may contain refresh_token echoes); only status + reason.
            // Zoho returns 400 for invalid_code/expired_code (restart OAuth) and 429 for rate limit.
            var hint = response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => "Authorization code likely expired or already used; restart OAuth flow.",
                System.Net.HttpStatusCode.Unauthorized => "Client credentials rejected; verify Zoho:<REGION>:ClientId/ClientSecret in appsettings.",
                System.Net.HttpStatusCode.TooManyRequests => "Zoho rate limit hit; back off and retry after 60s.",
                _ => "Check Zoho service status or contact support."
            };
            throw new InvalidOperationException(
                $"INV-INT-114: Zoho token endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}. {hint}");
        }

        ZohoTokenWire? wire;
        try
        {
            wire = await response.Content.ReadFromJsonAsync<ZohoTokenWire>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "INV-INT-114: Zoho token response was not valid JSON. This is unexpected; check Zoho service status or proxy/middlebox interference.",
                ex);
        }

        if (wire is null || string.IsNullOrEmpty(wire.AccessToken))
            throw new InvalidOperationException(
                "INV-INT-114: Zoho token response missing access_token. Verify scopes are not empty in appsettings Zoho:<REGION>:Scopes.");

        // Diagnostic: log prefix + scope returned from Zoho (scope may differ from requested after consent).
        Console.WriteLine(
            $"[ZOHO-DIAG] token refresh OK: access_token_prefix={(wire.AccessToken.Length >= 8 ? wire.AccessToken.Substring(0, 8) : wire.AccessToken)} api_domain={wire.ApiDomain} expires_in={wire.ExpiresIn} scope={wire.Scope}");

        return new ZohoTokenResponse(
            AccessToken:      wire.AccessToken,
            RefreshToken:     wire.RefreshToken,
            ApiDomain:        wire.ApiDomain ?? string.Empty,
            TokenType:        wire.TokenType ?? "Bearer",
            ExpiresInSeconds: wire.ExpiresIn > 0 ? wire.ExpiresIn : 3600,
            Scope:            wire.Scope);
    }

    private static string CacheKey(int tenantId) =>
        "zoho:access:" + tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TrimSlash(string url) => url.EndsWith('/') ? url.TrimEnd('/') : url;

    // Wire model — ONLY used to deserialize Zoho's response. Never logged/exposed.
    private sealed class ZohoTokenWire
    {
        [JsonPropertyName("access_token")]  public string AccessToken  { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("api_domain")]    public string? ApiDomain    { get; set; }
        [JsonPropertyName("token_type")]    public string? TokenType    { get; set; }
        [JsonPropertyName("expires_in")]    public int     ExpiresIn    { get; set; }
        [JsonPropertyName("scope")]         public string? Scope        { get; set; }
    }
}
