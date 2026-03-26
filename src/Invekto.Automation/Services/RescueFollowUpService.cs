using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// PKT-12 Faz 3: Background service that sends follow-up messages after rescue.
/// Timer fires every 4 hours.
/// Stage 1 (T+24h): "Memnun kaldınız mı?" satisfaction check.
/// Stage 2 (T+48h): If customer responded positively, send review redirect link.
/// </summary>
public sealed class RescueFollowUpService : IHostedService, IDisposable
{
    private readonly MarketingRescueClient _marketingClient;
    private readonly OutboundRescueClient _outboundClient;
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    private Timer? _timer;
    private int _isRunning;

    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(4);

    // Positive response keywords (Turkish)
    private static readonly string[] PositiveKeywords =
    [
        "evet", "teşekkür", "tesekkur", "memnunum", "güzel", "guzel",
        "iyi", "süper", "super", "harika", "tamam", "sağolun", "sagolun",
        "çözüldü", "cozuldu", "halloldu", "tamamdır", "tamamdir",
        "memnun kaldım", "memnun kaldim", "sorun kalmadı", "sorun kalmadi"
    ];

    public RescueFollowUpService(
        MarketingRescueClient marketingClient,
        OutboundRescueClient outboundClient,
        AutomationRepository repo,
        JsonLinesLogger logger)
    {
        _marketingClient = marketingClient;
        _outboundClient = outboundClient;
        _repo = repo;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Start after 5 minutes delay, then every 4 hours
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromMinutes(5), TickInterval);
        _logger.SystemInfo("RescueFollowUpService started (interval: 4h)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _logger.SystemInfo("RescueFollowUpService stopping");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void OnTimerTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return; // Previous tick still running

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessFollowUpsAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFollowUpQueryFailed}] RescueFollowUp tick cancelled");
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemError($"[{ErrorCodes.AutomationFollowUpQueryFailed}] RescueFollowUp tick HTTP error: {ex.Message}");
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.SystemError($"[{ErrorCodes.AutomationFollowUpQueryFailed}] RescueFollowUp tick DB error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }).ContinueWith(
            t => _logger.SystemError($"RescueFollowUp unhandled: {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task ProcessFollowUpsAsync(CancellationToken ct)
    {
        var dueRisks = await _marketingClient.GetFollowUpDueRisksAsync(ct);
        if (dueRisks.Count == 0)
            return;

        _logger.SystemInfo($"RescueFollowUp: processing {dueRisks.Count} due risks");

        int satisfactionSent = 0, redirectSent = 0, errors = 0;

        foreach (var risk in dueRisks)
        {
            try
            {
                if (risk.FollowUpStatus == "none")
                {
                    // Stage 1: T+24h satisfaction check
                    var sent = await SendSatisfactionCheckAsync(risk, ct);
                    if (sent) satisfactionSent++;
                    else errors++;
                }
                else if (risk.FollowUpStatus == "satisfaction_sent" && risk.CustomerResponse == "satisfied")
                {
                    // Stage 2: T+48h review redirect
                    var sent = await SendReviewRedirectAsync(risk, ct);
                    if (sent) redirectSent++;
                    else errors++;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationFollowUpSendFailed}] FollowUp failed for risk {risk.Id}, tenant {risk.TenantId}: {ex.Message}");
                errors++;
            }
            catch (OperationCanceledException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationFollowUpSendFailed}] FollowUp cancelled for risk {risk.Id}, tenant {risk.TenantId}: {ex.Message}");
                errors++;
            }
        }

        _logger.SystemInfo(
            $"RescueFollowUp: done — satisfaction_sent={satisfactionSent}, redirect_sent={redirectSent}, errors={errors}");
    }

    /// <summary>
    /// Stage 1: Send "Memnun kaldınız mı?" message via Outbound.
    /// </summary>
    private async Task<bool> SendSatisfactionCheckAsync(FollowUpDueItem risk, CancellationToken ct)
    {
        const string satisfactionMessage =
            "Merhaba, daha önce yaşadığınız sorunla ilgili size yardımcı olmaya çalışmıştık. " +
            "Memnun kaldınız mı? Geri bildiriminiz bizim için çok değerli. 🙏";

        var messageId = await _outboundClient.SendRescueMessageAsync(
            risk.TenantId, risk.CustomerPhone, satisfactionMessage,
            eventName: "rescue_followup_satisfaction", ct);

        if (messageId == null)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpSendFailed}] Satisfaction message send failed for risk {risk.Id}, tenant {risk.TenantId}");
            return false;
        }

        var updated = await _marketingClient.UpdateFollowUpStatusAsync(
            risk.TenantId, risk.Id, "satisfaction_sent", ct);

        _logger.StepInfo(
            $"RescueFollowUp: satisfaction sent for risk {risk.Id}, tenant={risk.TenantId}, phone={risk.CustomerPhone}, msgId={messageId}",
            "rescue-followup");

        return updated;
    }

    /// <summary>
    /// Stage 2: Send review redirect link (from tenant settings review_url).
    /// </summary>
    private async Task<bool> SendReviewRedirectAsync(FollowUpDueItem risk, CancellationToken ct)
    {
        var reviewUrl = await GetTenantReviewUrlAsync(risk.TenantId, ct);
        if (string.IsNullOrWhiteSpace(reviewUrl))
        {
            _logger.SystemWarn(
                $"RescueFollowUp: no review_url in settings for tenant {risk.TenantId}, skipping review redirect for risk {risk.Id}");
            // Mark completed even without URL — don't retry forever
            await _marketingClient.UpdateFollowUpStatusAsync(risk.TenantId, risk.Id, "closed", ct);
            return true;
        }

        var redirectMessage =
            $"Memnuniyetinizi duyduğumuza çok sevindik! 😊 " +
            $"Deneyiminizi başkalarıyla da paylaşmak ister misiniz? " +
            $"Değerlendirmenizi buradan bırakabilirsiniz: {reviewUrl}";

        var messageId = await _outboundClient.SendRescueMessageAsync(
            risk.TenantId, risk.CustomerPhone, redirectMessage,
            eventName: "rescue_followup_review", ct);

        if (messageId == null)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpSendFailed}] Review redirect send failed for risk {risk.Id}, tenant {risk.TenantId}");
            return false;
        }

        var updated = await _marketingClient.UpdateFollowUpStatusAsync(
            risk.TenantId, risk.Id, "review_redirect_sent", ct);

        _logger.StepInfo(
            $"RescueFollowUp: review redirect sent for risk {risk.Id}, tenant={risk.TenantId}, phone={risk.CustomerPhone}, url={reviewUrl}",
            "rescue-followup");

        return updated;
    }

    /// <summary>
    /// Extract review_url from tenant settings_json.
    /// </summary>
    private async Task<string?> GetTenantReviewUrlAsync(int tenantId, CancellationToken ct)
    {
        try
        {
            var settingsJson = await _repo.GetTenantSettingsJsonAsync(tenantId, ct);
            if (string.IsNullOrWhiteSpace(settingsJson))
                return null;

            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("review_url", out var urlEl))
            {
                var url = urlEl.GetString();
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpQueryFailed}] Invalid settings_json for tenant {tenantId}: {ex.Message}");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpQueryFailed}] Failed to get tenant settings for tenant {tenantId}: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Detect positive response from customer message using keyword matching.
    /// Called externally by AutomationOrchestrator when a message arrives from a phone
    /// with an active follow-up.
    /// </summary>
    public static bool IsPositiveResponse(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return false;

        var normalized = messageText.ToLowerInvariant().Trim();
        return PositiveKeywords.Any(kw => normalized.Contains(kw));
    }
}
