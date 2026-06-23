using System.Collections.Concurrent;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services;

/// <summary>
/// Background worker that processes analysis jobs from a ConcurrentQueue.
/// On startup, re-enqueues analyses stuck in non-terminal state (restart recovery).
/// One analysis at a time to avoid DB contention on large datasets.
/// </summary>
public sealed class AnalysisProcessingService : BackgroundService
{
    private readonly ConcurrentQueue<AnalysisProcessJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly AnalyticsRepository _repository;
    private readonly PipelineOrchestrator _orchestrator;
    private readonly JsonLinesLogger _logger;
    private readonly string _uploadPath;

    public AnalysisProcessingService(
        AnalyticsRepository repository,
        PipelineOrchestrator orchestrator,
        JsonLinesLogger logger,
        string uploadPath)
    {
        _repository = repository;
        _orchestrator = orchestrator;
        _logger = logger;
        _uploadPath = uploadPath;
    }

    /// <summary>
    /// Enqueue a new analysis job for background processing.
    /// </summary>
    public void EnqueueAnalysis(AnalysisProcessJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
        _logger.SystemInfo($"[AnalysisProcessingService] Analysis enqueued: id={job.AnalysisId}, tenant={job.TenantId}, file={job.SourceFileName}");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Restart recovery: re-enqueue analyses stuck in non-terminal state
        try
        {
            var pending = await _repository.ClaimPendingAnalysesAsync(cancellationToken);
            foreach (var analysis in pending)
            {
                // Parse config to determine source type
                var sourceType = "csv";
                var delimiter = ';';
                string? mssqlDatabase = null;
                int? mssqlInstanceId = null;

                if (!string.IsNullOrEmpty(analysis.ConfigJson))
                {
                    try
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(analysis.ConfigJson);
                        if (config != null)
                        {
                            if (config.TryGetValue("source_type", out var stVal))
                                sourceType = stVal?.ToString() ?? "csv";
                            if (config.TryGetValue("delimiter", out var delimVal))
                            {
                                var delimStr = delimVal?.ToString();
                                if (!string.IsNullOrEmpty(delimStr) && delimStr.Length == 1)
                                    delimiter = delimStr[0];
                            }
                            if (config.TryGetValue("mssql_database", out var dbVal))
                                mssqlDatabase = dbVal?.ToString();
                            if (config.TryGetValue("mssql_instance_id", out var instVal))
                            {
                                if (int.TryParse(instVal?.ToString(), out var instId))
                                    mssqlInstanceId = instId;
                            }
                        }
                    }
                    catch (Exception configEx)
                    {
                        _logger.SystemWarn($"[AnalysisProcessingService] Failed to parse config for analysis {analysis.Id}: {configEx.Message}");
                    }
                }

                // For CSV source, verify file exists
                var filePath = "";
                if (sourceType == "csv")
                {
                    filePath = Path.Combine(_uploadPath, analysis.TenantId.ToString(), analysis.SourceFileName);
                    if (!File.Exists(filePath))
                    {
                        _logger.SystemWarn($"[AnalysisProcessingService] Skipping stuck analysis {analysis.Id}: file not found at {filePath}");
                        await _repository.FailAnalysisAsync(analysis.Id, $"Recovery failed: source file not found at {filePath}");
                        continue;
                    }
                }

                _queue.Enqueue(new AnalysisProcessJob
                {
                    AnalysisId = analysis.Id,
                    TenantId = analysis.TenantId,
                    SourceType = sourceType,
                    FilePath = filePath,
                    SourceFileName = analysis.SourceFileName,
                    Delimiter = delimiter,
                    MssqlDatabase = mssqlDatabase,
                    MssqlInstanceId = mssqlInstanceId
                });
                _signal.Release();

                _logger.SystemWarn($"[AnalysisProcessingService] Re-enqueued stuck analysis: id={analysis.Id}, status={analysis.Status}, source={sourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[AnalysisProcessingService] Failed to recover stuck analyses on startup: {ex.Message}");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.SystemInfo("[AnalysisProcessingService] Background analysis processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_queue.TryDequeue(out var job))
            {
                try
                {
                    await _orchestrator.RunAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.SystemWarn($"[AnalysisProcessingService] Analysis {job.AnalysisId} cancelled due to shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.SystemError($"[AnalysisProcessingService] Fatal error processing analysis {job.AnalysisId}: {ex.Message}");
                    try
                    {
                        await _repository.FailAnalysisAsync(job.AnalysisId, $"Pipeline failed: {ex.Message}");
                    }
                    catch (Exception dbEx)
                    {
                        _logger.SystemError($"[AnalysisProcessingService] Failed to set error status for analysis {job.AnalysisId}: {dbEx.Message}");
                    }
                }
            }
        }

        _logger.SystemInfo("[AnalysisProcessingService] Background analysis processor stopped");
    }
}
