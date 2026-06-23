using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Campaigns.Dtos;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Chatinbox.Shared.Contracts.Campaigns;

/// <summary>
/// FEAT-MCC: DB-backed implementation of <see cref="ITenantCampaignResolver"/>.
/// Reads <c>tenant_settings.campaign_config JSONB</c> + <c>tenant_settings.timezone</c>
/// in a single composite cache entry so the window guard does not pay a second round-trip.
///
/// <para>
/// Cache: 5dk TTL per tenant + single-flight (CT-safe pattern from FEAT-TFM lessons
/// 2026-04-21 iter 3 CQ6+Q3). Per-caller awaits via <c>WaitAsync(ct)</c> so caller
/// cancellation does NOT poison joined awaiters. Backend PUT calls
/// <see cref="Invalidate"/> after a successful upsert.
/// </para>
///
/// <para>
/// Failure semantics: any DB error returns <see cref="CampaignConfig.Empty"/> + WARN
/// log under INV-BE-121. JSON malformed → INV-BE-118 + empty config. Empty config makes
/// window guard / substitution no-op so outbound flow continues for campaign-agnostic
/// messages — operator sees an empty Dashboard which signals the issue.
/// </para>
///
/// <para>
/// Multi-instance: cache is local per process (Backend, Automation, Marketing each carry
/// their own). Backend PUT invalidate runs on the receiving instance only — peers
/// pick up new state on TTL expiry (5dk eventual consistency, MVP). Cross-instance
/// invalidation deferred to a future paket parallel to FEAT-TFM-CACHE backlog.
/// </para>
/// </summary>
public sealed class DbTenantCampaignResolver : ITenantCampaignResolver
{
    private readonly PostgresConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private readonly JsonLinesLogger _log;
    private readonly ConcurrentDictionary<int, Task<CachedTenantCampaign>> _inflight = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Snake_case JSON contract for campaign_config — matches the DTO JsonPropertyName attributes
    /// and the canonical persisted shape so PUT round-trips do not mutate slug/date/hours casing.</summary>
    public static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DbTenantCampaignResolver(
        PostgresConnectionFactory db,
        IMemoryCache cache,
        JsonLinesLogger log)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<CampaignConfig> GetAsync(int tenantId, CancellationToken ct = default)
    {
        var cached = await GetOrFetchAsync(tenantId, ct).ConfigureAwait(false);
        return cached.Config;
    }

    public void Invalidate(int tenantId)
    {
        _cache.Remove(CacheKey(tenantId));
        _inflight.TryRemove(tenantId, out _);
    }

    public async Task<bool> IsWithinWindowAsync(
        int tenantId,
        string? campaignSlug,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        var cached = await GetOrFetchAsync(tenantId, ct).ConfigureAwait(false);
        if (cached.Config.Campaigns.Count == 0) return false;

        var nowLocal = ToTenantLocal(nowUtc, cached.TenantTimezone);

        if (!string.IsNullOrEmpty(campaignSlug))
        {
            var match = FindBySlug(cached.Config, campaignSlug);
            return match is not null && CampaignCoversLocalNow(match, nowLocal);
        }

        // No specific slug: pass if at least one active campaign covers NOW.
        return cached.Config.Campaigns.Any(c => CampaignCoversLocalNow(c, nowLocal));
    }

    public async Task<string> RenderPlaceholderAsync(
        int tenantId,
        string placeholderTail,
        string? campaignSlug,
        string? leadCity,
        string locale,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(placeholderTail)) return string.Empty;

        var cached = await GetOrFetchAsync(tenantId, ct).ConfigureAwait(false);
        if (cached.Config.Campaigns.Count == 0) return string.Empty;

        var nowLocal = ToTenantLocal(nowUtc, cached.TenantTimezone);

        // Resolve which campaign to render against. Caller-supplied slug wins; otherwise
        // the first active+in-window campaign (interview Q3+Q5: array-of-campaigns shape).
        var campaign = !string.IsNullOrEmpty(campaignSlug)
            ? FindBySlug(cached.Config, campaignSlug)
            : cached.Config.Campaigns.FirstOrDefault(c => CampaignCoversLocalNow(c, nowLocal))
              ?? cached.Config.Campaigns.FirstOrDefault(c => c.Active);
        if (campaign is null) return string.Empty;

