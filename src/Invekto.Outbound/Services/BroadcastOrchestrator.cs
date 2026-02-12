using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

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
    private readonly JsonLinesLogger _logger;

    public BroadcastOrchestrator(
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

        // Collect valid phones for batch opt-out check
        var validRecipients = request.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
            .ToList();

        // Batch opt-out check (single query instead of N queries)
        var phones = validRecipients.Select(r => r.Phone).ToList();
        var optedOutPhones = await _repository.BatchCheckOptOutsAsync(tenantId, phones, ct);

        // Filter and prepare messages
        var skippedOptout = 0;
        var messagesToInsert = new List<(string phone, string text)>();

        foreach (var recipient in validRecipients)
        {
            if (optedOutPhones.Contains(recipient.Phone))
            {
                skippedOptout++;
                continue;
            }

            // Apply template variables
            var (messageText, missingVars) = _templateEngine.Substitute(
                template.MessageTemplate, recipient.Variables);

            if (missingVars.Count > 0)
            {
                _logger.SystemWarn(
                    $"Broadcast skipping {recipient.Phone}: missing variables [{string.Join(", ", missingVars)}]");
                continue;
            }

            messagesToInsert.Add((recipient.Phone, messageText));
        }

        if (messagesToInsert.Count == 0)
        {
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload,
                "No valid recipients after opt-out filtering and variable validation");
        }

        // Create broadcast record
        var broadcastId = await _repository.CreateBroadcastAsync(
            tenantId, request.TemplateId, request.Recipients.Count,
            messagesToInsert.Count, request.ScheduledAt, ct);

        // Batch insert all messages (single multi-row INSERT)
        await _repository.BatchInsertMessagesAsync(
            tenantId, broadcastId, request.TemplateId, messagesToInsert, ct);
        var queuedCount = messagesToInsert.Count;

        _logger.SystemInfo(
            $"Broadcast created: id={broadcastId}, tenant={tenantId}, " +
            $"total={request.Recipients.Count}, queued={queuedCount}, skipped_optout={skippedOptout}");

        return (new BroadcastSendResponse
        {
            BroadcastId = broadcastId,
            TotalRecipients = request.Recipients.Count,
            Queued = queuedCount,
            SkippedOptout = skippedOptout
        }, null, null);
    }
}
