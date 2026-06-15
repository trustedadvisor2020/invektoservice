using Hangfire;
using Invekto.Automation.Data;
using Invekto.Automation.Services.NodeHandlers;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3a: fires customer_status_changed flows after C2 applies an INMA
/// <c>customer.selection_changed</c> status to a lead. Enqueued by Backend (one job per CHANGED lead)
/// only when the fail-closed loop guard passes (explicit human 'user' actor, no <c>invekto-</c> origin
/// echo) and the lead's status actually transitioned.
///
/// Pattern mirrors <see cref="TriggerWelcomeFlowJob"/> / <see cref="CronSchedulerJob"/>: resolve the
/// tenant's flow_config rows, build <see cref="FlowGraphV2"/>, create a synthetic chat session, run
/// <see cref="FlowEngineV2"/>, close the session. Differences: (1) resolves flows by TRIGGER TYPE via a
/// tenant-scoped JSONB scan, not by slug or cron; (2) MOST-SPECIFIC-WINS featureGroupId matching — if any
/// flow's trigger node targets the event's featureGroupId, only those run, otherwise the catch-all
/// (empty feature_group_id) flows run; (3) seeds the change context (group / old / new / changedBy) into
/// session Variables for <see cref="CustomerStatusChangedTriggerHandler"/> to surface as flow variables.
///
/// Queue: <c>automation</c>. AutomaticRetry=0 (matches the welcome/cron flow-dispatch posture): a failed
/// dispatch logs INV-AT-088 rather than retry-storming a misconfigured flow. Replay double-fire is
/// already prevented UPSTREAM by C2's dedupe(tenant_id,event_id), so no retry => no duplicate vector.
/// Synthetic chat_id range starts at -4_000_000 (clear of welcome -3M, cron -2M, webhook -1M) so an
/// operator can identify the origin from the id sign + magnitude alone.
/// </summary>
[Queue("automation")]
[AutomaticRetry(Attempts = 0)]
public sealed class TriggerCustomerStatusFlowJob
{
    private const string TriggerNodeType = "customer_status_changed";

    private readonly AutomationRepository _repo;
    private readonly FlowEngineV2 _engine;
    private readonly JsonLinesLogger _logger;

    // Synthetic chat_id counter; range chosen to never overlap welcome (-3M) / cron (-2M) / webhook (-1M).
    private static long _chatCounter = -4_000_000L;

    public TriggerCustomerStatusFlowJob(AutomationRepository repo, FlowEngineV2 engine, JsonLinesLogger logger)
    {
        _repo = repo;
        _engine = engine;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        int tenantId,
        int leadId,
        string? newStatus,
        string? oldStatus,
        int? featureGroupId,
        string? featureGroupName,
        string? changedBy,
        int? customerId,
        string? phone,
        CancellationToken ct = default)
    {
        // Resolve the tenant's customer_status_changed flows. NpgsqlException is INFRA (INV-AT-088);
        // cancellation is a graceful-shutdown race (also INV-AT-088 warn).
        List<CustomerStatusFlowInfo> flows;
        try
        {
            flows = await _repo.GetActiveCustomerStatusFlowsAsync(tenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob: " +
                $"flow lookup DB error tenant={tenantId} lead={leadId}: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob: " +
                $"cancelled during flow lookup tenant={tenantId} lead={leadId}");
            return;
        }

        if (flows.Count == 0)
        {
            // Benign: the tenant simply has no customer_status_changed flow wired (NOT an error).
            _logger.SystemInfo(
                $"TriggerCustomerStatusFlowJob: no customer_status_changed flow for tenant={tenantId} lead={leadId} (no-op)");
            return;
        }

        // Build candidates; skip rows whose flow_config can't be built or whose trigger node is unusable.
        var candidates = new List<FlowCandidate>();
        foreach (var flow in flows)
        {
            var graph = FlowGraphV2.Build(flow.FlowConfigJson);
            var trigger = graph?.TriggerStart;
            if (graph == null || trigger == null || trigger.Type != TriggerNodeType)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob: " +
                    $"flow {flow.FlowId} has no usable {TriggerNodeType} trigger node (tenant={tenantId} lead={leadId})");
                continue;
            }

