namespace Chatinbox.Automation.Services;

/// <summary>
/// Mock sentiment analyzer for simulation — keyword-based matching.
/// No Claude API call. Deterministic, instant response.
/// Register as singleton.
/// </summary>
public sealed class MockSentimentAnalyzer
{
    private static readonly string[] PositiveKeywords =
    {
        "tesekkur", "memnun", "harika", "mukemmel", "guzel", "iyi", "super",
        "sevdim", "basarili", "mutlu", "tatmin", "tavsiye", "begendim"
    };

    private static readonly string[] NegativeKeywords =
    {
        "sikayet", "kotu", "berbat", "sorun", "problem", "hata", "gecikme",
        "memnun degil", "calismadi", "iptal", "iade", "rezalet", "korku"
    };

    /// <summary>
    /// Detect sentiment from user input via keyword matching.
    /// Returns result (always non-null — defaults to neutral).
    /// </summary>
    public MockSentimentResult Detect(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return new MockSentimentResult { Sentiment = "neutral", Score = 0.5, MatchedKeyword = "" };

        var input = userInput.Trim().ToLowerInvariant();

        foreach (var keyword in NegativeKeywords)
        {
            if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return new MockSentimentResult
                {
                    Sentiment = "negative",
                    Score = 0.2,
                    MatchedKeyword = keyword
                };
            }
        }

        foreach (var keyword in PositiveKeywords)
        {
            if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return new MockSentimentResult
                {
                    Sentiment = "positive",
                    Score = 0.85,
                    MatchedKeyword = keyword
                };
            }
        }

        return new MockSentimentResult { Sentiment = "neutral", Score = 0.5, MatchedKeyword = "" };
    }
}

public sealed class MockSentimentResult
{
    public required string Sentiment { get; init; } // "positive", "negative", "neutral"
    public required double Score { get; init; }
    public required string MatchedKeyword { get; init; }
}
