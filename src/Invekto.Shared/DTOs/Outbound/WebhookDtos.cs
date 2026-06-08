using System.Text.Json;
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
/// POST /api/v1/webhook/delivery-status - delivery status from Main App (bridge) OR a cxapi
/// WapCRM ack relayed by Backend's /webhook/event (PR-3b-2). snake_case body.
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

    /// <summary>
    /// PR-3b-2: the WapCRM channel/InstanceID that produced the cxapi ack, carried for a SOFT
    /// defense-in-depth cross-check against the matched outbound_messages.instance_id (mismatch is
    /// logged, the status is still applied — wamid + the tenant-scoped UNIQUE already guarantee a
    /// single-row match). Null on the legacy Main App bridge path (no instance context).
    /// </summary>
    [JsonPropertyName("instance_id")]
    public int? InstanceId { get; set; }
}

/// <summary>
/// PR-3b-2: the raw WapCRM cxapi delivery-ack webhook payload (PascalCase) as posted to each tenant's
/// existing /api/v1/webhook/event?companyId=X. Distinguished from an inbound message by the ABSENCE of
/// a root <c>messages[]</c> array and the PRESENCE of <c>Status</c>/<c>InstanceMessageID</c> (INMA spec,
/// temp/WEBHOOK_OUTBOUND_PAYLOADS.md). <c>InstanceID</c> is an INT here (it is a STRING in the inbound
/// message envelope — that type trap is why the two shapes are deserialized into separate models after
/// shape discrimination, never into one). Backend maps this onto <see cref="DeliveryStatusRequest"/>.
/// </summary>
public sealed class WapCRMCloudAPIAckWebHookModel
{
    /// <summary>Globally-unique WapCRM channel id (one company). Soft cross-check only — correlation is by wamid.</summary>
    [JsonPropertyName("InstanceID")]
    public int InstanceID { get; set; }

    /// <summary>The sent message's WhatsApp wamid — equals outbound_messages.external_message_id captured at send (INMA C1, 2026-06-08).</summary>
    [JsonPropertyName("InstanceMessageID")]
    public string? InstanceMessageID { get; set; }

    /// <summary>1=Sent, 2=Delivered, 3=Viewed(read), 4=NotSent(failed). 0=Pending/5=Deleted are ignored.</summary>
    [JsonPropertyName("Status")]
    public int Status { get; set; }

    [JsonPropertyName("StatusText")]
    public string? StatusText { get; set; }

    /// <summary>Provider failure detail — populated only for Status=4 (NotSent).</summary>
    [JsonPropertyName("ReasonDetailForNotSent")]
    public string? ReasonDetailForNotSent { get; set; }
}

/// <summary>
/// PR-3b-2: pure mapping from the WapCRM ack <c>Status</c> int to our delivery-status string.
/// Extracted as a tested static so the shape-discrimination handler stays thin and the mapping
/// (the one piece of real logic in the ack branch) is unit-covered without a Backend test host.
/// </summary>
public static class WapCrmAckMapping
{
    /// <summary>Max length of outbound_messages.failed_reason (VARCHAR(500)).</summary>
    public const int FailedReasonMaxLength = 500;

    /// <summary>
    /// Maps a WapCRM ack Status int to a delivery-status string, or null for statuses we intentionally
    /// ignore (0=Pending, 5=Deleted, or any unknown value) so the ack branch can no-op them.
    /// </summary>
    public static string? MapStatus(int ackStatus) => ackStatus switch
    {
        1 => "sent",
        2 => "delivered",
        3 => "read",     // WapCRM "Viewed"
        4 => "failed",   // WapCRM "NotSent"
        _ => null
    };

    /// <summary>Truncates the provider failure detail to the failed_reason column width; null-safe.</summary>
    public static string? TruncateFailedReason(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return reason;
        return reason.Length <= FailedReasonMaxLength ? reason : reason[..FailedReasonMaxLength];
    }

    /// <summary>
    /// True when a /webhook/event body is a cxapi delivery ack (NOT an inbound message). Per the INMA
    /// shape contract an inbound message ALWAYS carries a root <c>messages</c> property and an ack NEVER
    /// does (it carries <c>Status</c>/<c>InstanceMessageID</c>). A body that has BOTH (e.g.
    /// <c>{"messages":[],"Status":1}</c>) or NEITHER is NOT a clean ack — it returns false so the caller
    /// falls through to the inbound path (and is rejected by the messages-required validation). Requiring
    /// the <c>messages</c> property to be ABSENT — not merely empty — is what keeps an ambiguous payload
    /// from being silently accepted as an ack.
    /// </summary>
    public static bool IsDeliveryAckShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("messages", out _)) return false;
        return root.TryGetProperty("Status", out _) || root.TryGetProperty("InstanceMessageID", out _);
    }
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
