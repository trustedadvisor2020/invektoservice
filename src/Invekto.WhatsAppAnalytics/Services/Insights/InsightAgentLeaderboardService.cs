using System.Diagnostics;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Microsoft.Data.SqlClient;

namespace Invekto.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.3: Agent Leaderboard engine.
/// Computes per-agent metrics by joining MSSQL Chats.OwnerUserID+Users
/// with PG wa_conversation_outcomes+wa_response_times.
/// weighted_score = conversion(50%) + response_time_score(30%) + ghost_rate_inverse(20%).
/// </summary>
public sealed class InsightAgentLeaderboardService
{
    private readonly InsightRepository _insightRepo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly JsonLinesLogger _logger;

    private const int MssqlBatchSize = 200;

    // Weighted score weights (quality deferred to RI-3.7)
    private const double WeightConversion = 0.50;
    private const double WeightResponseTime = 0.30;
    private const double WeightGhostInverse = 0.20;

    // Response time normalization: 5min=100, 4h+=0
    private const long RtBestMs = 5 * 60_000;      // 5 minutes
    private const long RtWorstMs = 4 * 60 * 60_000; // 4 hours

    public InsightAgentLeaderboardService(
        InsightRepository insightRepo,
        MssqlReaderService mssqlReader,
        JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _mssqlReader = mssqlReader;
        _logger = logger;
    }

