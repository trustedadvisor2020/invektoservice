using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// Orchestrates rescue message dispatch: fetch template from Marketing, render, send via Outbound.
/// PKT-12 Faz 2: Rescue Action Engine.
/// Called by ReviewRescueService after HIGH/CRITICAL risk is persisted.
/// </summary>
public sealed class RescueDispatcher
{
    private readonly MarketingRescueClient _marketingClient;
    private readonly OutboundRescueClient _outboundClient;
    private readonly JsonLinesLogger _logger;

    public RescueDispatcher(
        MarketingRescueClient marketingClient,
        OutboundRescueClient outboundClient,
        JsonLinesLogger logger)
    {
        _marketingClient = marketingClient;
        _outboundClient = outboundClient;
        _logger = logger;
    }

    /// <summary>
    /// Dispatch a rescue message for a detected risk.
    /// 1. Fetch matching template from Marketing by risk level
    /// 2. Render template (variable substitution)
    /// 3. Send rendered message via Outbound webhook trigger
    /// 4. Update risk status to "rescued" or "failed"
    /// Never throws — all exceptions caught and logged.
    /// </summary>
    public async Task DispatchAsync(
        int tenantId, string phone, string riskLevel, int riskId,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Fetch templates for this risk level
            var templates = await _marketingClient.GetTemplatesByRiskLevelAsync(tenantId, riskLevel, ct);

            // Fallback: if no templates for exact level, try one level down
            if (templates.Count == 0 && riskLevel == "critical")
                templates = await _marketingClient.GetTemplatesByRiskLevelAsync(tenantId, "high", ct);

            if (templates.Count == 0)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationRescueDispatchFailed}] No rescue template found for tenant {tenantId}, level={riskLevel}");
                await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "no_template", ct: ct);
                return;
            }

            // 2. Pick best template: prefer "apology" strategy, otherwise first available
            var template = templates.FirstOrDefault(t => t.Strategy == "apology") ?? templates[0];

            // 3. Render message (simple variable substitution)
            var renderedMessage = RenderTemplate(template.MessageTemplate, phone, riskLevel);

            // 4. Send via Outbound
            var messageId = await _outboundClient.SendRescueMessageAsync(
                tenantId, phone, renderedMessage, "review_rescue", ct);

            if (messageId != null)
            {
                _logger.StepInfo(
                    $"Rescue message dispatched: tenant={tenantId}, phone={phone}, riskId={riskId}, " +
                    $"template={template.TemplateName}, strategy={template.Strategy}, messageId={messageId}",
                    "rescue-dispatcher");

                await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "rescued", messageId, ct);
            }
            else
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationRescueDispatchFailed}] Outbound send failed for tenant {tenantId}, riskId={riskId}");
                await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "failed", ct: ct);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueDispatchFailed}] RescueDispatcher HTTP error for tenant {tenantId}: {ex.Message}");
            await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "failed", ct: ct);
        }
        catch (OperationCanceledException)
        {
            await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "failed", ct: ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueDispatchFailed}] RescueDispatcher config error for tenant {tenantId}: {ex.Message}");
            await _marketingClient.UpdateRiskStatusAsync(tenantId, riskId, "failed", ct: ct);
        }
    }

    /// <summary>
    /// Simple variable substitution in template message.
    /// Supported placeholders: {customer_phone}, {risk_level}
    /// </summary>
    private static string RenderTemplate(string template, string phone, string riskLevel)
    {
        return template
            .Replace("{customer_phone}", phone)
            .Replace("{risk_level}", riskLevel);
    }
}
