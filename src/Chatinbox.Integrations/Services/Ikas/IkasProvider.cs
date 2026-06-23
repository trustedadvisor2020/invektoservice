using System.Text.Json;
using Chatinbox.Integrations.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Integrations.Services.Ikas;

/// <summary>
/// ikas e-commerce provider — first real IEcommerceProvider implementation.
/// Uses OAuth2 (client_credentials) + GraphQL admin API.
/// Also implements IMarketplaceProvider (via IEcommerceProvider : IMarketplaceProvider)
/// so OrderSyncService can discover and use it for order sync.
/// </summary>
public sealed class IkasProvider : IEcommerceProvider
{
    private readonly IkasGraphQlClient _graphQl;
    private readonly IntegrationsRepository _repo;
    private readonly JsonLinesLogger _logger;

    public string ProviderName => "ikas";

    public IkasProvider(IkasGraphQlClient graphQl, IntegrationsRepository repo, JsonLinesLogger logger)
    {
        _graphQl = graphQl;
        _repo = repo;
        _logger = logger;
    }

    // ================================================================
    // IMarketplaceProvider (backward compat with OrderSyncService)
    // ================================================================

    public async Task<List<MarketplaceOrder>> FetchOrdersAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        DateTime? sinceDate, CancellationToken ct)
    {
        // OrderSyncService passes raw credentials. We need tenantId to get a token.
        // Resolve tenantId by looking up the account with this client_id.
        var tenantId = await ResolveTenantIdAsync(apiKeyEnc, ct);
        if (tenantId == null)
        {
            _logger.SystemWarn("[ikas] FetchOrders: could not resolve tenantId from client_id");
            return new List<MarketplaceOrder>();
        }

        var variables = new Dictionary<string, object?>();
        if (sinceDate.HasValue)
            variables["orderedAt"] = new { gte = sinceDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") };

        variables["pagination"] = new { limit = 50 };

        var response = await _graphQl.ExecuteAsync(tenantId.Value, IkasQueries.ListOrders, variables, ct);
        if (!response.Success || response.DataJson == null)
            return new List<MarketplaceOrder>();

        return ParseOrderList(response.DataJson);
    }

    public async Task<MarketplaceOrder?> FetchOrderDetailAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId,
        string externalOrderId, CancellationToken ct)
    {
        var tenantId = await ResolveTenantIdAsync(apiKeyEnc, ct);
        if (tenantId == null) return null;

        var variables = new { id = new { eq = externalOrderId } };
        var response = await _graphQl.ExecuteAsync(tenantId.Value, IkasQueries.GetOrder, variables, ct);
        if (!response.Success || response.DataJson == null) return null;

        var orders = ParseOrderList(response.DataJson);
        return orders.Count > 0 ? orders[0] : null;
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(
        string apiKeyEnc, string? apiSecretEnc, string? sellerId, CancellationToken ct)
    {
        var tenantId = await ResolveTenantIdAsync(apiKeyEnc, ct);
        if (tenantId == null)
            return (false, "Could not resolve tenant from credentials");

        // Simple test: fetch 1 product
        var variables = new { pagination = new { limit = 1 } };
        var response = await _graphQl.ExecuteAsync(tenantId.Value, IkasQueries.ListProducts, variables, ct);

        return response.Success
            ? (true, null)
            : (false, response.ErrorMessage ?? "GraphQL call failed");
    }

    // ================================================================
    // IEcommerceProvider — Products
    // ================================================================

    public async Task<EcommerceProductList> ListProductsAsync(
        int tenantId, EcommerceProductFilter filter, CancellationToken ct)
    {
        var variables = new Dictionary<string, object?>
        {
            ["pagination"] = new { limit = Math.Clamp(filter.Limit, 1, 50) }
        };

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            variables["name"] = new { contains = filter.SearchTerm };

        var response = await _graphQl.ExecuteAsync(tenantId, IkasQueries.ListProducts, variables, ct);
        if (!response.Success || response.DataJson == null)
        {
            return new EcommerceProductList();
        }

        return ParseProductList(response.DataJson);
    }

    public async Task<EcommerceProduct?> GetProductAsync(
        int tenantId, string productId, CancellationToken ct)
    {
        var variables = new { id = new { eq = productId } };
        var response = await _graphQl.ExecuteAsync(tenantId, IkasQueries.GetProduct, variables, ct);
        if (!response.Success || response.DataJson == null) return null;

        var list = ParseProductList(response.DataJson);
        return list.Products.Count > 0 ? list.Products[0] : null;
    }

    // ================================================================
    // IEcommerceProvider — Customers
    // ================================================================

    public async Task<EcommerceCustomerList> ListCustomersAsync(
        int tenantId, EcommerceCustomerFilter filter, CancellationToken ct)
    {
        var variables = new Dictionary<string, object?>
        {
            ["pagination"] = new { limit = Math.Clamp(filter.Limit, 1, 50) }
        };

        if (!string.IsNullOrEmpty(filter.Phone))
            variables["phone"] = new { eq = filter.Phone };
        if (!string.IsNullOrEmpty(filter.Email))
            variables["email"] = new { eq = filter.Email };
        if (!string.IsNullOrEmpty(filter.SearchTerm))
            variables["search"] = filter.SearchTerm;

        var response = await _graphQl.ExecuteAsync(tenantId, IkasQueries.ListCustomers, variables, ct);
        if (!response.Success || response.DataJson == null)
        {
            return new EcommerceCustomerList();
        }

        return ParseCustomerList(response.DataJson);
    }

    // ================================================================
    // IEcommerceProvider — Order Mutations
    // ================================================================

    public async Task<EcommerceOperationResult> FulfillOrderAsync(
        int tenantId, string orderId, FulfillOrderRequest request, CancellationToken ct)
    {
        var input = new Dictionary<string, object?>
        {
            ["orderId"] = orderId,
            ["lines"] = Array.Empty<object>() // Fulfill all lines
        };

        if (!string.IsNullOrEmpty(request.TrackingCode))
        {
            input["trackingInfoDetail"] = new
            {
                trackingNumber = request.TrackingCode,
                cargoCompany = request.CargoProvider ?? ""
            };
        }

        var response = await _graphQl.ExecuteAsync(tenantId, IkasQueries.FulfillOrder,
            new { input }, ct);

        return response.Success
            ? EcommerceOperationResult.Ok(response.DataJson)
            : EcommerceOperationResult.Fail(response.ErrorMessage ?? "Fulfill failed");
    }

    public async Task<EcommerceOperationResult> UpdateOrderStatusAsync(
        int tenantId, string orderId, string newStatus, CancellationToken ct)
    {
        // ikas uses package-level status updates via updateOrderPackageStatus
        var input = new
        {
            orderId,
            packages = new[]
            {
                new { status = MapToIkasPackageStatus(newStatus) }
            }
        };

        var response = await _graphQl.ExecuteAsync(tenantId, IkasQueries.UpdateOrderPackageStatus,
            new { input }, ct);

        return response.Success
            ? EcommerceOperationResult.Ok(response.DataJson)
            : EcommerceOperationResult.Fail(response.ErrorMessage ?? "Status update failed");
    }

    public Task<EcommerceOperationResult> RefundOrderLineAsync(
        int tenantId, string orderId, RefundLineRequest request, CancellationToken ct)
    {
        // ikas refund requires specific mutation not yet mapped — return not-implemented for now
        _logger.SystemWarn($"[ikas] RefundOrderLine not yet implemented for ikas. orderId={orderId}");
        return Task.FromResult(EcommerceOperationResult.Fail("ikas refund not yet implemented"));
    }

    // ================================================================
    // Parsers
    // ================================================================

    private List<MarketplaceOrder> ParseOrderList(string dataJson)
    {
        var orders = new List<MarketplaceOrder>();
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("listOrder", out var listOrder)) return orders;
            if (!listOrder.TryGetProperty("data", out var data)) return orders;

            foreach (var item in data.EnumerateArray())
            {
                var order = new MarketplaceOrder
                {
                    ExternalOrderId = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    OrderStatus = item.TryGetProperty("status", out var st) ? st.GetString() ?? "unknown" : "unknown",
                    TotalAmount = item.TryGetProperty("totalPrice", out var tp) ? tp.GetDecimal() : null,
                    Currency = item.TryGetProperty("currencyCode", out var cc) ? cc.GetString() ?? "TRY" : "TRY",
                    OrderDataJson = item.GetRawText()
                };

                // Extract customer info
                if (item.TryGetProperty("customer", out var cust))
                {
                    var firstName = cust.TryGetProperty("firstName", out var fn) ? fn.GetString() : "";
                    var lastName = cust.TryGetProperty("lastName", out var ln) ? ln.GetString() : "";
                    order.CustomerName = $"{firstName} {lastName}".Trim();
                    order.CustomerPhone = cust.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
                }

                // Extract tracking from first package (ikas: orderPackages, not packages)
                if (item.TryGetProperty("orderPackages", out var pkgs) &&
                    pkgs.ValueKind == JsonValueKind.Array && pkgs.GetArrayLength() > 0)
                {
                    var pkg = pkgs[0];
                    if (pkg.TryGetProperty("trackingInfo", out var ti))
                    {
                        order.TrackingCode = ti.TryGetProperty("trackingNumber", out var tn) ? tn.GetString() : null;
                        order.CargoProvider = ti.TryGetProperty("cargoCompany", out var cco) ? cco.GetString() : null;
                    }
                }

                orders.Add(order);
            }

            // Audit Int-4 (detector): FetchOrdersAsync currently requests only
            // pagination.limit=50 and does NOT page through results, so orders beyond the
            // first page are silently dropped. Surface that truncation so a partial sync is
            // observable in logs instead of disappearing without a signal. The full
            // pagination loop is a separate scoped task (pending live ikas PaginationInput
            // contract confirmation) — this detector is behavior-preserving.
            var hasNext = listOrder.TryGetProperty("hasNext", out var hn) &&
                          hn.ValueKind == JsonValueKind.True;
            int? reportedCount = listOrder.TryGetProperty("count", out var cnt) &&
                                 cnt.ValueKind == JsonValueKind.Number && cnt.TryGetInt32(out var c)
                ? c : null;
            if (hasNext || (reportedCount.HasValue && reportedCount.Value > orders.Count))
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas order sync PARTIAL: fetched {orders.Count}, " +
                    $"hasNext={hasNext}, reportedCount={(reportedCount.HasValue ? reportedCount.Value.ToString() : "n/a")} " +
                    "— orders beyond the first page are not synced yet (pagination loop pending, audit Int-4).");
            }
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas ParseOrderList JSON error: {ex.Message}");
        }

        return orders;
    }

    private EcommerceProductList ParseProductList(string dataJson)
    {
        var result = new EcommerceProductList();
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("listProduct", out var listProduct)) return result;

            result.TotalCount = listProduct.TryGetProperty("count", out var cnt) ? cnt.GetInt32() : 0;
            result.HasNextPage = listProduct.TryGetProperty("hasNextPage", out var hnp) && hnp.GetBoolean();

            if (!listProduct.TryGetProperty("data", out var data)) return result;

            foreach (var item in data.EnumerateArray())
            {
                var product = new EcommerceProduct
                {
                    Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    RawDataJson = item.GetRawText()
                };

                // Extract first variant's price and stock
                if (item.TryGetProperty("variants", out var variants) &&
                    variants.ValueKind == JsonValueKind.Array && variants.GetArrayLength() > 0)
                {
                    var firstVariant = variants[0];

                    if (firstVariant.TryGetProperty("prices", out var prices) &&
                        prices.ValueKind == JsonValueKind.Array && prices.GetArrayLength() > 0)
                    {
                        var price = prices[0];
                        product.Price = price.TryGetProperty("sellPrice", out var sp) ? sp.GetDecimal() : null;
                        product.Currency = price.TryGetProperty("currency", out var cur) ? cur.GetString() : "TRY";
                    }

                    // ikas: stocks is an array (per stock location), take first entry total
                    if (firstVariant.TryGetProperty("stocks", out var stockArr) &&
                        stockArr.ValueKind == JsonValueKind.Array && stockArr.GetArrayLength() > 0)
                    {
                        product.StockCount = stockArr[0].TryGetProperty("stockCount", out var sc) ? sc.GetInt32() : null;
                    }

                    // Main image
                    if (firstVariant.TryGetProperty("images", out var images) &&
                        images.ValueKind == JsonValueKind.Array && images.GetArrayLength() > 0)
                    {
                        var mainImg = images.EnumerateArray()
                            .FirstOrDefault(i => i.TryGetProperty("isMain", out var im) && im.GetBoolean());
                        if (mainImg.ValueKind == JsonValueKind.Undefined)
                            mainImg = images[0];

                        product.ImageUrl = mainImg.TryGetProperty("imageUrl", out var iu) ? iu.GetString() : null;
                    }

                    product.Status = firstVariant.TryGetProperty("isActive", out var ia)
                        ? (ia.GetBoolean() ? "active" : "inactive")
                        : "unknown";
                }

                result.Products.Add(product);
            }
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas ParseProductList JSON error: {ex.Message}");
        }

        return result;
    }

    private EcommerceCustomerList ParseCustomerList(string dataJson)
    {
        var result = new EcommerceCustomerList();
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("listCustomer", out var listCustomer)) return result;

            result.TotalCount = listCustomer.TryGetProperty("count", out var cnt) ? cnt.GetInt32() : 0;

            if (!listCustomer.TryGetProperty("data", out var data)) return result;

            foreach (var item in data.EnumerateArray())
            {
                var firstName = item.TryGetProperty("firstName", out var fn) ? fn.GetString() : "";
                var lastName = item.TryGetProperty("lastName", out var ln) ? ln.GetString() : "";

                var customer = new EcommerceCustomer
                {
                    Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    FullName = $"{firstName} {lastName}".Trim(),
                    Email = item.TryGetProperty("email", out var em) ? em.GetString() : null,
                    Phone = item.TryGetProperty("phone", out var ph) ? ph.GetString() : null,
                    OrderCount = item.TryGetProperty("orderCount", out var oc) ? oc.GetInt32() : 0,
                    TotalSpent = item.TryGetProperty("totalOrderPrice", out var tp) ? tp.GetDecimal() : null,
                    RawDataJson = item.GetRawText()
                };

                result.Customers.Add(customer);
            }
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas ParseCustomerList JSON error: {ex.Message}");
        }

        return result;
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Resolve tenantId from client_id (api_key_enc) for OrderSyncService backward compat.
    /// Uses targeted O(1) query instead of loading all accounts.
    /// </summary>
    private async Task<int?> ResolveTenantIdAsync(string clientId, CancellationToken ct)
    {
        return await _repo.GetTenantIdByClientIdAsync("ikas", clientId, ct);
    }

    private static string MapToIkasPackageStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "shipped" or "in_transit" => "shipped",
            "delivered" => "delivered",
            "cancelled" or "canceled" => "cancelled",
            "returned" => "returned",
            _ => status
        };
    }
}
