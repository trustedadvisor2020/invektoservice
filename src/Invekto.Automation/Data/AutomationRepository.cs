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
    // chatbot_flows (multi-flow: N flows per tenant, multiple active)
    // ============================================================

    /// <summary>
    /// Get the ACTIVE flow config for a tenant (backward compat: picks first active flow).
    /// Returns null if no active flow exists.
    /// </summary>
    public async Task<(JsonDocument? FlowConfig, bool IsActive, int FlowId)> GetFlowAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT flow_config, is_active, flow_id FROM chatbot_flows WHERE tenant_id = @tid AND is_active = true LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (null, false, 0);

        var json = reader.GetString(0);
        var isActive = reader.GetBoolean(1);
        var flowId = reader.GetInt32(2);
        return (JsonDocument.Parse(json), isActive, flowId);
    }

    /// <summary>
    /// Check if tenant has any instance configuration records.
    /// No records = old behavior (single flow routing).
    /// </summary>
    public async Task<bool> HasInstanceRecordsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM tenant_instances WHERE tenant_id = @tid)";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    /// <summary>
    /// Get flow config by instance assignment (multi-flow routing).
    /// Returns the active flow that this instance is assigned to.
    /// </summary>
    public async Task<(JsonDocument? FlowConfig, bool IsActive, int FlowId)> GetFlowByInstanceAsync(
        int tenantId, string instanceId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT cf.flow_config::text, cf.is_active, cf.flow_id
            FROM tenant_instances ti
            JOIN chatbot_flows cf ON cf.flow_id = ti.flow_id AND cf.tenant_id = ti.tenant_id
            WHERE ti.tenant_id = @tid AND ti.instance_id = @iid
              AND ti.is_enabled = true AND cf.is_active = true
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("iid", instanceId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (null, false, 0);

        var json = reader.GetString(0);
        var isActive = reader.GetBoolean(1);
        var flowId = reader.GetInt32(2);
        return (JsonDocument.Parse(json), isActive, flowId);
    }

    /// <summary>
    /// List all flows for a tenant with summary info (for FlowListPage).
    /// </summary>
    public async Task<List<FlowSummary>> ListFlowsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cf.flow_id, cf.flow_name, cf.is_active, cf.is_default,
                   cf.flow_config->>'version' AS config_version,
                   COALESCE(jsonb_array_length(CASE WHEN cf.flow_config ? 'nodes' THEN cf.flow_config->'nodes' ELSE NULL END), 0) AS node_count,
                   COALESCE(jsonb_array_length(CASE WHEN cf.flow_config ? 'edges' THEN cf.flow_config->'edges' ELSE NULL END), 0) AS edge_count,
                   cf.created_at, cf.updated_at,
                   CASE WHEN cf.flow_config->>'version' = '2' THEN cf.flow_config::text ELSE NULL END AS flow_config_raw,
                   cf.wizard_status,
                   (SELECT COALESCE(json_agg(json_build_object(
                       'instanceId', ti.instance_id,
                       'instanceName', ti.instance_name,
                       'instanceType', ti.instance_type
                   )), '[]'::json)
                   FROM tenant_instances ti
                   WHERE ti.flow_id = cf.flow_id AND ti.tenant_id = cf.tenant_id
                     AND ti.is_enabled = true) AS assigned_instances
            FROM chatbot_flows cf
            WHERE cf.tenant_id = @tid
            ORDER BY cf.is_active DESC, cf.updated_at DESC";
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
                FlowConfigJson = reader.IsDBNull(9) ? null : reader.GetString(9),
                WizardStatus = reader.IsDBNull(10) ? null : reader.GetString(10),
                AssignedInstancesJson = reader.IsDBNull(11) ? null : reader.GetString(11)
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
            SELECT flow_id, flow_name, flow_config, is_active, is_default, created_at, updated_at,
                   wizard_history, wizard_status, current_version
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
            UpdatedAt = reader.GetDateTime(6),
            WizardHistoryJson = reader.IsDBNull(7) ? null : reader.GetString(7),
            WizardStatus = reader.IsDBNull(8) ? null : reader.GetString(8),
            CurrentVersion = reader.GetInt32(9)
        };
    }

    /// <summary>
    /// Get all active v2 flows with schedule_trigger node (cross-tenant by design).
    /// CronSchedulerService is an IHostedService that evaluates cron expressions for ALL tenants.
    /// Cross-tenant query is architecturally intentional — same pattern as other scheduler services.
    /// </summary>
    public async Task<List<ScheduleFlowInfo>> GetActiveScheduleFlowsAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT flow_id, tenant_id, flow_config::text
            FROM chatbot_flows
            WHERE is_active = true
              AND flow_config->>'version' = '2'
              AND EXISTS (
                  SELECT 1
                  FROM jsonb_array_elements(flow_config->'nodes') AS node
                  WHERE node->>'type' = 'schedule_trigger'
              )";

        var result = new List<ScheduleFlowInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ScheduleFlowInfo
            {
                FlowId = reader.GetInt32(0),
                TenantId = reader.GetInt32(1),
                FlowConfigJson = reader.GetString(2)
            });
        }
        return result;
    }

    /// <summary>
    /// Create a new flow for a tenant. Returns the new flow_id.
    /// New flows start as inactive (draft).
    /// </summary>
    public async Task<int> CreateFlowAsync(int tenantId, string flowName, string flowConfigJson,
        string? wizardStatus = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO chatbot_flows (tenant_id, flow_name, flow_config, is_active, is_default, wizard_status)
            VALUES (@tid, @name, @cfg::jsonb, false, false, @ws)
            RETURNING flow_id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", flowName);
        cmd.Parameters.AddWithValue("cfg", flowConfigJson);
        cmd.Parameters.AddWithValue("ws", (object?)wizardStatus ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    /// <summary>
    /// Count active flows for a tenant. Used for max_flows quota check.
    /// </summary>
    public async Task<int> CountActiveFlowsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chatbot_flows WHERE tenant_id = @tid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long c ? (int)c : Convert.ToInt32(result);
    }

    /// <summary>
    /// Update an existing flow's config and name.
    /// </summary>
    public async Task<bool> UpdateFlowByIdAsync(int tenantId, int flowId, string? flowName, string flowConfigJson,
        string? wizardHistoryJson = null, string? wizardStatus = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE chatbot_flows SET
                flow_config = @cfg::jsonb,
                flow_name = COALESCE(@name, flow_name),
                wizard_history = COALESCE(@wh::jsonb, wizard_history),
                wizard_status = COALESCE(@ws, wizard_status)
            WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("cfg", flowConfigJson);
        cmd.Parameters.AddWithValue("name", (object?)flowName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("wh", (object?)wizardHistoryJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ws", (object?)wizardStatus ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>
    /// Update only wizard_history for a flow (used during wizard conversation).
    /// </summary>
    public async Task<bool> UpdateWizardHistoryAsync(int tenantId, int flowId, string wizardHistoryJson,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE chatbot_flows SET wizard_history = @wh::jsonb
            WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("wh", wizardHistoryJson);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // flow_versions (auto-version on save, rollback)
    // ============================================================

    /// <summary>
    /// Create a new version snapshot after a flow save.
    /// Increments version_number per flow, updates chatbot_flows.current_version.
    /// Returns the new version number.
    /// </summary>
    public async Task<int> CreateFlowVersionAsync(int tenantId, int flowId, string flowConfigJson,
        string? createdBy = "user", CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Get next version number (tenant_id for isolation)
        await using var cmdNext = new NpgsqlCommand(
            "SELECT COALESCE(MAX(version_number), 0) + 1 FROM flow_versions WHERE flow_id = @fid AND tenant_id = @tid", conn, tx);
        cmdNext.Parameters.AddWithValue("fid", flowId);
        cmdNext.Parameters.AddWithValue("tid", tenantId);
        var nextVersion = Convert.ToInt32(await cmdNext.ExecuteScalarAsync(ct));

        // Insert version snapshot
        await using var cmdInsert = new NpgsqlCommand(@"
            INSERT INTO flow_versions (flow_id, tenant_id, version_number, flow_config, created_by)
            VALUES (@fid, @tid, @vn, @cfg::jsonb, @cb)", conn, tx);
        cmdInsert.Parameters.AddWithValue("fid", flowId);
        cmdInsert.Parameters.AddWithValue("tid", tenantId);
        cmdInsert.Parameters.AddWithValue("vn", nextVersion);
        cmdInsert.Parameters.AddWithValue("cfg", flowConfigJson);
        cmdInsert.Parameters.AddWithValue("cb", (object?)createdBy ?? DBNull.Value);
        await cmdInsert.ExecuteNonQueryAsync(ct);

        // Update current_version on chatbot_flows
        await using var cmdUpdate = new NpgsqlCommand(
            "UPDATE chatbot_flows SET current_version = @vn WHERE flow_id = @fid AND tenant_id = @tid", conn, tx);
        cmdUpdate.Parameters.AddWithValue("vn", nextVersion);
        cmdUpdate.Parameters.AddWithValue("fid", flowId);
        cmdUpdate.Parameters.AddWithValue("tid", tenantId);
        await cmdUpdate.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return nextVersion;
    }

    /// <summary>
    /// List all versions for a flow (summary only, no config).
    /// </summary>
    public async Task<List<FlowVersionSummary>> GetFlowVersionsAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, flow_id, version_number, created_at, created_by
            FROM flow_versions
            WHERE flow_id = @fid AND tenant_id = @tid
            ORDER BY version_number DESC", conn);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<FlowVersionSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new FlowVersionSummary
            {
                Id = reader.GetInt32(0),
                FlowId = reader.GetInt32(1),
                VersionNumber = reader.GetInt32(2),
                CreatedAt = reader.GetDateTime(3),
                CreatedBy = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return result;
    }

    /// <summary>
    /// Get a specific version's full config.
    /// </summary>
    public async Task<FlowVersionDetail?> GetFlowVersionAsync(int tenantId, int flowId, int versionNumber, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, flow_id, version_number, created_at, created_by, flow_config::text
            FROM flow_versions
            WHERE flow_id = @fid AND tenant_id = @tid AND version_number = @vn", conn);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("vn", versionNumber);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new FlowVersionDetail
        {
            Id = reader.GetInt32(0),
            FlowId = reader.GetInt32(1),
            VersionNumber = reader.GetInt32(2),
            CreatedAt = reader.GetDateTime(3),
            CreatedBy = reader.IsDBNull(4) ? null : reader.GetString(4),
            FlowConfigJson = reader.GetString(5)
        };
    }

    /// <summary>
    /// Rollback: restore a specific version's config to chatbot_flows and create a new version.
    /// Returns the new version number.
    /// </summary>
    public async Task<int> RollbackFlowVersionAsync(int tenantId, int flowId, int versionNumber, CancellationToken ct = default)
    {
        // Get the target version's config
        var target = await GetFlowVersionAsync(tenantId, flowId, versionNumber, ct);
        if (target == null)
            return -1;

        // Update chatbot_flows with the old config
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE chatbot_flows SET flow_config = @cfg::jsonb WHERE flow_id = @fid AND tenant_id = @tid", conn);
        cmd.Parameters.AddWithValue("cfg", target.FlowConfigJson);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);

        // Create a new version snapshot (marked as rollback)
        return await CreateFlowVersionAsync(tenantId, flowId, target.FlowConfigJson, "rollback", ct);
    }

    /// <summary>
    /// Delete a flow. Active flows cannot be deleted (caller must deactivate first).
    /// </summary>
    public async Task<(bool Deleted, bool WasActive)> DeleteFlowByIdAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Check if flow is active
        await using var checkCmd = conn.CreateCommand();
        checkCmd.Transaction = tx;
        checkCmd.CommandText = "SELECT is_active FROM chatbot_flows WHERE tenant_id = @tid AND flow_id = @fid";
        checkCmd.Parameters.AddWithValue("tid", tenantId);
        checkCmd.Parameters.AddWithValue("fid", flowId);
        var activeResult = await checkCmd.ExecuteScalarAsync(ct);
        if (activeResult == null)
        {
            await tx.RollbackAsync(ct);
            return (false, false); // not found
        }

        var wasActive = (bool)activeResult;
        if (wasActive)
        {
            await tx.RollbackAsync(ct);
            return (false, true); // cannot delete active flow
        }

        // Delete execution logs first (FK has no CASCADE)
        await using var logCmd = conn.CreateCommand();
        logCmd.Transaction = tx;
        logCmd.CommandText = "DELETE FROM flow_execution_log WHERE tenant_id = @tid AND flow_id = @fid";
        logCmd.Parameters.AddWithValue("tid", tenantId);
        logCmd.Parameters.AddWithValue("fid", flowId);
        await logCmd.ExecuteNonQueryAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM chatbot_flows WHERE tenant_id = @tid AND flow_id = @fid AND is_active = false";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        var deleted = await cmd.ExecuteNonQueryAsync(ct) > 0;

        await tx.CommitAsync(ct);
        return (deleted, false);
    }

    /// <summary>
    /// Activate a flow: set target flow to is_active=true.
    /// Multi-flow: does NOT deactivate other flows (multiple flows can be active).
    /// </summary>
    public async Task<bool> ActivateFlowAsync(int tenantId, int flowId, CancellationToken ct = default)
    {
        // Multi-flow: activate only the target flow (no longer deactivates others)
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE chatbot_flows SET is_active = true WHERE tenant_id = @tid AND flow_id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
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

    /// <summary>
    /// Sync flow-instance mapping: clear old assignments for this flow, assign new ones.
    /// Called when saving a flow with trigger_start instance selection.
    /// </summary>
    public async Task SyncFlowInstanceMappingAsync(
        int tenantId, int flowId, List<string> instanceIds, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Clear old assignments for this flow
            await using (var clearCmd = new NpgsqlCommand(
                "UPDATE tenant_instances SET flow_id = NULL WHERE tenant_id = @tid AND flow_id = @fid", conn, tx))
            {
                clearCmd.Parameters.AddWithValue("tid", tenantId);
                clearCmd.Parameters.AddWithValue("fid", flowId);
                await clearCmd.ExecuteNonQueryAsync(ct);
            }

            // Assign new instances to this flow
            if (instanceIds.Count > 0)
            {
                await using var assignCmd = new NpgsqlCommand(@"
                    UPDATE tenant_instances
                    SET flow_id = @fid
                    WHERE tenant_id = @tid AND instance_id = ANY(@ids)
                      AND is_enabled = true", conn, tx);
                assignCmd.Parameters.AddWithValue("tid", tenantId);
                assignCmd.Parameters.AddWithValue("fid", flowId);
                assignCmd.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Varchar, instanceIds.ToArray());
                await assignCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
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

    public async Task<ChatSession?> GetActiveSessionAsync(int tenantId, string chatId, CancellationToken ct = default)
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

    public async Task<int> CreateSessionAsync(int tenantId, string chatId, string? phone, string currentNode, CancellationToken ct = default)
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

    public async Task LogAutoReplyAsync(int tenantId, string chatId, string? phone, string? messageText,
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
    // vip_flags (PKT-6A: GR-3.2 B2B/VIP Lead Detection)
    // ============================================================

    /// <summary>
    /// Upsert a VIP flag. Returns true if newly inserted (first detection), false if updated.
    /// ON CONFLICT: updates last_seen and takes the higher detection_score.
    /// </summary>
    public async Task<bool> UpsertVipFlagAsync(
        int tenantId, string phone, string vipType, decimal detectionScore,
        string[] matchedSignals, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO vip_flags (tenant_id, phone, vip_type, detection_score, matched_signals)
            VALUES (@tid, @phone, @type, @score, @signals)
            ON CONFLICT (tenant_id, phone) DO UPDATE SET
                last_seen = NOW(),
                detection_score = GREATEST(vip_flags.detection_score, EXCLUDED.detection_score),
                matched_signals = EXCLUDED.matched_signals
            RETURNING (xmax = 0) AS is_insert";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);
        cmd.Parameters.AddWithValue("type", vipType);
        cmd.Parameters.AddWithValue("score", detectionScore);
        cmd.Parameters.Add(new NpgsqlParameter("signals", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = matchedSignals });

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool isInsert && isInsert;
    }

    /// <summary>
    /// Check if a phone is flagged as VIP for a tenant.
    /// </summary>
    public async Task<bool> IsVipPhoneAsync(int tenantId, string phone, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM vip_flags WHERE tenant_id = @tid AND phone = @phone";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
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

    // ============================================================
    // KVKK health tenant check (GR-2.6)
    // ============================================================

    public async Task<(string? settingsJson, string? sector)> GetTenantHealthInfoAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT settings_json::text, sector FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var settingsJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            var sector = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (settingsJson, sector);
        }
        return (null, null);
    }

    // ============================================================
    // HFM-2: Lead preferred_locale (multi-language fallback)
    // ============================================================

    /// <summary>
    /// Read lead.preferred_locale for a given phone. Returns null when the lead row does
    /// not exist or preferred_locale is NULL (fallback chain kicks in).
    /// </summary>
    public async Task<string?> GetLeadPreferredLocaleAsync(int tenantId, string phone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT preferred_locale FROM leads WHERE tenant_id = @tid AND phone = @phone LIMIT 1";
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("phone", phone);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == DBNull.Value ? null : result as string;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[INV-AT-063] GetLeadPreferredLocaleAsync failed tenant={tenantId} phone={phone}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Upsert lead.preferred_locale. Insert creates a minimal lead row (source='whatsapp'
    /// default) if none exists. Existing preferred_locale is PRESERVED (sticky): COALESCE
    /// ensures the first detected locale wins — subsequent mis-detections (e.g. a user
    /// temporarily typing in English) never overwrite the canonical value.
    ///
    /// Returns true when a row was inserted or preferred_locale transitioned from NULL
    /// to the provided value. Returns false on DB failure (graceful degradation, logged).
    /// </summary>
    public async Task<bool> UpsertLeadPreferredLocaleAsync(
        int tenantId, string phone, string detectedLocale, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(detectedLocale))
            return false;

        // Guard against accidental region suffixes breaking the CHECK constraint.
        var normalized = detectedLocale.Trim();
        if (normalized.Length < 2 || normalized.Length > 5)
            return false;

        const string sql = @"
            INSERT INTO leads (tenant_id, phone, source, preferred_locale)
            VALUES (@tid, @phone, 'whatsapp', @loc)
            ON CONFLICT (tenant_id, phone) DO UPDATE
                SET preferred_locale = COALESCE(leads.preferred_locale, EXCLUDED.preferred_locale),
                    updated_at = NOW()
            RETURNING (xmax = 0) AS was_insert";

        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("phone", phone);
            cmd.Parameters.AddWithValue("loc", normalized);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is bool b && b;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Unique violation: expected when no unique constraint covers (tenant_id, phone).
            // Fall back to explicit UPDATE — preserves sticky value semantics.
            return await FallbackUpdatePreferredLocaleAsync(tenantId, phone, normalized, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P10")
        {
            // No ON CONFLICT target: leads table may not have (tenant_id, phone) unique idx on
            // some older deployments. Fall back to UPDATE-only path.
            return await FallbackUpdatePreferredLocaleAsync(tenantId, phone, normalized, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[INV-AT-063] UpsertLeadPreferredLocaleAsync failed tenant={tenantId} phone={phone}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> FallbackUpdatePreferredLocaleAsync(
        int tenantId, string phone, string normalized, CancellationToken ct)
    {
        const string sql = @"
            UPDATE leads SET preferred_locale = @loc, updated_at = NOW()
            WHERE tenant_id = @tid AND phone = @phone AND preferred_locale IS NULL";

        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("phone", phone);
            cmd.Parameters.AddWithValue("loc", normalized);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[INV-AT-063] FallbackUpdatePreferredLocale failed tenant={tenantId} phone={phone}: {ex.Message}");
            return false;
        }
    }

    // ============================================================
    // PKT-6B1: Return Deflections (GR-3.8 + GR-3.17)
    // ============================================================

    /// <summary>Insert a new return deflection record.</summary>
    public async Task<int> InsertReturnDeflectionAsync(
        int tenantId, string? conversationId, string phone,
        string reasonCategory, string? reasonText,
        string actionTaken, string? couponCode, decimal? couponValue,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO return_deflections
                (tenant_id, conversation_id, customer_phone, reason_category, reason_text,
                 action_taken, coupon_code, coupon_value)
            VALUES (@tid, @cid, @phone, @reason, @reasonText, @action, @coupon, @couponVal)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", (object?)conversationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", phone);
        cmd.Parameters.AddWithValue("reason", reasonCategory);
        cmd.Parameters.AddWithValue("reasonText", (object?)reasonText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("action", actionTaken);
        cmd.Parameters.AddWithValue("coupon", (object?)couponCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("couponVal", (object?)couponValue ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (int)(result ?? 0);
    }

    /// <summary>Update deflection result (was_deflected, revenue saved).</summary>
    public async Task<bool> UpdateReturnDeflectionResultAsync(
        int tenantId, int deflectionId, bool wasDeflected, decimal? revenueSaved,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE return_deflections
            SET was_deflected = @deflected,
                deflection_revenue = COALESCE(@revenue, deflection_revenue),
                updated_at = NOW()
            WHERE id = @id AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", deflectionId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("deflected", wasDeflected);
        cmd.Parameters.AddWithValue("revenue", (object?)revenueSaved ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>Get deflection stats for a tenant (GR-3.17 success rate).</summary>
    public async Task<(int Total, int Deflected, decimal RevenueSaved,
        Dictionary<string, int> ByReason, Dictionary<string, int> ByAction)>
        GetReturnDeflectionStatsAsync(int tenantId, DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE was_deflected = TRUE) AS deflected,
                COALESCE(SUM(deflection_revenue) FILTER (WHERE was_deflected = TRUE), 0) AS revenue_saved,
                reason_category,
                action_taken
            FROM return_deflections
            WHERE tenant_id = @tid
              AND (@from IS NULL OR created_at >= @from)
              AND (@to IS NULL OR created_at <= @to)
            GROUP BY GROUPING SETS ((), (reason_category), (action_taken))";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("from", (object?)from ?? DBNull.Value);
        cmd.Parameters.AddWithValue("to", (object?)to ?? DBNull.Value);

        int total = 0, deflected = 0;
        decimal revenueSaved = 0;
        var byReason = new Dictionary<string, int>();
        var byAction = new Dictionary<string, int>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var reasonCat = reader.IsDBNull(3) ? null : reader.GetString(3);
            var actionTk = reader.IsDBNull(4) ? null : reader.GetString(4);

            if (reasonCat == null && actionTk == null)
            {
                total = reader.GetInt32(0);
                deflected = reader.GetInt32(1);
                revenueSaved = reader.GetDecimal(2);
            }
            else if (reasonCat != null && actionTk == null)
            {
                byReason[reasonCat] = reader.GetInt32(0);
            }
            else if (reasonCat == null && actionTk != null)
            {
                byAction[actionTk] = reader.GetInt32(0);
            }
        }

        return (total, deflected, revenueSaved, byReason, byAction);
    }

    /// <summary>Get deflections pending follow-up for a specific tenant.</summary>
    public async Task<List<(int Id, int TenantId, string Phone, string ActionTaken)>>
        GetPendingFollowUpsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, customer_phone, action_taken
            FROM return_deflections
            WHERE tenant_id = @tid
              AND follow_up_sent = FALSE
              AND follow_up_at IS NOT NULL
              AND follow_up_at <= NOW()
            ORDER BY follow_up_at
            LIMIT 50
            FOR UPDATE SKIP LOCKED";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<(int, int, string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
        }
        return result;
    }

    /// <summary>Mark follow-up as sent for a deflection.</summary>
    public async Task MarkFollowUpSentAsync(int tenantId, int deflectionId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE return_deflections
            SET follow_up_sent = TRUE, updated_at = NOW()
            WHERE id = @id AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", deflectionId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<(int TotalFlowCount, int ActiveFlowCount)>
        GetOnboardingStatsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*)::int AS total_count,
                COUNT(*) FILTER (WHERE is_active = true)::int AS active_count
            FROM chatbot_flows
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (0, 0);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    // ============================================================
    // flow_execution_log (Flow Execution Log)
    // ============================================================

    /// <summary>Create a new execution log entry. Returns the log ID.</summary>
    public async Task<long> CreateExecutionLogAsync(
        int tenantId, int flowId, string? chatId, string? phone,
        string? instanceId, string? triggerMessage, string nodeTraceJson,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO flow_execution_log
                (tenant_id, flow_id, chat_id, phone, instance_id, trigger_message, node_trace)
            VALUES (@tid, @fid, @cid, @phone, @iid, @msg, @trace::jsonb)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);
        cmd.Parameters.AddWithValue("cid", (object?)chatId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iid", (object?)instanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("msg", (object?)triggerMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("trace", nodeTraceJson);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)(result ?? 0L);
    }

    /// <summary>Append node trace entries and update status atomically.</summary>
    public async Task UpdateExecutionLogAsync(
        long logId, int tenantId, string appendTraceJson, string status,
        string? variablesFinalJson = null, string? errorDetail = null,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE flow_execution_log
            SET node_trace = node_trace || @append::jsonb,
                status = @status,
                variables_final = CASE WHEN @vars IS NOT NULL THEN @vars::jsonb ELSE variables_final END,
                error_detail = COALESCE(@err, error_detail),
                completed_at = CASE WHEN @status IN ('completed','error','handed_off') THEN NOW() ELSE completed_at END
            WHERE id = @id AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", logId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("append", appendTraceJson);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("vars", (object?)variablesFinalJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("err", (object?)errorDetail ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>List execution logs for a flow (paginated, newest first).</summary>
    public async Task<(List<FlowExecutionLogSummary> Items, int Total)>
        ListExecutionLogsAsync(int tenantId, int flowId, int limit, int offset,
        CancellationToken ct = default)
    {
        const string countSql = @"
            SELECT COUNT(*)::int FROM flow_execution_log
            WHERE tenant_id = @tid AND flow_id = @fid";

        const string listSql = @"
            SELECT id, flow_id, chat_id, phone, trigger_message,
                   started_at, completed_at, status, jsonb_array_length(node_trace) AS node_count
            FROM flow_execution_log
            WHERE tenant_id = @tid AND flow_id = @fid
            ORDER BY started_at DESC
            LIMIT @lim OFFSET @off";

        await using var conn = await _db.OpenConnectionAsync(ct);

        await using var countCmd = new NpgsqlCommand(countSql, conn);
        countCmd.Parameters.AddWithValue("tid", tenantId);
        countCmd.Parameters.AddWithValue("fid", flowId);
        var total = (int)(await countCmd.ExecuteScalarAsync(ct) ?? 0);

        var items = new List<FlowExecutionLogSummary>();
        await using var listCmd = new NpgsqlCommand(listSql, conn);
        listCmd.Parameters.AddWithValue("tid", tenantId);
        listCmd.Parameters.AddWithValue("fid", flowId);
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", offset);

        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new FlowExecutionLogSummary
            {
                Id = reader.GetInt64(0),
                FlowId = reader.GetInt32(1),
                ChatId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
                TriggerMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                StartedAt = reader.GetDateTime(5),
                CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                Status = reader.GetString(7),
                NodeCount = reader.GetInt32(8)
            });
        }

        return (items, total);
    }

    /// <summary>Get a single execution log with full node_trace.</summary>
    public async Task<FlowExecutionLogDetail?> GetExecutionLogAsync(
        int tenantId, int flowId, long logId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, flow_id, chat_id, phone, instance_id, trigger_message,
                   started_at, completed_at, status,
                   jsonb_array_length(node_trace) AS node_count,
                   node_trace::text, variables_final::text, error_detail
            FROM flow_execution_log
            WHERE id = @id AND tenant_id = @tid AND flow_id = @fid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", logId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", flowId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new FlowExecutionLogDetail
        {
            Id = reader.GetInt64(0),
            FlowId = reader.GetInt32(1),
            ChatId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
            InstanceId = reader.IsDBNull(4) ? null : reader.GetString(4),
            TriggerMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
            StartedAt = reader.GetDateTime(6),
            CompletedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            Status = reader.GetString(8),
            NodeCount = reader.GetInt32(9),
            NodeTraceJson = reader.IsDBNull(10) ? "[]" : reader.GetString(10),
            VariablesFinalJson = reader.IsDBNull(11) ? null : reader.GetString(11),
            ErrorDetail = reader.IsDBNull(12) ? null : reader.GetString(12)
        };
    }

    // ============================================================
    // Flow Monitor (cross-flow execution listing)
    // ============================================================

    /// <summary>List execution logs across all flows for a tenant (with filters). JOIN chatbot_flows for flow_name.</summary>
    public async Task<(List<MonitorExecutionSummary> Items, int Total)>
        ListMonitorExecutionsAsync(int tenantId, int? flowId, string? status,
        DateTime? dateFrom, DateTime? dateTo, string? phone,
        int limit, int offset, CancellationToken ct = default)
    {
        var where = new System.Text.StringBuilder("WHERE e.tenant_id = @tid");
        if (flowId.HasValue) where.Append(" AND e.flow_id = @fid");
        if (!string.IsNullOrEmpty(status)) where.Append(" AND e.status = @status");
        if (dateFrom.HasValue) where.Append(" AND e.started_at >= @dfrom");
        if (dateTo.HasValue) where.Append(" AND e.started_at <= @dto");
        if (!string.IsNullOrEmpty(phone)) where.Append(" AND e.phone LIKE @phone");

        var countSql = $"SELECT COUNT(*)::int FROM flow_execution_log e {where}";
        var listSql = $@"
            SELECT e.id, e.flow_id, f.flow_name, e.chat_id, e.phone, e.trigger_message,
                   e.started_at, e.completed_at, e.status, jsonb_array_length(e.node_trace) AS node_count
            FROM flow_execution_log e
            JOIN chatbot_flows f ON f.flow_id = e.flow_id AND f.tenant_id = e.tenant_id
            {where}
            ORDER BY e.started_at DESC
            LIMIT @lim OFFSET @off";

        await using var conn = await _db.OpenConnectionAsync(ct);

        void AddParams(NpgsqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            if (flowId.HasValue) cmd.Parameters.AddWithValue("fid", flowId.Value);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("status", status);
            if (dateFrom.HasValue) cmd.Parameters.AddWithValue("dfrom", dateFrom.Value);
            if (dateTo.HasValue) cmd.Parameters.AddWithValue("dto", dateTo.Value);
            if (!string.IsNullOrEmpty(phone)) cmd.Parameters.AddWithValue("phone", $"%{phone}%");
        }

        await using var countCmd = new NpgsqlCommand(countSql, conn);
        AddParams(countCmd);
        var total = (int)(await countCmd.ExecuteScalarAsync(ct) ?? 0);

        var items = new List<MonitorExecutionSummary>();
        await using var listCmd = new NpgsqlCommand(listSql, conn);
        AddParams(listCmd);
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", offset);

        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new MonitorExecutionSummary
            {
                Id = reader.GetInt64(0),
                FlowId = reader.GetInt32(1),
                FlowName = reader.GetString(2),
                ChatId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
                TriggerMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                StartedAt = reader.GetDateTime(6),
                CompletedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                Status = reader.GetString(8),
                NodeCount = reader.GetInt32(9)
            });
        }

        return (items, total);
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
    public string ChatId { get; init; } = "";
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
    public string? WizardStatus { get; init; }
    public string? AssignedInstancesJson { get; init; }
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
    public string? WizardHistoryJson { get; init; }
    public string? WizardStatus { get; init; }
    public int CurrentVersion { get; init; }
}

public sealed class ScheduleFlowInfo
{
    public int FlowId { get; init; }
    public int TenantId { get; init; }
    public required string FlowConfigJson { get; init; }
}

public class FlowExecutionLogSummary
{
    public long Id { get; init; }
    public int FlowId { get; init; }
    public string? ChatId { get; init; }
    public string? Phone { get; init; }
    public string? TriggerMessage { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required string Status { get; init; }
    public int NodeCount { get; init; }
}

public sealed class FlowExecutionLogDetail : FlowExecutionLogSummary
{
    public string? InstanceId { get; init; }
    public required string NodeTraceJson { get; init; }
    public string? VariablesFinalJson { get; init; }
    public string? ErrorDetail { get; init; }
}

public class MonitorExecutionSummary
{
    public long Id { get; init; }
    public int FlowId { get; init; }
    public required string FlowName { get; init; }
    public string? ChatId { get; init; }
    public string? Phone { get; init; }
    public string? TriggerMessage { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required string Status { get; init; }
    public int NodeCount { get; init; }
}

public class FlowVersionSummary
{
    public int Id { get; init; }
    public int FlowId { get; init; }
    public int VersionNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed class FlowVersionDetail : FlowVersionSummary
{
    public required string FlowConfigJson { get; init; }
}
