// Adim 3 Paket 1: persistence for zoho_stage_mappings (per-tenant Gunes event -> Zoho transition).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Integrations.Data;

public sealed class ZohoStageMappingRepository
{
    private readonly PostgresConnectionFactory _db;

    public ZohoStageMappingRepository(PostgresConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ZohoStageMappingRow>> ListAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, gunes_event, zoho_transition_id, zoho_transition_name, updated_at
              FROM zoho_stage_mappings
             WHERE tenant_id = @tid
             ORDER BY gunes_event";

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tid", tenantId);

        var rows = new List<ZohoStageMappingRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new ZohoStageMappingRow(
                TenantId:           reader.GetInt32(0),
                GunesEvent:         reader.GetString(1),
                ZohoTransitionId:   reader.GetString(2),
                ZohoTransitionName: reader.IsDBNull(3) ? null : reader.GetString(3),
                UpdatedAt:          reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
        }
        return rows;
    }

    public async Task<ZohoStageMappingRow?> FindAsync(
        int tenantId, string gunesEvent, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, gunes_event, zoho_transition_id, zoho_transition_name, updated_at
              FROM zoho_stage_mappings
             WHERE tenant_id = @tid AND gunes_event = @evt
             LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tid", tenantId);
        cmd.Parameters.AddWithValue("@evt", gunesEvent);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        return new ZohoStageMappingRow(
            TenantId:           reader.GetInt32(0),
            GunesEvent:         reader.GetString(1),
            ZohoTransitionId:   reader.GetString(2),
            ZohoTransitionName: reader.IsDBNull(3) ? null : reader.GetString(3),
            UpdatedAt:          reader.IsDBNull(4) ? null : reader.GetDateTime(4));
    }

    /// <summary>Atomic replace: wipes existing tenant mappings and inserts the supplied entries in one transaction.</summary>
    public async Task ReplaceAllAsync(
        int tenantId,
        IReadOnlyList<(string GunesEvent, string ZohoTransitionId, string? ZohoTransitionName)> entries,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using (var del = new NpgsqlCommand("DELETE FROM zoho_stage_mappings WHERE tenant_id = @tid", conn, tx))
        {
            del.Parameters.AddWithValue("@tid", tenantId);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (entries.Count > 0)
        {
            const string insertSql = @"
                INSERT INTO zoho_stage_mappings
                    (tenant_id, gunes_event, zoho_transition_id, zoho_transition_name, updated_at)
                VALUES (@tid, @evt, @tid_zoho, @tname, NOW())";
            foreach (var e in entries)
            {
                await using var ins = new NpgsqlCommand(insertSql, conn, tx);
                ins.Parameters.AddWithValue("@tid", tenantId);
                ins.Parameters.AddWithValue("@evt", e.GunesEvent);
                ins.Parameters.AddWithValue("@tid_zoho", e.ZohoTransitionId);
                ins.Parameters.AddWithValue("@tname", (object?)e.ZohoTransitionName ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }
}

public sealed record ZohoStageMappingRow(
    int TenantId,
    string GunesEvent,
    string ZohoTransitionId,
    string? ZohoTransitionName,
    DateTime? UpdatedAt);
