namespace Invekto.Automation.Services;

/// <summary>
/// Mock FAQ matcher for simulation — hardcoded keyword match.
/// No DB access, no Claude API. Deterministic, instant response.
/// Phase 4 replaces with real FaqMatcher (DB-backed, keyword+semantic search).
/// Register as singleton.
/// </summary>
public sealed class MockFaqMatcher
{
    private static readonly List<MockFaqEntry> _entries = new()
    {
        new("fiyat", "Fiyat bilgisi icin satis ekibimizle iletisime gecebilirsiniz.", 0.9),
        new("kargo", "Kargo suresi 2-3 is gunu icerisindedir.", 0.85),
        new("iade", "Iade islemleri 14 gun icerisinde yapilabilir.", 0.85),
        new("iletisim", "Bize info@example.com adresinden ulasabilirsiniz.", 0.8),
        new("calisma saatleri", "Hafta ici 09:00-18:00 arasi hizmet vermekteyiz.", 0.8),
        new("odeme", "Kredi karti, havale ve kapida odeme secenekleri mevcuttur.", 0.85),
    };

    /// <summary>
    /// Match user input against hardcoded FAQ entries via keyword containment.
    /// Returns best match with confidence, or null if no match.
    /// </summary>
    public MockFaqResult? Match(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return null;

        var input = userInput.Trim().ToLowerInvariant();

        MockFaqEntry? bestMatch = null;
        foreach (var entry in _entries)
        {
            if (input.Contains(entry.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                if (bestMatch == null || entry.Confidence > bestMatch.Confidence)
                    bestMatch = entry;
            }
        }

        if (bestMatch == null)
            return null;

        return new MockFaqResult
        {
            Answer = bestMatch.Answer,
            Confidence = bestMatch.Confidence,
            MatchedKeyword = bestMatch.Keyword
        };
    }

    private sealed record MockFaqEntry(string Keyword, string Answer, double Confidence);
}

public sealed class MockFaqResult
{
    public required string Answer { get; init; }
    public required double Confidence { get; init; }
    public required string MatchedKeyword { get; init; }
}
