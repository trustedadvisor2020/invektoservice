using System.Net.Http.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for ChatAnalysis microservice (V2)
/// Submits analysis requests - results come via callback
/// </summary>
public sealed class ChatAnalysisClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatAnalysisClient> _logger;

    public ChatAnalysisClient(HttpClient httpClient, ILogger<ChatAnalysisClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Submit analysis request to ChatAnalysis service
    /// Returns immediately with accepted status - actual result comes via callback
    /// </summary>
    public async Task<ChatAnalysisSubmitResult> SubmitAnalysisAsync(
        RequestContext context,
        ChatAnalysisRequest analysisRequest,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analyze");
            request.Headers.Add(HeaderNames.RequestId, context.RequestId);
            request.Headers.Add(HeaderNames.TenantId, context.TenantId);
            request.Headers.Add(HeaderNames.ChatId, context.ChatId);
            request.Content = JsonContent.Create(analysisRequest);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var data = await response.Content.ReadFromJsonAsync<ChatAnalysisAcceptedResponse>(ct);

                    if (data == null || string.IsNullOrEmpty(data.RequestID))
                    {
                        _logger.LogWarning("ChatAnalysis returned invalid/empty body for {RequestId}", context.RequestId);
                        return ChatAnalysisSubmitResult.Failed(
                            "Microservice returned invalid response",
                            ErrorCodes.BackendMicroserviceInvalidResponse);
                    }

                    return ChatAnalysisSubmitResult.Success(data);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "ChatAnalysis returned malformed JSON for {RequestId}", context.RequestId);
                    return ChatAnalysisSubmitResult.Failed(
                        "Microservice returned malformed response",
                        ErrorCodes.BackendMicroserviceInvalidResponse);
                }
            }

            var statusCode = (int)response.StatusCode;
            _logger.LogWarning("ChatAnalysis returned {StatusCode} for {RequestId}", statusCode, context.RequestId);

            if (statusCode >= 500)
            {
                return ChatAnalysisSubmitResult.Failed(
                    $"Microservice error ({statusCode})",
                    ErrorCodes.BackendMicroserviceError);
            }

            return ChatAnalysisSubmitResult.Failed(
                $"Microservice client error ({statusCode})",
                ErrorCodes.BackendMicroserviceClientError);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("ChatAnalysis timeout for {RequestId}", context.RequestId);
            return ChatAnalysisSubmitResult.Failed(
                "Microservice timeout",
                ErrorCodes.BackendMicroserviceTimeout);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ChatAnalysis unavailable for {RequestId}", context.RequestId);
            return ChatAnalysisSubmitResult.Failed(
                "Microservice unavailable",
                ErrorCodes.BackendMicroserviceUnavailable);
        }
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
            _logger.LogWarning(ex, "ChatAnalysis health check failed");
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
            _logger.LogWarning(ex, "ChatAnalysis endpoint discovery failed");
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
            _logger.LogWarning(ex, "ChatAnalysis endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ChatAnalysis endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }
}

public sealed class TestEndpointResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Result of submitting analysis request
/// </summary>
public sealed class ChatAnalysisSubmitResult
{
    public bool IsSuccess { get; init; }
    public ChatAnalysisAcceptedResponse? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static ChatAnalysisSubmitResult Success(ChatAnalysisAcceptedResponse data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ChatAnalysisSubmitResult Failed(string errorMessage, string errorCode) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
