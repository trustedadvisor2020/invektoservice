using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.WebChat.Services;

/// <summary>
/// Fire-and-forget HTTP client for Automation webhook triggers.
/// Flow config comes from DB per-widget; tenantId and flowId are passed per call.
/// </summary>
public sealed class AutomationWebhookClient
{
    private readonly HttpClient _http;
    private readonly JsonLinesLogger _logger;

    public AutomationWebhookClient(HttpClient http, JsonLinesLogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task NotifyConversationCreatedAsync(
        int tenantId, int flowId,
        long conversationId, string visitorId, string? name,
        string? email, string? pageUrl, CancellationToken ct = default)
    {
        if (flowId <= 0) return;

        var payload = new
        {
            @event = "conversation_created",
            conversation_id = conversationId,
            visitor_id = visitorId,
            name,
            email,
            page_url = pageUrl,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        await FireWebhookAsync(tenantId, flowId, payload, ct);
    }

    public async Task NotifyVisitorMessageAsync(
        int tenantId, int flowId,
        long conversationId, string visitorId, string content,
        CancellationToken ct = default)
    {
        if (flowId <= 0) return;

        var payload = new
        {
            @event = "visitor_message",
            conversation_id = conversationId,
            visitor_id = visitorId,
            content,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        await FireWebhookAsync(tenantId, flowId, payload, ct);
    }

    public async Task NotifyConversationClosedAsync(
        int tenantId, int flowId,
        long conversationId, CancellationToken ct = default)
    {
        if (flowId <= 0) return;

        var payload = new
        {
            @event = "conversation_closed",
            conversation_id = conversationId,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        await FireWebhookAsync(tenantId, flowId, payload, ct);
    }

    private async Task FireWebhookAsync(int tenantId, int flowId, object payload, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var url = $"/api/v1/webhooks/{tenantId}/{flowId}";
            var requestId = Guid.NewGuid().ToString("N");

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Request-Id", requestId);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemError(
                    $"[{ErrorCodes.WebChatWebhookFailed}] Webhook POST {url} returned {(int)response.StatusCode}: {body}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.SystemError(
                $"[{ErrorCodes.WebChatWebhookTimeout}] Webhook POST to flow {flowId} timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.WebChatWebhookFailed}] Webhook POST to flow {flowId} failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.WebChatWebhookFailed}] Webhook unexpected error for flow {flowId}: {ex.Message}");
        }
    }
}
