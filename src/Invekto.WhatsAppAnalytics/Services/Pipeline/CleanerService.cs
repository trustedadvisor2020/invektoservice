using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 1: Clean raw CSV -> normalize text -> deduplicate -> insert to wa_messages.
/// C# port of Python 01_cleaner.py.
/// </summary>
public sealed class CleanerService
{
    private readonly AnalyticsRepository _repo;
    private readonly CsvStreamReader _csvReader;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    public CleanerService(AnalyticsRepository repo, CsvStreamReader csvReader, TextNormalizer normalizer, JsonLinesLogger logger)
    {
        _repo = repo;
        _csvReader = csvReader;
        _normalizer = normalizer;
        _logger = logger;
    }

    /// <summary>
    /// Run Stage 1: clean + dedup + insert.
    /// Returns total inserted message count.
    /// </summary>
    public async Task<int> RunAsync(int analysisId, int tenantId, string filePath, char delimiter,
        Func<StageProgress, Task> onProgress, CancellationToken ct)
    {
        _logger.SystemInfo($"[CleanerService] Starting Stage 1 for analysis {analysisId}");

        var totalLines = _csvReader.CountLines(filePath) - 1; // subtract header
        var processedRows = 0;
        var insertedTotal = 0;
        var duplicateCount = 0;
        var invalidCount = 0;

        // Track previous message per conversation for dedup
        var prevByConversation = new Dictionary<string, (string hash, DateTime timestamp)>();

        await foreach (var chunk in _csvReader.StreamChunksAsync(filePath, delimiter))
        {
            ct.ThrowIfCancellationRequested();

            var cleanedBatch = new List<CleanedMessage>();

            foreach (var row in chunk)
            {
                processedRows++;

                // Parse fields: business_phone;date;time;conversation_id;message_text;sender_type;agent_name
                var businessPhone = _normalizer.NormalizePhone(row[0]);
                var dateStr = row.Length > 1 ? row[1] : "";
                var timeStr = row.Length > 2 ? row[2] : "";
                var conversationId = row.Length > 3 ? row[3]?.Trim() ?? "" : "";
                var messageText = row.Length > 4 ? _normalizer.NormalizeText(row[4]) : "";
                var senderType = row.Length > 5 ? _normalizer.NormalizeSenderType(row[5]) : "";
                var agentName = row.Length > 6 ? _normalizer.NormalizeAgentName(row[6]) : "";

                // Validate: skip if critical fields missing
                var timestamp = _normalizer.TryParseTimestamp(dateStr, timeStr);
                if (timestamp == null)
                {
                    invalidCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(senderType))
                {
                    invalidCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(messageText))
                {
                    invalidCount++;
                    continue;
                }

                // For CUSTOMER messages, clear agent_name
                if (senderType == "CUSTOMER")
                    agentName = "";

                // Dedup: SHA256(conversation_id + clean_text)[:16], same conv + same hash + <=5s
                var cleanedForComparison = _normalizer.CleanForComparison(messageText);
                var messageHash = _normalizer.ComputeMessageHash(conversationId, cleanedForComparison);

                if (prevByConversation.TryGetValue(conversationId, out var prev))
                {
                    if (prev.hash == messageHash &&
                        Math.Abs((timestamp.Value - prev.timestamp).TotalSeconds) <= 5)
                    {
                        duplicateCount++;
                        continue; // Skip duplicate
                    }
                }

                prevByConversation[conversationId] = (messageHash, timestamp.Value);

                cleanedBatch.Add(new CleanedMessage
                {
                    ConversationId = conversationId,
                    BusinessPhone = businessPhone,
                    Timestamp = timestamp.Value,
                    MessageText = messageText,
                    SenderType = senderType,
                    AgentName = agentName,
                    MessageHash = messageHash
                });
            }

            // Batch insert
            if (cleanedBatch.Count > 0)
            {
                var inserted = await _repo.BatchInsertMessagesAsync(analysisId, tenantId, cleanedBatch, ct);
                insertedTotal += inserted;
            }

            // Report progress
            var percent = totalLines > 0 ? (int)((processedRows * 100.0) / totalLines) : 0;
            await onProgress(new StageProgress
            {
                Stage = "cleaning",
                StageNumber = 1,
                TotalStages = 3,
                Percent = Math.Min(percent, 100),
                Message = $"Processed {processedRows:N0}/{totalLines:N0} rows, {insertedTotal:N0} inserted, {duplicateCount:N0} duplicates"
            });
        }

        _logger.SystemInfo($"[CleanerService] Stage 1 complete: {insertedTotal:N0} inserted, {duplicateCount:N0} duplicates, {invalidCount:N0} invalid");
        return insertedTotal;
    }
}
