using Chatinbox.Shared.Data;
using Npgsql;

namespace Chatinbox.Backend.Data;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3 HARDENING — drain-side access to customer_status_flow_outbox. Mirrors the
/// proven inma_optout_outbox drain (Outbound OutboundRepository): atomic claim via FOR UPDATE SKIP LOCKED,
/// startup stale-'processing' recovery, attempts-bounded enqueue-failure transition.
///
/// The TERMINAL 'done' transition is intentionally NOT here — it is written by the drained
/// <c>TriggerCustomerStatusFlowJob</c> (Automation) as an atomic claim BEFORE it runs the flow, so a
/// re-enqueue (stale recovery) can never double-fire the flow (exactly-once at start; see
/// AutomationRepository.ClaimCustomerStatusOutboxAsync). attempts is bumped ONLY on an enqueue FAILURE
/// (not at claim and not on stale recovery), so a row that is merely re-driven because the worker crashed
/// is retried indefinitely until Hangfire accepts it — exactly the durability guarantee we want.
/// </summary>
public sealed class CustomerStatusFlowOutboxRepository
{
    private readonly PostgresConnectionFactory _db;

    public CustomerStatusFlowOutboxRepository(PostgresConnectionFactory db) => _db = db;

    /// <summary>
    /// Atomically claims up to <paramref name="limit"/> pending rows (status -> 'processing') in one
    /// UPDATE ... RETURNING. The inner SELECT uses FOR UPDATE SKIP LOCKED so parallel drains never pick the
    /// same row. Cross-tenant by design — a background sweep, NOT a request path; every returned row carries
    /// its own tenant_id for the per-tenant enqueue (sanctioned background-sweep exception, see
    /// arch/codex-context.md, precedent OutboundRepository.FetchPendingOutboxBatchAsync).
    /// </summary>
    public async Task<List<CustomerStatusOutboxRow>> FetchPendingBatchAsync(
        int limit, int maxAttempts, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE customer_status_flow_outbox AS o
            SET status = 'processing',
                attempted_at = NOW()
            WHERE o.id IN (
                SELECT id FROM customer_status_flow_outbox
                WHERE status = 'pending' AND attempts < @maxAttempts
                ORDER BY created_at
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, tenant_id, event_id, lead_id, new_status, old_status,
                      feature_group_id, feature_group_name, changed_by, customer_id, phone, attempts";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("maxAttempts", maxAttempts);

        var rows = new List<CustomerStatusOutboxRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new CustomerStatusOutboxRow(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetInt32(11)));
        }
        return rows;
    }

    /// <summary>
    /// Startup recovery: revert rows the drain claimed ('processing') but that never reached terminal 'done'
    /// (process crashed between claim/enqueue and the job's claim). Older than <paramref name="staleSeconds"/>
    /// so a healthy concurrent drain/job is not raced. attempts is NOT touched — a stale recovery is not an
    /// enqueue failure; the job's atomic claim makes the eventual second execution a no-op.
    /// </summary>
    public async Task<int> RecoverStuckProcessingAsync(int staleSeconds, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE customer_status_flow_outbox
            SET status = 'pending'
            WHERE status = 'processing'
              AND attempted_at IS NOT NULL
              AND attempted_at < NOW() - make_interval(secs => @stale)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("stale", staleSeconds);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Enqueue-failure transition (the drain could not hand the row to Hangfire): attempts++; the row returns
    /// to 'pending' for the next tick, or flips to 'failed' once attempts+1 >= maxAttempts (INV-INM-009 ops
    /// signal — the status was already applied to the lead in C2; only the dispatch is stuck).
    /// </summary>
    public async Task MarkEnqueueFailedAsync(
        long id, string? rawError, int maxAttempts, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE customer_status_flow_outbox
            SET attempts = attempts + 1,
                status = CASE WHEN attempts + 1 >= @maxAttempts THEN 'failed' ELSE 'pending' END,
                last_error = @err,
                attempted_at = NOW()
            WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("err", (object?)rawError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maxAttempts", maxAttempts);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>One claimed customer_status_flow_outbox row — carries the TriggerCustomerStatusFlowJob args.</summary>
public sealed record CustomerStatusOutboxRow(
    long Id,
    int TenantId,
    string EventId,
    int LeadId,
    string? NewStatus,
    string? OldStatus,
    int? FeatureGroupId,
    string? FeatureGroupName,
    string? ChangedBy,
    int? CustomerId,
    string? Phone,
    int Attempts);
