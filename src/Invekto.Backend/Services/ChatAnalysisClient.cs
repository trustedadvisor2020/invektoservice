using System.Net.Http.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for ChatAnalysis microservice
/// Stage-0: 600ms timeout, 0 retry
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

    public async Task<ChatAnalysisResult> AnalyzeAsync(RequestContext context, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analyze");
        request.Headers.Add(HeaderNames.RequestId, context.RequestId);
        request.Headers.Add(HeaderNames.TenantId, context.TenantId);
        request.Headers.Add(HeaderNames.ChatId, context.ChatId);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var data = await response.Content.ReadFromJsonAsync<ChatAnalysisResponse>(ct);

                    // Check for null/invalid body
                    if (data == null || string.IsNullOrEmpty(data.Status))
                    {
                        _logger.LogWarning("ChatAnalysis returned invalid/empty body for {RequestId}", context.RequestId);
                        return ChatAnalysisResult.Partial(
                            "Microservice returned invalid response",
                            ErrorCodes.BackendMicroserviceInvalidResponse);
                    }

                    return ChatAnalysisResult.Success(data);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "ChatAnalysis returned malformed JSON for {RequestId}", context.RequestId);
                    return ChatAnalysisResult.Partial(
                        "Microservice returned malformed response",
                        ErrorCodes.BackendMicroserviceInvalidResponse);
                }
            }

            // Non-success status codes
            var statusCode = (int)response.StatusCode;
            _logger.LogWarning("ChatAnalysis returned {StatusCode} for {RequestId}", statusCode, context.RequestId);

            if (statusCode >= 500)
            {
                return ChatAnalysisResult.Partial(
                    $"Microservice error ({statusCode})",
                    ErrorCodes.BackendMicroserviceError);
            }

            // 4xx = client error (different from 5xx server error)
            return ChatAnalysisResult.Partial(
                $"Microservice client error ({statusCode})",
                ErrorCodes.BackendMicroserviceClientError);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("ChatAnalysis timeout for {RequestId}", context.RequestId);
            return ChatAnalysisResult.Partial(
                "Microservice timeout",
                ErrorCodes.BackendMicroserviceTimeout);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ChatAnalysis unavailable for {RequestId}", context.RequestId);
            return ChatAnalysisResult.Partial(
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
        catch
        {
            return false;
        }
    }
}

public sealed class ChatAnalysisResponse
{
    public string? RequestId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ChatAnalysisResult
{
    public bool IsSuccess { get; init; }
    public bool IsPartial { get; init; }
    public ChatAnalysisResponse? Data { get; init; }
    public string? Warning { get; init; }
    public string? ErrorCode { get; init; }

    public static ChatAnalysisResult Success(ChatAnalysisResponse? data) => new()
    {
        IsSuccess = true,
        IsPartial = false,
        Data = data
    };

    public static ChatAnalysisResult Partial(string warning, string errorCode) => new()
    {
        IsSuccess = false,
        IsPartial = true,
        Warning = warning,
        ErrorCode = errorCode
    };
}
