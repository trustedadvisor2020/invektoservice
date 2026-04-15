// Adim 3 Paket 1: persistence for zoho_sync_log (every source -> Zoho sync attempt).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Integrations.Data;

public sealed class ZohoSyncLogRepository
{
    private readonly PostgresConnectionFactory _db;

    public ZohoSyncLogRepository(PostgresConnectionFactory db) => _db = db;

    /// <summary>
    /// Upsert-style: if a pending/failed row exists for (tenant_id, zoho_event, source_lead_id) it is reused;
    /// otherwise a new 'pending' row is inserted. Returns the log id.
    /// </summary>
    public async Task<long> BeginAttemptAsync(
        int tenantId,
        string zohoEvent,
        string sourceLeadId,
        string? zohoLeadId,
        CancellationToken ct = default)
    {
        // Atomic upsert: relies on partial unique index `ux_zoho_sync_log_open_attempt` on
        // (tenant_id, zoho_event, source_lead_id) WHERE status IN ('pending','failed').
        // This gives a single-statement CAS: a new row is created OR the existing non-terminal
        // row's attempt_count is incremented — no read-then-write race window.
        const string sql = @"
            INSERT INTO zoho_sync_log
                (tenant_id, zoho_event, source_lead_id, zoho_lead_id, status, attempt_count)
            VALUES
                (@tid, @evt, @lid, @zlid, 'pending', 1)
            ON CONFLICT (tenant_id, zoho_event, source_lead_id)
                WHERE status IN ('pending','failed')
            DO UPDATE SET
                status        = 'pending',
                attempt_count = zoho_sync_log.attempt_count + 1,
                zoho_lead_id  = COALESCE(EXCLUDED.zoho_lead_id, zoho_sync_log.zoho_lead_id),
                updated_at    = NOW()
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        cmd.Parameters.AddWithValue("@evt", zohoEvent);
        cmd.Parameters.AddWithValue("@lid", sourceLeadId);
        cmd.Parameters.AddWithValue("@zlid", (object?)zohoLeadId ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("INV-INT-125: zoho_sync_log insert/update returned no id.");
        return Convert.ToInt64(result);
    }

    public async Task MarkSuccessAsync(
        long id, string zohoLeadId, string zohoTransitionId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_sync_log
               SET status = 'success',
                   zoho_lead_id = @zlid,
                   zoho_transition_id = @tid,
                   last_error_code = NULL,
                   last_error_message = NULL,
                   updated_at = NOW(),
                   completed_at = NOW()
             WHERE id = @id";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@zlid", zohoLeadId);
        cmd.Parameters.AddWithValue("@tid", zohoTransitionId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        long id, string errorCode, string errorMessage, bool terminal, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_sync_log
               SET status = 'failed',
                   last_error_code = @code,
                   last_error_message = @msg,
                   updated_at = NOW(),
                   completed_at = CASE WHEN @terminal THEN NOW() ELSE completed_at END
             WHERE id = @id";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@code", errorCode);
        cmd.Parameters.AddWithValue("@msg", Truncate(errorMessage, 2000));
        cmd.Parameters.AddWithValue("@terminal", terminal);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<ZohoSyncLogRow?> GetAsync(int tenantId, long id, CancellationToken ct = default)
    {
        // Tenant isolation: every DB read must be scoped by tenant_id per project rules.
        const string sql = @"
            SELECT id, tenant_id, zoho_event, source_lead_id, zoho_lead_id, zoho_transition_id,
                   status, attempt_count, last_error_code, last_error_message, completed_at
              FROM zoho_sync_log
             WHERE id = @id AND tenant_id = @tid";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return ReadRow(reader);
    }

    /// <summary>
    /// Adim 3 Paket 2: Enumerate tenants that currently have retry-eligible zoho_sync_log rows.
    /// Used by ZohoRetryWorker to iterate per-tenant (preserves tenant_id scoping on the per-batch
    /// ListRetryCandidatesForTenantAsync query below).
    /// Schema reference: arch/db/migrations/014-zoho-sync-log.sql + 015-rename-gunes-to-zoho.sql (columns status, updated_at, attempt_count).
    /// </summary>
    public async Task<IReadOnlyList<int>> ListTenantsWithRetryCandidatesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT tenant_id
              FROM zoho_sync_log
             WHERE status = 'failed'
               AND updated_at < NOW() - INTERVAL '10 minutes'
               AND attempt_count < 2";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var rows = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(reader.GetInt32(0));
        return rows;
    }

