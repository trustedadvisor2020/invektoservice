using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Integrations;

/// <summary>
/// Integration account registration/update request.
/// </summary>
public sealed class IntegrationAccountRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("api_secret")]
    public string? ApiSecret { get; set; }

    [JsonPropertyName("seller_id")]
    public string? SellerId { get; set; }

    [JsonPropertyName("settings")]
    public Dictionary<string, object>? Settings { get; set; }
}

/// <summary>
/// Integration account response (credentials masked).
/// </summary>
public sealed class IntegrationAccountResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("seller_id")]
    public string? SellerId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("last_sync_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastSyncAt { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Cached order from marketplace.
/// </summary>
public sealed class OrderCacheResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("external_order_id")]
    public string ExternalOrderId { get; set; } = "";

    [JsonPropertyName("customer_phone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("tracking_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TrackingCode { get; set; }

    [JsonPropertyName("cargo_provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CargoProvider { get; set; }

    [JsonPropertyName("order_status")]
    public string OrderStatus { get; set; } = "";

    [JsonPropertyName("total_amount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TotalAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "TRY";

    [JsonPropertyName("synced_at")]
    public DateTime SyncedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Cargo tracking status response.
/// </summary>
public sealed class CargoTrackingResponse
{
    [JsonPropertyName("tracking_code")]
    public string TrackingCode { get; set; } = "";

    [JsonPropertyName("cargo_provider")]
    public string CargoProvider { get; set; } = "";

    [JsonPropertyName("current_status")]
    public string CurrentStatus { get; set; } = "";

    [JsonPropertyName("events")]
    public List<CargoTrackingEvent> Events { get; set; } = new();
}

public sealed class CargoTrackingEvent
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("location")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; set; }

    [JsonPropertyName("event_time")]
    public DateTime EventTime { get; set; }
}

/// <summary>
/// Order query filter.
/// </summary>
public sealed class OrderQueryRequest
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("customer_phone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("from_date")]
    public DateTime? FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime? ToDate { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
