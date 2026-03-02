using System.Collections.Concurrent;
using System.Text.Json;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Shared.Services;

/// <summary>
/// Per-tenant plan information cache. Reads plan_tier + features_json from
/// tenant_registry joined with plan_definitions. 5-minute TTL.
///
/// features_json NULL → falls back to plan_definitions tier defaults (all features enabled).
/// Fail-open: DB error → serve stale cache or allow request through.
/// Register as singleton in every service's DI.
/// </summary>
public sealed class TenantPlanCache
{
    private readonly string _connectionString;
    private readonly JsonLinesLogger _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<int, (TenantPlanInfo Info, DateTime Expiry)> _cache = new();

    public TenantPlanCache(string connectionString, JsonLinesLogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Returns plan info for a tenant. Hits DB on cache miss or TTL expiry.
    /// Returns null if tenant not found AND no stale cache exists.
    /// </summary>
    public async Task<TenantPlanInfo?> GetAsync(int tenantId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(tenantId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Info;

        try
        {
            var info = await LoadFromDbAsync(tenantId, ct);
            if (info != null)
                _cache[tenantId] = (info, DateTime.UtcNow.Add(CacheTtl));
            return info;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[TenantPlanCache] DB error loading plan for tenant {tenantId}: {ex.Message}");
            return _cache.TryGetValue(tenantId, out var stale) ? stale.Info : null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[TenantPlanCache] Failed to load plan for tenant {tenantId}: {ex.Message}");
            return _cache.TryGetValue(tenantId, out var stale2) ? stale2.Info : null;
        }
    }

    /// <summary>
    /// Invalidates cached entry for a tenant. Call after SuperAdmin updates features_json or plan_tier.
    /// </summary>
    public void Invalidate(int tenantId) => _cache.TryRemove(tenantId, out _);

    private async Task<TenantPlanInfo?> LoadFromDbAsync(int tenantId, CancellationToken ct)
    {
        const string sql = @"
            SELECT
                tr.plan_tier,
                tr.features_json::text,
                pd.features_json::text AS tier_features,
                pd.quotas_json::text
            FROM tenant_registry tr
            LEFT JOIN plan_definitions pd ON pd.tier_name = tr.plan_tier AND pd.is_active = true
            WHERE tr.tenant_id = @tid AND tr.is_active = true";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var planTier = reader.GetString(0);
        var tenantFeatJson = reader.IsDBNull(1) ? null : reader.GetString(1);
        var tierFeatJson = reader.IsDBNull(2) ? null : reader.GetString(2);
        var quotasJson = reader.IsDBNull(3) ? null : reader.GetString(3);

        var effectiveFeatures = ResolveFeatures(tenantFeatJson, tierFeatJson);
        var quotas = ParseQuotas(quotasJson);

        return new TenantPlanInfo
        {
            TenantId = tenantId,
            PlanTier = planTier,
            EffectiveFeatures = effectiveFeatures,
            Quotas = quotas
        };
    }

    /// <summary>
    /// Resolves effective features. Tenant override (features_json) takes priority;
    /// NULL/empty falls back to plan_definitions tier defaults.
    /// </summary>
    private Dictionary<string, HashSet<string>> ResolveFeatures(
        string? tenantFeatJson, string? tierFeatJson)
    {
        if (!string.IsNullOrWhiteSpace(tenantFeatJson) && tenantFeatJson != "{}")
        {
            var tenantFeatures = ParseFeaturesJson(tenantFeatJson);
            if (tenantFeatures.Count > 0)
                return tenantFeatures;
        }
        return ParseFeaturesJson(tierFeatJson);
    }

    private Dictionary<string, HashSet<string>> ParseFeaturesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var modes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in prop.Value.EnumerateArray())
                        if (el.GetString() is string mode)
                            modes.Add(mode);
                }
                result[prop.Name] = modes;
            }
            return result;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[TenantPlanCache] Failed to parse features_json: {ex.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private PlanQuotas ParseQuotas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PlanQuotas.Unlimited;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return new PlanQuotas
            {
                MessagesPerMonth = r.TryGetProperty("messages_per_month", out var m) ? m.GetInt32() : -1,
                MaxUsers = r.TryGetProperty("max_users", out var u) ? u.GetInt32() : -1,
                MaxFlows = r.TryGetProperty("max_flows", out var f) ? f.GetInt32() : -1
            };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[TenantPlanCache] Failed to parse quotas_json: {ex.Message}");
            return PlanQuotas.Unlimited;
        }
    }
}

/// <summary>
/// Resolved plan information for a tenant. Immutable after construction.
/// </summary>
public sealed class TenantPlanInfo
{
    public required int TenantId { get; init; }
    public required string PlanTier { get; init; }

    /// <summary>
    /// Effective features: service name → set of allowed modes.
    /// Key presence = feature enabled. Mode "on" = simple on/off feature.
    /// </summary>
    public required Dictionary<string, HashSet<string>> EffectiveFeatures { get; init; }
    public required PlanQuotas Quotas { get; init; }

    public bool IsFeatureEnabled(string featureName)
        => EffectiveFeatures.ContainsKey(featureName);

    public bool IsFeatureModeEnabled(string featureName, string mode)
        => EffectiveFeatures.TryGetValue(featureName, out var modes) && modes.Contains(mode);
}

public sealed class PlanQuotas
{
    public int MessagesPerMonth { get; init; } = -1;
    public int MaxUsers { get; init; } = -1;
    public int MaxFlows { get; init; } = -1;

    public static readonly PlanQuotas Unlimited = new();
}
