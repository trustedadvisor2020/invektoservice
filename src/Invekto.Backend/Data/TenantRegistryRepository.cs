using System.Text.Json;
using System.Text.Json.Serialization;
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
public sealed class TenantRegistryRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public TenantRegistryRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// List all tenants ordered by tenant_id.
    /// No pagination — tenant_registry is expected to be small (hundreds max).
    /// </summary>
    public async Task<List<TenantEntry>> ListTenantsAsync(CancellationToken ct = default)
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
    public async Task<WapCrmSettings?> GetWapCrmSettingsAsync(int tenantId, CancellationToken ct = default)
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
    /// Get working_hours sub-object from settings_json for a tenant.
    /// Returns null if tenant not found, inactive, or no working_hours configured.
    /// </summary>
    public async Task<string?> GetWorkingHoursJsonAsync(int tenantId, CancellationToken ct = default)
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
    public async Task<bool> UpdateWorkingHoursJsonAsync(int tenantId, string workingHoursJson, CancellationToken ct = default)
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
    public async Task<string?> GetSectorAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT sector FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : result.ToString();
    }

    /// <summary>Update sector for a tenant. Returns true if updated.</summary>
    public async Task<bool> UpdateSectorAsync(int tenantId, string sector, CancellationToken ct = default)
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
    public async Task<TenantEntry?> GetTenantAsync(int tenantId, CancellationToken ct = default)
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

/// <summary>
/// WapCRM integration settings stored in tenant_registry.settings_json->'wapcrm'.
/// Maps snake_case JSON keys from DB to PascalCase C# properties.
/// </summary>
public sealed class WapCrmSettings
{
    [JsonPropertyName("secret_key")]
    public string? SecretKey { get; init; }

    [JsonPropertyName("api_url")]
    public string? ApiUrl { get; init; }

    [JsonPropertyName("user_id")]
    public int UserId { get; init; }
}
