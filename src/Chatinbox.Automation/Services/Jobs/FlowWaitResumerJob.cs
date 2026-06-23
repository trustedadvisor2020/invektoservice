using Hangfire;
using Chatinbox.Automation.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Automation.Services.Jobs;

/// <summary>
/// G7: Hangfire recurring job replacing <c>FlowWaitResumerService</c>.
/// Polls <c>flow_execution_state</c> for due pending rows and delegates to
/// <see cref="AutomationOrchestrator.ResumeWaitAsync"/> per row. Service-wide
/// iteration (no tenant_id arg) — tenant scoping happens inside the orchestrator
/// via <see cref="DueWaitRow.TenantId"/>.
///
/// Queue: <c>automation</c>. Recurring id: <c>automation:flow-wait-resumer</c>
/// (<see cref="Cron.Minutely"/>). Cross-process overlap prevented by
/// <see cref="DisableConcurrentExecutionAttribute"/> (PG advisory lock).
/// </summary>
[Queue("automation")]
[DisableConcurrentExecution(timeoutInSeconds: 30)]
public sealed class FlowWaitResumerJob
{
    private readonly FlowWaitRepository _waitRepo;
    private readonly AutomationOrchestrator _orchestrator;
    private readonly JsonLinesLogger _logger;

    /// <summary>Max rows processed per tick — prevents a backlog spike from monopolizing a tick.</summary>
    private const int BatchLimit = 100;

    public FlowWaitResumerJob(
        FlowWaitRepository waitRepo,
        AutomationOrchestrator orchestrator,
        JsonLinesLogger logger)
    {
        _waitRepo = waitRepo;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await ProcessDueAsync(ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resumer tick DB error: {ex.Message}");
            throw;
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — expected during Hangfire server stop. Logged for ops visibility.
            _logger.SystemInfo("FlowWaitResumerJob run cancelled (graceful shutdown)");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Resumer tick invalid state: {ex.Message}");
            throw;
        }
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

        foreach (var row in due)
        {
            ct.ThrowIfCancellationRequested();

            // Atomic claim — skip if another tick (or replica) already took it.
            var claimed = await _waitRepo.TryMarkResumedAsync(row.Id, ct);
            if (!claimed) continue;

            try
            {
                var ok = await _orchestrator.ResumeWaitAsync(row, ct);
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
