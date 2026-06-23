using Chatinbox.Shared.Contracts.Campaigns.Dtos;

namespace Chatinbox.Shared.Contracts.Campaigns;

/// <summary>
/// FEAT-MCC: tenant-scoped campaign config reader + window guard. Single resolver
/// shared across Backend (PUT invalidate, GET render), Automation (substitution +
/// outbound window guard), and Marketing (FollowupStageJob window guard).
///
/// Cache: 5dk TTL per tenant + single-flight (concurrent misses share one DB fetch).
/// Backend PUT calls <see cref="Invalidate"/> after a successful upsert so the next
/// resolver call reads fresh state. Multi-instance: invalidation runs on the receiving
/// Backend instance only; peer Automation/Marketing instances pick up new state on
/// their own 5dk TTL expiry (eventual consistency, MVP — same trade-off as
/// FEAT-TFM <see cref="TenantFieldMapping.DbTenantFieldMappingResolver"/>).
///
/// Failure semantics: any DB / JSON failure returns <see cref="CampaignConfig.Empty"/>
/// + WARN log under INV-BE-121 (DB) or INV-BE-118 (JSON malformed). Resolver fail must
/// NOT crash the outbound path — the empty config makes window guard / substitution
/// no-op, preserving outbound flow for campaign-agnostic messages. The tenant operator
/// sees an empty Dashboard editor which signals the issue.
/// </summary>
public interface ITenantCampaignResolver
{
    /// <summary>Read the full campaign config for a tenant. Empty config when row missing
    /// or column unset — never null.</summary>
    Task<CampaignConfig> GetAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Drop any cached config for the given tenant. Backend PUT calls this after upsert.</summary>
    void Invalidate(int tenantId);

    /// <summary>
    /// Substitution helper: render a value for a <c>{{campaign.X}}</c> placeholder. Supported keys
    /// (subset documented in arch/features/multi-city-campaign.md Section 4 + interview Q3 answer):
    ///
    /// <list type="bullet">
    ///   <item><c>slug</c> → resolved campaign slug.</item>
    ///   <item><c>name</c> → resolved campaign display name.</item>
    ///   <item><c>start_date</c> / <c>end_date</c> → ISO-8601 strings.</item>
    ///   <item><c>cities_human</c> → locale-aware join (en: "Dublin and Cork"; tr: "Dublin ve Cork").</item>
    ///   <item><c>cities_csv</c>   → comma-space join ("Dublin, Cork").</item>
    ///   <item><c>cities_json</c>  → JSON array string (`["Dublin","Cork"]`).</item>
    ///   <item><c>event_date</c>   → ISO date for the lead's preferred city if <paramref name="leadCity"/>
    ///       is non-null and matches a campaigns[X].dates[].city; otherwise the first dates[] entry
    ///       (lead-aware override per interview Q4).</item>
    ///   <item><c>event_hours</c>  → display hours for the same matched dates[] entry.</item>
    /// </list>
    ///
    /// Resolution order: caller passes <paramref name="campaignSlug"/>; if null, the resolver picks
    /// the first <c>active=true</c> campaign whose <see cref="CampaignEntry.StartDate"/>..<see cref="CampaignEntry.EndDate"/>
    /// covers <paramref name="nowUtc"/> (in tenant timezone). Returns empty string when:
    /// no campaigns configured, key unknown, or no in-window active campaign.
    /// </summary>
    Task<string> RenderPlaceholderAsync(
        int tenantId,
        string placeholderTail,
        string? campaignSlug,
        string? leadCity,
        string locale,
        DateTimeOffset nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Window guard for outbound dispatch: returns true when the resolved campaign is
    /// active and NOW is within [start_date, end_date] inclusive in tenant timezone.
    /// When <paramref name="campaignSlug"/> is null, evaluates against ANY active+in-window
    /// campaign (true if at least one passes).
    /// </summary>
    Task<bool> IsWithinWindowAsync(
        int tenantId,
        string? campaignSlug,
        DateTimeOffset nowUtc,
        CancellationToken ct = default);
}
