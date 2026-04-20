using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// FEAT-TFM MVP: Backend-side repo over <c>tenant_settings.field_mapping JSONB</c>.
/// GET surfaces the raw JSON to the dashboard editor; UPSERT writes the validated payload.
///
/// Microservice isolation: Backend owns the CRUD endpoints; Outbound/Automation read the
/// same column via their own repos (OutboundRepository.GetEnableDynamicMessageAsync pattern).
/// No cross-service DB access — each service connects with its own PostgresConnectionFactory.
/// </summary>
public class TenantSettingsRepository
{
    private readonly PostgresConnectionFactory _db;

    public TenantSettingsRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Read the tenant's <c>field_mapping</c> JSON + <c>updated_at</c>. Missing row
    /// (tenant has never written settings) yields <c>("{}", null)</c>.
    /// </summary>
    public virtual async Task<(string FieldMappingJson, DateTime? UpdatedAt)> GetFieldMappingAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT field_mapping::text, updated_at
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return ("{}", null);

        var json = reader.IsDBNull(0) ? "{}" : reader.GetString(0);
        var updatedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        return (json, updatedAt);
    }

    /// <summary>
    /// UPSERT <c>field_mapping</c> for the given tenant. Caller validates the payload
    /// via <c>TenantFieldMappingValidator</c> first. <c>updated_at</c> set to NOW().
    /// Returns the post-write <c>updated_at</c> for the response payload.
    /// </summary>
    public virtual async Task<DateTime> UpsertFieldMappingAsync(
        int tenantId, string fieldMappingJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tenant_settings (tenant_id, field_mapping, updated_at)
            VALUES (@tid, @json::jsonb, NOW())
            ON CONFLICT (tenant_id) DO UPDATE
                SET field_mapping = EXCLUDED.field_mapping,
                    updated_at = NOW()
            RETURNING updated_at";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("json", fieldMappingJson);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DateTime dt ? dt : DateTime.UtcNow;
    }
}
