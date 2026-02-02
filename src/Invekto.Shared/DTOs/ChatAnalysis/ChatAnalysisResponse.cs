using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// Response DTO for chat analysis endpoint
/// Contract: arch/contracts/chat-analysis.json
/// </summary>
public sealed class ChatAnalysisResponse
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; init; }

    [JsonPropertyName("messageCount")]
    public required int MessageCount { get; init; }

    [JsonPropertyName("analysis")]
    public required AnalysisResult Analysis { get; init; }

    [JsonPropertyName("analyzedAt")]
    public required DateTime AnalyzedAt { get; init; }
}

/// <summary>
/// Analysis result from Claude
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>
    /// Overall sentiment: positive, negative, neutral
    /// </summary>
    [JsonPropertyName("sentiment")]
    public required string Sentiment { get; init; }

    /// <summary>
    /// Primary category: Destek, Satis, Sikayet, Bilgi
    /// </summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }

    /// <summary>
    /// Brief summary of the conversation (max 200 chars)
    /// </summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
}
