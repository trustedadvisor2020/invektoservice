using Chatinbox.Shared.Data;
using Chatinbox.Shared.DTOs.Analytics;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Backend.Services;

/// <summary>
/// PKT-3: Analytics repository for automation metrics (daily_metrics, daily_intent_metrics)
/// and WA analytics (direct query on wa_* tables, same PostgreSQL instance).
/// Thread-safe singleton, uses PostgresConnectionFactory for connection pooling.
/// </summary>
public class AnalyticsRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public AnalyticsRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // TENANT LIST
    // ============================================================

    /// <summary>
    /// List tenants with metrics availability info.
    /// </summary>
    public virtual async Task<List<TenantMetricsInfoDto>> GetTenantsWithMetricsAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                tr.tenant_id,
                tr.tenant_name,
                EXISTS(SELECT 1 FROM daily_metrics dm WHERE dm.tenant_id = tr.tenant_id LIMIT 1) AS has_automation,
                EXISTS(SELECT 1 FROM wa_analyses wa WHERE wa.tenant_id = tr.tenant_id AND wa.status = 'completed' LIMIT 1) AS has_wa,
                (SELECT MAX(metric_date)::text FROM daily_metrics dm WHERE dm.tenant_id = tr.tenant_id) AS latest_date
            FROM tenant_registry tr
            ORDER BY tr.tenant_id";

        var result = new List<TenantMetricsInfoDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new TenantMetricsInfoDto
            {
                TenantId = reader.GetInt32(0),
                TenantName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                HasAutomationData = reader.GetBoolean(2),
                HasWaData = reader.GetBoolean(3),
                LatestMetricDate = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return result;
    }

    // ============================================================
    // AUTOMATION METRICS (from daily_metrics + daily_intent_metrics)
    // ============================================================

    /// <summary>
    /// Get automation summary for tenant in date range (aggregated from daily_metrics).
    /// </summary>
    public virtual async Task<AutomationSummaryDto> GetAutomationSummaryAsync(
        int tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var summary = new AutomationSummaryDto
        {
            TenantId = tenantId,
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd")
        };

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Aggregate daily_metrics across date range
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    COALESCE(SUM(total_replies), 0)::int,
                    COALESCE(SUM(deflected_count), 0)::int,
                    COALESCE(SUM(handoff_count), 0)::int,
                    COALESCE(SUM(faq_count), 0)::int,
                    COALESCE(SUM(intent_count), 0)::int,
                    COALESCE(SUM(menu_count), 0)::int,
                    COALESCE(SUM(off_hours_count), 0)::int,
                    COALESCE(SUM(welcome_count), 0)::int,
                    CASE WHEN SUM(total_replies) > 0
                        THEN SUM(avg_processing_time_ms * total_replies) / SUM(total_replies)
                        ELSE 0 END,
                    CASE WHEN SUM(total_replies) > 0
                        THEN SUM(COALESCE(avg_confidence, 0) * total_replies) / SUM(total_replies)
                        ELSE 0 END,
                    COALESCE(SUM(active_sessions), 0)::int,
                    COALESCE(SUM(completed_sessions), 0)::int,
                    COALESCE(SUM(handed_off_sessions), 0)::int,
                    COALESCE(SUM(expired_sessions), 0)::int
                FROM daily_metrics
                WHERE tenant_id = @tid
                  AND metric_date >= @from_date
                  AND metric_date <= @to_date";
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("from_date", from.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("to_date", to.ToDateTime(TimeOnly.MinValue));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                summary.TotalReplies = reader.GetInt32(0);
                summary.DeflectedCount = reader.GetInt32(1);
                summary.HandoffCount = reader.GetInt32(2);
                var faqCount = reader.GetInt32(3);
                var intentCount = reader.GetInt32(4);
                var menuCount = reader.GetInt32(5);
                var offHoursCount = reader.GetInt32(6);
                var welcomeCount = reader.GetInt32(7);
                summary.AvgProcessingTimeMs = reader.GetDouble(8);
                summary.AvgConfidence = reader.GetDouble(9);

                summary.DeflectionRate = summary.TotalReplies > 0
                    ? Math.Round((double)summary.DeflectedCount / summary.TotalReplies * 100, 1)
                    : 0;
                summary.HandoffRate = summary.TotalReplies > 0
                    ? Math.Round((double)summary.HandoffCount / summary.TotalReplies * 100, 1)
                    : 0;

                summary.ReplyTypeBreakdown = new Dictionary<string, int>
                {
                    ["faq"] = faqCount,
                    ["intent"] = intentCount,
                    ["menu"] = menuCount,
                    ["off_hours"] = offHoursCount,
                    ["welcome"] = welcomeCount,
                    ["handoff"] = summary.HandoffCount
                };

                summary.SessionStatusBreakdown = new Dictionary<string, int>
                {
                    ["active"] = reader.GetInt32(10),
                    ["completed"] = reader.GetInt32(11),
                    ["handed_off"] = reader.GetInt32(12),
                    ["expired"] = reader.GetInt32(13)
                };
            }
        }

        return summary;
    }

    /// <summary>
    /// Get daily automation trends for charting.
    /// </summary>
    public virtual async Task<List<DailyMetricDto>> GetAutomationTrendsAsync(
        int tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                metric_date::text,
                total_replies,
                deflected_count,
                handoff_count,
                COALESCE(avg_processing_time_ms, 0)
            FROM daily_metrics
            WHERE tenant_id = @tid
              AND metric_date >= @from_date
              AND metric_date <= @to_date
            ORDER BY metric_date ASC";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("from_date", from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("to_date", to.ToDateTime(TimeOnly.MinValue));

        var result = new List<DailyMetricDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var total = reader.GetInt32(1);
            var deflected = reader.GetInt32(2);
            result.Add(new DailyMetricDto
            {
                Date = reader.GetString(0),
                TotalReplies = total,
                DeflectedCount = deflected,
                HandoffCount = reader.GetInt32(3),
                DeflectionRate = total > 0 ? Math.Round((double)deflected / total * 100, 1) : 0,
                AvgProcessingTimeMs = reader.GetDouble(4)
            });
        }
        return result;
    }

    /// <summary>
    /// Get intent performance breakdown.
    /// </summary>
    public virtual async Task<List<IntentMetricDto>> GetIntentMetricsAsync(
        int tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                intent,
                SUM(total_count)::int AS total,
                SUM(handoff_count)::int AS handoffs,
                CASE WHEN SUM(total_count) > 0
                    THEN SUM(COALESCE(avg_confidence, 0) * total_count) / SUM(total_count)
                    ELSE 0 END AS avg_conf,
                CASE WHEN SUM(total_count) > 0
                    THEN SUM(COALESCE(avg_processing_time_ms, 0) * total_count) / SUM(total_count)
                    ELSE 0 END AS avg_proc
            FROM daily_intent_metrics
            WHERE tenant_id = @tid
              AND metric_date >= @from_date
              AND metric_date <= @to_date
            GROUP BY intent
            ORDER BY SUM(total_count) DESC";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("from_date", from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("to_date", to.ToDateTime(TimeOnly.MinValue));

        var result = new List<IntentMetricDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var total = reader.GetInt32(1);
            var handoffs = reader.GetInt32(2);
            result.Add(new IntentMetricDto
            {
                Intent = reader.GetString(0),
                TotalCount = total,
                HandoffCount = handoffs,
                HandoffRate = total > 0 ? Math.Round((double)handoffs / total * 100, 1) : 0,
                AvgConfidence = Math.Round(reader.GetDouble(3), 3),
                AvgProcessingTimeMs = Math.Round(reader.GetDouble(4), 1)
            });
        }
        return result;
    }

    // ============================================================
    // WA ANALYTICS (direct query on wa_* tables)
    // ============================================================

    /// <summary>
    /// List completed WA analyses for a tenant.
    /// </summary>
    public virtual async Task<List<WaAnalysisInfoDto>> GetWaAnalysesAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, source_file_name, status,
                   COALESCE(total_messages, 0), COALESCE(total_conversations, 0),
                   completed_at::text
            FROM wa_analyses
            WHERE tenant_id = @tid
            ORDER BY created_at DESC
            LIMIT 50";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<WaAnalysisInfoDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WaAnalysisInfoDto
            {
                AnalysisId = reader.GetInt32(0),
                SourceFileName = reader.IsDBNull(1) ? null : reader.GetString(1),
                Status = reader.GetString(2),
                TotalMessages = reader.GetInt32(3),
                TotalConversations = reader.GetInt32(4),
                CompletedAt = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return result;
    }

    /// <summary>
    /// Get WA analysis summary (conversation outcomes, avg response time).
    /// </summary>
    public virtual async Task<WaSummaryDto> GetWaSummaryAsync(
        int tenantId, int analysisId, CancellationToken ct = default)
    {
        var summary = new WaSummaryDto { AnalysisId = analysisId };

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Get totals from wa_analyses
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(total_messages, 0), COALESCE(total_conversations, 0)
                FROM wa_analyses
                WHERE id = @aid AND tenant_id = @tid";
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                summary.TotalMessages = reader.GetInt32(0);
                summary.TotalConversations = reader.GetInt32(1);
            }
        }

        // Get outcome breakdown from wa_conversations
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT outcome, COUNT(*) AS cnt
                FROM wa_conversations
                WHERE analysis_id = @aid AND tenant_id = @tid AND outcome IS NOT NULL
                GROUP BY outcome
                ORDER BY cnt DESC";
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                summary.OutcomeBreakdown[reader.GetString(0)] = (int)reader.GetInt64(1);
            }
        }

        // Get avg first response time and duration
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    COALESCE(AVG(first_response_minutes), 0),
                    COALESCE(AVG(duration_minutes), 0)
                FROM wa_conversations
                WHERE analysis_id = @aid AND tenant_id = @tid";
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                summary.AvgFirstResponseMinutes = Math.Round(reader.GetDouble(0), 1);
                summary.AvgDurationMinutes = Math.Round(reader.GetDouble(1), 1);
            }
        }

        return summary;
    }

    /// <summary>
    /// Get WA agent performance comparison.
    /// </summary>
    public virtual async Task<List<WaAgentMetricDto>> GetWaAgentMetricsAsync(
        int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                primary_agent,
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE outcome = 'sale') AS sales,
                COUNT(*) FILTER (WHERE outcome = 'offered') AS offered,
                COUNT(*) FILTER (WHERE outcome = 'no_sale') AS no_sales,
                COALESCE(AVG(first_response_minutes), 0) AS avg_frt
            FROM wa_conversations
            WHERE analysis_id = @aid
              AND tenant_id = @tid
              AND primary_agent IS NOT NULL
            GROUP BY primary_agent
            ORDER BY COUNT(*) DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<WaAgentMetricDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var total = (int)reader.GetInt64(1);
            var sales = (int)reader.GetInt64(2);
            var offered = (int)reader.GetInt64(3);
            var noSales = (int)reader.GetInt64(4);
            var convBase = sales + offered + noSales;
            result.Add(new WaAgentMetricDto
            {
                AgentName = reader.GetString(0),
                TotalConversations = total,
                SaleCount = sales,
                OfferedCount = offered,
                NoSaleCount = noSales,
                ConversionRate = convBase > 0 ? Math.Round((double)sales / convBase * 100, 1) : 0,
                AvgFirstResponseMinutes = Math.Round(reader.GetDouble(5), 1)
            });
        }
        return result;
    }

    /// <summary>
    /// Get WA daily conversation trends.
    /// </summary>
    public virtual async Task<List<WaTrendDto>> GetWaTrendsAsync(
        int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                start_time::date::text AS day,
                SUM(message_count) AS msgs,
                COUNT(*) AS convos,
                COUNT(*) FILTER (WHERE outcome = 'sale') AS sales,
                COUNT(*) FILTER (WHERE outcome = 'offered') AS offered
            FROM wa_conversations
            WHERE analysis_id = @aid
              AND tenant_id = @tid
              AND start_time IS NOT NULL
            GROUP BY start_time::date
            ORDER BY start_time::date ASC";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<WaTrendDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WaTrendDto
            {
                Date = reader.GetString(0),
                MessageCount = (int)reader.GetInt64(1),
                ConversationCount = (int)reader.GetInt64(2),
                SaleCount = (int)reader.GetInt64(3),
                OfferedCount = (int)reader.GetInt64(4)
            });
        }
        return result;
    }

    // ============================================================
    // GR-3.18: CAMPAIGN STATS (from outbound_campaigns)
    // ============================================================

    /// <summary>
    /// Get campaign stats for a tenant (from outbound_campaigns table).
    /// Returns top 20 campaigns ordered by created_at DESC.
    /// </summary>
    public virtual async Task<List<CampaignStatDto>> GetCampaignStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                c.id, c.name, c.trigger_type, c.status,
                c.stats_json,
                t.template_name,
                c.created_at
            FROM outbound_campaigns c
            LEFT JOIN outbound_templates t ON t.id = c.template_id
            WHERE c.tenant_id = @tid
            ORDER BY c.created_at DESC
            LIMIT 20";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<CampaignStatDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var statsJson = reader.IsDBNull(4) ? "{}" : reader.GetString(4);
            result.Add(new CampaignStatDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                TriggerType = reader.GetString(2),
                Status = reader.GetString(3),
                StatsJson = statsJson,
                TemplateName = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm")
            });
        }
        return result;
    }

    // ============================================================
    // AGGREGATION (called by MetricsAggregationService)
    // ============================================================

    /// <summary>
    /// Get all tenant IDs that have auto_reply_log data.
    /// </summary>
    public virtual async Task<List<int>> GetTenantIdsWithAutoReplyDataAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT tenant_id FROM auto_reply_log ORDER BY tenant_id";

        var result = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetInt32(0));
        }
        return result;
    }

    /// <summary>
    /// Aggregate auto_reply_log into daily_metrics for a specific tenant and date.
    /// UPSERT: ON CONFLICT DO UPDATE (idempotent).
    /// </summary>
    public virtual async Task UpsertDailyMetricsAsync(int tenantId, DateOnly date, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO daily_metrics (
                tenant_id, metric_date,
                total_replies, deflected_count, handoff_count,
                faq_count, intent_count, menu_count, off_hours_count, welcome_count,
                avg_processing_time_ms, avg_confidence,
                active_sessions, completed_sessions, handed_off_sessions, expired_sessions
            )
            SELECT
                @tid,
                @metric_date,
                COUNT(*),
                COUNT(*) FILTER (WHERE reply_type IN ('menu','faq','intent','off_hours','welcome')),
                COUNT(*) FILTER (WHERE reply_type = 'handoff'),
                COUNT(*) FILTER (WHERE reply_type = 'faq'),
                COUNT(*) FILTER (WHERE reply_type = 'intent'),
                COUNT(*) FILTER (WHERE reply_type = 'menu'),
                COUNT(*) FILTER (WHERE reply_type = 'off_hours'),
                COUNT(*) FILTER (WHERE reply_type = 'welcome'),
                AVG(processing_time_ms),
                AVG(confidence),
                (SELECT COUNT(*) FROM chat_sessions WHERE tenant_id = @tid AND status = 'active' AND started_at::date = @metric_date),
                (SELECT COUNT(*) FROM chat_sessions WHERE tenant_id = @tid AND status = 'completed' AND started_at::date = @metric_date),
                (SELECT COUNT(*) FROM chat_sessions WHERE tenant_id = @tid AND status = 'handed_off' AND started_at::date = @metric_date),
                (SELECT COUNT(*) FROM chat_sessions WHERE tenant_id = @tid AND status = 'expired' AND started_at::date = @metric_date)
            FROM auto_reply_log
            WHERE tenant_id = @tid
              AND created_at::date = @metric_date
            ON CONFLICT (tenant_id, metric_date) DO UPDATE SET
                total_replies = EXCLUDED.total_replies,
                deflected_count = EXCLUDED.deflected_count,
                handoff_count = EXCLUDED.handoff_count,
                faq_count = EXCLUDED.faq_count,
                intent_count = EXCLUDED.intent_count,
                menu_count = EXCLUDED.menu_count,
                off_hours_count = EXCLUDED.off_hours_count,
                welcome_count = EXCLUDED.welcome_count,
                avg_processing_time_ms = EXCLUDED.avg_processing_time_ms,
                avg_confidence = EXCLUDED.avg_confidence,
                active_sessions = EXCLUDED.active_sessions,
                completed_sessions = EXCLUDED.completed_sessions,
                handed_off_sessions = EXCLUDED.handed_off_sessions,
                expired_sessions = EXCLUDED.expired_sessions,
                updated_at = NOW()";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("metric_date", date.ToDateTime(TimeOnly.MinValue));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Aggregate auto_reply_log into daily_intent_metrics for a specific tenant and date.
    /// UPSERT per intent: ON CONFLICT DO UPDATE (idempotent).
    /// </summary>
    public virtual async Task UpsertDailyIntentMetricsAsync(int tenantId, DateOnly date, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO daily_intent_metrics (
                tenant_id, metric_date, intent,
                total_count, handoff_count,
                avg_confidence, avg_processing_time_ms
            )
            SELECT
                @tid,
                @metric_date,
                COALESCE(intent, 'unknown'),
                COUNT(*),
                COUNT(*) FILTER (WHERE reply_type = 'handoff'),
                AVG(confidence),
                AVG(processing_time_ms)
            FROM auto_reply_log
            WHERE tenant_id = @tid
              AND created_at::date = @metric_date
              AND reply_type IN ('intent', 'handoff')
            GROUP BY COALESCE(intent, 'unknown')
            ON CONFLICT (tenant_id, metric_date, intent) DO UPDATE SET
                total_count = EXCLUDED.total_count,
                handoff_count = EXCLUDED.handoff_count,
                avg_confidence = EXCLUDED.avg_confidence,
                avg_processing_time_ms = EXCLUDED.avg_processing_time_ms,
                updated_at = NOW()";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("metric_date", date.ToDateTime(TimeOnly.MinValue));

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
