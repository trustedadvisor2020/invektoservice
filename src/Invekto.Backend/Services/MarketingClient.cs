using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for Marketing microservice (Port 7112).
/// Health checks, endpoint discovery, and generic HTTP proxy for all Marketing endpoints.
/// GR-3.21: Google Yorum + Referans Motoru.
/// GR-3.22: Medikal Turizm Lead Capture.
/// </summary>
public sealed class MarketingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketingClient> _logger;

    public MarketingClient(HttpClient httpClient, ILogger<MarketingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Marketing health check timeout");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Marketing health check failed");
            return false;
        }
    }

    public async Task<EndpointDiscoveryResponse?> GetEndpointsAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/ops/endpoints", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EndpointDiscoveryResponse>(ct);
            }
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Marketing endpoint discovery timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Marketing endpoint discovery failed");
            return null;
        }
    }

    public async Task<TestEndpointResult> TestEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, ct);
            return new TestEndpointResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode ? "OK" : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Marketing endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Marketing endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    public async Task<(int StatusCode, string? Body)> ProxyPostAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Post, path, requestBody, authHeader, requestId, ct);
    }

    public async Task<(int StatusCode, string? Body)> ProxyGetAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Get, path, null, authHeader, requestId, ct);
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
            _logger.LogWarning("Marketing proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "Marketing service timeout" }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Marketing proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"Marketing service unavailable: {ex.Message}" }));
        }
    }
}
