using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Integration;

/// <summary>
/// Async callback payload that InvektoServis sends back to Main App.
/// GR-1.9: After processing a webhook event, results are sent via POST callback.
/// </summary>
public sealed class OutgoingCallback
{
    /// <summary>Correlation ID - matches the X-Request-Id from the original webhook</summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>Action type that Main App should execute</summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>Tenant ID (int) - same as JWT tenant_id</summary>
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    /// <summary>Chat/conversation ID this callback relates to</summary>
    [JsonPropertyName("chat_id")]
    public int ChatId { get; init; }

    /// <summary>Sequence ID from the original webhook event (ordering)</summary>
    [JsonPropertyName("sequence_id")]
    public long SequenceId { get; init; }

    /// <summary>Action-specific payload</summary>
    [JsonPropertyName("data")]
    public CallbackData? Data { get; init; }

    /// <summary>Processing duration in milliseconds</summary>
    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; init; }

    /// <summary>When InvektoServis generated this callback</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Action-specific callback data. Fields are nullable, used based on action type.
/// </summary>
public sealed class CallbackData
{
    /// <summary>Message text to send (for send_message action)</summary>
    [JsonPropertyName("message_text")]
    public string? MessageText { get; init; }

    /// <summary>Suggested reply for agent (for suggest_reply action)</summary>
    [JsonPropertyName("suggested_reply")]
    public string? SuggestedReply { get; init; }

    /// <summary>Tag/label to apply (for apply_tag action)</summary>
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    /// <summary>Whether to hand off to human agent</summary>
    [JsonPropertyName("handoff_to_human")]
    public bool? HandoffToHuman { get; init; }

    /// <summary>AI confidence score (0.0 - 1.0)</summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    /// <summary>AI-generated summary for agent context</summary>
    [JsonPropertyName("ai_summary")]
    public string? AiSummary { get; init; }

    /// <summary>Detected intent (e.g., "price_inquiry", "appointment")</summary>
    [JsonPropertyName("intent")]
    public string? Intent { get; init; }
}

/// <summary>
/// Callback action types that InvektoServis can request from Main App.
/// </summary>
public static class CallbackActions
{
    /// <summary>Send a message to customer (auto-reply)</summary>
    public const string SendMessage = "send_message";

    /// <summary>Suggest a reply to the agent (agent assist)</summary>
    public const string SuggestReply = "suggest_reply";

    /// <summary>Apply a tag/label to conversation</summary>
    public const string ApplyTag = "apply_tag";

    /// <summary>Hand off conversation to human agent</summary>
    public const string HandoffToHuman = "handoff_to_human";

    /// <summary>No action needed (informational only)</summary>
    public const string NoAction = "no_action";
}
