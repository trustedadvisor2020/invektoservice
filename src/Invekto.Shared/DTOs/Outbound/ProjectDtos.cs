using System.Text.Json;
using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

// =============================================================
// FEAT-PROJELER (PKT-14) slice S2 — Projects CRUD (data layer)
// A "Proje" is the reusable parent over the bulk send engine: it groups a target
// set (1..N data_lists) under one named campaign. A Run = a bulk_send_job that
// hangs off the project (project_id FK, wired when runs dispatch in PR-4).
//
// S2 is METADATA CRUD only: name / description / target set + soft-delete-as-archive.
// The project-level cxapi send config (instance_id/template_kind/wa_template_id/
// template_language/param_mapping) and the denormalized run counters stay INERT here
// — PR-4 owns the send wiring + counter recompute, exactly like S1 reserved them.
// =============================================================

/// <summary>Project lifecycle states (mirrors chk_project_status, migration 057).</summary>
public static class ProjectStatuses
{
    public const string Draft = "draft";         // created, not yet launched (S2 default on create)
    public const string Running = "running";     // a run is dispatched / in flight (PR-4)
    public const string Paused = "paused";       // operator paused; runs may resume (PR-4)
    public const string Completed = "completed"; // all runs finished (PR-4)
    public const string Cancelled = "cancelled"; // stopped before completion (PR-4)
    public const string Archived = "archived";   // soft-deleted (S2 archive); name freed

    public static bool IsValid(string? s) =>
        s is Draft or Running or Paused or Completed or Cancelled or Archived;
}

/// <summary>
/// Project send message kind (mirrors chk_project_template_kind / projects.template_kind, migration 057).
/// Set when the operator configures the project's send channel + content (slice: channel+template settings).
/// </summary>
public static class ProjectTemplateKinds
{
    public const string PlainText = "plain_text";          // plain free-text bulk (PR-3a send path; text supplied at send time)
    public const string WapcrmTemplate = "wapcrm_template"; // approved WapCRM HSM template (wa_template_id + param_mapping)

    public static bool IsValid(string? s) => s is PlainText or WapcrmTemplate;
}

/// <summary>
/// plain_text content source (mirrors chk_project_content_mode / projects.content_mode, migration 059).
/// Applies ONLY when <see cref="ProjectTemplateKinds.PlainText"/>: the operator picks WHAT a plain_text
/// run sends in the project settings — either a Şablon Galerisi template, or free text typed in-place.
/// </summary>
public static class ProjectContentModes
{
    public const string GalleryTemplate = "gallery_template"; // outbound_template_id -> a saved Şablon Galerisi template
    public const string FreeText = "free_text";               // plain_text_body -> free text, dispatched via the inline-text bulk path

    public static bool IsValid(string? s) => s is GalleryTemplate or FreeText;
}

