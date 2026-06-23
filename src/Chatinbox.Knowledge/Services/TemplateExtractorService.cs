using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using Chatinbox.Knowledge.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Templates;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Knowledge.Services;

/// <summary>
/// Extracts templates from WA analysis results and compares against existing catalog.
/// Core engine: analyze → compare (pgvector similarity) → produce suggestions.
/// </summary>
public sealed class TemplateExtractorService
{
    private readonly TemplateRepository _templateRepo;
    private readonly KnowledgeConnectionFactory _db;
    private readonly EmbeddingService _embedding;
    private readonly TemplateResolutionService _resolution;
    private readonly JsonLinesLogger _logger;

    public TemplateExtractorService(
        TemplateRepository templateRepo,
        KnowledgeConnectionFactory db,
        EmbeddingService embedding,
        TemplateResolutionService resolution,
        JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _db = db;
        _embedding = embedding;
        _resolution = resolution;
        _logger = logger;
    }

    public async Task<TemplateCompareResult> ExtractAndCompareAsync(
        int analysisId, string tenantName, string? sector,
        decimal autoConfirmThreshold, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new TemplateCompareResult
        {
            AnalysisId = analysisId,
            TenantName = tenantName
        };

        try
        {
            // Get existing templates for comparison
            var existingFaqs = await _templateRepo.GetPublishedForComparisonAsync("faq", sector, ct);
            var existingIntents = await _templateRepo.GetPublishedForComparisonAsync("intent", sector, ct);

            // Process FAQ clusters
            var faqClusters = await ReadFaqClustersAsync(analysisId, ct);
            result.TotalClustersProcessed = faqClusters.Count;

            foreach (var cluster in faqClusters)
            {
                var suggestion = await CompareFaqClusterAsync(
                    analysisId, cluster, existingFaqs, sector, tenantName,
                    autoConfirmThreshold, ct);

                if (suggestion != null)
                {
                    result.Suggestions.Add(suggestion);
                    switch (suggestion.SuggestionType)
                    {
                        case "new": result.NewCount++; break;
                        case "update": result.UpdateCount++; break;
                        case "merge": result.ConfirmCount++; break;
                    }
                }
            }

            // Process intents
            var intents = await ReadIntentDistributionAsync(analysisId, ct);
            result.TotalIntentsProcessed = intents.Count;

            foreach (var intent in intents)
            {
                var suggestion = await CompareIntentAsync(
                    analysisId, intent, existingIntents, sector, tenantName, ct);

                if (suggestion != null)
                {
                    result.Suggestions.Add(suggestion);
                    switch (suggestion.SuggestionType)
                    {
                        case "new": result.NewCount++; break;
                        case "update": result.UpdateCount++; break;
                        case "merge": result.ConfirmCount++; break;
                    }
                }
            }

            // Invalidate resolution cache since source counts may have changed
            _resolution.InvalidateCache();

            _logger.SystemInfo($"[TemplateExtraction] Complete: analysis={analysisId}, tenant={tenantName}, sector={sector}, " +
                $"new={result.NewCount}, update={result.UpdateCount}, confirm={result.ConfirmCount}, " +
                $"duration={sw.ElapsedMilliseconds}ms");
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateComparisonFailed}] Extraction DB error: analysis={analysisId}: {ex.Message}");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateComparisonFailed}] Extraction service error: analysis={analysisId}: {ex.Message}");
            throw;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    // ── FAQ cluster comparison ──────────────────────────────────

    private async Task<TemplateSuggestionDto?> CompareFaqClusterAsync(
        int analysisId, FaqClusterData cluster,
        List<TemplateCatalogDto> existingFaqs, string? sector,
        string tenantName, decimal autoConfirmThreshold, CancellationToken ct)
    {
        if (cluster.QuestionCount < 3) return null; // skip tiny clusters

        decimal bestSimilarity = 0;
        TemplateCatalogDto? bestMatch = null;

        // Try embedding-based similarity
        if (_embedding.IsAvailable && existingFaqs.Count > 0)
        {
            var queryEmbedding = await _embedding.GetEmbeddingAsync(cluster.RepresentativeQuestion, ct);
            if (queryEmbedding != null)
            {
                foreach (var existing in existingFaqs)
                {
                    var existingQuestion = ExtractQuestion(existing.ContentJson);
                    if (string.IsNullOrEmpty(existingQuestion)) continue;

                    var existingEmbedding = await _embedding.GetEmbeddingAsync(existingQuestion, ct);
                    if (existingEmbedding == null) continue;

                    var similarity = CosineSimilarity(queryEmbedding.ToArray(), existingEmbedding.ToArray());
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestMatch = existing;
                    }
                }
            }
        }

        // Fallback: keyword overlap
        if (bestMatch == null || bestSimilarity < 0.3m)
        {
            foreach (var existing in existingFaqs)
            {
                var existingQuestion = ExtractQuestion(existing.ContentJson);
                if (string.IsNullOrEmpty(existingQuestion)) continue;

                var overlap = KeywordOverlap(cluster.RepresentativeQuestion, existingQuestion);
                if (overlap > bestSimilarity)
                {
                    bestSimilarity = overlap;
                    bestMatch = existing;
                }
            }
        }

        var slug = GenerateSlug(sector ?? "eticaret", "faq", cluster.RepresentativeQuestion);
        var contentJson = new
        {
            question = cluster.RepresentativeQuestion,
            answer = cluster.SampleAnswers.FirstOrDefault() ?? "",
            category = InferCategory(cluster.RepresentativeQuestion),
            keywords = ExtractKeywords(cluster.RepresentativeQuestion),
            sample_questions = cluster.SampleQuestions,
            sample_answers = cluster.SampleAnswers
        };

        string suggestionType;
        if (bestSimilarity > autoConfirmThreshold && bestMatch != null)
        {
            // CONFIRM — add source, increment count
            suggestionType = "merge";
            await _templateRepo.InsertSourceAsync(
                bestMatch.Id, analysisId, tenantName, "confirm", cluster.QuestionCount, ct);
            await _templateRepo.IncrementSourceCountAsync(bestMatch.Id, ct);
            // Still create a suggestion record for tracking
        }
        else if (bestSimilarity >= 0.50m && bestMatch != null)
        {
            suggestionType = "update";
        }
        else
        {
            suggestionType = "new";
        }

        var suggestionId = await _templateRepo.InsertSuggestionAsync(
            analysisId, suggestionType,
            bestMatch?.Id,
            bestSimilarity > 0 ? bestSimilarity : null,
            contentJson, slug,
            $"FAQ: {TruncateText(cluster.RepresentativeQuestion, 100)}",
            "faq",
            new { cluster.ClusterLabel, cluster.QuestionCount, tenantName },
            ct);

        return new TemplateSuggestionDto
        {
            Id = suggestionId,
            AnalysisId = analysisId,
            SuggestionType = suggestionType,
            ExistingTemplateId = bestMatch?.Id,
            ExistingTemplateName = bestMatch?.Name,
            SimilarityScore = bestSimilarity > 0 ? bestSimilarity : null,
            SuggestedContentJson = contentJson,
            SuggestedSlug = slug,
            SuggestedName = $"FAQ: {TruncateText(cluster.RepresentativeQuestion, 100)}",
            SuggestedType = "faq",
            Status = suggestionType == "merge" ? "approved" : "pending",
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── Intent comparison ───────────────────────────────────────

    private async Task<TemplateSuggestionDto?> CompareIntentAsync(
        int analysisId, IntentData intent,
        List<TemplateCatalogDto> existingIntents, string? sector,
        string tenantName, CancellationToken ct)
    {
        // Check if intent already exists in catalog
        var existing = existingIntents.FirstOrDefault(e =>
        {
            var name = ExtractIntentName(e.ContentJson);
            return string.Equals(name, intent.IntentName, StringComparison.OrdinalIgnoreCase);
        });

        if (existing != null)
        {
            // Confirm — intent already known
            await _templateRepo.InsertSourceAsync(
                existing.Id, analysisId, tenantName, "confirm", intent.MessageCount, ct);
            await _templateRepo.IncrementSourceCountAsync(existing.Id, ct);
            return null; // no suggestion needed for confirmed intents
        }

        // New intent
        var slug = GenerateSlug(sector ?? "eticaret", "intent", intent.IntentName);
        var contentJson = new
        {
            intent_name = intent.IntentName,
            keywords = intent.Keywords,
            patterns = Array.Empty<string>(),
            confidence_threshold = 0.7,
            sample_messages = intent.SampleMessages
        };

        var suggestionId = await _templateRepo.InsertSuggestionAsync(
            analysisId, "new", null, null, contentJson, slug,
            $"Intent: {intent.IntentName}",
            "intent",
            new { intent.IntentName, intent.MessageCount, tenantName },
            ct);

        return new TemplateSuggestionDto
        {
            Id = suggestionId,
            AnalysisId = analysisId,
            SuggestionType = "new",
            SuggestedContentJson = contentJson,
            SuggestedSlug = slug,
            SuggestedName = $"Intent: {intent.IntentName}",
            SuggestedType = "intent",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── WA data readers ─────────────────────────────────────────

    private async Task<List<FaqClusterData>> ReadFaqClustersAsync(int analysisId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cluster_label, representative_question, question_count,
                   sample_questions, sample_answers
            FROM wa_faq_clusters WHERE analysis_id = @aid
            ORDER BY question_count DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);

        var items = new List<FaqClusterData>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            items.Add(new FaqClusterData
            {
                ClusterLabel = r.GetInt32(0).ToString(),
                RepresentativeQuestion = r.GetString(1),
                QuestionCount = r.GetInt32(2),
                SampleQuestions = r.IsDBNull(3) ? [] : ParseJsonArray(r.GetString(3)),
                SampleAnswers = r.IsDBNull(4) ? [] : ParseJsonArray(r.GetString(4))
            });
        }
        return items;
    }

    private async Task<List<IntentData>> ReadIntentDistributionAsync(int analysisId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT intent, COUNT(*)::int as cnt,
                   array_agg(DISTINCT substring(message_text for 100)) FILTER (WHERE message_text IS NOT NULL) as samples
            FROM wa_intents
            WHERE analysis_id = @aid AND intent != 'unknown'
            GROUP BY intent
            ORDER BY cnt DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);

        var items = new List<IntentData>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var intentName = r.GetString(0);
            items.Add(new IntentData
            {
                IntentName = intentName,
                MessageCount = r.GetInt32(1),
                Keywords = InferKeywordsFromIntent(intentName),
                SampleMessages = r.IsDBNull(2) ? [] : ((string[])r.GetValue(2)).Take(5).ToArray()
            });
        }
        return items;
    }

    // ── Utility methods ─────────────────────────────────────────

    private static decimal CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : (decimal)(dot / denom);
    }

    private static decimal KeywordOverlap(string a, string b)
    {
        var wordsA = NormalizeAndSplit(a);
        var wordsB = NormalizeAndSplit(b);
        if (wordsA.Count == 0 || wordsB.Count == 0) return 0;
        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();
        return union == 0 ? 0 : (decimal)intersection / union;
    }

    private static HashSet<string> NormalizeAndSplit(string text)
    {
        return text.ToLowerInvariant()
            .Replace("?", "").Replace("!", "").Replace(".", "").Replace(",", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();
    }

    private static string GenerateSlug(string sector, string type, string name)
    {
        var normalized = name.ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace("İ", "i").Replace("Ğ", "g").Replace("Ü", "u")
            .Replace("Ş", "s").Replace("Ö", "o").Replace("Ç", "c");

        var slug = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        slug = slug.Trim('-');
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');

        return $"{sector}.{type}.{slug}";
    }

    private string? ExtractQuestion(object? contentJson)
    {
        if (contentJson == null) return null;
        try
        {
            var json = contentJson is JsonElement el ? el : JsonSerializer.SerializeToElement(contentJson);
            return json.TryGetProperty("question", out var q) ? q.GetString() : null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateComparisonFailed}] ExtractQuestion parse error: {ex.Message}");
            return null;
        }
    }

    private string? ExtractIntentName(object? contentJson)
    {
        if (contentJson == null) return null;
        try
        {
            var json = contentJson is JsonElement el ? el : JsonSerializer.SerializeToElement(contentJson);
            return json.TryGetProperty("intent_name", out var n) ? n.GetString() : null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateComparisonFailed}] ExtractIntentName parse error: {ex.Message}");
            return null;
        }
    }

    private static string InferCategory(string question)
    {
        var q = question.ToLowerInvariant();
        if (q.Contains("ödeme") || q.Contains("kapıda") || q.Contains("havale") || q.Contains("taksit")) return "payment";
        if (q.Contains("kargo") || q.Contains("teslimat") || q.Contains("günde")) return "shipping";
        if (q.Contains("iade") || q.Contains("değişim") || q.Contains("geri")) return "returns";
        if (q.Contains("beden") || q.Contains("numara") || q.Contains("boy")) return "sizing";
        if (q.Contains("fiyat") || q.Contains("kadar") || q.Contains("kaç")) return "pricing";
        if (q.Contains("stok") || q.Contains("var mı") || q.Contains("mevcut")) return "stock";
        if (q.Contains("renk") || q.Contains("model")) return "product_info";
        return "general";
    }

    private static string[] ExtractKeywords(string text)
    {
        return NormalizeAndSplit(text)
            .Where(w => w.Length > 3)
            .Take(10)
            .ToArray();
    }

    private static string[] InferKeywordsFromIntent(string intentName)
    {
        return intentName switch
        {
            "size_inquiry" => ["beden", "numara", "boy", "kilo", "manken"],
            "price_inquiry" => ["fiyat", "kadar", "ucret", "para"],
            "stock_inquiry" => ["stok", "mevcut", "kaldi", "tukendi"],
            "shipping_inquiry" => ["kargo", "teslimat", "gonderim", "gelir"],
            "return_request" => ["iade", "degisim", "geri", "uymadi"],
            "complaint" => ["kotu", "bozuk", "yanlis", "sikayetim"],
            "order_confirmation" => ["siparis", "aliyorum", "gonder", "onayliyorum"],
            "greeting" => ["merhaba", "selam", "iyi gunler"],
            "thank_you" => ["tesekkur", "sagol", "harika"],
            "product_inquiry" => ["urun", "bilgi", "detay"],
            "discount_inquiry" => ["indirim", "kampanya", "kupon"],
            "address_info" => ["adres", "mahalle", "sokak"],
            _ => [intentName]
        };
    }

    private static string TruncateText(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..maxLen] + "...";

    private string[] ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateComparisonFailed}] ParseJsonArray error: {ex.Message}");
            return [];
        }
    }

    // ── Internal data classes ────────────────────────────────────

    private sealed class FaqClusterData
    {
        public string ClusterLabel { get; set; } = "";
        public string RepresentativeQuestion { get; set; } = "";
        public int QuestionCount { get; set; }
        public string[] SampleQuestions { get; set; } = [];
        public string[] SampleAnswers { get; set; } = [];
    }

    private sealed class IntentData
    {
        public string IntentName { get; set; } = "";
        public int MessageCount { get; set; }
        public string[] Keywords { get; set; } = [];
        public string[] SampleMessages { get; set; } = [];
    }
}
