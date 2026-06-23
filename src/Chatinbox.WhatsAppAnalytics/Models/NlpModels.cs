using System.Text.Json.Serialization;

namespace Chatinbox.WhatsAppAnalytics.Models;

// ============================================================
// Stage 4: Intent Classification
// ============================================================

public sealed record IntentResult
{
    public required string ConversationId { get; init; }
    public required string MessageText { get; init; }
    public required string Intent { get; init; }
    public required float Confidence { get; init; }
    public required string Method { get; init; } // keyword, claude, claude_low_conf, skipped, unknown
}

/// <summary>Claude intent response item: {"i": 0, "intent": "greeting", "conf": 0.95}</summary>
public sealed class ClaudeIntentItem
{
    [JsonPropertyName("i")]
    public int Index { get; set; }

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("conf")]
    public float Confidence { get; set; }
}

// ============================================================
// Stage 5: FAQ Extraction
// ============================================================

public sealed class FaqPair
{
    public required string ConversationId { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public int QuestionLen => Question.Length;
    public int AnswerLen => Answer.Length;
    public int? ClusterId { get; set; }
}

public sealed class FaqCluster
{
    public required int ClusterLabel { get; init; }
    public required string RepresentativeQuestion { get; init; }
    public required int QuestionCount { get; init; }
    public required List<string> SampleQuestions { get; init; }
    public required List<string> SampleAnswers { get; init; }
}

// ============================================================
// Stage 6: Sentiment Analysis
// ============================================================

public sealed record SentimentResult
{
    public required string ConversationId { get; init; }
    public required string Sentiment { get; init; } // positive, neutral, negative
    public required float Score { get; init; }       // -1.0 to 1.0
    public required string Method { get; init; }     // keyword, claude, empty, skipped
}

/// <summary>Claude sentiment response item: {"i": 0, "s": "positive", "score": 0.8}</summary>
public sealed class ClaudeSentimentItem
{
    [JsonPropertyName("i")]
    public int Index { get; set; }

    [JsonPropertyName("s")]
    public string Sentiment { get; set; } = "neutral";

    [JsonPropertyName("score")]
    public float Score { get; set; }
}

// ============================================================
// Stage 7: Product Analysis
// ============================================================

public sealed class ProductResult
{
    public required string ConversationId { get; init; }
    public required string ProductCodes { get; init; }    // pipe-separated
    public required int ProductCount { get; init; }
    public required string PricesMentioned { get; init; } // pipe-separated
    public required int PriceCount { get; init; }
    public required string Outcome { get; init; }
    public required string PrimaryAgent { get; init; }
}

public sealed class PriceEntry
{
    public required decimal Price { get; init; }
    public required int MentionCount { get; init; }
    public required string LikelyTl { get; init; } // "yes" or "heuristic"
}
