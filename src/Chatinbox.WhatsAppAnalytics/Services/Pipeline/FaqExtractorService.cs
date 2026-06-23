using System.Text.RegularExpressions;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 5: FAQ pair extraction + hash-bucket grouping.
/// Extracts Q&A pairs from conversations (customer question → agent answer).
/// Groups similar questions using first-N-word hash bucketing + Levenshtein distance.
/// No Claude API calls — pure heuristic extraction and grouping.
/// </summary>
public sealed class FaqExtractorService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    private const int TotalStages = 7;
    private const int StageNumber = 5;
    private const int MinQuestionLen = 5;
    private const int MinAnswerLen = 10;
    private const int AnswerSearchWindow = 5;   // Look ahead N messages for answer
    private const int MaxBucketLevenshtein = 3;  // Max edit distance within bucket
    private const int MinClusterSize = 3;        // Minimum questions per cluster
    private const int MaxSampleQuestions = 5;
    private const int MaxSampleAnswers = 3;
    private const int BucketKeyWordCount = 3;    // First N words for bucket key
    private const int MaxBucketSize = 500;        // Cap bucket size to bound O(n*k) Levenshtein merge

    // Question detection patterns (transliterated Turkish)
    private static readonly Regex QuestionRegex = Compile(
        @"\?" +
        @"|\b(kac|kactir|ne kadar)\b" +
        @"|\b(var mi|varmi)\b" +
        @"|\b(nasil|nasi)\b" +
        @"|\b(ne zaman|nezaman)\b" +
        @"|\b(nerede|nere)\b" +
        @"|\b(hangisi|hangi)\b" +
        @"|\b(olur mu|olurmu)\b" +
        @"|\b(mumkun mu|mumkunmu)\b" +
        @"|\b(gonderir misiniz)\b" +
        @"|\b(ister misiniz)\b" +
        @"|\b(beden|numara)\b.*\b(kac|ne)\b");

    public FaqExtractorService(
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
    /// Run FAQ extraction on all conversations.
    /// Reads from wa_messages, writes to wa_faq_pairs + wa_faq_clusters.
    /// </summary>
    public async Task<(int PairCount, int ClusterCount)> RunAsync(
        int analysisId, int tenantId,
        Func<StageProgress, Task> onProgress,
        CancellationToken ct)
    {
        _logger.SystemInfo($"[FaqExtractor] Starting stage 5 for analysis {analysisId}");

        // Phase 1: Extract Q&A pairs from conversation messages
        await onProgress(new StageProgress("faq", 0, "Extracting Q&A pairs from conversations", StageNumber, TotalStages));

        var conversationMessages = await _repo.GetMessagesGroupedByConversationAsync(analysisId, tenantId, ct);
        _logger.SystemInfo($"[FaqExtractor] {conversationMessages.Count:N0} conversations to scan");

        var allPairs = new List<FaqPair>();
        var processedConvs = 0;

        foreach (var (convId, messages) in conversationMessages)
        {
            ct.ThrowIfCancellationRequested();

            var pairs = ExtractQAPairs(convId, messages);
            allPairs.AddRange(pairs);

            processedConvs++;
            if (processedConvs % 5000 == 0)
            {
                var pct = (int)((double)processedConvs / conversationMessages.Count * 40);
                await onProgress(new StageProgress("faq", pct, $"Extracting: {processedConvs:N0}/{conversationMessages.Count:N0} conversations, {allPairs.Count:N0} pairs", StageNumber, TotalStages));
            }
        }

        _logger.SystemInfo($"[FaqExtractor] Extracted {allPairs.Count:N0} Q&A pairs from {conversationMessages.Count:N0} conversations");

        if (allPairs.Count == 0)
        {
            await onProgress(new StageProgress("faq", 100, "No Q&A pairs found", StageNumber, TotalStages));
            return (0, 0);
        }

        // Phase 2: Hash-bucket grouping
        await onProgress(new StageProgress("faq", 45, $"Clustering {allPairs.Count:N0} pairs with hash-bucket grouping", StageNumber, TotalStages));

        var clusters = GroupByHashBucket(allPairs);
        _logger.SystemInfo($"[FaqExtractor] {clusters.Count:N0} clusters formed (min size {MinClusterSize})");

        // Assign cluster IDs to pairs
        var clusterLabel = 0;
        foreach (var cluster in clusters)
        {
            foreach (var pair in cluster)
                pair.ClusterId = clusterLabel;
            clusterLabel++;
        }

        // Phase 3: Build cluster summaries
        await onProgress(new StageProgress("faq", 70, "Building cluster summaries", StageNumber, TotalStages));

        var clusterSummaries = new List<FaqCluster>();
        for (var i = 0; i < clusters.Count; i++)
        {
            var clusterPairs = clusters[i];
            var representative = SelectRepresentative(clusterPairs);

            clusterSummaries.Add(new FaqCluster
            {
                ClusterLabel = i,
                RepresentativeQuestion = representative,
                QuestionCount = clusterPairs.Count,
                SampleQuestions = clusterPairs
                    .Select(p => p.Question)
                    .Distinct()
                    .Take(MaxSampleQuestions)
                    .ToList(),
                SampleAnswers = clusterPairs
                    .Select(p => p.Answer)
                    .Distinct()
                    .Take(MaxSampleAnswers)
                    .ToList()
            });
        }

        // Phase 4: Batch insert
        await onProgress(new StageProgress("faq", 80, $"Writing {allPairs.Count:N0} pairs + {clusterSummaries.Count:N0} clusters to DB", StageNumber, TotalStages));

        await _repo.BatchInsertFaqPairsAsync(analysisId, tenantId, allPairs, ct);
        await _repo.BatchInsertFaqClustersAsync(analysisId, tenantId, clusterSummaries, ct);

        await onProgress(new StageProgress("faq", 100, $"Complete: {allPairs.Count:N0} pairs, {clusterSummaries.Count:N0} clusters", StageNumber, TotalStages));

        _logger.SystemInfo($"[FaqExtractor] Stage 5 complete: {allPairs.Count:N0} pairs, {clusterSummaries.Count:N0} clusters");
        return (allPairs.Count, clusterSummaries.Count);
    }

    /// <summary>
    /// Extract Q&A pairs from a single conversation's messages.
    /// Pattern: CUSTOMER message (looks like question) → next ME message ≥10 chars.
    /// </summary>
    private List<FaqPair> ExtractQAPairs(string conversationId, List<(string SenderType, string Text)> messages)
    {
        var pairs = new List<FaqPair>();

        for (var i = 0; i < messages.Count; i++)
        {
            var (senderType, text) = messages[i];

            // Only look at customer messages
            if (senderType != "CUSTOMER") continue;
            if (text.Length < MinQuestionLen) continue;

            // Check if it looks like a question
            var transliterated = _normalizer.TransliterateTurkish(text);
            try
            {
                if (!QuestionRegex.IsMatch(transliterated)) continue;
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.SystemWarn($"[FaqExtractor] Regex timeout checking question pattern for conversation {conversationId}, message {i}");
                continue;
            }

            // Search for next ME message with ≥10 chars as answer
            for (var j = i + 1; j < Math.Min(i + 1 + AnswerSearchWindow, messages.Count); j++)
            {
                var (answerSender, answerText) = messages[j];
                if (answerSender != "ME") continue;
                if (answerText.Length < MinAnswerLen) continue;

                pairs.Add(new FaqPair
                {
                    ConversationId = conversationId,
                    Question = text,
                    Answer = answerText
                });
                break;
            }
        }

        return pairs;
    }

    /// <summary>
    /// Group FAQ pairs using hash-bucket approach:
    /// 1. Normalize question → take first N words as bucket key
    /// 2. Within each bucket, merge items with Levenshtein distance ≤ threshold
    /// 3. Filter: minimum cluster size
    /// </summary>
    private List<List<FaqPair>> GroupByHashBucket(List<FaqPair> pairs)
    {
        // Step 1: Build buckets by first-N-word key
        var buckets = new Dictionary<string, List<FaqPair>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in pairs)
        {
            var key = GetBucketKey(pair.Question);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<FaqPair>();
                buckets[key] = bucket;
            }
            bucket.Add(pair);
        }

        // Step 2: Within each bucket, merge by Levenshtein
        var allClusters = new List<List<FaqPair>>();

        foreach (var bucket in buckets.Values)
        {
            if (bucket.Count < MinClusterSize)
                continue;

            // Cap bucket size to bound O(n*k) Levenshtein merge — excess items stay unclustered
            var capped = bucket.Count > MaxBucketSize ? bucket.GetRange(0, MaxBucketSize) : bucket;
            var subClusters = MergeByLevenshtein(capped);
            allClusters.AddRange(subClusters.Where(c => c.Count >= MinClusterSize));
        }

        return allClusters.OrderByDescending(c => c.Count).ToList();
    }

    /// <summary>
    /// Get hash-bucket key: normalize question, take first N words.
    /// </summary>
    private string GetBucketKey(string question)
    {
        var normalized = _normalizer.CleanForComparison(question);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keyWords = words.Take(BucketKeyWordCount);
        return string.Join(" ", keyWords);
    }

    /// <summary>
    /// Within a bucket, group items by Levenshtein distance.
    /// Simple greedy: assign each item to first matching cluster, or create new.
    /// </summary>
    private static List<List<FaqPair>> MergeByLevenshtein(List<FaqPair> bucket)
    {
        var clusters = new List<(string Representative, List<FaqPair> Items)>();

        foreach (var pair in bucket)
        {
            var normalizedQ = pair.Question.ToLowerInvariant();
            var shortQ = normalizedQ.Length > 80 ? normalizedQ[..80] : normalizedQ;

            var assigned = false;
            foreach (var cluster in clusters)
            {
                var shortRep = cluster.Representative.Length > 80 ? cluster.Representative[..80] : cluster.Representative;
                if (LevenshteinDistance(shortQ, shortRep) <= MaxBucketLevenshtein)
                {
                    cluster.Items.Add(pair);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                clusters.Add((normalizedQ, new List<FaqPair> { pair }));
            }
        }

        return clusters.Select(c => c.Items).ToList();
    }

    /// <summary>
    /// Select representative question: shortest question closest to median length.
    /// </summary>
    private static string SelectRepresentative(List<FaqPair> cluster)
    {
        if (cluster.Count == 0) return "";

        var medianLen = cluster
            .Select(p => p.Question.Length)
            .OrderBy(l => l)
            .ElementAt(cluster.Count / 2);

        return cluster
            .OrderBy(p => Math.Abs(p.Question.Length - medianLen))
            .First()
            .Question;
    }

    /// <summary>
    /// Compute Levenshtein edit distance between two strings.
    /// Uses two-row optimization (O(min(m,n)) space).
    /// </summary>
    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        // Early termination: if length diff > threshold, skip
        if (Math.Abs(s.Length - t.Length) > MaxBucketLevenshtein) return MaxBucketLevenshtein + 1;

        var m = s.Length;
        var n = t.Length;

        // Ensure s is shorter (for space optimization)
        if (m > n)
        {
            (s, t) = (t, s);
            (m, n) = (n, m);
        }

        var prev = new int[m + 1];
        var curr = new int[m + 1];

        for (var i = 0; i <= m; i++) prev[i] = i;

        for (var j = 1; j <= n; j++)
        {
            curr[0] = j;
            for (var i = 1; i <= m; i++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                curr[i] = Math.Min(Math.Min(curr[i - 1] + 1, prev[i] + 1), prev[i - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
}
