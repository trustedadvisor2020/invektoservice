namespace Chatinbox.Automation.Services;

/// <summary>
/// Scans message text for negative review risk signals using keyword matching.
/// PKT-12: Review Rescue AI — static keyword detection layer.
/// </summary>
public static class RiskKeywordScanner
{
    /// <summary>Risk signal keywords (Turkish, lowercase for case-insensitive match).</summary>
    private static readonly (string Keyword, string Category, int Weight)[] RiskKeywords =
    {
        // Critical: explicit review/complaint threat (max 25 pts)
        ("yorum yazacağım", "review_threat", 25),
        ("yorum yazacagim", "review_threat", 25),
        ("şikayet edeceğim", "complaint_threat", 22),
        ("sikayet edecegim", "complaint_threat", 22),
        ("tüketici hakem", "legal_threat", 25),
        ("tuketici hakem", "legal_threat", 25),
        ("trendyol'a yazacağım", "marketplace_threat", 22),
        ("hepsiburada'ya yazacağım", "marketplace_threat", 22),

        // High: legal/fraud language
        ("avukat", "legal_threat", 20),
        ("dava açacağım", "legal_threat", 22),
        ("dolandırıcı", "fraud_accusation", 22),
        ("dolandirici", "fraud_accusation", 22),
        ("aldatma", "fraud_accusation", 20),

        // Medium: strong negative sentiment
        ("rezalet", "extreme_negative", 18),
        ("berbat", "extreme_negative", 15),
        ("felaket", "extreme_negative", 15),
        ("iğrenç", "extreme_negative", 15),
        ("igrenc", "extreme_negative", 15),
        ("sahtekarlık", "fraud_accusation", 18),
        ("sahtekarlik", "fraud_accusation", 18),

        // Lower: complaint/return signals
        ("şikayet", "complaint", 12),
        ("sikayet", "complaint", 12),
        ("kalitesiz", "quality_complaint", 10),
        ("kötü kalite", "quality_complaint", 12),
        ("kotu kalite", "quality_complaint", 12),
        ("kötü", "negative_general", 8),
        ("kotu", "negative_general", 8),
        ("iade istiyorum", "return_demand", 15),
        ("iade", "return_request", 10),
        ("memnun değilim", "dissatisfaction", 12),
        ("memnun degilim", "dissatisfaction", 12),
        ("cevap vermiyorsunuz", "response_complaint", 14),
    };

    /// <summary>
    /// Scan message text for risk keywords. Returns matched keywords and total score (max 25).
    /// Score is capped at 25 (the keyword_score weight in the composite formula).
    /// </summary>
    public static RiskKeywordResult Scan(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return RiskKeywordResult.Empty;

        var normalizedText = messageText.ToLowerInvariant();
        var matchedKeywords = new List<string>();
        int totalWeight = 0;

        foreach (var (keyword, category, weight) in RiskKeywords)
        {
            if (normalizedText.Contains(keyword, StringComparison.Ordinal))
            {
                matchedKeywords.Add($"{keyword}({category})");
                totalWeight += weight;
            }
        }

        if (matchedKeywords.Count == 0)
            return RiskKeywordResult.Empty;

        return new RiskKeywordResult
        {
            HasMatch = true,
            MatchedKeywords = matchedKeywords.ToArray(),
            RawWeight = totalWeight,
            Score = Math.Min(totalWeight, 25) // cap at 25 for composite formula
        };
    }
}

public sealed class RiskKeywordResult
{
    public bool HasMatch { get; init; }
    public string[] MatchedKeywords { get; init; } = [];
    public int RawWeight { get; init; }
    public int Score { get; init; } // capped at 25

    public static RiskKeywordResult Empty => new() { HasMatch = false };
}
