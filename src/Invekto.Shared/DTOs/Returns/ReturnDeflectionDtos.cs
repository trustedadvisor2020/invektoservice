namespace Invekto.Shared.DTOs.Returns;

/// <summary>PKT-6B1 GR-3.8+3.17: Return deflection request from webhook/automation.</summary>
public sealed class ReturnDeflectionRequest
{
    public string? ConversationId { get; set; }
    public string CustomerPhone { get; set; } = "";
    public string? ReasonCategory { get; set; }
    public string? ReasonText { get; set; }
}

/// <summary>Return deflection record for API responses.</summary>
public sealed class ReturnDeflectionResponse
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? ConversationId { get; set; }
    public string CustomerPhone { get; set; } = "";
    public string OriginalIntent { get; set; } = "return_request";
    public string ReasonCategory { get; set; } = "";
    public string? ReasonText { get; set; }
    public string ActionTaken { get; set; } = "";
    public string? CouponCode { get; set; }
    public decimal? CouponValue { get; set; }
    public string? ExchangeProduct { get; set; }
    public bool WasDeflected { get; set; }
    public decimal? DeflectionRevenue { get; set; }
    public bool FollowUpSent { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Stats for return deflection success rate (GR-3.17).</summary>
public sealed class ReturnDeflectionStatsResponse
{
    public int TenantId { get; set; }
    public int TotalReturns { get; set; }
    public int Deflected { get; set; }
    public decimal DeflectionRate { get; set; }
    public decimal TotalRevenueSaved { get; set; }
    public Dictionary<string, int> ByReason { get; set; } = new();
    public Dictionary<string, int> ByAction { get; set; } = new();
}
