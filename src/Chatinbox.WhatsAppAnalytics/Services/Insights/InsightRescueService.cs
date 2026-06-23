using System.Diagnostics;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;
using Microsoft.Data.SqlClient;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI-3.6: Follow-up Rescue Alerts engine.
/// Identifies rescue candidates from no_response/offered/offer_lost outcomes
/// within 30-day window. Queries MSSQL for last message timestamps.
/// rescue_priority_score = recency(50%) + outcome_weight(50%).
/// </summary>
public sealed class InsightRescueService
{
    private readonly InsightRepository _insightRepo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly JsonLinesLogger _logger;

    private const int MssqlBatchSize = 200;
    private const int MaxDaysCutoff = 30;

    public InsightRescueService(
        InsightRepository insightRepo,
        MssqlReaderService mssqlReader,
        JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _mssqlReader = mssqlReader;
        _logger = logger;
    }

    /// <summary>
    /// Compute rescue candidates for a tenant.
    /// 1. Read rescue-eligible outcomes from PG (no_response, offered, offer_lost)
    /// 2. Query MSSQL for last message timestamps per conversation
    /// 3. Filter by 30-day cutoff
    /// 4. Compute rescue_priority_score
    /// 5. Upsert to wa_rescue_candidates
    /// </summary>
    public async Task<RescueComputeResult> ComputeAsync(
        InsightComputeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"rc-{request.TenantId}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[Rescue] Starting compute for tenant {request.TenantId}, db={request.Database}, instance={request.InstanceId}", rid);

        // 1. Get rescue-eligible outcomes from PG
        var outcomes = await _insightRepo.GetRescueEligibleOutcomesAsync(request.TenantId, ct);
        if (outcomes.Count == 0)
        {
            _logger.StepInfo($"[Rescue] No rescue-eligible outcomes for tenant {request.TenantId}", rid);
            return new RescueComputeResult { DurationMs = sw.ElapsedMilliseconds };
        }

        _logger.StepInfo($"[Rescue] Found {outcomes.Count} rescue-eligible outcomes", rid);

        // 2. Build outcome lookup
        var outcomeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (convId, label) in outcomes)
            outcomeLookup[convId] = label;

        // 3. Query MSSQL in batches for last message timestamps
        var allMessages = new List<ConversationLastMessage>();
        var errorCount = 0;
        var phoneNumbers = outcomeLookup.Keys.ToList();

        foreach (var batch in phoneNumbers.Chunk(MssqlBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var batchMessages = await QueryLastMessagesFromMssqlAsync(
                    request.Database, batch.ToList(), request.InstanceId, ct);
                allMessages.AddRange(batchMessages);
            }
            catch (SqlException ex)
            {
                errorCount += batch.Length;
                _logger.SystemWarn($"[Rescue] MSSQL batch error ({batch.Length} items): {ex.Message}");
            }
        }

        _logger.StepInfo($"[Rescue] Got {allMessages.Count} last message records from MSSQL, {errorCount} errors", rid);

        // 4. Deduplicate by conversation_id (same phone in multiple instances -> take most recent)
        var deduped = allMessages
            .GroupBy(m => m.ConversationId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(m => m.LastMessageAt).First())
            .ToList();

        if (deduped.Count < allMessages.Count)
            _logger.StepInfo($"[Rescue] Deduplicated {allMessages.Count} -> {deduped.Count} (multi-instance phones)", rid);

        // 5. Filter by 30-day cutoff + build rescue records
        var now = DateTime.UtcNow;
        var rescueRecords = new List<RescueCandidateRecord>();
        var excluded30Days = 0;

        foreach (var msg in deduped)
        {
            var daysSince = msg.LastMessageAt.HasValue
                ? (int)(now - msg.LastMessageAt.Value).TotalDays
                : MaxDaysCutoff; // No timestamp = treat as edge of cutoff

            if (daysSince > MaxDaysCutoff)
            {
                excluded30Days++;
                continue;
            }

            if (!outcomeLookup.TryGetValue(msg.ConversationId, out var outcomeLabel))
                continue;

            rescueRecords.Add(new RescueCandidateRecord
            {
                TenantId = request.TenantId,
                ConversationId = msg.ConversationId,
                InstanceId = msg.InstanceId,
                OutcomeLabel = outcomeLabel,
                LastMessageAt = msg.LastMessageAt,
                LastMessageFrom = msg.LastMessageFromCustomer ? "customer" : "agent",
                DaysSince = daysSince,
                RescuePriorityScore = InsightRescueScoring.CalculatePriorityScore(daysSince, outcomeLabel)
            });
        }

        _logger.StepInfo($"[Rescue] {rescueRecords.Count} candidates within {MaxDaysCutoff}-day window, {excluded30Days} excluded", rid);

        // 6. Upsert to PG
        if (rescueRecords.Count > 0)
        {
            await _insightRepo.UpsertRescueCandidatesAsync(rescueRecords, ct);
            _logger.StepInfo($"[Rescue] Upserted {rescueRecords.Count} rescue candidates to PG", rid);
        }

        sw.Stop();
        _logger.StepInfo($"[Rescue] Compute complete: {rescueRecords.Count} candidates, {sw.ElapsedMilliseconds}ms", rid);

        return new RescueComputeResult
        {
            TotalOutcomes = outcomes.Count,
            EligibleOutcomes = outcomes.Count,
            RescueCandidates = rescueRecords.Count,
            Excluded30Days = excluded30Days,
            Errors = errorCount,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Query MSSQL for last message per conversation.
    /// Returns last message timestamp + whether it was from customer (FromMe=0) or agent (FromMe=1).
    /// </summary>
    private async Task<List<ConversationLastMessage>> QueryLastMessagesFromMssqlAsync(
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

        // Get last message per conversation: MAX(SentTime/CreateDate) + FromMe of that last message
        cmd.CommandText = $@"
            WITH LastMsg AS (
                SELECT
                    C.CustomerPhoneNumber AS ConversationId,
                    C.InstanceID,
                    CM.FromMe,
                    ISNULL(CM.SentTime, CM.CreateDate) AS MsgTime,
                    ROW_NUMBER() OVER (
                        PARTITION BY C.CustomerPhoneNumber
                        ORDER BY ISNULL(CM.SentTime, CM.CreateDate) DESC
                    ) AS rn
                FROM Chats C WITH (NOLOCK)
                INNER JOIN ChatMessages CM WITH (NOLOCK) ON CM.ChatID = C.ID
                WHERE C.CustomerPhoneNumber IN ({string.Join(",", paramNames)})
                    AND C.IsGroup = 0
                    AND CM.MessageType = 1
                    AND CM.SystemMessageType IS NULL
                    {instanceFilter}
            )
            SELECT ConversationId, InstanceID, MsgTime, FromMe
            FROM LastMsg
            WHERE rn = 1";

        var results = new List<ConversationLastMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ConversationLastMessage
            {
                ConversationId = reader.GetString(0),
                InstanceId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                LastMessageAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                LastMessageFromCustomer = !reader.IsDBNull(3) && !reader.GetBoolean(3)
            });
        }

        return results;
    }
}
