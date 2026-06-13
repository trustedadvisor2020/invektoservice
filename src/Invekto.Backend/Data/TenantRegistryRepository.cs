using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// PostgreSQL repository for tenant_registry table.
/// Thread-safe, register as singleton. SuperAdmin firma listesi + impersonate kontrolu.
///
/// NOT tenant-scoped (queries all tenants) — does not inherit TenantRepositoryBase.
/// </summary>
public class TenantRegistryRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public TenantRegistryRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// FEAT-LIW Chunk B: defense-in-depth check for the wa-direct internal
    /// endpoint. The endpoint authenticates the CALLER (Automation) via shared
    /// secret, but the tenant_id in the payload is otherwise trusted blindly —
    /// a buggy Automation routing the wrong tenant_id would silently create
    /// orphan rows. Cheap SELECT 1 against the PRIMARY KEY (tenant_registry.sql:9-10).
    /// Kept here rather than wrapped in <see cref="GetTenantAsync"/> so callers
    /// don't pay for unused TenantEntry materialization on every WA inbound.
    /// </summary>
    public virtual async Task<bool> TenantExistsAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM tenant_registry WHERE tenant_id = @tid)";
        cmd.Parameters.AddWithValue("tid", tenantId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    /// <summary>
    /// List all tenants ordered by tenant_id.
    /// No pagination — tenant_registry is expected to be small (hundreds max).
    /// </summary>
    public virtual async Task<List<TenantEntry>> ListTenantsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, tenant_name, is_active, sector, plan_tier, created_at
            FROM tenant_registry
            ORDER BY tenant_id ASC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        var result = new List<TenantEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new TenantEntry
            {
                TenantId = reader.GetInt32(0),
                TenantName = reader.GetString(1),
                IsActive = reader.GetBoolean(2),
                Sector = reader.IsDBNull(3) ? null : reader.GetString(3),
                PlanTier = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
            });
        }

        return result;
    }

    /// <summary>
    /// Get WapCRM integration settings for a tenant from settings_json->'wapcrm'.
    /// Returns null if tenant not found, inactive, or wapcrm settings missing.
    /// </summary>
    public virtual async Task<WapCrmSettings?> GetWapCrmSettingsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json->'wapcrm' AS wapcrm
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        var json = result.ToString();
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WapCrmSettings>(json);
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"WapCRM settings parse failed for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// FEAT-INMA-PIPELINE-V2 C2: per-tenant HMAC secret for verifying INMA's signed
    /// customer.selection_changed events, from settings_json->'inma'->>'webhook_secret'.
    /// Returns null if tenant inactive/missing or the secret is unset — the caller treats
    /// that as INV-INM-002 (fail-closed 401). ->> extracts the JSON string value verbatim.
    /// </summary>
    public virtual async Task<string?> GetInmaWebhookSecretAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json->'inma'->>'webhook_secret'
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        var secret = result.ToString();
        return string.IsNullOrWhiteSpace(secret) ? null : secret;
    }

    /// <summary>
    /// Get working_hours sub-object from settings_json for a tenant.
    /// Returns null if tenant not found, inactive, or no working_hours configured.
    /// </summary>
    public virtual async Task<string?> GetWorkingHoursJsonAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json->'working_hours' AS wh
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        var json = result.ToString();
        return string.IsNullOrWhiteSpace(json) ? null : json;
    }

    /// <summary>
    /// Update only the working_hours key in settings_json using JSONB merge.
    /// Initializes settings_json to empty object if currently NULL.
    /// Does not overwrite other keys (wapcrm, confidence_threshold, etc.).
    /// </summary>
    public virtual async Task<bool> UpdateWorkingHoursJsonAsync(int tenantId, string workingHoursJson, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE tenant_registry
            SET settings_json = COALESCE(settings_json, '{}'::jsonb) || jsonb_build_object('working_hours', @wh::jsonb),
                updated_at = NOW()
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("wh", workingHoursJson);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    /// <summary>Get current sector for a tenant. Returns null if not set.</summary>
    public virtual async Task<string?> GetSectorAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT sector FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : result.ToString();
    }

    /// <summary>Update sector for a tenant. Returns true if updated.</summary>
    public virtual async Task<bool> UpdateSectorAsync(int tenantId, string sector, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE tenant_registry
            SET sector = @sector, updated_at = NOW()
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("sector", sector);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    /// <summary>
    /// Get a single tenant by ID. Returns null if not found.
    /// Used before impersonate to verify tenant exists and check is_active.
    /// </summary>
    public virtual async Task<TenantEntry?> GetTenantAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, tenant_name, is_active, sector, plan_tier, created_at
            FROM tenant_registry
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new TenantEntry
        {
            TenantId = reader.GetInt32(0),
            TenantName = reader.GetString(1),
            IsActive = reader.GetBoolean(2),
            Sector = reader.IsDBNull(3) ? null : reader.GetString(3),
            PlanTier = reader.GetString(4),
            CreatedAt = reader.GetDateTime(5),
        };
    }

    /// <summary>
    /// Lazy auto-provision (UP0.3, planned in migration 009 NOTE): resolve INSE int
    /// tenant_id from opaque INMA CompanyCode string. Creates a new tenant_registry
    /// row with nextval('tenant_registry_auto_id_seq') on first login. Race-safe
    /// via partial unique uq_tenant_registry_inma_code + ON CONFLICT DO NOTHING.
    /// Caller (Program.cs exchange/login/ExtractTenantFromBearer) invokes this after
    /// InmaTokenIntrospector welcome-endpoint introspection succeeds.
    /// </summary>
    public virtual async Task<int> ResolveOrCreateByInmaCodeAsync(
        string inmaCode,
        string displayName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inmaCode))
            throw new ArgumentException("inmaCode is required (pre-validated by caller)", nameof(inmaCode));

        var trimmedCode = inmaCode.Trim();
        // tenant_name NOT NULL — fall back to the code itself when INMA omits FullName.
        var tenantName = string.IsNullOrWhiteSpace(displayName) ? trimmedCode : displayName.Trim();

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Fast path: existing tenant by inma_code.
        await using (var selectCmd = new NpgsqlCommand(
            "SELECT tenant_id FROM tenant_registry WHERE inma_code = @code LIMIT 1", conn))
        {
            selectCmd.Parameters.AddWithValue("code", trimmedCode);
            var existing = await selectCmd.ExecuteScalarAsync(ct);
            if (existing is int existingId)
                return existingId;
        }

        // Insert path: nextval() + ON CONFLICT DO NOTHING via partial-index inference.
        // `(inma_code) WHERE inma_code IS NOT NULL` matches partial UNIQUE INDEX
        // uq_tenant_registry_inma_code (migration 009). ON CONSTRAINT requires an
        // actual UNIQUE/EXCLUDE CONSTRAINT; a bare UNIQUE INDEX must be targeted
        // by inference clause. Concurrent first-logins: only one row inserted
        // (unique index); losers get NULL from RETURNING and re-SELECT the winner.
        await using (var insertCmd = new NpgsqlCommand(@"
            INSERT INTO tenant_registry (tenant_id, tenant_name, inma_code, is_active)
            VALUES (nextval('tenant_registry_auto_id_seq'), @name, @code, true)
            ON CONFLICT (inma_code) WHERE inma_code IS NOT NULL DO NOTHING
            RETURNING tenant_id", conn))
        {
            insertCmd.Parameters.AddWithValue("name", tenantName);
            insertCmd.Parameters.AddWithValue("code", trimmedCode);
            var inserted = await insertCmd.ExecuteScalarAsync(ct);
            if (inserted is int insertedId)
            {
                _logger.SystemInfo($"tenant auto-provisioned: inma_code={trimmedCode} tenant_id={insertedId}");
                return insertedId;
            }
        }

        // Race: a concurrent insert won; re-SELECT to fetch the winner's tenant_id.
        await using (var reselectCmd = new NpgsqlCommand(
            "SELECT tenant_id FROM tenant_registry WHERE inma_code = @code LIMIT 1", conn))
        {
            reselectCmd.Parameters.AddWithValue("code", trimmedCode);
            var winner = await reselectCmd.ExecuteScalarAsync(ct);
            if (winner is int winnerId)
                return winnerId;
        }

        // Should never happen — insert conflict but row not found on re-select (serialization
        // gap or wrong partial-index predicate). Caller maps to INV-AUTH-009 500.
        throw new InvalidOperationException(
            $"Tenant resolve/create failed after conflict fallback (inma_code={trimmedCode})");
    }
}

/// <summary>
/// Immutable response model for tenant_registry rows.
/// JSON serialized as camelCase by ASP.NET Core defaults.
/// </summary>
public sealed class TenantEntry
{
    public int TenantId { get; init; }
    public required string TenantName { get; init; }
    public bool IsActive { get; init; }
    public string? Sector { get; init; }
    public required string PlanTier { get; init; }
    public DateTime CreatedAt { get; init; }
}

// WapCrmSettings Invekto.Shared.Contracts.Inma.Dtos altına taşındı (UP0.1)
