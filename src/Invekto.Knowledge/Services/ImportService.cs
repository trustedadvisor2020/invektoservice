using System.Diagnostics;
using System.Text.Json;
using Invekto.Knowledge.Data;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Services;

/// <summary>
/// WA-3 NLP data import orchestrator.
/// Parses FAQ clusters JSON, intent CSV, product CSV, sentiment CSV.
/// Applies quality filters and batch inserts into Knowledge DB.
/// </summary>
public sealed class ImportService
{
    private readonly KnowledgeRepository _repository;
    private readonly JsonLinesLogger _logger;

    public ImportService(KnowledgeRepository repository, JsonLinesLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ImportSummary> ImportNlpDataAsync(
        int tenantId, string nlpOutputPath, int minQuestions, int minAnswerLength,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var summary = new ImportSummary();

        // Create document record for this import batch
        var docId = await _repository.InsertDocumentAsync(
            tenantId, $"WA-3 Import {DateTime.UtcNow:yyyy-MM-dd}", "wa_import",
            nlpOutputPath, JsonSerializer.Serialize(new { minQuestions, minAnswerLength }), ct);
        summary.DocumentId = docId;

        try
        {
            // 1. FAQ clusters
            var faqPath = Path.Combine(nlpOutputPath, "faq_clusters.json");
            if (File.Exists(faqPath))
            {
                var (imported, skipped) = await ImportFaqClustersAsync(tenantId, faqPath, minQuestions, minAnswerLength, ct);
                summary.FaqsImported = imported;
                summary.FaqsSkipped = skipped;
                _logger.SystemInfo($"FAQ import: {imported} imported, {skipped} skipped");
            }
            else
            {
                _logger.SystemWarn($"FAQ clusters file not found: {faqPath}");
            }

            // 2. Intent patterns
            var intentPath = Path.Combine(nlpOutputPath, "intent_classifications.csv");
            if (File.Exists(intentPath))
            {
                summary.IntentsImported = await ImportIntentPatternsAsync(tenantId, intentPath, ct);
                _logger.SystemInfo($"Intent import: {summary.IntentsImported} patterns");
            }
            else
            {
                _logger.SystemWarn($"Intent classifications file not found: {intentPath}");
            }

            // 3. Product catalog
            var productPath = Path.Combine(nlpOutputPath, "product_analysis.csv");
            if (File.Exists(productPath))
            {
                summary.ProductsImported = await ImportProductCatalogAsync(tenantId, productPath, ct);
                _logger.SystemInfo($"Product import: {summary.ProductsImported} products");
            }
            else
            {
                _logger.SystemWarn($"Product analysis file not found: {productPath}");
            }

            // 4. Sentiment
            var sentimentPath = Path.Combine(nlpOutputPath, "sentiment.csv");
            if (File.Exists(sentimentPath))
            {
                summary.SentimentsImported = await ImportSentimentsAsync(tenantId, sentimentPath, ct);
                _logger.SystemInfo($"Sentiment import: {summary.SentimentsImported} conversations");
            }
            else
            {
                _logger.SystemWarn($"Sentiment file not found: {sentimentPath}");
            }

            await _repository.UpdateDocumentStatusAsync(tenantId, docId, "ready", summary.FaqsImported, ct);
        }
        catch (Exception ex)
        {
            _logger.SystemError($"Import failed at document {docId}: {ex.Message}");
            await _repository.UpdateDocumentStatusAsync(tenantId, docId, "error", 0, ct);
            throw;
        }

        sw.Stop();
        summary.DurationMs = (int)sw.ElapsedMilliseconds;
        _logger.SystemInfo($"Import complete in {summary.DurationMs}ms: {summary.FaqsImported} FAQs, {summary.IntentsImported} intents, {summary.ProductsImported} products, {summary.SentimentsImported} sentiments");

        return summary;
    }

    // ============================================================
    // FAQ Clusters (faq_clusters.json)
    // ============================================================

    private async Task<(int imported, int skipped)> ImportFaqClustersAsync(
        int tenantId, string filePath, int minQuestions, int minAnswerLength, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        var clusters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new InvalidOperationException("Failed to parse faq_clusters.json");

        var rows = new List<FaqImportRow>();
        var skipped = 0;

        foreach (var (clusterId, cluster) in clusters)
        {
            var questionCount = cluster.GetProperty("question_count").GetInt32();
            if (questionCount < minQuestions)
            {
                skipped++;
                continue;
            }

            var representativeQuestion = cluster.GetProperty("representative_question").GetString() ?? "";
            var sampleAnswers = cluster.GetProperty("sample_answers");

            // Find best answer (longest answer that meets minimum length)
            string? bestAnswer = null;
            foreach (var ans in sampleAnswers.EnumerateArray())
            {
                var ansText = ans.GetString() ?? "";
                if (ansText.Length >= minAnswerLength && (bestAnswer == null || ansText.Length > bestAnswer.Length))
                    bestAnswer = ansText;
            }

            if (bestAnswer == null)
            {
                skipped++;
                continue;
            }

            var category = InferCategory(representativeQuestion);
            var metadata = JsonSerializer.Serialize(new
            {
                cluster_id = clusterId,
                question_count = questionCount
            });

            rows.Add(new FaqImportRow
            {
                Question = representativeQuestion.Length > 1000 ? representativeQuestion[..1000] : representativeQuestion,
                Answer = bestAnswer,
                Category = category,
                SourceMetadata = metadata,
                Keywords = ExtractKeywords(representativeQuestion)
            });
        }

        var imported = await _repository.BatchInsertFaqsAsync(tenantId, rows, ct);
        return (imported, skipped);
    }

    // ============================================================
    // Intent Patterns (intent_classifications.csv)
    // Stream processing: aggregate 1M rows -> 12 intent patterns
    // ============================================================

    private async Task<int> ImportIntentPatternsAsync(int tenantId, string filePath, CancellationToken ct)
    {
        var aggregated = new Dictionary<string, IntentAggregator>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(filePath);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (ct.IsCancellationRequested) break;

            var parts = line.Split(';');
            if (parts.Length < 7) continue;

            var intent = parts[4].Trim();
            if (string.IsNullOrEmpty(intent) || intent == "unknown") continue;

            if (!aggregated.TryGetValue(intent, out var agg))
            {
                agg = new IntentAggregator();
                aggregated[intent] = agg;
            }

            if (decimal.TryParse(parts[5].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var confidence))
            {
                agg.TotalConfidence += confidence;
            }

            agg.Count++;

            // Collect up to 10 sample messages
            if (agg.Samples.Count < 10)
            {
                var messageText = parts[2].Trim();
                if (messageText.Length > 5 && messageText.Length < 200)
                    agg.Samples.Add(messageText);
            }
        }

        var rows = aggregated.Select(kvp => new IntentPatternRow
        {
            IntentName = kvp.Key,
            Keywords = ExtractIntentKeywords(kvp.Key),
            ConfidenceAvg = kvp.Value.Count > 0 ? kvp.Value.TotalConfidence / kvp.Value.Count : null,
            SampleCount = kvp.Value.Count,
            SampleMessagesJson = JsonSerializer.Serialize(kvp.Value.Samples)
        }).ToList();

        return await _repository.BatchInsertIntentPatternsAsync(tenantId, rows, ct);
    }

