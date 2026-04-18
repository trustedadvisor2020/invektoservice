using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// FEAT-LIW: repository over tenant_landing_settings. Read-only in Chunk A —
/// rotation/create endpoints land in Chunk C (Dashboard editor). Lookup is
/// O(1) via uq_tls_api_key (active) + uq_tls_api_key_old (grace partial index).
/// </summary>
public class TenantLandingSettingsRepository
{
    private readonly PostgresConnectionFactory _db;

    public TenantLandingSettingsRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolve an incoming X-Invekto-Api-Key to the owning tenant + settings row.
    /// Matches the active key OR the old key when its grace window is still open.
    /// Returns null on unknown/expired key (callers map to 401 INV-BE-100).
    /// </summary>
    public virtual async Task<TenantLandingSettings?> FindByApiKeyAsync(
        string apiKey, DateTime nowUtc, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id,
                   landing_api_key,
                   landing_api_key_old,
                   landing_api_key_old_expires_at,
                   landing_field_map::text,
                   welcome_flow_slug,
                   intake_dup_window_days
            FROM tenant_landing_settings
            WHERE landing_api_key = @key
               OR (landing_api_key_old = @key
                   AND landing_api_key_old_expires_at IS NOT NULL
                   AND landing_api_key_old_expires_at > @now)
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("key", apiKey);
        cmd.Parameters.AddWithValue("now", nowUtc);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new TenantLandingSettings
        {
            TenantId = reader.GetInt32(0),
            ActiveApiKey = reader.GetString(1),
            OldApiKey = reader.IsDBNull(2) ? null : reader.GetString(2),
            OldApiKeyExpiresAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            LandingFieldMapJson = reader.GetString(4),
            WelcomeFlowSlug = reader.IsDBNull(5) ? null : reader.GetString(5),
            DupWindowDays = reader.GetInt32(6)
        };
    }

    /// <summary>
    /// FEAT-LIW Chunk B: tenant-id lookup for the wa-direct hook (no API key
    /// involved; Automation passes the resolved tenant_id from the inbound
    /// webhook context). Returns null when the tenant has no landing settings
    /// row yet (caller falls back to platform defaults: welcome_default slug,
    /// 30-day dup window) so a tenant can receive WA direct leads before
    /// configuring a landing page.
    /// Schema reference: tenant_landing_settings table is defined in
    /// arch/db/tenant-landing-settings.sql + migration 021-leads-intake-tenant-landing-settings.sql.
    /// PRIMARY KEY (tenant_id) → at most one row per tenant.
    /// </summary>
    public virtual async Task<TenantLandingSettings?> FindByTenantIdAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id,
                   landing_api_key,
                   landing_api_key_old,
                   landing_api_key_old_expires_at,
                   landing_field_map::text,
                   welcome_flow_slug,
                   intake_dup_window_days
            FROM tenant_landing_settings
            WHERE tenant_id = @tid
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new TenantLandingSettings
        {
            TenantId = reader.GetInt32(0),
            ActiveApiKey = reader.GetString(1),
            OldApiKey = reader.IsDBNull(2) ? null : reader.GetString(2),
            OldApiKeyExpiresAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            LandingFieldMapJson = reader.GetString(4),
            WelcomeFlowSlug = reader.IsDBNull(5) ? null : reader.GetString(5),
            DupWindowDays = reader.GetInt32(6)
        };
    }
}

/// <summary>Row projection for <see cref="TenantLandingSettingsRepository"/>.</summary>
public sealed class TenantLandingSettings
{
    public int TenantId { get; set; }
    public string ActiveApiKey { get; set; } = "";
    public string? OldApiKey { get; set; }
    public DateTime? OldApiKeyExpiresAt { get; set; }
    public string LandingFieldMapJson { get; set; } = "{}";
    public string? WelcomeFlowSlug { get; set; }
    public int DupWindowDays { get; set; } = 30;
}
