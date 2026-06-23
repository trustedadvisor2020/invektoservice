using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.AgentAI;

public sealed class SuggestReplyResponse
{
    [JsonPropertyName("suggestion_id")]
    public string SuggestionId { get; set; } = "";

    [JsonPropertyName("suggested_reply")]
    public string SuggestedReply { get; set; } = "";

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("warning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Warning { get; set; }

    /// <summary>
    /// GR-2.2: Knowledge source references (agent-facing only).
    /// Null/empty when Knowledge service was not used or unavailable.
    /// </summary>
    [JsonPropertyName("sources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<KnowledgeSourceRef>? Sources { get; set; }

    /// <summary>
    /// GR-2.2: AI-generated follow-up question suggestion for the agent.
    /// </summary>
    [JsonPropertyName("suggested_followup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedFollowup { get; set; }

    /// <summary>
    /// GR-2.2: Whether Knowledge service was used for this suggestion.
    /// </summary>
    [JsonPropertyName("knowledge_available")]
    public bool KnowledgeAvailable { get; set; }

    /// <summary>
    /// GR-2.2: Conversation summary (only present when history was long enough to summarize).
    /// </summary>
    [JsonPropertyName("conversation_summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationSummary { get; set; }

    /// <summary>
    /// GR-2.3: Detected language of the customer's message (ISO 639-1).
    /// </summary>
    [JsonPropertyName("detected_language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DetectedLanguage { get; set; }
}

/// <summary>
/// GR-2.2: Reference to a Knowledge source used in reply generation.
/// </summary>
public sealed class KnowledgeSourceRef
{
    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Unified title: FAQ question or document title (contract: sources[].title).
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("faq_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FaqId { get; set; }

    [JsonPropertyName("document_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DocumentId { get; set; }

    [JsonPropertyName("page_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PageNumber { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}
