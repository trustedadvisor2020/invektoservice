// Adim 3 Paket 1: persistence for zoho_sync_log (every Gunes -> Zoho sync attempt).
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
    /// Upsert-style: if a pending/failed row exists for (tenant_id, gunes_event, gunes_lead_id) it is reused;
    /// otherwise a new 'pending' row is inserted. Returns the log id.
    /// </summary>
    public async Task<long> BeginAttemptAsync(
        int tenantId,
        string gunesEvent,
        string gunesLeadId,
        string? zohoLeadId,
        CancellationToken ct = default)
    {
        // Atomic upsert: relies on partial unique index `ux_zoho_sync_log_open_attempt` on
        // (tenant_id, gunes_event, gunes_lead_id) WHERE status IN ('pending','failed').
        // This gives a single-statement CAS: a new row is created OR the existing non-terminal
        // row's attempt_count is incremented — no read-then-write race window.
        const string sql = @"
            INSERT INTO zoho_sync_log
                (tenant_id, gunes_event, gunes_lead_id, zoho_lead_id, status, attempt_count)
            VALUES
                (@tid, @evt, @lid, @zlid, 'pending', 1)
            ON CONFLICT (tenant_id, gunes_event, gunes_lead_id)
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
        cmd.Parameters.AddWithValue("@evt", gunesEvent);
        cmd.Parameters.AddWithValue("@lid", gunesLeadId);
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
            SELECT id, tenant_id, gunes_event, gunes_lead_id, zoho_lead_id, zoho_transition_id,
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
    /// Schema reference: arch/db/migrations/014-zoho-sync-log.sql (columns status, updated_at, attempt_count).
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
    /// Schema reference: arch/db/migrations/014-zoho-sync-log.sql.
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
            SELECT l.id, l.tenant_id, l.gunes_event, l.gunes_lead_id, l.zoho_lead_id, l.attempt_count
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
                    GunesEvent:   reader.GetString(2),
                    GunesLeadId:  reader.GetString(3),
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

    private static ZohoSyncLogRow ReadRow(NpgsqlDataReader r) => new(
        Id:                 r.GetInt64(0),
        TenantId:           r.GetInt32(1),
        GunesEvent:         r.GetString(2),
        GunesLeadId:        r.GetString(3),
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
    string GunesEvent,
    string GunesLeadId,
    string? ZohoLeadId,
    int AttemptCount);

public sealed record ZohoSyncLogRow(
    long Id,
    int TenantId,
    string GunesEvent,
    string GunesLeadId,
    string? ZohoLeadId,
    string? ZohoTransitionId,
    string Status,
    int AttemptCount,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime? CompletedAt);
