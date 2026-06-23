using Chatinbox.Shared.Logging;

namespace Chatinbox.Integrations.Services;

/// <summary>
/// Mock implementation of Hepsiburada marketplace API.
/// Returns simulated data until real API credentials are available.
/// </summary>
public sealed class HepsiburadaMockProvider : IMarketplaceProvider
{
    private readonly JsonLinesLogger _logger;

    public HepsiburadaMockProvider(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public string ProviderName => "hepsiburada";

    public Task<List<MarketplaceOrder>> FetchOrdersAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        DateTime? sinceDate, CancellationToken ct)
    {
        _logger.SystemWarn($"[HB-MOCK] FetchOrders called (mock mode, seller={sellerId}, since={sinceDate:yyyy-MM-dd})");

        var orders = new List<MarketplaceOrder>
        {
            new()
            {
                ExternalOrderId = $"HB-{DateTime.UtcNow:yyyyMMdd}-001",
                CustomerPhone = "+905321234567",
                CustomerName = "Mock Musteri",
                TrackingCode = "HB1234567890",
                CargoProvider = "aras",
                OrderStatus = "shipped",
                TotalAmount = 299.90m,
                Currency = "TRY",
                OrderDataJson = "{\"mock\":true,\"items\":[{\"name\":\"Test Urun\",\"qty\":1}]}"
            }
        };

        return Task.FromResult(orders);
    }

    public Task<MarketplaceOrder?> FetchOrderDetailAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        string externalOrderId, CancellationToken ct)
    {
        _logger.SystemWarn($"[HB-MOCK] FetchOrderDetail called (mock mode, order={externalOrderId})");

        var order = new MarketplaceOrder
        {
            ExternalOrderId = externalOrderId,
            CustomerPhone = "+905321234567",
            CustomerName = "Mock Musteri",
            OrderStatus = "delivered",
            TotalAmount = 199.90m,
            Currency = "TRY",
            OrderDataJson = "{\"mock\":true}"
        };

        return Task.FromResult<MarketplaceOrder?>(order);
    }

    public Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        CancellationToken ct)
    {
        _logger.SystemWarn("[HB-MOCK] TestConnection called (mock mode - always returns success)");
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
