using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.AgentAI;
using Chatinbox.Shared.Logging;

namespace Chatinbox.AgentAI.Services;

/// <summary>
/// Builds structured escalation notes from conversation context for human agent handoff.
/// Extracts key points, intent, and sentiment from conversation history.
/// Never throws — all exceptions caught and logged.
/// PKT-6B1: GR-3.3 Agent Assist E-commerce.
/// </summary>
public sealed class EscalationNoteService
{
    private readonly JsonLinesLogger _logger;

    public EscalationNoteService(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate an escalation note from conversation messages.
    /// Returns null only on critical failure.
    /// </summary>
    public EscalationNoteResponse? GenerateNote(
        int tenantId, string? intent, string? sentiment,
        List<ConversationEntry>? messages)
    {
        try
        {
            var keyPoints = new List<string>();
            var effectiveIntent = intent ?? "unknown";
            var effectiveSentiment = sentiment ?? "neutral";

            if (messages != null && messages.Count > 0)
            {
                // Extract customer messages for key points
                foreach (var msg in messages.Where(m =>
                    string.Equals(m.Role, "customer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrWhiteSpace(msg.Text) && msg.Text.Length > 10)
                        keyPoints.Add(TruncateText(msg.Text, 150));
                }

                // Limit to last 5 key points
                if (keyPoints.Count > 5)
                    keyPoints = keyPoints.TakeLast(5).ToList();
            }

            var summary = BuildSummary(effectiveIntent, effectiveSentiment, keyPoints, messages?.Count ?? 0);

            _logger.StepInfo(
                $"Escalation note generated: tenant={tenantId}, intent={effectiveIntent}, points={keyPoints.Count}",
                "escalation-note");

            return new EscalationNoteResponse
            {
                Summary = summary,
                Intent = effectiveIntent,
                Sentiment = effectiveSentiment,
                KeyPoints = keyPoints
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AgentAIEscalationNoteFailed}] Escalation note operation error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (ArgumentException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AgentAIEscalationNoteFailed}] Escalation note argument error for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    private static string BuildSummary(
        string intent, string sentiment, List<string> keyPoints, int messageCount)
    {
        var sentimentLabel = sentiment switch
        {
            "negative" => "Müşteri memnuniyetsiz.",
            "positive" => "Müşteri olumlu yaklaşıyor.",
            _ => ""
        };

        var intentLabel = intent switch
        {
            "return_request" => "İade talebi var.",
            "complaint" => "Şikayet bildirimi.",
            "order_status" => "Sipariş durumu sorgulanıyor.",
            "price_inquiry" => "Fiyat bilgisi isteniyor.",
            "appointment" => "Randevu talebi.",
            _ => $"Konu: {intent}."
        };

        var parts = new List<string>();
        parts.Add($"[{messageCount} mesaj]");
        parts.Add(intentLabel);
        if (!string.IsNullOrEmpty(sentimentLabel))
            parts.Add(sentimentLabel);
        if (keyPoints.Count > 0)
            parts.Add($"Son mesaj: \"{keyPoints[^1]}\"");

        return string.Join(" ", parts);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }
}

/// <summary>DTO for conversation entry in escalation note request.</summary>
public sealed class ConversationEntry
{
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
}
