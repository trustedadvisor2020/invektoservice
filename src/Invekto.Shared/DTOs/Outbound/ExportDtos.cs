using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

// =============================================================
// FEAT-OBI Phase 1A Plan B — Export Manager
// An operator exports their contact lists (data_lists/list_records) and
// bulk-send campaign results (bulk_send_recipients LEFT JOIN outbound_messages,
// per-recipient delivery status + campaign summary) as CSV / XLSX / PDF, with
// an export_logs KVKK audit trail.
//
// CSV + XLSX are built + streamed server-side (Outbound). PDF is rendered in
// the browser (jsPDF) from the report-data JSON below; the server still writes
// an export_logs row at data-serve time (delivery_mode='client_render').
// =============================================================

/// <summary>Supported export formats. CSV/XLSX stream server-side; PDF renders in-browser.</summary>
public static class ExportFormats
{
    public const string Csv = "csv";
    public const string Xlsx = "xlsx";
    public const string Pdf = "pdf";

    /// <summary>Phase 1B: audit-only "format" for list_from_export (no file is produced).</summary>
    public const string List = "list";

    /// <summary>Formats valid for a streamed (server-side) file export.</summary>
    public static bool IsStreamFormat(string? s) => s is Csv or Xlsx;
}

/// <summary>export_logs.export_type values.</summary>
public static class ExportTypes
{
    public const string ContactList = "contact_list";       // a data_lists list -> list_records
    public const string SendRecipients = "send_recipients"; // per-recipient delivery status for a bulk_send_jobs campaign
    public const string SendSummary = "send_summary";       // campaign rollup (report-data / PDF)

    // FEAT-OBI Phase 1B (Export Manager v2): filter-driven recipients surface.
    public const string FilteredRecipients = "filtered_recipients"; // a filtered CSV/XLSX export across all campaigns
    public const string ListFromExport = "list_from_export";        // "Liste Oluştur" — a new data_list built from the filter

    // FEAT-OBI Phase 2 (Telefon Numarası Ara): a single phone number's full outbound history export.
    public const string PhoneHistory = "phone_history";
}

/// <summary>export_logs.delivery_mode values (audit honesty — see migration 053).</summary>
public static class ExportDeliveryModes
{
    public const string ServerStream = "server_stream"; // bytes streamed to client (csv/xlsx)
    public const string ClientRender = "client_render"; // report-data served; PDF rendered in browser
    public const string Internal = "internal";          // Phase 1B: no egress — PII copied into a new internal list
}

/// <summary>
/// Per-recipient delivery status used across the send-results export. Encodes the
/// status precedence applied when a recipient defensively matches more than one
/// message row (best terminal state wins) and the operator-facing Turkish label.
/// Single source of truth so CSV, XLSX, and the PDF report-data all agree.
/// </summary>
public static class SendDeliveryStatus
{
    // Synthetic status for a snapshot recipient that has no outbound_messages row
    // (LEFT JOIN miss = the message was never created/sent for this phone).
    public const string NotSent = "not_sent";

    /// <summary>
    /// Precedence high->low: when multiple message rows match one recipient, keep the
    /// best terminal state. read > delivered > sent > sending > queued > failed > blocked > not_sent.
    /// </summary>
    private static readonly string[] PrecedenceHighToLow =
    {
        "read", "delivered", "sent", "sending", "queued", "failed", "blocked", NotSent
    };

    /// <summary>Lower rank = better/more-terminal state. Unknown status sorts worst.</summary>
    public static int Rank(string? status)
    {
        if (string.IsNullOrEmpty(status)) return int.MaxValue;
        var idx = Array.IndexOf(PrecedenceHighToLow, status);
        return idx < 0 ? int.MaxValue : idx;
    }

    /// <summary>Pick the higher-precedence (better) of two statuses.</summary>
    public static string Best(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? NotSent;
        if (string.IsNullOrEmpty(b)) return a;
        return Rank(a) <= Rank(b) ? a : b;
    }

    /// <summary>Operator-facing Turkish label (no internal codes leak to the export).</summary>
    public static string Label(string? status) => status switch
    {
        "read"      => "Okundu",
        "delivered" => "Teslim edildi",
        "sent"      => "Gönderildi",
        "sending"   => "Gönderiliyor",
        "queued"    => "Sırada",
        "failed"    => "Başarısız",
        "blocked"   => "Engellendi (opt-out)",
        // FEAT-PROJELER / cxapi statuses that appear on project sends (migration 056/061);
        // labelled here so the phone-history surface (and v2) show them instead of "Bilinmiyor".
        "posting"   => "Gönderiliyor",
        "ambiguous" => "Belirsiz",
        "paused"    => "Duraklatıldı",
        "cancelled" => "İptal edildi",
        NotSent     => "Gönderilmedi",
        _           => "Bilinmiyor"
    };
}

