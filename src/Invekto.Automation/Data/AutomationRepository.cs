using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.Automation.Data;

/// <summary>
/// PostgreSQL repository for Automation service tables.
/// Thread-safe, register as singleton. Uses PostgresConnectionFactory for pooled connections.
/// </summary>
public sealed class AutomationRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public AutomationRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // chatbot_flows (multi-flow: N flows per tenant, max 1 active)
    // ============================================================

    /// <summary>
    /// Get the ACTIVE flow config for a tenant (v1 engine backward compat).
    /// Returns null if no active flow exists.
    /// </summary>
    public async Task<(JsonDocument? FlowConfig, bool IsActive)> GetFlowAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT flow_config, is_active FROM chatbot_flows WHERE tenant_id = @tid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (null, false);

        var json = reader.GetString(0);
        var isActive = reader.GetBoolean(1);
        return (JsonDocument.Parse(json), isActive);
    }

    /// <summary>
    /// List all flows for a tenant with summary info (for FlowListPage).
    /// </summary>
    public async Task<List<FlowSummary>> ListFlowsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT flow_id, flow_name, is_active, is_default,
                   flow_config->>'version' AS config_version,
                   COALESCE(jsonb_array_length(CASE WHEN flow_config ? 'nodes' THEN flow_config->'nodes' ELSE NULL END), 0) AS node_count,
                   COALESCE(jsonb_array_length(CASE WHEN flow_config ? 'edges' THEN flow_config->'edges' ELSE NULL END), 0) AS edge_count,
                   created_at, updated_at,
                   CASE WHEN flow_config->>'version' = '2' THEN flow_config::text ELSE NULL END AS flow_config_raw
            FROM chatbot_flows
            WHERE tenant_id = @tid
            ORDER BY is_active DESC, updated_at DESC";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<FlowSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new FlowSummary
            {
                FlowId = reader.GetInt32(0),
                FlowName = reader.GetString(1),
                IsActive = reader.GetBoolean(2),
                IsDefault = reader.GetBoolean(3),
                ConfigVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                NodeCount = reader.GetInt32(5),
                EdgeCount = reader.GetInt32(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8),
                FlowConfigJson = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return result;
    }

    /// <summary>
    /// Get a single flow by ID (for flow editor load).
    /// </summary>
    public async Task<FlowDetail?> GetFlowByIdAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT flow_id, flow_name, flow_config, is_active, is_default, created_at, updated_at
            FROM chatbot_flows
            WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new FlowDetail
        {
            FlowId = reader.GetInt32(0),
            TenantId = tenantId,
            FlowName = reader.GetString(1),
            FlowConfigJson = reader.GetString(2),
            IsActive = reader.GetBoolean(3),
            IsDefault = reader.GetBoolean(4),
            CreatedAt = reader.GetDateTime(5),
            UpdatedAt = reader.GetDateTime(6)
        };
    }

    /// <summary>
    /// Create a new flow for a tenant. Returns the new flow_id.
    /// New flows start as inactive (draft).
    /// </summary>
    public async Task<int> CreateFlowAsync(int tenantId, string flowName, string flowConfigJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO chatbot_flows (tenant_id, flow_name, flow_config, is_active, is_default)
            VALUES (@tid, @name, @cfg::jsonb, false, false)
            RETURNING flow_id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", flowName);
        cmd.Parameters.AddWithValue("cfg", flowConfigJson);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    /// <summary>
    /// Update an existing flow's config and name.
    /// </summary>
    public async Task<bool> UpdateFlowByIdAsync(int tenantId, int flowId, string? flowName, string flowConfigJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = flowName != null
            ? @"UPDATE chatbot_flows SET flow_config = @cfg::jsonb, flow_name = @name
                WHERE tenant_id = @tid AND flow_id = @fid"
            : @"UPDATE chatbot_flows SET flow_config = @cfg::jsonb
                WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("cfg", flowConfigJson);
        if (flowName != null)
            cmd.Parameters.AddWithValue("name", flowName);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>
    /// Delete a flow. Active flows cannot be deleted (caller must deactivate first).
    /// </summary>
    public async Task<(bool Deleted, bool WasActive)> DeleteFlowByIdAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Check if flow is active
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT is_active FROM chatbot_flows WHERE tenant_id = @tid AND flow_id = @fid";
        checkCmd.Parameters.AddWithValue("tid", tenantId);
        checkCmd.Parameters.AddWithValue("fid", flowId);
        var activeResult = await checkCmd.ExecuteScalarAsync(ct);
        if (activeResult == null)
            return (false, false); // not found

        var wasActive = (bool)activeResult;
        if (wasActive)
            return (false, true); // cannot delete active flow

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM chatbot_flows WHERE tenant_id = @tid AND flow_id = @fid AND is_active = false";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        return (await cmd.ExecuteNonQueryAsync(ct) > 0, false);
    }

    /// <summary>
    /// Activate a flow: set target flow to is_active=true, deactivate all others for this tenant.
    /// Runs in a single transaction.
    /// </summary>
    public async Task<bool> ActivateFlowAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Deactivate all flows for this tenant
            await using var deactivateCmd = conn.CreateCommand();
            deactivateCmd.Transaction = tx;
            deactivateCmd.CommandText = "UPDATE chatbot_flows SET is_active = false WHERE tenant_id = @tid AND is_active = true";
            deactivateCmd.Parameters.AddWithValue("tid", tenantId);
            await deactivateCmd.ExecuteNonQueryAsync(ct);

            // Activate target flow
            await using var activateCmd = conn.CreateCommand();
            activateCmd.Transaction = tx;
            activateCmd.CommandText = "UPDATE chatbot_flows SET is_active = true WHERE tenant_id = @tid AND flow_id = @fid";
            activateCmd.Parameters.AddWithValue("tid", tenantId);
            activateCmd.Parameters.AddWithValue("fid", flowId);
            var affected = await activateCmd.ExecuteNonQueryAsync(ct);

            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                return false; // flow not found
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Deactivate a flow (set is_active = false).
    /// </summary>
    public async Task<bool> DeactivateFlowAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE chatbot_flows SET is_active = false WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // faq_entries
    // ============================================================

    public async Task<List<FaqEntry>> GetActiveFaqsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, question, answer, keywords, sort_order
            FROM faq_entries
            WHERE tenant_id = @tid AND is_active = true
            ORDER BY sort_order, id";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<FaqEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new FaqEntry
            {
                Id = reader.GetInt32(0),
                Question = reader.GetString(1),
                Answer = reader.GetString(2),
                Keywords = reader.GetFieldValue<string[]>(3),
                SortOrder = reader.GetInt32(4)
            });
        }
        return result;
    }

    public async Task<int> InsertFaqAsync(int tenantId, string question, string answer, string[] keywords, int sortOrder, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO faq_entries (tenant_id, question, answer, keywords, sort_order)
            VALUES (@tid, @q, @a, @kw, @so)
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("q", question);
        cmd.Parameters.AddWithValue("a", answer);
        cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keywords });
        cmd.Parameters.AddWithValue("so", sortOrder);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    public async Task<bool> UpdateFaqAsync(int id, int tenantId, string question, string answer, string[] keywords, bool isActive, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE faq_entries
            SET question = @q, answer = @a, keywords = @kw, is_active = @active
            WHERE id = @id AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("q", question);
        cmd.Parameters.AddWithValue("a", answer);
        cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keywords });
        cmd.Parameters.AddWithValue("active", isActive);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteFaqAsync(int id, int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM faq_entries WHERE id = @id AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tid", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // chat_sessions
    // ============================================================

    public async Task<ChatSession?> GetActiveSessionAsync(int tenantId, int chatId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, phone, current_node, session_data, started_at, last_activity_at, expires_at
            FROM chat_sessions
            WHERE tenant_id = @tid AND chat_id = @cid AND status = 'active' AND expires_at > NOW()";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", chatId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ChatSession
        {
            Id = reader.GetInt32(0),
            TenantId = tenantId,
            ChatId = chatId,
            Phone = reader.IsDBNull(1) ? null : reader.GetString(1),
            CurrentNode = reader.GetString(2),
            SessionData = reader.GetString(3),
            StartedAt = reader.GetDateTime(4),
            LastActivityAt = reader.GetDateTime(5),
            ExpiresAt = reader.GetDateTime(6)
        };
    }

    public async Task<int> CreateSessionAsync(int tenantId, int chatId, string? phone, string currentNode, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Expire any existing active session for this chat
        await using var expireCmd = conn.CreateCommand();
        expireCmd.CommandText = @"
            UPDATE chat_sessions SET status = 'expired'
            WHERE tenant_id = @tid AND chat_id = @cid AND status = 'active'";
        expireCmd.Parameters.AddWithValue("tid", tenantId);
        expireCmd.Parameters.AddWithValue("cid", chatId);
        await expireCmd.ExecuteNonQueryAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO chat_sessions (tenant_id, chat_id, phone, current_node)
            VALUES (@tid, @cid, @phone, @node)
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", chatId);
        cmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("node", currentNode);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    public async Task UpdateSessionAsync(int sessionId, string currentNode, string? sessionData = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE chat_sessions
            SET current_node = @node,
                session_data = COALESCE(@data::jsonb, session_data),
                last_activity_at = NOW(),
                expires_at = NOW() + INTERVAL '30 minutes'
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", sessionId);
        cmd.Parameters.AddWithValue("node", currentNode);
        cmd.Parameters.AddWithValue("data", (object?)sessionData ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EndSessionAsync(int sessionId, string status, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE chat_sessions SET status = @st, last_activity_at = NOW() WHERE id = @id";
        cmd.Parameters.AddWithValue("id", sessionId);
        cmd.Parameters.AddWithValue("st", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ExpireOldSessionsAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE chat_sessions SET status = 'expired' WHERE status = 'active' AND expires_at < NOW()";
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // auto_reply_log
    // ============================================================

    public async Task LogAutoReplyAsync(int tenantId, int chatId, string? phone, string? messageText,
        string? replyText, string replyType, string? intent, double? confidence, int? processingTimeMs,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO auto_reply_log (tenant_id, chat_id, phone, message_text, reply_text, reply_type, intent, confidence, processing_time_ms)
            VALUES (@tid, @cid, @phone, @msg, @reply, @rtype, @intent, @conf, @ptime)";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", chatId);
        cmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("msg", (object?)messageText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reply", (object?)replyText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rtype", replyType);
        cmd.Parameters.AddWithValue("intent", (object?)intent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("conf", confidence.HasValue ? (object)confidence.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("ptime", processingTimeMs.HasValue ? (object)processingTimeMs.Value : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // Working hours (from tenant_registry.settings_json)
    // ============================================================

    public async Task<string?> GetTenantSettingsJsonAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT settings_json FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }
}

// ============================================================
// DTOs for repository results
// ============================================================

public sealed class FaqEntry
{
    public int Id { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required string[] Keywords { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ChatSession
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public int ChatId { get; init; }
    public string? Phone { get; init; }
    public required string CurrentNode { get; init; }
    public required string SessionData { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public sealed class FlowSummary
{
    public int FlowId { get; init; }
    public required string FlowName { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public string? ConfigVersion { get; init; }
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? FlowConfigJson { get; init; }
}

public sealed class FlowDetail
{
    public int FlowId { get; init; }
    public int TenantId { get; init; }
    public required string FlowName { get; init; }
    public required string FlowConfigJson { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
