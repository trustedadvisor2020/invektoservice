using System.Diagnostics;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.5: Objection Map engine.
/// PG-only compute — extracts objection types from evidence text of offer_lost conversations.
/// Keyword-based classification into 10 objection categories.
/// </summary>
public sealed class InsightObjectionMapService
{
    private readonly InsightRepository _insightRepo;
    private readonly JsonLinesLogger _logger;

    public InsightObjectionMapService(InsightRepository insightRepo, JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _logger = logger;
    }

    /// <summary>
    /// Compute objection map for a tenant.
    /// 1. Read offer_lost outcomes with evidence from PG
    /// 2. Classify objection type from evidence text (keyword-based)
    /// 3. Upsert to wa_objection_map
    /// </summary>
    public async Task<ObjectionMapComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"obj-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[Objection] Starting compute for tenant {request.TenantId}, instance={request.InstanceId}", rid);

        // 1. Get offer_lost outcomes with evidence from PG
        var outcomes = await _insightRepo.GetOfferLostWithEvidenceAsync(
            request.TenantId, request.InstanceId, ct);

        if (outcomes.Count == 0)
        {
            _logger.StepInfo($"[Objection] No offer_lost outcomes for tenant {request.TenantId}", rid);
            return new ObjectionMapComputeResult { DurationMs = sw.ElapsedMilliseconds };
        }

        _logger.StepInfo($"[Objection] Found {outcomes.Count} offer_lost outcomes", rid);

        // 2. Classify objection type from evidence text
        var records = new List<ObjectionMapRecord>();
        var noEvidence = 0;

        foreach (var (convId, instanceId, evidence, outcomeLabel) in outcomes)
        {
            if (string.IsNullOrWhiteSpace(evidence))
            {
                noEvidence++;
                continue;
            }

            var objectionType = ObjectionTypes.ClassifyFromEvidence(evidence);

            records.Add(new ObjectionMapRecord
            {
                TenantId = request.TenantId,
                InstanceId = instanceId,
                ConversationId = convId,
                ObjectionType = objectionType,
                Detail = evidence.Length > 500 ? evidence[..500] : evidence,
                OutcomeLabel = outcomeLabel
            });
        }

        _logger.StepInfo($"[Objection] Classified {records.Count} objections, {noEvidence} no evidence", rid);

        // 3. Upsert to PG
        if (records.Count > 0)
        {
            await _insightRepo.DeleteObjectionMapAsync(request.TenantId, request.InstanceId, ct);
            await _insightRepo.UpsertObjectionMapAsync(records, ct);
            _logger.StepInfo($"[Objection] Upserted {records.Count} objection records to PG", rid);
        }

        sw.Stop();
        _logger.StepInfo($"[Objection] Compute complete: {records.Count} objections, {sw.ElapsedMilliseconds}ms", rid);

        return new ObjectionMapComputeResult
        {
            TotalOfferLost = outcomes.Count,
            TotalObjections = records.Count,
            NoEvidence = noEvidence,
            DurationMs = sw.ElapsedMilliseconds
        };
    }
}