        return placeholderTail switch
        {
            "slug"        => campaign.Slug,
            "name"        => campaign.Name,
            "start_date"  => campaign.StartDate,
            "end_date"    => campaign.EndDate,
            "cities_human" => RenderCitiesHuman(campaign, locale),
            "cities_csv"  => string.Join(", ", campaign.Cities.Select(c => c.Name)),
            "cities_json" => JsonSerializer.Serialize(campaign.Cities.Select(c => c.Name).ToArray()),
            "event_date"  => ResolveDateField(campaign, leadCity, d => d.Date),
            "event_hours" => ResolveDateField(campaign, leadCity, d => d.Hours ?? string.Empty),
            _ => string.Empty
        };
    }

    private static CampaignEntry? FindBySlug(CampaignConfig config, string slug)
        => config.Campaigns.FirstOrDefault(
            c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static bool CampaignCoversLocalNow(CampaignEntry campaign, DateOnly nowLocal)
    {
        if (!campaign.Active) return false;
        if (!DateOnly.TryParse(campaign.StartDate, out var start)) return false;
        if (!DateOnly.TryParse(campaign.EndDate, out var end)) return false;
        return nowLocal >= start && nowLocal <= end; // Inclusive both edges (interview Q2).
    }

    private static DateOnly ToTenantLocal(DateTimeOffset utc, string? tenantTimezone)
    {
        if (string.IsNullOrWhiteSpace(tenantTimezone))
            return DateOnly.FromDateTime(utc.UtcDateTime);

        try
        {
            var tzi = TimeZoneInfo.FindSystemTimeZoneById(tenantTimezone);
            var local = TimeZoneInfo.ConvertTime(utc, tzi);
            return DateOnly.FromDateTime(local.DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(utc.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(utc.UtcDateTime);
        }
    }

    private static string RenderCitiesHuman(CampaignEntry campaign, string locale)
    {
        var names = campaign.Cities.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        if (names.Length == 0) return string.Empty;
        if (names.Length == 1) return names[0];

        // Locale-aware "and" join. tr-* uses "ve", everything else falls back to "and"/"&" English.
        var conjunction = locale != null && locale.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? "ve"
            : "and";

        if (names.Length == 2) return $"{names[0]} {conjunction} {names[1]}";

        // 3+: comma-join all but last, then conjunction last (Oxford-style without comma — locale-safe).
        var head = string.Join(", ", names.Take(names.Length - 1));
        return $"{head} {conjunction} {names[^1]}";
    }

    /// <summary>Lead-aware date lookup: prefers a dates[] entry whose city matches
    /// <paramref name="leadCity"/>; otherwise returns the first dates[] entry value.
    /// Empty string when dates[] is empty (interview Q4: lead.custom_1 &gt; tenant default).</summary>
    private static string ResolveDateField(CampaignEntry campaign, string? leadCity, Func<CampaignDate, string> projector)
    {
        if (campaign.Dates.Count == 0) return string.Empty;
        if (!string.IsNullOrWhiteSpace(leadCity))
        {
            var match = campaign.Dates.FirstOrDefault(
                d => string.Equals(d.City, leadCity, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return projector(match) ?? string.Empty;
        }
        return projector(campaign.Dates[0]) ?? string.Empty;
    }

    private Task<CachedTenantCampaign> GetOrFetchAsync(int tenantId, CancellationToken ct)
    {
        if (_cache.TryGetValue<CachedTenantCampaign>(CacheKey(tenantId), out var cached) && cached is not null)
            return Task.FromResult(cached);

        // CT-safe single-flight (FEAT-TFM lessons 2026-04-21 iter 3): factory uses CancellationToken.None
        // so per-caller cancel does NOT poison joined awaiters.
        var fetch = _inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId));
        return AwaitWithCallerCancellation(fetch, ct);
    }

    private static async Task<CachedTenantCampaign> AwaitWithCallerCancellation(
        Task<CachedTenantCampaign> fetch, CancellationToken ct)
    {
        return await fetch.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<CachedTenantCampaign> FetchAndCacheAsync(int tenantId)
    {
        var logCorrelationId = $"mcc:{tenantId}";
        try
        {
            var loaded = await LoadFromDbAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
            _cache.Set(CacheKey(tenantId), loaded, CacheTtl);
            return loaded;
        }
        catch (NpgsqlException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.CampaignConfigDbUnavailable}] DbTenantCampaignResolver DB fail (tenant={tenantId}): {ex.Message} — falling back to empty config (window guard becomes no-op for campaign-agnostic dispatch).",
                logCorrelationId);
            return CachedTenantCampaign.EmptyFor(tenantTimezone: null);
        }
        catch (JsonException ex)
        {
            _log.StepWarn(
                $"[{ErrorCodes.CampaignConfigInvalid}] DbTenantCampaignResolver JSON parse fail (tenant={tenantId}): {ex.Message} — tenant_settings.campaign_config malformed; treating as empty. Operator: SELECT campaign_config::text FROM tenant_settings WHERE tenant_id={tenantId}; ile veri kontrolu yapip PUT ile dogru JSON yazilsin.",
                logCorrelationId);
            return CachedTenantCampaign.EmptyFor(tenantTimezone: null);
        }
        finally
        {
            _inflight.TryRemove(tenantId, out _);
        }
    }

    private async Task<CachedTenantCampaign> LoadFromDbAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT campaign_config::text, timezone
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return CachedTenantCampaign.EmptyFor(tenantTimezone: null);

        var json = reader.IsDBNull(0) ? null : reader.GetString(0);
        var tz = reader.IsDBNull(1) ? null : reader.GetString(1);

        if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "{\"campaigns\":[]}")
            return CachedTenantCampaign.EmptyFor(tz);

        var parsed = JsonSerializer.Deserialize<CampaignConfig>(json, SnakeCaseJson)
                     ?? CampaignConfig.Empty();
        return new CachedTenantCampaign(parsed, tz);
    }

    private static string CacheKey(int tenantId) => $"tenant:campaign_config:{tenantId}";

    private sealed record CachedTenantCampaign(CampaignConfig Config, string? TenantTimezone)
    {
        public static CachedTenantCampaign EmptyFor(string? tenantTimezone)
            => new(CampaignConfig.Empty(), tenantTimezone);
    }
}