/// <summary>
/// GET /api/v1/exports/send-jobs item — a recent bulk-send campaign the operator can export.
/// Read-only metadata only (no PII); drives the Export Manager campaign picker.
/// </summary>
public sealed class SendJobSummary
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    // Nullable: NULL for HSM (wa_template_id) and inline-text jobs (chk_bulk_message_source XOR).
    [JsonPropertyName("template_id")] public int? TemplateId { get; set; }
    [JsonPropertyName("total_recipients")] public int TotalRecipients { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("completed_at")] public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// GET /api/v1/exports/send-job/{jobId}/report-data?for=pdf response.
/// Drives the in-browser PDF (summary card + capped recipient table). The server
/// writes an export_logs row (format=pdf, delivery_mode=client_render) when this is served.
/// </summary>
public sealed class SendReportData
{
    [JsonPropertyName("job_id")] public long JobId { get; set; }
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("generated_at")] public DateTime GeneratedAt { get; set; }

    [JsonPropertyName("summary")] public SendReportSummary Summary { get; set; } = new();

    /// <summary>Total recipients in the campaign snapshot (may exceed the table below).</summary>
    [JsonPropertyName("total_recipient_count")] public int TotalRecipientCount { get; set; }

    /// <summary>True when the recipient table was capped (use CSV/XLSX for the full set).</summary>
    [JsonPropertyName("recipient_table_truncated")] public bool RecipientTableTruncated { get; set; }

    /// <summary>Hard cap applied to the in-PDF recipient table.</summary>
    [JsonPropertyName("recipient_table_limit")] public int RecipientTableLimit { get; set; }

    [JsonPropertyName("recipients")] public List<SendRecipientRow> Recipients { get; set; } = new();
}

/// <summary>Campaign rollup for the PDF summary card.</summary>
public sealed class SendReportSummary
{
    // Nullable: NULL for HSM (wa_template_id) and inline-text jobs (chk_bulk_message_source XOR);
    // TemplateName is then null too. Sourced from JobExportMeta.TemplateId (audit Outbound-5).
    [JsonPropertyName("template_id")] public int? TemplateId { get; set; }
    [JsonPropertyName("template_name")] public string? TemplateName { get; set; }
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";

    [JsonPropertyName("total_recipients")] public int TotalRecipients { get; set; }
    [JsonPropertyName("sent")] public int Sent { get; set; }
    [JsonPropertyName("delivered")] public int Delivered { get; set; }
    [JsonPropertyName("read")] public int Read { get; set; }
    [JsonPropertyName("failed")] public int Failed { get; set; }
    [JsonPropertyName("blocked")] public int Blocked { get; set; }
    [JsonPropertyName("not_sent")] public int NotSent { get; set; }

    [JsonPropertyName("skipped_optout")] public int SkippedOptout { get; set; }
    [JsonPropertyName("skipped_consent")] public int SkippedConsent { get; set; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("confirmed_at")] public DateTime? ConfirmedAt { get; set; }
    [JsonPropertyName("completed_at")] public DateTime? CompletedAt { get; set; }
}

/// <summary>One per-recipient row in the send-results report (PDF table + parity with CSV/XLSX).</summary>
public sealed class SendRecipientRow
{
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = SendDeliveryStatus.NotSent;
    [JsonPropertyName("status_label")] public string StatusLabel { get; set; } = "";
    [JsonPropertyName("sent_at")] public DateTime? SentAt { get; set; }
    [JsonPropertyName("delivered_at")] public DateTime? DeliveredAt { get; set; }
    [JsonPropertyName("read_at")] public DateTime? ReadAt { get; set; }
    [JsonPropertyName("failed_reason")] public string? FailedReason { get; set; }
}

// =============================================================
// FEAT-OBI Phase 1B — Export Manager v2 (filter-driven recipients surface)
// One surface over ALL of a tenant's bulk-send recipients, filtered by template
// / campaign / list-membership / delivery status / campaign-date. Drives an
// instant unique-vs-total count, filtered CSV/XLSX, and "Liste Oluştur" (a new
// data_list source='export' from the filtered unique sendable phones). Audit:
// export_logs (type 'filtered_recipients' | 'list_from_export').
// =============================================================

