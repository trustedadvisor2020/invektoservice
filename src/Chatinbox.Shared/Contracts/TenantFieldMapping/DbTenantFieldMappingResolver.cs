using System.Collections.Concurrent;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.TenantFieldMapping.Dtos;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Chatinbox.Shared.Contracts.TenantFieldMapping;

/// <summary>
/// FEAT-TFM MVP: DB-backed resolver replacing <see cref="NullTenantFieldMappingResolver"/>.
/// Reads <c>tenant_settings.field_mapping JSONB</c>, returns the INMA key (cf1..cf10) for
/// a semantic placeholder. Mapping miss / unknown placeholder → null so the DMP
/// <c>DynamicMessageValidator</c> raw INMA-key allowlist fallback stays intact.
///
/// Cache: 5dk TTL per tenant + single-flight coalescing (concurrent misses share one DB
/// fetch). Invalidate() called by Backend PUT endpoint after a successful upsert.
///
/// Multi-instance note: cache is local per process. Outbound/Automation instances
/// observe new mappings within the 5dk TTL (eventual consistency — acceptable for MVP).
/// Cross-instance invalidation deferred to FEAT-TFM-CACHE.
///
/// Failure semantics: any DB / JSON failure logs WARN with TFM-specific INV codes
/// (INV-BE-110 for DB unavailable, INV-BE-096 for malformed JSON) and returns null.
/// Resolver fail must not crash the placeholder substitution path — DMP raw-key
/// fallback handles it. Register as singleton.
/// </summary>
public sealed class DbTenantFieldMappingResolver : ITenantFieldMappingResolver
{
    private readonly PostgresConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private readonly JsonLinesLogger _log;
    private readonly ConcurrentDictionary<int, Task<IReadOnlyDictionary<string, string>>> _inflight = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Snake_case JSON contract for tenant_settings.field_mapping. Matches AC2 plan
    /// requirement (PropertyNamingPolicy.SnakeCaseLower) so even fields without an
    /// explicit JsonPropertyName attribute serialize/deserialize as snake_case.
    /// Reused for both Deserialize (load) and any future Serialize (debug dump).
    /// </summary>
    public static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null // Semantic names are user-supplied; preserve casing.
    };

    public DbTenantFieldMappingResolver(
        PostgresConnectionFactory db,
        IMemoryCache cache,
        JsonLinesLogger log)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<string?> ResolveToInmaKeyAsync(int tenantId, string placeholder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) return null;

        var map = await GetOrFetchMapAsync(tenantId, ct).ConfigureAwait(false);
        return map.TryGetValue(placeholder, out var inmaKey) ? inmaKey : null;
    }

    /// <summary>
    /// Drop the cached mapping for the given tenant. Called by Backend PUT after upsert.
    /// Local-process only (see multi-instance note in class summary).
    /// <para>
    /// Race-safety: also clears any in-flight fetch slot. If a fetch was running concurrently
    /// with PUT, the in-flight task may resolve with pre-update data and (in the prior
    /// implementation) re-populate cache after Remove. Clearing _inflight forces the next
    /// caller to start a fresh DB fetch instead of awaiting the stale promise. The currently
    /// awaiting callers still receive the stale value (cannot cancel mid-await), but no new
    /// reader joins them and the cache stays empty until the fresh fetch completes.
    /// </para>
    /// </summary>
    public void Invalidate(int tenantId)
    {
        _cache.Remove(CacheKey(tenantId));
        _inflight.TryRemove(tenantId, out _);
    }

    private Task<IReadOnlyDictionary<string, string>> GetOrFetchMapAsync(int tenantId, CancellationToken ct)
    {
        if (_cache.TryGetValue<IReadOnlyDictionary<string, string>>(CacheKey(tenantId), out var cached) && cached is not null)
            return Task.FromResult(cached);

        // Single-flight: spawn fetch with CancellationToken.None so individual caller
        // cancellation does NOT poison joined awaiters. Each caller still observes its
        // own ct via the await of the shared task — caller-cancel propagates only to
        // the calling stack, not to the underlying fetch nor to other concurrent waiters.
        var fetch = _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId));
        return AwaitWithCallerCancellation(fetch, ct);
    }

    private static async Task<IReadOnlyDictionary<string, string>> AwaitWithCallerCancellation(
        Task<IReadOnlyDictionary<string, string>> fetch,
        CancellationToken ct)
    {
        // WaitAsync(ct) throws OperationCanceledException for THIS caller without
        // cancelling the underlying fetch task — joined callers stay safe.
        return await fetch.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> FetchAndCacheAsync(int tenantId)
    {
        // CancellationToken.None: see GetOrFetchMapAsync comment. The fetch must complete
        // (or naturally fail) regardless of any individual caller's cancel — joined awaiters
        // depend on it. Network/DB timeouts in PostgresConnectionFactory bound the duration.
        var logCorrelationId = $"tfm:{tenantId}";
        try
        {
            var map = await LoadFromDbAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
            _cache.Set(CacheKey(tenantId), map, CacheTtl);
            return map;
        }
        catch (NpgsqlException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.FieldMappingDbUnavailable}] DbTenantFieldMappingResolver DB fail (tenant={tenantId}): {ex.Message} — falling back to empty map (DMP raw-key allowlist still active).",
                logCorrelationId);
            return EmptyMap;
        }
        catch (JsonException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.FieldMappingInvalid}] DbTenantFieldMappingResolver JSON parse fail (tenant={tenantId}): {ex.Message} — tenant_settings.field_mapping malformed; treating as empty. Operator: SELECT field_mapping::text FROM tenant_settings WHERE tenant_id={tenantId} ile veri kontrolu yapin, gerekiyorsa PUT ile dogru JSON ile uzeri yazilsin.",
                logCorrelationId);
            return EmptyMap;
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadFromDbAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT field_mapping::text
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (raw is not string json || string.IsNullOrWhiteSpace(json) || json == "{}")
            return EmptyMap;

        var entries = JsonSerializer.Deserialize<Dictionary<string, TenantFieldMappingEntry>>(json, SnakeCaseJson);
        if (entries is null || entries.Count == 0) return EmptyMap;

        // Project to semantic→source (INMA key) for hot-path lookups.
        var map = new Dictionary<string, string>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (semantic, entry) in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry?.Source))
                map[semantic] = entry.Source;
        }
        return map;
    }

    private static string CacheKey(int tenantId) => $"tenant:field_mapping:{tenantId}";
}
