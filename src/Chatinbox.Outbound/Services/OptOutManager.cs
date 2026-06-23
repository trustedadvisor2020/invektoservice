using Chatinbox.Outbound.Data;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// Manages opt-out registry and stop keyword detection.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class OptOutManager
{
    private readonly OutboundRepository _repository;
    private readonly JsonLinesLogger _logger;
    private ConsentManager? _consentManager;

    // Stop keywords - normalized to uppercase for comparison
    private static readonly string[] StopKeywords =
    {
        "STOP", "DUR", "İPTAL", "IPTAL", "DURDU", "ÇIKIŞ", "CIKIS"
    };

    public OptOutManager(OutboundRepository repository, JsonLinesLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// GR-3.26: Set ConsentManager for STOP keyword sync.
    /// Called after DI to avoid circular dependency.
    /// </summary>
    public void SetConsentManager(ConsentManager consentManager)
        => _consentManager = consentManager;

    /// <summary>
    /// Check if a phone number has opted out for a given tenant.
    /// </summary>
    public Task<bool> IsOptedOutAsync(int tenantId, string phone, CancellationToken ct = default)
        => _repository.IsOptedOutAsync(tenantId, phone, ct);

    /// <summary>
    /// Detect stop keyword in incoming message text.
    /// Returns the matched keyword or null.
    /// </summary>
    public string? DetectStopKeyword(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return null;

        var normalized = messageText.Trim().ToUpperInvariant();
        foreach (var keyword in StopKeywords)
        {
            if (normalized == keyword)
                return keyword;
        }
        return null;
    }

    /// <summary>
    /// Process incoming message for opt-out detection.
    /// Returns (optedOut, matchedKeyword).
    ///
    /// FEAT-J2: instanceId (when provided) is forwarded to the INMA outbox so the
    /// later push carries the correct WapCRM InstanceID. When null (legacy caller
    /// or no webhook context), outbox enqueue is skipped with a warn log —
    /// INSE-side opt-out still succeeds.
    /// </summary>
    public async Task<(bool optedOut, string? keyword)> ProcessIncomingMessageAsync(
        int tenantId, string phone, string messageText, int? instanceId = null, CancellationToken ct = default)
    {
        var keyword = DetectStopKeyword(messageText);
        if (keyword == null)
            return (false, null);

        var added = await _repository.AddOptOutAsync(tenantId, phone, $"Keyword: {keyword}", ct);
        if (added)
        {
            _logger.SystemInfo($"Opt-out registered: tenant={tenantId}, phone={phone}, keyword={keyword}");
        }
        else
        {
            _logger.SystemInfo($"Opt-out already exists: tenant={tenantId}, phone={phone}");
        }

        // GR-3.26: Sync STOP keyword to consent_records
        if (_consentManager != null)
        {
            await _consentManager.SyncStopKeywordToConsentAsync(tenantId, phone, keyword, ct);
        }

        await TryEnqueueInmaSyncAsync(
            tenantId, phone, instanceId, "opt_out",
            reason: $"Keyword: {keyword}", source: "whatsapp", ct);

        return (true, keyword);
    }

    /// <summary>
    /// FEAT-J2: Manuel admin opt-out enqueue — Backend resolves last-known instance
    /// then forwards here. When <paramref name="instanceId"/> is null, the enqueue
    /// is skipped; this path is INMA sync best-effort and must never break the
    /// INSE-side opt-out succeeding.
    /// </summary>
    public async Task<bool> RegisterAdminOptOutAsync(
        int tenantId, string phone, int? instanceId, string? reason, CancellationToken ct = default)
    {
        var added = await _repository.AddOptOutAsync(tenantId, phone, reason, ct);
        if (added)
        {
            _logger.SystemInfo($"Admin opt-out registered: tenant={tenantId}, phone={phone}");
        }

        if (_consentManager != null)
        {
            await _consentManager.SyncStopKeywordToConsentAsync(tenantId, phone, "admin", ct);
        }

        await TryEnqueueInmaSyncAsync(
            tenantId, phone, instanceId, "opt_out",
            reason ?? "manual_admin", source: "manual_admin", ct);
        return added;
    }

    /// <summary>
    /// FEAT-J2: Admin opt-in — removes INSE opt-out flag and enqueues opt_in
    /// event for INMA contact sync.
    /// </summary>
    public async Task<bool> RegisterAdminOptInAsync(
        int tenantId, string phone, int? instanceId, CancellationToken ct = default)
    {
        var removed = await _repository.RemoveOptOutAsync(tenantId, phone, ct);
        if (removed)
        {
            _logger.SystemInfo($"Admin opt-in registered: tenant={tenantId}, phone={phone}");
        }

        await TryEnqueueInmaSyncAsync(
            tenantId, phone, instanceId, "opt_in",
            reason: "admin_opt_in", source: "manual_admin", ct);
        return removed;
    }

    private async Task TryEnqueueInmaSyncAsync(
        int tenantId, string phone, int? instanceId, string eventType,
        string? reason, string? source, CancellationToken ct)
    {
        if (instanceId is null or 0)
        {
            _logger.SystemWarn(
                $"INMA outbox enqueue skipped (no instance): tenant={tenantId}, phone={phone}, event={eventType}");
            return;
        }

        // Best-effort outbox enqueue — INSE-side opt-out has already succeeded in the
        // caller. DB transport faults (connection drop, constraint violation) must
        // log-and-continue so the user-facing opt-out isn't rolled back. Other
        // exception types (ArgumentException, InvalidOperationException coming from
        // a programming bug) are left to bubble up and surface as a 500.
        try
        {
            await _repository.EnqueueOptOutSyncAsync(
                tenantId, phone, instanceId.Value, eventType, scope: "all", reason, source, ct);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError(
                $"INMA outbox enqueue DB failure (INSE opt-out still applied): tenant={tenantId}, phone={phone}, event={eventType}, err={ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.SystemError(
                $"INMA outbox enqueue timeout (INSE opt-out still applied): tenant={tenantId}, phone={phone}, event={eventType}, err={ex.Message}");
        }
    }
}
