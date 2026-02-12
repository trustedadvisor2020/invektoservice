using Invekto.Outbound.Data;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

/// <summary>
/// Manages opt-out registry and stop keyword detection.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class OptOutManager
{
    private readonly OutboundRepository _repository;
    private readonly JsonLinesLogger _logger;

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
    /// </summary>
    public async Task<(bool optedOut, string? keyword)> ProcessIncomingMessageAsync(
        int tenantId, string phone, string messageText, CancellationToken ct = default)
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

        return (true, keyword);
    }
}
