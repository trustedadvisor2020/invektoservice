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
}
