using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Automation.Services;

/// <summary>
/// G6: Background service polling flow_execution_state for due pending rows (resume_at &lt;= now).
/// Fires AutomationOrchestrator.ResumeWaitAsync for each. Restart-safe: state in DB, timer is stateless.
/// Timer interval 60s (mirrors CronSchedulerService cadence).
/// Non-overlapping: reentry guarded by Interlocked flag.
/// </summary>
public sealed class FlowWaitResumerService : IHostedService, IDisposable
{
    private readonly FlowWaitRepository _waitRepo;
    private readonly IServiceProvider _services;
    private readonly JsonLinesLogger _logger;

    private Timer? _timer;
    private int _isRunning;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    /// <summary>Max rows processed per tick — prevents a backlog spike from monopolizing a tick.</summary>
    private const int BatchLimit = 100;

    public FlowWaitResumerService(
        FlowWaitRepository waitRepo,
        IServiceProvider services,
        JsonLinesLogger logger)
    {
        _waitRepo = waitRepo;
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(10), TickInterval);
        _logger.SystemInfo("FlowWaitResumerService started (interval: 60s, batch: 100)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _logger.SystemInfo("FlowWaitResumerService stopping");
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private void OnTimerTick(object? _)
    {
        // Prevent overlapping ticks if a previous run is still processing.
        if (Interlocked.Exchange(ref _isRunning, 1) == 1) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessDueAsync(CancellationToken.None);
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resumer tick DB error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                // Shutdown — expected.
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resumer tick invalid state: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        });
    }

    private async Task ProcessDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        List<DueWaitRow> due;
        try
        {
            due = await _waitRepo.GetDueAsync(now, BatchLimit, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] GetDueAsync DB error: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            return; // shutdown
        }

        if (due.Count == 0) return;
        _logger.SystemInfo($"G6: {due.Count} due wait row(s) to resume");

        // Resolve orchestrator lazily to break circular dep at DI composition.
        var orchestrator = _services.GetService(typeof(AutomationOrchestrator)) as AutomationOrchestrator;
        if (orchestrator == null)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] AutomationOrchestrator unavailable from DI");
            return;
        }

        foreach (var row in due)
        {
            ct.ThrowIfCancellationRequested();

            // Atomic claim — skip if another tick (or replica) already took it.
            var claimed = await _waitRepo.TryMarkResumedAsync(row.Id, ct);
            if (!claimed) continue;

            try
            {
                var ok = await orchestrator.ResumeWaitAsync(row, ct);
                if (!ok)
                {
                    await _waitRepo.MarkFailedAsync(row.Id, "ResumeWaitAsync returned false", ct);
                }
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resume row {row.Id} DB error: {ex.Message}");
                await _waitRepo.MarkFailedAsync(row.Id, ex.Message, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resume row {row.Id} invalid state: {ex.Message}");
                await _waitRepo.MarkFailedAsync(row.Id, ex.Message, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resume row {row.Id} HTTP callback failed: {ex.Message}");
                await _waitRepo.MarkFailedAsync(row.Id, ex.Message, ct);
            }
        }
    }
}