/// <summary>GET /api/v1/projects item (list view).</summary>
public sealed class ProjectSummary
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = ProjectStatuses.Draft;

    /// <summary>Number of data_lists this project currently targets (computed live, not stored).</summary>
    [JsonPropertyName("target_count")] public int TargetCount { get; set; }

    // Denormalized run roll-up counters. INERT 0 until runs attach in PR-4 — surfaced
    // now so the list contract is stable when PR-4 starts populating them.
    [JsonPropertyName("run_count")] public int RunCount { get; set; }
    [JsonPropertyName("total_targets")] public int TotalTargets { get; set; }
    [JsonPropertyName("sent_count")] public int SentCount { get; set; }
    [JsonPropertyName("delivered_count")] public int DeliveredCount { get; set; }
    [JsonPropertyName("read_count")] public int ReadCount { get; set; }
    [JsonPropertyName("failed_count")] public int FailedCount { get; set; }
    [JsonPropertyName("ambiguous_count")] public int AmbiguousCount { get; set; }
    /// <summary>Operator-cancelled messages across this project's runs (SS-D, migration 061). 0 until a run is cancelled.</summary>
    [JsonPropertyName("cancelled_count")] public int CancelledCount { get; set; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("started_at")] public DateTime? StartedAt { get; set; }
    [JsonPropertyName("completed_at")] public DateTime? CompletedAt { get; set; }

    // ---- Send config (channel + template), persisted into the migration-057 cxapi columns.
    // Surfaced so the edit modal can re-populate the operator's channel/template choices.
    // Send EXECUTION that consumes these is a later slice; they are persisted now, not dispatched.

    /// <summary>WapCRM Cloud API instance (cxapi integer instanceID) this project sends from. Null until configured.</summary>
    [JsonPropertyName("instance_id")] public int? InstanceId { get; set; }
    /// <summary>Message kind: 'plain_text' | 'wapcrm_template' (see <see cref="ProjectTemplateKinds"/>). Null until configured.</summary>
    [JsonPropertyName("template_kind")] public string? TemplateKind { get; set; }
    /// <summary>Selected approved-template id (cxapi templateId) when TemplateKind == wapcrm_template.</summary>
    [JsonPropertyName("wa_template_id")] public string? WaTemplateId { get; set; }
    /// <summary>Template language code (reserved; language is embedded in the templateId slug today). Usually null.</summary>
    [JsonPropertyName("template_language")] public string? TemplateLanguage { get; set; }
    /// <summary>Operator-filled template parameters (requiredInputs) as stored JSONB; null for plain_text / zero-param templates.</summary>
    [JsonPropertyName("param_mapping")] public JsonElement? ParamMapping { get; set; }

    // ---- plain_text content config (migration 059). Applies only to TemplateKind == plain_text.
    // Surfaced so the edit modal can re-populate the operator's content choice (template vs free text).

    /// <summary>plain_text content source: 'gallery_template' | 'free_text' (see <see cref="ProjectContentModes"/>). Null until a plain_text content is configured.</summary>
    [JsonPropertyName("content_mode")] public string? ContentMode { get; set; }
    /// <summary>Selected Şablon Galerisi (outbound_templates) id when ContentMode == gallery_template; null otherwise.</summary>
    [JsonPropertyName("outbound_template_id")] public int? OutboundTemplateId { get; set; }
    /// <summary>Free text the run sends (via the inline-text bulk path) when ContentMode == free_text; null otherwise.</summary>
    [JsonPropertyName("plain_text_body")] public string? PlainTextBody { get; set; }
}

/// <summary>One targeted data_list inside a project (GET detail view).</summary>
public sealed class ProjectTargetDto
{
    [JsonPropertyName("data_list_id")] public long DataListId { get; set; }
    [JsonPropertyName("list_name")] public string ListName { get; set; } = "";
    [JsonPropertyName("total_records")] public int TotalRecords { get; set; }
    [JsonPropertyName("sendable_count")] public int SendableCount { get; set; }
    [JsonPropertyName("sort_order")] public int SortOrder { get; set; }
}

/// <summary>GET /api/v1/projects/{id} — a project plus its ordered target lists.</summary>
public sealed class ProjectDetail
{
    [JsonPropertyName("project")] public ProjectSummary Project { get; set; } = new();
    [JsonPropertyName("targets")] public List<ProjectTargetDto> Targets { get; set; } = new();
}

/// <summary>POST /api/v1/projects — create a project with its initial target set.</summary>
public sealed class CreateProjectRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }

    /// <summary>data_list ids targeted by this project (deduped + order-preserved). May be empty.</summary>
    [JsonPropertyName("target_list_ids")] public List<long> TargetListIds { get; set; } = new();

    // ---- Optional send config on create. When template_kind is supplied the whole block is
    // validated + persisted; when null the project is created without send config (draft).
    [JsonPropertyName("instance_id")] public int? InstanceId { get; set; }
    [JsonPropertyName("template_kind")] public string? TemplateKind { get; set; }
    [JsonPropertyName("wa_template_id")] public string? WaTemplateId { get; set; }
    [JsonPropertyName("template_language")] public string? TemplateLanguage { get; set; }
    [JsonPropertyName("param_mapping")] public JsonElement? ParamMapping { get; set; }

    // ---- plain_text content (migration 059). Validated only when TemplateKind == plain_text;
    // content_mode drives which of outbound_template_id / plain_text_body is required.
    [JsonPropertyName("content_mode")] public string? ContentMode { get; set; }
    [JsonPropertyName("outbound_template_id")] public int? OutboundTemplateId { get; set; }
    [JsonPropertyName("plain_text_body")] public string? PlainTextBody { get; set; }
}

