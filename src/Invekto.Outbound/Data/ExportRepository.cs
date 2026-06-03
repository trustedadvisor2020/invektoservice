using System.Runtime.CompilerServices;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Data;

/// <summary>
/// FEAT-OBI Phase 1A Plan B persistence for the Export Manager.
///
/// TWO responsibilities, both tenant_id scoped:
///   1. export_logs audit — INSERT ONLY. No UPDATE/DELETE statement targets
///      export_logs anywhere in this class (append-only is enforced in code,
///      not via column grants — see migration 053).
///   2. Read the data to export — contact lists (data_lists/list_records) and
///      bulk-send campaign results (bulk_send_recipients LEFT JOIN
///      outbound_messages). Row reads are streamed via IAsyncEnumerable so a
///      50k-row CSV never fully buffers.
///
/// Every read verifies ownership (the list/job belongs to the JWT tenant);
/// callers map a miss to INV-OB-055. Register as singleton.
/// </summary>
public class ExportRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public ExportRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Read DTOs (internal to the export path)
    // ------------------------------------------------------------------
    public sealed class ContactExportRecord
    {
        public string? Phone { get; init; }
        public string? Name { get; init; }
        public string? Surname { get; init; }
        public string? Email { get; init; }
        public string? Tags { get; init; }
        public string? Note { get; init; }
        public string? Field1 { get; init; }
        public string? Field2 { get; init; }
        public string? Field3 { get; init; }
        public string? Field4 { get; init; }
        public string? Field5 { get; init; }
        public string? CustomFieldsJson { get; init; }
        public bool Sendable { get; init; }
        public string? InvalidReason { get; init; }
    }

    public sealed class JobExportMeta
    {
        public long Id { get; init; }
        public string CampaignId { get; init; } = "";
        public Guid[] BroadcastIds { get; init; } = Array.Empty<Guid>();
        public int TemplateId { get; init; }
        public string? Lang { get; init; }
        public string Status { get; init; } = "";
        public int TotalSkippedOptout { get; init; }
        public int TotalSkippedConsent { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ConfirmedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }

    // ==================================================================
    // export_logs audit — INSERT ONLY
    // ==================================================================
    /// <summary>
    /// Append one audit row. THROWS NpgsqlException on failure — the caller writes this
    /// BEFORE any PII is served, so a logging failure must surface (mapped to INV-OB-057)
    /// rather than letting PII egress with no KVKK audit trail. Callers map the exception
    /// to a failed export. (PostgresException is a subtype, so it propagates too.)
    /// </summary>
    public virtual async Task InsertLogAsync(
        int tenantId, string exportType, long? sourceId, string? sourceName,
        string format, string deliveryMode, int rowCount, string status,
        string? requestedBy, string? errorCode, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await InsertLogCoreAsync(conn, tenantId, exportType, sourceId, sourceName,
            format, deliveryMode, rowCount, status, requestedBy, errorCode, ct);
    }

    /// <summary>
    /// Best-effort failure-audit used inside a catch block: the export already failed, so a
    /// secondary logging failure is logged and swallowed (never masks the original error).
    /// </summary>
    public virtual async Task TryInsertFailureLogAsync(
        int tenantId, string exportType, long? sourceId, string? sourceName,
        string format, string deliveryMode, string? requestedBy, string errorCode,
        CancellationToken ct = default)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await InsertLogCoreAsync(conn, tenantId, exportType, sourceId, sourceName,
                format, deliveryMode, 0, "failed", requestedBy, errorCode, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"export_logs failure-audit insert failed (best-effort): tenant={tenantId}, type={exportType}, msg={ex.Message}");
        }
    }

    private static async Task InsertLogCoreAsync(
        NpgsqlConnection conn, int tenantId, string exportType, long? sourceId, string? sourceName,
        string format, string deliveryMode, int rowCount, string status,
        string? requestedBy, string? errorCode, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO export_logs
                (tenant_id, export_type, source_id, source_name_snapshot, format,
                 delivery_mode, row_count, status, requested_by, error_code)
            VALUES
                (@tid, @type, @sid, @sname, @fmt, @mode, @rows, @status, @by, @err)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("type", exportType);
        cmd.Parameters.AddWithValue("sid", (object?)sourceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sname", (object?)Trunc(sourceName, 255) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fmt", format);
        cmd.Parameters.AddWithValue("mode", deliveryMode);
        cmd.Parameters.AddWithValue("rows", rowCount);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("by", (object?)Trunc(requestedBy, 120) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("err", (object?)errorCode ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================================================================
    // Contact-list reads
    // ==================================================================
    /// <summary>Ownership + snapshot metadata for a contact list. null => not found for this tenant.</summary>
    public virtual async Task<(string name, int totalRecords)?> GetListMetaAsync(
        int tenantId, long listId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT name, total_records
            FROM data_lists
            WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetString(0), reader.GetInt32(1));
    }

    /// <summary>Stream a list's records (tenant-scoped). Connection lives for the enumeration.</summary>
    public virtual async IAsyncEnumerable<ContactExportRecord> ReadListRecordsAsync(
        int tenantId, long listId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        const string sql = @"
            SELECT normalized_phone, name, surname, email, tags, note,
                   field1, field2, field3, field4, field5,
                   custom_fields::text, sendable, invalid_reason
            FROM list_records
            WHERE tenant_id=@tid AND list_id=@lid
            ORDER BY id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            yield return new ContactExportRecord
            {
                Phone = S(reader, 0),
                Name = S(reader, 1),
                Surname = S(reader, 2),
                Email = S(reader, 3),
                Tags = S(reader, 4),
                Note = S(reader, 5),
                Field1 = S(reader, 6),
                Field2 = S(reader, 7),
                Field3 = S(reader, 8),
                Field4 = S(reader, 9),
                Field5 = S(reader, 10),
                CustomFieldsJson = S(reader, 11),
                Sendable = reader.GetBoolean(12),
                InvalidReason = S(reader, 13)
            };
        }
    }

    // ==================================================================
    // Bulk-send campaign reads
    // ==================================================================
    /// <summary>Ownership + summary metadata for a bulk-send job. null => not found for this tenant.</summary>
    public virtual async Task<JobExportMeta?> GetJobMetaAsync(
        int tenantId, long jobId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, campaign_id, broadcast_ids, template_id, lang, status,
                   total_skipped_optout, total_skipped_consent,
                   created_at, confirmed_at, completed_at
            FROM bulk_send_jobs
            WHERE tenant_id=@tid AND id=@jid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new JobExportMeta
        {
            Id = reader.GetInt64(0),
            CampaignId = reader.GetString(1),
            BroadcastIds = reader.IsDBNull(2) ? Array.Empty<Guid>() : (Guid[])reader.GetValue(2),
            TemplateId = reader.GetInt32(3),
            Lang = S(reader, 4),
            Status = reader.GetString(5),
            TotalSkippedOptout = reader.GetInt32(6),
            TotalSkippedConsent = reader.GetInt32(7),
            CreatedAt = reader.GetDateTime(8),
            ConfirmedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
        };
    }

    /// <summary>Count recipients in a job's immutable snapshot (for the XLSX/PDF cap pre-check).</summary>
    public virtual async Task<int> CountRecipientsAsync(
        int tenantId, long jobId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*)::int FROM bulk_send_recipients WHERE tenant_id=@tid AND job_id=@jid";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>
    /// Per-recipient delivery status (Codex#5). Base = bulk_send_recipients (the job's
    /// immutable snapshot — who we intended to message), LEFT JOIN LATERAL the best
    /// matching outbound_messages row. A recipient with no message row yields a NULL
    /// status -> mapped to not_sent (NEVER dropped by an inner join). The tenant_id
    /// predicate is on BOTH r and om; broadcast ids come from the server-loaded job,
    /// never from the client. Defensive multi-match resolved by the precedence CASE
    /// (best terminal state wins). An empty broadcast_ids array makes ANY(@bids) match
    /// nothing -> every recipient is not_sent, no error.
    /// </summary>
    public virtual async IAsyncEnumerable<SendRecipientRow> ReadSendRecipientsAsync(
        int tenantId, long jobId, Guid[] broadcastIds, int? limit,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Two fixed statements (no string building): the capped form (PDF table) and the
        // full form (CSV/XLSX). LIMIT @lim is a bound parameter, not concatenated text.
        const string baseSql = @"
            SELECT r.normalized_phone,
                   m.status, m.sent_at, m.delivered_at, m.read_at, m.failed_reason
            FROM bulk_send_recipients r
            LEFT JOIN LATERAL (
                SELECT om.status, om.sent_at, om.delivered_at, om.read_at, om.failed_reason
                FROM outbound_messages om
                WHERE om.tenant_id = @tid
                  AND om.broadcast_id = ANY(@bids)
                  AND om.recipient_phone = r.normalized_phone
                ORDER BY CASE om.status
                    WHEN 'read' THEN 0 WHEN 'delivered' THEN 1 WHEN 'sent' THEN 2
                    WHEN 'sending' THEN 3 WHEN 'queued' THEN 4 WHEN 'failed' THEN 5
                    WHEN 'blocked' THEN 6 ELSE 7 END
                LIMIT 1
            ) m ON TRUE
            WHERE r.tenant_id = @tid AND r.job_id = @jid
            ORDER BY r.id";
        const string cappedSql = baseSql + "\n            LIMIT @lim";
        var sql = limit.HasValue ? cappedSql : baseSql;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("bids", broadcastIds);
        if (limit.HasValue) cmd.Parameters.AddWithValue("lim", limit.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = reader.IsDBNull(1) ? SendDeliveryStatus.NotSent : reader.GetString(1);
            yield return new SendRecipientRow
            {
                Phone = reader.GetString(0),
                Status = status,
                StatusLabel = SendDeliveryStatus.Label(status),
                SentAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                DeliveredAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                ReadAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                FailedReason = S(reader, 5)
            };
        }
    }

    /// <summary>Aggregate the per-recipient statuses into the campaign summary counts.</summary>
    public virtual async Task<(int total, int sent, int delivered, int read, int failed, int blocked, int notSent)>
        SummarizeRecipientsAsync(int tenantId, long jobId, Guid[] broadcastIds, CancellationToken ct = default)
    {
        // 'sent' here = reached-WhatsApp count (sent OR delivered OR read), matching the
        // operator's mental model of "left our system"; delivered/read are tracked separately.
        const string sql = @"
            WITH best AS (
                SELECT r.id, (
                    SELECT om.status FROM outbound_messages om
                    WHERE om.tenant_id=@tid AND om.broadcast_id = ANY(@bids)
                      AND om.recipient_phone = r.normalized_phone
                    ORDER BY CASE om.status
                        WHEN 'read' THEN 0 WHEN 'delivered' THEN 1 WHEN 'sent' THEN 2
                        WHEN 'sending' THEN 3 WHEN 'queued' THEN 4 WHEN 'failed' THEN 5
                        WHEN 'blocked' THEN 6 ELSE 7 END
                    LIMIT 1) AS status
                FROM bulk_send_recipients r
                WHERE r.tenant_id=@tid AND r.job_id=@jid
            )
            -- COUNT(*) is bigint in PostgreSQL; cast to int (bounded by the 50k cap) so the
            -- reader can use GetInt32 without an InvalidCastException.
            SELECT
                COUNT(*)::int,
                COUNT(*) FILTER (WHERE status IN ('sent','delivered','read'))::int,
                COUNT(*) FILTER (WHERE status IN ('delivered','read'))::int,
                COUNT(*) FILTER (WHERE status = 'read')::int,
                COUNT(*) FILTER (WHERE status = 'failed')::int,
                COUNT(*) FILTER (WHERE status = 'blocked')::int,
                COUNT(*) FILTER (WHERE status IS NULL)::int
            FROM best";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("bids", broadcastIds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    /// <summary>Recent bulk-send jobs for this tenant (export picker). Read-only metadata, newest first.</summary>
    public virtual async Task<List<SendJobSummary>> ListRecentJobsAsync(
        int tenantId, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, campaign_id, status, template_id, total_valid, created_at, completed_at
            FROM bulk_send_jobs
            WHERE tenant_id=@tid
            ORDER BY created_at DESC
            LIMIT @lim";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var jobs = new List<SendJobSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            jobs.Add(new SendJobSummary
            {
                Id = reader.GetInt64(0),
                CampaignId = reader.GetString(1),
                Status = reader.GetString(2),
                TemplateId = reader.GetInt32(3),
                TotalRecipients = reader.GetInt32(4),
                CreatedAt = reader.GetDateTime(5),
                CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
            });
        }
        return jobs;
    }

    /// <summary>Template display name for the summary card. null when missing.</summary>
    public virtual async Task<string?> GetTemplateNameAsync(int tenantId, int templateId, CancellationToken ct = default)
    {
        const string sql = "SELECT name FROM outbound_templates WHERE tenant_id=@tid AND id=@id";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", templateId);
        var name = await cmd.ExecuteScalarAsync(ct);
        return name == null || name is DBNull ? null : (string)name;
    }

    // ------------------------------------------------------------------
    private static string? S(NpgsqlDataReader reader, int ord) => reader.IsDBNull(ord) ? null : reader.GetString(ord);

    private static string? Trunc(string? s, int max) =>
        s == null ? null : (s.Length <= max ? s : s[..max]);
}
