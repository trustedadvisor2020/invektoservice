using System.Text.RegularExpressions;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 7: Product code / price separation per conversation.
/// Pure heuristic (no Claude): KMR codes, price patterns, 4-digit heuristic.
/// Fixes Phase A bug where prices and product codes were lumped together.
/// </summary>
public sealed class ProductAnalyzerService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    private const int TotalStages = 7;
    private const int StageNumber = 7;

    // KMR product code pattern (always product code)
    private static readonly Regex KmrPattern = new(@"\b(kmr[\w-]*)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    // Explicit price with currency suffix
    private static readonly Regex PriceSuffixPattern = new(@"(\d{3,5})\s*(?:tl|₺|lira|turk lirasi)", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    // Price in answer form: "fiyatı: 1599"
    private static readonly Regex PriceAnswerPattern = new(@"(?:fiyat[ıi]?|ucret[i]?|tutar[ıi]?)\s*[:=]?\s*(\d{3,5})", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    // Generic 4-digit number
    private static readonly Regex FourDigitPattern = new(@"\b(\d{4})\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public ProductAnalyzerService(
        AnalyticsRepository repo,
        AnalyticsConnectionFactory db,
        TextNormalizer normalizer,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _db = db;
        _normalizer = normalizer;
        _logger = logger;
    }

    /// <summary>
    /// Run product/price analysis on all conversations.
    /// Reads from wa_conversations + wa_messages, writes to wa_products + wa_prices.
    /// </summary>
    public async Task<int> RunAsync(
        int analysisId, int tenantId,
        Func<StageProgress, Task> onProgress,
        CancellationToken ct)
    {
        _logger.SystemInfo($"[ProductAnalyzer] Starting stage 7 for analysis {analysisId}");

        // Get conversations with their message text aggregated
        var conversations = await _repo.GetConversationsWithTextAsync(analysisId, tenantId, ct);
        _logger.SystemInfo($"[ProductAnalyzer] {conversations.Count:N0} conversations to analyze");

        if (conversations.Count == 0) return 0;

        var results = new List<ProductResult>();
        var allPrices = new Dictionary<decimal, int>(); // price → mention count

        for (var i = 0; i < conversations.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var conv = conversations[i];
            var transliterated = _normalizer.TransliterateTurkish(conv.AllText);
            var originalText = conv.AllText;

            var productCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prices = new HashSet<decimal>();

            try
            {
                // 1. KMR codes → always product codes
                foreach (Match m in KmrPattern.Matches(originalText))
                    productCodes.Add(m.Groups[1].Value);

                // 2. Explicit price suffix → always prices
                foreach (Match m in PriceSuffixPattern.Matches(transliterated))
                {
                    if (decimal.TryParse(m.Groups[1].Value, out var price))
                    {
                        prices.Add(price);
                        allPrices[price] = allPrices.GetValueOrDefault(price) + 1;
                    }
                }

                // 3. Price answer patterns → always prices
                foreach (Match m in PriceAnswerPattern.Matches(transliterated))
                {
                    if (decimal.TryParse(m.Groups[1].Value, out var price))
                    {
                        prices.Add(price);
                        allPrices[price] = allPrices.GetValueOrDefault(price) + 1;
                    }
                }

                // 4. 4-digit numbers → heuristic
                foreach (Match m in FourDigitPattern.Matches(originalText))
                {
                    var numStr = m.Groups[1].Value;
                    // Skip if already captured as explicit price
                    if (decimal.TryParse(numStr, out var num) && prices.Contains(num)) continue;

                    if (IsLikelyPrice(numStr))
                    {
                        prices.Add(num);
                        allPrices[num] = allPrices.GetValueOrDefault(num) + 1;
                    }
                    else
                    {
                        productCodes.Add(numStr);
                    }
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.SystemWarn($"[ProductAnalyzer] Regex timeout for conversation {conv.ConversationId}: {ex.Message}");
            }

            results.Add(new ProductResult
            {
                ConversationId = conv.ConversationId,
                ProductCodes = string.Join("|", productCodes),
                ProductCount = productCodes.Count,
                PricesMentioned = string.Join("|", prices),
                PriceCount = prices.Count,
                Outcome = conv.Outcome,
                PrimaryAgent = conv.PrimaryAgent
            });

            if (i % 5000 == 0 && i > 0)
            {
                var pct = (int)((double)i / conversations.Count * 80);
                await onProgress(new StageProgress("products", pct, $"Analyzing: {i:N0}/{conversations.Count:N0}", StageNumber, TotalStages));
            }
        }

        // Build price entries
        var priceEntries = allPrices
            .Select(kv => new PriceEntry
            {
                Price = kv.Key,
                MentionCount = kv.Value,
                LikelyTl = "yes"
            })
            .OrderByDescending(p => p.MentionCount)
            .ToList();

        // Batch insert
        await onProgress(new StageProgress("products", 85, $"Writing {results.Count:N0} products + {priceEntries.Count:N0} prices to DB", StageNumber, TotalStages));
        await _repo.BatchInsertProductsAsync(analysisId, tenantId, results, ct);
        await _repo.BatchInsertPricesAsync(analysisId, tenantId, priceEntries, ct);

        await onProgress(new StageProgress("products", 100, $"Complete: {results.Count:N0} conversations, {priceEntries.Count:N0} unique prices", StageNumber, TotalStages));

        var totalProducts = results.Sum(r => r.ProductCount);
        var totalPrices = results.Sum(r => r.PriceCount);
        _logger.SystemInfo(
            $"[ProductAnalyzer] Stage 7 complete: {totalProducts:N0} products, {totalPrices:N0} price mentions, {priceEntries.Count:N0} unique prices");

        return results.Count;
    }

    /// <summary>
    /// Heuristic: is this 4-digit number likely a Turkish fashion price?
    /// Prices typically end in 99, 00, 50 or are divisible by 1000.
    /// </summary>
    private static bool IsLikelyPrice(string numberStr)
    {
        if (!int.TryParse(numberStr, out var num)) return false;

        // Numbers ending in 99 are almost always prices (1099, 1199, 1299, etc.)
        if (numberStr.EndsWith("99")) return true;

        // Round thousands (1000, 2000, 3000, 4000, 5000)
        if (num % 1000 == 0) return true;

        // Numbers ending in 00 (1500, 2500, etc.)
        if (numberStr.EndsWith("00")) return true;

        // Numbers ending in 50 (1250, 1350, etc.)
        if (numberStr.EndsWith("50")) return true;

        return false;
    }
}