    /// <summary>
    /// Adim 3 Paket 2: Claim failed rows eligible for retry, scoped to a single tenant.
    /// Contract: tenant_id=@tid AND status='failed' AND updated_at &lt; NOW() - INTERVAL '10 minutes' AND attempt_count &lt; 2.
    /// Uses SELECT ... FOR UPDATE SKIP LOCKED for horizontal-scale safety; the lock window is bounded by the
    /// explicit transaction. Worker calls IZohoSyncService.SyncAsync which itself runs BeginAttemptAsync
    /// (attempt++) so this method does NOT mutate state — it only projects the request reconstruction fields.
    /// </summary>
    public async Task<IReadOnlyList<ZohoRetryCandidate>> ListRetryCandidatesForTenantAsync(
        int tenantId, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            WITH claimed AS (
                SELECT id
                  FROM zoho_sync_log
                 WHERE tenant_id = @tid
                   AND status = 'failed'
                   AND updated_at < NOW() - INTERVAL '10 minutes'
                   AND attempt_count < 2
                 ORDER BY updated_at ASC
                 LIMIT @lim
                 FOR UPDATE SKIP LOCKED
            )
            SELECT l.id, l.tenant_id, l.zoho_event, l.source_lead_id, l.zoho_lead_id, l.attempt_count
              FROM zoho_sync_log l
              JOIN claimed c ON c.id = l.id";

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        cmd.Parameters.AddWithValue("@lim", Math.Max(1, limit));

        var rows = new List<ZohoRetryCandidate>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(new ZohoRetryCandidate(
                    Id:           reader.GetInt64(0),
                    TenantId:     reader.GetInt32(1),
                    ZohoEvent:    reader.GetString(2),
                    SourceLeadId: reader.GetString(3),
                    ZohoLeadId:   reader.IsDBNull(4) ? null : reader.GetString(4),
                    AttemptCount: reader.GetInt32(5)));
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return rows;
    }

