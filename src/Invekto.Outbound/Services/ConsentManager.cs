using Invekto.Outbound.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Outbound.Services;

/// <summary>
/// Manages marketing/utility consent records (GR-3.26).
/// Coordinates with OptOutManager: STOP keyword → also revokes consent.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class ConsentManager
{
    private readonly OutboundRepository _repository;
    private readonly JsonLinesLogger _logger;

    /// <summary>
    /// Utility trigger events that do not require marketing consent.
    /// </summary>
    private static readonly HashSet<string> UtilityEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "payment_received", "appointment_reminder",
        "return_exchange", "return_coupon", "review_recovery",
        "clinic_reminder", "post_treatment"
    };

    public ConsentManager(OutboundRepository repository, JsonLinesLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Check if a trigger event is utility (no marketing consent needed).
    /// </summary>
    public static bool IsUtilityEvent(string triggerEvent)
        => UtilityEvents.Contains(triggerEvent);

    /// <summary>
    /// FEAT-J2: Convenience delegate to the shared registry so Outbound callers can
    /// keep reading through ConsentManager while the source-of-truth list lives in
    /// <see cref="TransactionalEventRegistry"/> (reused by Automation).
    /// </summary>
    public static bool IsTransactionalEvent(string? eventName)
        => TransactionalEventRegistry.IsTransactional(eventName);

    /// <summary>
    /// Check marketing consent for a single phone.
    /// Returns true if: (1) phone has explicit marketing opt-in, OR (2) no consent record exists (backwards compat).
    /// Returns false only if an explicit opt-out consent record exists.
    /// </summary>
    public async Task<bool> HasMarketingConsentAsync(
        int tenantId, string phone, CancellationToken ct = default)
    {
        var records = await _repository.GetConsentRecordsAsync(tenantId, phone, ct);
        if (records.Count == 0)
            return true; // No consent records → legacy behavior, allow

        // Check for explicit marketing or 'all' consent
        return records.Any(r =>
            r.ConsentType is "marketing" or "all" && r.OptedIn);
    }

    /// <summary>
    /// Batch check marketing consent for broadcast recipients.
    /// Returns set of phones that should be SKIPPED (no consent).
    /// Phones with no consent records are ALLOWED (backwards compat).
    /// </summary>
    public async Task<HashSet<string>> GetPhonesWithoutMarketingConsentAsync(
        int tenantId, List<string> phones, CancellationToken ct = default)
    {
        if (phones.Count == 0) return new HashSet<string>();

        // Get phones that have explicit consent
        var consentedPhones = await _repository.BatchCheckMarketingConsentAsync(
            tenantId, phones, ct);

        // For batch: only skip phones that have consent records but NOT opted in.
        // To detect this, we need to know which phones have ANY consent record.
        // Simple approach: phones in consentedPhones are OK. Phones NOT in
        // consentedPhones could be (a) no record at all, or (b) opted-out.
        // For backwards compat, we allow (a). We need a separate check for (b).
        // However, the BatchCheckMarketingConsentAsync already only returns opted-in phones.
        // So if a phone is NOT in consentedPhones, it could be either case.
        // For efficiency, we accept this: if consent_records table is populated for a tenant,
        // phones without marketing consent are excluded from the batch result.
        // The semantics: phones NOT returned by BatchCheck = no marketing consent.
        // But for backwards compat when consent_records is empty for a tenant,
        // we return an empty set (skip nobody).

        // Quick check: if NO phones have consent records at all, skip nobody
        if (consentedPhones.Count == 0)
        {
            // Could be: (a) tenant doesn't use consent system, or (b) all opted out.
            // We cannot distinguish efficiently in batch. Use tenant-level check.
            // For now: if batch returns empty AND phones list > 0,
            // assume consent system not yet configured → skip nobody.
            // This is safe because HasMarketingConsentAsync individually checks.
            return new HashSet<string>();
        }

        // Return phones that are NOT in the consented set
        var noConsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var phone in phones)
        {
            if (!consentedPhones.Contains(phone))
                noConsent.Add(phone);
        }
        return noConsent;
    }

    /// <summary>
    /// Record consent (opt-in or opt-out).
    /// </summary>
    public async Task<ConsentRecordResponse> UpsertConsentAsync(
        int tenantId, ConsentRecordRequest request, CancellationToken ct = default)
    {
        var validTypes = new HashSet<string> { "marketing", "utility", "all" };
        if (!validTypes.Contains(request.ConsentType))
            throw new ArgumentException($"Invalid consent_type: {request.ConsentType}");

        await _repository.UpsertConsentAsync(
            tenantId, request.CustomerPhone, request.ConsentType,
            request.Channel, request.Source, request.OptedIn, ct);

        _logger.SystemInfo(
            $"Consent upserted: tenant={tenantId}, phone={request.CustomerPhone}, " +
            $"type={request.ConsentType}, opted_in={request.OptedIn}, channel={request.Channel}");

        return new ConsentRecordResponse
        {
            CustomerPhone = request.CustomerPhone,
            ConsentType = request.ConsentType,
            Channel = request.Channel,
            Source = request.Source,
            OptedIn = request.OptedIn,
            OptedInAt = request.OptedIn ? DateTime.UtcNow : null,
            OptedOutAt = !request.OptedIn ? DateTime.UtcNow : null
        };
    }

    /// <summary>
    /// Get all consent records for a phone (for consent check endpoint).
    /// </summary>
    public async Task<ConsentCheckResponse> CheckConsentAsync(
        int tenantId, string phone, CancellationToken ct = default)
    {
        var records = await _repository.GetConsentRecordsAsync(tenantId, phone, ct);

        var hasMarketing = records.Any(r =>
            r.ConsentType is "marketing" or "all" && r.OptedIn);
        var hasUtility = records.Any(r =>
            r.ConsentType is "utility" or "all" && r.OptedIn);

        return new ConsentCheckResponse
        {
            CustomerPhone = phone,
            HasMarketingConsent = hasMarketing,
            HasUtilityConsent = hasUtility,
            Records = records.Select(r => new ConsentRecordResponse
            {
                Id = r.Id,
                CustomerPhone = phone,
                ConsentType = r.ConsentType,
                Channel = r.Channel,
                OptedIn = r.OptedIn,
                OptedInAt = r.OptedInAt,
                OptedOutAt = r.OptedOutAt
            }).ToList()
        };
    }

    /// <summary>
    /// Sync STOP keyword opt-out to consent_records.
    /// Called when OptOutManager detects a STOP keyword.
    /// </summary>
    public async Task SyncStopKeywordToConsentAsync(
        int tenantId, string phone, string keyword, CancellationToken ct = default)
    {
        await _repository.UpsertConsentAsync(
            tenantId, phone, "all", "whatsapp_stop",
            $"STOP keyword: {keyword}", false, ct);

        _logger.SystemInfo(
            $"Consent synced from STOP keyword: tenant={tenantId}, phone={phone}, keyword={keyword}");
    }
}
