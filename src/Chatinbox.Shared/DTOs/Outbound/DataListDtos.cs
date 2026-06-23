using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Outbound;

// =============================================================
// FEAT-OBI Phase 1A — Contact Lists (data layer)
// Reusable tenant-scoped contact lists that feed the bulk send engine.
// Excel/CSV are parsed CLIENT-SIDE (SheetJS); the client posts already
// field-mapped rows here. The server normalizes phones, dedups, applies
// the import scenario, and snapshots a list into a send via preview-from-list.
// =============================================================

/// <summary>Import scenario keys (adapted from TONIVA's 4 modes; 'callable' -> 'sendable').</summary>
public static class ImportScenarios
{
    public const string NewOnly = "new_only";        // insert phones not already in the list; skip existing
    public const string RecallAll = "recall_all";    // upsert: reactivate (sendable=true) + merge non-empty fields
    public const string UpdateInfo = "update_info";  // update fields of EXISTING records only; no insert, no sendable change
    public const string Custom = "custom";           // insert new + apply explicit duplicate flags

    public static bool IsValid(string? s) =>
        s is NewOnly or RecallAll or UpdateInfo or Custom;
}

/// <summary>GET /api/v1/data-lists item.</summary>
public sealed class DataListSummary
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "upload";
    [JsonPropertyName("active")] public bool Active { get; set; } = true;
    [JsonPropertyName("status")] public string Status { get; set; } = "ready";
    [JsonPropertyName("total_records")] public int TotalRecords { get; set; }
    [JsonPropertyName("sendable_count")] public int SendableCount { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>POST /api/v1/data-lists — create an empty list.</summary>
public sealed class CreateDataListRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

/// <summary>PATCH /api/v1/data-lists/{id} — rename / toggle visibility.</summary>
public sealed class UpdateDataListRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("active")] public bool? Active { get; set; }
}

/// <summary>Explicit duplicate-handling flags for the 'custom' scenario.</summary>
public sealed class ImportCustomFlags
{
    /// <summary>Set sendable=true on records that already exist in the list.</summary>
    [JsonPropertyName("set_duplicates_sendable")] public bool SetDuplicatesSendable { get; set; }

    /// <summary>Overwrite non-empty fields on records that already exist in the list.</summary>
    [JsonPropertyName("update_duplicate_fields")] public bool UpdateDuplicateFields { get; set; }
}

/// <summary>One field-mapped contact row (client posts these after Excel/CSV parse + mapping).</summary>
public sealed class ImportRowDto
{
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("surname")] public string? Surname { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("field1")] public string? Field1 { get; set; }
    [JsonPropertyName("field2")] public string? Field2 { get; set; }
    [JsonPropertyName("field3")] public string? Field3 { get; set; }
    [JsonPropertyName("field4")] public string? Field4 { get; set; }
    [JsonPropertyName("field5")] public string? Field5 { get; set; }
    [JsonPropertyName("custom_fields")] public Dictionary<string, string>? CustomFields { get; set; }
}

/// <summary>
/// POST /api/v1/data-lists/import — bulk import field-mapped rows into a list.
/// Target = an existing <see cref="ListId"/> OR a new list named <see cref="NewListName"/> (exactly one).
/// </summary>
public sealed class ImportBatchRequest
{
    [JsonPropertyName("list_id")] public long? ListId { get; set; }
    [JsonPropertyName("new_list_name")] public string? NewListName { get; set; }

    /// <summary>One of <see cref="ImportScenarios"/>.</summary>
    [JsonPropertyName("scenario")] public string Scenario { get; set; } = ImportScenarios.NewOnly;

    /// <summary>Only consulted when scenario == 'custom'.</summary>
    [JsonPropertyName("custom")] public ImportCustomFlags? Custom { get; set; }

    [JsonPropertyName("rows")] public List<ImportRowDto> Rows { get; set; } = new();
}