    /// <summary>
    /// Compute agent leaderboard metrics for a tenant.
    /// 1. Read outcomes from PG
    /// 2. Read response times from PG (already computed by RI-3.1)
    /// 3. Query MSSQL for conversation→agent mapping (OwnerUserID + Users, IsBotUser=0)
    /// 4. Aggregate per agent, compute rates + weighted_score
    /// 5. Upsert to wa_agent_metrics
    /// </summary>
    public async Task<AgentLeaderboardComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"al-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[AgentLeaderboard] Starting compute for tenant {request.TenantId}, db={request.Database}, instance={request.InstanceId}", rid);

        // 1. Get classified outcomes from PG
        var outcomes = await _insightRepo.GetOutcomesForTenantAsync(request.TenantId, ct);
        if (outcomes.Count == 0)
        {
            _logger.StepInfo($"[AgentLeaderboard] No classified outcomes for tenant {request.TenantId}", rid);
            return new AgentLeaderboardComputeResult { DurationMs = sw.ElapsedMilliseconds };
        }

        _logger.StepInfo($"[AgentLeaderboard] Found {outcomes.Count} classified outcomes", rid);

        // 2. Get response times from PG (for avg_response_time per agent)
        var responseTimes = await _insightRepo.GetResponseTimesByConversationAsync(request.TenantId, ct);
        _logger.StepInfo($"[AgentLeaderboard] Found {responseTimes.Count} response time records", rid);

        // 3. Build outcome lookup
        var outcomeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (convId, label) in outcomes)
            outcomeLookup[convId] = label;

        // 4. Query MSSQL for conversation→agent mapping
        var allMappings = new List<ConversationAgentMapping>();
        var errorCount = 0;
        var phoneNumbers = outcomeLookup.Keys.ToList();

        foreach (var batch in phoneNumbers.Chunk(MssqlBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var batchMappings = await QueryAgentMappingsFromMssqlAsync(
                    request.Database, batch.ToList(), request.InstanceId, ct);
                allMappings.AddRange(batchMappings);
            }
            catch (SqlException ex)
            {
                errorCount += batch.Length;
                _logger.SystemWarn($"[AgentLeaderboard] MSSQL batch error ({batch.Length} items): {ex.Message}");
            }
        }

        _logger.StepInfo($"[AgentLeaderboard] Got {allMappings.Count} agent mappings from MSSQL, {errorCount} errors", rid);

        // 5. Deduplicate by conversation_id (same phone in multiple instances → take first)
        var deduped = allMappings
            .GroupBy(m => m.ConversationId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (deduped.Count < allMappings.Count)
            _logger.StepInfo($"[AgentLeaderboard] Deduplicated {allMappings.Count} → {deduped.Count} (multi-instance phones)", rid);

        // Count unassigned (conversations not found in MSSQL or OwnerUserID was NULL/bot)
        var unassigned = outcomes.Count - deduped.Count - errorCount;

        // 6. Aggregate per agent+instance
        var agentGroups = deduped
            .GroupBy(m => (m.AgentId, m.InstanceId))
            .ToList();

        var agentMetrics = new List<AgentMetricRecord>();
        foreach (var group in agentGroups)
        {
            var agentId = group.Key.AgentId;
            var instanceId = group.Key.InstanceId;
            var agentName = group.First().AgentName;
            var convIds = group.Select(m => m.ConversationId).ToList();

            // Count outcomes by type
            var saleCount = 0;
            var offeredCount = 0;
            var noResponseCount = 0;
            var offerLostCount = 0;
            var otherCount = 0;

            foreach (var convId in convIds)
            {
                if (!outcomeLookup.TryGetValue(convId, out var label)) continue;
                switch (label)
                {
                    case "sale": saleCount++; break;
                    case "appointment_booked": saleCount++; break; // Counts as conversion
                    case "offered": offeredCount++; break;
                    case "no_response": noResponseCount++; break;
                    case "offer_lost": offerLostCount++; break;
                    default: otherCount++; break;
                }
            }

            var total = convIds.Count;
            var conversionRate = total > 0 ? Math.Round((double)saleCount / total * 100, 1) : 0;
            var ghostRate = total > 0 ? Math.Round((double)noResponseCount / total * 100, 1) : 0;

            // Avg response time from wa_response_times
            var rtValues = convIds
                .Where(c => responseTimes.ContainsKey(c))
                .Select(c => responseTimes[c])
                .ToList();
            long? avgRt = rtValues.Count > 0 ? (long)rtValues.Average() : null;

            // Weighted score
            var weightedScore = CalculateWeightedScore(conversionRate, avgRt, ghostRate);

            agentMetrics.Add(new AgentMetricRecord
            {
                TenantId = request.TenantId,
                InstanceId = instanceId,
                AgentId = agentId,
                AgentName = agentName,
                TotalConversations = total,
                SaleCount = saleCount,
                OfferedCount = offeredCount,
                NoResponseCount = noResponseCount,
                OfferLostCount = offerLostCount,
                OtherCount = otherCount,
                ConversionRate = conversionRate,
                AvgResponseTimeMs = avgRt,
                GhostRate = ghostRate,
                WeightedScore = weightedScore
            });
        }

        // 7. Upsert to PG
        if (agentMetrics.Count > 0)
        {
            await _insightRepo.UpsertAgentMetricsAsync(agentMetrics, ct);
            _logger.StepInfo($"[AgentLeaderboard] Upserted {agentMetrics.Count} agent metrics to PG", rid);
        }

        sw.Stop();
        _logger.StepInfo($"[AgentLeaderboard] Compute complete: {agentMetrics.Count} agents, {sw.ElapsedMilliseconds}ms", rid);

        return new AgentLeaderboardComputeResult
        {
            TotalOutcomes = outcomes.Count,
            TotalAssigned = deduped.Count,
            UnassignedCount = unassigned > 0 ? unassigned : 0,
            BotFilteredCount = allMappings.Count - deduped.Count,
            AgentCount = agentMetrics.Count,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Calculate weighted score: conversion(50%) + response_time_score(30%) + ghost_rate_inverse(20%).
    /// All inputs normalized to 0-100 scale.
    /// </summary>
    internal static double CalculateWeightedScore(double conversionRate, long? avgResponseTimeMs, double ghostRate)
    {
        // Conversion: already 0-100 (percentage)
        var convScore = Math.Min(conversionRate, 100.0);

        // Response time: 5min or less = 100, 4h+ = 0, linear interpolation
        double rtScore;
        if (avgResponseTimeMs is null)
            rtScore = 0; // No data = worst score
        else if (avgResponseTimeMs.Value <= RtBestMs)
            rtScore = 100;
        else if (avgResponseTimeMs.Value >= RtWorstMs)
            rtScore = 0;
        else
            rtScore = 100.0 * (RtWorstMs - avgResponseTimeMs.Value) / (RtWorstMs - RtBestMs);

        // Ghost rate inverse: 0% ghost = 100 score, 100% ghost = 0 score
        var ghostInverseScore = 100.0 - Math.Min(ghostRate, 100.0);

        var weighted = (convScore * WeightConversion) +
                       (rtScore * WeightResponseTime) +
                       (ghostInverseScore * WeightGhostInverse);

        return Math.Round(weighted, 1);
    }

    /// <summary>
    /// Query MSSQL for conversation→agent mapping using Chats.OwnerUserID + Users join.
    /// Filters: IsGroup=0, OwnerUserID IS NOT NULL, IsBotUser=0.
    /// </summary>
    private async Task<List<ConversationAgentMapping>> QueryAgentMappingsFromMssqlAsync(
        string database, List<string> phoneNumbers, int? instanceId,
        CancellationToken ct)
    {
        if (phoneNumbers.Count == 0) return [];

        await using var conn = await _mssqlReader.CreateConnectionAsync(database, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 120;

        // Build parameterized IN clause
        var paramNames = new List<string>();
        for (var i = 0; i < phoneNumbers.Count; i++)
        {
            var paramName = $"@p{i}";
            paramNames.Add(paramName);
            cmd.Parameters.AddWithValue(paramName, phoneNumbers[i]);
        }

        var instanceFilter = instanceId.HasValue ? "AND C.InstanceID = @instanceId" : "";
        if (instanceId.HasValue)
            cmd.Parameters.AddWithValue("@instanceId", instanceId.Value);

        cmd.CommandText = $@"
            SELECT
                C.CustomerPhoneNumber AS ConversationId,
                C.InstanceID,
                C.OwnerUserID AS AgentId,
                U.Name AS AgentName,
                U.Surname AS AgentSurname
            FROM Chats C WITH (NOLOCK)
            INNER JOIN Users U WITH (NOLOCK) ON U.ID = C.OwnerUserID
            WHERE C.CustomerPhoneNumber IN ({string.Join(",", paramNames)})
                AND C.IsGroup = 0
                AND C.OwnerUserID IS NOT NULL
                AND U.IsBotUser = 0
                {instanceFilter}";

        var results = new List<ConversationAgentMapping>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var convId = reader.GetString(0);
            var instId = reader.GetInt32(1);
            var agentId = reader.GetInt32(2);
            var name = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var surname = reader.IsDBNull(4) ? "" : reader.GetString(4);

            var fullName = string.IsNullOrWhiteSpace(surname)
                ? name.Trim()
                : $"{name.Trim()} {surname.Trim()}";

            results.Add(new ConversationAgentMapping
            {
                ConversationId = convId,
                InstanceId = instId,
                AgentId = agentId,
                AgentName = fullName
            });
        }

        return results;
    }
}
