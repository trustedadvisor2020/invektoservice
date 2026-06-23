using Chatinbox.Shared.Contracts.Inma.Webhooks;
using Chatinbox.Shared.DTOs.Attribution;
using Chatinbox.Shared.DTOs.Integration;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Backend.Services;

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
    public Task<int> TrackFromWebhookAsync(
        int tenantId, WebhookMessage message, CancellationToken ct = default)
    {
        // INMA webhook does not include UTM/attribution data.
        // Attribution tracking requires UTM parameters or meta click ID.
        // When INMA starts sending UTM params, this method will be re-enabled.
        return Task.FromResult(0);
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
