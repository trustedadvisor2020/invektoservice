namespace Invekto.Automation.Services;

/// <summary>
/// Mock intent detector for simulation — rule-based keyword matching.
/// No Claude API call. Deterministic, instant response.
/// Supports custom intents with automatic synonym expansion.
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
    /// Synonyms for common Turkish sector/domain keywords.
    /// Used to expand custom intent names into matchable keywords.
    /// </summary>
    private static readonly Dictionary<string, string[]> _synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["restoran"] = new[] { "yemek", "cafe", "kafe", "lokanta", "pizza", "kebap", "menu", "mutfak", "garson", "masa" },
        ["klinik"] = new[] { "dis", "doktor", "hastane", "hekim", "tedavi", "hasta", "muayene", "ameliyat", "implant", "ortodonti" },
        ["saglik"] = new[] { "dis", "doktor", "hastane", "hekim", "tedavi", "hasta", "klinik", "muayene", "ilac", "recete" },
        ["eticaret"] = new[] { "magaza", "online", "shop", "urun", "sepet", "alisveris" },
        ["hizmet"] = new[] { "servis", "temizlik", "tamir", "bakim", "danismanlik", "ajans", "kurye" },
        ["bilgi"] = new[] { "ogrenmek", "merak", "sormak", "nedir", "nasil" },
    };

    /// <summary>
    /// Detect intent from user input via keyword matching.
    /// Returns best matching intent with confidence, or null if no match.
    /// </summary>
    public MockIntentResult? Detect(string userInput) => Detect(userInput, null);

    /// <summary>
    /// Detect intent with custom intent support.
    /// First checks built-in rules, then expands custom intent names
    /// using synonym map to match user input.
    /// </summary>
    public MockIntentResult? Detect(string userInput, string[]? customIntents)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return null;

        var input = userInput.Trim().ToLowerInvariant();

        MockIntentResult? bestMatch = null;

        // Built-in rules
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
                    break;
                }
            }
        }

        // Custom intents with synonym expansion
        if (customIntents is { Length: > 0 })
        {
            foreach (var intent in customIntents)
            {
                var keywords = ExpandIntentKeywords(intent);
                foreach (var keyword in keywords)
                {
                    if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        const double customConfidence = 0.80;
                        if (bestMatch == null || customConfidence > bestMatch.Confidence)
                        {
                            bestMatch = new MockIntentResult
                            {
                                Intent = intent,
                                Confidence = customConfidence,
                                MatchedKeyword = keyword
                            };
                        }
                        break;
                    }
                }
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Expand an intent name into matchable keywords.
    /// Splits on '_' and adds synonyms from the built-in map.
    /// E.g. "klinik_saglik" → ["klinik", "saglik", "dis", "doktor", "hastane", ...]
    /// </summary>
    private static HashSet<string> ExpandIntentKeywords(string intent)
    {
        var parts = intent.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            keywords.Add(lower);

            if (_synonyms.TryGetValue(lower, out var syns))
                foreach (var syn in syns)
                    keywords.Add(syn);
        }

        return keywords;
    }

    private sealed record IntentRule(string Intent, string[] Keywords, double Confidence);
}

public sealed class MockIntentResult
{
    public required string Intent { get; init; }
    public required double Confidence { get; init; }
    public required string MatchedKeyword { get; init; }
}
