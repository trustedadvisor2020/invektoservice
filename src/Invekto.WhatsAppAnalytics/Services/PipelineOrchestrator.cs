using System.Diagnostics;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Invekto.WhatsAppAnalytics.Services.Pipeline;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Orchestrates pipeline stages 1-3 (Phase A) sequentially.
/// Updates wa_analyses status and stage_progress after each stage.
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly AnalyticsRepository _repo;
    private readonly CleanerService _cleaner;
    private readonly ThreaderService _threader;
    private readonly StatsService _stats;
    private readonly JsonLinesLogger _logger;

    public PipelineOrchestrator(
        AnalyticsRepository repo,
        CleanerService cleaner,
        ThreaderService threader,
        StatsService stats,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _cleaner = cleaner;
        _threader = threader;
        _stats = stats;
        _logger = logger;
    }

    /// <summary>
    /// Run the full pipeline for an analysis job.
    /// Updates status to cleaning -> threading -> stats -> completed (or error).
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
        // Stage 1: Cleaning
        // ============================================================
        await _repo.UpdateAnalysisStatusAsync(analysisId, "cleaning", null, ct);

        var messageCount = await _cleaner.RunAsync(
            analysisId, tenantId, job.FilePath, job.Delimiter,
            OnProgress, ct);

        _logger.SystemInfo($"[PipelineOrchestrator] Stage 1 complete: {messageCount:N0} messages inserted");

        if (messageCount == 0)
        {
            await _repo.FailAnalysisAsync(analysisId, "No valid messages found in CSV file");
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
        // Complete
        // ============================================================
        await _repo.CompleteAnalysisAsync(analysisId, ct);

        sw.Stop();
        _logger.SystemInfo(
            $"[PipelineOrchestrator] Pipeline complete for analysis {analysisId}: " +
            $"{messageCount:N0} messages, {conversationCount:N0} conversations in {sw.ElapsedMilliseconds}ms");
    }
}
