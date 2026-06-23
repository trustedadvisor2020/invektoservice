using System.Diagnostics;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.7: Conversation Quality Score engine.
/// PG-only compute — combines response_time, outcome, and sentiment into quality score.
/// Per-conversation scoring: speed(35%) + resolution(40%) + sentiment(25%).
/// Engagement dimension placeholder (enriched later with MSSQL data).
/// </summary>
public sealed class InsightQualityService
{
    private readonly InsightRepository _insightRepo;
    private readonly JsonLinesLogger _logger;

    public InsightQualityService(InsightRepository insightRepo, JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _logger = logger;
    }

    /// <summary>
    /// Compute quality scores for all conversations of a tenant.
    /// 1. Read outcomes from PG (wa_conversation_outcomes)
    /// 2. Read response times from PG (wa_response_times)
    /// 3. Read sentiments from PG (wa_sentiments)
    /// 4. Read agent assignments from PG (wa_agent_metrics for agent mapping)
    /// 5. Compute per-conversation quality score
    /// 6. Upsert to wa_quality_scores
    /// </summary>
    public async Task<QualityComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"qs-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[Quality] Starting compute for tenant {request.TenantId}, instance={request.InstanceId}", rid);

        // 1. Get all outcomes
        var outcomes = await _insightRepo.GetOutcomesForTenantAsync(request.TenantId, ct);
        if (outcomes.Count == 0)
            throw new InvalidOperationException("No classified outcomes found for this tenant");

        _logger.StepInfo($"[Quality] Found {outcomes.Count} outcomes", rid);

        // 2. Get response times (conversation_id -> bucket)
        var responseTimes = await _insightRepo.GetResponseTimeBucketsAsync(request.TenantId, ct);
        _logger.StepInfo($"[Quality] Found {responseTimes.Count} response time records", rid);

        // 3. Get sentiments (conversation_id -> score)
        var sentiments = await _insightRepo.GetSentimentScoresAsync(request.TenantId, ct);
        _logger.StepInfo($"[Quality] Found {sentiments.Count} sentiment records", rid);

        // 4. Get agent mapping from wa_agent_metrics (conversation_id -> agent)
        var agentMap = await _insightRepo.GetConversationAgentMapAsync(request.TenantId, ct);
        _logger.StepInfo($"[Quality] Found {agentMap.Count} agent mappings", rid);

        // 5. Compute per-conversation quality scores
        var records = new List<QualityScoreRecord>();
        var noResponseTime = 0;

        foreach (var (convId, outcomeLabel) in outcomes)
        {
            // Speed score from response time bucket
            double speedScore;
            if (responseTimes.TryGetValue(convId, out var bucket))
            {
                speedScore = QualityScoring.BucketToSpeedScore(bucket);
            }
            else
            {
                speedScore = 0;
                noResponseTime++;
            }

            // Resolution score from outcome
            var resolutionScore = QualityScoring.OutcomeToResolutionScore(outcomeLabel);

            // Sentiment score
            var sentimentScore = sentiments.TryGetValue(convId, out var sentVal)
                ? QualityScoring.SentimentToScore(sentVal)
                : 50.0; // neutral default

            // Engagement placeholder (0 = no data, redistributes weights)
            const double engagementScore = 0.0;

            // Overall score
            var overallScore = QualityScoring.ComputeOverall(speedScore, engagementScore, resolutionScore, sentimentScore);

            // Agent info
            int? agentId = null;
            string? agentName = null;
            var instanceId = 0;
            if (agentMap.TryGetValue(convId, out var agent))
            {
                agentId = agent.AgentId;
                agentName = agent.AgentName;
                instanceId = agent.InstanceId;
            }

            records.Add(new QualityScoreRecord
            {
                TenantId = request.TenantId,
                InstanceId = instanceId,
                ConversationId = convId,
                AgentId = agentId,
                AgentName = agentName,
                ResponseSpeedScore = speedScore,
                EngagementScore = engagementScore,
                ResolutionScore = resolutionScore,
                SentimentScore = Math.Round(sentimentScore, 1),
                OverallScore = overallScore
            });
        }

        _logger.StepInfo($"[Quality] Computed {records.Count} quality scores, {noResponseTime} without response time", rid);

        // 6. Upsert to PG
        if (records.Count > 0)
        {
            await _insightRepo.DeleteQualityScoresAsync(request.TenantId, request.InstanceId, ct);
            await _insightRepo.UpsertQualityScoresAsync(records, ct);
        }

        // Compute average
        var avgScore = records.Count > 0 ? Math.Round(records.Average(r => r.OverallScore), 1) : 0;

        sw.Stop();
        _logger.StepInfo($"[Quality] Compute complete: {records.Count} scores, avg={avgScore}, {sw.ElapsedMilliseconds}ms", rid);

        return new QualityComputeResult
        {
            TotalOutcomes = outcomes.Count,
            TotalScored = records.Count,
            NoResponseTime = noResponseTime,
            AvgOverallScore = avgScore,
            DurationMs = sw.ElapsedMilliseconds
        };
    }
}
