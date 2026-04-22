namespace Invekto.Shared.Contracts.Campaigns;

/// <summary>
/// FEAT-MCC: structured validation failure for campaign config PUT.
/// Carries the offending campaign slug + field path so the endpoint can surface a
/// targeted 400 to the client (Dashboard editor highlights the offending row).
///
/// Mirrors <c>TenantFieldMappingValidationException</c> shape (FEAT-TFM precedent)
/// to keep ErrorResponse.Create call sites uniform across tenant settings endpoints.
/// </summary>
public sealed class TenantCampaignConfigValidationException : Exception
{
    /// <summary>INV-BE-118 (structural) or INV-BE-120 (reserved slug).</summary>
    public string ErrorCode { get; }

    /// <summary>The campaign slug being validated when the rule failed; empty for top-level
    /// failures (e.g. malformed root JSON, max-campaigns cap).</summary>
    public string CampaignSlug { get; }

    /// <summary>Optional dotted-path of the failing field (e.g. "dates[0].city" or "start_date").</summary>
    public string? FieldPath { get; }

    public TenantCampaignConfigValidationException(string errorCode, string campaignSlug, string? fieldPath, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        CampaignSlug = campaignSlug ?? string.Empty;
        FieldPath = fieldPath;
    }
}