    // ============================================================
    // Product Catalog (product_analysis.csv)
    // ============================================================

    private async Task<int> ImportProductCatalogAsync(int tenantId, string filePath, CancellationToken ct)
    {
        var products = new Dictionary<string, ProductAggregator>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(filePath);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (ct.IsCancellationRequested) break;

            var parts = line.Split(';');
            if (parts.Length < 7) continue;

            var codes = parts[1].Trim();
            var outcome = parts[5].Trim();
            var pricesPart = parts[3].Trim();

            foreach (var code in codes.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmedCode = code.Trim();
                if (string.IsNullOrEmpty(trimmedCode)) continue;

                if (!products.TryGetValue(trimmedCode, out var agg))
                {
                    agg = new ProductAggregator();
                    products[trimmedCode] = agg;
                }

                agg.MentionCount++;

                switch (outcome.ToLowerInvariant())
                {
                    case "sale": agg.SaleCount++; break;
                    case "offered": agg.OfferedCount++; break;
                }

                if (!agg.Outcomes.ContainsKey(outcome))
                    agg.Outcomes[outcome] = 0;
                agg.Outcomes[outcome]++;

                // Try to extract price for this product
                foreach (var priceStr in pricesPart.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (decimal.TryParse(priceStr.Trim(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                    {
                        agg.TotalPrice += price;
                        agg.PriceCount++;
                    }
                }
            }
        }

        var rows = products.Select(kvp => new ProductCatalogRow
        {
            ProductCode = kvp.Key.Length > 50 ? kvp.Key[..50] : kvp.Key,
            MentionCount = kvp.Value.MentionCount,
            SaleCount = kvp.Value.SaleCount,
            OfferedCount = kvp.Value.OfferedCount,
            AvgPrice = kvp.Value.PriceCount > 0 ? kvp.Value.TotalPrice / kvp.Value.PriceCount : null,
            OutcomesJson = JsonSerializer.Serialize(kvp.Value.Outcomes)
        }).ToList();

        return await _repository.BatchInsertProductCatalogAsync(tenantId, rows, ct);
    }

    // ============================================================
    // Sentiments (sentiment.csv)
    // ============================================================

    private async Task<int> ImportSentimentsAsync(int tenantId, string filePath, CancellationToken ct)
    {
        const int batchSize = 500;
        var batch = new List<SentimentRow>(batchSize);
        var totalInserted = 0;

        using var reader = new StreamReader(filePath);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (ct.IsCancellationRequested) break;

            var parts = line.Split(';');
            if (parts.Length < 4) continue;

            var conversationId = parts[0].Trim();
            var sentiment = parts[1].Trim();
            var method = parts[3].Trim();

            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(sentiment))
                continue;

            // Normalize sentiment value
            sentiment = sentiment.ToLowerInvariant() switch
            {
                "positive" => "positive",
                "negative" => "negative",
                _ => "neutral"
            };

            decimal? score = null;
            if (decimal.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedScore))
            {
                score = parsedScore;
            }

            batch.Add(new SentimentRow
            {
                ConversationId = conversationId,
                Sentiment = sentiment,
                Score = score,
                Method = string.IsNullOrEmpty(method) ? "keyword" : method
            });

            // Flush batch when full (keeps max ~500 rows in memory)
            if (batch.Count >= batchSize)
            {
                totalInserted += await _repository.BatchInsertSentimentsAsync(tenantId, batch, batchSize, ct);
                batch.Clear();
            }
        }

