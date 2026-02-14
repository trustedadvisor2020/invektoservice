using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for Knowledge microservice (Port 7104).
/// Health checks, endpoint discovery, test proxy, and generic HTTP proxy.
/// Phase B: Full proxy for Dashboard -> Backend -> Knowledge bridge.
/// </summary>
public sealed class KnowledgeClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KnowledgeClient> _logger;

    public KnowledgeClient(HttpClient httpClient, ILogger<KnowledgeClient> logger)
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge health check failed");
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge endpoint discovery failed");
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
            _logger.LogWarning(ex, "Knowledge endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Knowledge endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    // ============================================================
    // Generic HTTP proxy (Phase B)
    // ============================================================

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

    public async Task<(int StatusCode, string? Body)> ProxyPutAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Put, path, requestBody, authHeader, requestId, ct);
    }

    public async Task<(int StatusCode, string? Body)> ProxyDeleteAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Delete, path, null, authHeader, requestId, ct);
    }

    /// <summary>
    /// Upload proxy - forwards multipart/form-data to Knowledge service.
    /// Used for PDF document uploads (Dashboard -> Backend -> Knowledge).
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyUploadAsync(
        string path, Stream fileStream, string fileName, string? title,
        string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(streamContent, "file", fileName);

            if (!string.IsNullOrEmpty(title))
                content.Add(new StringContent(title), "title");

            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };

            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);

            if (!string.IsNullOrEmpty(requestId))
                request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return ((int)response.StatusCode, body);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Knowledge upload proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "Knowledge service timeout" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge upload proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"Knowledge service unavailable: {ex.Message}" }));
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
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Knowledge proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "Knowledge service timeout" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"Knowledge service unavailable: {ex.Message}" }));
        }
    }
}
