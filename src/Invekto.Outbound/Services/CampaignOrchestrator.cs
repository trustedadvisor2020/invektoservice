using System.Text.Json;
using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

/// <summary>
/// Orchestrates campaign lifecycle: create, activate, A/B selection,
/// conversion tracking, and ROI reporting (GR-3.15).
/// Thread-safe, register as singleton.
/// </summary>
public sealed class CampaignOrchestrator
{
    private readonly OutboundRepository _repository;
    private readonly JsonLinesLogger _logger;

    private static readonly HashSet<string> ValidTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual", "scheduled", "event_based", "recurring"
    };

    private static readonly HashSet<string> ValidConversionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "reply", "purchase", "appointment", "click", "custom"
    };

    public CampaignOrchestrator(OutboundRepository repository, JsonLinesLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create a new campaign (draft status).
    /// </summary>
    public async Task<(CampaignResponse? response, string? errorCode, string? errorMessage)>
        CreateCampaignAsync(int tenantId, CampaignCreateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, ErrorCodes.OutboundInvalidCampaignPayload, "name is required");

        if (request.Name.Length > 200)
            return (null, ErrorCodes.OutboundInvalidCampaignPayload, "name must be 200 characters or less");

        if (!ValidTriggerTypes.Contains(request.TriggerType))
            return (null, ErrorCodes.OutboundInvalidCampaignPayload,
                $"trigger_type must be one of: {string.Join(", ", ValidTriggerTypes)}");

        if (request.TemplateId <= 0)
            return (null, ErrorCodes.OutboundInvalidCampaignPayload, "template_id is required");

        // Validate template exists
        var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct);
        if (template == null)
            return (null, ErrorCodes.OutboundTemplateNotFound,
                $"Template {request.TemplateId} not found or inactive");

        // Validate A/B template if specified
        if (request.AbTemplateId.HasValue)
        {
            var abTemplate = await _repository.GetTemplateByIdAsync(tenantId, request.AbTemplateId.Value, ct);
            if (abTemplate == null)
                return (null, ErrorCodes.OutboundTemplateNotFound,
                    $"A/B template {request.AbTemplateId} not found or inactive");
        }

        var criteriaJson = request.TargetCriteria != null
            ? JsonSerializer.Serialize(request.TargetCriteria)
            : null;

        var scheduleJson = request.Schedule != null
            ? JsonSerializer.Serialize(request.Schedule)
            : null;

        var id = await _repository.CreateCampaignAsync(
            tenantId, request.Name, request.TriggerType, request.TemplateId,
            request.AbTemplateId, request.AbSplitPct,
            criteriaJson, scheduleJson, ct);

        _logger.SystemInfo($"Campaign created: id={id}, name={request.Name}, type={request.TriggerType}");

        var campaign = await _repository.GetCampaignAsync(tenantId, id, ct);
        return (campaign, null, null);
    }

    /// <summary>
    /// Activate a campaign (draft → active/scheduled).
    /// </summary>
    public async Task<(bool success, string? errorCode, string? errorMessage)>
        ActivateCampaignAsync(int tenantId, int campaignId, CancellationToken ct = default)
    {
        var campaign = await _repository.GetCampaignAsync(tenantId, campaignId, ct);
        if (campaign == null)
            return (false, ErrorCodes.OutboundCampaignNotFound, $"Campaign {campaignId} not found");

        if (campaign.Status != "draft" && campaign.Status != "paused")
            return (false, ErrorCodes.OutboundCampaignAlreadyActive,
                $"Campaign {campaignId} is {campaign.Status}, can only activate from draft or paused");

        var newStatus = campaign.TriggerType == "scheduled" ? "scheduled" : "active";
        await _repository.UpdateCampaignStatusAsync(tenantId, campaignId, newStatus, ct);

        _logger.SystemInfo($"Campaign activated: id={campaignId}, status={newStatus}");
        return (true, null, null);
    }

    /// <summary>
    /// Select template for A/B testing. Returns template_id based on split percentage.
    /// </summary>
    public int SelectAbTemplate(CampaignResponse campaign)
    {
        if (!campaign.AbTemplateId.HasValue)
            return campaign.TemplateId;

        // Simple random split based on ab_split_pct
        var roll = Random.Shared.Next(100);
        return roll < campaign.AbSplitPct
            ? campaign.TemplateId      // Variant A
            : campaign.AbTemplateId.Value;  // Variant B
    }

    /// <summary>
    /// Record a conversion event.
    /// </summary>
    public async Task<(int? conversionId, string? errorCode, string? errorMessage)>
        RecordConversionAsync(int tenantId, ConversionRecordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConversionType))
            return (null, ErrorCodes.OutboundConversionRecordFailed, "conversion_type is required");

        if (!ValidConversionTypes.Contains(request.ConversionType))
            return (null, ErrorCodes.OutboundConversionRecordFailed,
                $"conversion_type must be one of: {string.Join(", ", ValidConversionTypes)}");

        if (request.MessageId == null && request.CampaignId == null)
            return (null, ErrorCodes.OutboundConversionRecordFailed,
                "Either message_id or campaign_id is required");

        var metadataJson = request.Metadata != null
            ? JsonSerializer.Serialize(request.Metadata)
            : null;

        var id = await _repository.RecordConversionAsync(
            tenantId, request.MessageId, request.CampaignId,
            request.ConversionType, request.ValueAmount,
            metadataJson, ct);

        _logger.SystemInfo(
            $"Conversion recorded: id={id}, type={request.ConversionType}, " +
            $"campaign={request.CampaignId}, value={request.ValueAmount}");

        return (id, null, null);
    }
}
