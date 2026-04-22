using System.Text.RegularExpressions;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Campaigns;
using Invekto.Shared.Contracts.Campaigns.Dtos;

namespace Invekto.Shared.Services;

/// <summary>
/// FEAT-MCC: validates <see cref="CampaignConfig"/> before UPSERT (PUT endpoint call site).
/// Fail-fast — throws <see cref="TenantCampaignConfigValidationException"/> on the first
/// failing rule. Caller translates the exception into a 400 response + bracketed INV-BE-* code.
///
/// Interview-driven rules (arch/plans/20260425-feat-mcc-multi-city.json):
///   * Slug regex: lowercase start, [a-z0-9_-]{1,63}; reserved set rejected (INV-BE-120).
///   * Max 8 campaigns per tenant — prevents JSONB bloat + Dashboard-editor scroll hell.
///   * Max 20 cities per campaign; max 20 dates per campaign.
///   * start_date &lt;= end_date; both ISO-8601 calendar dates (no time component).
///   * cities[].slug regex [a-z0-9_-]{1,40}; non-empty name; cities not empty.
///   * dates[].city MUST reference a known cities[].slug in the same campaign.
///   * dates[].date parseable via DateOnly.
///   * hours display text bounded to 64 chars (safety cap on template substitution size).
///
/// <para>
/// Codex notes: validator is static + throws on first failure — this keeps endpoint call
/// sites single-statement without a Results&lt;T,Error&gt; shape. Pattern mirrors
/// <see cref="TenantFieldMappingValidator"/> (FEAT-TFM precedent).
/// </para>
/// </summary>
public static class TenantCampaignConfigValidator
{
    private static readonly Regex SlugPattern = new(
        @"^[a-z][a-z0-9_-]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CitySlugPattern = new(
        @"^[a-z][a-z0-9_-]{0,39}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ReservedSlugs = new(
        new[] { "primary", "system", "default", "all" },
        StringComparer.OrdinalIgnoreCase);

    private const int MaxCampaignsPerTenant = 8;
    private const int MaxCitiesPerCampaign = 20;
    private const int MaxDatesPerCampaign = 20;
    private const int MaxHoursLength = 64;

