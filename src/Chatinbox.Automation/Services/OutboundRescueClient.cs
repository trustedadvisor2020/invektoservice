using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// HTTP client for Outbound service — sends rescue messages via webhook trigger.
/// PKT-12 Faz 2: Rescue Action Engine.
/// </summary>
public sealed class OutboundRescueClient
{
    private readonly HttpClient _httpClient;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public OutboundRescueClient(HttpClient httpClient, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Send a rescue message via Outbound webhook trigger.
    /// Returns message_id on success, null on failure (graceful degradation).
    /// </summary>
    public async Task<string?> SendRescueMessageAsync(
        int tenantId, string phone, string messageText,
        string eventName = "review_rescue", CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                @event = eventName,
                phone,
                variables = new Dictionary<string, string>
                {
                    ["message"] = messageText
                },
                lang = "tr"
            });

            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TriggerResponse>(ct);
                return result?.MessageId;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueDispatchFailed}] Outbound trigger returned {(int)response.StatusCode} for tenant {tenantId}: {body}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueDispatchFailed}] Outbound service unreachable for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueDispatchFailed}] Outbound rescue send cancelled for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    private sealed class TriggerResponse
    {
        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }
    }
}