        // Flush remaining rows
        if (batch.Count > 0)
            totalInserted += await _repository.BatchInsertSentimentsAsync(tenantId, batch, batchSize, ct);

        return totalInserted;
    }

    // ============================================================
    // Category inference (heuristic)
    // ============================================================

    private static string InferCategory(string question)
    {
        var lower = question.ToLowerInvariant();

        if (lower.Contains("fiyat") || lower.Contains("kaç tl") || lower.Contains("ücret") || lower.Contains("tutar"))
            return "price";
        if (lower.Contains("kargo") || lower.Contains("teslimat") || lower.Contains("gönderim") || lower.Contains("teslim"))
            return "shipping";
        if (lower.Contains("iade") || lower.Contains("değişim") || lower.Contains("iptal"))
            return "returns";
        if (lower.Contains("beden") || lower.Contains("ölçü") || lower.Contains("numara") || lower.Contains("boy"))
            return "size";
        if (lower.Contains("stok") || lower.Contains("mevcut") || lower.Contains("var mı"))
            return "stock";
        if (lower.Contains("sipariş") || lower.Contains("satın") || lower.Contains("almak"))
            return "order";
        if (lower.Contains("ödeme") || lower.Contains("kapıda") || lower.Contains("havale") || lower.Contains("kredi"))
            return "payment";

        return "general";
    }

    private static string[] ExtractKeywords(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !StopWords.Contains(w))
            .Distinct()
            .Take(10)
            .ToArray();
        return words;
    }

    private static string[] ExtractIntentKeywords(string intentName)
    {
        return intentName switch
        {
            "order_confirmation" => new[] { "sipariş", "onay", "teyit" },
            "stock_inquiry" => new[] { "stok", "mevcut", "var mı" },
            "price_inquiry" => new[] { "fiyat", "kaç", "ücret", "tl" },
            "size_inquiry" => new[] { "beden", "numara", "ölçü", "boy" },
            "shipping_inquiry" => new[] { "kargo", "teslimat", "gönderim", "ne zaman" },
            "return_request" => new[] { "iade", "değişim", "iptal", "geri" },
            "address_info" => new[] { "adres", "konum", "nerede" },
            "greeting" => new[] { "merhaba", "selam", "günaydın" },
            "thank_you" => new[] { "teşekkür", "sağol", "eyvallah" },
            "product_inquiry" => new[] { "ürün", "model", "çeşit" },
            _ => new[] { intentName }
        };
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bir", "bu", "da", "de", "ve", "ile", "için", "gibi", "çok", "var",
        "ben", "sen", "biz", "siz", "olan", "olarak", "daha", "merhaba",
        "teşekkür", "eder", "ederim", "lütfen", "rica"
    };
}

// ============================================================
// Import DTOs
// ============================================================

public sealed class ImportSummary
{
    public bool Success => FaqsImported > 0 || IntentsImported > 0 || ProductsImported > 0 || SentimentsImported > 0;
    public int FaqsImported { get; set; }
    public int FaqsSkipped { get; set; }
    public int IntentsImported { get; set; }
    public int ProductsImported { get; set; }
    public int SentimentsImported { get; set; }
    public int DocumentId { get; set; }
    public int DurationMs { get; set; }
}

internal sealed class IntentAggregator
{
    public int Count { get; set; }
    public decimal TotalConfidence { get; set; }
    public List<string> Samples { get; } = new();
}

internal sealed class ProductAggregator
{
    public int MentionCount { get; set; }
    public int SaleCount { get; set; }
    public int OfferedCount { get; set; }
    public decimal TotalPrice { get; set; }
    public int PriceCount { get; set; }
    public Dictionary<string, int> Outcomes { get; } = new(StringComparer.OrdinalIgnoreCase);
}
