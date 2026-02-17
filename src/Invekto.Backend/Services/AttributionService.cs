using Invekto.Shared.DTOs.Attribution;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services;

/// <summary>
/// GR-3.14: Attribution business logic.
/// Auto-detects lead_source from UTM data and meta_click_id.
/// Thread-safe singleton (no mutable state).
/// </summary>
public sealed class AttributionService
{
    private readonly AttributionRepository _repo;
    private readonly JsonLinesLogger _logger;

    public AttributionService(AttributionRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Process attribution from webhook conversation_started event.
    /// Inline call - must be fast (target < 5ms).
    /// Returns inserted lead ID or 0 if no attribution data present.
    /// </summary>
    public async Task<int> TrackFromWebhookAsync(
        int tenantId, IncomingWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        var data = webhookEvent.Data;
        if (data == null) return 0;

        // Only track if there's meaningful attribution data
        var hasUtm = !string.IsNullOrEmpty(data.UtmSource)
                  || !string.IsNullOrEmpty(data.UtmMedium)
                  || !string.IsNullOrEmpty(data.UtmCampaign);
        var hasMeta = !string.IsNullOrEmpty(data.MetaClickId);
        var hasPhone = !string.IsNullOrEmpty(data.Phone);

        if (!hasPhone || (!hasUtm && !hasMeta))
            return 0;

        var leadSource = DetectLeadSource(data.UtmSource, data.MetaClickId);

        var req = new AttributionTrackRequest
        {
            CustomerPhone = data.Phone ?? "",
            ChatId = webhookEvent.ChatId > 0 ? (int)webhookEvent.ChatId : null,
            UtmSource = data.UtmSource,
            UtmMedium = data.UtmMedium,
            UtmCampaign = data.UtmCampaign,
            UtmContent = data.UtmContent,
            UtmTerm = data.UtmTerm,
            MetaClickId = data.MetaClickId
        };

        try
        {
            var id = await _repo.InsertLeadAttributionAsync(tenantId, req, leadSource, ct);
            if (id > 0)
            {
                _logger.StepInfo($"Attribution tracked: lead_id={id}, source={leadSource}, tenant={tenantId}", "-");
            }
            return id;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // Attribution tracking must never break the webhook flow
            _logger.SystemWarn($"Attribution tracking failed for tenant {tenantId}: {ex.Message}");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"Attribution tracking configuration error for tenant {tenantId}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Auto-detect lead source from UTM parameters and meta click ID.
    /// Priority: meta_click_id > utm_source mapping > direct
    /// </summary>
    public static string DetectLeadSource(string? utmSource, string? metaClickId)
    {
        // Meta click ID takes priority (definitive Meta Ads signal)
        if (!string.IsNullOrEmpty(metaClickId))
            return "meta_ad";

        if (string.IsNullOrEmpty(utmSource))
            return "direct";

        // Normalize to lowercase for matching
        var src = utmSource.ToLowerInvariant();

        return src switch
        {
            "facebook" or "fb" or "instagram" or "ig" or "meta" => "meta_ad",
            "google" or "gads" or "google_ads" or "adwords" => "google_ad",
            "tiktok" or "tt" => "tiktok_ad",
            "linkedin" or "li" => "linkedin_ad",
            "twitter" or "x" => "twitter_ad",
            "youtube" or "yt" => "google_ad",
            _ => src.Contains("ad") || src.Contains("paid") ? "paid_other" : "organic"
        };
    }

    /// <summary>
    /// Validate conversion status transition.
    /// </summary>
    public static bool IsValidConversionStatus(string status)
    {
        return status is "new" or "contacted" or "qualified" or "converted" or "lost";
    }

    /// <summary>
    /// Validate ad cost platform.
    /// </summary>
    public static bool IsValidPlatform(string platform)
    {
        return platform is "meta" or "google" or "tiktok" or "linkedin" or "other";
    }
}
