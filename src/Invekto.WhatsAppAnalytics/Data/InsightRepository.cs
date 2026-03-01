using System.Text;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for insight engine tables: wa_response_times, wa_agent_metrics, wa_rescue_candidates.
/// </summary>
public sealed class InsightRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public InsightRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Shared: Read from wa_conversation_outcomes (read-only)
    // ============================================================

    /// <summary>
    /// Get classified outcomes for a tenant (conversation_id + outcome_label).
    /// Read-only access to wa_conversation_outcomes for insight computation.
    /// </summary>
    public async Task<List<(string ConversationId, string OutcomeLabel)>> GetOutcomesForTenantAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, outcome_label
            FROM wa_conversation_outcomes
            WHERE tenant_id = @tid";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1)));
        return results;
    }

    // ============================================================
    // wa_response_times
    // ============================================================

    /// <summary>
    /// Batch upsert response time records (INSERT ON CONFLICT UPDATE).
    /// </summary>
    public async Task UpsertResponseTimesAsync(List<ResponseTimeRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        const int batchSize = 50;
        for (var offset = 0; offset < records.Count; offset += batchSize)
        {
            var batch = records.Skip(offset).Take(batchSize).ToList();
            await UpsertResponseTimeBatchAsync(batch, ct);
        }
    }

    private async Task UpsertResponseTimeBatchAsync(List<ResponseTimeRecord> batch, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var sb = new StringBuilder();
        sb.AppendLine(@"INSERT INTO wa_response_times
            (tenant_id, conversation_id, instance_id, first_customer_msg_at, first_agent_response_at,
             response_time_ms, bucket, outcome_label, computed_at)
            VALUES ");

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.AppendLine($"(@tid{i}, @cid{i}, @iid{i}, @fcm{i}, @far{i}, @rtm{i}, @bkt{i}, @lbl{i}, NOW())");
            var r = batch[i];
            cmd.Parameters.AddWithValue($"tid{i}", r.TenantId);
            cmd.Parameters.AddWithValue($"cid{i}", r.ConversationId);
            cmd.Parameters.AddWithValue($"iid{i}", (object?)r.InstanceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"fcm{i}", r.FirstCustomerMsgAt.HasValue
                ? (object)r.FirstCustomerMsgAt.Value.ToUniversalTime()
                : DBNull.Value);
            cmd.Parameters.AddWithValue($"far{i}", r.FirstAgentResponseAt.HasValue
                ? (object)r.FirstAgentResponseAt.Value.ToUniversalTime()
                : DBNull.Value);
            cmd.Parameters.AddWithValue($"rtm{i}", (object?)r.ResponseTimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"bkt{i}", r.Bucket);
            cmd.Parameters.AddWithValue($"lbl{i}", (object?)r.OutcomeLabel ?? DBNull.Value);
        }

        sb.AppendLine(@"ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET
            instance_id = EXCLUDED.instance_id,
            first_customer_msg_at = EXCLUDED.first_customer_msg_at,
            first_agent_response_at = EXCLUDED.first_agent_response_at,
            response_time_ms = EXCLUDED.response_time_ms,
            bucket = EXCLUDED.bucket,
            outcome_label = EXCLUDED.outcome_label,
            computed_at = NOW()");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get response time bucket correlation with outcome labels for a tenant.
    /// Returns per-bucket: count, sale_count, conversion_rate.
    /// </summary>
    public async Task<ResponseTimeInsight> GetResponseTimeInsightAsync(int tenantId, int? instanceId = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var where = "WHERE rt.tenant_id = @tid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (instanceId.HasValue)
        {
            where += " AND rt.instance_id = @iid";
            cmd.Parameters.AddWithValue("iid", instanceId.Value);
        }

        cmd.CommandText = $@"
            SELECT
                rt.bucket,
                COUNT(*)::INT AS conversation_count,
                COUNT(*) FILTER (WHERE rt.outcome_label = 'sale')::INT AS sale_count,
                AVG(rt.response_time_ms) AS avg_rt
            FROM wa_response_times rt
            {where}
            GROUP BY rt.bucket
            ORDER BY
                CASE rt.bucket
                    WHEN '0-5m' THEN 1
                    WHEN '5-15m' THEN 2
                    WHEN '15-60m' THEN 3
                    WHEN '1-4h' THEN 4
                    WHEN '4h+' THEN 5
                    WHEN 'no_response' THEN 6
                    ELSE 7
                END";

        var buckets = new List<ResponseTimeBucketCorrelation>();
        var totalConversations = 0;
        long totalResponseTime = 0;
        var responseTimeCount = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var bucket = reader.GetString(0);
            var count = reader.GetInt32(1);
            var saleCount = reader.GetInt32(2);
            var avgRt = reader.IsDBNull(3) ? (long?)null : (long)reader.GetDouble(3);

            totalConversations += count;
            if (avgRt.HasValue)
            {
                totalResponseTime += avgRt.Value * count;
                responseTimeCount += count;
            }

            buckets.Add(new ResponseTimeBucketCorrelation
            {
                Bucket = bucket,
                BucketLabel = ResponseTimeBuckets.GetLabel(bucket),
                ConversationCount = count,
                SaleCount = saleCount,
                ConversionRate = count > 0 ? Math.Round((double)saleCount / count * 100, 1) : 0
            });
        }

        // Fill percentages
        foreach (var b in buckets)
        {
            b.Percentage = totalConversations > 0
                ? Math.Round((double)b.ConversationCount / totalConversations * 100, 1)
                : 0;
        }

        return new ResponseTimeInsight
        {
            TenantId = tenantId,
            InstanceId = instanceId,
            TotalConversations = totalConversations,
            AvgResponseTimeMs = responseTimeCount > 0 ? totalResponseTime / responseTimeCount : null,
            Buckets = buckets
        };
    }

    /// <summary>
    /// Delete all response time records for a tenant (used before recompute).
    /// </summary>
    public async Task DeleteResponseTimesAsync(int tenantId, int? instanceId = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var where = "WHERE tenant_id = @tid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (instanceId.HasValue)
        {
            where += " AND instance_id = @iid";
            cmd.Parameters.AddWithValue("iid", instanceId.Value);
        }

        cmd.CommandText = $"DELETE FROM wa_response_times {where}";
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        _logger.StepInfo($"[InsightRepo] Deleted {deleted} response time records for tenant {tenantId}", "insight");
    }
}
