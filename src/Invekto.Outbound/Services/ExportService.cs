using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 1A Plan B — export engine. Produces CSV/XLSX (built in-memory,
/// bounded by ExportOptions.MaxXlsxRows) for contact lists + bulk-send campaign
/// results, and the report-data JSON the browser turns into a PDF.
///
/// AUDIT-BEFORE-SERVE invariant (KVKK): the export_logs row is written via the
/// THROWING ExportRepository.InsertLogAsync AFTER the bytes are built but BEFORE
/// they are returned to the endpoint. If the audit insert fails, the export fails
/// with INV-OB-057 and NO bytes are served — PII never egresses without an audit
/// row. Validation/gating/ownership/cap are resolved by Prepare* first, so a
/// disabled/unknown/over-cap request is a clean JSON error before any build.
///
/// Building in-memory (not streaming from the DB into the live response) means
/// every DB failure happens before a single response byte, so it always maps to a
/// clean INV-OB-057 — there is no partial-response / un-auditable mid-stream case.
/// Backend still streams the finished body to the browser without buffering.
/// </summary>
public sealed class ExportService
{
    private readonly ExportRepository _repo;
    private readonly ExportOptions _options;
    private readonly JsonLinesLogger _logger;
    private readonly PhoneNormalizer _phones;

    public ExportService(ExportRepository repo, ExportOptions options, JsonLinesLogger logger, PhoneNormalizer phones)
    {
        _repo = repo;
        _options = options;
        _logger = logger;
        _phones = phones;
    }

    public ExportOptions Options => _options;

    // CSV columns mirror the Plan A import mapper exactly so an exported list
    // re-imports cleanly (edit-in-Excel round-trip, aha #4). The two trailing
    // info columns are ignored by the importer's field mapping.
    private static readonly string[] ContactHeaders =
    {
        "phone", "name", "surname", "email", "tags", "note",
        "field1", "field2", "field3", "field4", "field5",
        "custom_fields", "sendable", "invalid_reason"
    };

    private static readonly string[] RecipientHeaders =
    {
        "Telefon", "Durum", "Gonderildi", "Teslim Edildi", "Okundu", "Hata Nedeni"
    };

    public const string CsvContentType = "text/csv; charset=utf-8";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Built file ready to serve. Err non-null => clean JSON error, no bytes.</summary>
    public sealed class ExportFile
    {
        public string? ErrorCode { get; init; }
        public byte[]? Bytes { get; init; }
        public string ContentType { get; init; } = CsvContentType;
        public string FileName { get; init; } = "export";
    }

    /// <summary>Outcome of a pre-build validation pass. ErrorCode null = OK to build.</summary>
    public sealed class ExportPrep
    {
        public string? ErrorCode { get; init; }
        public string SourceName { get; init; } = "";
        public int RowCount { get; init; }
        public ExportRepository.JobExportMeta? Job { get; init; }
    }

    // ==================================================================
    // Gate + picker
    // ==================================================================
    public bool IsTenantAllowed(int tenantId) => _options.IsTenantAllowed(tenantId);

    /// <summary>Recent campaigns for the export picker. null list => gated/DB error (with code).</summary>
    public async Task<(string? err, List<SendJobSummary>? jobs)> ListSendJobsAsync(int tenantId, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId))
            return (ErrorCodes.ExportFeatureDisabled, null);
        try
        {
            var jobs = await _repo.ListRecentJobsAsync(tenantId, 100, ct);
            return (null, jobs);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Export list send-jobs DB failure: tenant={tenantId}, msg={ex.Message}");
            return (ErrorCodes.ExportDbError, null);
        }
    }

    // ==================================================================
    // Contact list export
    // ==================================================================
    public async Task<ExportFile> ExportContactListAsync(
        int tenantId, long listId, string format, string? requestedBy, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId))
            return new ExportFile { ErrorCode = ErrorCodes.ExportFeatureDisabled };
        if (!ExportFormats.IsStreamFormat(format))
            return new ExportFile { ErrorCode = ErrorCodes.ExportValidationError };

        try
        {
            var meta = await _repo.GetListMetaAsync(tenantId, listId, ct);
            if (meta == null)
                return new ExportFile { ErrorCode = ErrorCodes.ExportSourceNotFound };

            // The row cap is an XLSX-only limit; CSV is the unbounded "full data" path
            // (a list is already capped at import time, so CSV stays memory-bounded).
            var isXlsx = format == ExportFormats.Xlsx;
            if (isXlsx && meta.Value.totalRecords > _options.MaxXlsxRows)
                return new ExportFile { ErrorCode = ErrorCodes.ExportTooLarge };

            var name = meta.Value.name;
            var rows = new List<string[]>(meta.Value.totalRecords);
            await foreach (var r in _repo.ReadListRecordsAsync(tenantId, listId, ct))
            {
                if (isXlsx && rows.Count >= _options.MaxXlsxRows) // defensive: list grew past the pre-check
                    return new ExportFile { ErrorCode = ErrorCodes.ExportTooLarge };
                rows.Add(ContactCells(r));
            }

            var (bytes, contentType) = BuildFile(format, ContactHeaders, rows, "Liste");
            // AUDIT BEFORE SERVE — throws if it cannot be written; then no bytes are returned.
            await _repo.InsertLogAsync(tenantId, ExportTypes.ContactList, listId, name,
                format, ExportDeliveryModes.ServerStream, rows.Count, "completed", requestedBy, null, ct);

            return new ExportFile { Bytes = bytes, ContentType = contentType, FileName = FileName("liste", name, listId, format) };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Contact-list export DB failure: tenant={tenantId}, list={listId}, fmt={format}, msg={ex.Message}");
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.ContactList, listId, null,
                format, ExportDeliveryModes.ServerStream, requestedBy, ErrorCodes.ExportDbError, ct);
            return new ExportFile { ErrorCode = ErrorCodes.ExportDbError };
        }
    }

    // ==================================================================
    // Send-results (per-recipient) export
    // ==================================================================
    public async Task<ExportFile> ExportSendRecipientsAsync(
        int tenantId, long jobId, string format, string? requestedBy, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId))
            return new ExportFile { ErrorCode = ErrorCodes.ExportFeatureDisabled };
        if (!ExportFormats.IsStreamFormat(format))
            return new ExportFile { ErrorCode = ErrorCodes.ExportValidationError };

        try
        {
            var job = await _repo.GetJobMetaAsync(tenantId, jobId, ct);
            if (job == null)
                return new ExportFile { ErrorCode = ErrorCodes.ExportSourceNotFound };

            // The row cap is an XLSX-only limit; CSV is the unbounded "full data" path
            // (recipients are already bounded by the bulk-send cap, so CSV stays memory-bounded).
            var isXlsx = format == ExportFormats.Xlsx;
            var count = await _repo.CountRecipientsAsync(tenantId, jobId, ct);
            if (isXlsx && count > _options.MaxXlsxRows)
                return new ExportFile { ErrorCode = ErrorCodes.ExportTooLarge };

            var rows = new List<string[]>(count);
            await foreach (var r in _repo.ReadSendRecipientsAsync(tenantId, job.Id, job.BroadcastIds, null, ct))
            {
                if (isXlsx && rows.Count >= _options.MaxXlsxRows)
                    return new ExportFile { ErrorCode = ErrorCodes.ExportTooLarge };
                rows.Add(RecipientCells(r));
            }

            var (bytes, contentType) = BuildFile(format, RecipientHeaders, rows, "Alicilar");
            await _repo.InsertLogAsync(tenantId, ExportTypes.SendRecipients, job.Id, job.CampaignId,
                format, ExportDeliveryModes.ServerStream, rows.Count, "completed", requestedBy, null, ct);

            return new ExportFile { Bytes = bytes, ContentType = contentType, FileName = FileName("kampanya", job.CampaignId, jobId, format) };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Send-recipients export DB failure: tenant={tenantId}, job={jobId}, fmt={format}, msg={ex.Message}");
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.SendRecipients, jobId, null,
                format, ExportDeliveryModes.ServerStream, requestedBy, ErrorCodes.ExportDbError, ct);
            return new ExportFile { ErrorCode = ErrorCodes.ExportDbError };
        }
    }

    // ==================================================================
    // Report-data (PDF rendered in browser)
    // ==================================================================
    /// <summary>
    /// Build the campaign report-data (summary + capped recipient table) for the
    /// in-browser PDF. Writes an export_logs row (format=pdf, delivery_mode=client_render)
    /// BEFORE returning the data; an audit failure => INV-OB-057, no data served.
    /// </summary>
    public async Task<(string? err, SendReportData? data)> BuildSendReportDataAsync(
        int tenantId, long jobId, string? requestedBy, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId))
            return (ErrorCodes.ExportFeatureDisabled, null);

        try
        {
            var job = await _repo.GetJobMetaAsync(tenantId, jobId, ct);
            if (job == null)
                return (ErrorCodes.ExportSourceNotFound, null);

            var totalCount = await _repo.CountRecipientsAsync(tenantId, jobId, ct);
            var sums = await _repo.SummarizeRecipientsAsync(tenantId, jobId, job.BroadcastIds, ct);
            // template_id is null for HSM/inline jobs — skip the lookup (audit Outbound-5);
            // passing 0 would have silently resolved the wrong template's name.
            var templateName = job.TemplateId.HasValue
                ? await _repo.GetTemplateNameAsync(tenantId, job.TemplateId.Value, ct)
                : null;

            var limit = _options.PdfRecipientTableLimit;
            var recipients = new List<SendRecipientRow>(Math.Min(totalCount, limit));
            await foreach (var r in _repo.ReadSendRecipientsAsync(tenantId, jobId, job.BroadcastIds, limit, ct))
                recipients.Add(r);

            var data = new SendReportData
            {
                JobId = job.Id,
                CampaignId = job.CampaignId,
                GeneratedAt = DateTime.UtcNow,
                TotalRecipientCount = totalCount,
                RecipientTableTruncated = totalCount > limit,
                RecipientTableLimit = limit,
                Recipients = recipients,
                Summary = new SendReportSummary
                {
                    TemplateId = job.TemplateId,
                    TemplateName = templateName,
                    Lang = job.Lang,
                    Status = job.Status,
                    TotalRecipients = sums.total,
                    Sent = sums.sent,
                    Delivered = sums.delivered,
                    Read = sums.read,
                    Failed = sums.failed,
                    Blocked = sums.blocked,
                    NotSent = sums.notSent,
                    SkippedOptout = job.TotalSkippedOptout,
                    SkippedConsent = job.TotalSkippedConsent,
                    CreatedAt = job.CreatedAt,
                    ConfirmedAt = job.ConfirmedAt,
                    CompletedAt = job.CompletedAt
                }
            };

            // AUDIT BEFORE SERVE — throws if it cannot be written; then data is not returned.
            await _repo.InsertLogAsync(tenantId, ExportTypes.SendSummary, job.Id, job.CampaignId,
                ExportFormats.Pdf, ExportDeliveryModes.ClientRender, recipients.Count, "completed", requestedBy, null, ct);
            return (null, data);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Send report-data DB failure: tenant={tenantId}, job={jobId}, msg={ex.Message}");
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.SendSummary, jobId, null,
                ExportFormats.Pdf, ExportDeliveryModes.ClientRender, requestedBy, ErrorCodes.ExportDbError, ct);
            return (ErrorCodes.ExportDbError, null);
        }
    }

    // ==================================================================
    // FEAT-OBI Phase 1B — filter-driven recipients surface
    // ==================================================================
    private static readonly string[] FilteredHeaders =
    {
        "Telefon", "Durum", "Kampanya", "Sablon", "Gonderildi", "Teslim Edildi", "Okundu", "Kampanya Tarihi"
    };

    // Delivery-status values a filter may legally request (computed best status set).
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "read", "delivered", "sent", "sending", "queued", "failed", "blocked", SendDeliveryStatus.NotSent
    };

    /// <summary>Validate a filter. Returns an error code, or null when OK.</summary>
    private static string? ValidateFilter(ExportFilter f)
    {
        if (f.From.HasValue && f.To.HasValue && f.From.Value.Date > f.To.Value.Date)
            return ErrorCodes.ExportFilterInvalid;
        if (!string.IsNullOrWhiteSpace(f.Status) && !ValidStatuses.Contains(f.Status.Trim()))
            return ErrorCodes.ExportFilterInvalid;
        return null;
    }

    /// <summary>Filter-dropdown options (templates + recent campaigns + active lists).</summary>
    public async Task<(string? err, ExportFilterOptions? options)> ListFilterOptionsAsync(int tenantId, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId)) return (ErrorCodes.ExportFeatureDisabled, null);
        try
        {
            var templates = await _repo.ListTemplateOptionsAsync(tenantId, ct);
            var lists = await _repo.ListDataListOptionsAsync(tenantId, ct);
            var jobs = await _repo.ListRecentJobsAsync(tenantId, 200, ct);
            var campaigns = jobs.Select(j => new FilterOptionItem { Id = j.Id, Label = j.CampaignId }).ToList();
            return (null, new ExportFilterOptions { Templates = templates, Campaigns = campaigns, Lists = lists });
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Export filter-options DB failure: tenant={tenantId}, msg={ex.Message}");
            return (ErrorCodes.ExportDbError, null);
        }
    }

    /// <summary>The count card: distinct ("benzersiz numara") + total ("toplam kayıt").</summary>
    public async Task<(string? err, FilteredCountResult? count)> CountFilteredAsync(
        int tenantId, ExportFilter filter, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId)) return (ErrorCodes.ExportFeatureDisabled, null);
        var invalid = ValidateFilter(filter);
        if (invalid != null) return (invalid, null);
        try
        {
            return (null, await _repo.CountFilteredAsync(tenantId, filter, ct));
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Export filtered-count DB failure: tenant={tenantId}, msg={ex.Message}");
            return (ErrorCodes.ExportDbError, null);
        }
    }

    /// <summary>
    /// Filtered CSV/XLSX export of recipients across ALL campaigns. Like the Plan B paths,
    /// the file is built in-memory (every DB error happens before a response byte → clean
    /// INV-OB-057) and the export_logs row is written BEFORE the bytes are returned.
    /// XLSX is row-capped (INV-OB-058); CSV is the full-data path.
    /// </summary>
    public async Task<ExportFile> ExportFilteredAsync(
        int tenantId, ExportFilter filter, string format, string? requestedBy, CancellationToken ct)
    {
        // Every failure path writes a best-effort status=failed export_logs row (no silent failure):
        // a refused export attempt — gated, malformed, over-cap, or a DB error — is itself an
        // auditable event. The logged format is clamped to a CHECK-valid value (the bad-format
        // case must not make the audit insert violate chk_export_log_format).
        var logFmt = ExportFormats.IsStreamFormat(format) ? format : ExportFormats.Csv;
        async Task<ExportFile> FailAsync(string code)
        {
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.FilteredRecipients, null, FilterSnapshot(filter),
                logFmt, ExportDeliveryModes.ServerStream, requestedBy, code, ct);
            return new ExportFile { ErrorCode = code };
        }

        if (!IsTenantAllowed(tenantId))
            return await FailAsync(ErrorCodes.ExportFeatureDisabled);
        if (!ExportFormats.IsStreamFormat(format))
            return await FailAsync(ErrorCodes.ExportValidationError);
        var invalid = ValidateFilter(filter);
        if (invalid != null) return await FailAsync(invalid);

        try
        {
            var isXlsx = format == ExportFormats.Xlsx;
            if (isXlsx)
            {
                // Pre-check the bound before building the workbook (ClosedXML builds in-memory).
                var counts = await _repo.CountFilteredAsync(tenantId, filter, ct);
                if (counts.TotalCount > _options.MaxXlsxRows)
                    return await FailAsync(ErrorCodes.ExportTooLarge);
            }

            var rows = new List<string[]>();
            await foreach (var r in _repo.ReadFilteredRecipientsAsync(tenantId, filter, null, ct))
            {
                if (isXlsx && rows.Count >= _options.MaxXlsxRows) // defensive
                    return await FailAsync(ErrorCodes.ExportTooLarge);
                rows.Add(FilteredCells(r));
            }

            var (bytes, contentType) = BuildFile(format, FilteredHeaders, rows, "Aramalar");
            // AUDIT BEFORE SERVE — throws if it cannot be written; then no bytes are returned.
            await _repo.InsertLogAsync(tenantId, ExportTypes.FilteredRecipients, null, FilterSnapshot(filter),
                format, ExportDeliveryModes.ServerStream, rows.Count, "completed", requestedBy, null, ct);

            var ext = isXlsx ? "xlsx" : "csv";
            return new ExportFile { Bytes = bytes, ContentType = contentType, FileName = $"aramalar_export.{ext}" };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Filtered export DB failure: tenant={tenantId}, fmt={format}, msg={ex.Message}");
            return await FailAsync(ErrorCodes.ExportDbError);
        }
    }

    /// <summary>
    /// "Liste Oluştur": create a new data_list (source='export') from the filter's distinct
    /// sendable phones. Pre-checks empty (INV-OB-061) and over-cap (INV-OB-058) via the count,
    /// then the repository performs an atomic create (name conflict → INV-OB-059). Writes an
    /// export_logs row (type 'list_from_export') only on success.
    /// </summary>
    public async Task<(string? err, CreateListFromExportResult? result)> CreateListFromExportAsync(
        int tenantId, CreateListFromExportRequest request, string? requestedBy, CancellationToken ct)
    {
        // Every failure path writes a best-effort status=failed export_logs row (no silent failure):
        // a refused list-creation attempt — gated, malformed, empty, over-cap, name-conflict, or a
        // DB error — is an auditable PII-derivation attempt. Logged honestly as format='list'/'internal'.
        var name = request.Name?.Trim();
        // A JSON body with "filter":null overrides the DTO default; treat it as an empty filter
        // (all recipients) rather than NRE'ing on ValidateFilter/FilterSnapshot/CountFiltered.
        var filter = request.Filter ?? new ExportFilter();
        async Task<(string?, CreateListFromExportResult?)> FailAsync(string code)
        {
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.ListFromExport, null,
                string.IsNullOrWhiteSpace(name) ? FilterSnapshot(filter) : name,
                ExportFormats.List, ExportDeliveryModes.Internal, requestedBy, code, ct);
            return (code, null);
        }

        if (!IsTenantAllowed(tenantId)) return await FailAsync(ErrorCodes.ExportFeatureDisabled);
        if (string.IsNullOrWhiteSpace(name)) return await FailAsync(ErrorCodes.ExportValidationError);
        var invalid = ValidateFilter(filter);
        if (invalid != null) return await FailAsync(invalid);

        try
        {
            var counts = await _repo.CountFilteredAsync(tenantId, filter, ct);
            if (counts.UniqueCount == 0) return await FailAsync(ErrorCodes.ExportListEmpty);
            if (counts.UniqueCount > _options.MaxXlsxRows) return await FailAsync(ErrorCodes.ExportTooLarge);

            // CreateListFromFilterAsync writes the success export_logs row INSIDE its transaction,
            // so the new list and its KVKK audit row commit atomically (no partial side effect).
            var (outcome, listId, recordCount) =
                await _repo.CreateListFromFilterAsync(tenantId, name, filter, requestedBy, ct);
            if (outcome == ExportRepository.CreateListOutcome.NameConflict)
                return await FailAsync(ErrorCodes.ExportListNameConflict);
            if (outcome == ExportRepository.CreateListOutcome.Empty)
                return await FailAsync(ErrorCodes.ExportListEmpty); // raced to empty (immutable snapshot, defensive)

            return (null, new CreateListFromExportResult { ListId = listId, Name = name, RecordCount = recordCount });
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Create-list-from-export DB failure: tenant={tenantId}, msg={ex.Message}");
            return await FailAsync(ErrorCodes.ExportDbError);
        }
    }

    /// <summary>Export history (export_logs) for the history table.</summary>
    public async Task<(string? err, List<ExportLogEntry>? entries)> ListHistoryAsync(int tenantId, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId)) return (ErrorCodes.ExportFeatureDisabled, null);
        try
        {
            return (null, await _repo.ListExportHistoryAsync(tenantId, 50, ct));
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Export history DB failure: tenant={tenantId}, msg={ex.Message}");
            return (ErrorCodes.ExportDbError, null);
        }
    }

    private static string[] FilteredCells(FilteredRecipientRow r) => new[]
    {
        Sanitize(r.Phone), Sanitize(r.StatusLabel), Sanitize(r.CampaignId), Sanitize(r.TemplateName),
        Ts(r.SentAt), Ts(r.DeliveredAt), Ts(r.ReadAt), Ts(r.CampaignDate)
    };

    // ==================================================================
    // FEAT-OBI Phase 2 — Telefon Numarası Ara (single-number history)
    // ==================================================================
    // The screen LOOKUP is NOT audited (it is an authenticated operator viewing their own
    // tenant's data, like the Projeler report drawer). A DOWNLOAD (CSV bytes OR the client-PDF
    // data) IS audited (export_logs 'phone_history') — that is the PII-egress event. Phone is
    // normalized with the SAME PhoneNormalizer used at send time; a null result = INV-OB-097.
    private static readonly string[] PhoneHistoryHeaders =
    {
        "Tarih", "Durum", "Sablon", "Kampanya", "Mesaj", "Gonderildi", "Teslim Edildi", "Okundu", "Hata"
    };

    /// <summary>
    /// On-screen lookup: the number's most-recent history (capped at PhoneHistoryScreenLimit, with a
    /// truncation marker). Reads limit+1 to detect "more exists". NOT audited (view, not egress).
    /// </summary>
    public async Task<(string? err, PhoneHistoryResult? result)> GetPhoneHistoryAsync(
        int tenantId, string? rawPhone, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId)) return (ErrorCodes.ExportFeatureDisabled, null);
        var phone = _phones.Normalize(rawPhone);
        if (phone == null) return (ErrorCodes.PhoneHistoryInvalidNumber, null);

        try
        {
            var limit = _options.PhoneHistoryScreenLimit;
            var rows = new List<PhoneHistoryMessageRow>(Math.Min(limit, 256));
            var truncated = false;
            await foreach (var r in _repo.ReadPhoneHistoryAsync(tenantId, phone, limit + 1, ct))
            {
                if (rows.Count >= limit) { truncated = true; break; }
                rows.Add(r);
            }
            return (null, new PhoneHistoryResult { NormalizedPhone = phone, Rows = rows, Truncated = truncated, Limit = limit });
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Phone-history lookup DB failure: tenant={tenantId}, msg={ex.Message}");
            return (ErrorCodes.ExportDbError, null);
        }
    }

    /// <summary>
    /// CSV download of the number's FULL history (uncapped, like the other CSV paths). Built
    /// in-memory; the export_logs row is written BEFORE the bytes return (audit-before-serve);
    /// every failure path writes a best-effort status=failed audit row.
    /// </summary>
    public async Task<ExportFile> ExportPhoneHistoryCsvAsync(
        int tenantId, string? rawPhone, string? requestedBy, CancellationToken ct)
    {
        async Task<ExportFile> FailAsync(string code)
        {
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.PhoneHistory, null, PhoneSnapshot(rawPhone),
                ExportFormats.Csv, ExportDeliveryModes.ServerStream, requestedBy, code, ct);
            return new ExportFile { ErrorCode = code };
        }

        if (!IsTenantAllowed(tenantId)) return await FailAsync(ErrorCodes.ExportFeatureDisabled);
        var phone = _phones.Normalize(rawPhone);
        if (phone == null) return await FailAsync(ErrorCodes.PhoneHistoryInvalidNumber);

        try
        {
            var rows = new List<string[]>();
            await foreach (var r in _repo.ReadPhoneHistoryAsync(tenantId, phone, null, ct))
                rows.Add(PhoneHistoryCells(r));

            var bytes = BuildCsv(PhoneHistoryHeaders, rows);
            // AUDIT BEFORE SERVE — throws if it cannot be written; then no bytes are returned.
            // source_name is the MASKED number (last-4), never the full PII phone (export_logs is the
            // audit store, not a PII store) — consistent with the failure path + PhoneSnapshot's contract.
            await _repo.InsertLogAsync(tenantId, ExportTypes.PhoneHistory, null, PhoneSnapshot(phone),
                ExportFormats.Csv, ExportDeliveryModes.ServerStream, rows.Count, "completed", requestedBy, null, ct);

            return new ExportFile { Bytes = bytes, ContentType = CsvContentType, FileName = PhoneFileName(phone, "csv") };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Phone-history CSV export DB failure: tenant={tenantId}, msg={ex.Message}");
            return await FailAsync(ErrorCodes.ExportDbError);
        }
    }

    /// <summary>
    /// Full history data for the in-browser PDF (jsPDF). Writes an export_logs row (format=pdf,
    /// delivery_mode=client_render) BEFORE returning — so a client-rendered PDF of the message
    /// bodies (PII) is audited just like the CSV. Mirrors BuildSendReportDataAsync.
    /// </summary>
    public async Task<(string? err, PhoneHistoryResult? data)> BuildPhoneHistoryPdfDataAsync(
        int tenantId, string? rawPhone, string? requestedBy, CancellationToken ct)
    {
        if (!IsTenantAllowed(tenantId)) return (ErrorCodes.ExportFeatureDisabled, null);
        var phone = _phones.Normalize(rawPhone);
        if (phone == null) return (ErrorCodes.PhoneHistoryInvalidNumber, null);

        try
        {
            var rows = new List<PhoneHistoryMessageRow>();
            await foreach (var r in _repo.ReadPhoneHistoryAsync(tenantId, phone, null, ct))
                rows.Add(r);

            // AUDIT BEFORE SERVE — a client-rendered PDF of PII is an egress event. source_name is the
            // MASKED number (last-4), never the full PII phone (see CSV path note).
            await _repo.InsertLogAsync(tenantId, ExportTypes.PhoneHistory, null, PhoneSnapshot(phone),
                ExportFormats.Pdf, ExportDeliveryModes.ClientRender, rows.Count, "completed", requestedBy, null, ct);

            return (null, new PhoneHistoryResult { NormalizedPhone = phone, Rows = rows, Truncated = false, Limit = rows.Count });
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"Phone-history PDF-data DB failure: tenant={tenantId}, msg={ex.Message}");
            await _repo.TryInsertFailureLogAsync(tenantId, ExportTypes.PhoneHistory, null, PhoneSnapshot(rawPhone),
                ExportFormats.Pdf, ExportDeliveryModes.ClientRender, requestedBy, ErrorCodes.ExportDbError, ct);
            return (ErrorCodes.ExportDbError, null);
        }
    }

    private static string[] PhoneHistoryCells(PhoneHistoryMessageRow r) => new[]
    {
        // Kampanya: friendly project name when the send came from a project; else the campaign_id so the
        // audit/evidence export never loses the campaign identity (the on-screen table shows name-or-'—').
        Ts(r.CreatedAt), Sanitize(r.StatusLabel), Sanitize(r.TemplateName), Sanitize(r.CampaignName ?? r.CampaignId),
        Sanitize(r.MessageText), Ts(r.SentAt), Ts(r.DeliveredAt), Ts(r.ReadAt), Sanitize(r.Error)
    };

    private static string Ts(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // export_logs forensic column: never the full number (PII). Last-4 only.
    private static string PhoneSnapshot(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? "no=***" + digits[^4..] : "no=***";
    }

    private static string PhoneFileName(string normalizedPhone, string ext)
    {
        var slug = new string(normalizedPhone.Where(char.IsLetterOrDigit).ToArray());
        if (slug.Length == 0) slug = "numara";
        return $"numara_gecmis_{slug}.{ext}";
    }

    // Compact, ascii-safe snapshot of the active filter for the export_logs forensic column.
    private static string FilterSnapshot(ExportFilter f)
    {
        var parts = new List<string>();
        if (f.TemplateId.HasValue) parts.Add($"tpl={f.TemplateId}");
        if (f.JobId.HasValue) parts.Add($"job={f.JobId}");
        if (f.ListId.HasValue) parts.Add($"list={f.ListId}");
        if (!string.IsNullOrWhiteSpace(f.Status)) parts.Add($"status={f.Status.Trim()}");
        if (f.From.HasValue) parts.Add($"from={f.From.Value:yyyy-MM-dd}");
        if (f.To.HasValue) parts.Add($"to={f.To.Value:yyyy-MM-dd}");
        return parts.Count == 0 ? "filtre yok (tum aramalar)" : string.Join(" ", parts);
    }

    // ==================================================================
    // File builders (in-memory, bounded)
    // ==================================================================
    private static (byte[] bytes, string contentType) BuildFile(
        string format, string[] headers, List<string[]> rows, string sheetName) =>
        format == ExportFormats.Xlsx
            ? (BuildXlsx(headers, rows, sheetName), XlsxContentType)
            : (BuildCsv(headers, rows), CsvContentType);

    private static byte[] BuildCsv(string[] headers, List<string[]> rows)
    {
        var sb = new StringBuilder();
        AppendCsvLine(sb, headers);
        foreach (var row in rows) AppendCsvLine(sb, row);

        // UTF-8 BOM so Excel renders Turkish characters.
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
        Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);
        return bytes;
    }

    private static byte[] BuildXlsx(string[] headers, List<string[]> rows, string sheetName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Style.NumberFormat.Format = "@"; // text
            cell.SetValue(headers[c]);
            cell.Style.Font.Bold = true;
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            for (var c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(i + 2, c + 1);
                // Force text so phones/ids keep '+', leading zeros, and never render in
                // scientific notation; SetValue stores a string literal (never a formula).
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(row[c]);
            }
        }
        // No AdjustToContents (O(rows*cols), too costly at the 50k cap).
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ==================================================================
    // Row shaping (Sanitize applied at the cell level for CSV + XLSX)
    // ==================================================================
    private static string[] ContactCells(ExportRepository.ContactExportRecord r) => new[]
    {
        Sanitize(r.Phone), Sanitize(r.Name), Sanitize(r.Surname), Sanitize(r.Email),
        Sanitize(r.Tags), Sanitize(r.Note), Sanitize(r.Field1), Sanitize(r.Field2),
        Sanitize(r.Field3), Sanitize(r.Field4), Sanitize(r.Field5),
        Sanitize(r.CustomFieldsJson), r.Sendable ? "true" : "false", Sanitize(r.InvalidReason)
    };

    private static string[] RecipientCells(SendRecipientRow r) => new[]
    {
        Sanitize(r.Phone), Sanitize(r.StatusLabel), Ts(r.SentAt), Ts(r.DeliveredAt), Ts(r.ReadAt), Sanitize(r.FailedReason)
    };

    private static string Ts(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "";

    // ==================================================================
    // CSV line/field helpers
    // ==================================================================
    private static void AppendCsvLine(StringBuilder sb, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(CsvField(cells[i]));
        }
        sb.Append("\r\n");
    }

    private static string CsvField(string value)
    {
        var needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                         || (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ==================================================================
    // Formula-injection hardening (CSV + XLSX). A leading = + - @ (optionally
    // after spaces) or a leading TAB/CR/LF gets an apostrophe prefix so the cell
    // is treated as text, never an executable formula.
    // ==================================================================
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var trimmed = value.TrimStart(' ');
        if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')
            return "'" + value;
        if (value[0] is '\t' or '\r' or '\n')
            return "'" + value;
        return value;
    }

    // ==================================================================
    // Sanitized download filename: ascii-safe slug of the source name + id + ext.
    // ==================================================================
    private static string FileName(string prefix, string sourceName, long id, string ext)
    {
        var slug = new string((sourceName ?? "").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        if (slug.Length > 40) slug = slug[..40];
        if (slug.Length == 0) slug = prefix;
        return $"{prefix}_{slug}_{id}.{ext}";
    }
}
