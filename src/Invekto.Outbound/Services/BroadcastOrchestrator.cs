using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Outbound.Services;

/// <summary>
/// Orchestrates broadcast creation: validates, checks opt-outs,
/// applies template, inserts messages as 'queued'.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class BroadcastOrchestrator
{
    private readonly OutboundRepository _repository;
    private readonly TemplateEngine _templateEngine;
    private readonly OptOutManager _optOutManager;
    private readonly ConsentManager _consentManager;
    private readonly DynamicMessageValidator _dynamicValidator;
    private readonly JsonLinesLogger _logger;

    public BroadcastOrchestrator(
        OutboundRepository repository,
        TemplateEngine templateEngine,
        OptOutManager optOutManager,
        ConsentManager consentManager,
        DynamicMessageValidator dynamicValidator,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _templateEngine = templateEngine;
        _optOutManager = optOutManager;
        _consentManager = consentManager;
        _dynamicValidator = dynamicValidator;
        _logger = logger;
    }

    /// <summary>
    /// Create a broadcast: validate template, filter opt-outs, insert messages.
    /// Returns the broadcast response or an error tuple.
    /// </summary>
    public async Task<(BroadcastSendResponse? response, string? errorCode, string? errorMessage)>
        CreateBroadcastAsync(
            int tenantId, BroadcastSendRequest request, CancellationToken ct = default)
    {
        // Validate recipients count
        if (request.Recipients == null || request.Recipients.Count == 0)
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "recipients is required and cannot be empty");

        if (request.Recipients.Count > 1000)
            return (null, ErrorCodes.OutboundTooManyRecipients, $"Max 1000 recipients per broadcast, got {request.Recipients.Count}");

        // Validate template exists
        var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct);
        if (template == null)
            return (null, ErrorCodes.OutboundTemplateNotFound, $"Template {request.TemplateId} not found or inactive");

        // GR-2.3: Resolve broadcast language (request override > template lang)
        var lang = request.Lang ?? template.Lang;

        // FEAT-DMP: per-broadcast DynamicMessage activation gate (interview Q5).
        // enable_dynamic_message=FALSE forces legacy INSE substitution regardless of placeholder shape.
        // Placeholder scan runs once per broadcast (template is shared across recipients).
        var dynamicEnabled = await _repository.GetEnableDynamicMessageAsync(tenantId, ct);
        var validation = await _dynamicValidator.ValidateAsync(tenantId, template.MessageTemplate, ct);
        var useDynamic = dynamicEnabled && validation.HasPlaceholders && validation.IsValid;
        string[]? broadcastDynamicFields = useDynamic ? validation.InmaFieldKeys.ToArray() : null;

        if (dynamicEnabled && validation.HasPlaceholders && !validation.IsValid)
        {
            // Unknown placeholders + flag TRUE = template references a key outside the INMA
            // allowlist AND TFM doesn't map it. Fall through to legacy TemplateEngine.Substitute
            // which will consume recipient.Variables — if the token is a user-variable the
            // broadcast succeeds; if not, recipient is skipped per its own missingVars path.
            // Logged once per broadcast so the tenant can spot stale placeholders in their template.
            _logger.SystemWarn(
                $"[{ErrorCodes.DynamicFieldValidationFailed}] Broadcast template has non-INMA placeholders: " +
                $"tenant={tenantId}, template={request.TemplateId}, unknown=[{string.Join(",", validation.UnknownPlaceholders)}]");
        }

        // Collect valid phones for batch opt-out check
        var validRecipients = request.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
            .ToList();

        // Batch opt-out check (single query instead of N queries)
        var phones = validRecipients.Select(r => r.Phone).ToList();
        var optedOutPhones = await _repository.BatchCheckOptOutsAsync(tenantId, phones, ct);

        // GR-2.6: Check if health tenant (once per broadcast, not per message)
        var (healthSettingsJson, healthSector) = await _repository.GetTenantHealthInfoAsync(tenantId, ct);
        var isHealthTenant = KvkkHelper.IsHealthTenant(healthSettingsJson, healthSector);

        // GR-3.26: Batch marketing consent check (broadcasts are marketing)
        var nonOptoutPhones = validRecipients
            .Where(r => !optedOutPhones.Contains(r.Phone))
            .Select(r => r.Phone).ToList();
        var noConsentPhones = await _consentManager.GetPhonesWithoutMarketingConsentAsync(
            tenantId, nonOptoutPhones, ct);

        // Filter and prepare messages
        var skippedOptout = 0;
        var skippedConsent = 0;
        var messagesToInsert = new List<(string phone, string text, string[]? dynamicFields)>();

        foreach (var recipient in validRecipients)
        {
            if (optedOutPhones.Contains(recipient.Phone))
            {
                skippedOptout++;
                continue;
            }

            // GR-3.26: Skip if no marketing consent
            if (noConsentPhones.Contains(recipient.Phone))
            {
                skippedConsent++;
                continue;
            }

            string messageText;
            string[]? recipientDynamicFields = null;
            if (useDynamic)
            {
                // FEAT-DMP: raw template text ships to INMA which resolves placeholders
                // from Customer DB. TemplateEngine.Substitute is bypassed entirely —
                // recipient.Variables is ignored in dynamic mode (INMA doesn't use it).
                messageText = template.MessageTemplate;
                recipientDynamicFields = broadcastDynamicFields;
            }
            else
            {
                var (substituted, missingVars) = _templateEngine.Substitute(
                    template.MessageTemplate, recipient.Variables);

                if (missingVars.Count > 0)
                {
                    _logger.SystemWarn(
                        $"Broadcast skipping {recipient.Phone}: missing variables [{string.Join(", ", missingVars)}]");
                    continue;
                }
                messageText = substituted;
            }

            // GR-2.6.1: Append KVKK health disclaimer if applicable
            var finalText = KvkkHelper.AppendDisclaimerIfHealth(messageText, isHealthTenant);
            messagesToInsert.Add((recipient.Phone, finalText, recipientDynamicFields));
        }

        if (messagesToInsert.Count == 0)
        {
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload,
                "No valid recipients after opt-out filtering and variable validation");
        }

        // Create broadcast record (GR-2.3: with language)
        var broadcastId = await _repository.CreateBroadcastAsync(
            tenantId, request.TemplateId, request.Recipients.Count,
            messagesToInsert.Count, request.ScheduledAt, lang, ct);

        // Batch insert all messages (single multi-row INSERT, GR-2.3: with language)
        await _repository.BatchInsertMessagesAsync(
            tenantId, broadcastId, request.TemplateId, messagesToInsert, lang, ct);
        var queuedCount = messagesToInsert.Count;

        // GR-3.29: Audit trail - batch insert for compliance (single multi-row INSERT).
        // AuditTrail records the rendered MessageText only (DynamicFields is per-message
        // metadata, not customer-visible content) — project to the legacy (phone, content) shape.
        var auditRecords = messagesToInsert
            .Select(m => (phone: m.phone, content: m.text))
            .ToList();
        await _repository.BatchInsertAuditTrailAsync(
            tenantId, request.TemplateId, null, auditRecords, ct);

        _logger.SystemInfo(
            $"Broadcast created: id={broadcastId}, tenant={tenantId}, " +
            $"total={request.Recipients.Count}, queued={queuedCount}, " +
            $"skipped_optout={skippedOptout}, skipped_consent={skippedConsent}");

        return (new BroadcastSendResponse
        {
            BroadcastId = broadcastId,
            TotalRecipients = request.Recipients.Count,
            Queued = queuedCount,
            SkippedOptout = skippedOptout,
            SkippedConsent = skippedConsent
        }, null, null);
    }
}
