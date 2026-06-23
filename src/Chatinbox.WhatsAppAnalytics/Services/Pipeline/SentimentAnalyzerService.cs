using System.Text;
using System.Text.RegularExpressions;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 6: Per-conversation sentiment analysis.
/// Keyword-first + Claude Haiku hybrid. 3-level: positive/neutral/negative.
/// Score range: -1.0 (very negative) to 1.0 (very positive).
/// </summary>
public sealed class SentimentAnalyzerService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly ClaudeClient _claude;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    private const int ClaudeBatchSize = 100;
    private const int TotalStages = 7;
    private const int StageNumber = 6;

    // Positive keyword patterns (transliterated Turkish)
    private static readonly Regex PositiveRegex = Compile(
        @"\b(tesekkur|sagol|sagolun)\b|" +
        @"\b(harika|super|mukemmel|muhtesem)\b|" +
        @"\b(cok (guzel|iyi|begendim|tesekkur))\b|" +
        @"\b(bayildim|asik oldum)\b|" +
        @"\b(tam istedigim|tam aradigim)\b|" +
        @"\b(cok memnun|memnun kaldim)\b|" +
        @"\b(eline saglik|ellerinize saglik)\b|" +
        @"\b(kaliteli|sik|zarif)\b");

    // Negative keyword patterns (transliterated Turkish)
    private static readonly Regex NegativeRegex = Compile(
        @"\b(memnun degil|memnun kalmadim)\b|" +
        @"\b(kotu|berbat|rezalet|felaket)\b|" +
        @"\b(bozuk|defolu|yirtik|lekeli)\b|" +
        @"\b(yanlis geldi|eksik geldi)\b|" +
        @"\b(hayal kirikligi)\b|" +
        @"\b(uymadi|dar geldi|buyuk geldi|kucuk geldi|genis geldi)\b|" +
        @"\b(gec geldi|gec kaldi|gecikme)\b|" +
        @"\b(saygisizlik|ilgisiz|umursamaz)\b|" +
        @"\b(iade|geri gonder|degisim|iptal)\b");

    private static readonly string ClaudeSystemPrompt = @"Sen bir musteri sentiment analiz uzmanisin. Turkce giyim e-ticaret WhatsApp konusmalari.

Her konusmadaki MUSTERI mesajlarinin genel duygusal tonunu belirle.

Kurallar:
- positive: Memnuniyet, tesekkur, begeni, mutluluk
- neutral: Bilgi sorma, standart iletisim, duygusal ton yok
- negative: Sikayet, memnuniyetsizlik, hayal kirikligi, ofke

JSON formatinda cevap ver:
[{""i"": 0, ""s"": ""positive"", ""score"": 0.8}, ...]

s: sentiment (positive/neutral/negative)
score: -1.0 (cok olumsuz) ile 1.0 (cok olumlu) arasi

