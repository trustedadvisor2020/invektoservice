using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Integration;

/// <summary>
/// Webhook payload that Main App sends to InvektoServis.
/// GR-1.9: Standardized event format for all Main App -> InvektoServis communication.
/// tenant_id comes from JWT token, NOT from payload (security: prevent tenant spoofing).
/// </summary>
public sealed class IncomingWebhookEvent
{
    /// <summary>Event type (see WebhookEventTypes constants)</summary>
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    /// <summary>Sequence ID for ordering guarantee. Main App increments per tenant.</summary>
    [JsonPropertyName("sequence_id")]
    public long SequenceId { get; init; }

    /// <summary>Conversation/chat ID in Main App</summary>
    [JsonPropertyName("chat_id")]
    public int ChatId { get; init; }

    /// <summary>Channel type (whatsapp, web, instagram, etc.)</summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    /// <summary>Event-specific data (structure depends on event_type)</summary>
    [JsonPropertyName("data")]
    public WebhookEventData? Data { get; init; }

    /// <summary>Main App timestamp when event occurred</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>Callback URL where InvektoServis sends async results. If null, uses tenant default.</summary>
    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; init; }
}

/// <summary>
/// Event-specific data. Fields are nullable because different events use different fields.
/// </summary>
public sealed class WebhookEventData
{
    /// <summary>Customer phone number (for new_message)</summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    /// <summary>Customer name (if known)</summary>
    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; init; }

    /// <summary>Message text (for new_message)</summary>
    [JsonPropertyName("message_text")]
    public string? MessageText { get; init; }

    /// <summary>Message source: CUSTOMER or AGENT</summary>
    [JsonPropertyName("message_source")]
    public string? MessageSource { get; init; }

    /// <summary>Assigned agent ID (for agent_assigned)</summary>
    [JsonPropertyName("agent_id")]
    public int? AgentId { get; init; }

    /// <summary>Tag/label name (for tag_changed)</summary>
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    /// <summary>Tag action: added or removed (for tag_changed)</summary>
    [JsonPropertyName("tag_action")]
    public string? TagAction { get; init; }
}
