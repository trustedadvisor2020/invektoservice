using System.Text;
using System.Text.Json;
using Chatinbox.Shared.DTOs;

namespace Chatinbox.Backend.Services;

/// <summary>
/// HTTP client for Outbound microservice (Port 7107).
/// Health checks, endpoint discovery, and generic HTTP proxy for all Outbound endpoints.
/// GR-1.3: Broadcast, trigger, template, opt-out proxy.
/// </summary>
public sealed class OutboundClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OutboundClient> _logger;

    public OutboundClient(HttpClient httpClient, ILogger<OutboundClient> logger)
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
            _logger.LogWarning(ex, "Outbound health check failed");
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
            _logger.LogWarning(ex, "Outbound endpoint discovery failed");
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
            _logger.LogWarning(ex, "Outbound endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Outbound endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    /// <summary>
    /// Generic POST proxy - forwards body + auth + request-id to Outbound.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyPostAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Post, path, requestBody, authHeader, requestId, ct);
    }

    /// <summary>
    /// Generic GET proxy - forwards auth + request-id to Outbound.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyGetAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Get, path, null, authHeader, requestId, ct);
    }

    /// <summary>
    /// Generic PUT proxy - forwards body + auth + request-id to Outbound.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyPutAsync(
        string path, string requestBody, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Put, path, requestBody, authHeader, requestId, ct);
    }

    /// <summary>
    /// Generic DELETE proxy - forwards auth + request-id to Outbound.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyDeleteAsync(
        string path, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        return await ProxyRequestAsync(HttpMethod.Delete, path, null, authHeader, requestId, ct);
    }

    /// <summary>
    /// Streaming GET proxy for file downloads (FEAT-OBI Plan B export). Returns the upstream
    /// HttpResponseMessage with HttpCompletionOption.ResponseHeadersRead so the BODY is NOT
    /// buffered in Backend memory — a 50k-row XLSX/CSV streams straight through. The caller
    /// MUST dispose the returned message. NOTE: with ResponseHeadersRead, HttpClient.Timeout
    /// governs only time-to-headers; the body copy is bounded by the caller's CancellationToken.
    /// Throws (TaskCanceledException/HttpRequestException) on transport failure — the caller maps it.
    /// </summary>
    public async Task<HttpResponseMessage> ProxyStreamGetAsync(
        string path, string? authHeader, string? requestId, CancellationToken ct = default)
    {
        // GET has no request content, so disposing the request after headers are read does
        // not affect the response body stream (the response owns the connection). Disposing
        // here keeps the IDisposable lifecycle correct under concurrent downloads.
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrEmpty(authHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        if (!string.IsNullOrEmpty(requestId))
            request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    /// <summary>
    /// Streaming POST proxy (FEAT-OBI Phase 2 phone-history download). Same body-not-buffered
    /// contract as <see cref="ProxyStreamGetAsync"/>, but the lookup key (a PII phone number) travels
    /// in the POST body, never a querystring. Forwards whatever Outbound returns — CSV bytes OR the
    /// PDF-data JSON — straight through (the caller passes the upstream Content-Type to the client).
    /// The request (with its body) is already sent by the time headers are read, so disposing it here
    /// does not affect the response body stream (the response owns the connection). Caller MUST dispose
    /// the returned message. Throws on transport failure — the caller maps it.
    /// </summary>
    public async Task<HttpResponseMessage> ProxyStreamPostAsync(
        string path, string requestBody, string? authHeader, string? requestId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(authHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        if (!string.IsNullOrEmpty(requestId))
            request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
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
            _logger.LogWarning("Outbound proxy timeout: {Path}", path);
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "Outbound service timeout" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outbound proxy failed: {Path}", path);
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"Outbound service unavailable: {ex.Message}" }));
        }
    }
}
