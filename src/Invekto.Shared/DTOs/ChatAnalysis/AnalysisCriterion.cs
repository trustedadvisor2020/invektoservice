using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// Single analysis criterion with summary and details
/// </summary>
public sealed class AnalysisCriterion
{
    /// <summary>
    /// 1-2 word summary
    /// </summary>
    [JsonPropertyName("Summary")]
    public required string Summary { get; init; }

    /// <summary>
    /// Detailed explanation (at least 2 sentences)
    /// </summary>
    [JsonPropertyName("Details")]
    public required string Details { get; init; }

    public static AnalysisCriterion Empty => new()
    {
        Summary = "-",
        Details = "Analiz yapılamadı."
    };
}

/// <summary>
/// Purchase probability with color coding
/// </summary>
public sealed class PurchaseProbabilityCriterion
{
    /// <summary>
    /// 1-2 word summary
    /// </summary>
    [JsonPropertyName("Summary")]
    public required string Summary { get; init; }

    /// <summary>
    /// Detailed explanation
    /// </summary>
    [JsonPropertyName("Details")]
    public required string Details { get; init; }

    /// <summary>
    /// Percentage (0-100)
    /// </summary>
    [JsonPropertyName("Percentage")]
    public required int Percentage { get; init; }

    /// <summary>
    /// Color code: "red" (0-50%) or "green" (51-100%)
    /// </summary>
    [JsonPropertyName("Color")]
    public required string Color { get; init; }

    public static PurchaseProbabilityCriterion Empty => new()
    {
        Summary = "-",
        Details = "Analiz yapılamadı.",
        Percentage = 0,
        Color = "red"
    };
}
