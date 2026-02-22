using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.Backend.Data;

/// <summary>
/// PostgreSQL repository for tenant_instances table.
/// Thread-safe, register as singleton. Instance filtering + flow routing.
/// </summary>
public sealed class InstanceRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public InstanceRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Check if tenant has any instance records (for backward compatibility gate).
    /// No records = old behavior (single flow, no filtering).
    /// </summary>
    public async Task<bool> HasInstanceRecordsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM tenant_instances WHERE tenant_id = @tid)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    /// <summary>
    /// Get instance status for webhook filtering.
    /// Returns null if instance not found in cache.
    /// </summary>
    public async Task<InstanceStatus?> GetInstanceStatusAsync(
        int tenantId, string instanceId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT instance_name, is_enabled, flow_id
            FROM tenant_instances
            WHERE tenant_id = @tid AND instance_id = @iid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("iid", instanceId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new InstanceStatus
        {
            InstanceName = reader.GetString(0),
            IsEnabled = reader.GetBoolean(1),
            FlowId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
        };
    }

    /// <summary>
    /// Upsert instances fetched from WapCRM API.
    /// Preserves is_enabled and flow_id on conflict — only updates display fields.
    /// </summary>
    public async Task UpsertInstancesAsync(
        int tenantId, List<WapCrmInstanceDto> instances, CancellationToken ct = default)
    {
        if (instances.Count == 0) return;

        const string sql = @"
            INSERT INTO tenant_instances
                (tenant_id, instance_id, instance_name, account, instance_type, fetched_at)
            VALUES
                (@tid, @iid, @name, @account, @itype, NOW())
            ON CONFLICT (tenant_id, instance_id) DO UPDATE SET
                instance_name = EXCLUDED.instance_name,
                account       = EXCLUDED.account,
                instance_type = EXCLUDED.instance_type,
                fetched_at    = NOW()";

        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var inst in instances)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("iid", inst.InstanceId);
            cmd.Parameters.AddWithValue("name", inst.InstanceName);
            cmd.Parameters.AddWithValue("account", (object?)inst.Account ?? DBNull.Value);
            cmd.Parameters.AddWithValue("itype", inst.InstanceType);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// List all instances for a tenant with flow name joined.
    /// Used by Settings page.
    /// </summary>
    public async Task<List<InstanceEntry>> ListInstancesAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ti.id, ti.instance_id, ti.instance_name, ti.account,
                   ti.instance_type, ti.is_enabled, ti.flow_id, cf.flow_name,
                   ti.fetched_at
            FROM tenant_instances ti
            LEFT JOIN chatbot_flows cf ON cf.flow_id = ti.flow_id AND cf.tenant_id = ti.tenant_id
            WHERE ti.tenant_id = @tid
            ORDER BY ti.instance_name ASC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<InstanceEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new InstanceEntry
            {
                Id = reader.GetInt32(0),
                InstanceId = reader.GetString(1),
                InstanceName = reader.GetString(2),
                Account = reader.IsDBNull(3) ? null : reader.GetString(3),
                InstanceType = reader.GetInt32(4),
                IsEnabled = reader.GetBoolean(5),
                FlowId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                FlowName = reader.IsDBNull(7) ? null : reader.GetString(7),
                FetchedAt = reader.GetDateTime(8),
            });
        }

        return result;
    }

    /// <summary>
    /// Toggle instance enabled state.
    /// Returns false if instance is in use by a flow and caller tries to disable.
    /// </summary>
    public async Task<(bool Success, string? Error)> SetInstanceEnabledAsync(
        int tenantId, string instanceId, bool enabled, CancellationToken ct = default)
    {
        // If disabling, check flow assignment first
        if (!enabled)
        {
            var status = await GetInstanceStatusAsync(tenantId, instanceId, ct);
            if (status == null)
                return (false, "Instance not found");
            if (status.FlowId != null)
                return (false, "Instance is assigned to a flow — remove from flow first");
        }

        const string sql = @"
            UPDATE tenant_instances
            SET is_enabled = @enabled
            WHERE tenant_id = @tid AND instance_id = @iid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("iid", instanceId);
        cmd.Parameters.AddWithValue("enabled", enabled);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0 ? (true, null) : (false, "Instance not found");
    }

    /// <summary>
    /// Get enabled instances available for flow assignment.
    /// Returns enabled instances that are either unassigned OR already assigned to the given flow.
    /// </summary>
    public async Task<List<AvailableInstanceEntry>> GetAvailableInstancesAsync(
        int tenantId, int? flowId = null, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT instance_id, instance_name, instance_type, account
            FROM tenant_instances
            WHERE tenant_id = @tid
              AND is_enabled = true
              AND (flow_id IS NULL OR flow_id = @fid)
            ORDER BY instance_name ASC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", (object?)flowId ?? DBNull.Value);

        var result = new List<AvailableInstanceEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AvailableInstanceEntry
            {
                InstanceId = reader.GetString(0),
                InstanceName = reader.GetString(1),
                InstanceType = reader.GetInt32(2),
                Account = reader.IsDBNull(3) ? null : reader.GetString(3),
            });
        }

        return result;
    }

    /// <summary>
    /// Sync flow-instance mapping: clear old assignments for this flow, assign new ones.
    /// Runs in a transaction for atomicity.
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
}

/// <summary>
/// Instance status for webhook filtering decisions.
/// </summary>
public sealed class InstanceStatus
{
    public required string InstanceName { get; init; }
    public bool IsEnabled { get; init; }
    public int? FlowId { get; init; }
}

/// <summary>
/// Full instance entry for Settings page listing.
/// </summary>
public sealed class InstanceEntry
{
    public int Id { get; init; }
    public required string InstanceId { get; init; }
    public required string InstanceName { get; init; }
    public string? Account { get; init; }
    public int InstanceType { get; init; }
    public bool IsEnabled { get; init; }
    public int? FlowId { get; init; }
    public string? FlowName { get; init; }
    public DateTime FetchedAt { get; init; }
}

/// <summary>
/// Available instance for flow assignment (FlowBuilder API).
/// </summary>
public sealed class AvailableInstanceEntry
{
    public required string InstanceId { get; init; }
    public required string InstanceName { get; init; }
    public int InstanceType { get; init; }
    public string? Account { get; init; }
}

/// <summary>
/// DTO for WapCRM API instance response, used in UpsertInstancesAsync.
/// </summary>
public sealed class WapCrmInstanceDto
{
    public required string InstanceId { get; init; }
    public required string InstanceName { get; init; }
    public string? Account { get; init; }
    public int InstanceType { get; init; }
}

/// <summary>
/// WapCRM GET /api/Instances response envelope.
/// </summary>
public sealed class WapCrmApiEnvelope
{
    public bool Status { get; set; }
    public string? Message { get; set; }
    public List<WapCrmRawInstance>? Data { get; set; }
}

/// <summary>
/// Single instance from WapCRM API response.
/// </summary>
public sealed class WapCrmRawInstance
{
    public int InstanceId { get; set; }
    public string? InstanceName { get; set; }
    public string? Account { get; set; }
    public int InstanceType { get; set; }
}
