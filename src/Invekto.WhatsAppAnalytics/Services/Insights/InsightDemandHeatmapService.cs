using System.Diagnostics;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Microsoft.Data.SqlClient;

namespace Invekto.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.2: Demand Heatmap engine.
/// Computes DayOfWeek(0-6) x HourOfDay(0-23) traffic matrix with outcome correlation.
/// Each cell contains total_conversations, sale_count, conversion_rate, avg_response_time_ms.
/// </summary>
public sealed class InsightDemandHeatmapService
{
    private readonly InsightRepository _insightRepo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly JsonLinesLogger _logger;

    private const int MssqlBatchSize = 200;

    public InsightDemandHeatmapService(
        InsightRepository insightRepo,
        MssqlReaderService mssqlReader,
        JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _mssqlReader = mssqlReader;
        _logger = logger;
    }

    /// <summary>
    /// Compute demand heatmap for all classified conversations of a tenant.
    /// Reads outcomes from PG, queries MSSQL for first message timestamps,
    /// aggregates into 7x24 grid, correlates with response times and outcomes.
    /// </summary>
    public async Task<DemandHeatmapComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"dh-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[DemandHeatmap] Starting compute for tenant {request.TenantId}, db={request.Database}, instance={request.InstanceId}", rid);

        // 1. Get classified outcomes from PG
        var outcomes = await _insightRepo.GetOutcomesForTenantAsync(request.TenantId, ct);
        if (outcomes.Count == 0)
        {
            _logger.StepInfo($"[DemandHeatmap] No classified outcomes for tenant {request.TenantId}", rid);
            return new DemandHeatmapComputeResult { DurationMs = sw.ElapsedMilliseconds };
        }

        _logger.StepInfo($"[DemandHeatmap] Found {outcomes.Count} classified outcomes", rid);

        // 2. Build lookup: conversation_id -> outcome_label
        var outcomeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (convId, label) in outcomes)
            outcomeLookup[convId] = label;

        // 3. Get response times from PG (for avg_response_time per cell)
        var responseTimes = await _insightRepo.GetResponseTimesByConversationAsync(request.TenantId, ct);

        // 4. Query MSSQL in batches for first customer message timestamps
        var allTimestamps = new List<ConversationTimestamp>();
        var errorCount = 0;
        var phoneNumbers = outcomeLookup.Keys.ToList();

        foreach (var batch in phoneNumbers.Chunk(MssqlBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var batchResults = await QueryFirstMessageTimestampsAsync(
                    request.Database, batch.ToList(), request.InstanceId, ct);
                allTimestamps.AddRange(batchResults);
            }
            catch (SqlException ex)
            {
                errorCount += batch.Length;
                _logger.SystemWarn($"[DemandHeatmap] MSSQL batch error ({batch.Length} items): {ex.Message}");
            }
        }

        _logger.StepInfo($"[DemandHeatmap] Got {allTimestamps.Count} timestamps from MSSQL, {errorCount} errors", rid);

        // 5. Deduplicate by conversation_id (multi-instance phones → take first)
        var deduped = allTimestamps
            .GroupBy(t => t.ConversationId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (deduped.Count < allTimestamps.Count)
            _logger.StepInfo($"[DemandHeatmap] Deduplicated {allTimestamps.Count} → {deduped.Count} (multi-instance phones)", rid);

        // 6. Aggregate into grid keyed by (dayOfWeek, hourOfDay, instanceId)
        // Instance-level granularity: each instance gets its own cells
        var grid = new Dictionary<(int day, int hour, int instanceId), List<string>>();
        foreach (var ts in deduped)
        {
            // DayOfWeek: .NET Sunday=0, we want Monday=0
            var dotnetDay = ts.FirstMessageAt.DayOfWeek;
            var day = dotnetDay == DayOfWeek.Sunday ? 6 : (int)dotnetDay - 1;
            var hour = ts.FirstMessageAt.Hour;
            var instId = ts.InstanceId ?? 0; // default 0 for NULL instance (avoids PG UNIQUE NULL issue)
            var key = (day, hour, instId);

            if (!grid.ContainsKey(key))
                grid[key] = new List<string>();
            grid[key].Add(ts.ConversationId);
        }

        // 7. Build records with outcome + response time correlation
        var records = new List<DemandHeatmapRecord>();
        foreach (var ((day, hour, instId), conversationIds) in grid)
        {
            var totalConvs = conversationIds.Count;
            var saleCount = 0;
            var rtSum = 0L;
            var rtCount = 0;

            foreach (var convId in conversationIds)
            {
                if (outcomeLookup.TryGetValue(convId, out var label) &&
                    (label == "sale" || label == "appointment_booked"))
                    saleCount++;

                if (responseTimes.TryGetValue(convId, out var rtMs))
                {
                    rtSum += rtMs;
                    rtCount++;
                }
            }

            records.Add(new DemandHeatmapRecord
            {
                TenantId = request.TenantId,
                InstanceId = instId,
                DayOfWeek = day,
                HourOfDay = hour,
                TotalConversations = totalConvs,
                SaleCount = saleCount,
                ConversionRate = totalConvs > 0 ? Math.Round((double)saleCount / totalConvs * 100, 1) : 0,
                AvgResponseTimeMs = rtCount > 0 ? rtSum / rtCount : null
            });
        }

        // 8. Upsert into PG
        if (records.Count > 0)
        {
            await _insightRepo.UpsertDemandHeatmapAsync(records, ct);
            _logger.StepInfo($"[DemandHeatmap] Upserted {records.Count} cells to PG", rid);
        }

        sw.Stop();
        _logger.StepInfo($"[DemandHeatmap] Compute complete: {records.Count} cells, {sw.ElapsedMilliseconds}ms", rid);

        return new DemandHeatmapComputeResult
        {
            TotalOutcomes = outcomes.Count,
            TotalMessages = deduped.Count,
            CellsWritten = records.Count,
            Errors = errorCount,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Query MSSQL for first customer message timestamp per conversation.
    /// Groups by CustomerPhoneNumber. Optionally filters by InstanceID.
    /// </summary>
    private async Task<List<ConversationTimestamp>> QueryFirstMessageTimestampsAsync(
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
                MIN(ISNULL(CM.SentTime, CM.CreateDate)) AS FirstMessageAt
            FROM Chats C WITH (NOLOCK)
            INNER JOIN ChatMessages CM WITH (NOLOCK) ON CM.ChatID = C.ID
            WHERE C.CustomerPhoneNumber IN ({string.Join(",", paramNames)})
                AND C.IsGroup = 0
                AND CM.MessageType = 1
                AND CM.SystemMessageType IS NULL
                AND CM.FromMe = 0
                {instanceFilter}
            GROUP BY C.CustomerPhoneNumber, C.InstanceID";

        var results = new List<ConversationTimestamp>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.IsDBNull(2)) continue; // skip if no first message timestamp

            results.Add(new ConversationTimestamp
            {
                ConversationId = reader.GetString(0),
                InstanceId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                FirstMessageAt = reader.GetDateTime(2)
            });
        }

        return results;
    }
}