/// <summary>
/// Filter applied across all of the tenant's bulk-send recipients. Every field is
/// optional; present fields are AND-ed. The status is one of SendDeliveryStatus's
/// values ('read'|'delivered'|'sent'|'sending'|'queued'|'failed'|'blocked'|'not_sent')
/// or null/empty = all. Dates bound bulk_send_jobs.created_at (the campaign date).
/// This is a query DTO bound from the querystring on GET endpoints.
/// </summary>
public sealed class ExportFilter
{
    /// <summary>outbound_templates.id — restrict to campaigns using this template.</summary>
    [JsonPropertyName("template_id")] public int? TemplateId { get; set; }

    /// <summary>bulk_send_jobs.id — restrict to a single campaign.</summary>
    [JsonPropertyName("job_id")] public long? JobId { get; set; }

    /// <summary>data_lists.id — keep only recipients whose phone is a member of this list.</summary>
    [JsonPropertyName("list_id")] public long? ListId { get; set; }

    /// <summary>Computed best delivery status to keep; null/empty = all statuses.</summary>
    [JsonPropertyName("status")] public string? Status { get; set; }

    /// <summary>Inclusive lower bound on the campaign date (UTC).</summary>
    [JsonPropertyName("from")] public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on the campaign date (treated as end-of-day; UTC).</summary>
    [JsonPropertyName("to")] public DateTime? To { get; set; }
}

/// <summary>GET /api/v1/exports/recipients/count response — the count card.</summary>
public sealed class FilteredCountResult
{
    /// <summary>Distinct normalized phones matching the filter ("benzersiz numara").</summary>
    [JsonPropertyName("unique_count")] public int UniqueCount { get; set; }

    /// <summary>Total recipient rows matching the filter, repeats included ("toplam kayıt").</summary>
    [JsonPropertyName("total_count")] public int TotalCount { get; set; }
}

/// <summary>One filtered recipient row in the CSV/XLSX export (cross-campaign).</summary>
public sealed class FilteredRecipientRow
{
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = SendDeliveryStatus.NotSent;
    [JsonPropertyName("status_label")] public string StatusLabel { get; set; } = "";
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("template_name")] public string? TemplateName { get; set; }
    [JsonPropertyName("sent_at")] public DateTime? SentAt { get; set; }
    [JsonPropertyName("delivered_at")] public DateTime? DeliveredAt { get; set; }
    [JsonPropertyName("read_at")] public DateTime? ReadAt { get; set; }
    [JsonPropertyName("campaign_date")] public DateTime CampaignDate { get; set; }
}

/// <summary>GET /api/v1/exports/filter-options response — populates the 5 filter dropdowns.</summary>
public sealed class ExportFilterOptions
{
    [JsonPropertyName("templates")] public List<FilterOptionItem> Templates { get; set; } = new();
    [JsonPropertyName("campaigns")] public List<FilterOptionItem> Campaigns { get; set; } = new();
    [JsonPropertyName("lists")] public List<FilterOptionItem> Lists { get; set; } = new();
}

/// <summary>A single id/label option for a filter dropdown.</summary>
public sealed class FilterOptionItem
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}

/// <summary>POST /api/v1/exports/recipients/create-list body — "Liste Oluştur".</summary>
public sealed class CreateListFromExportRequest
{
    /// <summary>Name for the new data_list (must be unique among the tenant's active lists).</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>The active filter; the list is built from its distinct sendable phones.</summary>
    [JsonPropertyName("filter")] public ExportFilter Filter { get; set; } = new();
}

/// <summary>POST /api/v1/exports/recipients/create-list response.</summary>
public sealed class CreateListFromExportResult
{
    [JsonPropertyName("list_id")] public long ListId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Number of distinct sendable phones written into the new list.</summary>
    [JsonPropertyName("record_count")] public int RecordCount { get; set; }
}

/// <summary>GET /api/v1/exports/history item — one export_logs row for the history table.</summary>
public sealed class ExportLogEntry
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("export_type")] public string ExportType { get; set; } = "";
    [JsonPropertyName("source_name")] public string? SourceName { get; set; }
    [JsonPropertyName("format")] public string Format { get; set; } = "";
    [JsonPropertyName("delivery_mode")] public string DeliveryMode { get; set; } = "";
    [JsonPropertyName("row_count")] public int RowCount { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("requested_by")] public string? RequestedBy { get; set; }
    [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
}