    public async Task<int> CountFailedAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM zoho_sync_log WHERE tenant_id = @tid AND status = 'failed'";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is null ? 0 : Convert.ToInt32(result);
    }

    /// <summary>
    /// Adim 3 Paket 3-B1: Dashboard UI icin paginated+filtered sync log listesi.
    /// Tenant izolasyonu: her sorgu tenant_id ile baslar. Filtre alanlari: status (tam esleme),
    /// zoho_event (tam esleme), from/to (updated_at araligi). Sayfa boyutu yukarida endpoint'te clamp edilir.
    /// </summary>
    public async Task<(IReadOnlyList<ZohoSyncLogRow> Items, int TotalCount)> ListForDashboardAsync(
        int tenantId,
        string? statusFilter,
        string? eventFilter,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // WHERE composition stays parameterized — no string concat of values.
        var where = new System.Text.StringBuilder("WHERE tenant_id = @tid");
        if (!string.IsNullOrEmpty(statusFilter)) where.Append(" AND status = @status");
        if (!string.IsNullOrEmpty(eventFilter))  where.Append(" AND zoho_event = @evt");
        if (fromUtc.HasValue)                    where.Append(" AND updated_at >= @from");
        if (toUtc.HasValue)                      where.Append(" AND updated_at <= @to");

        var listSql =
            "SELECT id, tenant_id, zoho_event, source_lead_id, zoho_lead_id, zoho_transition_id, " +
            "       status, attempt_count, last_error_code, last_error_message, completed_at, updated_at " +
            "  FROM zoho_sync_log " + where +
            " ORDER BY updated_at DESC, id DESC " +
            " LIMIT @lim OFFSET @off";

        var countSql = "SELECT COUNT(*) FROM zoho_sync_log " + where;

        var safePage     = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 1 : pageSize;
        var offset       = (safePage - 1) * safePageSize;

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);

        int total;
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("@tid", tenantId);
            if (!string.IsNullOrEmpty(statusFilter)) countCmd.Parameters.AddWithValue("@status", statusFilter);
            if (!string.IsNullOrEmpty(eventFilter))  countCmd.Parameters.AddWithValue("@evt", eventFilter);
            if (fromUtc.HasValue)                    countCmd.Parameters.AddWithValue("@from", fromUtc.Value);
            if (toUtc.HasValue)                      countCmd.Parameters.AddWithValue("@to", toUtc.Value);
            var raw = await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            total = raw is null ? 0 : Convert.ToInt32(raw);
        }

        var items = new List<ZohoSyncLogRow>();
        await using (var cmd = new NpgsqlCommand(listSql, conn))
        {
            cmd.Parameters.AddWithValue("@tid", tenantId);
            if (!string.IsNullOrEmpty(statusFilter)) cmd.Parameters.AddWithValue("@status", statusFilter);
            if (!string.IsNullOrEmpty(eventFilter))  cmd.Parameters.AddWithValue("@evt", eventFilter);
            if (fromUtc.HasValue)                    cmd.Parameters.AddWithValue("@from", fromUtc.Value);
            if (toUtc.HasValue)                      cmd.Parameters.AddWithValue("@to", toUtc.Value);
            cmd.Parameters.AddWithValue("@lim", safePageSize);
            cmd.Parameters.AddWithValue("@off", offset);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                items.Add(new ZohoSyncLogRow(
                    Id:               reader.GetInt64(0),
                    TenantId:         reader.GetInt32(1),
                    ZohoEvent:        reader.GetString(2),
                    SourceLeadId:     reader.GetString(3),
                    ZohoLeadId:       reader.IsDBNull(4)  ? null : reader.GetString(4),
                    ZohoTransitionId: reader.IsDBNull(5)  ? null : reader.GetString(5),
                    Status:           reader.GetString(6),
                    AttemptCount:     reader.GetInt32(7),
                    LastErrorCode:    reader.IsDBNull(8)  ? null : reader.GetString(8),
                    LastErrorMessage: reader.IsDBNull(9)  ? null : reader.GetString(9),
                    CompletedAt:      reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    UpdatedAt:        reader.GetDateTime(11)));
            }
        }

        return (items, total);
    }

    /// <summary>
    /// Adim 3 Paket 3-B1: Manuel retry. Sadece status='failed' olan (tenant-scoped) row icin
    /// status='pending' + attempt_count=0 + updated_at=NOW() set eder. RetryWorker bir sonraki
    /// tick'te pickup eder. Dondurulen tuple: (Updated, NewAttemptCount) — Updated=false ise
    /// endpoint 404/409 cikarir (GetAsync ile ayirt edilir).
    /// </summary>
    public async Task<bool> ResetForRetryAsync(int tenantId, long id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_sync_log
               SET status = 'pending',
                   attempt_count = 0,
                   updated_at = NOW()
             WHERE tenant_id = @tid
               AND id = @id
               AND status = 'failed'";
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        cmd.Parameters.AddWithValue("@id", id);
        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return affected == 1;
    }

    private static ZohoSyncLogRow ReadRow(NpgsqlDataReader r) => new(
        Id:                 r.GetInt64(0),
        TenantId:           r.GetInt32(1),
        ZohoEvent:          r.GetString(2),
        SourceLeadId:       r.GetString(3),
        ZohoLeadId:         r.IsDBNull(4) ? null : r.GetString(4),
        ZohoTransitionId:   r.IsDBNull(5) ? null : r.GetString(5),
        Status:             r.GetString(6),
        AttemptCount:       r.GetInt32(7),
        LastErrorCode:      r.IsDBNull(8) ? null : r.GetString(8),
        LastErrorMessage:   r.IsDBNull(9) ? null : r.GetString(9),
        CompletedAt:        r.IsDBNull(10) ? null : r.GetDateTime(10));

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max));
}

public sealed record ZohoRetryCandidate(
    long Id,
    int TenantId,
    string ZohoEvent,
    string SourceLeadId,
    string? ZohoLeadId,
    int AttemptCount);

public sealed record ZohoSyncLogRow(
    long Id,
    int TenantId,
    string ZohoEvent,
    string SourceLeadId,
    string? ZohoLeadId,
    string? ZohoTransitionId,
    string Status,
    int AttemptCount,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime? CompletedAt,
    DateTime UpdatedAt = default);
