using System.Collections.Concurrent;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Benchmark;

/// <summary>
/// Background worker that processes benchmark jobs from a ConcurrentQueue.
/// One benchmark at a time. Mirrors AnalysisProcessingService pattern.
/// </summary>
public sealed class BenchmarkProcessingService : BackgroundService
{
    private readonly ConcurrentQueue<BenchmarkProcessJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly BenchmarkOrchestrator _orchestrator;
    private readonly BenchmarkRepository _repo;
    private readonly JsonLinesLogger _logger;

    public BenchmarkProcessingService(
        BenchmarkOrchestrator orchestrator,
        BenchmarkRepository repo,
        JsonLinesLogger logger)
    {
        _orchestrator = orchestrator;
        _repo = repo;
        _logger = logger;
    }

    public void EnqueueBenchmark(BenchmarkProcessJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
        _logger.SystemInfo($"[BenchmarkProcessingService] Benchmark enqueued: id={job.BenchmarkId}, db={job.DatabaseName}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.SystemInfo("[BenchmarkProcessingService] Background benchmark processor started");

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
                    _logger.SystemWarn($"[BenchmarkProcessingService] Benchmark {job.BenchmarkId} cancelled due to shutdown");
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.SystemError($"[{ErrorCodes.WABenchmarkMssqlError}] Benchmark {job.BenchmarkId} connection failed: {ex.Message}");
                    await SafeFailJobAsync(job.BenchmarkId, $"[{ErrorCodes.WABenchmarkMssqlError}] {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemError($"[{ErrorCodes.WABenchmarkInvalidConfig}] Benchmark {job.BenchmarkId} config error: {ex.Message}");
                    await SafeFailJobAsync(job.BenchmarkId, $"[{ErrorCodes.WABenchmarkInvalidConfig}] {ex.Message}");
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    _logger.SystemError($"[{ErrorCodes.WABenchmarkMssqlError}] Benchmark {job.BenchmarkId} database error: {ex.Message}");
                    // Cannot SafeFailJobAsync — the PG connection itself may be down
                }
                catch (Microsoft.Data.SqlClient.SqlException ex)
                {
                    _logger.SystemError($"[{ErrorCodes.WABenchmarkMssqlError}] Benchmark {job.BenchmarkId} SQL error: {ex.Message}");
                    await SafeFailJobAsync(job.BenchmarkId, $"[{ErrorCodes.WABenchmarkMssqlError}] {ex.Message}");
                }
            }
        }

        _logger.SystemInfo("[BenchmarkProcessingService] Background benchmark processor stopped");
    }

    private async Task SafeFailJobAsync(int benchmarkId, string errorMessage)
    {
        try
        {
            await _repo.FailJobAsync(benchmarkId, errorMessage);
        }
        catch (Npgsql.NpgsqlException dbEx)
        {
            _logger.SystemError($"[BenchmarkProcessingService] Failed to set error status for benchmark {benchmarkId}: {dbEx.Message}");
        }
    }
}