/// <summary>
/// PUT /api/v1/projects/{id} — partial update. name/description are COALESCE-partial
/// (null = leave unchanged). target_list_ids: null = leave the target set untouched;
/// non-null (incl. empty) = REPLACE the whole target set with the supplied ids.
/// </summary>
public sealed class UpdateProjectRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("target_list_ids")] public List<long>? TargetListIds { get; set; }

    // ---- Send config. template_kind is the DRIVER: when non-null, the WHOLE config block
    // (instance_id/template_kind/wa_template_id/template_language/param_mapping) is validated and
    // REPLACED authoritatively (the edit modal always sends the full current config). When
    // template_kind is null the config block is left untouched (metadata-only update).
    [JsonPropertyName("instance_id")] public int? InstanceId { get; set; }
    [JsonPropertyName("template_kind")] public string? TemplateKind { get; set; }
    [JsonPropertyName("wa_template_id")] public string? WaTemplateId { get; set; }
    [JsonPropertyName("template_language")] public string? TemplateLanguage { get; set; }
    [JsonPropertyName("param_mapping")] public JsonElement? ParamMapping { get; set; }

    // ---- plain_text content (migration 059). Part of the send-config block: when template_kind is
    // non-null the whole block (incl. content) is validated + replaced authoritatively; when null it
    // is left untouched. content_mode drives which content carrier is required for plain_text.
    [JsonPropertyName("content_mode")] public string? ContentMode { get; set; }
    [JsonPropertyName("outbound_template_id")] public int? OutboundTemplateId { get; set; }
    [JsonPropertyName("plain_text_body")] public string? PlainTextBody { get; set; }
}

/// <summary>
/// FEAT-PROJELER (PKT-14) send-exec SS-C — body for the project run preview/confirm endpoints
/// (POST /api/v1/projects/{id}/send/preview and .../send/confirm). The project id comes from the
/// route; the audience + content + instance are read from the project (not supplied here). A run
/// is a bulk_send_job(project_id) so it reuses the bulk preview->confirm machinery: the frontend
/// generates one campaign_id per Gönder flow (idempotency key) and passes it to BOTH calls.
/// </summary>
public sealed class ProjectSendRequest
{
    /// <summary>Client-supplied idempotency key, unique per tenant. One per Gönder flow (a run).</summary>
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
}

/// <summary>
/// FEAT-PROJELER UI/UX pack (GR-8) — body for POST /api/v1/projects/send/test: ONE message to
/// ONE number, fired from the project modal so the operator can smoke-test content on their own
/// phone BEFORE saving/dispatching. Not bound to a project id (works in create mode). EXACTLY ONE
/// content source: <see cref="MessageText"/> (plain text) OR <see cref="WaTemplateId"/> (approved
/// HSM template — Q 2026-06-10 öğleden sonra: the test must exercise the REAL template wire model
/// per wapcrm-api-integration-guide §3.2; the earlier render-text-as-plain approximation is
/// superseded now that PR-4 + the CxapiSend gate are live). Server side: Projects + BulkSend gates,
/// per-tenant throttle, PhoneNormalizer, then the SAME BroadcastOrchestrator path every real
/// broadcast uses (HSM rides cxapi only — non-allowlisted tenants reject INV-OB-085 inside the
/// orchestrator). Response is the standard BroadcastSendResponse.
/// </summary>
public sealed class ProjectTestSendRequest
{
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";

    /// <summary>Plain-text body. Empty when <see cref="WaTemplateId"/> drives an HSM test.</summary>
    [JsonPropertyName("message_text")] public string MessageText { get; set; } = "";

    /// <summary>HSM test: the sending Cloud API channel (cxapi instanceID). Must be tenant-owned; required with <see cref="WaTemplateId"/>.</summary>
    [JsonPropertyName("instance_id")] public int? InstanceId { get; set; }

    /// <summary>HSM test: approved-template slug. Non-empty switches the test to the template wire model.</summary>
    [JsonPropertyName("wa_template_id")] public string? WaTemplateId { get; set; }

