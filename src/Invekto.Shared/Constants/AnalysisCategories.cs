namespace Invekto.Shared.Constants;

/// <summary>
/// Chat analysis categories and sentiments
/// </summary>
public static class AnalysisCategories
{
    // Sentiment values
    public const string SentimentPositive = "positive";
    public const string SentimentNegative = "negative";
    public const string SentimentNeutral = "neutral";

    // Category values (Turkish)
    public const string CategoryDestek = "Destek";
    public const string CategorySatis = "Satis";
    public const string CategorySikayet = "Sikayet";
    public const string CategoryBilgi = "Bilgi";

    /// <summary>
    /// Valid sentiment values
    /// </summary>
    public static readonly string[] ValidSentiments = [SentimentPositive, SentimentNegative, SentimentNeutral];

    /// <summary>
    /// Valid category values
    /// </summary>
    public static readonly string[] ValidCategories = [CategoryDestek, CategorySatis, CategorySikayet, CategoryBilgi];
}
