using Chatinbox.Shared.Data;
using Npgsql;

namespace Chatinbox.Backend.Data;

/// <summary>
/// FEAT-LIW: repository over tenant_landing_settings.
/// - Chunk A (read-only lookups by api_key / tenant_id) feeds the intake path.
/// - Chunk C adds the Dashboard mutation surface: EnsureRowExistsOrCreateWithKeyAsync
///   (lazy row auto-creation on first-time rotate), RotateApiKeyAsync + RevokeApiKeyAsync
///   + UpdateFieldMapAsync (optimistic concurrency via updated_at). Mutation methods
///   take an externally-opened NpgsqlConnection so they participate in the caller's
///   transaction (LiwSettingsService orchestrates BEGIN/COMMIT across the settings
///   UPDATE + the liw_audit_log INSERT so audit can never silently drift).
/// - Chunk C relaxed landing_api_key NOT NULL -> NULL (migration 022) to support
///   Revoke semantics (see FindByApiKeyAsync guard below). ActiveApiKey projection
///   is now nullable; existing callers (LeadIntakeService) do not read that property.
/// Schema reference: arch/db/tenant-landing-settings.sql + migrations 021 + 022.
/// Lookup is O(1) via uq_tls_api_key (active) + uq_tls_api_key_old (grace partial index).
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
    /// Chunk C guard: 'AND landing_api_key IS NOT NULL' ensures a revoked row
    /// (landing_api_key cleared to NULL) never matches any inbound key even if
    /// the caller happens to send a value that equals NULL's typed representation.
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
                   intake_dup_window_days,
                   created_at,
                   updated_at
            FROM tenant_landing_settings
            WHERE (landing_api_key = @key AND landing_api_key IS NOT NULL)
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

        return ReadProjection(reader);
    }

    /// <summary>
    /// FEAT-LIW Chunk B: tenant-id lookup for the wa-direct hook (no API key
    /// involved; Automation passes the resolved tenant_id from the inbound
    /// webhook context). Chunk C: same method is reused by the Dashboard
    /// GET /api/v1/tenant/landing/settings path. Returns null when the tenant
    /// has no landing settings row yet (caller falls back to platform defaults:
    /// welcome_default slug, 30-day dup window) so a tenant can receive WA
    /// direct leads before configuring a landing page.
    /// Schema reference: tenant_landing_settings table is defined in
    /// arch/db/tenant-landing-settings.sql + migrations 021 + 022.
    /// PRIMARY KEY (tenant_id) → at most one row per tenant.
    /// </summary>
    public virtual async Task<TenantLandingSettings?> FindByTenantIdAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await FindByTenantIdInternalAsync(conn, tenantId, ct);
    }

    /// <summary>
    /// FEAT-LIW Chunk C: transaction-participating overload. When LiwSettingsService
    /// runs a mutation it wants to capture the before-snapshot inside the SAME
    /// transaction as the UPDATE + audit INSERT; this overload accepts the caller's
    /// already-open connection so the SELECT runs under the same transaction
    /// boundary, eliminating the theoretical stale-before-snapshot window Codex
    /// iter 1 CQ9 flagged.
    /// </summary>
    public virtual async Task<TenantLandingSettings?> FindByTenantIdAsync(
        NpgsqlConnection conn, int tenantId, CancellationToken ct = default)
    {
        return await FindByTenantIdInternalAsync(conn, tenantId, ct);
    }

    private static async Task<TenantLandingSettings?> FindByTenantIdInternalAsync(
        NpgsqlConnection conn, int tenantId, CancellationToken ct)
    {
        const string sql = @"
            SELECT tenant_id,
                   landing_api_key,
                   landing_api_key_old,
                   landing_api_key_old_expires_at,
                   landing_field_map::text,
                   welcome_flow_slug,
                   intake_dup_window_days,
                   created_at,
                   updated_at
            FROM tenant_landing_settings
            WHERE tenant_id = @tid
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return ReadProjection(reader);
    }

    /// <summary>
    /// FEAT-LIW Chunk C: first-time row creation for tenants that never had a
    /// landing settings row. Called by LiwSettingsService.RotateApiKeyAsync when
    /// the tenant is rotating for the first time (no row to UPDATE). Idempotent
    /// via ON CONFLICT (tenant_id) DO NOTHING so concurrent first-rotate calls
    /// don't duplicate rows; when another call won the race, the SELECT fallback
    /// returns the existing row's updated_at + landing_api_key so the caller
    /// knows to surface a 409 (the other rotate set a different active key).
    /// Returns EnsureRowResult { RowVersion, InsertedNewRow, PersistedActiveKey }.
    /// Schema reference: arch/db/tenant-landing-settings.sql (columns) + migration 022 (landing_api_key nullable).
    /// </summary>
    public virtual async Task<EnsureRowResult> EnsureRowExistsOrCreateWithKeyAsync(
        NpgsqlConnection conn,
        int tenantId,
        string initialActiveKey,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // INSERT first; when conflict, SELECT the winning row.
        const string insertSql = @"
            INSERT INTO tenant_landing_settings
                (tenant_id, landing_api_key, landing_field_map, created_at, updated_at)
            VALUES (@tid, @key, '{}'::jsonb, @now, @now)
            ON CONFLICT (tenant_id) DO NOTHING
            RETURNING updated_at, landing_api_key";

        await using (var cmd = new NpgsqlCommand(insertSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("key", initialActiveKey);
            cmd.Parameters.AddWithValue("now", nowUtc);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new EnsureRowResult(
                    RowVersion: reader.GetDateTime(0),
                    InsertedNewRow: true,
                    PersistedActiveKey: reader.IsDBNull(1) ? null : reader.GetString(1));
            }
        }

        // Conflict path: row already exists. Return its current state.
        const string selectSql = @"
            SELECT updated_at, landing_api_key
            FROM tenant_landing_settings
            WHERE tenant_id = @tid
            LIMIT 1";

        await using (var selectCmd = new NpgsqlCommand(selectSql, conn))
        {
            selectCmd.Parameters.AddWithValue("tid", tenantId);
            await using var selectReader = await selectCmd.ExecuteReaderAsync(ct);
            if (!await selectReader.ReadAsync(ct))
            {
                // Shouldn't happen (INSERT conflicted, row exists by definition); defensive return.
                throw new InvalidOperationException(
                    $"tenant_landing_settings row for tenant={tenantId} disappeared between INSERT conflict and SELECT fallback.");
            }
            return new EnsureRowResult(
                RowVersion: selectReader.GetDateTime(0),
                InsertedNewRow: false,
                PersistedActiveKey: selectReader.IsDBNull(1) ? null : selectReader.GetString(1));
        }
    }

    /// <summary>
    /// FEAT-LIW Chunk C: atomic key rotation. Moves landing_api_key -> landing_api_key_old
    /// (+ sets old_expires_at = @grace), installs @newActive. Optimistic concurrency:
    /// WHERE updated_at = @expected. Returns the fresh row_version when updated;
    /// null when 0 rows matched (caller maps to 409 INV-BE-113).
    /// On a PostgreSQL 23505 unique_violation (astronomically unlikely key
    /// collision on landing_api_key), the NpgsqlException bubbles so the caller
    /// can retry once with a fresh key.
    /// </summary>
    public virtual async Task<DateTime?> RotateApiKeyAsync(
        NpgsqlConnection conn,
        int tenantId,
        string newActive,
        DateTime graceUntil,
        DateTime expectedRowVersion,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE tenant_landing_settings
            SET landing_api_key_old = landing_api_key,
                landing_api_key_old_expires_at = @grace,
                landing_api_key = @new,
                updated_at = NOW()
            WHERE tenant_id = @tid
              AND updated_at = @expected
            RETURNING updated_at";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("new", newActive);
        cmd.Parameters.AddWithValue("grace", graceUntil);
        cmd.Parameters.AddWithValue("expected", expectedRowVersion);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.GetDateTime(0);
    }

    /// <summary>
    /// FEAT-LIW Chunk C: atomic revoke. Clears BOTH active and old keys + clears
    /// old_expires_at so no inbound request can possibly match this tenant's row
    /// after revoke. Preserves field_map, welcome_flow_slug, dup_window so the
    /// tenant can re-enable via future rotate without losing their configuration.
    /// Optimistic concurrency: WHERE updated_at = @expected; 0 rows -> null ->
    /// caller maps to 409 INV-BE-113.
    /// </summary>
    public virtual async Task<DateTime?> RevokeApiKeyAsync(
        NpgsqlConnection conn,
        int tenantId,
        DateTime expectedRowVersion,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE tenant_landing_settings
            SET landing_api_key = NULL,
                landing_api_key_old = NULL,
                landing_api_key_old_expires_at = NULL,
                updated_at = NOW()
            WHERE tenant_id = @tid
              AND updated_at = @expected
            RETURNING updated_at";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("expected", expectedRowVersion);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.GetDateTime(0);
    }

    /// <summary>
    /// FEAT-LIW Chunk C: atomic field map save. @fieldMapJson is the ALREADY-
    /// SERIALIZED canonical JSONB shape (caller folds phone_country_hint into
    /// the 'phone.country_hint' reserved key before calling). Optimistic
    /// concurrency via updated_at; 0 rows -> null -> 409 INV-BE-113.
    /// </summary>
    public virtual async Task<DateTime?> UpdateFieldMapAsync(
        NpgsqlConnection conn,
        int tenantId,
        string fieldMapJson,
        DateTime expectedRowVersion,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE tenant_landing_settings
            SET landing_field_map = @json::jsonb,
                updated_at = NOW()
            WHERE tenant_id = @tid
              AND updated_at = @expected
            RETURNING updated_at";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("json", fieldMapJson);
        cmd.Parameters.AddWithValue("expected", expectedRowVersion);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.GetDateTime(0);
    }

    private static TenantLandingSettings ReadProjection(NpgsqlDataReader reader)
    {
        return new TenantLandingSettings
        {
            TenantId = reader.GetInt32(0),
            ActiveApiKey = reader.IsDBNull(1) ? null : reader.GetString(1),
            OldApiKey = reader.IsDBNull(2) ? null : reader.GetString(2),
            OldApiKeyExpiresAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            LandingFieldMapJson = reader.GetString(4),
            WelcomeFlowSlug = reader.IsDBNull(5) ? null : reader.GetString(5),
            DupWindowDays = reader.GetInt32(6),
            CreatedAt = reader.GetDateTime(7),
            UpdatedAt = reader.GetDateTime(8)
        };
    }
}

/// <summary>Row projection for <see cref="TenantLandingSettingsRepository"/>.</summary>
public sealed class TenantLandingSettings
{
    public int TenantId { get; set; }
    /// <summary>Chunk C: nullable because Revoke sets this column to NULL (channel disabled).</summary>
    public string? ActiveApiKey { get; set; }
    public string? OldApiKey { get; set; }
    public DateTime? OldApiKeyExpiresAt { get; set; }
    public string LandingFieldMapJson { get; set; } = "{}";
    public string? WelcomeFlowSlug { get; set; }
    public int DupWindowDays { get; set; } = 30;
    /// <summary>Chunk C: added to projection for row creation timestamp display in UI.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Chunk C: row_version for optimistic concurrency on Dashboard mutations.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Outcome of <see cref="TenantLandingSettingsRepository.EnsureRowExistsOrCreateWithKeyAsync"/>.</summary>
public sealed record EnsureRowResult(
    DateTime RowVersion,
    bool InsertedNewRow,
    string? PersistedActiveKey);
