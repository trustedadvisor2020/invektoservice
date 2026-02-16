using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for WhatsApp Analytics microservice (Port 7109).
/// Health checks, endpoint discovery, and generic HTTP proxy.
/// PKT-4: Proxies upload, analysis CRUD, and NLP query endpoints.
/// </summary>
public sealed class WhatsAppAnalyticsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppAnalyticsClient> _logger;

    public WhatsAppAnalyticsClient(HttpClient httpClient, ILogger<WhatsAppAnalyticsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics health check failed: connection error");
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics health check timed out");
            return false;
        }
    }

    public async Task<EndpointDiscoveryResponse?> GetEndpointsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/ops/endpoints", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EndpointDiscoveryResponse>(ct);
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics endpoint discovery failed: connection error");
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics endpoint discovery timed out");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics endpoint discovery: invalid JSON response");
            return null;
        }
    }

    public async Task<TestEndpointResult> TestEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct);
            return new TestEndpointResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode ? "OK" : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    public async Task<(int StatusCode, string? Body)> ProxyGetAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Get, path, null, authHeader, requestId, ct);
    }

    public async Task<(int StatusCode, string? Body)> ProxyPostAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Post, path, requestBody, authHeader, requestId, ct);
    }

    public async Task<(int StatusCode, string? Body)> ProxyDeleteAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Delete, path, null, authHeader, requestId, ct);
    }

    /// <summary>
    /// Upload proxy - forwards multipart/form-data to WA Analytics service.
    /// Used for CSV file uploads (Dashboard -> Backend -> WA Analytics).
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyUploadAsync(
        string path, Stream fileStream, string fileName, char delimiter, string? configJson,
        string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent(delimiter.ToString()), "delimiter");

            if (!string.IsNullOrEmpty(configJson))
                content.Add(new StringContent(configJson), "config");

            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };

            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);

            if (!string.IsNullOrEmpty(requestId))
                request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return ((int)response.StatusCode, body);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("WhatsApp Analytics upload proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "WhatsApp Analytics service timeout" }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics upload proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"WhatsApp Analytics service unavailable: {ex.Message}" }));
        }
    }

    private async Task<(int StatusCode, string? Body)> ProxyRequestAsync(
        HttpMethod method, string path, string? requestBody, string? authHeader, string? requestId,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);

            if (requestBody != null)
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);

            if (!string.IsNullOrEmpty(requestId))
                request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return ((int)response.StatusCode, body);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("WhatsApp Analytics proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "WhatsApp Analytics service timeout" }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WhatsApp Analytics proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"WhatsApp Analytics service unavailable: {ex.Message}" }));
        }
    }
}
