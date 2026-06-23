namespace Chatinbox.Automation.Services;

/// <summary>
/// Mock intent detector for simulation — rule-based keyword matching.
/// No Claude API call. Deterministic, instant response.
/// Supports custom intents with automatic synonym expansion.
/// Register as singleton.
/// Uses Turkish-aware comparison for ş/ç/ğ/ı/ö/ü characters.
/// </summary>
public sealed class MockIntentDetector
{
    private static readonly System.Globalization.CultureInfo TrCulture =
        System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    private static readonly List<IntentRule> _rules = new()
    {
        new("greeting", new[] { "merhaba", "selam", "hey", "iyi günler", "hello", "hi" }, 0.9),
        new("farewell", new[] { "görüşürüz", "bye", "hoşça kal", "iyi akşamlar", "iyi geceler" }, 0.85),
        new("complaint", new[] { "şikayet", "şikayette", "memnun değil", "sorun", "problem", "kötü" }, 0.8),
        new("purchase", new[] { "satın al", "sipariş", "almak istiyorum", "fiyat" }, 0.75),
        new("support", new[] { "yardım", "destek", "nasıl yapılır", "çalışmıyor", "hata" }, 0.8),
    };

    /// <summary>
    /// Synonyms for common Turkish sector/domain keywords.
    /// Used to expand custom intent names into matchable keywords.
    /// Keys include both Turkish and ASCII forms for flexible matching.
    /// </summary>
    private static readonly Dictionary<string, string[]> _synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["restoran"] = new[] { "yemek", "cafe", "kafe", "lokanta", "pizza", "kebap", "menü", "mutfak", "garson", "masa" },
        ["klinik"] = new[] { "diş", "dişçi", "doktor", "hastane", "hekim", "tedavi", "hasta", "muayene", "ameliyat", "implant", "ortodonti", "dolgu", "kanal", "protez", "çekim" },
        ["sağlık"] = new[] { "diş", "doktor", "hastane", "hekim", "tedavi", "hasta", "klinik", "muayene", "ilaç", "reçete" },
        ["saglik"] = new[] { "diş", "doktor", "hastane", "hekim", "tedavi", "hasta", "klinik", "muayene", "ilaç", "reçete" },
        ["eticaret"] = new[] { "mağaza", "online", "shop", "ürün", "sepet", "alışveriş" },
        ["hizmet"] = new[] { "servis", "temizlik", "tamir", "bakım", "danışmanlık", "ajans", "kurye" },
        ["bilgi"] = new[] { "öğrenmek", "merak", "sormak", "nedir", "nasıl" },
        ["diş"] = new[] { "ortodonti", "implant", "dolgu", "kanal", "protez", "çekim", "diş teli", "beyazlatma", "kaplama", "dişçi", "tedavi" },
        ["dis"] = new[] { "ortodonti", "implant", "dolgu", "kanal", "protez", "çekim", "diş teli", "beyazlatma", "kaplama", "dişçi", "tedavi" },
        ["randevu"] = new[] { "randevu", "rezervasyon", "saat", "gün", "tarih", "müsait" },
        ["güzellik"] = new[] { "cilt", "bakım", "estetik", "botoks", "dolgu", "lazer", "epilasyon", "peeling" },
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

        var input = TrLower(userInput.Trim());

        MockIntentResult? bestMatch = null;

        // Built-in rules
        foreach (var rule in _rules)
        {
            foreach (var keyword in rule.Keywords)
            {
                if (TrContains(input, keyword))
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
                    if (TrContains(input, keyword))
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
            var lower = TrLower(part);
            keywords.Add(lower);

            if (_synonyms.TryGetValue(lower, out var syns))
                foreach (var syn in syns)
                    keywords.Add(syn);
        }

        return keywords;
    }

    private static string TrLower(string s) => s.ToLower(TrCulture);

    private static bool TrContains(string haystack, string needle) =>
        TrCulture.CompareInfo.IndexOf(haystack, needle, System.Globalization.CompareOptions.IgnoreCase) >= 0;

    private sealed record IntentRule(string Intent, string[] Keywords, double Confidence);
}

public sealed class MockIntentResult
{
    public required string Intent { get; init; }
    public required double Confidence { get; init; }
    public required string MatchedKeyword { get; init; }
}
