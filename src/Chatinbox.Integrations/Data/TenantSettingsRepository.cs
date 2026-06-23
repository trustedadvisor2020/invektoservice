using Chatinbox.Shared.Data;
using Npgsql;

namespace Chatinbox.Integrations.Data;

/// <summary>
/// FEAT-VCP Chunk A: read-only repository over <c>tenant_settings</c>
/// (schema: arch/db/tenant-settings.sql; migration 023).
/// Chunk A only needs the per-tenant read (VideoProviderFactory); writes happen in
/// Chunk C when the Dashboard settings page is built.
/// </summary>
public class TenantSettingsRepository
{
    private readonly PostgresConnectionFactory _db;

    public TenantSettingsRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Return the tenant_settings row for <paramref name="tenantId"/> or null when no row exists.
    /// Callers treat null as "provider not configured" (INV-INT-142 business state).
    /// <see cref="NpgsqlException"/> is intentionally NOT caught here and propagates through
    /// <see cref="VideoProviderFactory.ResolveAsync"/> to the Chunk B appointment handler,
    /// which translates it to INV-INT-143 with an HTTP 503 envelope so DB outages stay
    /// operationally distinct from genuine configuration-miss situations.
    /// Schema: arch/db/tenant-settings.sql (tenant_id PK, video_provider VARCHAR(20), video_provider_config JSONB).
    /// </summary>
    public virtual async Task<TenantSettingsRow?> FindByTenantIdAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT tenant_id,
                   video_provider,
                   video_provider_config::text,
                   created_at,
                   updated_at
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return new TenantSettingsRow(
            TenantId: reader.GetInt32(0),
            VideoProvider: reader.IsDBNull(1) ? null : reader.GetString(1),
            VideoProviderConfigJson: reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt: reader.GetDateTime(3),
            UpdatedAt: reader.GetDateTime(4));
    }
}

/// <summary>
/// Projection of a single <c>tenant_settings</c> row.
/// VideoProviderConfigJson is surfaced as raw JSON text so Chunk C can deserialise
/// provider-specific shapes (GoogleMeet: refresh_token_encrypted, calendar_id, etc.)
/// without coupling this repository to any particular provider's schema.
/// </summary>
public sealed record TenantSettingsRow(
    int TenantId,
    string? VideoProvider,
    string? VideoProviderConfigJson,
    DateTime CreatedAt,
    DateTime UpdatedAt);
