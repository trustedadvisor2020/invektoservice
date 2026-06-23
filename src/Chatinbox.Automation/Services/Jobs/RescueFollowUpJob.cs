using System.Text.Json;
using Hangfire;
using Chatinbox.Automation.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services.Jobs;

/// <summary>
/// G7 Faz 3: Hangfire recurring job replacing <c>RescueFollowUpService</c>.
/// Sends follow-up messages after rescue.
/// Stage 1 (T+24h): "Memnun kaldınız mı?" satisfaction check.
/// Stage 2 (T+48h): If customer responded positively, send review redirect link.
///
/// Queue: <c>automation</c>. Recurring id: <c>automation:rescue-followup</c> (cron "0 */4 * * *").
/// </summary>
[Queue("automation")]
[DisableConcurrentExecution(timeoutInSeconds: 600)]
public sealed class RescueFollowUpJob
{
    private readonly MarketingRescueClient _marketingClient;
    private readonly OutboundRescueClient _outboundClient;
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    public RescueFollowUpJob(
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

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await ProcessFollowUpsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("RescueFollowUpJob: cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire (AutomaticRetry + INV-JOB-005 on final failure).
    }

    private async Task ProcessFollowUpsAsync(CancellationToken ct)
    {
        var dueRisks = await _marketingClient.GetFollowUpDueRisksAsync(ct);
        if (dueRisks.Count == 0)
            return;

        _logger.SystemInfo($"RescueFollowUpJob: processing {dueRisks.Count} due risks");

        int satisfactionSent = 0, redirectSent = 0, errors = 0;

        foreach (var risk in dueRisks)
        {
            try
            {
                if (risk.FollowUpStatus == "none")
                {
                    var sent = await SendSatisfactionCheckAsync(risk, ct);
                    if (sent) satisfactionSent++;
                    else errors++;
                }
                else if (risk.FollowUpStatus == "satisfaction_sent" && risk.CustomerResponse == "satisfied")
                {
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
            $"RescueFollowUpJob: done — satisfaction_sent={satisfactionSent}, redirect_sent={redirectSent}, errors={errors}");
    }

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
            $"RescueFollowUpJob: satisfaction sent for risk {risk.Id}, tenant={risk.TenantId}, phone={risk.CustomerPhone}, msgId={messageId}",
            "rescue-followup");

        return updated;
    }

    private async Task<bool> SendReviewRedirectAsync(FollowUpDueItem risk, CancellationToken ct)
    {
        var reviewUrl = await GetTenantReviewUrlAsync(risk.TenantId, ct);
        if (string.IsNullOrWhiteSpace(reviewUrl))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpQueryFailed}] RescueFollowUpJob: no review_url in settings for tenant {risk.TenantId}, skipping review redirect for risk {risk.Id}");
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
            $"RescueFollowUpJob: review redirect sent for risk {risk.Id}, tenant={risk.TenantId}, phone={risk.CustomerPhone}, url={reviewUrl}",
            "rescue-followup");

        return updated;
    }

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
}
