using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Benchmark;

/// <summary>
/// Sends a conversation thread to an LLM model for outcome classification.
/// Uses a single system prompt for all models (fair comparison).
/// </summary>
public sealed class OutcomeClassifierService
{
    private readonly JsonLinesLogger _logger;

    private const string SystemPrompt = @"You are a WhatsApp conversation outcome classifier for a business. Classify the conversation into exactly ONE of these 7 labels:

Labels:
- sale: Payment/deposit received OR order/appointment confirmed with payment
- appointment_booked: Appointment/consultation/surgery date confirmed (payment may be pending)
- offered: Price/info given, customer hasn't decided yet
- no_sale: Customer explicitly declined, chose competitor, or not suitable
- no_response: Agent sent messages but customer went silent (last message from agent)
- abandoned: Very short conversation (1-2 messages), no real interaction
- return_or_complaint: Post-service complaint, refund request, or return

Decision priority (use first matching rule):
1. If payment/deposit was received or confirmed -> sale
2. If a specific date/time was confirmed for appointment/visit -> appointment_booked
3. If price/offer was given but no decision yet -> offered
4. If customer said no, too expensive, or chose elsewhere -> no_sale
5. If last message is from agent and customer stopped replying -> no_response
6. If only 1-2 messages total with no real conversation -> abandoned
7. If complaint, refund, or return discussed -> return_or_complaint

Respond with ONLY a JSON object, no other text:
{""label"": ""sale"", ""confidence"": 0.92, ""evidence"": ""Brief explanation of why this label""}";

    public OutcomeClassifierService(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Format a thread's messages into LLM-consumable text.
    /// Format: [AGENT] message\n[CUSTOMER] message\n...
    /// Truncates to maxLength chars (first half + last half).
    /// </summary>
    public static string FormatThread(SampledThread thread, int maxLength = 4000)
    {
        var lines = thread.Messages.Select(m =>
        {
            var role = m.SenderType == "ME" ? "AGENT" : "CUSTOMER";
            return $"[{role}] {m.Text}";
        });

        var fullText = string.Join("\n", lines);

        if (fullText.Length <= maxLength) return fullText;

        // Truncate from middle: keep first half + last half
        var half = maxLength / 2;
        return fullText[..half] + "\n[...truncated...]\n" + fullText[^half..];
    }

    /// <summary>
    /// Classify a single thread using the given LLM client.
    /// Returns parsed classification or null on failure.
    /// </summary>
    public async Task<LlmClassification?> ClassifyAsync(ILlmClient client, SampledThread thread, CancellationToken ct)
    {
        var threadText = thread.MaskedText;
        if (string.IsNullOrWhiteSpace(threadText))
        {
            _logger.SystemWarn($"[OutcomeClassifier:{client.ModelName}] Empty thread text for {thread.ConversationId}");
            return null;
        }

        var responseText = await client.ClassifyAsync(SystemPrompt, threadText, ct);
        var parsed = LlmClassification.Parse(responseText);

        if (parsed == null && responseText != null)
        {
            _logger.SystemWarn($"[OutcomeClassifier:{client.ModelName}] Failed to parse response for {thread.ConversationId}: {responseText[..Math.Min(responseText.Length, 100)]}");
        }

        return parsed;
    }
}
