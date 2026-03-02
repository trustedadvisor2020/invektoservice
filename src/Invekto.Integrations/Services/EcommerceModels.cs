namespace Invekto.Integrations.Services;

// ── Product Models ──

public sealed class EcommerceProductList
{
    public List<EcommerceProduct> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasNextPage { get; set; }
    public string? Cursor { get; set; }
}

public sealed class EcommerceProduct
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public int? StockCount { get; set; }
    public string? ImageUrl { get; set; }
    public string? Status { get; set; }
    public string? RawDataJson { get; set; }
}

public sealed class EcommerceProductFilter
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public int Limit { get; set; } = 20;
    public string? Cursor { get; set; }
}

// ── Customer Models ──

public sealed class EcommerceCustomerList
{
    public List<EcommerceCustomer> Customers { get; set; } = new();
    public int TotalCount { get; set; }
}

public sealed class EcommerceCustomer
{
    public string Id { get; set; } = "";
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int OrderCount { get; set; }
    public decimal? TotalSpent { get; set; }
    public string? RawDataJson { get; set; }
}

public sealed class EcommerceCustomerFilter
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? SearchTerm { get; set; }
    public int Limit { get; set; } = 20;
}

// ── Order Mutation Models ──

public sealed class FulfillOrderRequest
{
    public string? TrackingCode { get; set; }
    public string? CargoProvider { get; set; }
}

public sealed class RefundLineRequest
{
    public string LineItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? Reason { get; set; }
}

// ── Generic Result ──

public sealed class EcommerceOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultJson { get; set; }

    public static EcommerceOperationResult Ok(string? json = null) =>
        new() { Success = true, ResultJson = json };

    public static EcommerceOperationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
