// Adim 3 Paket 1: Zoho Blueprint client.
// - GET /crm/v6/Leads/{id}/actions/blueprint  -> list transitions available on the record
// - PUT /crm/v6/Leads/{id}/actions/blueprint  -> execute a transition
// Policy: Blueprint-only (field update fallback forbidden). Missing blueprint -> INV-INT-121.
// Caching: transition metadata cached 10 min per (tenant, leadId) via IMemoryCache.
// Auth: access_token via IZohoTokenProvider; 401 -> InvalidateCache + retry once.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Microsoft.Extensions.Caching.Memory;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoBlueprintClient : IZohoBlueprintClient
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly IZohoTokenProvider _tokenProvider;
    private readonly ZohoConnectionRepository _connectionRepo;
    private readonly IMemoryCache _cache;

    public ZohoBlueprintClient(
        HttpClient httpClient,
        IZohoTokenProvider tokenProvider,
        ZohoConnectionRepository connectionRepo,
        IMemoryCache cache)
    {
        _httpClient     = httpClient;
        _tokenProvider  = tokenProvider;
        _connectionRepo = connectionRepo;
        _cache          = cache;
    }

    public async Task<IReadOnlyList<ZohoBlueprintTransition>> GetLeadTransitionsAsync(
        int tenantId, string zohoLeadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zohoLeadId))
            throw new ArgumentException("zohoLeadId is required", nameof(zohoLeadId));

        var cacheKey = CacheKey(tenantId, zohoLeadId);
        if (_cache.TryGetValue<IReadOnlyList<ZohoBlueprintTransition>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/Leads/" + Uri.EscapeDataString(zohoLeadId) + "/actions/blueprint";

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Leads Blueprint not configured for tenant {tenantId}. Activate a Blueprint on the Leads module in Zoho Setup -> Automation -> Blueprint.");

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "GET blueprint", tenantId, ct).ConfigureAwait(false);

        BlueprintResponseWire? wire;
        try
        {
            wire = await response.Content.ReadFromJsonAsync<BlueprintResponseWire>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            // Parse failure is infrastructure, not a business configuration problem.
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho Blueprint response was not valid JSON for tenant {tenantId}.",
                ex);
        }

        var transitions = wire?.Blueprint?.Count > 0
            ? ExtractTransitions(wire.Blueprint[0])
            : Array.Empty<ZohoBlueprintTransition>();

        if (transitions.Count == 0)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Blueprint has no transitions for lead {zohoLeadId} (tenant {tenantId}). Verify the Blueprint definition on the Leads module.");

        _cache.Set(cacheKey, transitions, CacheTtl);
        return transitions;
    }

    /// <summary>
    /// Adim 4: Module-level Blueprint transitions (editor dropdown). Calls
    /// GET /crm/v6/settings/blueprint?module=Leads and returns all transitions in the blueprint.
    /// Cache key is distinct from lead-scoped GetLeadTransitionsAsync (zoho:bp:all:{tid}).
    /// </summary>
    public async Task<(IReadOnlyList<ZohoBlueprintTransition> Transitions, bool FromCache)> GetAllBlueprintTransitionsAsync(
        int tenantId, bool forceRefresh, CancellationToken ct = default)
    {
        var cacheKey = "zoho:bp:all:" + tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (forceRefresh)
        {
            _cache.Remove(cacheKey);
        }
        else if (_cache.TryGetValue<IReadOnlyList<ZohoBlueprintTransition>>(cacheKey, out var cached) && cached is not null)
        {
            return (cached, FromCache: true);
        }

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/settings/blueprint?module=Leads";

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Leads Blueprint not configured for tenant {tenantId}. Activate a Blueprint on the Leads module in Zoho Setup -> Automation -> Blueprint.");

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "GET settings/blueprint", tenantId, ct).ConfigureAwait(false);

        BlueprintResponseWire? wire;
        try
        {
            wire = await response.Content.ReadFromJsonAsync<BlueprintResponseWire>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho settings/blueprint response was not valid JSON for tenant {tenantId}.",
                ex);
        }

        var transitions = wire?.Blueprint?.Count > 0
            ? ExtractTransitions(wire.Blueprint[0])
            : Array.Empty<ZohoBlueprintTransition>();

        if (transitions.Count == 0)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Blueprint has no transitions for Leads module (tenant {tenantId}). Verify the Blueprint definition.");

        _cache.Set(cacheKey, transitions, CacheTtl);
        return (transitions, FromCache: false);
    }

    public async Task ExecuteTransitionAsync(
        int tenantId, string zohoLeadId, string transitionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zohoLeadId))
            throw new ArgumentException("zohoLeadId is required", nameof(zohoLeadId));
        if (string.IsNullOrWhiteSpace(transitionId))
            throw new ArgumentException("transitionId is required", nameof(transitionId));

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/Leads/" + Uri.EscapeDataString(zohoLeadId) + "/actions/blueprint";

        var payload = new { blueprint = new[] { new { transition_id = transitionId } } };
        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Put, url, content, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintTransitionNotFound +
                $": Zoho Blueprint transition '{transitionId}' not found for lead {zohoLeadId} (tenant {tenantId}). " +
                "The Blueprint definition may have changed; re-run stage mapping discovery.");

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException(
                ZohoErrorCodes.RateLimitReached +
                $": Zoho rate limit reached while executing blueprint transition (tenant {tenantId}). Retry after 60s.");

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "PUT blueprint transition", tenantId, ct).ConfigureAwait(false);

        // Invalidate cached transitions for this lead: available set changes after a transition commits.
        _cache.Remove(CacheKey(tenantId, zohoLeadId));
    }

    private async Task<string> GetApiBaseAsync(int tenantId, CancellationToken ct)
    {
        var connection = await _connectionRepo.GetActiveAsync(tenantId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ZohoErrorCodes.ConnectionNotFound +
                $": No active Zoho connection for tenant {tenantId}. Tenant must connect via /api/v1/zoho/connect-url.");

        // api_domain is stored host-only (e.g. www.zohoapis.eu); prepend scheme.
        return "https://" + connection.ApiDomain.TrimEnd('/');
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(
        int tenantId, HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var response = await SendOnceAsync(tenantId, method, url, content, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Access token likely expired between cache check and call; invalidate + retry once.
            response.Dispose();
            _tokenProvider.InvalidateCache(tenantId);
            response = await SendOnceAsync(tenantId, method, url, content, ct).ConfigureAwait(false);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        int tenantId, HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(tenantId, ct).ConfigureAwait(false);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);
        if (content is not null)
            request.Content = content;

        try
        {
            return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"INV-INT-125: Zoho API network failure ({method} {url}) for tenant {tenantId}.",
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                $"INV-INT-125: Zoho API call timed out ({method} {url}) for tenant {tenantId}.",
                ex);
        }
    }

    private static async Task<InvalidOperationException> BuildHttpFailureAsync(
        HttpResponseMessage response, string op, int tenantId, CancellationToken ct)
    {
        string body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        // Typed catches only (project rule): body read is best-effort for diagnostics.
        catch (HttpRequestException) { body = "<unreadable: transport error>"; }
        catch (System.IO.IOException) { body = "<unreadable: io error>"; }
        catch (TaskCanceledException) { body = "<unreadable: canceled>"; }

        if (body.Length > 512) body = body.Substring(0, 512);

        return new InvalidOperationException(
            $"INV-INT-125: Zoho API {op} returned {(int)response.StatusCode} {response.ReasonPhrase} (tenant {tenantId}). Body: {body}");
    }

    private static string CacheKey(int tenantId, string leadId) =>
        "zoho:bp:" + tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + leadId;

    private static IReadOnlyList<ZohoBlueprintTransition> ExtractTransitions(BlueprintWire bp)
    {
        if (bp.Transitions is null || bp.Transitions.Count == 0)
            return Array.Empty<ZohoBlueprintTransition>();

        var list = new List<ZohoBlueprintTransition>(bp.Transitions.Count);
        foreach (var t in bp.Transitions)
        {
            if (string.IsNullOrEmpty(t.Id)) continue;
            list.Add(new ZohoBlueprintTransition(t.Id, t.Name ?? t.Id, t.NextTransitionState?.Name));
        }
        return list;
    }

    // Wire contracts — Zoho Blueprint API response shape (v6).
    private sealed class BlueprintResponseWire
    {
        [JsonPropertyName("blueprint")] public List<BlueprintWire>? Blueprint { get; set; }
    }

    private sealed class BlueprintWire
    {
        [JsonPropertyName("transitions")] public List<TransitionWire>? Transitions { get; set; }
    }

    private sealed class TransitionWire
    {
        [JsonPropertyName("id")]                     public string? Id { get; set; }
        [JsonPropertyName("name")]                   public string? Name { get; set; }
        [JsonPropertyName("next_transitions")]       public List<TransitionWire>? NextTransitions { get; set; }
        [JsonPropertyName("next_transition_state")]  public NextStateWire? NextTransitionState { get; set; }
    }

    private sealed class NextStateWire
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
