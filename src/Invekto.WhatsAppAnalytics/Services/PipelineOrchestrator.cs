using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Invekto.WhatsAppAnalytics.Services.Pipeline;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Orchestrates pipeline stages 1-7 sequentially.
/// Phase A: stages 1-3 (cleaning → threading → stats).
/// Phase B (PKT-4): stages 4-7 (intents → FAQ → sentiment → products).
/// Updates wa_analyses status and stage_progress after each stage.
/// Stage failures in 4-7 log a warning and continue to next stage (partial NLP results preserved).
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly AnalyticsRepository _repo;
    private readonly CleanerService _cleaner;
    private readonly ThreaderService _threader;
    private readonly StatsService _stats;
    private readonly IntentClassifierService _intentClassifier;
    private readonly FaqExtractorService _faqExtractor;
    private readonly SentimentAnalyzerService _sentimentAnalyzer;
    private readonly ProductAnalyzerService _productAnalyzer;
    private readonly JsonLinesLogger _logger;

    public PipelineOrchestrator(
        AnalyticsRepository repo,
        CleanerService cleaner,
        ThreaderService threader,
        StatsService stats,
        IntentClassifierService intentClassifier,
        FaqExtractorService faqExtractor,
        SentimentAnalyzerService sentimentAnalyzer,
        ProductAnalyzerService productAnalyzer,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _cleaner = cleaner;
        _threader = threader;
        _stats = stats;
        _intentClassifier = intentClassifier;
        _faqExtractor = faqExtractor;
        _sentimentAnalyzer = sentimentAnalyzer;
        _productAnalyzer = productAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Run the full pipeline for an analysis job.
    /// Updates status: cleaning → threading → stats → intents → faq → sentiment → products → completed (or error).
    /// </summary>
    public async Task RunAsync(AnalysisProcessJob job, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var analysisId = job.AnalysisId;
        var tenantId = job.TenantId;

        _logger.SystemInfo($"[PipelineOrchestrator] Starting pipeline for analysis {analysisId}, tenant {tenantId}");

        // Progress callback: update wa_analyses.stage_progress
        async Task OnProgress(StageProgress progress)
        {
            await _repo.UpdateAnalysisStatusAsync(analysisId, progress.Stage, progress.ToJson(), ct);
        }

        // ============================================================
        // Stage 1: Cleaning (CSV or MSSQL source)
        // ============================================================
        await _repo.UpdateAnalysisStatusAsync(analysisId, "cleaning", null, ct);

        int messageCount;
        if (job.SourceType == "mssql" && job.MssqlDatabase != null && job.MssqlInstanceId.HasValue)
        {
            messageCount = await _cleaner.RunFromMssqlAsync(
                analysisId, tenantId, job.MssqlDatabase, job.MssqlInstanceId.Value,
                OnProgress, ct);
        }
        else
        {
            messageCount = await _cleaner.RunAsync(
                analysisId, tenantId, job.FilePath, job.Delimiter,
                OnProgress, ct);
        }

        _logger.SystemInfo($"[PipelineOrchestrator] Stage 1 complete: {messageCount:N0} messages inserted (source={job.SourceType})");

        if (messageCount == 0)
        {
            await _repo.FailAnalysisAsync(analysisId, $"No valid messages found in {job.SourceType} source");
            return;
        }

        // ============================================================
        // Stage 2: Threading
        // ============================================================
        await _repo.UpdateAnalysisStatusAsync(analysisId, "threading", null, ct);

        var conversationCount = await _threader.RunAsync(
            analysisId, tenantId, OnProgress, ct);

        _logger.SystemInfo($"[PipelineOrchestrator] Stage 2 complete: {conversationCount:N0} conversations");

        // ============================================================
        // Stage 3: Stats
        // ============================================================
        await _repo.UpdateAnalysisStatusAsync(analysisId, "stats", null, ct);

        var analysis = await _repo.GetAnalysisAsync(tenantId, analysisId, ct);
        var configJson = analysis?.ConfigJson;

        await _stats.RunAsync(analysisId, tenantId, configJson, OnProgress, ct);

        // ============================================================
        // Stage 4: Intent Classification (NLP)
        // ============================================================
        await RunNlpStageAsync("intents", analysisId, async () =>
        {
            await _repo.UpdateAnalysisStatusAsync(analysisId, "intents", null, ct);
            var intentCount = await _intentClassifier.RunAsync(analysisId, tenantId, OnProgress, ct);
            _logger.SystemInfo($"[PipelineOrchestrator] Stage 4 complete: {intentCount:N0} intents classified");
        }, ct);

        // ============================================================
        // Stage 5: FAQ Extraction
        // ============================================================
        await RunNlpStageAsync("faq", analysisId, async () =>
        {
            await _repo.UpdateAnalysisStatusAsync(analysisId, "faq", null, ct);
            var (pairCount, clusterCount) = await _faqExtractor.RunAsync(analysisId, tenantId, OnProgress, ct);
            _logger.SystemInfo($"[PipelineOrchestrator] Stage 5 complete: {pairCount:N0} FAQ pairs, {clusterCount:N0} clusters");
        }, ct);

        // ============================================================
        // Stage 6: Sentiment Analysis
        // ============================================================
        await RunNlpStageAsync("sentiment", analysisId, async () =>
        {
            await _repo.UpdateAnalysisStatusAsync(analysisId, "sentiment", null, ct);
            var sentimentCount = await _sentimentAnalyzer.RunAsync(analysisId, tenantId, OnProgress, ct);
            _logger.SystemInfo($"[PipelineOrchestrator] Stage 6 complete: {sentimentCount:N0} sentiments");
        }, ct);

        // ============================================================
        // Stage 7: Product Analysis
        // ============================================================
        await RunNlpStageAsync("products", analysisId, async () =>
        {
            await _repo.UpdateAnalysisStatusAsync(analysisId, "products", null, ct);
            var productCount = await _productAnalyzer.RunAsync(analysisId, tenantId, OnProgress, ct);
            _logger.SystemInfo($"[PipelineOrchestrator] Stage 7 complete: {productCount:N0} conversations analyzed");
        }, ct);

        // ============================================================
        // Complete
        // ============================================================
        await _repo.CompleteAnalysisAsync(analysisId, ct);

        sw.Stop();
        _logger.SystemInfo(
            $"[PipelineOrchestrator] Pipeline complete for analysis {analysisId}: " +
            $"{messageCount:N0} messages, {conversationCount:N0} conversations in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Run an NLP stage with error isolation.
    /// Stage failures are logged but don't stop the pipeline — prior stage data is preserved.
    /// OperationCanceledException propagates naturally (not caught).
    /// Typed catches for known NLP stage failure modes: DB, HTTP, regex, JSON, config.
    /// </summary>
    private async Task RunNlpStageAsync(string stageName, int analysisId, Func<Task> stageAction, CancellationToken ct)
    {
        try
        {
            await stageAction();
        }
        catch (DbException ex)
        {
            _logger.SystemWarn(
                $"[PipelineOrchestrator] NLP stage '{stageName}' DB error for analysis {analysisId}: {ex.Message}. " +
                "Continuing to next stage — partial NLP results preserved.");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[PipelineOrchestrator] NLP stage '{stageName}' API error for analysis {analysisId}: {ex.Message}. " +
                "Continuing to next stage — partial NLP results preserved.");
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.SystemWarn(
                $"[PipelineOrchestrator] NLP stage '{stageName}' regex timeout for analysis {analysisId}: {ex.Message}. " +
                "Continuing to next stage — partial NLP results preserved.");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[PipelineOrchestrator] NLP stage '{stageName}' JSON error for analysis {analysisId}: {ex.Message}. " +
                "Continuing to next stage — partial NLP results preserved.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[PipelineOrchestrator] NLP stage '{stageName}' config error for analysis {analysisId}: {ex.Message}. " +
                "Continuing to next stage — partial NLP results preserved.");
        }
    }
}
