using System.Text;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for all WhatsApp Analytics DB operations.
/// Phase A: wa_analyses, wa_messages, wa_conversations, wa_metadata.
/// </summary>
public sealed class AnalyticsRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;
    private const int BatchSize = 50;

    public AnalyticsRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Health
    // ============================================================

    public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[AnalyticsRepository] Health check failed: {ex.Message}");
            return false;
        }
    }

    // ============================================================
    // wa_analyses CRUD
    // ============================================================

    public async Task<int> CreateAnalysisAsync(int tenantId, string sourceFileName, string? configJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_analyses (tenant_id, source_file_name, config_json, status, started_at)
            VALUES (@tid, @src, @cfg::jsonb, 'pending', NOW())
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("src", sourceFileName);
        cmd.Parameters.AddWithValue("cfg", (object?)configJson ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (int)result!;
    }

    public async Task<AnalysisJob?> GetAnalysisAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, status, source_file_name, config_json::text,
                   total_messages, total_conversations, stage_progress::text,
                   error_message, started_at, completed_at, created_at, updated_at
            FROM wa_analyses
            WHERE id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadAnalysisJob(reader);
    }

    public async Task<(List<AnalysisJob> Items, int Total)> ListAnalysesAsync(int tenantId, int page, int limit, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM wa_analyses WHERE tenant_id = @tid";
        countCmd.Parameters.AddWithValue("tid", tenantId);
        var total = (int)(long)(await countCmd.ExecuteScalarAsync(ct))!;

        // List
        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = @"
            SELECT id, tenant_id, status, source_file_name, config_json::text,
                   total_messages, total_conversations, stage_progress::text,
                   error_message, started_at, completed_at, created_at, updated_at
            FROM wa_analyses
            WHERE tenant_id = @tid
            ORDER BY created_at DESC
            LIMIT @lim OFFSET @off";
        listCmd.Parameters.AddWithValue("tid", tenantId);
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", (page - 1) * limit);

        var items = new List<AnalysisJob>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadAnalysisJob(reader));

        return (items, total);
    }

    public async Task UpdateAnalysisStatusAsync(int analysisId, string status, string? stageProgressJson = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = @st, stage_progress = @sp::jsonb, updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("sp", (object?)stageProgressJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAnalysisTotalsAsync(int analysisId, int totalMessages, int totalConversations, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET total_messages = @tm, total_conversations = @tc, updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tm", totalMessages);
        cmd.Parameters.AddWithValue("tc", totalConversations);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CompleteAnalysisAsync(int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'completed', completed_at = NOW(), updated_at = NOW(),
                stage_progress = NULL
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task FailAnalysisAsync(int analysisId, string errorMessage, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'error', error_message = @err, completed_at = NOW(), updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("err", errorMessage);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAnalysisAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_analyses WHERE id = @aid AND tenant_id = @tid RETURNING id";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    /// <summary>
    /// Atomically claim analyses stuck in non-terminal state for restart recovery.
    /// Uses UPDATE ... RETURNING with FOR UPDATE SKIP LOCKED to prevent double-processing.
    /// Stale timeout (30 min) ensures actively-processing analyses are not re-claimed.
    /// </summary>
    public async Task<List<AnalysisJob>> ClaimPendingAnalysesAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'recovering', updated_at = NOW()
            WHERE id IN (
                SELECT id FROM wa_analyses
                WHERE status NOT IN ('completed', 'error')
                AND updated_at < NOW() - INTERVAL '30 minutes'
                ORDER BY created_at ASC
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, tenant_id, status, source_file_name, config_json::text,
                      total_messages, total_conversations, stage_progress::text,
                      error_message, started_at, completed_at, created_at, updated_at";

        var items = new List<AnalysisJob>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadAnalysisJob(reader));

        return items;
    }

    // ============================================================
    // wa_messages batch insert
    // ============================================================

    public async Task<int> BatchInsertMessagesAsync(int analysisId, int tenantId, List<CleanedMessage> messages, CancellationToken ct = default)
    {
        if (messages.Count == 0) return 0;

        var inserted = 0;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < messages.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, messages.Count - i);
            var batch = messages.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(
                "INSERT INTO wa_messages (analysis_id, tenant_id, conversation_id, business_phone, timestamp, message_text, sender_type, agent_name, message_hash) VALUES ");

            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @bp{j}, @ts{j}, @mt{j}, @st{j}, @an{j}, @mh{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"bp{j}", batch[j].BusinessPhone);
                cmd.Parameters.AddWithValue($"ts{j}", batch[j].Timestamp);
                cmd.Parameters.AddWithValue($"mt{j}", batch[j].MessageText);
                cmd.Parameters.AddWithValue($"st{j}", batch[j].SenderType);
                cmd.Parameters.AddWithValue($"an{j}", batch[j].AgentName);
                cmd.Parameters.AddWithValue($"mh{j}", batch[j].MessageHash);
            }

            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();

            inserted += await cmd.ExecuteNonQueryAsync(ct);
        }

        return inserted;
    }

    // ============================================================
    // wa_conversations batch insert
    // ============================================================

    public async Task<int> BatchInsertConversationsAsync(int analysisId, int tenantId, List<Conversation> conversations, CancellationToken ct = default)
    {
        if (conversations.Count == 0) return 0;

        var inserted = 0;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < conversations.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, conversations.Count - i);
            var batch = conversations.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(@"
                INSERT INTO wa_conversations (analysis_id, tenant_id, conversation_id, business_phone,
                    start_time, end_time, duration_minutes, message_count, customer_message_count,
                    agent_message_count, primary_agent, first_response_minutes, outcome,
                    product_codes, first_customer_msg, last_agent_msg) VALUES ");

            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @bp{j}, @st{j}, @et{j}, @dm{j}, @mc{j}, @cmc{j}, @amc{j}, @pa{j}, @frm{j}, @oc{j}, @pc{j}, @fcm{j}, @lam{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"bp{j}", batch[j].BusinessPhone);
                cmd.Parameters.AddWithValue($"st{j}", batch[j].StartTime);
                cmd.Parameters.AddWithValue($"et{j}", batch[j].EndTime);
                cmd.Parameters.AddWithValue($"dm{j}", batch[j].DurationMinutes);
                cmd.Parameters.AddWithValue($"mc{j}", batch[j].MessageCount);
                cmd.Parameters.AddWithValue($"cmc{j}", batch[j].CustomerMessageCount);
                cmd.Parameters.AddWithValue($"amc{j}", batch[j].AgentMessageCount);
                cmd.Parameters.AddWithValue($"pa{j}", batch[j].PrimaryAgent);
                cmd.Parameters.AddWithValue($"frm{j}", (float)batch[j].FirstResponseMinutes);
                cmd.Parameters.AddWithValue($"oc{j}", batch[j].Outcome);
                cmd.Parameters.AddWithValue($"pc{j}", batch[j].ProductCodes);
                cmd.Parameters.AddWithValue($"fcm{j}", batch[j].FirstCustomerMsg);
                cmd.Parameters.AddWithValue($"lam{j}", batch[j].LastAgentMsg);
            }

            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();

            inserted += await cmd.ExecuteNonQueryAsync(ct);
        }

        return inserted;
    }

    // ============================================================
    // wa_metadata
    // ============================================================

    public async Task InsertMetadataAsync(int analysisId, int tenantId, string metadataJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_metadata (analysis_id, tenant_id, metadata_json)
            VALUES (@aid, @tid, @mj::jsonb)
            ON CONFLICT (analysis_id) DO UPDATE
            SET metadata_json = @mj::jsonb";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("mj", metadataJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> GetMetadataAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT metadata_json::text FROM wa_metadata
            WHERE analysis_id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    // ============================================================
    // Helper
    // ============================================================

    private static AnalysisJob ReadAnalysisJob(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TenantId = reader.GetInt32(1),
        Status = reader.GetString(2),
        SourceFileName = reader.IsDBNull(3) ? "" : reader.GetString(3),
        ConfigJson = reader.IsDBNull(4) ? null : reader.GetString(4),
        TotalMessages = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        TotalConversations = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        StageProgress = reader.IsDBNull(7) ? null : reader.GetString(7),
        ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
        StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
        CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
        CreatedAt = reader.GetDateTime(11),
        UpdatedAt = reader.GetDateTime(12)
    };
}
