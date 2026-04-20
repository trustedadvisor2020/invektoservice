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

    /// <summary>
    /// GR-2.3: Target language (ISO 639-1). If null, defaults to "tr".
    /// Fallback chain: requested lang -> "tr" (tenant default) -> any available template.
    /// </summary>
    [JsonPropertyName("lang")]
    public string? Lang { get; set; }
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

    /// <summary>
    /// GR-2.3: Warning when language fallback occurred (e.g., requested lang not available).
    /// </summary>
    [JsonPropertyName("warning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Warning { get; set; }
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

    // FEAT-J2: instance_id carries the WapCRM instance that delivered the incoming
    // message. Outbound uses it at outbox enqueue so the subsequent INMA /api/optout
    // push can pass InstanceID without cross-service lookup. Optional for backward
    // compatibility; legacy Backend callers without instance context send null.
    [JsonPropertyName("instance_id")]
    public int? InstanceId { get; set; }
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
/// FEAT-J2: Backend → Outbound manual opt-out forwarder payload.
/// Backend resolves last-known instance via MessageLogRepository before forwarding.
/// Auth: X-Internal-Service-Token header (InternalServices:SharedSecret).
/// </summary>
public sealed class InternalOptOutRequest
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("instance_id")]
    public int? InstanceId { get; set; }

    /// <summary>"opt_out" (default) or "opt_in".</summary>
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "opt_out";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// FEAT-J2: SuperAdmin-triggered drain of 'skipped_noop' outbox rows.
/// TenantId=null drains all tenants; SinceUtc=null drains all history.
/// </summary>
public sealed class OutboxRetryRequest
{
    [JsonPropertyName("tenant_id")]
    public int? TenantId { get; set; }

    [JsonPropertyName("since_utc")]
    public DateTime? SinceUtc { get; set; }
}

/// <summary>
/// POST /api/v1/optout request - manual opt-out add.
/// FEAT-J2: optional <see cref="EventType"/> extends this DTO with opt-in support
/// on the Backend Dashboard admin action (null/opt_out = register opt-out,
/// "opt_in" = clear opt-out).
/// </summary>
public sealed class OptOutRequest
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>FEAT-J2: "opt_out" (default/null) or "opt_in".</summary>
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }
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
