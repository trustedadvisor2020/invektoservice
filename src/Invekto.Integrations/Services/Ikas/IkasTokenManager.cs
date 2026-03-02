using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Invekto.Integrations.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Integrations.Services.Ikas;

/// <summary>
/// OAuth2 client_credentials token manager for ikas API.
/// Caches tokens per tenant, auto-refreshes before expiry.
/// Thread-safe via per-tenant SemaphoreSlim.
/// Token endpoint: https://{store_name}.myikas.com/api/admin/oauth/token
/// Token lifetime: 4 hours (14400 seconds). Refresh buffer: 10 minutes.
/// </summary>
public sealed class IkasTokenManager : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IntegrationsRepository _repo;
    private readonly JsonLinesLogger _logger;
    private readonly ConcurrentDictionary<int, TokenEntry> _cache = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    private sealed record TokenEntry(string AccessToken, DateTime ExpiresAtUtc);

    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(10);

    public IkasTokenManager(HttpClient httpClient, IntegrationsRepository repo, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Get a valid access token for the given tenant. Returns null if credentials are missing or token fetch fails.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(int tenantId, CancellationToken ct)
    {
        // Check cache first (no lock needed for read)
        if (_cache.TryGetValue(tenantId, out var cached) &&
            cached.ExpiresAtUtc > DateTime.UtcNow.Add(RefreshBuffer))
        {
            return cached.AccessToken;
        }

        // Acquire per-tenant lock to prevent thundering herd
        var semaphore = _locks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            // Double-check after lock acquisition
            if (_cache.TryGetValue(tenantId, out cached) &&
                cached.ExpiresAtUtc > DateTime.UtcNow.Add(RefreshBuffer))
            {
                return cached.AccessToken;
            }

            return await FetchAndCacheTokenAsync(tenantId, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Invalidate cached token for a tenant (e.g., after receiving 401 from ikas API).
    /// </summary>
    public void InvalidateToken(int tenantId)
    {
        _cache.TryRemove(tenantId, out _);
    }

    private async Task<string?> FetchAndCacheTokenAsync(int tenantId, CancellationToken ct)
    {
        // Get credentials from DB
        var creds = await _repo.GetAccountFullCredentialsAsync(tenantId, "ikas", ct);
        if (creds == null)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas: no active account for tenant {tenantId}");
            return null;
        }

        var (clientId, clientSecret, _, settingsJson) = creds.Value;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas: missing client_id/client_secret for tenant {tenantId}");
            return null;
        }

        // Extract store_name from settings_json
        var storeName = ExtractStoreName(settingsJson);
        if (string.IsNullOrEmpty(storeName))
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas: missing store_name in settings for tenant {tenantId}");
            return null;
        }

        // POST to ikas OAuth token endpoint
        var tokenUrl = $"https://{storeName}.myikas.com/api/admin/oauth/token";

        try
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret)
            });

            using var response = await _httpClient.PostAsync(tokenUrl, formContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas OAuth failed: HTTP {(int)response.StatusCode} " +
                    $"tenant={tenantId}, store={storeName}, body={Truncate(errorBody, 200)}");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atProp)
                ? atProp.GetString() : null;
            var expiresIn = root.TryGetProperty("expires_in", out var eiProp)
                ? eiProp.GetInt32() : 14400;

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas OAuth: no access_token in response, tenant={tenantId}");
                return null;
            }

            // Cache with absolute expiry
            var entry = new TokenEntry(accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
            _cache[tenantId] = entry;

            _logger.SystemInfo($"ikas OAuth token acquired: tenant={tenantId}, store={storeName}, expires_in={expiresIn}s");
            return accessToken;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas OAuth HTTP error: tenant={tenantId}, store={storeName}, {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas OAuth response parse failed: tenant={tenantId}, {ex.Message}");
            return null;
        }
    }

    private string? ExtractStoreName(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("store_name", out var prop))
                return prop.GetString();
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOAuthFailed}] ikas ExtractStoreName parse error: {ex.Message}");
        }

        return null;
    }

    private static string Truncate(string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "...";

    public void Dispose()
    {
        foreach (var kvp in _locks)
            kvp.Value.Dispose();
    }
}
