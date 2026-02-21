using System.Collections.Concurrent;
using Cronos;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Background service that fires schedule_trigger flows based on cron expressions.
/// Timer fires every 60 seconds. Uses Cronos for cron parsing and timezone support.
/// On restart: lastCheckTime = now (acceptable single-window miss, prevents double-fire).
/// </summary>
public sealed class CronSchedulerService : IHostedService, IDisposable
{
    private readonly AutomationRepository _repo;
    private readonly FlowEngineV2 _engine;
    private readonly JsonLinesLogger _logger;

    private Timer? _timer;
    private int _isRunning;

    // flow_id -> last fired UTC. In-memory only — resets on restart (by-design).
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastFired = new();

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeZoneInfo DefaultTimezone =
        TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); // Windows ID for Europe/Istanbul

    // Synthetic chat_id counter: starts at -2_000_000, decrements atomically.
    // Separate range from webhook counter (-1_000_000) to avoid collision.
    private static long _scheduleCounter = -2_000_000L;

    public CronSchedulerService(
        AutomationRepository repo,
        FlowEngineV2 engine,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _engine = engine;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(OnTimerTick, null, TickInterval, TickInterval);
        _logger.SystemInfo("CronSchedulerService started (interval: 60s)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _logger.SystemInfo("CronSchedulerService stopping");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void OnTimerTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return; // Previous tick still running — skip

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteScheduledFlowsAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _logger.SystemWarn("CronScheduler tick cancelled");
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.SystemError($"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronScheduler tick DB error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemError($"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronScheduler tick config error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }).ContinueWith(
            t => _logger.SystemError($"CronScheduler unhandled: {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task ExecuteScheduledFlowsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        List<ScheduleFlowInfo> flows;
        try
        {
            flows = await _repo.GetActiveScheduleFlowsAsync(ct);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError($"CronScheduler DB query failed: {ex.Message}");
            return;
        }

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
                    $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronScheduler DB error: " +
                    $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.SystemWarn($"CronScheduler: flow {flow.FlowId} cancelled");
            }
        }
    }

    private async Task TryExecuteFlowAsync(ScheduleFlowInfo flow, DateTimeOffset now, CancellationToken ct)
    {
        var graph = FlowGraphV2.Build(flow.FlowConfigJson);
        if (graph?.TriggerStart == null || graph.TriggerStart.Type != "schedule_trigger")
        {
            _logger.SystemWarn($"CronScheduler: flow {flow.FlowId} has no schedule_trigger start node");
            return;
        }

        var cronExpr = graph.TriggerStart.GetData("cron_expression", "");
        if (string.IsNullOrWhiteSpace(cronExpr))
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationCronExpressionInvalid}] CronScheduler: empty cron for flow {flow.FlowId}");
            return;
        }

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(cronExpr, CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationCronExpressionInvalid}] CronScheduler: invalid cron '{cronExpr}' for flow {flow.FlowId}: {ex.Message}");
            return;
        }

        var tz = ResolveTimezone(graph.TriggerStart.GetData("timezone", ""));

        // Determine check window: from last fired (or now on first run — prevents double-fire on restart)
        var lastFired = _lastFired.TryGetValue(flow.FlowId, out var lf)
            ? lf
            : now;

        var nextOccurrence = cron.GetNextOccurrence(lastFired, tz);
        if (nextOccurrence == null || nextOccurrence.Value > now)
            return; // Not due yet

        // Mark fired BEFORE execution to prevent double-fire on slow execution
        _lastFired[flow.FlowId] = now;

        _logger.SystemInfo($"CronScheduler: firing flow {flow.FlowId} (tenant={flow.TenantId}), cron='{cronExpr}', due={nextOccurrence:O}");
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
                : "completed"; // Schedule flows don't wait for input

            await _repo.EndSessionAsync(sessionId, finalStatus, ct);

            _logger.SystemInfo(
                $"CronScheduler: flow {flow.FlowId} (tenant={flow.TenantId}) done: " +
                $"status={finalStatus}, messages={result.Messages.Count}, path={state.ExecutionPath.Count} nodes");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronScheduler execution DB error: " +
                $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");

            if (sessionId > 0)
            {
                try { await _repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
                catch (Npgsql.NpgsqlException ex2) { _logger.SystemWarn($"CronScheduler: secondary EndSession failed for session {sessionId}: {ex2.Message}"); }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn($"CronScheduler: flow {flow.FlowId} execution cancelled");
            if (sessionId > 0)
            {
                try { await _repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
                catch (Npgsql.NpgsqlException ex2) { _logger.SystemWarn($"CronScheduler: secondary EndSession failed for session {sessionId}: {ex2.Message}"); }
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationScheduleExecutionFailed}] CronScheduler execution error: " +
                $"flow={flow.FlowId}, tenant={flow.TenantId}: {ex.Message}");

            if (sessionId > 0)
            {
                try { await _repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
                catch (Npgsql.NpgsqlException ex2) { _logger.SystemWarn($"CronScheduler: secondary EndSession failed for session {sessionId}: {ex2.Message}"); }
            }
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
            _logger.SystemWarn($"CronScheduler: unknown timezone '{timezoneId}', using default");
            return DefaultTimezone;
        }
    }
}
