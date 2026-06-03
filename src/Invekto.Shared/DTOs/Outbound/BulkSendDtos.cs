using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

// =============================================================
// FEAT-OBI Phase 0 — Bulk Send (CSV source)
// Parent-job layer over the existing /broadcast/send engine.
// Flow: preview (parse+dedup+snapshot) -> confirm (chunked send) -> status.
// =============================================================

/// <summary>
/// POST /api/v1/bulk-send/preview request.
/// Operator supplies a recipient list as raw CSV/pasted text (Phase 0 source is CSV only).
/// No message is sent — this only parses, normalizes, dedups and snapshots.
/// </summary>
public sealed class BulkSendPreviewRequest
{
    /// <summary>Client-supplied idempotency key (Codex §5). Unique per tenant.</summary>
    [JsonPropertyName("campaign_id")]
    public string CampaignId { get; set; } = "";

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    /// <summary>
    /// Raw CSV/pasted text. One recipient per line. First column = phone.
    /// Optional extra columns map to template variables via <see cref="VariableColumns"/>.
    /// Required (Phase 0 source is CSV only).
    /// </summary>
    [JsonPropertyName("csv_text")]
    public string? CsvText { get; set; }

    /// <summary>
    /// Header names for CSV columns 2..N (column 1 is always phone).
    /// e.g. ["name","city"] -> variables {name, city}. Empty = phone-only.
    /// </summary>
    [JsonPropertyName("variable_columns")]
    public List<string>? VariableColumns { get; set; }
}

/// <summary>
/// POST /api/v1/bulk-send/preview response. Operator reviews this before confirm.
/// </summary>
public sealed class BulkSendPreviewResponse
{
    [JsonPropertyName("campaign_id")]
    public string CampaignId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("hard_cap")]
    public int HardCap { get; set; }

    [JsonPropertyName("total_input")]
    public int TotalInput { get; set; }

    [JsonPropertyName("total_valid")]
    public int TotalValid { get; set; }

    [JsonPropertyName("total_duplicate")]
    public int TotalDuplicate { get; set; }

    [JsonPropertyName("total_invalid")]
    public int TotalInvalid { get; set; }

    /// <summary>First N normalized phones for eyeball verification (Codex §1 preview).</summary>
    [JsonPropertyName("sample")]
    public List<string> Sample { get; set; } = new();

    /// <summary>Raw values that failed normalization, for operator correction (capped).</summary>
    [JsonPropertyName("invalid_samples")]
    public List<string> InvalidSamples { get; set; } = new();
}

/// <summary>
/// POST /api/v1/bulk-send/confirm request. Sends the snapshot built at preview.
/// Idempotent on campaign_id: a second confirm returns the existing job.
/// </summary>
public sealed class BulkSendConfirmRequest
{
    [JsonPropertyName("campaign_id")]
    public string CampaignId { get; set; } = "";
}

/// <summary>
/// Confirm + status response. Aggregates the parent job over its child broadcasts.
/// </summary>
public sealed class BulkSendStatusResponse
{
    [JsonPropertyName("campaign_id")]
    public string CampaignId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("total_valid")]
    public int TotalValid { get; set; }

    [JsonPropertyName("total_queued")]
    public int TotalQueued { get; set; }

    [JsonPropertyName("total_skipped_optout")]
    public int TotalSkippedOptout { get; set; }

    [JsonPropertyName("total_skipped_consent")]
    public int TotalSkippedConsent { get; set; }

    // Aggregated delivery counters across all child broadcasts
    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("delivered")]
    public int Delivered { get; set; }

    [JsonPropertyName("read")]
    public int Read { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("broadcast_count")]
    public int BroadcastCount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
