using System.Diagnostics;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.4: Revenue Attribution engine.
/// PG-only compute — no MSSQL round-trip.
/// Assigns fixed TL values to conversation outcomes and aggregates across 4 dimensions:
/// agent, hour, outcome, instance + summary row.
/// </summary>
public sealed class InsightRevenueService
{
    private readonly InsightRepository _insightRepo;
    private readonly JsonLinesLogger _logger;

    /// <summary>
    /// Fixed TL values per outcome label.
    /// sale=500, offered=150, offer_lost=50, return_or_complaint=-100, appointment_booked=200.
    /// </summary>
    private static readonly Dictionary<string, decimal> OutcomeValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sale"] = 500m,
        ["offered"] = 150m,
        ["offer_lost"] = 50m,
        ["return_or_complaint"] = -100m,
        ["appointment_booked"] = 200m,
        ["no_response"] = 0m,
        ["abandoned"] = 0m
    };

    public InsightRevenueService(InsightRepository insightRepo, JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _logger = logger;
    }

    /// <summary>
    /// Compute revenue attribution for a tenant from PG data only.
    /// Reads wa_conversation_outcomes, wa_response_times, wa_agent_metrics.
    /// Produces 4 dimension slices + summary, upserts to wa_revenue_attribution.
    /// </summary>
    public async Task<RevenueComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"rev-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[Revenue] Starting compute for tenant {request.TenantId}, instance={request.InstanceId}", rid);

        // 1. Get outcome counts grouped by (outcome_label, instance_id) from PG
        var outcomeCounts = await _insightRepo.GetOutcomeCountsGroupedAsync(
            request.TenantId, request.InstanceId, ct);

        if (outcomeCounts.Count == 0)
            throw new InvalidOperationException("No classified outcomes found for this tenant");

        _logger.StepInfo($"[Revenue] Found {outcomeCounts.Count} outcome groups", rid);

        // 2. Get hourly outcome counts from wa_response_times
        var hourlyCounts = await _insightRepo.GetHourlyOutcomeCountsAsync(
            request.TenantId, request.InstanceId, ct);

        _logger.StepInfo($"[Revenue] Found {hourlyCounts.Count} hourly outcome groups", rid);

        // 3. Get agent metrics for revenue calculation
        var agentMetrics = await _insightRepo.GetAgentMetricsForRevenueAsync(
            request.TenantId, request.InstanceId, ct);

        _logger.StepInfo($"[Revenue] Found {agentMetrics.Count} agents", rid);

        // 4. Build attribution records across all dimensions
        var records = new List<RevenueAttributionRecord>();

        // ── Outcome dimension: per outcome_label aggregate ──
        var outcomeAgg = outcomeCounts
            .GroupBy(o => o.OutcomeLabel, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Label = g.Key, Count = g.Sum(x => x.Count) });

        foreach (var oa in outcomeAgg)
        {
            var value = GetOutcomeValue(oa.Label);
            records.Add(new RevenueAttributionRecord
            {
                TenantId = request.TenantId,
                InstanceId = 0,
                Dimension = "outcome",
                DimensionKey = oa.Label,
                DimensionLabel = oa.Label,
                TotalConversations = oa.Count,
                AttributedRevenue = oa.Count * value,
                AvgRevenue = value
            });
        }

        // ── Hour dimension: per hour aggregate ──
        var hourAgg = hourlyCounts
            .GroupBy(h => h.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Total = g.Sum(x => x.Count),
                Revenue = g.Sum(x => x.Count * GetOutcomeValue(x.OutcomeLabel)),
                Breakdown = g
                    .GroupBy(x => x.OutcomeLabel, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.Count))
            });

        foreach (var ha in hourAgg)
        {
            records.Add(new RevenueAttributionRecord
            {
                TenantId = request.TenantId,
                InstanceId = 0,
                Dimension = "hour",
                DimensionKey = ha.Hour.ToString(),
                DimensionLabel = $"{ha.Hour:D2}:00",
                TotalConversations = ha.Total,
                AttributedRevenue = ha.Revenue,
                AvgRevenue = ha.Total > 0 ? Math.Round(ha.Revenue / ha.Total, 2) : 0,
                BreakdownJson = JsonSerializer.Serialize(ha.Breakdown)
            });
        }

        // ── Agent dimension: per agent from wa_agent_metrics ──
        foreach (var am in agentMetrics)
        {
            var revenue = am.SaleCount * GetOutcomeValue("sale")
                        + am.OfferedCount * GetOutcomeValue("offered")
                        + am.OfferLostCount * GetOutcomeValue("offer_lost")
                        + am.NoResponseCount * GetOutcomeValue("no_response")
                        + am.OtherCount * GetOutcomeValue("abandoned");

            var breakdown = new Dictionary<string, int>
            {
                ["sale"] = am.SaleCount,
                ["offered"] = am.OfferedCount,
                ["offer_lost"] = am.OfferLostCount,
                ["no_response"] = am.NoResponseCount,
                ["other"] = am.OtherCount
            };

            records.Add(new RevenueAttributionRecord
            {
                TenantId = request.TenantId,
                InstanceId = am.InstanceId ?? 0,
                Dimension = "agent",
                DimensionKey = am.AgentId.ToString(),
                DimensionLabel = am.AgentName,
                TotalConversations = am.TotalConversations,
                AttributedRevenue = revenue,
                AvgRevenue = am.TotalConversations > 0
                    ? Math.Round(revenue / am.TotalConversations, 2) : 0,
                BreakdownJson = JsonSerializer.Serialize(breakdown)
            });
        }

        // ── Instance dimension: per WA instance ──
        var instanceAgg = outcomeCounts
            .GroupBy(o => o.InstanceId)
            .Select(g => new
            {
                InstId = g.Key,
                Total = g.Sum(x => x.Count),
                Revenue = g.Sum(x => x.Count * GetOutcomeValue(x.OutcomeLabel)),
                Breakdown = g
                    .GroupBy(x => x.OutcomeLabel, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.Count))
            });

        foreach (var ia in instanceAgg)
        {
            records.Add(new RevenueAttributionRecord
            {
                TenantId = request.TenantId,
                InstanceId = 0,
                Dimension = "instance",
                DimensionKey = ia.InstId.ToString(),
                DimensionLabel = $"Instance {ia.InstId}",
                TotalConversations = ia.Total,
                AttributedRevenue = ia.Revenue,
                AvgRevenue = ia.Total > 0 ? Math.Round(ia.Revenue / ia.Total, 2) : 0,
                BreakdownJson = JsonSerializer.Serialize(ia.Breakdown)
            });
        }

        // ── Summary row ──
        var totalConv = outcomeCounts.Sum(x => x.Count);
        var totalRev = outcomeCounts.Sum(x => x.Count * GetOutcomeValue(x.OutcomeLabel));

        records.Add(new RevenueAttributionRecord
        {
            TenantId = request.TenantId,
            InstanceId = 0,
            Dimension = "summary",
            DimensionKey = "total",
            DimensionLabel = "Toplam",
            TotalConversations = totalConv,
            AttributedRevenue = totalRev,
            AvgRevenue = totalConv > 0 ? Math.Round(totalRev / totalConv, 2) : 0
        });

        // 5. Delete existing data for this tenant/instance, then upsert
        await _insightRepo.DeleteRevenueAttributionAsync(request.TenantId, request.InstanceId, ct);
        await _insightRepo.UpsertRevenueAttributionAsync(records, ct);

        sw.Stop();
        _logger.StepInfo($"[Revenue] Done: {records.Count} records, totalRevenue={totalRev:F0} TL, {sw.ElapsedMilliseconds}ms", rid);

        return new RevenueComputeResult
        {
            TotalOutcomes = totalConv,
            TotalRecords = records.Count,
            TotalRevenue = totalRev,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    private static decimal GetOutcomeValue(string outcomeLabel) =>
        OutcomeValues.TryGetValue(outcomeLabel, out var v) ? v : 0m;
}
