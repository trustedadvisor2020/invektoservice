using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.AgentAI;
using Chatinbox.Shared.Logging;

namespace Chatinbox.AgentAI.Services;

/// <summary>
/// Fetches recent orders for a customer phone from Integrations service.
/// Returns an OrderCardResponse for agent assist display.
/// Never throws — all exceptions caught and logged.
/// PKT-6B1: GR-3.3 Agent Assist E-commerce.
/// </summary>
public sealed class OrderCardService
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public OrderCardService(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetch recent orders from Integrations service for the given phone.
    /// Returns null on failure (never throws).
    /// </summary>
    public async Task<OrderCardResponse?> GetOrderCardAsync(
        int tenantId, string phone, string? jwtToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        try
        {
            var requestUrl = $"/api/v1/orders?phone={Uri.EscapeDataString(phone)}&limit=5";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (!string.IsNullOrEmpty(jwtToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AgentAIOrderCardFetchFailed}] Integrations orders query returned {(int)response.StatusCode} for tenant {tenantId}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var items = new List<OrderCardItem>();

            // Integrations returns { orders: [...] } or direct array
            JsonElement ordersEl;
            if (doc.RootElement.TryGetProperty("orders", out var arr))
                ordersEl = arr;
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                ordersEl = doc.RootElement;
            else
                return new OrderCardResponse { CustomerPhone = phone, Orders = items };

            foreach (var order in ordersEl.EnumerateArray())
            {
                items.Add(new OrderCardItem
                {
                    Provider = order.TryGetProperty("provider", out var p) ? p.GetString() ?? "" : "",
                    ExternalOrderId = order.TryGetProperty("external_order_id", out var eid) ? eid.GetString() ?? "" : "",
                    Status = order.TryGetProperty("order_status", out var s) ? s.GetString() ?? "" : "",
                    TotalAmount = order.TryGetProperty("total_amount", out var a) && a.ValueKind == JsonValueKind.Number
                        ? a.GetDecimal() : null,
                    Currency = order.TryGetProperty("currency", out var c) ? c.GetString() ?? "TRY" : "TRY",
                    TrackingCode = order.TryGetProperty("tracking_code", out var t) ? t.GetString() : null,
                    CreatedAt = order.TryGetProperty("created_at", out var ca) ? ca.GetString() : null
                });
            }

            _logger.StepInfo(
                $"Order card fetched: tenant={tenantId}, phone={phone}, orders={items.Count}",
                "order-card");

            return new OrderCardResponse { CustomerPhone = phone, Orders = items };
        }
        catch (TaskCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AgentAIOrderCardFetchFailed}] Integrations timeout for tenant {tenantId}, phone={phone}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AgentAIOrderCardFetchFailed}] Integrations connection error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AgentAIOrderCardFetchFailed}] Integrations response parse error: {ex.Message}");
            return null;
        }
    }
}