    /// <summary>
    /// Validate a full campaign config. Empty (zero campaigns) is valid; null is rejected
    /// fail-fast so silent-success paths cannot hide malformed deserializer output. Endpoints
    /// also reject null bodies upstream; this guard is the validator's structural baseline.
    /// </summary>
    public static void Validate(CampaignConfig config)
    {
        if (config is null)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                string.Empty,
                "campaign_config",
                "campaign_config govdesi bos veya parse edilemedi; { \"campaigns\": [...] } sema ile gonderin.");
        }
        var campaigns = config.Campaigns ?? new List<CampaignEntry>();

        if (campaigns.Count > MaxCampaignsPerTenant)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                string.Empty,
                "campaigns",
                $"Tenant en fazla {MaxCampaignsPerTenant} kampanya tanimlayabilir (gelen: {campaigns.Count}).");
        }

        var slugsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var campaign in campaigns)
        {
            if (campaign is null)
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    string.Empty,
                    "campaigns[i]",
                    "campaigns[] icinde null girdi var; her kampanya icin JSON nesnesi gerekli.");
            }
            ValidateCampaign(campaign, slugsSeen);
        }
    }

    private static void ValidateCampaign(CampaignEntry campaign, HashSet<string> slugsSeen)
    {
        var slug = campaign.Slug ?? string.Empty;

        if (!SlugPattern.IsMatch(slug))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "slug",
                $"Kampanya slug gecersiz: '{slug}'. Kucuk harfle baslamali, sadece [a-z0-9_-], 2-64 karakter.");
        }

        if (ReservedSlugs.Contains(slug))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignSlugReserved,
                slug,
                "slug",
                $"Slug '{slug}' sistem rezerv kelimesi (primary/system/default/all); lutfen alan-anlamli baska bir slug secin.");
        }

        if (!slugsSeen.Add(slug))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "slug",
                $"Ayni slug '{slug}' birden fazla kampanyada kullanilmis; tenant icinde slug'lar benzersiz olmali.");
        }

        if (string.IsNullOrWhiteSpace(campaign.Name))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "name",
                $"Kampanya adi bos: '{slug}'.");
        }

        // Date ordering — inclusive both sides (Q interview Q2 answer).
        if (!DateOnly.TryParse(campaign.StartDate, out var start))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "start_date",
                $"start_date ISO-8601 formatinda (YYYY-MM-DD) olmali: '{campaign.StartDate}' ('{slug}').");
        }
        if (!DateOnly.TryParse(campaign.EndDate, out var end))
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "end_date",
                $"end_date ISO-8601 formatinda (YYYY-MM-DD) olmali: '{campaign.EndDate}' ('{slug}').");
        }
        if (end < start)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "end_date",
                $"end_date ({end:yyyy-MM-dd}) start_date'ten ({start:yyyy-MM-dd}) once olamaz ('{slug}').");
        }

        ValidateCities(campaign, slug);
        ValidateDates(campaign, slug);
    }

    private static void ValidateCities(CampaignEntry campaign, string slug)
    {
        var cities = campaign.Cities ?? new List<CampaignCity>();
        if (cities.Count == 0)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "cities",
                $"Kampanya en az bir sehir icermeli ('{slug}').");
        }
        if (cities.Count > MaxCitiesPerCampaign)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "cities",
                $"Kampanya en fazla {MaxCitiesPerCampaign} sehir icerebilir (gelen: {cities.Count}, '{slug}').");
        }

        var citySlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < cities.Count; i++)
        {
            var city = cities[i];
            if (city is null)
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"cities[{i}]",
                    $"cities[{i}] null girdi; her sehir icin JSON nesnesi gerekli ('{slug}').");
            }
            var citySlug = city.Slug ?? string.Empty;
            if (!CitySlugPattern.IsMatch(citySlug))
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"cities[{i}].slug",
                    $"City slug gecersiz: '{citySlug}' ('{slug}'). Kucuk harfle baslamali, [a-z0-9_-], max 40 karakter.");
            }
            if (!citySlugs.Add(citySlug))
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"cities[{i}].slug",
                    $"Ayni city slug '{citySlug}' birden fazla kullanilmis ('{slug}').");
            }
            if (string.IsNullOrWhiteSpace(city.Name))
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"cities[{i}].name",
                    $"City adi bos: '{citySlug}' ('{slug}').");
            }
        }
    }

    private static void ValidateDates(CampaignEntry campaign, string slug)
    {
        var dates = campaign.Dates ?? new List<CampaignDate>();
        if (dates.Count > MaxDatesPerCampaign)
        {
            throw new TenantCampaignConfigValidationException(
                ErrorCodes.CampaignConfigInvalid,
                slug,
                "dates",
                $"Kampanya en fazla {MaxDatesPerCampaign} tarih icerebilir (gelen: {dates.Count}, '{slug}').");
        }

        var citySlugLookup = new HashSet<string>(
            (campaign.Cities ?? new List<CampaignCity>()).Select(c => c?.Slug ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < dates.Count; i++)
        {
            var entry = dates[i];
            if (entry is null)
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"dates[{i}]",
                    $"dates[{i}] null girdi; her tarih icin JSON nesnesi gerekli ('{slug}').");
            }
            var citySlug = entry.City ?? string.Empty;
            if (!citySlugLookup.Contains(citySlug))
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"dates[{i}].city",
                    $"dates[{i}].city '{citySlug}' cities[] icinde yok ('{slug}'). Once ilgili sehri ekleyin.");
            }
            if (!DateOnly.TryParse(entry.Date, out _))
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"dates[{i}].date",
                    $"dates[{i}].date ISO-8601 (YYYY-MM-DD) olmali: '{entry.Date}' ('{slug}').");
            }
            if (!string.IsNullOrEmpty(entry.Hours) && entry.Hours.Length > MaxHoursLength)
            {
                throw new TenantCampaignConfigValidationException(
                    ErrorCodes.CampaignConfigInvalid,
                    slug,
                    $"dates[{i}].hours",
                    $"dates[{i}].hours {MaxHoursLength} karakterden uzun olamaz ('{slug}').");
            }
        }
    }
}
