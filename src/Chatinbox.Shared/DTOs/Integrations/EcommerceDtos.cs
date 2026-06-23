using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Integrations;

// ── Product DTOs (Integrations API response) ──

public sealed class EcommerceProductListResponse
{
    [JsonPropertyName("products")] public List<EcommerceProductResponse> Products { get; set; } = new();
    [JsonPropertyName("total_count")] public int TotalCount { get; set; }
    [JsonPropertyName("has_next_page")] public bool HasNextPage { get; set; }
    [JsonPropertyName("cursor")] public string? Cursor { get; set; }
}

public sealed class EcommerceProductResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("price")] public decimal? Price { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("stock_count")] public int? StockCount { get; set; }
    [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}

// ── Customer DTOs ──

public sealed class EcommerceCustomerListResponse
{
    [JsonPropertyName("customers")] public List<EcommerceCustomerResponse> Customers { get; set; } = new();
    [JsonPropertyName("total_count")] public int TotalCount { get; set; }
}

public sealed class EcommerceCustomerResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("full_name")] public string? FullName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("order_count")] public int OrderCount { get; set; }
    [JsonPropertyName("total_spent")] public decimal? TotalSpent { get; set; }
}

// ── Operation Result DTO ──

public sealed class EcommerceOperationResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("result")] public object? Result { get; set; }
}
