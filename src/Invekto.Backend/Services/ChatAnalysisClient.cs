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
                var data = await response.Content.ReadFromJsonAsync<ChatAnalysisResponse>(ct);
                return ChatAnalysisResult.Success(data);
            }

            _logger.LogWarning("ChatAnalysis returned {StatusCode} for {RequestId}",
                response.StatusCode, context.RequestId);

            return ChatAnalysisResult.Partial($"Microservice returned {response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("ChatAnalysis timeout for {RequestId}", context.RequestId);
            return ChatAnalysisResult.Partial("Microservice timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ChatAnalysis unavailable for {RequestId}", context.RequestId);
            return ChatAnalysisResult.Partial("Microservice unavailable");
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

    public static ChatAnalysisResult Success(ChatAnalysisResponse? data) => new()
    {
        IsSuccess = true,
        IsPartial = false,
        Data = data
    };

    public static ChatAnalysisResult Partial(string warning) => new()
    {
        IsSuccess = false,
        IsPartial = true,
        Warning = warning
    };
}
