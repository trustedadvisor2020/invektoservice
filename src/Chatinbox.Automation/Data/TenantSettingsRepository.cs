using Chatinbox.Shared.Data;
using Microsoft.Extensions.Caching.Memory;

namespace Chatinbox.Automation.Data;

/// <summary>
/// FEAT-J2: Read-only access to the <c>tenant_settings.enforce_message_category</c>
/// flag used by <c>AutomationOrchestrator</c> to decide whether to reject
/// <c>send_message</c> callbacks with a null event_name (or outside the
/// transactional allow-list).
///
/// Cache: 5-minute TTL per tenant (hot path — every flow-driven message build
/// reads this). Single canonical entry point; do not cache per call-site.
///
/// Schema: arch/db/tenant-settings.sql (column added in migration 026).
/// </summary>
public class TenantSettingsRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public TenantSettingsRepository(PostgresConnectionFactory db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Returns the tenant's <c>enforce_message_category</c> flag. Missing row
    /// (tenant has never written settings) yields <c>false</c> — backward-compat
    /// behaviour preserved.
    /// </summary>
    public virtual async Task<bool> GetEnforceMessageCategoryAsync(int tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"tenant_settings:enforce_msg_category:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT enforce_message_category
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var enforce = result is bool b && b;

        _cache.Set(cacheKey, enforce, CacheTtl);
        return enforce;
    }

    /// <summary>
    /// FEAT-DMP: Read <c>tenant_settings.enable_dynamic_message</c>. Missing row
    /// yields <c>true</c> — matches the column default (migration 027) so new
    /// tenants get feature-enabled behaviour. 5-minute cache (hot path on every
    /// flow-driven send_message).
    /// </summary>
    public virtual async Task<bool> GetEnableDynamicMessageAsync(int tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"tenant_settings:enable_dynamic_msg:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT enable_dynamic_message
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var enabled = result is bool b ? b : true;

        _cache.Set(cacheKey, enabled, CacheTtl);
        return enabled;
    }
}
