using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;
using Chatinbox.WhatsAppAnalytics.Services.Pipeline;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Chatinbox.WhatsAppAnalytics.Services.Benchmark;

/// <summary>
/// Orchestrates the LLM benchmark pipeline:
/// Stage 1: Sample threads from MSSQL
/// Stage 2: PII masking
/// Stage 3: Keyword baseline (DetectOutcome)
/// Stage 4: LLM classification (multiple models)
/// Stage 5: Store results
/// Stage 6: Complete job
/// </summary>
public sealed class BenchmarkOrchestrator
{
    private readonly BenchmarkRepository _repo;
    private readonly MssqlReaderService _mssqlReader;
    private readonly PiiMasker _piiMasker;
    private readonly OutcomeClassifierService _classifier;
    private readonly ThreaderService _threaderService;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonLinesLogger _logger;
    private readonly int _delayMs;
    private readonly int _maxTextLength;

    // Model keys (matching DI keyed service registration)
    private static readonly string[] AllModelKeys =
        { "claude_haiku", "claude_sonnet", "gemini_flash", "gemini_pro", "gemini_3_flash", "tiered" };

    public BenchmarkOrchestrator(
        BenchmarkRepository repo,
        MssqlReaderService mssqlReader,
        PiiMasker piiMasker,
        OutcomeClassifierService classifier,
        ThreaderService threaderService,
        IServiceProvider serviceProvider,
        JsonLinesLogger logger,
        int delayBetweenCallsMs = 500,
        int maxThreadTextLength = 4000)
    {
        _repo = repo;
        _mssqlReader = mssqlReader;
        _piiMasker = piiMasker;
        _classifier = classifier;
        _threaderService = threaderService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _delayMs = delayBetweenCallsMs;
        _maxTextLength = maxThreadTextLength;
    }

