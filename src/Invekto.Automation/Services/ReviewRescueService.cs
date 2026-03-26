using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Computes review risk score from incoming messages and persists HIGH/CRITICAL risks.
/// Called post-flow by AutomationOrchestrator (fire-and-forget side effect).
/// PKT-12: Review Rescue AI — risk scoring engine.
/// </summary>
public sealed class ReviewRescueService
{
    private readonly MarketingRescueClient _marketingClient;
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public ReviewRescueService(MarketingRescueClient marketingClient, HttpClient httpClient, JsonLinesLogger logger)
    {
        _marketingClient = marketingClient;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Score message for review risk and take action if HIGH or CRITICAL.
    /// Never throws — all exceptions caught and logged.
    /// </summary>
    public async Task<ReviewRescueResult?> ScoreAndProcessAsync(
        int tenantId, string phone, string messageText,
        Dictionary<string, string> flowVariables,
        string? settingsJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        try
        {
            // 1. Keyword score (0-25)
            var keywordResult = RiskKeywordScanner.Scan(messageText);

            // 2. Sentiment score (0-30)
            // SentimentAnalyzer score: 0.0 = very negative, 1.0 = very positive
            // Invert: lower sentiment = higher risk
            int sentimentScore = 0;
            if (flowVariables.TryGetValue("sentiment_score", out var sentimentStr)
                && double.TryParse(sentimentStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var rawSentiment))
            {
                sentimentScore = (int)((1.0 - Math.Clamp(rawSentiment, 0.0, 1.0)) * 30);
            }

            // 3. Response delay score (0-15)
            int responseDelayScore = 0;
            if (flowVariables.TryGetValue("last_agent_reply_at", out var replyAtStr)
                && DateTime.TryParse(replyAtStr, out var lastReplyAt))
            {
                var delayMinutes = (DateTime.UtcNow - lastReplyAt).TotalMinutes;
                responseDelayScore = delayMinutes switch
                {
                    > 240 => 15,  // > 4 hours
                    > 60 => 10,   // > 1 hour
                    > 30 => 5,    // > 30 min
                    _ => 0
                };
            }

            // 4. History score (0-10) — prior risks for this phone
            int historyScore = 0;
            var priorCount = await _marketingClient.GetPriorRiskCountAsync(tenantId, phone, ct);
            historyScore = priorCount switch
            {
                >= 2 => 10,
                1 => 5,
                _ => 0
            };

            // 5. Timing score (0-20) — reserved for Phase 2 (delivery tracking integration)
            int timingScore = 0;

            // Composite risk score
            int totalScore = Math.Min(
                sentimentScore + keywordResult.Score + timingScore + responseDelayScore + historyScore,
                100);

            var riskLevel = totalScore switch
            {
                >= 80 => "critical",
                >= 60 => "high",
                >= 30 => "medium",
                _ => "low"
            };

            // Only persist and act on HIGH+ risks
            if (totalScore < 60)
            {
                return new ReviewRescueResult
                {
                    RiskScore = totalScore,
                    RiskLevel = riskLevel,
                    Persisted = false
                };
            }

            // Build trigger reason string
            var reasons = new List<string>();
            if (sentimentScore > 0) reasons.Add($"sentiment:{sentimentScore}/30");
            if (keywordResult.HasMatch) reasons.Add($"keywords:[{string.Join(",", keywordResult.MatchedKeywords)}]={keywordResult.Score}/25");
            if (responseDelayScore > 0) reasons.Add($"delay:{responseDelayScore}/15");
            if (historyScore > 0) reasons.Add($"history:{priorCount}={historyScore}/10");
            var triggerReason = string.Join(", ", reasons);

            // Persist to Marketing
            var riskId = await _marketingClient.CreateRiskAsync(
                tenantId, phone, null, (short)totalScore, riskLevel, triggerReason, ct);

            _logger.StepInfo(
                $"Review risk detected: tenant={tenantId}, phone={phone}, score={totalScore}, " +
                $"level={riskLevel}, reason=[{triggerReason}], riskId={riskId}",
                "review-rescue");

            // Fire supervisor webhook for CRITICAL
            if (riskLevel == "critical")
            {
                var webhookUrl = ExtractSupervisorWebhookUrl(settingsJson);
                if (webhookUrl != null)
                {
                    _ = FireSupervisorAlertAsync(tenantId, phone, totalScore, triggerReason, webhookUrl)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                _logger.SystemWarn(
                                    $"[{ErrorCodes.AutomationRescueAlertFailed}] Supervisor alert failed for tenant {tenantId}: {t.Exception?.InnerException?.Message}");
                        }, TaskScheduler.Default);
                }
            }

            return new ReviewRescueResult
            {
                RiskScore = totalScore,
                RiskLevel = riskLevel,
                RiskId = riskId,
                Persisted = riskId.HasValue,
                TriggerReason = triggerReason
            };
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRiskScoringFailed}] Review rescue DB error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRiskScoringFailed}] Review rescue unexpected error for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    private async Task FireSupervisorAlertAsync(
        int tenantId, string phone, int riskScore, string triggerReason, string webhookUrl)
    {
        var payload = new
        {
            type = "review_risk_critical",
            tenant_id = tenantId,
            phone,
            risk_score = riskScore,
            risk_level = "critical",
            trigger_reason = triggerReason,
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
                $"[{ErrorCodes.AutomationRescueAlertFailed}] Supervisor webhook returned {(int)response.StatusCode} for tenant {tenantId}");
        }
    }

    private static string? ExtractSupervisorWebhookUrl(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("rescue_supervisor_webhook_url", out var urlEl))
            {
                var url = urlEl.GetString();
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
        }
        catch (JsonException) { }
        return null;
    }
}

public sealed class ReviewRescueResult
{
    public int RiskScore { get; init; }
    public string RiskLevel { get; init; } = "low";
    public int? RiskId { get; init; }
    public bool Persisted { get; init; }
    public string? TriggerReason { get; init; }
}
