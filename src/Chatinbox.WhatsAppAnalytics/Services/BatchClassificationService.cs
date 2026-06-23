using System.Collections.Concurrent;
using System.Text.Json;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;
using System.Text.RegularExpressions;
using Chatinbox.WhatsAppAnalytics.Services.Benchmark;
using Microsoft.Data.SqlClient;

namespace Chatinbox.WhatsAppAnalytics.Services;

/// <summary>
/// Background service for batch conversation classification.
/// Follows BenchmarkProcessingService pattern: ConcurrentQueue + SemaphoreSlim.
/// Reuses MssqlReaderService, PiiMasker, OutcomeClassifierService, TieredClassifierService.
/// </summary>
public sealed class BatchClassificationService : BackgroundService
{
    private readonly ConcurrentQueue<BatchProcessJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ConversationOutcomeRepository _outcomeRepo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly PiiMasker _piiMasker;
    private readonly OutcomeClassifierService _classifier;
    private readonly IServiceProvider _sp;
    private readonly JsonLinesLogger _logger;
    private readonly int _delayMs;
    private readonly int _maxThreadTextLen;
    private readonly int _minMessages;
    private readonly int _maxMessages;
    private readonly TextNormalizer _normalizer;
    private readonly int _maxConcurrency;

    // RI-8.1: High-confidence keyword patterns for pre-filter (skip LLM)
    // Subset from ThreaderService.DetectOutcome — only unambiguous outcomes
    private static readonly Regex[] KeywordSalePatterns =
    [
        new(@"siparisiniz.*olusturulmustur", RegexOptions.Compiled),
        new(@"siparisiniz.*onaylandi", RegexOptions.Compiled),
        new(@"kargo.*takip\s*(no|numar)", RegexOptions.Compiled),
        new(@"havale.*yapildi", RegexOptions.Compiled),
        new(@"eft.*yapildi", RegexOptions.Compiled),
        new(@"odeme.*alindi", RegexOptions.Compiled),
        new(@"odemeniz.*onaylandi", RegexOptions.Compiled),
    ];

    private static readonly Regex[] KeywordReturnPatterns =
    [
        new(@"iade.*taleb", RegexOptions.Compiled),
        new(@"degisim.*taleb", RegexOptions.Compiled),
        new(@"geri.*gonder", RegexOptions.Compiled),
    ];

    private static readonly Regex[] KeywordComplaintPatterns =
    [
        new(@"memnun\s*degil", RegexOptions.Compiled),
        new(@"yanlis.*geldi", RegexOptions.Compiled),
        new(@"bozuk.*geldi", RegexOptions.Compiled),
        new(@"berbat|rezalet", RegexOptions.Compiled),
    ];

    public BatchClassificationService(
        ConversationOutcomeRepository outcomeRepo,
        MssqlReaderService mssqlReader,
        PiiMasker piiMasker,
        OutcomeClassifierService classifier,
        TextNormalizer normalizer,
        IServiceProvider sp,
        JsonLinesLogger logger,
        int delayMs = 500,
        int maxThreadTextLen = 4000,
        int minMessages = 6,
        int maxMessages = 200,
        int maxConcurrency = 4)
    {
        _outcomeRepo = outcomeRepo;
        _mssqlReader = mssqlReader;
        _piiMasker = piiMasker;
        _classifier = classifier;
        _normalizer = normalizer;
        _sp = sp;
        _logger = logger;
        _delayMs = delayMs;
        _maxThreadTextLen = maxThreadTextLen;
        _minMessages = minMessages;
        _maxMessages = maxMessages;
        _maxConcurrency = maxConcurrency;
    }

    public void Enqueue(BatchProcessJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.SystemInfo("[BatchClassification] Background worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(stoppingToken);
            if (_queue.TryDequeue(out var job))
            {
                try
                {
                    await RunAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.SystemError($"[BatchClassification] Job {job.BatchJobId} failed: {ex.Message}");
                    await _outcomeRepo.FailBatchJobAsync(job.BatchJobId, ex.Message, stoppingToken);
                }
            }
        }
    }

