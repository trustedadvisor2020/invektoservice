using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Leads;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Npgsql;
using System.Text.Json;

namespace Chatinbox.Backend.Data;

/// <summary>
/// FEAT-LIW Chunk C: append-only audit trail for tenant_landing_settings mutations.
/// Separate class from <see cref="TenantLandingSettingsRepository"/> (SRP — settings
/// repo speaks to one table; audit is a distinct table with distinct access patterns).
/// Writes run inside the caller's transaction (the connection is passed in) so an
/// audit-insert failure rolls back the main settings UPDATE — avoids the CQ2
/// fire-and-forget silent-drift pattern. Reads run on their own connection (no
/// transaction needed).
/// Schema reference: arch/db/liw-audit-log.sql (canonical) + arch/db/migrations/022-liw-audit-log.sql.
/// Table shape: (id BIGSERIAL, tenant_id INT, user_id INT?, action VARCHAR(50),
/// before_json JSONB?, after_json JSONB?, created_at TIMESTAMPTZ) with index
/// idx_liw_audit_log_tenant_created(tenant_id, created_at DESC).
/// </summary>
public class LiwAuditRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public LiwAuditRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Insert a single audit row using the CALLER'S OPEN connection (so the write
    /// participates in the caller's transaction). Action value MUST come from
    /// <see cref="LiwAuditActions"/>; beforeJson / afterJson are raw JSON strings
    /// (null for actions that have no prior state / no resulting state).
    /// </summary>
    public virtual async Task InsertAsync(
        NpgsqlConnection conn,
        int tenantId,
        int? userId,
        string action,
        string? beforeJson,
        string? afterJson,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO liw_audit_log (tenant_id, user_id, action, before_json, after_json)
            VALUES (@tid, @uid, @action, @before::jsonb, @after::jsonb)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("before", (object?)beforeJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("after", (object?)afterJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Timeline read for the Dashboard AuditLogTimeline component.
    /// SELECT WHERE tenant_id = @tid ORDER BY created_at DESC LIMIT @limit.
    /// Caller clamps limit to [1, 200]; this method trusts the caller.
    /// </summary>
    public virtual async Task<List<LiwAuditEntryDto>> ListAsync(
        int tenantId,
        int limit,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, user_id, action, before_json::text, after_json::text, created_at
            FROM liw_audit_log
            WHERE tenant_id = @tid
            ORDER BY created_at DESC
            LIMIT @limit";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("limit", limit);

        var list = new List<LiwAuditEntryDto>(capacity: limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var userId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var beforeText = reader.IsDBNull(3) ? null : reader.GetString(3);
            var afterText = reader.IsDBNull(4) ? null : reader.GetString(4);

            var id = reader.GetInt64(0);
            list.Add(new LiwAuditEntryDto
            {
                Id = id,
                Action = reader.GetString(2),
                BeforeJson = ParseElement(beforeText, id),
                AfterJson = ParseElement(afterText, id),
                CreatedAt = reader.GetDateTime(5),
                UserDisplay = userId.HasValue ? $"User#{userId.Value}" : "Sistem"
            });
        }
        return list;
    }

    private JsonElement? ParseElement(string? json, long auditId)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            // Clone so the JsonElement outlives the disposed document.
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            // Audit payload malformed — observe rather than silent-convert. Timeline
            // keeps rendering (return null) but ops sees a structured log line so
            // corruption is not hidden. Data-integrity drift warrants investigation.
            _logger.SystemWarn(
                $"[{ErrorCodes.GeneralValidation}] LiwAuditRepository.ParseElement: " +
                $"audit_id={auditId} malformed JSONB: {ex.Message}");
            return null;
        }
    }
}
