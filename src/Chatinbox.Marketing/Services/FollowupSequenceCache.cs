using System.Collections.Concurrent;
using Chatinbox.Marketing.Data;
using Chatinbox.Shared.Contracts.Followup;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Marketing.Services;

/// <summary>
/// Per-tenant single-flight cache for <see cref="FollowupSequenceConfig"/> lookups by slug.
/// Used by FollowupOrchestrator + FollowupStageJob hot paths so the same sequence row is
/// not re-fetched per stage during a single trigger burst.
///
/// Cancellation safety (lessons 2026-04-21 FEAT-TFM iter 3 CQ6+Q3 FAIL): the factory is
/// invoked WITHOUT the caller's CancellationToken — fetch runs under
/// <see cref="CancellationToken.None"/>, bound only by the underlying Npgsql command
/// timeout. Each caller observes its own CT via <c>WaitAsync(ct)</c>. This prevents the
/// classic <see cref="ConcurrentDictionary{TKey, TValue}"/> single-flight bug where the
/// first caller's CT cancels the shared task and poisons joined awaiters.
///
/// Eventual consistency: TTL <see cref="DefaultTtl"/> (5 minutes). Operators changing a
/// sequence via PUT /api/v1/followup/sequences also call <see cref="Invalidate"/> on the
/// receiving instance only — multi-instance invalidation is BACKLOG (mirrors FEAT-TFM-CACHE
/// B2 decision).
/// </summary>
public sealed class FollowupSequenceCache
{
    /// <summary>5-minute TTL — matches FEAT-TFM resolver convention.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly FollowupSequenceRepository _repo;
    private readonly JsonLinesLogger _log;
    private readonly TimeSpan _ttl;

    private readonly ConcurrentDictionary<CacheKey, CacheEntry> _entries = new();
    private readonly ConcurrentDictionary<CacheKey, Task<FollowupSequenceConfig?>> _inflight = new();

    public FollowupSequenceCache(
        FollowupSequenceRepository repo,
        JsonLinesLogger log,
        TimeSpan? ttl = null)
    {
        _repo = repo;
        _log = log;
        _ttl = ttl ?? DefaultTtl;
    }

    /// <summary>
    /// Get a sequence by (tenant, slug). Returns NULL if not found in DB. Cancellation
    /// observed per-caller; first caller's cancel does NOT cancel joined awaiters.
    /// </summary>
    public async Task<FollowupSequenceConfig?> GetOrFetchAsync(
        int tenantId, string slug, CancellationToken ct)
    {
        var key = new CacheKey(tenantId, slug);

        if (_entries.TryGetValue(key, out var entry) && entry.IsFresh(_ttl))
            return entry.Value;

        // Single-flight via _inflight; CT.None for the underlying fetch (lessons 2026-04-21).
        var fetch = _inflight.GetOrAdd(key, _ => RunFetchAsync(key));

        try
        {
            // WaitAsync(ct) isolates each caller's CT from the shared task.
            return await fetch.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            // Best-effort cleanup: remove the inflight entry if it's the one we observed.
            // _inflight may already be cleared by RunFetchAsync's finally; that is safe.
            _inflight.TryRemove(new KeyValuePair<CacheKey, Task<FollowupSequenceConfig?>>(key, fetch));
        }
    }

    /// <summary>Invalidate the cached entry for (tenant, slug). Called by upsert handlers.</summary>
    public void Invalidate(int tenantId, string slug)
    {
        _entries.TryRemove(new CacheKey(tenantId, slug), out _);
    }

    /// <summary>Tenant-wide invalidation (e.g. after multi-sequence upsert).</summary>
    public void InvalidateTenant(int tenantId)
    {
        foreach (var key in _entries.Keys)
        {
            if (key.TenantId == tenantId)
                _entries.TryRemove(key, out _);
        }
    }

    private async Task<FollowupSequenceConfig?> RunFetchAsync(CacheKey key)
    {
        try
        {
            var fetched = await _repo.FindBySlugAsync(key.TenantId, key.Slug, CancellationToken.None)
                .ConfigureAwait(false);
            if (fetched != null)
                _entries[key] = new CacheEntry(fetched, DateTime.UtcNow);
            return fetched;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // Typed catch (lessons 2026-04-21 Codex iter 0 CQ1/CQ5) — DB transport error.
            // Log + rethrow so joined awaiters surface a single observed failure; callers
            // (orchestrator / endpoint) map to INV-MK-056 FollowupStorageUnavailable.
            _log.SystemWarn(
                $"[{Chatinbox.Shared.Constants.ErrorCodes.FollowupStorageUnavailable}] FollowupSequenceCache: " +
                $"DB fetch failed (tenant={key.TenantId}, slug='{key.Slug}'): {ex.Message}");
            throw;
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Malformed stages JSONB — operator-visible distinct code so ops know to
            // inspect event_followup_sequences.stages for the offending row rather than
            // assuming a transient DB outage.
            _log.SystemWarn(
                $"[{Chatinbox.Shared.Constants.ErrorCodes.FollowupSequenceConfigInvalid}] FollowupSequenceCache: " +
                $"malformed stages JSONB (tenant={key.TenantId}, slug='{key.Slug}'): {ex.Message}");
            throw;
        }
    }

    private readonly record struct CacheKey(int TenantId, string Slug);

    private sealed record CacheEntry(FollowupSequenceConfig Value, DateTime FetchedAt)
    {
        public bool IsFresh(TimeSpan ttl) => DateTime.UtcNow - FetchedAt < ttl;
    }
}