// =============================================================
// FEAT-OBI Phase 2 — Telefon Numarası Ara (single-number history)
// Operator enters a phone -> the tenant's FULL outbound history to that number
// (bulk + project + legacy broadcast), newest-first, with metadata + message body.
// Read-only; tenant resolved from JWT only. Screen is capped (rows + truncated
// marker); CSV/PDF export the full set. Audit: export_logs (type 'phone_history').
// =============================================================

/// <summary>POST /api/v1/exports/phone-history (and /download) body. Phone is the raw operator
/// input; the server normalizes it with the SAME PhoneNormalizer used at send time. Carried in a
/// POST body (not the querystring) because a phone number is PII and must not leak to access logs.</summary>
public sealed class PhoneHistoryRequest
{
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";

    /// <summary>Download only: 'csv' for the streamed export. Ignored by the screen lookup endpoint.</summary>
    [JsonPropertyName("format")] public string? Format { get; set; }
}

/// <summary>POST /api/v1/exports/phone-history response — the on-screen lookup result.</summary>
public sealed class PhoneHistoryResult
{
    /// <summary>The normalized (E.164 '+...') phone the history was queried for.</summary>
    [JsonPropertyName("normalized_phone")] public string NormalizedPhone { get; set; } = "";

    /// <summary>Rows returned (capped to <see cref="Limit"/>); newest-first.</summary>
    [JsonPropertyName("rows")] public List<PhoneHistoryMessageRow> Rows { get; set; } = new();

    /// <summary>True when more rows exist than the screen cap — operator should use CSV for the full set.</summary>
    [JsonPropertyName("truncated")] public bool Truncated { get; set; }

    /// <summary>Screen-cap applied to <see cref="Rows"/>.</summary>
    [JsonPropertyName("limit")] public int Limit { get; set; }
}

/// <summary>
/// One outbound_messages row in a single number's history. Every column here is read from a
/// nullable DB column EXCEPT status/created_at/message_kind — all reads are IsDBNull-guarded.
/// </summary>
public sealed class PhoneHistoryMessageRow
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("status_label")] public string StatusLabel { get; set; } = "";
    [JsonPropertyName("sent_at")] public DateTime? SentAt { get; set; }
    [JsonPropertyName("delivered_at")] public DateTime? DeliveredAt { get; set; }
    [JsonPropertyName("read_at")] public DateTime? ReadAt { get; set; }

    /// <summary>Template name (gallery/INSE) when resolvable; null for inline/free-text sends.</summary>
    [JsonPropertyName("template_name")] public string? TemplateName { get; set; }

    /// <summary>Campaign id — populated for bulk-send rows only; null for project/legacy single sends.</summary>
    [JsonPropertyName("campaign_id")] public string? CampaignId { get; set; }

    /// <summary>Human campaign (project) name from the row's bulk-send job project_id; null for project-less /
    /// legacy sends. The SPA's Kampanya column shows this (falls back to '—'); CSV/PDF fall back to campaign_id.</summary>
    [JsonPropertyName("campaign_name")] public string? CampaignName { get; set; }

    /// <summary>cxapi approved-template id (outbound_messages.template_ref) for HSM rows — same value as the
    /// "[WAPCRM_TEMPLATE] {id}" body marker and as WaTemplate.templateId. The SPA resolves it to the template
    /// name + structured preview via the existing per-channel template list. Null for plain-text/gallery rows.</summary>
    [JsonPropertyName("wa_template_id")] public string? WaTemplateId { get; set; }

    /// <summary>cxapi channel (outbound_messages.instance_id) the HSM message was sent through — the key the SPA
    /// uses to fetch that channel's approved-template list for name/preview resolution. Null for bridge/plain rows.</summary>
    [JsonPropertyName("instance_id")] public int? InstanceId { get; set; }

    [JsonPropertyName("message_kind")] public string MessageKind { get; set; } = "";

    /// <summary>The sent message body (message_text). Personal data — tenant-scoped + audited on export.</summary>
    [JsonPropertyName("message_text")] public string MessageText { get; set; } = "";

    /// <summary>
    /// Not-sent/failure reason text (COALESCE(failed_reason, NULLIF(provider_error_message,'Success'))),
    /// null when the message did not fail. The SPA's WaErrorPopover extracts any "(NNNNN)" WhatsApp code
    /// from this string — same field shape the Projeler report feeds the popover.
    /// </summary>
    [JsonPropertyName("error")] public string? Error { get; set; }
}
