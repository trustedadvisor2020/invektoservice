using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Chatinbox.Automation.Data;

/// <summary>
/// G6: PostgreSQL repository for flow_execution_state (persistent wait rows).
/// Thread-safe, register as singleton.
/// </summary>
public sealed class FlowWaitRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public FlowWaitRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Insert a pending wait row. Returns the new row id.
    /// </summary>
    public async Task<long> InsertPendingAsync(PendingWaitRow row, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO flow_execution_state
                (tenant_id, flow_id, chat_id, phone, instance_id, node_id,
                 resume_at, max_wait_at, session_state, callback_url, status)
            VALUES
                (@tid, @fid, @cid, @phone, @iid, @node,
                 @resume, @maxw, @state::jsonb, @cb, 'pending')
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", row.TenantId);
        cmd.Parameters.AddWithValue("fid", row.FlowId);
        cmd.Parameters.AddWithValue("cid", row.ChatId);
        cmd.Parameters.AddWithValue("phone", (object?)row.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iid", (object?)row.InstanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("node", row.NodeId);
        cmd.Parameters.AddWithValue("resume", row.ResumeAt.UtcDateTime);
        cmd.Parameters.AddWithValue("maxw", row.MaxWaitAt.UtcDateTime);
        cmd.Parameters.AddWithValue("state", NpgsqlDbType.Jsonb, row.SessionStateJson);
        cmd.Parameters.AddWithValue("cb", (object?)row.CallbackUrl ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id is long l ? l : 0;
    }

    /// <summary>
    /// Fetch due pending rows (resume_at &lt;= now) up to limit. Ordered by resume_at ASC.
    /// </summary>
    public async Task<List<DueWaitRow>> GetDueAsync(DateTimeOffset now, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, flow_id, chat_id, phone, instance_id, node_id,
                   resume_at, max_wait_at, session_state::text, callback_url, created_at
            FROM flow_execution_state
            WHERE status = 'pending' AND resume_at <= @now
            ORDER BY resume_at ASC
            LIMIT @lim";

        var result = new List<DueWaitRow>();
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("now", now.UtcDateTime);
        cmd.Parameters.AddWithValue("lim", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new DueWaitRow
            {
                Id = reader.GetInt64(0),
                TenantId = reader.GetInt32(1),
                FlowId = reader.GetInt32(2),
                ChatId = reader.GetString(3),
                Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
                InstanceId = reader.IsDBNull(5) ? null : reader.GetString(5),
                NodeId = reader.GetString(6),
                ResumeAt = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)),
                MaxWaitAt = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)),
                SessionStateJson = reader.GetString(9),
                CallbackUrl = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc))
            });
        }
        return result;
    }

    /// <summary>
    /// Atomic claim: mark a pending row as resumed. Returns true if claim succeeded
    /// (prevents double-resume if multiple resumer ticks overlap).
    /// </summary>
    public async Task<bool> TryMarkResumedAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE flow_execution_state
            SET status = 'resumed', resumed_at = NOW()
            WHERE id = @id AND status = 'pending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows == 1;
    }

    /// <summary>Mark a row as failed with error text (ops visibility).</summary>
    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE flow_execution_state
            SET status = 'failed', last_error = @err, resumed_at = NOW()
            WHERE id = @id AND status IN ('pending','resumed')";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("err", error.Length > 2000 ? error.Substring(0, 2000) : error);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Cancel all pending waits for a given tenant+chat (e.g. user replied during wait).
    /// Returns count of cancelled rows.
    /// </summary>
    public async Task<int> CancelPendingForChatAsync(int tenantId, string chatId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE flow_execution_state
            SET status = 'cancelled', resumed_at = NOW()
            WHERE tenant_id = @tid AND chat_id = @cid AND status = 'pending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", chatId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Ops metrics: pending count + overdue count (pending AND resume_at &lt; now - threshold).
    /// </summary>
    public async Task<FlowWaitMetrics> GetMetricsAsync(TimeSpan overdueThreshold, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) FILTER (WHERE status = 'pending') AS pending,
                COUNT(*) FILTER (WHERE status = 'pending' AND resume_at < @threshold) AS overdue,
                COUNT(*) FILTER (WHERE status = 'failed') AS failed
            FROM flow_execution_state";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("threshold", (DateTimeOffset.UtcNow - overdueThreshold).UtcDateTime);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new FlowWaitMetrics();

        return new FlowWaitMetrics
        {
            PendingCount = reader.GetInt64(0),
            OverdueCount = reader.GetInt64(1),
            FailedCount = reader.GetInt64(2)
        };
    }
}

public sealed class PendingWaitRow
{
    public required int TenantId { get; init; }
    public required int FlowId { get; init; }
    public required string ChatId { get; init; }
    public string? Phone { get; init; }
    public string? InstanceId { get; init; }
    public required string NodeId { get; init; }
    public required DateTimeOffset ResumeAt { get; init; }
    public required DateTimeOffset MaxWaitAt { get; init; }
    public required string SessionStateJson { get; init; }
    public string? CallbackUrl { get; init; }
}

public sealed class DueWaitRow
{
    public required long Id { get; init; }
    public required int TenantId { get; init; }
    public required int FlowId { get; init; }
    public required string ChatId { get; init; }
    public string? Phone { get; init; }
    public string? InstanceId { get; init; }
    public required string NodeId { get; init; }
    public required DateTimeOffset ResumeAt { get; init; }
    public required DateTimeOffset MaxWaitAt { get; init; }
    public required string SessionStateJson { get; init; }
    public string? CallbackUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class FlowWaitMetrics
{
    public long PendingCount { get; init; }
    public long OverdueCount { get; init; }
    public long FailedCount { get; init; }
}