    private async Task RunAsync(BatchProcessJob job, CancellationToken ct)
    {
        var rid = $"batch-{job.BatchJobId}";
        _logger.StepInfo($"[BatchClassification] Starting job {job.BatchJobId}: {job.DatabaseName} inst={job.InstanceId}", rid);

        await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "sampling",
            ProgressJson("sampling", 0, "Sampling conversations from MSSQL..."), ct);

        // Stage 1: Get candidate conversations from MSSQL (last N days)
        var candidates = await SampleCandidatesAsync(job, ct);
        _logger.StepInfo($"[BatchClassification] {candidates.Count} candidates from MSSQL", rid);

        // Stage 2: Filter out already-classified conversations
        var alreadyClassified = await _outcomeRepo.GetClassifiedConversationIdsAsync(job.TenantId, ct);
        var newCandidates = candidates.Where(c => !alreadyClassified.Contains(c.conversationId)).ToList();

        await _outcomeRepo.UpdateBatchCountsAsync(job.BatchJobId,
            totalCandidates: candidates.Count,
            alreadyClassified: candidates.Count - newCandidates.Count, ct: ct);

        _logger.StepInfo($"[BatchClassification] {newCandidates.Count} new (skipped {candidates.Count - newCandidates.Count} already classified)", rid);

