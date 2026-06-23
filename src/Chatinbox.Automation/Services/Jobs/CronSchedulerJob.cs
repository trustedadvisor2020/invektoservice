using System.Collections.Concurrent;
using Cronos;
using Hangfire;
using Chatinbox.Automation.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services.Jobs;

/// <summary>
/// G7 Faz 3: Hangfire recurring job replacing <c>CronSchedulerService</c>.
/// Fires schedule_trigger flows based on cron expressions. Hangfire ticks this minutely;
/// the handler reads active schedule flows from DB and evaluates each Cronos expression
/// against the (lastFired, now] window, firing those due.
///
/// Queue: <c>automation</c>. Recurring id: <c>automation:cron-scheduler</c> (cron every minute).
/// Registered as Singleton because <see cref="_lastFired"/> state must persist across invocations.
/// Overlap prevention via <see cref="DisableConcurrentExecutionAttribute"/> (PG advisory lock).
/// </summary>
[Queue("automation")]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class CronSchedulerJob
{
    private readonly AutomationRepository _repo;
    private readonly FlowEngineV2 _engine;
    private readonly JsonLinesLogger _logger;

    // flow_id -> last fired UTC. In-memory only — resets on restart (by-design; prevents double-fire).
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastFired = new();

    private static readonly TimeZoneInfo DefaultTimezone =
        TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); // Windows ID for Europe/Istanbul

    // Synthetic chat_id counter: starts at -2_000_000, decrements atomically.
    // Separate range from webhook counter (-1_000_000) to avoid collision.
    private static long _scheduleCounter = -2_000_000L;

    public CronSchedulerJob(
        AutomationRepository repo,
        FlowEngineV2 engine,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _engine = engine;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await ExecuteScheduledFlowsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("CronSchedulerJob: cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire (AutomaticRetry + INV-JOB-005 on final failure).
    }

    private async Task ExecuteScheduledFlowsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var flows = await _repo.GetActiveScheduleFlowsAsync(ct);
        if (flows.Count == 0)
            return;

        foreach (var flow in flows)
        {
            try
            {
                await TryExecuteFlowAsync(flow, now, ct);
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.SystemError(
                    $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob DB error: " +
                    $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob: flow {flow.FlowId} cancelled");
            }
        }
    }

    private async Task TryExecuteFlowAsync(ScheduleFlowInfo flow, DateTimeOffset now, CancellationToken ct)
    {
        var graph = FlowGraphV2.Build(flow.FlowConfigJson);
        if (graph?.TriggerStart == null || graph.TriggerStart.Type != "schedule_trigger")
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCronExpressionInvalid}] CronSchedulerJob: flow {flow.FlowId} has no schedule_trigger start node");
            return;
        }

        var cronExpr = graph.TriggerStart.GetData("cron_expression", "");
        if (string.IsNullOrWhiteSpace(cronExpr))
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationCronExpressionInvalid}] CronSchedulerJob: empty cron for flow {flow.FlowId}");
            return;
        }

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(cronExpr, CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationCronExpressionInvalid}] CronSchedulerJob: invalid cron '{cronExpr}' for flow {flow.FlowId}: {ex.Message}");
            return;
        }

        var tz = ResolveTimezone(graph.TriggerStart.GetData("timezone", ""));

        var lastFired = _lastFired.TryGetValue(flow.FlowId, out var lf)
            ? lf
            : now;

        var nextOccurrence = cron.GetNextOccurrence(lastFired, tz);
        if (nextOccurrence == null || nextOccurrence.Value > now)
            return;

        // Mark fired BEFORE execution to prevent double-fire on slow execution
        _lastFired[flow.FlowId] = now;

        _logger.SystemInfo($"CronSchedulerJob: firing flow {flow.FlowId} (tenant={flow.TenantId}), cron='{cronExpr}', due={nextOccurrence:O}");
        await ExecuteSingleFlowAsync(flow, graph, ct);
    }

    private async Task ExecuteSingleFlowAsync(ScheduleFlowInfo flow, FlowGraphV2 graph, CancellationToken ct)
    {
        var syntheticChatId = $"cron_{Interlocked.Decrement(ref _scheduleCounter)}";
        var sessionId = -1;

        try
        {
            sessionId = await _repo.CreateSessionAsync(
                flow.TenantId, syntheticChatId, phone: null, currentNode: "v2_active", ct);

            var state = new SessionStateV2
            {
                CurrentNodeId = graph.TriggerStart!.Id,
                Status = "active"
            };

            var result = await _engine.ExecuteAsync(graph, state, ct, tenantId: flow.TenantId);

            var finalStatus = result.IsTerminal
                ? (result.NeedsHandoff ? "handed_off" : (result.ErrorCode != null ? "error" : "completed"))
                : "completed";

            await _repo.EndSessionAsync(sessionId, finalStatus, ct);

            _logger.SystemInfo(
                $"CronSchedulerJob: flow {flow.FlowId} (tenant={flow.TenantId}) done: " +
                $"status={finalStatus}, messages={result.Messages.Count}, path={state.ExecutionPath.Count} nodes");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob execution DB error: " +
                $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob: flow {flow.FlowId} execution cancelled");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob execution error: " +
                $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
    }

    private async Task TryEndSessionOnErrorAsync(int sessionId)
    {
        if (sessionId <= 0) return;
        try
        {
            await _repo.EndSessionAsync(sessionId, "error", CancellationToken.None);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronSchedulerJob: secondary EndSession failed for session {sessionId}: {ex.Message}");
        }
    }

    private TimeZoneInfo ResolveTimezone(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
            return DefaultTimezone;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.SystemWarn($"CronSchedulerJob: unknown timezone '{timezoneId}', using default");
            return DefaultTimezone;
        }
    }
}
