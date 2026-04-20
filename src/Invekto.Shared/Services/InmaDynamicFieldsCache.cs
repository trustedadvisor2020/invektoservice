using System.Collections.Concurrent;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Invekto.Shared.Services;

/// <summary>
/// FEAT-DMP: tenant-scoped IMemoryCache wrapper for INMA dynamic-fields.
/// 1h TTL + manual <c>Invalidate</c> entry point for admin-triggered refresh.
/// Cache miss \u2192 <c>IInmaDynamicFieldsClient.GetFieldsAsync</c> \u2192 populate + return.
/// <para>
/// Iter 2 stampede fix: concurrent callers for the same tenant coalesce onto a
/// single in-flight <see cref="Task{T}"/> stored in <see cref="_inflight"/>; the
/// slot is cleared once the fetch resolves so a later invalidate + GET does not
/// get stuck on a stale promise. Exceptions propagate to all awaiters naturally.
/// </para>
/// Register as singleton. Thread-safe via IMemoryCache + ConcurrentDictionary.
/// </summary>
public sealed class InmaDynamicFieldsCache
{
    private readonly IMemoryCache _cache;
    private readonly IInmaDynamicFieldsClient _client;
    private readonly ConcurrentDictionary<int, Task<IReadOnlyList<InmaDynamicField>>> _inflight = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public InmaDynamicFieldsCache(IMemoryCache cache, IInmaDynamicFieldsClient client)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Cache-first lookup with single-flight coalescing. Cache hit returns the stored
    /// list. Cache miss either joins an in-flight fetch for the same tenant or starts a
    /// new one. The client throws <see cref="InmaDynamicFieldsFetchException"/> on upstream
    /// failure; the cache deliberately does NOT swallow \u2014 the caller (Backend proxy)
    /// maps it to 503 so the UI can distinguish fetch-fail from empty-list.
    /// Empty-but-successful lists are cached (legitimate tenant state with no custom fields).
    /// </summary>
    public Task<IReadOnlyList<InmaDynamicField>> GetOrFetchAsync(
        int tenantId,
        string secretKey,
        CancellationToken ct = default)
    {
        var key = CacheKey(tenantId);
        if (_cache.TryGetValue<IReadOnlyList<InmaDynamicField>>(key, out var cached) && cached is not null)
            return Task.FromResult(cached);

        // Single-flight: concurrent misses for the same tenant share one task.
        return _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId, secretKey, ct));
    }

    private async Task<IReadOnlyList<InmaDynamicField>> FetchAndCacheAsync(
        int tenantId,
        string secretKey,
        CancellationToken ct)
    {
        try
        {
            var fields = await _client.GetFieldsAsync(tenantId, secretKey, ct);
            _cache.Set(CacheKey(tenantId), fields, Ttl);
            return fields;
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    /// <summary>Drop the cached slot for the given tenant. Next fetch repopulates under single-flight.</summary>
    public void Invalidate(int tenantId) => _cache.Remove(CacheKey(tenantId));

    private static string CacheKey(int tenantId) => $"inma:dynamicfields:{tenantId}";
}
