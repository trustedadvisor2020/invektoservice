using System.Collections.Concurrent;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Invekto.WhatsAppAnalytics.Services.Benchmark;
using Microsoft.Data.SqlClient;

namespace Invekto.WhatsAppAnalytics.Services;

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

    public BatchClassificationService(
        ConversationOutcomeRepository outcomeRepo,
        MssqlReaderService mssqlReader,
        PiiMasker piiMasker,
        OutcomeClassifierService classifier,
        IServiceProvider sp,
        JsonLinesLogger logger,
        int delayMs = 500,
        int maxThreadTextLen = 4000,
        int minMessages = 6,
        int maxMessages = 200)
    {
        _outcomeRepo = outcomeRepo;
        _mssqlReader = mssqlReader;
        _piiMasker = piiMasker;
        _classifier = classifier;
        _sp = sp;
        _logger = logger;
        _delayMs = delayMs;
        _maxThreadTextLen = maxThreadTextLen;
        _minMessages = minMessages;
        _maxMessages = maxMessages;
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

        for (var i = 0; i < threads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (consecutiveErrors >= 5)
            {
                _logger.SystemWarn($"[BatchClassification] 5+ consecutive errors, stopping at {i}/{threads.Count}");
                break;
            }

            var thread = threads[i];
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
                    ModelVersion = $"tiered-v0.6"
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
                    ProgressJson("classifying", pct, $"tiered: {i + 1}/{threads.Count} threads classified"), ct);
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

        _logger.StepInfo($"[BatchClassification] Job {job.BatchJobId} completed: {outcomes.Count} classified, {errorCount} errors", rid);
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
    /// Load full conversation threads from MSSQL and apply PII masking.
    /// Reuses the same message query pattern as BenchmarkOrchestrator.
    /// </summary>
    private async Task<List<SampledThread>> LoadThreadsAsync(string databaseName, int? instanceId,
        List<(string conversationId, int msgCount)> candidates, CancellationToken ct)
    {
        var connStr = _mssqlReader.BuildConnectionString(databaseName);
        var threads = new List<SampledThread>();

        var instanceFilter = instanceId.HasValue
            ? "AND C.InstanceID = @instanceId"
            : "";

        foreach (var (convId, _) in candidates)
        {
            ct.ThrowIfCancellationRequested();

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

            if (messages.Count < _minMessages) continue;

            var thread = new SampledThread
            {
                ConversationId = convId,
                Messages = messages
            };

            // PII mask + format
            var formatted = OutcomeClassifierService.FormatThread(thread, _maxThreadTextLen);
            thread.MaskedText = _piiMasker.Mask(formatted);

            threads.Add(thread);
        }

        return threads;
    }

    private static string ProgressJson(string stage, int percent, string message) =>
        JsonSerializer.Serialize(new { stage, percent, message });
}
