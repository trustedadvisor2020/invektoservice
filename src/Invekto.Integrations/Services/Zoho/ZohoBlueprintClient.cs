// Adim 3 Paket 1: Zoho Blueprint client.
// - GET /crm/v6/Leads/{id}/actions/blueprint  -> list transitions available on the record
// - PUT /crm/v6/Leads/{id}/actions/blueprint  -> execute a transition
// Policy: Blueprint-only (field update fallback forbidden). Missing blueprint -> INV-INT-121.
// Caching: transition metadata cached 10 min per (tenant, leadId) via IMemoryCache.
// Auth: access_token via IZohoTokenProvider; 401 -> InvalidateCache + retry once.
using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly TimeSpan PicklistCacheTtl = TimeSpan.FromMinutes(5);

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

        var transitions = wire?.Blueprint is not null
            ? ExtractTransitions(wire.Blueprint)
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

        // Primary: metadata-based enum via /settings/blueprint (requires ZohoCRM.settings.ALL scope).
        // Returns the complete blueprint graph (all transitions regardless of state).
        var metadataTransitions = await TryGetBlueprintMetadataAsync(tenantId, apiBase, ct).ConfigureAwait(false);
        if (metadataTransitions is { Count: > 0 })
        {
            _cache.Set(cacheKey, metadataTransitions, CacheTtl);
            return (metadataTransitions, FromCache: false);
        }

        // P4.1: Picklist-driven sampling preferred — covers states beyond the latest 50 leads.
        // Falls back to legacy 50-lead grouping when picklist fetch fails.
        IReadOnlyList<string> sampleLeadIds;
        try
        {
            sampleLeadIds = await GetSampleLeadsViaPicklistAsync(tenantId, apiBase, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            sampleLeadIds = await GetSampleLeadsPerStateAsync(tenantId, apiBase, ct).ConfigureAwait(false);
        }

        if (sampleLeadIds.Count == 0)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Leads modulunde hic kayit yok (tenant {tenantId}). Stage mapping editorunu kullanmak icin en az 1 lead olmali.");

        var aggregated = new Dictionary<string, ZohoBlueprintTransition>(StringComparer.Ordinal);
        foreach (var leadId in sampleLeadIds)
        {
            var url = apiBase + "/crm/v6/Leads/" + Uri.EscapeDataString(leadId) + "/actions/blueprint";
            using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

            // TEMP [ZOHO-BP-RAW] diagnostic: log EVERY response (status + body head) BEFORE routing logic.
            var rawBody = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Console.WriteLine($"[ZOHO-BP-RAW] tenant={tenantId} lead={leadId} status={(int)response.StatusCode} len={rawBody.Length} head={Truncate(rawBody, 500)}");

            // 204/404 for this specific lead just means no transitions available from its state; continue.
            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
                continue;

            // 400 RECORD_NOT_IN_PROCESS: lead exists but isn't in the blueprint process (hasn't been
            // triggered into it). Not an error for aggregation — just means no transitions from that
            // lead. Continue to next sample. Any OTHER 400 body is still a hard failure.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                if (rawBody.Contains("RECORD_NOT_IN_PROCESS", StringComparison.Ordinal))
                    continue;
                throw new InvalidOperationException(
                    ZohoErrorCodes.SyncInfrastructureError +
                    $": Zoho blueprint returned 400 for tenant {tenantId} lead {leadId}. Body: {rawBody}");
            }

            if (!response.IsSuccessStatusCode)
                throw await BuildHttpFailureAsync(response, "GET Leads/{id}/actions/blueprint", tenantId, ct).ConfigureAwait(false);

            BlueprintResponseWire? wire;
            try
            {
                wire = JsonSerializer.Deserialize<BlueprintResponseWire>(rawBody);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    ZohoErrorCodes.SyncInfrastructureError +
                    $": Zoho blueprint response was not valid JSON for tenant {tenantId} (lead {leadId}).",
                    ex);
            }

            if (wire?.Blueprint is null)
            {
                Console.WriteLine($"[ZOHO-BP-RAW] tenant={tenantId} lead={leadId} PARSE_FAIL wire.Blueprint=null (shape mismatch?)");
                continue;
            }
            var extracted = ExtractTransitions(wire.Blueprint);
            Console.WriteLine($"[ZOHO-BP-RAW] tenant={tenantId} lead={leadId} extracted_count={extracted.Count}");
            foreach (var t in extracted)
                aggregated[t.TransitionId] = t;  // dedupe by id; later wins (same id == same transition)
        }

        var transitions = (IReadOnlyList<ZohoBlueprintTransition>)aggregated.Values
            .OrderBy(t => t.NextState ?? t.Name, StringComparer.Ordinal)
            .ToList();

        if (transitions.Count == 0)
            throw new InvalidOperationException(
                ZohoErrorCodes.BlueprintNotConfigured +
                $": Zoho Blueprint'ten transition alinamadi (tenant {tenantId}). {sampleLeadIds.Count} farkli state'teki lead denenmesine ragmen blueprint aktif degil olabilir — Zoho Setup -> Automation -> Blueprint kontrolu yapin.");

        _cache.Set(cacheKey, transitions, CacheTtl);
        return (transitions, FromCache: false);
    }

    /// <summary>
    /// P4.1: Lead_Status picklist values from Zoho /crm/v6/settings/fields?module=Leads.
    /// Returns ALL configured states regardless of whether any lead is currently in them.
    /// 5-min cache per tenant. Throws INV-INT-136 if Lead_Status field missing from response.
    /// </summary>
    public async Task<(IReadOnlyList<ZohoLeadStatusInfo> Statuses, bool FromCache)> GetLeadStatusPicklistAsync(
        int tenantId, bool forceRefresh, CancellationToken ct = default)
    {
        var cacheKey = "zoho:lead-statuses:" + tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (forceRefresh)
        {
            _cache.Remove(cacheKey);
        }
        else if (_cache.TryGetValue<IReadOnlyList<ZohoLeadStatusInfo>>(cacheKey, out var cached) && cached is not null)
        {
            return (cached, FromCache: true);
        }

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/settings/fields?module=Leads";

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "GET settings/fields?module=Leads", tenantId, ct).ConfigureAwait(false);

        FieldsResponseWire? wire;
        try
        {
            wire = await response.Content.ReadFromJsonAsync<FieldsResponseWire>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho /settings/fields response was not valid JSON for tenant {tenantId}.",
                ex);
        }

        var leadStatusField = wire?.Fields?.FirstOrDefault(f =>
            string.Equals(f.ApiName, "Lead_Status", StringComparison.Ordinal));

        if (leadStatusField?.PickListValues is null || leadStatusField.PickListValues.Count == 0)
            throw new InvalidOperationException(
                ZohoErrorCodes.LeadStatusFieldNotFound +
                $": Zoho /settings/fields response did not contain Lead_Status picklist values for tenant {tenantId}. Verify Lead_Status field is configured on the Leads module.");

        var statuses = new List<ZohoLeadStatusInfo>(leadStatusField.PickListValues.Count);
        foreach (var v in leadStatusField.PickListValues)
        {
            if (string.IsNullOrEmpty(v.ActualValue)) continue;
            statuses.Add(new ZohoLeadStatusInfo(v.ActualValue, v.DisplayValue ?? v.ActualValue));
        }

        var result = (IReadOnlyList<ZohoLeadStatusInfo>)statuses;
        _cache.Set(cacheKey, result, PicklistCacheTtl);
        return (result, FromCache: false);
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

    /// <summary>
    /// Adim 4 follow-up: Metadata-based blueprint enum. Calls /settings/layouts to get layout_id,
    /// then /settings/blueprint?module=Leads&layout_id=X to get FULL blueprint graph (all transitions,
    /// state-independent). Requires ZohoCRM.settings.ALL scope. Returns null on scope mismatch / 4xx /
    /// shape mismatch so caller can fallback to record-level aggregation.
    /// </summary>
    private async Task<IReadOnlyList<ZohoBlueprintTransition>?> TryGetBlueprintMetadataAsync(
        int tenantId, string apiBase, CancellationToken ct)
    {
        // Step 1: discover first Leads layout id.
        string? layoutId = null;
        try
        {
            var layoutsUrl = apiBase + "/crm/v6/settings/layouts?module=Leads";
            using var layoutsResp = await SendWithAuthAsync(tenantId, HttpMethod.Get, layoutsUrl, content: null, ct).ConfigureAwait(false);
            if (!layoutsResp.IsSuccessStatusCode)
            {
                var errorBody = await layoutsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Console.WriteLine($"[ZOHO-META] layouts FAIL {(int)layoutsResp.StatusCode}: {Truncate(errorBody, 300)}");
                return null;
            }
            var layoutsWire = await layoutsResp.Content.ReadFromJsonAsync<LayoutsResponseWire>(cancellationToken: ct).ConfigureAwait(false);
            layoutId = layoutsWire?.Layouts?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Id))?.Id;
            Console.WriteLine($"[ZOHO-META] layouts OK: layout_id={layoutId}");
        }
        catch (JsonException ex) { Console.WriteLine($"[ZOHO-META] layouts JsonException: {ex.Message}"); return null; }
        if (string.IsNullOrEmpty(layoutId)) { Console.WriteLine("[ZOHO-META] no layout_id returned"); return null; }

        // Step 2: fetch blueprint metadata for that layout.
        string body;
        try
        {
            var url = apiBase + "/crm/v6/settings/blueprint?module=Leads&layout_id=" + Uri.EscapeDataString(layoutId);
            using var resp = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);
            body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Console.WriteLine($"[ZOHO-META] settings/blueprint status={(int)resp.StatusCode} bodyLen={body.Length} bodyHead={Truncate(body, 300)}");
            if (!resp.IsSuccessStatusCode) return null;
        }
        catch (HttpRequestException ex) { Console.WriteLine($"[ZOHO-META] blueprint HttpRequestException: {ex.Message}"); return null; }
        catch (TaskCanceledException ex) { Console.WriteLine($"[ZOHO-META] blueprint TaskCanceledException: {ex.Message}"); return null; }

        // settings/blueprint can return `blueprints: [{transitions:[...]}]` (array) OR
        // `blueprint: {transitions:[...]}` (single, like record API). Parse both shapes.
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var aggregated = new Dictionary<string, ZohoBlueprintTransition>(StringComparer.Ordinal);

            if (root.TryGetProperty("blueprints", out var blueprintsArr) && blueprintsArr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var bp in blueprintsArr.EnumerateArray())
                    CollectTransitionsFromElement(bp, aggregated);
            }
            else if (root.TryGetProperty("blueprint", out var blueprintEl))
            {
                if (blueprintEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var bp in blueprintEl.EnumerateArray())
                        CollectTransitionsFromElement(bp, aggregated);
                else if (blueprintEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                    CollectTransitionsFromElement(blueprintEl, aggregated);
            }

            if (aggregated.Count == 0) return null;
            return aggregated.Values
                .OrderBy(t => t.NextState ?? t.Name, StringComparer.Ordinal)
                .ToList();
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max));

    private static void CollectTransitionsFromElement(
        System.Text.Json.JsonElement bp,
        Dictionary<string, ZohoBlueprintTransition> sink)
    {
        if (!bp.TryGetProperty("transitions", out var transitions) ||
            transitions.ValueKind != System.Text.Json.JsonValueKind.Array) return;

        foreach (var t in transitions.EnumerateArray())
        {
            var id = t.TryGetProperty("id", out var idEl) && idEl.ValueKind == System.Text.Json.JsonValueKind.String ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            var name = t.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == System.Text.Json.JsonValueKind.String ? nameEl.GetString() : id;
            string? nextState = null;
            if (t.TryGetProperty("next_field_value", out var nfv) && nfv.ValueKind == System.Text.Json.JsonValueKind.String)
                nextState = nfv.GetString();
            else if (t.TryGetProperty("next_transition_state", out var nts) &&
                     nts.ValueKind == System.Text.Json.JsonValueKind.Object &&
                     nts.TryGetProperty("name", out var ntsName) &&
                     ntsName.ValueKind == System.Text.Json.JsonValueKind.String)
                nextState = ntsName.GetString();
            sink[id] = new ZohoBlueprintTransition(id, name ?? id, nextState);
        }
    }

    /// <summary>
    /// P4.1: Picklist-driven sample fetch. For each Lead_Status picklist value, runs a COQL probe
    /// (`SELECT id FROM Leads WHERE Lead_Status='X' LIMIT 1`) and collects the first lead per state.
    /// Returns 1 lead id per state that actually has at least one lead. Picklist enum gives broader
    /// coverage than the legacy 50-most-recent grouping.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetSampleLeadsViaPicklistAsync(
        int tenantId, string apiBase, CancellationToken ct)
    {
        var (statuses, _) = await GetLeadStatusPicklistAsync(tenantId, forceRefresh: false, ct).ConfigureAwait(false);
        if (statuses.Count == 0) return Array.Empty<string>();

        var coqlUrl = apiBase + "/crm/v6/coql";
        var result = new List<string>(statuses.Count);
        foreach (var s in statuses)
        {
            var escaped = s.Value.Replace("'", "''", StringComparison.Ordinal);
            var query = $"select id from Leads where Lead_Status = '{escaped}' limit 1";
            var payload = JsonSerializer.Serialize(new { select_query = query });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await SendWithAuthAsync(tenantId, HttpMethod.Post, coqlUrl, content, ct).ConfigureAwait(false);

            // Probe semantics: best-effort per state. Each non-success path is logged with
            // [ZOHO-PROBE] prefix so silent-degradation is observable in logs (CQ2 visibility).
            // 204 NoContent = expected case (state has no leads); informational.
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                Console.WriteLine($"[ZOHO-PROBE] tenant={tenantId} state='{s.Value}' result=no_rows (HTTP 204)");
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ZOHO-PROBE] tenant={tenantId} state='{s.Value}' result=http_fail status={(int)response.StatusCode} reason={response.ReasonPhrase}");
                continue;
            }

            CoqlResponseWire? wire;
            try
            {
                wire = await response.Content.ReadFromJsonAsync<CoqlResponseWire>(cancellationToken: ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ZOHO-PROBE] tenant={tenantId} state='{s.Value}' result=parse_fail err={ex.Message}");
                continue;
            }

            var id = wire?.Data?.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(id))
            {
                Console.WriteLine($"[ZOHO-PROBE] tenant={tenantId} state='{s.Value}' result=empty_data (200 but no id)");
                continue;
            }
            result.Add(id);
        }
        return result;
    }

    // Fallback helper (kept for when /settings/blueprint unavailable):
    /// <summary>
    /// Adim 4: Sample one lead id per distinct Lead_Status value (max 50 leads fetched).
    /// Returns up to N lead ids (one per unique state) so blueprint aggregation can discover all transitions.
    /// Returns empty list if module has no leads.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetSampleLeadsPerStateAsync(int tenantId, string apiBase, CancellationToken ct)
    {
        var url = apiBase + "/crm/v6/Leads?per_page=50&fields=id,Lead_Status";
        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<string>();

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "GET Leads?per_page=50", tenantId, ct).ConfigureAwait(false);

        LeadsListWire? wire;
        try
        {
            wire = await response.Content.ReadFromJsonAsync<LeadsListWire>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho Leads list response was not valid JSON for tenant {tenantId}.",
                ex);
        }

        if (wire?.Data is null || wire.Data.Count == 0)
            return Array.Empty<string>();

        // Group by Lead_Status (null-safe); pick first lead per distinct state. Leads without a state
        // also contribute one sample (blueprint pre-start transitions may exist).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var lead in wire.Data)
        {
            if (string.IsNullOrEmpty(lead.Id)) continue;
            var stateKey = lead.LeadStatus ?? "__null__";
            if (!seen.Add(stateKey)) continue;
            result.Add(lead.Id);
        }
        return result;
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

        // Diagnostic: for 401/403 Zoho often returns empty body; dump ALL response headers.
        var hdrParts = new List<string>();
        foreach (var h in response.Headers)
            hdrParts.Add(h.Key + "=" + string.Join("|", h.Value));
        foreach (var h in response.Content.Headers)
            hdrParts.Add(h.Key + "=" + string.Join("|", h.Value));
        var hdr = hdrParts.Count > 0 ? $" Headers: {{{string.Join("; ", hdrParts)}}}" : string.Empty;

        return new InvalidOperationException(
            $"INV-INT-125: Zoho API {op} returned {(int)response.StatusCode} {response.ReasonPhrase} (tenant {tenantId}). Body: {body}{hdr}");
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
            list.Add(new ZohoBlueprintTransition(t.Id, t.Name ?? t.Id, t.NextFieldValue ?? t.NextTransitionState?.Name));
        }
        return list;
    }

    // Wire contracts — Zoho Blueprint API response shape (v6).
    // GET /Leads/{id}/actions/blueprint returns `blueprint` as SINGLE OBJECT, not an array.
    // (Confirmed via live Zoho response on dev edition: {"blueprint":{"process_info":{...},"transitions":[...]}}.)
    private sealed class BlueprintResponseWire
    {
        [JsonPropertyName("blueprint")] public BlueprintWire? Blueprint { get; set; }
    }

    private sealed class BlueprintWire
    {
        [JsonPropertyName("transitions")] public List<TransitionWire>? Transitions { get; set; }
    }

    // Adim 4: Zoho v6 /crm/v6/settings/layouts?module=Leads response shape.
    private sealed class LayoutsResponseWire
    {
        [JsonPropertyName("layouts")] public List<LayoutWire>? Layouts { get; set; }
    }

    private sealed class LayoutWire
    {
        [JsonPropertyName("id")]   public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    // Adim 4: Zoho v6 /crm/v6/Leads?per_page=1&fields=id response shape (sample lead).
    private sealed class LeadsListWire
    {
        [JsonPropertyName("data")] public List<LeadIdWire>? Data { get; set; }
    }

    private sealed class LeadIdWire
    {
        [JsonPropertyName("id")]          public string? Id { get; set; }
        [JsonPropertyName("Lead_Status")] public string? LeadStatus { get; set; }
    }

    private sealed class TransitionWire
    {
        [JsonPropertyName("id")]                     public string? Id { get; set; }
        [JsonPropertyName("name")]                   public string? Name { get; set; }
        [JsonPropertyName("next_transitions")]       public List<TransitionWire>? NextTransitions { get; set; }
        // Zoho v6 returns next_field_value as a direct string on the transition object
        // (see live response; legacy NextTransitionState.name kept for back-compat but usually absent).
        [JsonPropertyName("next_field_value")]       public string? NextFieldValue { get; set; }
        [JsonPropertyName("next_transition_state")]  public NextStateWire? NextTransitionState { get; set; }
    }

    private sealed class NextStateWire
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    // P4.1: Zoho v6 /crm/v6/settings/fields?module=Leads response shape (subset).
    private sealed class FieldsResponseWire
    {
        [JsonPropertyName("fields")] public List<FieldWire>? Fields { get; set; }
    }

    private sealed class FieldWire
    {
        [JsonPropertyName("api_name")]         public string? ApiName { get; set; }
        [JsonPropertyName("pick_list_values")] public List<PickListValueWire>? PickListValues { get; set; }
    }

    private sealed class PickListValueWire
    {
        [JsonPropertyName("actual_value")]  public string? ActualValue { get; set; }
        [JsonPropertyName("display_value")] public string? DisplayValue { get; set; }
    }

    // P4.1: Zoho v6 /crm/v6/coql response shape (subset; only id needed for sample probe).
    private sealed class CoqlResponseWire
    {
        [JsonPropertyName("data")] public List<CoqlRowWire>? Data { get; set; }
    }

    private sealed class CoqlRowWire
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