Sadece JSON array dondur.";

    public SentimentAnalyzerService(
        AnalyticsRepository repo,
        AnalyticsConnectionFactory db,
        ClaudeClient claude,
        TextNormalizer normalizer,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _db = db;
        _claude = claude;
        _normalizer = normalizer;
        _logger = logger;
    }

    /// <summary>
    /// Run sentiment analysis on all conversations for the given analysis.
    /// Aggregates customer messages per conversation, then classifies.
    /// Reads from wa_messages, writes to wa_sentiments.
    /// </summary>
    public async Task<int> RunAsync(
        int analysisId, int tenantId,
        Func<StageProgress, Task> onProgress,
        CancellationToken ct)
    {
        _logger.SystemInfo($"[SentimentAnalyzer] Starting stage 6 for analysis {analysisId}");

        // Get aggregated customer text per conversation
        var conversations = await _repo.GetCustomerTextPerConversationAsync(analysisId, tenantId, ct);
        _logger.SystemInfo($"[SentimentAnalyzer] {conversations.Count:N0} conversations to analyze");

        if (conversations.Count == 0) return 0;

        var results = new List<SentimentResult>();
        var unmatchedForClaude = new List<(int Index, string ConversationId, string Text)>();

        // Phase 1: Keyword matching
        for (var i = 0; i < conversations.Count; i++)
        {
            var (convId, customerText) = conversations[i];

            if (string.IsNullOrWhiteSpace(customerText))
            {
                results.Add(new SentimentResult
                {
                    ConversationId = convId,
                    Sentiment = "neutral",
                    Score = 0.0f,
                    Method = "empty"
                });
                continue;
            }

            var transliterated = _normalizer.TransliterateTurkish(customerText);
            var keywordResult = ClassifyByKeyword(transliterated);

            if (keywordResult != null)
            {
                results.Add(new SentimentResult
                {
                    ConversationId = convId,
                    Sentiment = keywordResult.Value.Sentiment,
                    Score = keywordResult.Value.Score,
                    Method = "keyword"
                });
            }
            else
            {
                unmatchedForClaude.Add((results.Count, convId, customerText));
                results.Add(new SentimentResult
                {
                    ConversationId = convId,
                    Sentiment = "neutral",
                    Score = 0.0f,
                    Method = _claude.IsAvailable ? "pending_claude" : "skipped"
                });
            }

            if (i % 5000 == 0 && i > 0)
            {
                var pct = (int)((double)i / conversations.Count * 50);
                await onProgress(new StageProgress("sentiment", pct, $"Keyword analysis: {i:N0}/{conversations.Count:N0}", StageNumber, TotalStages));
            }
        }

        var keywordCount = results.Count(r => r.Method == "keyword");
        var emptyCount = results.Count(r => r.Method == "empty");
        _logger.SystemInfo($"[SentimentAnalyzer] Keyword: {keywordCount}, empty: {emptyCount}, unmatched: {unmatchedForClaude.Count}");

        // Phase 2: Claude Haiku for mixed/unmatched
        if (_claude.IsAvailable && unmatchedForClaude.Count > 0)
        {
            _logger.SystemInfo($"[SentimentAnalyzer] Sending {unmatchedForClaude.Count:N0} conversations to Claude");

            for (var batchStart = 0; batchStart < unmatchedForClaude.Count; batchStart += ClaudeBatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(batchStart + ClaudeBatchSize, unmatchedForClaude.Count);
                var batch = unmatchedForClaude.GetRange(batchStart, batchEnd - batchStart);

                try
                {
                    var userContent = BuildClaudeBatchContent(batch);
                    var responseText = await _claude.SendBatchAsync(ClaudeSystemPrompt, userContent, ct);
                    var parsed = ClaudeClient.ParseJsonArray<ClaudeSentimentItem>(responseText, _logger);

                    foreach (var item in parsed)
                    {
                        if (item.Index < 0 || item.Index >= batch.Count) continue;
                        var (resultIdx, convId, _) = batch[item.Index];

                        var sentiment = item.Sentiment is "positive" or "neutral" or "negative"
                            ? item.Sentiment
                            : "neutral";
                        var score = Math.Clamp(item.Score, -1.0f, 1.0f);

                        results[resultIdx] = new SentimentResult
                        {
                            ConversationId = convId,
                            Sentiment = sentiment,
                            Score = score,
                            Method = "claude"
                        };
                    }
                }
                catch (TaskCanceledException) { throw; }
                catch (ArgumentOutOfRangeException ex)
                {
                    _logger.SystemWarn($"[SentimentAnalyzer] Claude batch {batchStart / ClaudeBatchSize} result mapping error: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemWarn($"[SentimentAnalyzer] Claude batch {batchStart / ClaudeBatchSize} processing error: {ex.Message}");
                }

                if (batchStart % (ClaudeBatchSize * 5) == 0)
                {
                    var pct = 50 + (int)((double)batchStart / unmatchedForClaude.Count * 40);
                    await onProgress(new StageProgress("sentiment", pct, $"Claude analysis: batch {batchStart / ClaudeBatchSize}", StageNumber, TotalStages));
                }
            }
        }

        // Fix remaining "pending_claude" entries
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Method == "pending_claude")
            {
                results[i] = results[i] with { Method = "skipped" };
            }
        }

        // Phase 3: Batch insert to wa_sentiments
        await onProgress(new StageProgress("sentiment", 90, $"Writing {results.Count:N0} sentiments to DB", StageNumber, TotalStages));
        await _repo.BatchInsertSentimentsAsync(analysisId, tenantId, results, ct);

        await onProgress(new StageProgress("sentiment", 100, $"Complete: {results.Count:N0} sentiments", StageNumber, TotalStages));

        var claudeCount = results.Count(r => r.Method == "claude");
        _logger.SystemInfo(
            $"[SentimentAnalyzer] Stage 6 complete: keyword={keywordCount}, claude={claudeCount}, empty={emptyCount}, total={results.Count}");

        return results.Count;
    }

    private (string Sentiment, float Score)? ClassifyByKeyword(string transliterated)
    {
        int posMatches, negMatches;
        try
        {
            posMatches = PositiveRegex.Matches(transliterated).Count;
            negMatches = NegativeRegex.Matches(transliterated).Count;
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.SystemWarn("[SentimentAnalyzer] Regex timeout in keyword classification, falling back to Claude");
            return null;
        }

        if (posMatches > 0 && negMatches == 0)
        {
            var score = Math.Min(0.5f + posMatches * 0.1f, 1.0f);
            return ("positive", score);
        }

        if (negMatches > 0 && posMatches == 0)
        {
            var score = Math.Max(-0.5f - negMatches * 0.1f, -1.0f);
            return ("negative", score);
        }

        // Mixed signals or no matches → Claude fallback
        return null;
    }

    private static string BuildClaudeBatchContent(List<(int Index, string ConversationId, string Text)> batch)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < batch.Count; i++)
        {
            var text = batch[i].Text.Length > 500 ? batch[i].Text[..500] : batch[i].Text;
            sb.AppendLine($"[{i}] {text}");
        }
        return sb.ToString();
    }

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
}
