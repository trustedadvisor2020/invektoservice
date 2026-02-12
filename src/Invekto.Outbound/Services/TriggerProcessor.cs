using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

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
    private readonly JsonLinesLogger _logger;

    public TriggerProcessor(
        OutboundRepository repository,
        TemplateEngine templateEngine,
        OptOutManager optOutManager,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _templateEngine = templateEngine;
        _optOutManager = optOutManager;
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

        // Check opt-out
        if (await _optOutManager.IsOptedOutAsync(tenantId, request.Phone, ct))
        {
            _logger.SystemInfo($"Trigger skipped (opted out): tenant={tenantId}, phone={request.Phone}, event={request.Event}");
            return (null, ErrorCodes.OutboundRecipientOptedOut,
                $"Recipient {request.Phone} has opted out of messages", 409);
        }

        // Find matching trigger template
        var template = await _repository.GetTriggerTemplateAsync(tenantId, request.Event, ct);
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

        // Insert single message (no broadcast_id)
        var messageId = await _repository.InsertMessageAsync(
            tenantId, null, template.Id, request.Phone, messageText, ct);

        _logger.SystemInfo(
            $"Trigger message queued: id={messageId}, tenant={tenantId}, " +
            $"event={request.Event}, phone={request.Phone}, template={template.Id}");

        return (new TriggerWebhookResponse
        {
            MessageId = messageId,
            TemplateId = template.Id,
            TemplateName = template.Name
        }, null, null, 202);
    }
}
