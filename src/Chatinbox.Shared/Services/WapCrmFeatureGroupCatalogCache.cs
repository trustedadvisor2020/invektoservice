using System.Collections.Concurrent;
using Chatinbox.Shared.Contracts.Inma;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Chatinbox.Shared.Services;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3b: tenant-scoped IMemoryCache wrapper for the cxapi
/// customer-feature-groups catalog. 1h TTL (parity with InmaDynamicFieldsCache; Codex
/// recommendation for fresher config after a WapCRM reconfig) + manual <c>Invalidate</c> entry point
/// (cxapi exposes NO catalog-change event, so the dropdown polls/caches; ids are stable,
/// only display names drift). Cache miss → <see cref="WapCrmFeatureGroupCatalogClient.GetCatalogAsync"/>
/// → populate + return. Failures are NOT cached (the client throws; the cache does not swallow).
/// <para>
/// Single-flight + cancellation-isolation copied verbatim from <see cref="InmaDynamicFieldsCache"/>:
/// concurrent misses for the same tenant coalesce onto one in-flight task started with
/// <see cref="CancellationToken.None"/> so a Dashboard tab closing mid-flight cannot poison the
/// joined awaiters; each caller observes its own <see cref="CancellationToken"/> via
/// <see cref="Task.WaitAsync(CancellationToken)"/>. Register as a singleton (thread-safe via
/// IMemoryCache + ConcurrentDictionary). Tenant id is server-derived (signed JWT claim); the
/// cache key is tenant-only and never contains the secret.
/// </para>
/// </summary>
public sealed class WapCrmFeatureGroupCatalogCache
{
    private readonly IMemoryCache _cache;
    private readonly WapCrmFeatureGroupCatalogClient _client;
    private readonly ConcurrentDictionary<int, Task<IReadOnlyList<CustomerFeatureGroupView>>> _inflight = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public WapCrmFeatureGroupCatalogCache(IMemoryCache cache, WapCrmFeatureGroupCatalogClient client)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Cache-first lookup with single-flight coalescing. The client throws
    /// <see cref="WapCrmFeatureGroupCatalogFetchException"/> on upstream failure; this cache
    /// deliberately does NOT swallow — the Backend proxy maps it to a 503. Empty-but-successful
    /// lists ARE cached (a tenant may legitimately have no feature groups).
    /// </summary>
    public Task<IReadOnlyList<CustomerFeatureGroupView>> GetOrFetchAsync(
        int tenantId,
        string secretKey,
        CancellationToken ct = default)
    {
        var key = CacheKey(tenantId);
        if (_cache.TryGetValue<IReadOnlyList<CustomerFeatureGroupView>>(key, out var cached) && cached is not null)
            return Task.FromResult(cached);

        // Single-flight: concurrent misses for the same tenant share one task. Spawn the fetch
        // with CancellationToken.None so individual caller cancellation does NOT poison joined
        // awaiters; each caller still observes its own ct via WaitAsync below.
        var fetch = _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId, secretKey));
        return AwaitWithCallerCancellation(fetch, ct);
    }

    private static async Task<IReadOnlyList<CustomerFeatureGroupView>> AwaitWithCallerCancellation(
        Task<IReadOnlyList<CustomerFeatureGroupView>> fetch,
        CancellationToken ct)
    {
        return await fetch.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CustomerFeatureGroupView>> FetchAndCacheAsync(
        int tenantId,
        string secretKey)
    {
        try
        {
            var groups = await _client.GetCatalogAsync(tenantId, secretKey, CancellationToken.None).ConfigureAwait(false);
            _cache.Set(CacheKey(tenantId), groups, Ttl);
            return groups;
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    /// <summary>
    /// Drop the cached slot for the given tenant (powers the manual "Kataloğu yenile" button and
    /// any future settings-save hook). Also clears any in-flight fetch slot so the next caller
    /// starts a fresh fetch instead of awaiting a stale promise.
    /// </summary>
    public void Invalidate(int tenantId)
    {
        _cache.Remove(CacheKey(tenantId));
        _inflight.TryRemove(tenantId, out _);
    }

    private static string CacheKey(int tenantId) => $"wapcrm:featuregroups:{tenantId}";
}
