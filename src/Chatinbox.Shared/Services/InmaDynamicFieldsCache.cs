using System.Collections.Concurrent;
using Chatinbox.Shared.Contracts.Inma;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Chatinbox.Shared.Services;

/// <summary>
/// FEAT-DMP: tenant-scoped IMemoryCache wrapper for INMA dynamic-fields.
/// 1h TTL + manual <c>Invalidate</c> entry point for admin-triggered refresh.
/// Cache miss → <c>IInmaDynamicFieldsClient.GetFieldsAsync</c> → populate + return.
/// <para>
/// Iter 2 stampede fix: concurrent callers for the same tenant coalesce onto a
/// single in-flight <see cref="Task{T}"/> stored in <see cref="_inflight"/>; the
/// slot is cleared once the fetch resolves so a later invalidate + GET does not
/// get stuck on a stale promise. Exceptions propagate to all awaiters naturally.
/// </para>
/// <para>
/// 2026-04-22 cancellation-isolation fix (mirrors FEAT-TFM <c>DbTenantFieldMappingResolver</c>):
/// the in-flight fetch runs decoupled from any individual caller's token. The first caller
/// previously captured its <see cref="CancellationToken"/> inside the shared task factory,
/// so a Dashboard tab closing mid-flight cancelled the INMA call and poisoned every joined
/// awaiter with <see cref="OperationCanceledException"/>. Fix: factory uses
/// <see cref="CancellationToken.None"/>; per-caller cancellation is observed via
/// <see cref="Task.WaitAsync(CancellationToken)"/>, which throws for THAT caller only
/// without cancelling the underlying fetch or affecting other joined awaiters.
/// </para>
/// <para>
/// Secret-rotation note: the first caller's <paramref name="secretKey"/> wins inside a
/// single in-flight window. Concurrent callers racing with different secretKey values
/// share the first-caller result; after rotation, admins must call <see cref="Invalidate"/>
/// (exposed via <c>/api/v1/dynamic-fields/cache-invalidate</c>) for the next fetch to
/// pick up the new secret.
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
    /// failure; the cache deliberately does NOT swallow — the caller (Backend proxy)
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
        // Spawn fetch with CancellationToken.None so individual caller cancellation does
        // NOT poison joined awaiters. Each caller still observes its own ct via WaitAsync
        // below; cancel propagates only to the calling stack, not to the underlying fetch
        // nor to other concurrent waiters.
        var fetch = _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId, secretKey));
        return AwaitWithCallerCancellation(fetch, ct);
    }

    private static async Task<IReadOnlyList<InmaDynamicField>> AwaitWithCallerCancellation(
        Task<IReadOnlyList<InmaDynamicField>> fetch,
        CancellationToken ct)
    {
        // WaitAsync(ct) throws OperationCanceledException for THIS caller without
        // cancelling the underlying fetch task — joined callers stay safe.
        return await fetch.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<InmaDynamicField>> FetchAndCacheAsync(
        int tenantId,
        string secretKey)
    {
        // CancellationToken.None: see GetOrFetchAsync comment. The fetch must complete
        // (or naturally fail) regardless of any individual caller's cancel — joined
        // awaiters depend on it. HttpInmaDynamicFieldsClient uses a 5s HttpClient timeout
        // so the duration is bounded even without a caller ct.
        try
        {
            var fields = await _client.GetFieldsAsync(tenantId, secretKey, CancellationToken.None).ConfigureAwait(false);
            _cache.Set(CacheKey(tenantId), fields, Ttl);
            return fields;
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    /// <summary>
    /// Drop the cached slot for the given tenant. Next fetch repopulates under single-flight.
    /// <para>
    /// Race-safety (2026-04-22): also clears any in-flight fetch slot. If a fetch was
    /// running concurrently with Invalidate, the in-flight task may resolve with pre-update
    /// data and (in the prior implementation) re-populate cache after Remove. Clearing
    /// <see cref="_inflight"/> forces the next caller to start a fresh fetch instead of
    /// awaiting the stale promise. Currently awaiting callers still receive the stale
    /// value (cannot cancel mid-await); no new reader joins them and the cache stays
    /// empty until the fresh fetch completes.
    /// </para>
    /// </summary>
    public void Invalidate(int tenantId)
    {
        _cache.Remove(CacheKey(tenantId));
        _inflight.TryRemove(tenantId, out _);
    }

    private static string CacheKey(int tenantId) => $"inma:dynamicfields:{tenantId}";
}
