using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Scans message text for B2B/VIP signals using keyword matching.
/// Called post-flow by AutomationOrchestrator (fire-and-forget side effect).
/// Writes to vip_flags table. Fires webhook to sales team if new VIP detected.
/// PKT-6A: GR-3.2 B2B/VIP Lead Detection.
/// </summary>
public sealed class VipDetectionService
{
    private readonly AutomationRepository _repo;
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    /// <summary>B2B signal keywords (Turkish, lowercase for case-insensitive match).</summary>
    private static readonly (string Keyword, string Category, decimal Weight)[] B2bSignals =
    {
        ("toptan", "b2b", 30m),
        ("toptan fiyat", "b2b", 40m),
        ("bayi", "b2b", 35m),
        ("bayilik", "b2b", 35m),
        ("acente", "b2b", 30m),
        ("kurumsal", "b2b", 25m),
        ("kurumsal fatura", "b2b", 40m),
        ("ihracat", "b2b", 30m),
        ("ihale", "b2b", 30m),
        ("100 adet", "b2b", 40m),
        ("200 adet", "b2b", 40m),
        ("500 adet", "b2b", 50m),
        ("1000 adet", "b2b", 60m),
        ("bulk", "b2b", 25m),
        ("wholesale", "b2b", 25m)
    };

    public VipDetectionService(AutomationRepository repo, HttpClient httpClient, JsonLinesLogger logger)
    {
        _repo = repo;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Check message for B2B/VIP signals and record if detected.
    /// Returns null if phone is null (cannot flag without phone).
    /// Never throws — all exceptions caught and logged with INV-AT-026.
    /// </summary>
    public async Task<VipDetectionResult?> CheckAndRecordAsync(
        int tenantId, string? phone, string messageText,
        string? settingsJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        try
        {
            var normalizedText = messageText.ToLowerInvariant();
            var matchedSignals = new List<string>();
            decimal totalScore = 0;

            foreach (var (keyword, category, weight) in B2bSignals)
            {
                if (normalizedText.Contains(keyword, StringComparison.Ordinal))
                {
                    matchedSignals.Add(keyword);
                    totalScore += weight;
                }
            }

            if (matchedSignals.Count == 0)
                return new VipDetectionResult { IsVip = false };

            var clampedScore = Math.Min(totalScore, 100m);
            var isNew = await _repo.UpsertVipFlagAsync(
                tenantId, phone, "b2b", clampedScore, matchedSignals.ToArray(), ct);

            _logger.StepInfo(
                $"VIP detected: tenant={tenantId}, phone={phone}, type=b2b, score={clampedScore}, " +
                $"signals=[{string.Join(",", matchedSignals)}], isNew={isNew}",
                "vip-detection");

            // Fire sales webhook if new VIP and webhook URL configured
            if (isNew)
            {
                var webhookUrl = ExtractSalesWebhookUrl(settingsJson);
                if (webhookUrl != null)
                {
                    _ = FireSalesWebhookAsync(tenantId, phone, matchedSignals, clampedScore, webhookUrl)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                _logger.SystemWarn(
                                    $"[{ErrorCodes.AutomationVipDetectionFailed}] Sales webhook failed for tenant {tenantId}: {t.Exception?.InnerException?.Message}");
                        }, TaskScheduler.Default);
                }
            }

            return new VipDetectionResult
            {
                IsVip = true,
                VipType = "b2b",
                MatchedSignals = matchedSignals.ToArray(),
                Score = clampedScore,
                IsNew = isNew
            };
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationVipDetectionFailed}] VIP detection DB error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationVipDetectionFailed}] VIP detection unexpected error for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    private async Task FireSalesWebhookAsync(int tenantId, string phone, List<string> signals, decimal score, string webhookUrl)
    {
        var payload = new
        {
            type = "vip_lead_detected",
            tenant_id = tenantId,
            phone,
            vip_type = "b2b",
            matched_signals = signals,
            detection_score = score,
            timestamp = DateTime.UtcNow
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(webhookUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationVipDetectionFailed}] Sales webhook returned {(int)response.StatusCode} for tenant {tenantId}");
        }
    }

    private static string? ExtractSalesWebhookUrl(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("sales_webhook_url", out var urlEl))
            {
                var url = urlEl.GetString();
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
        }
        catch (JsonException)
        {
            // settings_json is written by our own system. Malformed = code bug, not runtime condition.
            // Return null safely: sales webhook simply won't fire.
        }
        return null;
    }
}

public sealed class VipDetectionResult
{
    public bool IsVip { get; init; }
    public string? VipType { get; init; }
    public string[]? MatchedSignals { get; init; }
    public decimal Score { get; init; }
    public bool IsNew { get; init; }
}
