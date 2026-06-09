using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

/// <summary>
/// POST /api/v1/broadcast/send request body.
/// Main App sends recipients + template_id to start a broadcast.
/// </summary>
public sealed class BroadcastSendRequest
{
    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    /// <summary>
    /// FEAT-PROJELER send-exec (SS-A): inline free-text body. When set (and <see cref="TemplateId"/>
    /// is 0/unset) the broadcast sends this literal text to EVERY recipient — no INSE template lookup,
    /// no DMP/dynamic substitution. EXACTLY ONE of template_id / message_text must be supplied; the
    /// orchestrator rejects both-or-neither. Used by the Projeler free_text run path (SS-C). Null for
    /// the legacy template-based broadcast/bulk path (fully backward-compatible).
    /// </summary>
    [JsonPropertyName("message_text")]
    public string? MessageText { get; set; }

    [JsonPropertyName("recipients")]
    public List<BroadcastRecipient> Recipients { get; set; } = new();

    [JsonPropertyName("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// GR-2.3: Target language for broadcast (ISO 639-1).
    /// If set, template must match this language.
    /// Stored on broadcast and individual messages for tracking.
    /// </summary>
    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    // ---------------------------------------------------------
    // FEAT-PROJELER / cxapi (PR-1, migration 055) — reserved, NO-OP.
    // Accepted on the wire for the upcoming cxapi route (PR-3/PR-4); the
    // broadcast orchestrator IGNORES them in PR-1. All nullable, so existing
    // /broadcast/send requests stay valid.
    // ---------------------------------------------------------

    /// <summary>WapCRM instance the broadcast targets. Reserved (PR-3); unused in PR-1.</summary>
    [JsonPropertyName("instance_id")]
    public int? InstanceId { get; set; }

    /// <summary>Send kind: <c>plain_text</c> | <c>wapcrm_template</c>. Reserved (PR-4); unused in PR-1.</summary>
    [JsonPropertyName("template_kind")]
    public string? TemplateKind { get; set; }

    /// <summary>WhatsApp approved-template (HSM) id. Reserved (PR-4); unused in PR-1.</summary>
    [JsonPropertyName("wa_template_id")]
    public string? WaTemplateId { get; set; }

    /// <summary>Approved-template language (ISO 639-1). Reserved (PR-4); unused in PR-1.</summary>
    [JsonPropertyName("template_language")]
    public string? TemplateLanguage { get; set; }

    /// <summary>Maps approved-template params to recipient variables. Reserved (PR-4); unused in PR-1.</summary>
    [JsonPropertyName("param_mapping")]
    public List<TemplateParamMapping>? ParamMapping { get; set; }

    /// <summary>
    /// PR-4 (HSM): the dynamic HEADER media literal URL for this broadcast (one per broadcast —
    /// the cxapi wire carries a single headerMedia). Null when the template needs no dynamic media.
    /// Per-recipient PARAM VALUES are NOT here — they ride each <see cref="BroadcastRecipient.Variables"/>
    /// (resolved at preview snapshot; the orchestrator copies, never re-derives).
    /// </summary>
    [JsonPropertyName("template_header_media_url")]
    public string? TemplateHeaderMediaUrl { get; set; }
}

public sealed class BroadcastRecipient
{
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("variables")]
    public Dictionary<string, string>? Variables { get; set; }
}

/// <summary>
/// 202 Accepted response for broadcast send.
/// </summary>
public sealed class BroadcastSendResponse
{
    [JsonPropertyName("broadcast_id")]
    public Guid BroadcastId { get; set; }

    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; set; }

    [JsonPropertyName("queued")]
    public int Queued { get; set; }

    [JsonPropertyName("skipped_optout")]
    public int SkippedOptout { get; set; }

    /// <summary>
    /// GR-3.26: Recipients skipped due to missing marketing consent.
    /// </summary>
    [JsonPropertyName("skipped_consent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SkippedConsent { get; set; }
}

/// <summary>
/// GET /api/v1/broadcast/{broadcastId}/status response.
/// </summary>
public sealed class BroadcastStatusResponse
{
    [JsonPropertyName("broadcast_id")]
    public Guid BroadcastId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; set; }

    [JsonPropertyName("queued")]
    public int Queued { get; set; }

    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("delivered")]
    public int Delivered { get; set; }

    [JsonPropertyName("read")]
    public int Read { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// <summary>
    /// FEAT-PROJELER / cxapi (PR-3a): cxapi sends that ended with unknown delivery
    /// (timeout/transport/stranded-posting). Counted separately from sent/failed so the
    /// totals reconcile (sent + failed + ambiguous + delivered + read + queued = total).
    /// Always 0 for bridge-only broadcasts.
    /// </summary>
    [JsonPropertyName("ambiguous")]
    public int Ambiguous { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
