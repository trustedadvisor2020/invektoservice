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

    // ============================================================
    // wa_response_times: Read for agent leaderboard (RI-3.3)
    // ============================================================

    /// <summary>
    /// Get response times per conversation for a tenant (for agent avg_response_time aggregation).
    /// Returns dictionary: conversation_id -> response_time_ms.
    /// </summary>
    public async Task<Dictionary<string, long>> GetResponseTimesByConversationAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, response_time_ms
            FROM wa_response_times
            WHERE tenant_id = @tid AND response_time_ms IS NOT NULL";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results[reader.GetString(0)] = reader.GetInt64(1);
        return results;
    }

    // ============================================================
    // wa_agent_metrics (RI-3.3)
    // ============================================================

    /// <summary>
    /// Batch upsert agent metric records (INSERT ON CONFLICT UPDATE).
    /// UNIQUE constraint: (tenant_id, instance_id, agent_id).
    /// </summary>
    public async Task UpsertAgentMetricsAsync(List<AgentMetricRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        const int batchSize = 50;
        for (var offset = 0; offset < records.Count; offset += batchSize)
        {
            var batch = records.Skip(offset).Take(batchSize).ToList();
            await UpsertAgentMetricsBatchAsync(batch, ct);
        }
    }

    private async Task UpsertAgentMetricsBatchAsync(List<AgentMetricRecord> batch, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var sb = new StringBuilder();
        sb.AppendLine(@"INSERT INTO wa_agent_metrics
            (tenant_id, instance_id, agent_id, agent_name, total_conversations,
             sale_count, offered_count, no_response_count, offer_lost_count, other_count,
             conversion_rate, avg_response_time_ms, ghost_rate, weighted_score, computed_at)
            VALUES ");

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.AppendLine($"(@tid{i}, @iid{i}, @aid{i}, @anm{i}, @tot{i}, @sal{i}, @off{i}, @nrc{i}, @olc{i}, @oth{i}, @cvr{i}, @art{i}, @ghr{i}, @wsc{i}, NOW())");
            var r = batch[i];
            cmd.Parameters.AddWithValue($"tid{i}", r.TenantId);
            cmd.Parameters.AddWithValue($"iid{i}", (object?)r.InstanceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"aid{i}", r.AgentId);
            cmd.Parameters.AddWithValue($"anm{i}", r.AgentName);
            cmd.Parameters.AddWithValue($"tot{i}", r.TotalConversations);
            cmd.Parameters.AddWithValue($"sal{i}", r.SaleCount);
            cmd.Parameters.AddWithValue($"off{i}", r.OfferedCount);
            cmd.Parameters.AddWithValue($"nrc{i}", r.NoResponseCount);
            cmd.Parameters.AddWithValue($"olc{i}", r.OfferLostCount);
            cmd.Parameters.AddWithValue($"oth{i}", r.OtherCount);
            cmd.Parameters.AddWithValue($"cvr{i}", (float)r.ConversionRate);
            cmd.Parameters.AddWithValue($"art{i}", (object?)r.AvgResponseTimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"ghr{i}", (float)r.GhostRate);
            cmd.Parameters.AddWithValue($"wsc{i}", (float)r.WeightedScore);
        }

        sb.AppendLine(@"ON CONFLICT (tenant_id, instance_id, agent_id) DO UPDATE SET
            agent_name = EXCLUDED.agent_name,
            total_conversations = EXCLUDED.total_conversations,
            sale_count = EXCLUDED.sale_count,
            offered_count = EXCLUDED.offered_count,
            no_response_count = EXCLUDED.no_response_count,
            offer_lost_count = EXCLUDED.offer_lost_count,
            other_count = EXCLUDED.other_count,
            conversion_rate = EXCLUDED.conversion_rate,
            avg_response_time_ms = EXCLUDED.avg_response_time_ms,
            ghost_rate = EXCLUDED.ghost_rate,
            weighted_score = EXCLUDED.weighted_score,
            computed_at = NOW()");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get agent leaderboard for a tenant, ordered by weighted_score DESC.
    /// Optionally filtered by instance_id.
    /// </summary>
    public async Task<AgentLeaderboardInsight> GetAgentLeaderboardAsync(int tenantId, int? instanceId = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var where = "WHERE am.tenant_id = @tid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (instanceId.HasValue)
        {
            where += " AND am.instance_id = @iid";
            cmd.Parameters.AddWithValue("iid", instanceId.Value);
        }

        cmd.CommandText = $@"
            SELECT am.agent_id, am.agent_name, am.instance_id,
                   am.total_conversations, am.sale_count, am.offered_count,
                   am.no_response_count, am.offer_lost_count, am.other_count,
                   am.conversion_rate, am.avg_response_time_ms, am.ghost_rate, am.weighted_score
            FROM wa_agent_metrics am
            {where}
            ORDER BY am.weighted_score DESC";

        var agents = new List<AgentLeaderboardEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            agents.Add(new AgentLeaderboardEntry
            {
                AgentId = reader.GetInt32(0),
                AgentName = reader.GetString(1),
                InstanceId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                TotalConversations = reader.GetInt32(3),
                SaleCount = reader.GetInt32(4),
                OfferedCount = reader.GetInt32(5),
                NoResponseCount = reader.GetInt32(6),
                OfferLostCount = reader.GetInt32(7),
                OtherCount = reader.GetInt32(8),
                ConversionRate = Math.Round(reader.GetFloat(9), 1),
                AvgResponseTimeMs = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                GhostRate = Math.Round(reader.GetFloat(11), 1),
                WeightedScore = Math.Round(reader.GetFloat(12), 1)
            });
        }

        return new AgentLeaderboardInsight
        {
            TenantId = tenantId,
            InstanceId = instanceId,
            TotalAgents = agents.Count,
            Agents = agents
        };
    }

    /// <summary>
    /// Delete all agent metric records for a tenant (used before recompute).
    /// </summary>
    public async Task DeleteAgentMetricsAsync(int tenantId, int? instanceId = null,
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

        cmd.CommandText = $"DELETE FROM wa_agent_metrics {where}";
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        _logger.StepInfo($"[InsightRepo] Deleted {deleted} agent metric records for tenant {tenantId}", "insight");
    }

    // ============================================================
    // wa_rescue_candidates (RI-3.6)
    // ============================================================

    /// <summary>
    /// Get rescue-eligible outcomes (no_response, offered, offer_lost) for a tenant.
    /// Returns conversation_id + outcome_label pairs.
    /// </summary>
    public async Task<List<(string ConversationId, string OutcomeLabel)>> GetRescueEligibleOutcomesAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, outcome_label
            FROM wa_conversation_outcomes
            WHERE tenant_id = @tid
              AND outcome_label IN ('no_response', 'offered', 'offer_lost')";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1)));
        return results;
    }

    /// <summary>
    /// Batch upsert rescue candidate records (INSERT ON CONFLICT UPDATE).
    /// estimated_value left NULL (deferred to RI-4+).
    /// </summary>
    public async Task UpsertRescueCandidatesAsync(List<RescueCandidateRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        const int batchSize = 50;
        for (var offset = 0; offset < records.Count; offset += batchSize)
        {
            var batch = records.Skip(offset).Take(batchSize).ToList();
            await UpsertRescueCandidatesBatchAsync(batch, ct);
        }
    }

    private async Task UpsertRescueCandidatesBatchAsync(List<RescueCandidateRecord> batch, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var sb = new StringBuilder();
        sb.AppendLine(@"INSERT INTO wa_rescue_candidates
            (tenant_id, conversation_id, instance_id, outcome_label,
             last_message_at, last_message_from, days_since, rescue_status, computed_at)
            VALUES ");

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.AppendLine($"(@tid{i}, @cid{i}, @iid{i}, @lbl{i}, @lma{i}, @lmf{i}, @ds{i}, 'pending', NOW())");
            var r = batch[i];
            cmd.Parameters.AddWithValue($"tid{i}", r.TenantId);
            cmd.Parameters.AddWithValue($"cid{i}", r.ConversationId);
            cmd.Parameters.AddWithValue($"iid{i}", (object?)r.InstanceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"lbl{i}", r.OutcomeLabel);
            cmd.Parameters.AddWithValue($"lma{i}", r.LastMessageAt.HasValue
                ? (object)r.LastMessageAt.Value.ToUniversalTime()
                : DBNull.Value);
            cmd.Parameters.AddWithValue($"lmf{i}", (object?)r.LastMessageFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"ds{i}", r.DaysSince);
        }

        sb.AppendLine(@"ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET
            instance_id = EXCLUDED.instance_id,
            outcome_label = EXCLUDED.outcome_label,
            last_message_at = EXCLUDED.last_message_at,
            last_message_from = EXCLUDED.last_message_from,
            days_since = EXCLUDED.days_since,
            computed_at = NOW()");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get rescue candidates for a tenant, ordered by rescue_priority_score DESC.
    /// Optionally filtered by instance_id.
    /// </summary>
    public async Task<RescueInsight> GetRescueCandidatesAsync(int tenantId, int? instanceId = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var where = "WHERE rc.tenant_id = @tid AND rc.rescue_status = 'pending'";
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (instanceId.HasValue)
        {
            where += " AND rc.instance_id = @iid";
            cmd.Parameters.AddWithValue("iid", instanceId.Value);
        }

        cmd.CommandText = $@"
            SELECT rc.conversation_id, rc.instance_id, rc.outcome_label,
                   rc.last_message_at, rc.last_message_from, rc.days_since, rc.rescue_status
            FROM wa_rescue_candidates rc
            {where}
            ORDER BY rc.days_since ASC, rc.outcome_label ASC";

        var candidates = new List<RescueCandidateEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var daysSince = reader.GetInt32(5);
            var outcomeLabel = reader.GetString(2);

            candidates.Add(new RescueCandidateEntry
            {
                ConversationId = reader.GetString(0),
                InstanceId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                OutcomeLabel = outcomeLabel,
                LastMessageAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                LastMessageFrom = reader.IsDBNull(4) ? null : reader.GetString(4),
                DaysSince = daysSince,
                RescuePriorityScore = InsightRescueScoring.CalculatePriorityScore(daysSince, outcomeLabel),
                RescueStatus = reader.GetString(6)
            });
        }

        // Sort by priority score DESC (computed in-memory)
        candidates.Sort((a, b) => b.RescuePriorityScore.CompareTo(a.RescuePriorityScore));

        return new RescueInsight
        {
            TenantId = tenantId,
            InstanceId = instanceId,
            TotalCandidates = candidates.Count,
            Candidates = candidates
        };
    }

    /// <summary>
    /// Delete all rescue candidate records for a tenant (used before recompute).
    /// </summary>
    public async Task DeleteRescueCandidatesAsync(int tenantId, int? instanceId = null,
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

        cmd.CommandText = $"DELETE FROM wa_rescue_candidates {where}";
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        _logger.StepInfo($"[InsightRepo] Deleted {deleted} rescue candidate records for tenant {tenantId}", "insight");
    }
}
