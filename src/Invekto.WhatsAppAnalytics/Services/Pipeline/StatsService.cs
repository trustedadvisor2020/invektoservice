using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;

namespace Invekto.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 3: Generate aggregated metadata from wa_messages + wa_conversations -> insert wa_metadata.
/// C# port of Python 03_stats.py.
/// </summary>
public sealed class StatsService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public StatsService(AnalyticsRepository repo, AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _repo = repo;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Run Stage 3: aggregate stats and insert metadata.
    /// </summary>
    public async Task RunAsync(int analysisId, int tenantId, string? configJson,
        Func<StageProgress, Task> onProgress, CancellationToken ct)
    {
        _logger.SystemInfo($"[StatsService] Starting Stage 3 for analysis {analysisId}");

        await onProgress(new StageProgress
        {
            Stage = "stats", StageNumber = 3, TotalStages = 3,
            Percent = 10, Message = "Computing message statistics..."
        });

        await using var conn = await _db.OpenConnectionAsync(ct);

        // 1. Message-level stats
        var messageStats = await GetMessageStatsAsync(conn, analysisId, tenantId, ct);

        await onProgress(new StageProgress
        {
            Stage = "stats", StageNumber = 3, TotalStages = 3,
            Percent = 30, Message = "Computing conversation statistics..."
        });

        // 2. Conversation-level stats
        var conversationStats = await GetConversationStatsAsync(conn, analysisId, tenantId, ct);

        await onProgress(new StageProgress
        {
            Stage = "stats", StageNumber = 3, TotalStages = 3,
            Percent = 50, Message = "Computing agent performance..."
        });

        // 3. Agent performance
        var agentStats = await GetAgentStatsAsync(conn, analysisId, tenantId, ct);

        await onProgress(new StageProgress
        {
            Stage = "stats", StageNumber = 3, TotalStages = 3,
            Percent = 70, Message = "Computing temporal patterns..."
        });

        // 4. Temporal patterns
        var temporalStats = await GetTemporalStatsAsync(conn, analysisId, tenantId, ct);

        await onProgress(new StageProgress
        {
            Stage = "stats", StageNumber = 3, TotalStages = 3,
            Percent = 90, Message = "Building metadata JSON..."
        });

        // 5. Build metadata JSON
        var tenantInfo = ParseTenantInfo(configJson);
        var metadata = new Dictionary<string, object?>
        {
            ["tenant"] = tenantInfo,
            ["generated_at"] = DateTime.UtcNow.ToString("o"),
            ["messages"] = messageStats,
            ["conversations"] = conversationStats,
            ["date_range"] = await GetDateRangeAsync(conn, analysisId, tenantId, ct),
            ["agents"] = agentStats,
            ["temporal"] = temporalStats
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = false });
        await _repo.InsertMetadataAsync(analysisId, tenantId, metadataJson, ct);

        _logger.SystemInfo($"[StatsService] Stage 3 complete for analysis {analysisId}");
    }

    private async Task<Dictionary<string, object>> GetMessageStatsAsync(NpgsqlConnection conn, int analysisId, int tenantId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                COUNT(*) as total,
                COUNT(*) FILTER (WHERE sender_type = 'ME') as agent_messages,
                COUNT(*) FILTER (WHERE sender_type = 'CUSTOMER') as customer_messages,
                COALESCE(AVG(LENGTH(message_text)) FILTER (WHERE sender_type = 'ME'), 0) as avg_agent_len,
                COALESCE(AVG(LENGTH(message_text)) FILTER (WHERE sender_type = 'CUSTOMER'), 0) as avg_customer_len
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var total = reader.GetInt64(0);
        var agentMsgs = reader.GetInt64(1);
        var customerMsgs = reader.GetInt64(2);

        return new Dictionary<string, object>
        {
            ["total"] = total,
            ["agent_messages"] = agentMsgs,
            ["customer_messages"] = customerMsgs,
            ["agent_customer_ratio"] = customerMsgs > 0 ? Math.Round((double)agentMsgs / customerMsgs, 2) : 0,
            ["avg_message_length_agent"] = Math.Round(reader.GetDouble(3), 1),
            ["avg_message_length_customer"] = Math.Round(reader.GetDouble(4), 1)
        };
    }

    private async Task<Dictionary<string, object>> GetConversationStatsAsync(NpgsqlConnection conn, int analysisId, int tenantId, CancellationToken ct)
    {
        // Read basic stats first and close reader before opening second query
        long total; double avgMessages, avgDuration, avgFirstResponse;
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COUNT(*) as total,
                    COALESCE(AVG(message_count), 0) as avg_messages,
                    COALESCE(AVG(duration_minutes), 0) as avg_duration,
                    COALESCE(AVG(first_response_minutes), 0) as avg_first_response
                FROM wa_conversations
                WHERE analysis_id = @aid AND tenant_id = @tid";
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            total = reader.GetInt64(0);
            avgMessages = reader.GetDouble(1);
            avgDuration = reader.GetDouble(2);
            avgFirstResponse = reader.GetDouble(3);
        } // reader disposed here

        // Outcome distribution (safe: previous reader is closed)
        var outcomes = new Dictionary<string, long>();
        {
            await using var outcomeCmd = conn.CreateCommand();
            outcomeCmd.CommandText = @"
                SELECT outcome, COUNT(*) as count
                FROM wa_conversations
                WHERE analysis_id = @aid AND tenant_id = @tid
                GROUP BY outcome ORDER BY count DESC";
            outcomeCmd.Parameters.AddWithValue("aid", analysisId);
            outcomeCmd.Parameters.AddWithValue("tid", tenantId);

            await using var outcomeReader = await outcomeCmd.ExecuteReaderAsync(ct);
            while (await outcomeReader.ReadAsync(ct))
            {
                outcomes[outcomeReader.GetString(0)] = outcomeReader.GetInt64(1);
            }
        }

        return new Dictionary<string, object>
        {
            ["total"] = total,
            ["avg_messages_per_conversation"] = Math.Round(avgMessages, 1),
            ["avg_duration_minutes"] = Math.Round(avgDuration, 1),
            ["avg_first_response_minutes"] = Math.Round(avgFirstResponse, 1),
            ["outcome_distribution"] = outcomes
        };
    }

    private async Task<List<Dictionary<string, object>>> GetAgentStatsAsync(NpgsqlConnection conn, int analysisId, int tenantId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                primary_agent,
                COUNT(*) as conversation_count,
                SUM(agent_message_count) as message_count,
                COALESCE(AVG(first_response_minutes), 0) as avg_response_time,
                SUM(CASE WHEN outcome = 'sale' THEN 1 ELSE 0 END) as sales
            FROM wa_conversations
            WHERE analysis_id = @aid AND tenant_id = @tid AND primary_agent != ''
            GROUP BY primary_agent
            ORDER BY conversation_count DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var agents = new List<Dictionary<string, object>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var convCount = reader.GetInt64(1);
            var sales = reader.GetInt64(4);
            agents.Add(new Dictionary<string, object>
            {
                ["name"] = reader.GetString(0),
                ["conversation_count"] = convCount,
                ["message_count"] = reader.GetInt64(2),
                ["avg_response_time_minutes"] = Math.Round(reader.GetDouble(3), 1),
                ["sales"] = sales,
                ["sale_rate"] = convCount > 0 ? Math.Round((double)sales / convCount * 100, 1) : 0
            });
        }

        return agents;
    }

    private async Task<Dictionary<string, object>> GetTemporalStatsAsync(NpgsqlConnection conn, int analysisId, int tenantId, CancellationToken ct)
    {
        // Hourly distribution
        var hourlyCmd = conn.CreateCommand();
        hourlyCmd.CommandText = @"
            SELECT EXTRACT(HOUR FROM timestamp)::int as hour, COUNT(*) as count
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid
            GROUP BY hour ORDER BY hour";
        hourlyCmd.Parameters.AddWithValue("aid", analysisId);
        hourlyCmd.Parameters.AddWithValue("tid", tenantId);

        var hourly = new List<Dictionary<string, object>>();
        await using (var reader = await hourlyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                hourly.Add(new Dictionary<string, object>
                {
                    ["hour"] = reader.GetInt32(0),
                    ["message_count"] = reader.GetInt64(1)
                });
        }

        // Daily distribution (day of week)
        var dailyCmd = conn.CreateCommand();
        dailyCmd.CommandText = @"
            SELECT TO_CHAR(timestamp, 'Day') as day_name, COUNT(*) as count
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid
            GROUP BY day_name, EXTRACT(DOW FROM timestamp)
            ORDER BY EXTRACT(DOW FROM timestamp)";
        dailyCmd.Parameters.AddWithValue("aid", analysisId);
        dailyCmd.Parameters.AddWithValue("tid", tenantId);

        var daily = new Dictionary<string, long>();
        await using (var reader = await dailyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                daily[reader.GetString(0).Trim()] = reader.GetInt64(1);
        }

        // Monthly distribution
        var monthlyCmd = conn.CreateCommand();
        monthlyCmd.CommandText = @"
            SELECT TO_CHAR(timestamp, 'YYYY-MM') as month, COUNT(*) as count
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid
            GROUP BY month ORDER BY month";
        monthlyCmd.Parameters.AddWithValue("aid", analysisId);
        monthlyCmd.Parameters.AddWithValue("tid", tenantId);

        var monthly = new Dictionary<string, long>();
        await using (var reader = await monthlyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                monthly[reader.GetString(0)] = reader.GetInt64(1);
        }

        return new Dictionary<string, object>
        {
            ["peak_hours"] = hourly.OrderByDescending(h => (long)h["message_count"]).Take(5).ToList(),
            ["daily_distribution"] = daily,
            ["monthly_volume"] = monthly
        };
    }

    private async Task<Dictionary<string, object>> GetDateRangeAsync(NpgsqlConnection conn, int analysisId, int tenantId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT MIN(timestamp), MAX(timestamp)
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var start = reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(0);
        var end = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);

        return new Dictionary<string, object>
        {
            ["start"] = start.ToString("yyyy-MM-dd"),
            ["end"] = end.ToString("yyyy-MM-dd"),
            ["total_days"] = (int)(end - start).TotalDays
        };
    }

    private Dictionary<string, object> ParseTenantInfo(string? configJson)
    {
        if (string.IsNullOrEmpty(configJson)) return new Dictionary<string, object> { ["id"] = 0, ["name"] = "unknown" };
        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson);
            return config ?? new Dictionary<string, object> { ["id"] = 0, ["name"] = "unknown" };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[StatsService] Failed to parse config JSON for tenant info: {ex.Message}");
            return new Dictionary<string, object> { ["id"] = 0, ["name"] = "unknown" };
        }
    }
}
