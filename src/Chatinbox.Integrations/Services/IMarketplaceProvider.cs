namespace Chatinbox.Integrations.Services;

/// <summary>
/// Interface for marketplace provider integrations (HB, Trendyol, etc.).
/// Mock implementations until real API credentials are available.
/// </summary>
public interface IMarketplaceProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Fetch orders from marketplace API (or mock data).
    /// </summary>
    Task<List<MarketplaceOrder>> FetchOrdersAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        DateTime? sinceDate, CancellationToken ct);

    /// <summary>
    /// Fetch single order detail.
    /// </summary>
    Task<MarketplaceOrder?> FetchOrderDetailAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        string externalOrderId, CancellationToken ct);

    /// <summary>
    /// Test connection to provider API.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        CancellationToken ct);
}

/// <summary>
/// Marketplace order model returned by provider implementations.
/// </summary>
public sealed class MarketplaceOrder
{
    public string ExternalOrderId { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public string? CustomerName { get; set; }
    public string? TrackingCode { get; set; }
    public string? CargoProvider { get; set; }
    public string OrderStatus { get; set; } = "pending";
    public decimal? TotalAmount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? OrderDataJson { get; set; }
}
