using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

/// <summary>
/// POST /api/v1/webhook/trigger request - event from Main App triggers a template message.
/// </summary>
public sealed class TriggerWebhookRequest
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("variables")]
    public Dictionary<string, string>? Variables { get; set; }
}

/// <summary>
/// 202 response for trigger webhook.
/// </summary>
public sealed class TriggerWebhookResponse
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("template_name")]
    public string TemplateName { get; set; } = "";
}

/// <summary>
/// POST /api/v1/webhook/delivery-status - delivery status from Main App.
/// </summary>
public sealed class DeliveryStatusRequest
{
    [JsonPropertyName("external_message_id")]
    public string ExternalMessageId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("failed_reason")]
    public string? FailedReason { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// POST /api/v1/webhook/message - incoming message for opt-out detection.
/// </summary>
public sealed class IncomingMessageRequest
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("message_text")]
    public string MessageText { get; set; } = "";
}

/// <summary>
/// Response for incoming message opt-out check.
/// </summary>
public sealed class IncomingMessageResponse
{
    [JsonPropertyName("opted_out")]
    public bool OptedOut { get; set; }

    [JsonPropertyName("keyword_matched")]
    public string? KeywordMatched { get; set; }
}

/// <summary>
/// POST /api/v1/optout request - manual opt-out add.
/// </summary>
public sealed class OptOutRequest
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// GET /api/v1/optout/check/{phone} response.
/// </summary>
public sealed class OptOutCheckResponse
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("opted_out")]
    public bool OptedOut { get; set; }

    [JsonPropertyName("opted_out_at")]
    public DateTime? OptedOutAt { get; set; }
}
