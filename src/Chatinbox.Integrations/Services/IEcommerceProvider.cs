namespace Chatinbox.Integrations.Services;

/// <summary>
/// Extended e-commerce provider interface: full CRUD for orders, products, customers.
/// Extends IMarketplaceProvider for backward-compat with OrderSyncService.
/// Tenant-aware methods (tenantId instead of raw credentials) — cleaner for OAuth2 flows.
/// First real implementation: IkasProvider. Pattern generalizable for Shopify, Trendyol, etc.
/// </summary>
public interface IEcommerceProvider : IMarketplaceProvider
{
    // ── Products ──

    Task<EcommerceProductList> ListProductsAsync(
        int tenantId, EcommerceProductFilter filter, CancellationToken ct);

    Task<EcommerceProduct?> GetProductAsync(
        int tenantId, string productId, CancellationToken ct);

    // ── Customers ──

    Task<EcommerceCustomerList> ListCustomersAsync(
        int tenantId, EcommerceCustomerFilter filter, CancellationToken ct);

    // ── Order Mutations ──

    Task<EcommerceOperationResult> FulfillOrderAsync(
        int tenantId, string orderId, FulfillOrderRequest request, CancellationToken ct);

    Task<EcommerceOperationResult> UpdateOrderStatusAsync(
        int tenantId, string orderId, string newStatus, CancellationToken ct);

    Task<EcommerceOperationResult> RefundOrderLineAsync(
        int tenantId, string orderId, RefundLineRequest request, CancellationToken ct);
}
