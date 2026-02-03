using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// Request DTO for chat analysis endpoint (V2)
/// Async processing - returns immediately, sends callback when done
/// </summary>
public sealed class ChatAnalysisRequest
{
    /// <summary>
    /// Chat ID - echoed back in response
    /// </summary>
    [JsonPropertyName("ChatID")]
    public required int ChatID { get; init; }

    /// <summary>
    /// Instance ID - echoed back in response
    /// </summary>
    [JsonPropertyName("InstanceID")]
    public required int InstanceID { get; init; }

    /// <summary>
    /// User ID - echoed back in response
    /// </summary>
    [JsonPropertyName("UserID")]
    public required int UserID { get; init; }

    /// <summary>
    /// Request ID - echoed back in response
    /// </summary>
    [JsonPropertyName("RequestID")]
    public required string RequestID { get; init; }

    /// <summary>
    /// Callback URL - full URL where result will be POSTed
    /// </summary>
    [JsonPropertyName("ChatServerURL")]
    public required string ChatServerURL { get; init; }

    /// <summary>
    /// List of messages (alternative to MessageList)
    /// </summary>
    [JsonPropertyName("MessageListObject")]
    public List<MessageItem>? MessageListObject { get; init; }

    /// <summary>
    /// Raw message list string (legacy)
    /// </summary>
    [JsonPropertyName("MessageList")]
    public string? MessageList { get; init; }

    /// <summary>
    /// Allowed labels (comma-separated)
    /// </summary>
    [JsonPropertyName("LabelSearchText")]
    public string? LabelSearchText { get; init; }

    /// <summary>
    /// Label ID list (legacy)
    /// </summary>
    [JsonPropertyName("LabelIDList")]
    public string? LabelIDList { get; init; }

    /// <summary>
    /// Language for analysis output (null = auto-detect, response always Turkish)
    /// </summary>
    [JsonPropertyName("Lang")]
    public string? Lang { get; init; }
}

/// <summary>
/// Single message in a conversation
/// </summary>
public sealed class MessageItem
{
    /// <summary>
    /// Message source: AGENT or CUSTOMER
    /// </summary>
    [JsonPropertyName("Source")]
    public required string Source { get; init; }

    /// <summary>
    /// Message content
    /// </summary>
    [JsonPropertyName("Message")]
    public required string Message { get; init; }
}
