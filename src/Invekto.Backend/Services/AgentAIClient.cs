using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for AgentAI microservice.
/// Health checks, endpoint discovery, and suggest/feedback proxy.
/// Uses longer timeout (15s) for suggest endpoint due to Claude API latency.
/// </summary>
public sealed class AgentAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentAIClient> _logger;

    public AgentAIClient(HttpClient httpClient, ILogger<AgentAIClient> logger)
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
            _logger.LogWarning(ex, "AgentAI health check failed");
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
            _logger.LogWarning(ex, "AgentAI endpoint discovery failed");
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
            _logger.LogWarning(ex, "AgentAI endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AgentAI endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    /// <summary>
    /// Proxy suggest request to AgentAI (sync, up to 15s timeout).
    /// Passes JWT token through.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxySuggestAsync(
        string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyPostAsync("/api/v1/suggest", requestBody, authHeader, requestId, ct);
    }

    /// <summary>
    /// Proxy feedback request to AgentAI (fire-and-forget).
    /// Passes JWT token through.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyFeedbackAsync(
        string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyPostAsync("/api/v1/feedback", requestBody, authHeader, requestId, ct);
    }

    private async Task<(int StatusCode, string? Body)> ProxyPostAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
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
            _logger.LogWarning("AgentAI proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "AgentAI service timeout" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AgentAI proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"AgentAI service unavailable: {ex.Message}" }));
        }
    }
}
