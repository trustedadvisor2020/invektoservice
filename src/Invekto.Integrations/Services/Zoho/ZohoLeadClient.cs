// Adim 3 Paket 1: Zoho Lead client.
// Scope: Get (read by id) + Create (POST /crm/v6/Leads). Update/Delete postponed to Adim 4.
// Auth: IZohoTokenProvider; 401 -> InvalidateCache + retry once.
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
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoLeadClient : IZohoLeadClient
{
    private readonly HttpClient _httpClient;
    private readonly IZohoTokenProvider _tokenProvider;
    private readonly ZohoConnectionRepository _connectionRepo;

    public ZohoLeadClient(
        HttpClient httpClient,
        IZohoTokenProvider tokenProvider,
        ZohoConnectionRepository connectionRepo)
    {
        _httpClient     = httpClient;
        _tokenProvider  = tokenProvider;
        _connectionRepo = connectionRepo;
    }

    public async Task<ZohoLeadRecord> GetAsync(int tenantId, string zohoLeadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zohoLeadId))
            throw new ArgumentException("zohoLeadId is required", nameof(zohoLeadId));

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/Leads/" + Uri.EscapeDataString(zohoLeadId);

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Get, url, content: null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            throw new InvalidOperationException(
                ZohoErrorCodes.LeadNotFound +
                $": Zoho Lead '{zohoLeadId}' not found for tenant {tenantId}. Verify the id or recreate in Zoho CRM.");

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "GET Lead", tenantId, ct).ConfigureAwait(false);

        LeadResponseWire? wire;
        try
        {
            wire = await response.Content
                .ReadFromJsonAsync<LeadResponseWire>(cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho Lead GET response was not valid JSON (tenant {tenantId}).", ex);
        }

        var first = wire?.Data?.Count > 0 ? wire.Data[0] : null;
        if (first is null || string.IsNullOrEmpty(first.Id))
            throw new InvalidOperationException(
                ZohoErrorCodes.LeadNotFound +
                $": Zoho Lead '{zohoLeadId}' response contained no data (tenant {tenantId}).");

        return new ZohoLeadRecord(first.Id, first.FullName, first.Email, first.Phone);
    }

    public async Task<string> CreateAsync(int tenantId, ZohoLeadFields fields, CancellationToken ct = default)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));
        if (string.IsNullOrWhiteSpace(fields.LastName))
            throw new ArgumentException("Zoho requires Last_Name for Lead creation", nameof(fields));

        var apiBase = await GetApiBaseAsync(tenantId, ct).ConfigureAwait(false);
        var url = apiBase + "/crm/v6/Leads";

        var payload = new
        {
            data = new[]
            {
                new
                {
                    First_Name = fields.FirstName,
                    Last_Name  = fields.LastName,
                    Email      = fields.Email,
                    Phone      = fields.Phone,
                    Company    = fields.Company
                }
            }
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await SendWithAuthAsync(tenantId, HttpMethod.Post, url, content, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException(
                ZohoErrorCodes.RateLimitReached +
                $": Zoho rate limit reached while creating lead (tenant {tenantId}).");

        if (!response.IsSuccessStatusCode)
            throw await BuildHttpFailureAsync(response, "POST Lead", tenantId, ct).ConfigureAwait(false);

        LeadCreateResponseWire? wire;
        try
        {
            wire = await response.Content
                .ReadFromJsonAsync<LeadCreateResponseWire>(cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                $": Zoho POST Lead response was not valid JSON (tenant {tenantId}).", ex);
        }

        var first = wire?.Data?.Count > 0 ? wire.Data[0] : null;
        if (first?.Details is null || string.IsNullOrEmpty(first.Details.Id))
            throw new InvalidOperationException(
                ZohoErrorCodes.SyncInfrastructureError +
                ": Zoho POST Lead response missing details.id (tenant " + tenantId + ").");

        return first.Details.Id;
    }

    private async Task<string> GetApiBaseAsync(int tenantId, CancellationToken ct)
    {
        var connection = await _connectionRepo.GetActiveAsync(tenantId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ZohoErrorCodes.ConnectionNotFound +
                $": No active Zoho connection for tenant {tenantId}.");
        return "https://" + connection.ApiDomain.TrimEnd('/');
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(
        int tenantId, HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var response = await SendOnceAsync(tenantId, method, url, content, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
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
        if (content is not null) request.Content = content;
        try
        {
            return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"INV-INT-125: Zoho API network failure ({method} {url}) for tenant {tenantId}.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                $"INV-INT-125: Zoho API call timed out ({method} {url}) for tenant {tenantId}.", ex);
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

    // Wire models — Zoho CRM v6 Lead GET/POST response shapes.
    private sealed class LeadResponseWire
    {
        [JsonPropertyName("data")] public List<LeadWire>? Data { get; set; }
    }

    private sealed class LeadWire
    {
        [JsonPropertyName("id")]        public string? Id { get; set; }
        [JsonPropertyName("Full_Name")] public string? FullName { get; set; }
        [JsonPropertyName("Email")]     public string? Email { get; set; }
        [JsonPropertyName("Phone")]     public string? Phone { get; set; }
    }

    private sealed class LeadCreateResponseWire
    {
        [JsonPropertyName("data")] public List<LeadCreateEntryWire>? Data { get; set; }
    }

    private sealed class LeadCreateEntryWire
    {
        [JsonPropertyName("code")]    public string? Code { get; set; }
        [JsonPropertyName("status")]  public string? Status { get; set; }
        [JsonPropertyName("details")] public LeadCreateDetailsWire? Details { get; set; }
    }

    private sealed class LeadCreateDetailsWire
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
