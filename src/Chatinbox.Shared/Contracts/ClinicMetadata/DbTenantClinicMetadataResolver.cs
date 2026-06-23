using System.Collections.Concurrent;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Chatinbox.Shared.Contracts.ClinicMetadata;

/// <summary>
/// FEAT-CLINIC-METADATA: DB-backed implementation of <see cref="ITenantClinicMetadataResolver"/>.
/// Reads <c>tenant_settings.clinic_contact JSONB</c> + <c>tenant_settings.team_members JSONB</c>
/// in a single composite cache entry so {{clinic.*}} + {{team.*}} substitution does not pay
/// a second round-trip.
///
/// <para>
/// Cache: 5dk TTL per tenant + single-flight (CT-safe pattern from FEAT-TFM lessons
/// 2026-04-21 iter 3 CQ6+Q3 — per-caller awaits via <c>WaitAsync(ct)</c> so caller
/// cancellation does NOT poison joined awaiters). Backend PUT calls
/// <see cref="Invalidate"/> after a successful upsert.
/// </para>
///
/// <para>
/// Failure semantics: any DB error returns <see cref="ClinicMetadata.Empty"/> + WARN
/// log under INV-BE-125. JSON malformed → INV-BE-122 + empty metadata. Empty makes
/// substitution no-op so outbound flow continues for clinic-agnostic messages —
/// operator sees an empty Dashboard which signals the issue.
/// </para>
///
/// <para>
/// Multi-instance: cache is local per process (Backend, Automation each carry their own).
/// Backend PUT invalidate runs on the receiving instance only — peers pick up new state
/// on TTL expiry (5dk eventual consistency, MVP). Cross-instance invalidation deferred
/// to a future paket parallel to FEAT-TFM-CACHE backlog.
/// </para>
/// </summary>
public sealed class DbTenantClinicMetadataResolver : ITenantClinicMetadataResolver
{
    private readonly PostgresConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private readonly JsonLinesLogger _log;
    private readonly ConcurrentDictionary<int, Task<ClinicMetadata>> _inflight = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Snake_case JSON contract — matches DTO JsonPropertyName attributes and the
    /// canonical JSONB shape so PUT round-trips do not mutate field casing.</summary>
    public static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DbTenantClinicMetadataResolver(
        PostgresConnectionFactory db,
        IMemoryCache cache,
        JsonLinesLogger log)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ClinicMetadata> GetAsync(int tenantId, CancellationToken ct = default)
    {
        return await GetOrFetchAsync(tenantId, ct).ConfigureAwait(false);
    }

    public void Invalidate(int tenantId)
    {
        _cache.Remove(CacheKey(tenantId));
        _inflight.TryRemove(tenantId, out _);
    }

    private Task<ClinicMetadata> GetOrFetchAsync(int tenantId, CancellationToken ct)
    {
        if (_cache.TryGetValue<ClinicMetadata>(CacheKey(tenantId), out var cached) && cached is not null)
            return Task.FromResult(cached);

        // CT-safe single-flight (FEAT-TFM lessons 2026-04-21 iter 3): factory uses
        // CancellationToken.None so per-caller cancel does NOT poison joined awaiters.
        var fetch = _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId));
        return AwaitWithCallerCancellation(fetch, ct);
    }

    private static async Task<ClinicMetadata> AwaitWithCallerCancellation(
        Task<ClinicMetadata> fetch, CancellationToken ct)
    {
        return await fetch.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<ClinicMetadata> FetchAndCacheAsync(int tenantId)
    {
        var logCorrelationId = $"clinic_metadata:{tenantId}";
        try
        {
            var loaded = await LoadFromDbAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
            _cache.Set(CacheKey(tenantId), loaded, CacheTtl);
            return loaded;
        }
        catch (NpgsqlException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.ClinicMetadataDbUnavailable}] DbTenantClinicMetadataResolver DB fail (tenant={tenantId}): {ex.Message} — falling back to empty metadata (clinic/team substitution becomes no-op for clinic-agnostic dispatch).",
                logCorrelationId);
            return ClinicMetadata.Empty();
        }
        catch (JsonException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.ClinicMetadataInvalidJson}] DbTenantClinicMetadataResolver JSON parse fail (tenant={tenantId}): {ex.Message} — tenant_settings.clinic_contact or team_members malformed; treating as empty. Operator: SELECT clinic_contact::text, team_members::text FROM tenant_settings WHERE tenant_id={tenantId}; verify shape and PUT corrected JSON via Dashboard.",
                logCorrelationId);
            return ClinicMetadata.Empty();
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    private async Task<ClinicMetadata> LoadFromDbAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT clinic_contact::text, team_members::text
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return ClinicMetadata.Empty();

        var contactJson = reader.IsDBNull(0) ? null : reader.GetString(0);
        var teamJson    = reader.IsDBNull(1) ? null : reader.GetString(1);

        var contact = ParseContact(contactJson);
        var team = ParseTeam(teamJson);

        return new ClinicMetadata { Contact = contact, Team = team };
    }

    private static ClinicContactDto ParseContact(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return ClinicContactDto.Empty();

        return JsonSerializer.Deserialize<ClinicContactDto>(json, SnakeCaseJson)
               ?? ClinicContactDto.Empty();
    }

    private static List<TeamMemberDto> ParseTeam(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<TeamMemberDto>();

        return JsonSerializer.Deserialize<List<TeamMemberDto>>(json, SnakeCaseJson)
               ?? new List<TeamMemberDto>();
    }

    private static string CacheKey(int tenantId) => $"tenant:clinic_metadata:{tenantId}";
}