    /// <summary>HSM test: RESOLVED named parameter values (paramKey → value) — goes out as template.parameters[{paramKey,value}].</summary>
    [JsonPropertyName("template_params")] public Dictionary<string, string>? TemplateParams { get; set; }

    /// <summary>HSM test: dynamic HEADER media public URL (template.headerMedia). Null = omitted.</summary>
    [JsonPropertyName("template_header_media_url")] public string? TemplateHeaderMediaUrl { get; set; }
}

// =============================================================
// FEAT-PROJELER — Rapor (per-run delivery report) — 2026-06-12
// A project's runs (bulk_send_jobs) -> broadcasts -> per-recipient outbound_messages.
// The report drawer lists each run (dropdown) and a searchable/paged per-recipient status
// table; an undelivered recipient (failed/ambiguous) can be resent. Read-only aggregation
// over existing rows + an in-place re-queue for resend (no new send-orchestration path).
// =============================================================

/// <summary>One run (gönderim) of a project — a dropdown entry in the report drawer. Counters are
/// the live partition sums over the run's broadcasts (a recipient is in exactly one bucket).</summary>
public sealed class ProjectRunDto
{
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("queued")] public int Queued { get; set; }
    [JsonPropertyName("sent")] public int Sent { get; set; }
    [JsonPropertyName("delivered")] public int Delivered { get; set; }
    [JsonPropertyName("read")] public int Read { get; set; }
    [JsonPropertyName("failed")] public int Failed { get; set; }
    [JsonPropertyName("ambiguous")] public int Ambiguous { get; set; }
    [JsonPropertyName("cancelled")] public int Cancelled { get; set; }
}

/// <summary>One recipient row in the report table: the message's current status + (when undelivered)
/// the failure reason, plus the delivery timestamps. <see cref="CanResend"/> is true only for the
/// resend-eligible terminal states (failed / ambiguous) — the UI shows the Resend button on those.</summary>
public sealed class ProjectRecipientDto
{
    [JsonPropertyName("message_id")] public long MessageId { get; set; }
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>failed_reason (preferred) or provider_error_message; null for non-error states.</summary>
    [JsonPropertyName("error")] public string? Error { get; set; }
    /// <summary>sent_at — when the message left for the provider (first 'sent' transition); null while still queued/sending.</summary>
    [JsonPropertyName("sent_at")] public DateTime? SentAt { get; set; }
    [JsonPropertyName("delivered_at")] public DateTime? DeliveredAt { get; set; }
    [JsonPropertyName("read_at")] public DateTime? ReadAt { get; set; }
    [JsonPropertyName("last_attempt_at")] public DateTime? LastAttemptAt { get; set; }
    [JsonPropertyName("can_resend")] public bool CanResend { get; set; }
}

/// <summary>FEATURE B (status-pull): result of a "Durumu Yenile" run — how many pending recipients were
/// queried against cxapi and how many actually advanced status (monotonic apply; a no-op ack counts as 0).</summary>
public sealed class ProjectStatusPullResultDto
{
    [JsonPropertyName("checked")] public int Checked { get; set; }
    [JsonPropertyName("updated")] public int Updated { get; set; }
}

/// <summary>Server-paged recipient page for GET .../report/recipients. <see cref="Total"/> is the full
/// filtered count (drives pagination + the "export all filtered" CSV which re-fetches with a big page).</summary>
public sealed class ProjectRecipientsPage
{
    [JsonPropertyName("items")] public List<ProjectRecipientDto> Items { get; set; } = new();
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("page_size")] public int PageSize { get; set; }
}

/// <summary>POST .../report/resend — re-queue ONE undelivered recipient (by message id) for a real
/// re-send via the project's preserved send config. Only 'failed'/'ambiguous' rows are eligible.</summary>
public sealed class ProjectResendRequest
{
    [JsonPropertyName("message_id")] public long MessageId { get; set; }
}

/// <summary>POST .../report/refresh-status — FEATURE B status-pull. Optional run scope: when
/// <see cref="CampaignId"/> is set only that run's pending recipients are pulled; null = all runs.</summary>
public sealed class ProjectStatusPullRequest
{
    [JsonPropertyName("campaign_id")] public string? CampaignId { get; set; }
}
