namespace Invekto.Automation.Services;

/// <summary>
/// Mock intent detector for simulation — rule-based keyword matching.
/// No Claude API call. Deterministic, instant response.
/// Phase 4 replaces with real IntentDetector (Claude Haiku-based).
/// Register as singleton.
/// </summary>
public sealed class MockIntentDetector
{
    private static readonly List<IntentRule> _rules = new()
    {
        new("greeting", new[] { "merhaba", "selam", "hey", "iyi gunler", "hello", "hi" }, 0.9),
        new("farewell", new[] { "gorusuruz", "bye", "hosca kal", "iyi aksamlar", "iyi geceler" }, 0.85),
        new("complaint", new[] { "sikayet", "sikayette", "memnun degil", "sorun", "problem", "kotu" }, 0.8),
        new("purchase", new[] { "satin al", "siparis", "almak istiyorum", "fiyat" }, 0.75),
        new("support", new[] { "yardim", "destek", "nasil yapilir", "calismiyor", "hata" }, 0.8),
    };

    /// <summary>
    /// Detect intent from user input via keyword matching.
    /// Returns best matching intent with confidence, or null if no match.
    /// </summary>
    public MockIntentResult? Detect(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return null;

        var input = userInput.Trim().ToLowerInvariant();

        MockIntentResult? bestMatch = null;
        foreach (var rule in _rules)
        {
            foreach (var keyword in rule.Keywords)
            {
                if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    if (bestMatch == null || rule.Confidence > bestMatch.Confidence)
                    {
                        bestMatch = new MockIntentResult
                        {
                            Intent = rule.Intent,
                            Confidence = rule.Confidence,
                            MatchedKeyword = keyword
                        };
                    }
                    break; // One keyword match per rule is enough
                }
            }
        }

        return bestMatch;
    }

    private sealed record IntentRule(string Intent, string[] Keywords, double Confidence);
}

public sealed class MockIntentResult
{
    public required string Intent { get; init; }
    public required double Confidence { get; init; }
    public required string MatchedKeyword { get; init; }
}