    public async Task RunAsync(BenchmarkProcessJob job, CancellationToken ct)
    {
        var benchmarkId = job.BenchmarkId;
        var rid = $"bench-{benchmarkId}";
        _logger.StepInfo($"[BenchmarkOrchestrator] Starting benchmark {benchmarkId}: db={job.DatabaseName}, sample={job.SampleSize}", rid);

        try
        {
            // Stage 1: Sample threads from MSSQL
            await UpdateProgress(benchmarkId, "sampling", 0, "Sampling threads from MSSQL...", ct);
            var threads = await SampleThreadsAsync(job, ct);
            _logger.StepInfo($"[BenchmarkOrchestrator] Sampled {threads.Count} threads from {job.DatabaseName}", rid);

            if (threads.Count == 0)
            {
                await _repo.CompleteJobAsync(benchmarkId, 0, "{\"message\": \"No threads found matching criteria\"}", ct);
                return;
            }

            // Stage 2: PII masking + formatting
            await UpdateProgress(benchmarkId, "masking", 15, $"Masking PII for {threads.Count} threads...", ct);
            foreach (var thread in threads)
            {
                var formatted = OutcomeClassifierService.FormatThread(thread, _maxTextLength);
                thread.MaskedText = _piiMasker.Mask(formatted);
            }

            // Stage 3: Keyword baseline
            await UpdateProgress(benchmarkId, "keyword", 25, "Running keyword baseline...", ct);
            var keywordLabels = RunKeywordBaseline(threads);

            // Stage 4: LLM classification
            var modelKeys = job.Models.Length > 0
                ? job.Models.Where(m => AllModelKeys.Contains(m)).ToArray()
                : AllModelKeys;

            var modelResults = new Dictionary<string, List<LlmClassification?>>();

            var modelsDone = 0;
            foreach (var modelKey in modelKeys)
            {
                var client = _serviceProvider.GetKeyedService<ILlmClient>(modelKey);
                if (client == null || !client.IsAvailable)
                {
                    _logger.SystemWarn($"[BenchmarkOrchestrator] Model {modelKey} not available, skipping");
                    modelResults[modelKey] = threads.Select(_ => (LlmClassification?)null).ToList();
                    modelsDone++;
                    continue;
                }

                var pctBase = 30 + (modelsDone * 60 / modelKeys.Length);
                await UpdateProgress(benchmarkId, "classifying", pctBase,
                    $"Classifying with {client.ModelName} ({modelsDone + 1}/{modelKeys.Length})...", ct);

                var results = new List<LlmClassification?>();
                var consecutiveErrors = 0;

                for (var i = 0; i < threads.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (consecutiveErrors >= 5)
                    {
                        _logger.SystemWarn($"[BenchmarkOrchestrator] {modelKey}: 5+ consecutive errors, skipping remaining {threads.Count - i} threads");
                        results.AddRange(Enumerable.Repeat((LlmClassification?)null, threads.Count - i));
                        break;
                    }

                    var classification = await _classifier.ClassifyAsync(client, threads[i], ct);
                    results.Add(classification);

                    if (classification == null) consecutiveErrors++;
                    else consecutiveErrors = 0;

                    // Rate limiting delay
                    if (i < threads.Count - 1)
                        await Task.Delay(_delayMs, ct);

                    // Update progress periodically
                    if (i % 10 == 0)
                    {
                        var threadPct = (int)(60.0 * (i + 1) / threads.Count / modelKeys.Length);
                        await UpdateProgress(benchmarkId, "classifying", pctBase + threadPct,
                            $"{client.ModelName}: {i + 1}/{threads.Count} threads classified", ct);
                    }
                }

                modelResults[modelKey] = results;
                modelsDone++;

                var classified = results.Count(r => r != null);
                _logger.StepInfo($"[BenchmarkOrchestrator] {modelKey}: {classified}/{threads.Count} classified", rid);
            }

            // Stage 5: Build and store results
            await UpdateProgress(benchmarkId, "storing", 92, "Storing results...", ct);
            var benchmarkResults = BuildResults(benchmarkId, job.TenantId, threads, keywordLabels, modelResults, modelKeys);
            await _repo.InsertResultsBatchAsync(benchmarkResults, ct);

            // Stage 6: Complete
            var summary = BuildSummary(threads.Count, modelKeys, modelResults, keywordLabels);
            await _repo.CompleteJobAsync(benchmarkId, threads.Count, summary, ct);

            _logger.StepInfo($"[BenchmarkOrchestrator] Benchmark {benchmarkId} completed: {threads.Count} threads, {modelKeys.Length} models", rid);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Let the processing service handle shutdown
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.WABenchmarkMssqlError}] Benchmark {benchmarkId} connection failed: {ex.Message}");
            await _repo.FailJobAsync(benchmarkId, $"[{ErrorCodes.WABenchmarkMssqlError}] {ex.Message}", CancellationToken.None);
        }
        catch (SqlException ex)
        {
            _logger.SystemError($"[{ErrorCodes.WABenchmarkMssqlError}] Benchmark {benchmarkId} SQL error: {ex.Message}");
            await _repo.FailJobAsync(benchmarkId, $"[{ErrorCodes.WABenchmarkMssqlError}] {ex.Message}", CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError($"[{ErrorCodes.WABenchmarkInvalidConfig}] Benchmark {benchmarkId} failed: {ex.Message}");
            await _repo.FailJobAsync(benchmarkId, $"[{ErrorCodes.WABenchmarkInvalidConfig}] {ex.Message}", CancellationToken.None);
        }
    }

    // ============================================================
    // Stage 1: MSSQL Thread Sampling
    // ============================================================

    private async Task<List<SampledThread>> SampleThreadsAsync(BenchmarkProcessJob job, CancellationToken ct)
    {
        // Use MssqlReaderService's connection pattern
        var connStr = BuildMssqlConnectionString(job.DatabaseName);
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        var candidates = new List<(string conversationId, int msgCount)>();

        if (job.ConversationIds is { Length: > 0 })
        {
            // Explicit conversation IDs provided — skip random sampling
            foreach (var id in job.ConversationIds)
                candidates.Add((id, 0));

            _logger.SystemInfo($"[BenchmarkOrchestrator] Using {candidates.Count} explicit conversation IDs (skip random sampling)");
        }
        else
        {
            // Step 1: Find random conversation IDs with message count in range
            var candidateQuery = @"
                SELECT TOP (@sampleSize) sub.conversation_id, sub.msg_count
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
                      AND C.InstanceID = @instanceId
                    GROUP BY C.CustomerPhoneNumber
                    HAVING COUNT(*) >= @minMsgs AND COUNT(*) <= @maxMsgs
                ) sub
                ORDER BY NEWID()";

            await using var candidateCmd = new SqlCommand(candidateQuery, conn);
            candidateCmd.CommandTimeout = 120;
            candidateCmd.Parameters.AddWithValue("@sampleSize", job.SampleSize);
            candidateCmd.Parameters.AddWithValue("@instanceId", job.InstanceId ?? 0);
            candidateCmd.Parameters.AddWithValue("@minMsgs", job.MinMessages);
            candidateCmd.Parameters.AddWithValue("@maxMsgs", job.MaxMessages);

            await using (var reader = await candidateCmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    candidates.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }
        }

        if (candidates.Count == 0) return new List<SampledThread>();

        _logger.SystemInfo($"[BenchmarkOrchestrator] Found {candidates.Count} candidate conversations");

        // Step 2: Read messages for each conversation
        var threads = new List<SampledThread>();
        var messageQuery = @"
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
              AND C.InstanceID = @instanceId
            ORDER BY ISNULL(CM.SentTime, CM.CreateDate)";

        foreach (var (convId, _) in candidates)
        {
            ct.ThrowIfCancellationRequested();

            await using var msgCmd = new SqlCommand(messageQuery, conn);
            msgCmd.CommandTimeout = 30;
            msgCmd.Parameters.AddWithValue("@phone", convId);
            msgCmd.Parameters.AddWithValue("@instanceId", job.InstanceId ?? 0);

            var thread = new SampledThread { ConversationId = convId };
            await using var msgReader = await msgCmd.ExecuteReaderAsync(ct);

            while (await msgReader.ReadAsync(ct))
            {
                thread.Messages.Add(new ThreadMessage
                {
                    Text = msgReader.IsDBNull(0) ? "" : msgReader.GetString(0),
                    SenderType = msgReader.GetString(1),
                    AgentName = msgReader.IsDBNull(2) ? "" : msgReader.GetString(2),
                    Timestamp = msgReader.GetDateTime(3)
                });
            }

            if (thread.Messages.Count > 0)
                threads.Add(thread);
        }

        return threads;
    }

    private string BuildMssqlConnectionString(string database) =>
        _mssqlReader.BuildConnectionString(database);

    // ============================================================
    // Stage 3: Keyword Baseline
    // ============================================================

    private Dictionary<string, string> RunKeywordBaseline(List<SampledThread> threads)
    {
        var labels = new Dictionary<string, string>();
        foreach (var thread in threads)
        {
            var messages = thread.Messages.Select(m =>
                (m.Text, m.SenderType, m.AgentName, m.Timestamp)).ToList();

            var label = _threaderService.DetectOutcome(messages);

            // Map to v0.2 taxonomy
            label = label switch
            {
                "return" => "return_or_complaint",
                "complaint" => "return_or_complaint",
                _ => label
            };

            labels[thread.ConversationId] = label;
        }
        return labels;
    }

    // ============================================================
    // Stage 5: Build Results
    // ============================================================

    private static List<BenchmarkResult> BuildResults(int benchmarkId, int tenantId,
        List<SampledThread> threads, Dictionary<string, string> keywordLabels,
        Dictionary<string, List<LlmClassification?>> modelResults, string[] modelKeys)
    {
        var results = new List<BenchmarkResult>();

        for (var i = 0; i < threads.Count; i++)
        {
            var thread = threads[i];
            var result = new BenchmarkResult
            {
                BenchmarkId = benchmarkId,
                TenantId = tenantId,
                ConversationId = thread.ConversationId,
                MessageCount = thread.MessageCount,
                ThreadTextMasked = thread.MaskedText.Length > 2000
                    ? thread.MaskedText[..2000] + "...[truncated]"
                    : thread.MaskedText,
                KeywordLabel = keywordLabels.GetValueOrDefault(thread.ConversationId)
            };

            // Map model results to flat columns
            foreach (var key in modelKeys)
            {
                if (!modelResults.ContainsKey(key)) continue;
                var cls = modelResults[key][i];
                switch (key)
                {
                    case "claude_haiku":
                        result.ClaudeHaikuLabel = cls?.Label;
                        result.ClaudeHaikuConfidence = cls?.Confidence;
                        result.ClaudeHaikuEvidence = cls?.Evidence;
                        break;
                    case "claude_sonnet":
                        result.ClaudeSonnetLabel = cls?.Label;
                        result.ClaudeSonnetConfidence = cls?.Confidence;
                        result.ClaudeSonnetEvidence = cls?.Evidence;
                        break;
                    case "gemini_flash":
                        result.GeminiFlashLabel = cls?.Label;
                        result.GeminiFlashConfidence = cls?.Confidence;
                        result.GeminiFlashEvidence = cls?.Evidence;
                        break;
                    case "gemini_pro":
                        result.GeminiProLabel = cls?.Label;
                        result.GeminiProConfidence = cls?.Confidence;
                        result.GeminiProEvidence = cls?.Evidence;
                        break;
                    case "gemini_3_flash":
                        result.Gemini3FlashLabel = cls?.Label;
                        result.Gemini3FlashConfidence = cls?.Confidence;
                        result.Gemini3FlashEvidence = cls?.Evidence;
                        break;
                    case "tiered":
                        result.TieredLabel = cls?.Label;
                        result.TieredConfidence = cls?.Confidence;
                        result.TieredEvidence = cls?.Evidence;
                        result.HasOffer = cls?.HasOffer;
                        break;
                }
            }

            results.Add(result);
        }

        return results;
    }

    // ============================================================
    // Summary
    // ============================================================

    private static string BuildSummary(int threadCount, string[] modelKeys,
        Dictionary<string, List<LlmClassification?>> modelResults, Dictionary<string, string> keywordLabels)
    {
        var summary = new Dictionary<string, object>
        {
            ["thread_count"] = threadCount,
            ["models"] = modelKeys,
            ["keyword_distribution"] = MetricsCalculator.ComputeDistribution(
                keywordLabels.Values.Select(v => (string?)v).ToList())
        };

        var modelDistributions = new Dictionary<string, object>();
        foreach (var key in modelKeys)
        {
            if (!modelResults.ContainsKey(key)) continue;
            var labels = modelResults[key].Select(r => r?.Label).ToList();
            var classified = labels.Count(l => l != null);
            modelDistributions[key] = new
            {
                classified,
                failed = threadCount - classified,
                distribution = MetricsCalculator.ComputeDistribution(labels)
            };
        }
        summary["model_results"] = modelDistributions;

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
    }

    private async Task UpdateProgress(int benchmarkId, string stage, int percent, string message, CancellationToken ct)
    {
        var progress = JsonSerializer.Serialize(new { stage, percent, message });
        await _repo.UpdateJobStatusAsync(benchmarkId, stage == "storing" || stage == "completed" ? "classifying" : stage, progress, ct);
    }
}