            var fgRaw = trigger.GetData("feature_group_id", "").Trim();
            if (fgRaw.Length == 0)
            {
                candidates.Add(new FlowCandidate(flow.FlowId, graph, trigger, IsCatchAll: true, NodeFeatureGroupId: null));
            }
            else if (int.TryParse(fgRaw, out var nodeFg))
            {
                candidates.Add(new FlowCandidate(flow.FlowId, graph, trigger, IsCatchAll: false, NodeFeatureGroupId: nodeFg));
            }
            else
            {
                // Non-empty but non-numeric feature_group_id = misconfiguration; never fire on garbage.
                _logger.SystemWarn(
                    $"TriggerCustomerStatusFlowJob: flow {flow.FlowId} has non-numeric feature_group_id='{fgRaw}', skipping (tenant={tenantId})");
            }
        }

        if (candidates.Count == 0)
            return;

        // Most-specific-wins: a flow targeting the event's exact group beats a catch-all. A text-mode
        // event (featureGroupId null) can only match catch-all flows.
        var specific = featureGroupId.HasValue
            ? candidates.Where(c => !c.IsCatchAll && c.NodeFeatureGroupId == featureGroupId.Value).ToList()
            : new List<FlowCandidate>();
        var toRun = specific.Count > 0
            ? specific
            : candidates.Where(c => c.IsCatchAll).ToList();

        if (toRun.Count == 0)
        {
            _logger.SystemInfo(
                $"TriggerCustomerStatusFlowJob: no flow matched fg={featureGroupId?.ToString() ?? "-"} " +
                $"tenant={tenantId} lead={leadId} (candidates={candidates.Count}, no-op)");
            return;
        }

        var seedVars = BuildSeedVariables(newStatus, oldStatus, featureGroupId, featureGroupName, changedBy);
        foreach (var candidate in toRun)
        {
            await ExecuteFlowAsync(tenantId, leadId, candidate, seedVars, customerId, phone, ct);
        }
    }

    private async Task ExecuteFlowAsync(int tenantId, int leadId, FlowCandidate candidate,
        IReadOnlyDictionary<string, string> seedVars, int? customerId, string? phone, CancellationToken ct)
    {
        var syntheticChatId = $"custstatus_{Interlocked.Decrement(ref _chatCounter)}";
        var sessionId = -1;

        try
        {
            sessionId = await _repo.CreateSessionAsync(
                tenantId, syntheticChatId, phone: null, currentNode: "v2_active", ct);

            var state = new SessionStateV2
            {
                CurrentNodeId = candidate.Trigger.Id,
                Status = "active",
                // Fresh per-run copy — the engine merges VariableUpdates into state.Variables.
                Variables = new Dictionary<string, string>(seedVars, StringComparer.Ordinal)
            };

            // C4: thread the INMA customer identity so a 'Set Customer Status' action in this flow can
            // write back (customerId preferred, phone fallback).
            var result = await _engine.ExecuteAsync(candidate.Graph, state, ct, tenantId: tenantId,
                customerId: customerId, phone: phone);

            var finalStatus = result.IsTerminal
                ? (result.NeedsHandoff ? "handed_off" : (result.ErrorCode != null ? "error" : "completed"))
                : "completed";

            await _repo.EndSessionAsync(sessionId, finalStatus, ct);

            _logger.SystemInfo(
                $"TriggerCustomerStatusFlowJob: tenant={tenantId} flow={candidate.FlowId} lead={leadId} " +
                $"status={finalStatus} messages={result.Messages.Count} path={state.ExecutionPath.Count}");
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob execution DB error: " +
                $"tenant={tenantId} flow={candidate.FlowId} lead={leadId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob: " +
                $"execution cancelled tenant={tenantId} flow={candidate.FlowId} lead={leadId}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob execution error: " +
                $"tenant={tenantId} flow={candidate.FlowId} lead={leadId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
    }

    private static Dictionary<string, string> BuildSeedVariables(
        string? newStatus, string? oldStatus, int? featureGroupId, string? featureGroupName, string? changedBy)
    {
        string group = !string.IsNullOrWhiteSpace(featureGroupName)
            ? featureGroupName
            : (featureGroupId?.ToString() ?? string.Empty);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CustomerStatusChangedTriggerHandler.SeedGroup] = group,
            [CustomerStatusChangedTriggerHandler.SeedOld] = oldStatus ?? string.Empty,
            [CustomerStatusChangedTriggerHandler.SeedNew] = newStatus ?? string.Empty,
            [CustomerStatusChangedTriggerHandler.SeedChangedBy] = changedBy ?? string.Empty
        };
    }

    private async Task TryEndSessionOnErrorAsync(int sessionId)
    {
        if (sessionId <= 0) return;
        try
        {
            await _repo.EndSessionAsync(sessionId, "error", CancellationToken.None);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCustomerStatusFlowDispatchFailed}] TriggerCustomerStatusFlowJob: " +
                $"secondary EndSession failed for session {sessionId}: {ex.Message}");
        }
    }

    private sealed record FlowCandidate(
        int FlowId, FlowGraphV2 Graph, FlowNodeV2 Trigger, bool IsCatchAll, int? NodeFeatureGroupId);
}
