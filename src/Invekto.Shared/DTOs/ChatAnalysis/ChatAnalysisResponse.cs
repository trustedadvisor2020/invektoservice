using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// Immediate response when request is accepted (async processing)
/// </summary>
public sealed class ChatAnalysisAcceptedResponse
{
    [JsonPropertyName("RequestID")]
    public required string RequestID { get; init; }

    [JsonPropertyName("Status")]
    public required string Status { get; init; }

    public static ChatAnalysisAcceptedResponse Processing(string requestId) => new()
    {
        RequestID = requestId,
        Status = "Processing"
    };
}

/// <summary>
/// Full analysis response sent to callback URL
/// Contains all 15 analysis criteria
/// </summary>
public sealed class ChatAnalysisCallbackResponse
{
    // Echo fields
    [JsonPropertyName("ChatID")]
    public required int ChatID { get; init; }

    [JsonPropertyName("InstanceID")]
    public required int InstanceID { get; init; }

    [JsonPropertyName("UserID")]
    public required int UserID { get; init; }

    [JsonPropertyName("RequestID")]
    public required string RequestID { get; init; }

    // Labels
    [JsonPropertyName("SelectedLabels")]
    public required List<string> SelectedLabels { get; init; }

    [JsonPropertyName("SuggestedLabels")]
    public required List<string> SuggestedLabels { get; init; }

    // 15 Analysis Criteria
    [JsonPropertyName("Content")]
    public required AnalysisCriterion Content { get; init; }

    [JsonPropertyName("Attitude")]
    public required AnalysisCriterion Attitude { get; init; }

    [JsonPropertyName("ApproachRecommendation")]
    public required AnalysisCriterion ApproachRecommendation { get; init; }

    [JsonPropertyName("PurchaseProbability")]
    public required PurchaseProbabilityCriterion PurchaseProbability { get; init; }

    [JsonPropertyName("Needs")]
    public required AnalysisCriterion Needs { get; init; }

    [JsonPropertyName("DecisionProcess")]
    public required AnalysisCriterion DecisionProcess { get; init; }

    [JsonPropertyName("SalesBarriers")]
    public required AnalysisCriterion SalesBarriers { get; init; }

    [JsonPropertyName("CommunicationStyle")]
    public required AnalysisCriterion CommunicationStyle { get; init; }

    [JsonPropertyName("CustomerProfile")]
    public required AnalysisCriterion CustomerProfile { get; init; }

    [JsonPropertyName("SatisfactionAndFeedback")]
    public required AnalysisCriterion SatisfactionAndFeedback { get; init; }

    [JsonPropertyName("OfferAndConversionRate")]
    public required AnalysisCriterion OfferAndConversionRate { get; init; }

    [JsonPropertyName("SupportStrategy")]
    public required AnalysisCriterion SupportStrategy { get; init; }

    [JsonPropertyName("CompetitorAnalysis")]
    public required AnalysisCriterion CompetitorAnalysis { get; init; }

    [JsonPropertyName("BehaviorPatterns")]
    public required AnalysisCriterion BehaviorPatterns { get; init; }

    [JsonPropertyName("RepresentativeResponseSuggestion")]
    public required AnalysisCriterion RepresentativeResponseSuggestion { get; init; }

    // Metadata
    [JsonPropertyName("AnalyzedAt")]
    public required DateTime AnalyzedAt { get; init; }

    [JsonPropertyName("Error")]
    public string? Error { get; init; }
}

/// <summary>
/// Error response for callback when analysis fails
/// </summary>
public sealed class ChatAnalysisErrorResponse
{
    [JsonPropertyName("ChatID")]
    public required int ChatID { get; init; }

    [JsonPropertyName("InstanceID")]
    public required int InstanceID { get; init; }

    [JsonPropertyName("UserID")]
    public required int UserID { get; init; }

    [JsonPropertyName("RequestID")]
    public required string RequestID { get; init; }

    [JsonPropertyName("Error")]
    public required string Error { get; init; }

    [JsonPropertyName("AnalyzedAt")]
    public required DateTime AnalyzedAt { get; init; }
}