/// <summary>POST /api/v1/data-lists/import response — operator-facing counts (no silent drops).</summary>
public sealed class ImportBatchResponse
{
    [JsonPropertyName("list_id")] public long ListId { get; set; }
    [JsonPropertyName("list_name")] public string ListName { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "ready";

    [JsonPropertyName("total_input")] public int TotalInput { get; set; }
    [JsonPropertyName("valid")] public int Valid { get; set; }
    [JsonPropertyName("file_duplicate")] public int FileDuplicate { get; set; }
    [JsonPropertyName("invalid")] public int Invalid { get; set; }
    [JsonPropertyName("inserted")] public int Inserted { get; set; }
    [JsonPropertyName("updated")] public int Updated { get; set; }
    [JsonPropertyName("skipped_existing")] public int SkippedExisting { get; set; }

    [JsonPropertyName("total_records")] public int TotalRecords { get; set; }
    [JsonPropertyName("sendable_count")] public int SendableCount { get; set; }

    /// <summary>Raw values that failed normalization, capped, for operator correction.</summary>
    [JsonPropertyName("invalid_samples")] public List<string> InvalidSamples { get; set; } = new();
}

/// <summary>POST /api/v1/data-lists/records/exists — which raw phones already exist for this tenant.</summary>
public sealed class RecordsExistsRequest
{
    [JsonPropertyName("phones")] public List<string> Phones { get; set; } = new();
    /// <summary>Optional: scope the existence check to a single list instead of the whole tenant.</summary>
    [JsonPropertyName("list_id")] public long? ListId { get; set; }
}

public sealed class RecordsExistsResponse
{
    /// <summary>Normalized phones (E.164) that already exist.</summary>
    [JsonPropertyName("exists")] public List<string> Exists { get; set; } = new();
}

/// <summary>POST /api/v1/bulk-send/preview-from-list — snapshot a list into a bulk-send preview.</summary>
public sealed class PreviewFromListRequest
{
    [JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "";
    [JsonPropertyName("list_id")] public long ListId { get; set; }
    [JsonPropertyName("template_id")] public int TemplateId { get; set; }
    [JsonPropertyName("lang")] public string? Lang { get; set; }
}

// =============================================================
// FEAT-PROJELER PKT-14 — read-only list insight endpoints (aha pack + list viewer).
// Both are tenant-scoped + ContactList-gated; NO send path.
// =============================================================

/// <summary>One list_records row as shown to the operator (viewer table + preview sample).</summary>
public sealed class ListRecordDto
{
    [JsonPropertyName("id")] public long Id { get; set; }                     // list_records PK (single-record delete target)
    [JsonPropertyName("phone")] public string? Phone { get; set; }            // normalized_phone (null = invalid row)
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("surname")] public string? Surname { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("field1")] public string? Field1 { get; set; }
    [JsonPropertyName("field2")] public string? Field2 { get; set; }
    [JsonPropertyName("field3")] public string? Field3 { get; set; }
    [JsonPropertyName("field4")] public string? Field4 { get; set; }
    [JsonPropertyName("field5")] public string? Field5 { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("sendable")] public bool Sendable { get; set; }
}

/// <summary>Filled/total counts for one column across the selected lists (sendable rows).</summary>
public sealed class ListColumnStatDto
{
    [JsonPropertyName("filled")] public int Filled { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}

/// <summary>
/// GET /api/v1/data-lists/preview-sample?listIds=1,2 — one round-trip feeding the Projeler
/// modal: a real sample recipient (first sendable record of the FIRST id), the precise
/// deduplicated reach, and per-column fill stats. All computed over SENDABLE rows only
/// (matches who a send would actually target).
/// </summary>
public sealed class DataListPreviewSampleResponse
{
    /// <summary>First sendable record of the first selected list; null when none.</summary>
    [JsonPropertyName("sample")] public ListRecordDto? Sample { get; set; }

    /// <summary>COUNT(DISTINCT normalized_phone) across the selected lists (sendable rows).</summary>
    [JsonPropertyName("reach")] public int Reach { get; set; }

    /// <summary>Keyed by column (name/surname/email/field1..5/tags/note).</summary>
    [JsonPropertyName("column_stats")] public Dictionary<string, ListColumnStatDto> ColumnStats { get; set; } = new();
}

/// <summary>GET /api/v1/data-lists/{id}/records — server-paged, searchable records page.</summary>
public sealed class ListRecordsPageResponse
{
    [JsonPropertyName("records")] public List<ListRecordDto> Records { get; set; } = new();
    [JsonPropertyName("total")] public int Total { get; set; }                // rows matching the search
    [JsonPropertyName("page")] public int Page { get; set; }                  // 1-based
    [JsonPropertyName("page_size")] public int PageSize { get; set; }
}

// =============================================================
// Single-record mutations from the list-viewer popup (Veri Yönetimi).
// Permanent delete + manual add; invalid phone and duplicates are
// REJECTED (unlike bulk import, which stores invalid rows sendable=false).
// =============================================================

/// <summary>POST /api/v1/data-lists/{id}/records — manually add one record to a ready list.</summary>
public sealed class AddListRecordRequest
{
    [JsonPropertyName("phone")] public string? Phone { get; set; }            // required; must pass PhoneNormalizer
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("surname")] public string? Surname { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}

/// <summary>POST /api/v1/data-lists/{id}/records response — the stored record + fresh counters.</summary>
public sealed class AddListRecordResponse
{
    [JsonPropertyName("record")] public ListRecordDto Record { get; set; } = new();
    [JsonPropertyName("total_records")] public int TotalRecords { get; set; }
    [JsonPropertyName("sendable_count")] public int SendableCount { get; set; }
}

/// <summary>DELETE /api/v1/data-lists/{id}/records/{recordId} response — fresh counters.</summary>
public sealed class DeleteListRecordResponse
{
    [JsonPropertyName("deleted")] public bool Deleted { get; set; }
    [JsonPropertyName("total_records")] public int TotalRecords { get; set; }
    [JsonPropertyName("sendable_count")] public int SendableCount { get; set; }
}
