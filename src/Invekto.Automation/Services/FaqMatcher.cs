using Invekto.Automation.Data;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Matches user messages against tenant's FAQ entries using keyword search.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class FaqMatcher
{
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    public FaqMatcher(AutomationRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Search tenant's FAQs for a match against the user message.
    /// Returns the best matching FAQ entry or null if no match.
    /// </summary>
    public async Task<FaqMatchResult?> FindMatchAsync(int tenantId, string userMessage, CancellationToken ct = default)
    {
        var faqs = await _repo.GetActiveFaqsAsync(tenantId, ct);
        if (faqs.Count == 0)
            return null;

        var normalizedInput = Normalize(userMessage);
        var inputWords = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        FaqEntry? bestMatch = null;
        int bestScore = 0;

        foreach (var faq in faqs)
        {
            var score = CalculateMatchScore(inputWords, normalizedInput, faq);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = faq;
            }
        }

        if (bestMatch == null || bestScore == 0)
            return null;

        // Confidence: normalize score to 0-1 range
        // Max possible score = keyword count * 10 (exact match) + 5 (question substring)
        var maxPossible = (bestMatch.Keywords.Length * 10) + 5;
        var confidence = Math.Min(1.0, (double)bestScore / Math.Max(maxPossible, 1));

        return new FaqMatchResult
        {
            FaqId = bestMatch.Id,
            Answer = bestMatch.Answer,
            MatchedQuestion = bestMatch.Question,
            Confidence = confidence
        };
    }

    private static int CalculateMatchScore(string[] inputWords, string normalizedInput, FaqEntry faq)
    {
        var score = 0;

        // Keyword matching (highest weight)
        foreach (var keyword in faq.Keywords)
        {
            var normalizedKeyword = Normalize(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
                continue;

            // Exact word match
            if (inputWords.Contains(normalizedKeyword))
            {
                score += 10;
            }
            // Substring/partial match
            else if (normalizedInput.Contains(normalizedKeyword))
            {
                score += 5;
            }
        }

        // Question text similarity (lower weight)
        var normalizedQuestion = Normalize(faq.Question);
        var questionWords = normalizedQuestion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commonWords = inputWords.Intersect(questionWords).Count();
        if (commonWords >= 2)
            score += commonWords * 2;

        return score;
    }

    private static string Normalize(string text)
    {
        return text.ToLowerInvariant()
            .Replace("?", "")
            .Replace("!", "")
            .Replace(".", "")
            .Replace(",", "")
            .Trim();
    }
}

public sealed class FaqMatchResult
{
    public int FaqId { get; init; }
    public required string Answer { get; init; }
    public required string MatchedQuestion { get; init; }
    public double Confidence { get; init; }
}
