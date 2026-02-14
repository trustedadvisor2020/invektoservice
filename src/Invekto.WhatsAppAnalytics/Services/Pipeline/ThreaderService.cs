using System.Text.RegularExpressions;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;

namespace Invekto.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 2: Group messages by conversation_id -> detect outcomes -> insert wa_conversations.
/// C# port of Python 02_threader.py.
/// Streams messages from DB to avoid loading entire dataset into RAM.
/// </summary>
public sealed class ThreaderService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    // Product code pattern: 4-digit numbers
    private static readonly Regex ProductCodeRegex = new(@"\b(\d{4})\b", RegexOptions.Compiled);

    // ============================================================
    // Outcome detection regex patterns (priority order)
    // All patterns use ASCII-only chars; input is transliterated via TextNormalizer.TransliterateTurkish()
    // ============================================================

    // CONFIRMED_SALE: agent messages only
    private static readonly Regex[] SalePatterns = new[]
    {
        new Regex(@"siparisiniz.*olusturulmustur", RegexOptions.Compiled),
        new Regex(@"siparisiniz.*onaylandi", RegexOptions.Compiled),
        new Regex(@"siparisiniz.*hazirlaniyor", RegexOptions.Compiled),
        new Regex(@"guzel gunlerde giymenizi", RegexOptions.Compiled),
        new Regex(@"mutlu gunlerde giymenizi", RegexOptions.Compiled),
        new Regex(@"kargo.*takip\s*(no|numar)", RegexOptions.Compiled),
        new Regex(@"aras.*kargo.*takip", RegexOptions.Compiled),
        new Regex(@"yurtici.*kargo.*takip", RegexOptions.Compiled),
        new Regex(@"mng.*kargo.*takip", RegexOptions.Compiled),
        new Regex(@"havale.*yapildi", RegexOptions.Compiled),
        new Regex(@"eft.*yapildi", RegexOptions.Compiled),
        new Regex(@"odeme.*alindi", RegexOptions.Compiled),
        new Regex(@"odemeniz.*onaylandi", RegexOptions.Compiled),
    };

    // OFFERED: agent messages
    private static readonly Regex[] OfferedPatterns = new[]
    {
        new Regex(@"siparisinizi.*olusturalim", RegexOptions.Compiled),
        new Regex(@"siparis.*olusturayim", RegexOptions.Compiled),
        new Regex(@"siparis.*verelim", RegexOptions.Compiled),
        new Regex(@"\bkargo\b", RegexOptions.Compiled),
    };

    // RETURN: any message
    private static readonly Regex[] ReturnPatterns = new[]
    {
        new Regex(@"iade.*taleb", RegexOptions.Compiled),
        new Regex(@"degisim.*taleb", RegexOptions.Compiled),
        new Regex(@"geri.*gonder", RegexOptions.Compiled),
        new Regex(@"kargo.*iade", RegexOptions.Compiled),
    };

    // COMPLAINT: any message
    private static readonly Regex[] ComplaintPatterns = new[]
    {
        new Regex(@"memnun\s*degil", RegexOptions.Compiled),
        new Regex(@"yanlis.*geldi", RegexOptions.Compiled),
        new Regex(@"bozuk.*geldi", RegexOptions.Compiled),
        new Regex(@"berbat|rezalet", RegexOptions.Compiled),
    };

    public ThreaderService(AnalyticsRepository repo, AnalyticsConnectionFactory db, TextNormalizer normalizer, JsonLinesLogger logger)
    {
        _repo = repo;
        _db = db;
        _normalizer = normalizer;
        _logger = logger;
    }

    /// <summary>
    /// Run Stage 2: stream messages from DB, group by conversation, detect outcomes, insert conversations.
    /// Streams to avoid loading all messages into RAM at once.
    /// Returns total conversation count.
    /// </summary>
    public async Task<int> RunAsync(int analysisId, int tenantId,
        Func<StageProgress, Task> onProgress, CancellationToken ct)
    {
        _logger.SystemInfo($"[ThreaderService] Starting Stage 2 for analysis {analysisId}");

        var batchSize = 500;
        var batch = new List<Conversation>(batchSize);
        var totalConversations = 0;
        var totalMessages = 0;

        await foreach (var conv in StreamConversationsAsync(analysisId, tenantId, ct))
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(conv);
            totalMessages += conv.MessageCount;

            if (batch.Count >= batchSize)
            {
                await _repo.BatchInsertConversationsAsync(analysisId, tenantId, batch, ct);
                totalConversations += batch.Count;
                batch.Clear();

                await onProgress(new StageProgress
                {
                    Stage = "threading",
                    StageNumber = 2,
                    TotalStages = 3,
                    Percent = 50, // Cannot know total upfront in streaming mode
                    Message = $"Threaded {totalConversations:N0} conversations so far..."
                });
            }
        }

        // Flush remaining batch
        if (batch.Count > 0)
        {
            await _repo.BatchInsertConversationsAsync(analysisId, tenantId, batch, ct);
            totalConversations += batch.Count;
        }

        // Update totals on the analysis record
        await _repo.UpdateAnalysisTotalsAsync(analysisId, totalMessages, totalConversations, ct);

        await onProgress(new StageProgress
        {
            Stage = "threading",
            StageNumber = 2,
            TotalStages = 3,
            Percent = 100,
            Message = $"Threaded {totalConversations:N0} conversations, {totalMessages:N0} messages"
        });

        _logger.SystemInfo($"[ThreaderService] Stage 2 complete: {totalConversations:N0} conversations, {totalMessages:N0} messages");
        return totalConversations;
    }

    /// <summary>
    /// Stream messages from DB ordered by (conversation_id, timestamp) and yield one Conversation
    /// at a time as conversation boundaries are detected. Only one conversation's messages in RAM.
    /// </summary>
    private async IAsyncEnumerable<Conversation> StreamConversationsAsync(
        int analysisId, int tenantId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, message_text, sender_type, agent_name, timestamp
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid
            ORDER BY conversation_id, timestamp";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        string? currentConvId = null;
        var messages = new List<(string text, string senderType, string agentName, DateTime timestamp)>();

        while (await reader.ReadAsync(ct))
        {
            var convId = reader.GetString(0);
            var text = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var senderType = reader.GetString(2);
            var agentName = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var timestamp = reader.GetDateTime(4);

            if (currentConvId != null && convId != currentConvId)
            {
                // Conversation boundary: emit previous conversation
                yield return BuildConversation(currentConvId, messages);
                messages.Clear();
            }

            currentConvId = convId;
            messages.Add((text, senderType, agentName, timestamp));
        }

        // Emit last conversation
        if (currentConvId != null && messages.Count > 0)
            yield return BuildConversation(currentConvId, messages);
    }

    /// <summary>
    /// Build a Conversation from sorted messages with outcome detection.
    /// </summary>
    private Conversation BuildConversation(string convId, List<(string text, string senderType, string agentName, DateTime timestamp)> messages)
    {
        var customerMessages = messages.Where(m => m.senderType == "CUSTOMER").ToList();
        var agentMessages = messages.Where(m => m.senderType == "ME").ToList();

        // Primary agent: agent with most messages
        var primaryAgent = agentMessages
            .Where(m => !string.IsNullOrEmpty(m.agentName))
            .GroupBy(m => m.agentName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "";

        // First response time: first customer msg -> first agent response
        double firstResponseMinutes = 0;
        if (customerMessages.Count > 0 && agentMessages.Count > 0)
        {
            var firstCustomerTime = customerMessages[0].timestamp;
            var firstAgentAfter = agentMessages.FirstOrDefault(a => a.timestamp > firstCustomerTime);
            if (firstAgentAfter.timestamp > DateTime.MinValue)
            {
                firstResponseMinutes = (firstAgentAfter.timestamp - firstCustomerTime).TotalMinutes;
            }
        }

        // Product codes: extract from all messages
        var allText = string.Join(" ", messages.Select(m => m.text));
        var productCodes = ProductCodeRegex.Matches(allText)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        // Outcome detection (priority order)
        var outcome = DetectOutcome(messages);

        // Business phone: from first message (if available, stored elsewhere)
        var startTime = messages[0].timestamp;
        var endTime = messages[^1].timestamp;

        return new Conversation
        {
            ConversationId = convId,
            BusinessPhone = "", // Set from context if needed
            StartTime = startTime,
            EndTime = endTime,
            DurationMinutes = (int)(endTime - startTime).TotalMinutes,
            MessageCount = messages.Count,
            CustomerMessageCount = customerMessages.Count,
            AgentMessageCount = agentMessages.Count,
            PrimaryAgent = primaryAgent,
            FirstResponseMinutes = firstResponseMinutes,
            Outcome = outcome,
            ProductCodes = string.Join("|", productCodes),
            FirstCustomerMsg = customerMessages.Count > 0 ? Truncate(customerMessages[0].text, 500) : "",
            LastAgentMsg = agentMessages.Count > 0 ? Truncate(agentMessages[^1].text, 500) : ""
        };
    }

    /// <summary>
    /// Detect conversation outcome based on regex patterns (priority order).
    /// Text is transliterated to ASCII via TextNormalizer to avoid encoding issues.
    /// </summary>
    private string DetectOutcome(List<(string text, string senderType, string agentName, DateTime timestamp)> messages)
    {
        // Transliterate all texts once for robust matching (Turkish -> ASCII)
        var agentTransliterated = messages
            .Where(m => m.senderType == "ME")
            .Select(m => _normalizer.TransliterateTurkish(m.text))
            .ToList();
        var allTransliterated = messages
            .Select(m => _normalizer.TransliterateTurkish(m.text))
            .ToList();

        // Priority 1: CONFIRMED_SALE (agent messages only)
        foreach (var text in agentTransliterated)
        {
            if (SalePatterns.Any(p => p.IsMatch(text)))
                return "sale";
        }

        // Priority 2: OFFERED (agent messages)
        foreach (var text in agentTransliterated)
        {
            if (OfferedPatterns.Any(p => p.IsMatch(text)))
                return "offered";
        }

        // Priority 3: RETURN (any message)
        foreach (var text in allTransliterated)
        {
            if (ReturnPatterns.Any(p => p.IsMatch(text)))
                return "return";
        }

        // Priority 4: COMPLAINT (any message)
        foreach (var text in allTransliterated)
        {
            if (ComplaintPatterns.Any(p => p.IsMatch(text)))
                return "complaint";
        }

        // Priority 5: ABANDONED (<=2 messages)
        if (messages.Count <= 2)
            return "abandoned";

        // Priority 6: NO_RESPONSE (last message is from customer)
        if (messages[^1].senderType == "CUSTOMER")
            return "no_response";

        // Default: NO_SALE
        return "no_sale";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