        if (newCandidates.Count == 0)
        {
            await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "completed",
                ProgressJson("completed", 100, "No new conversations to classify"), ct);
            await _outcomeRepo.CompleteBatchJobAsync(job.BatchJobId, ct);
            return;
        }

        // Apply max threads limit
        if (job.MaxThreads.HasValue && newCandidates.Count > job.MaxThreads.Value)
            newCandidates = newCandidates.Take(job.MaxThreads.Value).ToList();

        // Stage 3: Fetch full threads + PII mask
        await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "loading",
            ProgressJson("loading", 15, $"Loading {newCandidates.Count} conversation threads..."), ct);

        var threads = await LoadThreadsAsync(job.DatabaseName, job.InstanceId, newCandidates, ct);
        _logger.StepInfo($"[BatchClassification] {threads.Count} threads loaded and masked", rid);

        // Stage 4: Classify with tiered model
        await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "classifying",
            ProgressJson("classifying", 30, $"Classifying 0/{threads.Count} threads..."), ct);

        var tieredClient = _sp.GetRequiredKeyedService<ILlmClient>("tiered");
        var outcomes = new List<ConversationOutcome>();
        var errorCount = 0;
        var consecutiveErrors = 0;
        var keywordCount = 0;

        for (var i = 0; i < threads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (consecutiveErrors >= 5)
            {
                _logger.SystemWarn($"[BatchClassification] 5+ consecutive errors, stopping at {i}/{threads.Count}");
                break;
            }

            var thread = threads[i];

            // RI-8.1: Keyword pre-filter — skip LLM for high-confidence keyword matches
            var keywordLabel = TryKeywordClassify(thread);
            if (keywordLabel != null)
            {
                keywordCount++;
                outcomes.Add(new ConversationOutcome
                {
                    TenantId = job.TenantId,
                    DatabaseName = job.DatabaseName,
                    InstanceId = job.InstanceId,
                    ConversationId = thread.ConversationId,
                    Sector = job.Sector,
                    OutcomeLabel = keywordLabel,
                    Confidence = 0.95f,
                    HasOffer = keywordLabel == "sale",
                    Evidence = "keyword-match",
                    ModelVersion = "keyword-v1"
                });
                if (i % 10 == 0)
                {
                    var pct = 30 + (int)(60.0 * (i + 1) / threads.Count);
                    await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "classifying",
                        ProgressJson("classifying", pct, $"kw:{keywordCount} llm:{i + 1 - keywordCount}/{threads.Count}"), ct);
                }
                continue;
            }

            var result = await _classifier.ClassifyAsync(tieredClient, thread, ct);

            if (result != null)
            {
                consecutiveErrors = 0;
                outcomes.Add(new ConversationOutcome
                {
                    TenantId = job.TenantId,
                    DatabaseName = job.DatabaseName,
                    InstanceId = job.InstanceId,
                    ConversationId = thread.ConversationId,
                    Sector = job.Sector,
                    OutcomeLabel = result.Label,
                    Confidence = result.Confidence,
                    HasOffer = result.HasOffer,
                    Evidence = result.Evidence,
                    ModelVersion = "tiered-v0.6"
                });
            }
            else
            {
                consecutiveErrors++;
                errorCount++;
            }

            if (i < threads.Count - 1)
                await Task.Delay(_delayMs, ct);

            // Update progress every 10 threads
            if (i % 10 == 0)
            {
                var pct = 30 + (int)(60.0 * (i + 1) / threads.Count);
                await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "classifying",
                    ProgressJson("classifying", pct, $"kw:{keywordCount} llm:{i + 1 - keywordCount}/{threads.Count}"), ct);
                await _outcomeRepo.UpdateBatchCountsAsync(job.BatchJobId,
                    classifiedCount: outcomes.Count, errorCount: errorCount, ct: ct);
            }
        }

        // Stage 5: Store results
        await _outcomeRepo.UpdateBatchStatusAsync(job.BatchJobId, "storing",
            ProgressJson("storing", 92, $"Storing {outcomes.Count} outcomes..."), ct);

        await _outcomeRepo.UpsertOutcomesAsync(outcomes, ct);

        // Stage 6: Complete
        await _outcomeRepo.UpdateBatchCountsAsync(job.BatchJobId,
            classifiedCount: outcomes.Count, errorCount: errorCount, ct: ct);
        await _outcomeRepo.CompleteBatchJobAsync(job.BatchJobId, ct);

        _logger.StepInfo($"[BatchClassification] Job {job.BatchJobId} completed: {outcomes.Count} classified ({keywordCount} keyword, {outcomes.Count - keywordCount} LLM), {errorCount} errors", rid);
    }

    /// <summary>
    /// Sample candidate conversations from MSSQL with lookback filter.
    /// Returns conversation IDs (CustomerPhoneNumber) that have messages in the last N days.
    /// </summary>
    private async Task<List<(string conversationId, int msgCount)>> SampleCandidatesAsync(
        BatchProcessJob job, CancellationToken ct)
    {
        var connStr = _mssqlReader.BuildConnectionString(job.DatabaseName);
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 120;

        var instanceFilter = job.InstanceId.HasValue
            ? "AND C.InstanceID = @instanceId"
            : "";

        cmd.CommandText = $@"
            SELECT sub.conversation_id, sub.msg_count
            FROM (
                SELECT
                    C.CustomerPhoneNumber AS conversation_id,
                    COUNT(*) AS msg_count
                FROM ChatMessages CM WITH (NOLOCK)
                INNER JOIN Chats C WITH (NOLOCK) ON CM.ChatID = C.ID
                WHERE CM.MessageType = 1
                  AND CM.SystemMessageType IS NULL
                  AND C.CustomerPhoneNumber IS NOT NULL
                  AND C.IsGroup = 0
                  AND LEN(CM.Body) > 0
                  AND CM.Body NOT IN (N'Dosya İndirilememiştir', N'Media could not be downloaded')
                  {instanceFilter}
                  AND ISNULL(CM.SentTime, CM.CreateDate) >= DATEADD(DAY, -@lookbackDays, GETDATE())
                GROUP BY C.CustomerPhoneNumber
                HAVING COUNT(*) >= @minMsgs AND COUNT(*) <= @maxMsgs
            ) sub
            ORDER BY sub.msg_count DESC";

        if (job.InstanceId.HasValue)
            cmd.Parameters.AddWithValue("@instanceId", job.InstanceId.Value);
        cmd.Parameters.AddWithValue("@lookbackDays", job.LookbackDays);
        cmd.Parameters.AddWithValue("@minMsgs", _minMessages);
        cmd.Parameters.AddWithValue("@maxMsgs", _maxMessages);

        var result = new List<(string, int)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    /// <summary>
    /// RI-8.4: Load full conversation threads from MSSQL with parallel reads.
    /// Uses bounded concurrency to avoid overwhelming the SQL Server.
    /// </summary>
    private async Task<List<SampledThread>> LoadThreadsAsync(string databaseName, int? instanceId,
        List<(string conversationId, int msgCount)> candidates, CancellationToken ct)
    {
        var connStr = _mssqlReader.BuildConnectionString(databaseName);
        var results = new ConcurrentBag<SampledThread>();

        await Parallel.ForEachAsync(candidates,
            new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency, CancellationToken = ct },
            async (candidate, token) =>
            {
                var thread = await LoadSingleThreadAsync(connStr, instanceId, candidate.conversationId, token);
                if (thread != null)
                    results.Add(thread);
            });

        return results.ToList();
    }

    private async Task<SampledThread?> LoadSingleThreadAsync(
        string connStr, int? instanceId, string convId, CancellationToken ct)
    {
        var instanceFilter = instanceId.HasValue
            ? "AND C.InstanceID = @instanceId"
            : "";

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 30;
        cmd.CommandText = $@"
            SELECT
                CM.Body,
                CASE WHEN CM.FromMe = 0 THEN 'CUSTOMER' ELSE 'ME' END AS sender_type,
                ISNULL(U.Name + ' ' + U.Surname, '') AS agent_name,
                ISNULL(CM.SentTime, CM.CreateDate) AS ts
            FROM ChatMessages CM WITH (NOLOCK)
            INNER JOIN Chats C WITH (NOLOCK) ON CM.ChatID = C.ID
            LEFT JOIN Users U WITH (NOLOCK) ON CM.UserID = U.ID
            WHERE CM.MessageType = 1
              AND CM.SystemMessageType IS NULL
              AND C.CustomerPhoneNumber = @phone
              AND C.IsGroup = 0
              AND LEN(CM.Body) > 0
              AND CM.Body NOT IN (N'Dosya İndirilememiştir', N'Media could not be downloaded')
              {instanceFilter}
            ORDER BY ISNULL(CM.SentTime, CM.CreateDate)";

        cmd.Parameters.AddWithValue("@phone", convId);
        if (instanceId.HasValue)
            cmd.Parameters.AddWithValue("@instanceId", instanceId.Value);

        var messages = new List<ThreadMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new ThreadMessage
            {
                Text = reader.GetString(0),
                SenderType = reader.GetString(1),
                AgentName = reader.GetString(2),
                Timestamp = reader.GetDateTime(3)
            });
        }

        if (messages.Count < _minMessages) return null;

        var thread = new SampledThread
        {
            ConversationId = convId,
            Messages = messages
        };

        // PII mask + format
        var formatted = OutcomeClassifierService.FormatThread(thread, _maxThreadTextLen);
        thread.MaskedText = _piiMasker.Mask(formatted);

        return thread;
    }

    /// <summary>
    /// RI-8.1: Try keyword-based classification before LLM.
    /// Returns outcome label for high-confidence matches, null otherwise.
    /// Maps to 7-label taxonomy (return/complaint → return_or_complaint).
    /// </summary>
    private string? TryKeywordClassify(SampledThread thread)
    {
        var agentTexts = thread.Messages
            .Where(m => m.SenderType == "ME" && !string.IsNullOrEmpty(m.Text))
            .Select(m => _normalizer.TransliterateTurkish(m.Text))
            .ToList();
        var allTexts = thread.Messages
            .Where(m => !string.IsNullOrEmpty(m.Text))
            .Select(m => _normalizer.TransliterateTurkish(m.Text))
            .ToList();

        // Priority 1: Sale (agent messages only — payment/shipment confirmation)
        foreach (var text in agentTexts)
            if (KeywordSalePatterns.Any(p => p.IsMatch(text)))
                return "sale";

        // Priority 2: Return (any message)
        foreach (var text in allTexts)
            if (KeywordReturnPatterns.Any(p => p.IsMatch(text)))
                return "return_or_complaint";

        // Priority 3: Complaint (any message)
        foreach (var text in allTexts)
            if (KeywordComplaintPatterns.Any(p => p.IsMatch(text)))
                return "return_or_complaint";

        return null; // No high-confidence match → needs LLM
    }

    private static string ProgressJson(string stage, int percent, string message) =>
        JsonSerializer.Serialize(new { stage, percent, message });
}
