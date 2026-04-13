using Microsoft.Extensions.Caching.Memory;

namespace Invekto.Shared.Services;

/// <summary>
/// In-memory IFeatureFlagService backed by IMemoryCache. Default TTL: 5 minutes.
/// Thread-safe (IMemoryCache is concurrent). Process-local (no cross-instance sharing).
/// Designed for stateless services where features are re-populated on each token exchange.
///
/// Closed-by-default: missing cache entry → IsEnabled returns false. This avoids granting
/// access to a feature when license info has not been refreshed yet.
/// </summary>
public sealed class InMemoryFeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public InMemoryFeatureFlagService(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public bool IsEnabled(int tenantId, int userId, string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        if (!_cache.TryGetValue<HashSet<string>>(BuildKey(tenantId, userId), out var set) || set is null)
        {
            return false;
        }
        return set.Contains(flag);
    }

    public void SetFeatures(int tenantId, int userId, IReadOnlyCollection<string> features, TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(features);
        var set = new HashSet<string>(features, StringComparer.Ordinal);
        _cache.Set(BuildKey(tenantId, userId), set, ttl ?? DefaultTtl);
    }

    public void Invalidate(int tenantId, int userId)
    {
        _cache.Remove(BuildKey(tenantId, userId));
    }

    private static string BuildKey(int tenantId, int userId) => $"ff:{tenantId}:{userId}";
}
