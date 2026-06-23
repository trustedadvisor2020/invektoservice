using System.Text.Json;
using Chatinbox.Shared.DTOs;

namespace Chatinbox.Backend.Services;

/// <summary>
/// HTTP client for VoiceAI microservice (Port 7114).
/// Health checks, endpoint discovery, and multipart proxy for transcription.
/// PKT-11: Voice Message AI.
/// </summary>
public sealed class VoiceAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VoiceAIClient> _logger;

    public VoiceAIClient(HttpClient httpClient, ILogger<VoiceAIClient> logger)
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
            _logger.LogWarning(ex, "VoiceAI health check timeout");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "VoiceAI health check failed");
            return false;
        }
    }

    public async Task<EndpointDiscoveryResponse?> GetEndpointsAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/ops/endpoints", ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<EndpointDiscoveryResponse>(ct);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "VoiceAI endpoint discovery timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "VoiceAI endpoint discovery failed");
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
            _logger.LogWarning(ex, "VoiceAI endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "VoiceAI endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    /// <summary>
    /// Proxy multipart form data to VoiceAI transcribe endpoint.
    /// Forwards the audio file + auth headers directly.
    /// </summary>
    public async Task<(int StatusCode, string? Body)> ProxyTranscribeAsync(
        Stream audioStream, string fileName, string? authHeader, string? requestId,
        CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(audioStream);
            content.Add(streamContent, "audio", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/voice/transcribe")
            {
                Content = content
            };

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
            _logger.LogWarning("VoiceAI proxy timeout: transcribe");
            return (504, JsonSerializer.Serialize(new { error_code = "INV-BE-002", message = "VoiceAI service timeout" }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "VoiceAI proxy failed: transcribe");
            return (502, JsonSerializer.Serialize(new { error_code = "INV-BE-001", message = $"VoiceAI service unavailable: {ex.Message}" }));
        }
    }
}
