using System.Text.Json;
using Chatinbox.Outbound.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Outbound;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 1A — contact-list orchestration (gating + validation + server-authoritative
/// normalization + in-file dedup), delegating persistence to DataListRepository.
///
/// The Excel/CSV file is parsed CLIENT-SIDE (SheetJS); the client posts field-mapped rows. The
/// server is the authority for phone normalization (PhoneNormalizer, reused from Phase 0), dedup
/// (last-row-wins within a file), and the import scenario. Invalid phones are reported as counts +
/// samples (Phase 0 PII-minimization: unsendable rows are not persisted), never silently dropped.
/// Register as singleton.
/// </summary>
public sealed class ContactListImportService
{
    private readonly DataListRepository _repo;
    private readonly PhoneNormalizer _normalizer;
    private readonly ContactListOptions _options;
    private readonly JsonLinesLogger _logger;

    public ContactListImportService(
        DataListRepository repo, PhoneNormalizer normalizer,
        ContactListOptions options, JsonLinesLogger logger)
    {
        _repo = repo;
        _normalizer = normalizer;
        _options = options;
        _logger = logger;
    }

    private bool Allowed(int tenantId) => _options.IsTenantAllowed(tenantId);

    // ------------------------------------------------------------------
    // List CRUD
    // ------------------------------------------------------------------
    public async Task<(List<DataListSummary>? lists, string? errorCode)> ListAsync(int tenantId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled);
        try { return (await _repo.ListAsync(tenantId, ct), null); }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-lists list failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError);
        }
    }

    public async Task<(DataListSummary? list, string? errorCode, string? message)> CreateListAsync(
        int tenantId, CreateDataListRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (null, ErrorCodes.ContactListInvalidPayload, "List name is required");

        try
        {
            var (id, conflict) = await _repo.CreateAsync(tenantId, name, "upload", ct);
            if (conflict) return (null, ErrorCodes.ContactListNameConflict, $"A list named '{name}' already exists");
            if (id == null) return (null, ErrorCodes.ContactListInvalidPayload, "Could not create list");

            var summary = await _repo.GetAsync(tenantId, id.Value, ct);
            return (summary, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list create failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "List could not be created due to a database error; please retry.");
        }
    }

    public async Task<(DataListSummary? list, string? errorCode, string? message)> UpdateListAsync(
        int tenantId, long listId, UpdateDataListRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");
        var name = request.Name?.Trim();
        if (request.Name != null && string.IsNullOrWhiteSpace(name))
            return (null, ErrorCodes.ContactListInvalidPayload, "List name cannot be blank");

        try
        {
            var rc = await _repo.UpdateAsync(tenantId, listId, name, request.Active, ct);
            if (rc == -1) return (null, ErrorCodes.ContactListNameConflict, "Another list already uses that name");
            if (rc == 0) return (null, ErrorCodes.ContactListNotFound, $"List {listId} not found");

            var summary = await _repo.GetAsync(tenantId, listId, ct);
            return (summary, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list update failed (tenant={tenantId}, list={listId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "List could not be updated due to a database error; please retry.");
        }
    }

    public async Task<(bool ok, string? errorCode)> DeleteListAsync(int tenantId, long listId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (false, ErrorCodes.ContactListDisabled);
        try
        {
            var ok = await _repo.SoftDeleteAsync(tenantId, listId, ct);
            return ok ? (true, null) : (false, ErrorCodes.ContactListNotFound);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list delete failed (tenant={tenantId}, list={listId}): {ex.Message}");
            return (false, ErrorCodes.ContactListDbError);
        }
    }

    // ------------------------------------------------------------------
    // records/exists
    // ------------------------------------------------------------------
    public async Task<(RecordsExistsResponse? response, string? errorCode, string? message)> RecordsExistsAsync(
        int tenantId, RecordsExistsRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");
        if (request.Phones.Count == 0) return (new RecordsExistsResponse(), null, null);
        if (request.Phones.Count > _options.MaxExistsProbe)
            return (null, ErrorCodes.ContactListInvalidPayload,
                $"Too many phones ({request.Phones.Count} > {_options.MaxExistsProbe})");

        // Normalize server-side, then probe. Distinct to avoid redundant ANY() params.
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in request.Phones)
        {
            var n = _normalizer.Normalize(raw);
            if (n != null) normalized.Add(n);
        }
        try
        {
            var exists = await _repo.ExistingPhonesAsync(tenantId, request.ListId, normalized, ct);
            return (new RecordsExistsResponse { Exists = exists.ToList() }, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"records/exists probe failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "Existence check failed due to a database error; please retry.");
        }
    }

    // ------------------------------------------------------------------
    // FEAT-PROJELER PKT-14 — read-only list insights (aha pack + viewer)
    // ------------------------------------------------------------------
    private const int MaxPreviewSampleLists = 50;   // defensive cap on ?listIds= ids per call
    private const int MaxRecordsPageSize = 200;     // viewer page ceiling (default 50 client-side)
    private const int MaxSearchLength = 200;        // search term cap before pattern build

    /// <summary>
    /// Projeler modal insight: sample recipient + deduplicated reach + per-column fill stats
    /// over the selected lists. Read-only; tenant scoping in the repo IS the ownership check
    /// (foreign ids contribute nothing).
    /// </summary>
    public async Task<(DataListPreviewSampleResponse? response, string? errorCode, string? message)> PreviewSampleAsync(
        int tenantId, string? listIdsCsv, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");

        var ids = new List<long>();
        foreach (var part in (listIdsCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(part, out var id) || id <= 0)
                return (null, ErrorCodes.ContactListInvalidPayload, "listIds must be a comma-separated list of positive ids");
            ids.Add(id);
        }
        if (ids.Count == 0)
            return (null, ErrorCodes.ContactListInvalidPayload, "listIds is required");
        if (ids.Count > MaxPreviewSampleLists)
            return (null, ErrorCodes.ContactListInvalidPayload, $"Too many lists ({ids.Count} > {MaxPreviewSampleLists})");

        try { return (await _repo.GetPreviewSampleAsync(tenantId, ids.ToArray(), ct), null, null); }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-lists preview-sample failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "Preview sample failed due to a database error; please retry.");
        }
    }

    /// <summary>
    /// Viewer popup: one server-paged, searchable records page. The search term is matched
    /// LITERALLY — LIKE wildcards in user input are escaped here before the parameterized
    /// ILIKE pattern is built ('' disables the filter in SQL). 404 when the list isn't this
    /// tenant's (existing GetAsync probe).
    /// </summary>
    public async Task<(ListRecordsPageResponse? response, string? errorCode, string? message)> RecordsPageAsync(
        int tenantId, long listId, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxRecordsPageSize);

        var trimmed = (search ?? "").Trim();
        if (trimmed.Length > MaxSearchLength) trimmed = trimmed[..MaxSearchLength];
        var pattern = trimmed.Length == 0
            ? ""
            : "%" + trimmed.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        try
        {
            var list = await _repo.GetAsync(tenantId, listId, ct);
            if (list == null) return (null, ErrorCodes.ContactListNotFound, $"List {listId} not found");

            return (await _repo.GetRecordsPageAsync(tenantId, listId, pattern, page, pageSize, ct), null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list records failed (tenant={tenantId}, list={listId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "Records could not be loaded due to a database error; please retry.");
        }
    }

    // ------------------------------------------------------------------
    // Single-record mutations (list-viewer popup) — manual add + permanent delete.
    // Existence/status/cap decisions live INSIDE the repo's locked transaction
    // (data_lists FOR UPDATE — plan-consult PR-001/PR-002); this layer only
    // gates the feature, validates the payload, and maps outcomes to INV codes.
    // ------------------------------------------------------------------
    private const int MaxNameLength = 255;     // list_records.name / surname VARCHAR(255)
    private const int MaxEmailLength = 320;    // list_records.email VARCHAR(320)

    /// <summary>
    /// Manually add ONE record. Unlike bulk import, an invalid phone is REJECTED (047) instead
    /// of stored sendable=false, and a duplicate phone is REJECTED (088) — Q decision 2026-06-11:
    /// no silent-broken rows from the manual path.
    /// </summary>
    public async Task<(AddListRecordResponse? response, string? errorCode, string? message)> AddRecordAsync(
        int tenantId, long listId, AddListRecordRequest? request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");
        if (request == null) return (null, ErrorCodes.ContactListInvalidPayload, "Request body is required");

        var phone = _normalizer.Normalize(request.Phone);
        if (phone == null)
            return (null, ErrorCodes.ContactListInvalidPayload, "Phone could not be normalized; expected a valid number like 905xxxxxxxxx");

        var name = Cap(request.Name, MaxNameLength);
        var surname = Cap(request.Surname, MaxNameLength);
        var email = Cap(request.Email, MaxEmailLength);

        try
        {
            var result = await _repo.InsertRecordAsync(tenantId, listId, phone, name, surname, email, _options.MaxRecordsPerList, ct);
            return result.Outcome switch
            {
                DataListRepository.RecordMutationOutcome.Ok => (new AddListRecordResponse
                {
                    Record = new ListRecordDto
                    {
                        Id = result.RecordId, Phone = phone, Name = name, Surname = surname,
                        Email = email, Sendable = true
                    },
                    TotalRecords = result.TotalRecords,
                    SendableCount = result.SendableCount
                }, null, null),
                DataListRepository.RecordMutationOutcome.ListNotFound =>
                    (null, ErrorCodes.ContactListNotFound, $"List {listId} not found"),
                DataListRepository.RecordMutationOutcome.ListNotReady =>
                    (null, ErrorCodes.ContactListNotReady, "List is not ready (import in progress or failed); retry when it is ready"),
                DataListRepository.RecordMutationOutcome.CapExceeded =>
                    (null, ErrorCodes.ContactListCapExceeded, $"List is at the per-list cap of {_options.MaxRecordsPerList} records"),
                DataListRepository.RecordMutationOutcome.Duplicate =>
                    (null, ErrorCodes.ContactListRecordDuplicate, "This phone already exists in the list"),
                _ => (null, ErrorCodes.ContactListDbError, "Record could not be added; please retry.")
            };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list record add failed (tenant={tenantId}, list={listId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "Record could not be added due to a database error; the change was not confirmed — please retry.");
        }
    }

    /// <summary>Permanently delete ONE record (viewer popup trash action, 2-stage confirm client-side).</summary>
    public async Task<(DeleteListRecordResponse? response, string? errorCode, string? message)> DeleteRecordAsync(
        int tenantId, long listId, long recordId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");

        try
        {
            var result = await _repo.DeleteRecordAsync(tenantId, listId, recordId, ct);
            return result.Outcome switch
            {
                DataListRepository.RecordMutationOutcome.Ok => (new DeleteListRecordResponse
                {
                    Deleted = true,
                    TotalRecords = result.TotalRecords,
                    SendableCount = result.SendableCount
                }, null, null),
                DataListRepository.RecordMutationOutcome.ListNotFound =>
                    (null, ErrorCodes.ContactListNotFound, $"List {listId} not found"),
                DataListRepository.RecordMutationOutcome.ListNotReady =>
                    (null, ErrorCodes.ContactListNotReady, "List is not ready (import in progress or failed); retry when it is ready"),
                DataListRepository.RecordMutationOutcome.RecordNotFound =>
                    (null, ErrorCodes.ContactListRecordNotFound, $"Record {recordId} not found in list {listId}"),
                _ => (null, ErrorCodes.ContactListDbError, "Record could not be deleted; please retry.")
            };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"data-list record delete failed (tenant={tenantId}, list={listId}, record={recordId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError, "Record could not be deleted due to a database error; the change was not confirmed — please retry.");
        }
    }

    /// <summary>Trim + length-cap an optional text field (empty -> null; SQL NULLIF is the second guard).</summary>
    private static string? Cap(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    // ------------------------------------------------------------------
    // import
    // ------------------------------------------------------------------
    public async Task<(ImportBatchResponse? response, string? errorCode, string? message)> ImportAsync(
        int tenantId, ImportBatchRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ContactListDisabled, "Contact lists not enabled for this tenant");

        if (request.Rows == null || request.Rows.Count == 0)
            return (null, ErrorCodes.ContactListInvalidPayload, "No rows to import");
        if (request.Rows.Count > _options.MaxImportRows)
            return (null, ErrorCodes.ContactListCapExceeded,
                $"Import has {request.Rows.Count} rows; the per-request ceiling is {_options.MaxImportRows}");
        if (!ImportScenarios.IsValid(request.Scenario))
            return (null, ErrorCodes.ContactListInvalidPayload, $"Unknown scenario '{request.Scenario}'");

        var newName = request.NewListName?.Trim();
        var hasExisting = request.ListId.HasValue;
        var hasNew = !string.IsNullOrWhiteSpace(newName);
        if (hasExisting == hasNew)
            return (null, ErrorCodes.ContactListInvalidPayload,
                "Provide exactly one of list_id or new_list_name");

        // For an existing target, validate it up-front. 'ready' and 'failed' are both retryable
        // ('failed' = a prior import rolled back; re-importing recovers it); only 'importing'
        // (an in-flight import) is blocked to avoid concurrent writes to the same list.
        string listName;
        if (request.ListId is { } targetListId)
        {
            DataListSummary? existing;
            try { existing = await _repo.GetAsync(tenantId, targetListId, ct); }
            catch (NpgsqlException ex)
            {
                _logger.SystemError($"data-list lookup failed (tenant={tenantId}, list={targetListId}): {ex.Message}");
                return (null, ErrorCodes.ContactListDbError, "List lookup failed due to a database error; please retry.");
            }
            if (existing == null) return (null, ErrorCodes.ContactListNotFound, $"List {targetListId} not found");
            if (existing.Status == "importing")
                return (null, ErrorCodes.ContactListNotReady,
                    $"List '{existing.Name}' is currently importing; wait for it to finish before re-importing");
            listName = existing.Name;
        }
        else
        {
            // The exactly-one guard above guarantees newName is non-empty on this branch.
            listName = newName ?? throw new InvalidOperationException("new_list_name expected after validation");
        }

        // Server-authoritative normalize + in-file dedup (last-row-wins).
        var totalInput = request.Rows.Count;
        var invalid = 0;
        var invalidSamples = new List<string>();
        var deduped = new Dictionary<string, DataListRepository.NormalizedImportRow>(StringComparer.Ordinal);
        var validBeforeDedup = 0;

        foreach (var row in request.Rows)
        {
            var phone = _normalizer.Normalize(row.Phone);
            if (phone == null)
            {
                invalid++;
                if (invalidSamples.Count < _options.InvalidSampleSize && !string.IsNullOrWhiteSpace(row.Phone))
                    invalidSamples.Add(row.Phone.Trim());
                continue;
            }
            validBeforeDedup++;
            // Last-row-wins: a later row for the same normalized phone overrides an earlier one.
            deduped[phone] = new DataListRepository.NormalizedImportRow
            {
                Phone = phone,
                Name = Trim(row.Name),
                Surname = Trim(row.Surname),
                Email = Trim(row.Email),
                Tags = Trim(row.Tags),
                Note = Trim(row.Note),
                Field1 = Trim(row.Field1),
                Field2 = Trim(row.Field2),
                Field3 = Trim(row.Field3),
                Field4 = Trim(row.Field4),
                Field5 = Trim(row.Field5),
                CustomFieldsJson = SerializeCustom(row.CustomFields)
            };
        }

        var rows = deduped.Values.ToList();
        var fileDuplicate = validBeforeDedup - rows.Count;

        DataListRepository.ImportWriteResult result;
        try
        {
            result = await _repo.ImportRecordsAsync(
                tenantId,
                existingListId: hasExisting ? request.ListId : null,
                createName: hasExisting ? null : newName,
                request.Scenario, request.Custom, rows, _options.MaxRecordsPerList, ct);
        }
        catch (NpgsqlException ex)
        {
            // NpgsqlException (base) covers BOTH server PostgresExceptions AND connection/transient
            // driver failures, so any DB error maps to INV-OB-053. Covers the read-only probe phase
            // before the repo's own write-phase catch engages; the tx never committed, no half-list.
            _logger.SystemError(
                $"Contact import failed (tenant={tenantId}, list={request.ListId}): {ex.Message}");
            return (null, ErrorCodes.ContactListDbError,
                "Import could not complete due to a database error; the list was not modified. Please retry.");
        }

        if (result.NameConflict)
            return (null, ErrorCodes.ContactListNameConflict, $"A list named '{newName}' already exists");
        if (!result.Found)
            return (null, ErrorCodes.ContactListNotFound, $"List {request.ListId} not found");
        if (result.DbError)
            return (null, ErrorCodes.ContactListDbError,
                "Import could not complete due to a database error; the list was not modified (an existing list is flagged 'failed'). Please retry.");
        if (result.CapExceeded)
            return (null, ErrorCodes.ContactListCapExceeded,
                $"List has {result.CurrentTotal} records; adding {result.AttemptedNew} would exceed the per-list cap of {_options.MaxRecordsPerList}. Reduce the file or split into another list.");

        _logger.SystemInfo(
            $"Contact import: tenant={tenantId}, list={result.ListId}, scenario={request.Scenario}, " +
            $"input={totalInput}, valid={rows.Count}, fileDup={fileDuplicate}, invalid={invalid}, " +
            $"inserted={result.Inserted}, updated={result.Updated}, skipped={result.Skipped}");

        return (new ImportBatchResponse
        {
            ListId = result.ListId,
            ListName = listName,
            Status = "ready",
            TotalInput = totalInput,
            Valid = rows.Count,
            FileDuplicate = fileDuplicate,
            Invalid = invalid,
            Inserted = result.Inserted,
            Updated = result.Updated,
            SkippedExisting = result.Skipped,
            TotalRecords = result.TotalRecords,
            SendableCount = result.SendableCount,
            InvalidSamples = invalidSamples
        }, null, null);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Bound the per-row custom_fields blob so a malicious/huge upload cannot bloat the JSONB column.
    private const int MaxCustomKeys = 25;
    private const int MaxCustomKeyLen = 64;
    private const int MaxCustomValueLen = 512;

    private static string? SerializeCustom(Dictionary<string, string>? custom)
    {
        if (custom == null || custom.Count == 0) return null;
        var bounded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in custom)
        {
            if (bounded.Count >= MaxCustomKeys) break;
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            var key = kv.Key.Length > MaxCustomKeyLen ? kv.Key[..MaxCustomKeyLen] : kv.Key;
            var val = kv.Value ?? "";
            if (val.Length > MaxCustomValueLen) val = val[..MaxCustomValueLen];
            bounded[key] = val;
        }
        return bounded.Count == 0 ? null : JsonSerializer.Serialize(bounded);
    }
}
