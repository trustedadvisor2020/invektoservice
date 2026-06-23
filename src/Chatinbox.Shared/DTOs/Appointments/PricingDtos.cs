using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Appointments;

/// <summary>
/// Service pricing response item. GET /api/v1/pricing
/// </summary>
public sealed class ServicePricingDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("service_name")]
    public string ServiceName { get; set; } = "";

    [JsonPropertyName("price_min")]
    public decimal PriceMin { get; set; }

    [JsonPropertyName("price_max")]
    public decimal PriceMax { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "TRY";

    [JsonPropertyName("duration_minutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DurationMinutes { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// POST /api/v1/pricing request body.
/// </summary>
public sealed class PricingCreateRequest
{
    [JsonPropertyName("service_name")]
    public string ServiceName { get; set; } = "";

    [JsonPropertyName("price_min")]
    public decimal PriceMin { get; set; }

    [JsonPropertyName("price_max")]
    public decimal PriceMax { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "TRY";

    [JsonPropertyName("duration_minutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DurationMinutes { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

/// <summary>
/// PUT /api/v1/pricing/{id} request body. All fields optional.
/// </summary>
public sealed class PricingUpdateRequest
{
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; set; }

    [JsonPropertyName("price_min")]
    public decimal? PriceMin { get; set; }

    [JsonPropertyName("price_max")]
    public decimal? PriceMax { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("duration_minutes")]
    public int? DurationMinutes { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
}
