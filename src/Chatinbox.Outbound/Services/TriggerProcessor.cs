using Chatinbox.Outbound.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Outbound;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Services;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// Processes trigger events from Main App webhooks.
/// Finds matching template, applies variables, inserts single message.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class TriggerProcessor
{
    private readonly OutboundRepository _repository;
    private readonly TemplateEngine _templateEngine;
    private readonly OptOutManager _optOutManager;
    private readonly ConsentManager _consentManager;
    private readonly JsonLinesLogger _logger;

    public TriggerProcessor(
        OutboundRepository repository,
        TemplateEngine templateEngine,
        OptOutManager optOutManager,
        ConsentManager consentManager,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _templateEngine = templateEngine;
        _optOutManager = optOutManager;
        _consentManager = consentManager;
        _logger = logger;
    }

    /// <summary>
    /// Process a trigger event. Returns response or error.
    /// </summary>
    public async Task<(TriggerWebhookResponse? response, string? errorCode, string? errorMessage, int statusCode)>
        ProcessTriggerAsync(
            int tenantId, TriggerWebhookRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Event))
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "event is required", 400);

        if (string.IsNullOrWhiteSpace(request.Phone))
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "phone is required", 400);

        // FEAT-J2: transactional allow-list bypasses opt-out (appointment confirms,
        // meeting links, payment receipts). Audit trail kept via INV-OB-030 so
        // the bypass path remains visible in ops logs even though it's legitimate.
        var isTransactional = ConsentManager.IsTransactionalEvent(request.Event);

        // Check opt-out (skipped for transactional events)
        if (!isTransactional && await _optOutManager.IsOptedOutAsync(tenantId, request.Phone, ct))
        {
            _logger.SystemInfo($"Trigger skipped (opted out): tenant={tenantId}, phone={request.Phone}, event={request.Event}");
            return (null, ErrorCodes.OutboundRecipientOptedOut,
                $"Recipient {request.Phone} has opted out of messages", 409);
        }
        if (isTransactional)
        {
            _logger.SystemInfo(
                $"[{ErrorCodes.TransactionalBypassAudit}] opt-out bypassed for transactional event: tenant={tenantId}, phone={request.Phone}, event={request.Event}");
        }

        // GR-3.26: Marketing consent check (utility events exempt)
        if (!ConsentManager.IsUtilityEvent(request.Event))
        {
            var hasConsent = await _consentManager.HasMarketingConsentAsync(
                tenantId, request.Phone, ct);
            if (!hasConsent)
            {
                _logger.SystemInfo(
                    $"Trigger skipped (no marketing consent): tenant={tenantId}, phone={request.Phone}, event={request.Event}");
                return (null, ErrorCodes.OutboundConsentNotGiven,
                    $"Recipient {request.Phone} has not given marketing consent", 403);
            }
        }

        // GR-2.3: Resolve language (request > normalize > default "tr", no auto-detect)
        var lang = LanguageDetector.Normalize(request.Lang) ?? "tr";

        // GR-2.3: Find matching trigger template with lang fallback chain:
        // 1) Try requested lang  2) Try tenant default "tr"  3) Try any lang
        string? langFallbackWarning = null;
        var template = await _repository.GetTriggerTemplateAsync(tenantId, request.Event, lang, ct);
        if (template == null && lang != "tr")
        {
            // Fallback step 1: try tenant default language "tr"
            template = await _repository.GetTriggerTemplateAsync(tenantId, request.Event, "tr", ct);
            if (template != null)
            {
                langFallbackWarning = $"No '{lang}' template for event '{request.Event}', " +
                    $"fell back to default 'tr' template '{template.Name}'";
                _logger.SystemWarn(
                    $"Trigger template lang fallback: {langFallbackWarning} in tenant {tenantId}");
            }
        }
        if (template == null)
        {
            // Fallback step 2: try any available language
            template = await _repository.GetTriggerTemplateAsync(tenantId, request.Event, null, ct);
            if (template != null && langFallbackWarning == null)
            {
                langFallbackWarning = $"No '{lang}' template for event '{request.Event}', " +
                    $"using template '{template.Name}' (lang={template.Lang ?? "any"})";
                _logger.SystemWarn(
                    $"Trigger template lang fallback: {langFallbackWarning} in tenant {tenantId}");
            }
        }
        if (template == null)
        {
            return (null, ErrorCodes.OutboundNoMatchingTriggerTemplate,
                $"No active template found for event '{request.Event}' in tenant {tenantId}", 404);
        }

        // Apply template variables
        var (messageText, missingVars) = _templateEngine.Substitute(
            template.MessageTemplate, request.Variables);

        if (missingVars.Count > 0)
        {
            _logger.SystemWarn(
                $"Trigger template {template.Id} has missing variables: [{string.Join(", ", missingVars)}]");
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload,
                $"Template requires variables: {string.Join(", ", missingVars)}", 400);
        }

        // GR-2.6.1: Append KVKK health disclaimer if applicable
        var (healthSettingsJson, healthSector) = await _repository.GetTenantHealthInfoAsync(tenantId, ct);
        var isHealthTenant = KvkkHelper.IsHealthTenant(healthSettingsJson, healthSector);
        var finalText = KvkkHelper.AppendDisclaimerIfHealth(messageText, isHealthTenant);

        // GR-2.3: Use template's actual language after fallback (not the originally requested lang)
        var effectiveLang = template.Lang ?? lang;

        // Insert single message (no broadcast_id, GR-2.3: with effective language).
        // FEAT-DMP: dynamicFields optional param sits AFTER ct in the repository signature,
        // so positional call remains compatible; triggers render text upstream (dynamicFields=null).
        var messageId = await _repository.InsertMessageAsync(
            tenantId, null, template.Id, request.Phone, finalText, effectiveLang, ct);

        // GR-3.29: Audit trail for compliance
        await _repository.InsertAuditTrailAsync(
            tenantId, template.Id, null, request.Phone, finalText, ct);

        _logger.SystemInfo(
            $"Trigger message queued: id={messageId}, tenant={tenantId}, " +
            $"event={request.Event}, phone={request.Phone}, template={template.Id}");

        return (new TriggerWebhookResponse
        {
            MessageId = messageId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Warning = langFallbackWarning
        }, null, null, 202);
    }
}
