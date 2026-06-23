using System.Diagnostics;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;
using Microsoft.Data.SqlClient;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.1: Response Time Correlation engine.
/// Computes first-customer-msg to first-agent-response delta per conversation,
/// buckets into 5 time ranges, and correlates with outcome labels.
/// </summary>
public sealed class InsightResponseTimeService
{
    private readonly InsightRepository _insightRepo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly JsonLinesLogger _logger;

    private const int MssqlBatchSize = 200;

    public InsightResponseTimeService(
        InsightRepository insightRepo,
        MssqlReaderService mssqlReader,
        JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _mssqlReader = mssqlReader;
        _logger = logger;
    }

    /// <summary>
    /// Compute response times for all classified conversations of a tenant.
    /// Reads conversation IDs from PG, queries MSSQL for timestamps, stores results.
    /// </summary>
    public async Task<ResponseTimeComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"rt-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[ResponseTime] Starting compute for tenant {request.TenantId}, db={request.Database}, instance={request.InstanceId}", rid);

        // 1. Get classified outcomes from PG
        var outcomes = await _insightRepo.GetOutcomesForTenantAsync(request.TenantId, ct);
        if (outcomes.Count == 0)
        {
            _logger.StepInfo($"[ResponseTime] No classified outcomes for tenant {request.TenantId}", rid);
            return new ResponseTimeComputeResult { DurationMs = sw.ElapsedMilliseconds };
        }

        _logger.StepInfo($"[ResponseTime] Found {outcomes.Count} classified outcomes", rid);

        // 2. Build lookup: conversation_id -> outcome_label
        var outcomeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (convId, label) in outcomes)
            outcomeLookup[convId] = label;

        // 3. Query MSSQL in batches for response times
        var allRecords = new List<ResponseTimeRecord>();
        var errorCount = 0;
        var phoneNumbers = outcomeLookup.Keys.ToList();

        foreach (var batch in phoneNumbers.Chunk(MssqlBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var batchRecords = await QueryResponseTimesFromMssqlAsync(
                    request.Database, batch.ToList(), request.InstanceId, request.TenantId, ct);
                allRecords.AddRange(batchRecords);
            }
            catch (SqlException ex)
            {
                errorCount += batch.Length;
                _logger.SystemWarn($"[ResponseTime] MSSQL batch error ({batch.Length} items): {ex.Message}");
            }
        }

        _logger.StepInfo($"[ResponseTime] Got {allRecords.Count} response time records from MSSQL, {errorCount} errors", rid);

        // 4. Deduplicate by conversation_id (same phone in multiple instances → take first)
        var deduped = allRecords
            .GroupBy(r => r.ConversationId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (deduped.Count < allRecords.Count)
            _logger.StepInfo($"[ResponseTime] Deduplicated {allRecords.Count} → {deduped.Count} (multi-instance phones)", rid);

        // 5. Enrich with outcome labels and compute buckets
        foreach (var record in deduped)
        {
            if (outcomeLookup.TryGetValue(record.ConversationId, out var label))
                record.OutcomeLabel = label;

            record.Bucket = ResponseTimeBuckets.Classify(record.ResponseTimeMs);
        }

        // 6. Upsert into PG
        if (deduped.Count > 0)
        {
            await _insightRepo.UpsertResponseTimesAsync(deduped, ct);
            _logger.StepInfo($"[ResponseTime] Upserted {deduped.Count} records to PG", rid);
        }

        // 7. Build summary
        var bucketGroups = deduped
            .GroupBy(r => r.Bucket)
            .Select(g => new BucketSummary
            {
                Bucket = g.Key,
                BucketLabel = ResponseTimeBuckets.GetLabel(g.Key),
                Count = g.Count()
            })
            .OrderBy(b => Array.IndexOf(ResponseTimeBuckets.OrderedBuckets, b.Bucket))
            .ToList();

        sw.Stop();
        _logger.StepInfo($"[ResponseTime] Compute complete: {deduped.Count} records, {sw.ElapsedMilliseconds}ms", rid);

        return new ResponseTimeComputeResult
        {
            TotalOutcomes = outcomes.Count,
            TotalComputed = deduped.Count,
            Skipped = outcomes.Count - deduped.Count - errorCount,
            Errors = errorCount,
            Buckets = bucketGroups,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Query MSSQL for first customer message and first agent response timestamps.
    /// Groups by CustomerPhoneNumber. Optionally filters by InstanceID.
    /// </summary>
    private async Task<List<ResponseTimeRecord>> QueryResponseTimesFromMssqlAsync(
        string database, List<string> phoneNumbers, int? instanceId, int tenantId,
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
                MIN(CASE WHEN CM.FromMe = 0 THEN ISNULL(CM.SentTime, CM.CreateDate) END) AS FirstCustomerMsg,
                MIN(CASE WHEN CM.FromMe = 1 THEN ISNULL(CM.SentTime, CM.CreateDate) END) AS FirstAgentResponse
            FROM Chats C WITH (NOLOCK)
            INNER JOIN ChatMessages CM WITH (NOLOCK) ON CM.ChatID = C.ID
            WHERE C.CustomerPhoneNumber IN ({string.Join(",", paramNames)})
                AND C.IsGroup = 0
                AND CM.MessageType = 1
                AND CM.SystemMessageType IS NULL
                {instanceFilter}
            GROUP BY C.CustomerPhoneNumber, C.InstanceID";

        var results = new List<ResponseTimeRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var convId = reader.GetString(0);
            var instId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var firstCustomer = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
            var firstAgent = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);

            long? deltaMs = null;
            if (firstCustomer.HasValue && firstAgent.HasValue && firstAgent.Value > firstCustomer.Value)
                deltaMs = (long)(firstAgent.Value - firstCustomer.Value).TotalMilliseconds;

            results.Add(new ResponseTimeRecord
            {
                TenantId = tenantId,
                ConversationId = convId,
                InstanceId = instId,
                FirstCustomerMsgAt = firstCustomer,
                FirstAgentResponseAt = firstAgent,
                ResponseTimeMs = deltaMs
            });
        }

        return results;
    }
}
