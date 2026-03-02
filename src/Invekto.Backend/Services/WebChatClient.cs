using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.DTOs;

namespace Invekto.Backend.Services;

/// <summary>
/// HTTP client for WebChat microservice (Port 7113).
/// Health checks, endpoint discovery, test endpoint, and operator proxy (internal API key auth).
/// </summary>
public sealed class WebChatClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebChatClient> _logger;
    private readonly string _internalApiKey;

    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public WebChatClient(HttpClient httpClient, ILogger<WebChatClient> logger, string internalApiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _internalApiKey = internalApiKey;
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
            _logger.LogWarning(ex, "WebChat health check timeout");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat health check failed");
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
            _logger.LogWarning(ex, "WebChat endpoint discovery timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat endpoint discovery failed");
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
            _logger.LogWarning(ex, "WebChat endpoint test timeout: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat endpoint test failed: {Endpoint}", endpoint);
            return new TestEndpointResult { Success = false, StatusCode = 0, Message = ex.Message };
        }
    }

    // ================================================================
    // Operator proxy methods (uses X-Internal-Key for server-to-server auth)
    // ================================================================

    private HttpRequestMessage WithInternalKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_internalApiKey))
            request.Headers.Add("X-Internal-Key", _internalApiKey);
        return request;
    }

    public async Task<WebChatConversationsResponse?> GetConversationsAsync(CancellationToken ct = default)
    {
        try
        {
            var request = WithInternalKey(new HttpRequestMessage(HttpMethod.Get, "/api/v1/operator/conversations"));
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<WebChatConversationsResponse>(SnakeCase, ct);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "WebChat get conversations timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat get conversations failed");
            return null;
        }
    }

    public async Task<WebChatMessagesResponse?> GetMessagesAsync(long conversationId, CancellationToken ct = default)
    {
        try
        {
            var request = WithInternalKey(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/conversations/{conversationId}/messages"));
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<WebChatMessagesResponse>(SnakeCase, ct);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "WebChat get messages timeout for conversation {Id}", conversationId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat get messages failed for conversation {Id}", conversationId);
            return null;
        }
    }

    public async Task<WebChatSendResult?> SendOperatorMessageAsync(long conversationId, string content, CancellationToken ct = default)
    {
        try
        {
            var request = WithInternalKey(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/operator/conversations/{conversationId}/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json")
            });
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("WebChat send message failed: {Status} {Body}", (int)response.StatusCode, body);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<WebChatSendResult>(SnakeCase, ct);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "WebChat send message timeout for conversation {Id}", conversationId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat send message failed for conversation {Id}", conversationId);
            return null;
        }
    }

    public async Task<bool> CloseConversationAsync(long conversationId, CancellationToken ct = default)
    {
        try
        {
            var request = WithInternalKey(new HttpRequestMessage(HttpMethod.Put, $"/api/v1/operator/conversations/{conversationId}/close"));
            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "WebChat close conversation timeout for {Id}", conversationId);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "WebChat close conversation failed for {Id}", conversationId);
            return false;
        }
    }
}

// DTOs for WebChat proxy responses
public sealed class WebChatConversationsResponse
{
    [JsonPropertyName("conversations")]
    public List<WebChatConversationDto> Conversations { get; set; } = [];
}

public sealed class WebChatConversationDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("visitor_id")]
    public string VisitorId { get; set; } = "";

    [JsonPropertyName("visitor_name")]
    public string? VisitorName { get; set; }

    [JsonPropertyName("visitor_email")]
    public string? VisitorEmail { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("last_message_at")]
    public DateTime? LastMessageAt { get; set; }

    [JsonPropertyName("last_message")]
    public WebChatLastMessageDto? LastMessage { get; set; }
}

public sealed class WebChatLastMessageDto
{
    [JsonPropertyName("sender_type")]
    public string SenderType { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public sealed class WebChatMessagesResponse
{
    [JsonPropertyName("messages")]
    public List<WebChatMessageDto> Messages { get; set; } = [];
}

public sealed class WebChatMessageDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("conversation_id")]
    public long ConversationId { get; set; }

    [JsonPropertyName("sender_type")]
    public string SenderType { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public sealed class WebChatSendResult
{
    [JsonPropertyName("message")]
    public WebChatMessageDto? Message { get; set; }
}
