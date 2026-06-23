using Hangfire;
using Chatinbox.Automation.Data;
using Chatinbox.Automation.Services.Lifecycle;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Automation.Services.Jobs;

/// <summary>
/// FEAT-LIW Chunk B: real welcome-flow dispatch. Replaces the Chunk A placeholder
/// that only logged the trigger. Pattern mirrors <see cref="CronSchedulerJob"/>:
/// resolve flow_config (here by slug→flow_name, not by cron schedule), build the
/// FlowGraphV2, create a synthetic chat session, run FlowEngineV2, then close
/// the session with the engine's terminal status. Stays best-effort
/// (<see cref="AutomaticRetryAttribute"/> = 0): a failed welcome dispatch logs
/// a structured warn rather than retrying — the next inbound message after the
/// dup-window expiry will re-engage and re-enqueue from the intake side, which
/// matches Q's preserved Chunk A semantic and avoids retry storms when a tenant
/// has misconfigured the slug.
///
/// Error code split (post Codex iter 0 feedback):
///   INV-AT-068 — defensive: empty/whitespace slug (Backend never sends one; this
///                fires only on contract violation).
///   INV-AT-069 — config gap: chatbot_flows row missing/inactive, OR matched row
///                has no recognized welcome-trigger entry node.
///   INV-AT-071 — execution-time infra failure: NpgsqlException during lookup,
///                FlowGraphV2.Build returning null on malformed JSON, cancellation,
///                or InvalidOperationException from FlowEngineV2.ExecuteAsync.
/// Misclassifying these (e.g. logging a DB outage as INV-AT-069) would mask infra
/// problems behind a "tenant config" alert in ops dashboards.
///
/// Slug→flow trade-off: chatbot_flows currently has no dedicated slug column, so
/// flow_name doubles as the slug (Q-approved over a migration). A tenant
/// renaming the welcome flow in Dashboard breaks the binding silently — Chunk C
/// will surface a rename-warning UI in the Dashboard flow editor; until then,
/// INV-AT-069 keeps the failure observable in ops logs.
///
/// Queue: <c>automation</c>. Synthetic chat_id range: starts at -3_000_000 and
/// decrements; this stays clear of the cron job's -2_000_000 range and the
/// webhook's -1_000_000 range so collisions across welcome / cron / inbound
/// can't happen and an operator looking at chat_sessions can identify the
/// origin from the id sign + magnitude alone.
/// </summary>
[Queue("automation")]
[AutomaticRetry(Attempts = 0)]
public sealed class TriggerWelcomeFlowJob
{
    /// <summary>
    /// Trigger node types acceptable as a welcome-flow entry point. Excludes
    /// 'schedule_trigger' (cron-only — owned by <see cref="CronSchedulerJob"/>;
    /// running it from welcome dispatch would double-fire the schedule).
    /// </summary>
    private static readonly HashSet<string> WelcomeCompatibleTriggers =
        new(StringComparer.Ordinal)
        {
            "trigger_start",
            "webhook_trigger",
            "outbound_trigger"
        };

    private readonly AutomationRepository _repo;
    private readonly FlowEngineV2 _engine;
    private readonly ILifecycleBackendClient _lifecycleClient;
    private readonly JsonLinesLogger _logger;

    // Welcome-trigger synthetic chat_id counter; range chosen to never overlap
    // with CronSchedulerJob (-2_000_000) or webhook handler (-1_000_000).
    private static long _welcomeChatCounter = -3_000_000L;

    public TriggerWelcomeFlowJob(
        AutomationRepository repo,
        FlowEngineV2 engine,
        ILifecycleBackendClient lifecycleClient,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _engine = engine;
        _lifecycleClient = lifecycleClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(int tenantId, string welcomeFlowSlug, int leadId, CancellationToken ct = default)
    {
        // Defensive guard: Backend's intake services always emit a non-empty slug
        // (falling back to 'welcome_default' before enqueue). This branch fires
        // only if the contract is violated — INV-AT-068 keeps that signal alive.
        if (string.IsNullOrWhiteSpace(welcomeFlowSlug))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowSlugMissing}] TriggerWelcomeFlowJob: empty slug, skipping tenant={tenantId} lead={leadId}");
            return;
        }

        // Resolve slug → flow_config. NpgsqlException is INFRA (INV-AT-071);
        // a null result is a CONFIG GAP (INV-AT-069). Cancellation is logged
        // as infra (INV-AT-071) so a graceful-shutdown timing race during a
        // welcome doesn't masquerade as a tenant-config alert.
        (string FlowConfigJson, int FlowId)? flow;
        try
        {
            flow = await _repo.GetFlowByNameAndTenantAsync(tenantId, welcomeFlowSlug, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob: " +
                $"flow lookup DB error tenant={tenantId} slug={welcomeFlowSlug} lead={leadId}: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob: " +
                $"cancelled during flow lookup tenant={tenantId} slug={welcomeFlowSlug} lead={leadId}");
            return;
        }

        if (flow == null)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowDefinitionMissing}] TriggerWelcomeFlowJob: " +
                $"no active chatbot_flows row matches tenant={tenantId} slug={welcomeFlowSlug} lead={leadId}");
            return;
        }

        // FlowGraphV2.Build returns null on JSON parse error or version mismatch
        // — that's a CONFIG-AUTHORING failure (the tenant saved unusable JSON);
        // we still log it under INV-AT-071 because the operator response is
        // "look at flow_config", not "the row is missing", and those resolve at
        // different surfaces. (Codex iter 0 feedback: don't reuse INV-AT-069
        // here since the row IS present, just unparseable.)
        var graph = FlowGraphV2.Build(flow.Value.FlowConfigJson);
        if (graph == null)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob: " +
                $"flow {flow.Value.FlowId} flow_config could not be built (malformed JSON or wrong version) " +
                $"tenant={tenantId} slug={welcomeFlowSlug} lead={leadId}");
            return;
        }

        // Capture trigger node into a non-null local once, after the null check,
        // so the rest of the method can use a normal reference instead of the
        // null-forgiving operator (forbidden by project rules per Codex iter 0).
        var triggerNode = graph.TriggerStart;
        if (triggerNode == null)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowDefinitionMissing}] TriggerWelcomeFlowJob: " +
                $"flow {flow.Value.FlowId} has no v2 trigger entry node (tenant={tenantId} slug={welcomeFlowSlug} lead={leadId})");
            return;
        }

        // Reject schedule_trigger explicitly: that node is cron-owned and lives
        // in its own dispatch path. Accept the documented welcome-compatible
        // set; anything else means the tenant wired a non-trigger node as
        // entry point (impossible in normal Dashboard flows but cheap to guard).
        if (!WelcomeCompatibleTriggers.Contains(triggerNode.Type))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowDefinitionMissing}] TriggerWelcomeFlowJob: " +
                $"flow {flow.Value.FlowId} trigger node type '{triggerNode.Type}' is not welcome-compatible " +
                $"(must be one of: {string.Join(", ", WelcomeCompatibleTriggers)}) " +
                $"tenant={tenantId} slug={welcomeFlowSlug} lead={leadId}");
            return;
        }

        await ExecuteFlowAsync(tenantId, leadId, welcomeFlowSlug, flow.Value.FlowId, graph, triggerNode, ct);
    }

    private async Task ExecuteFlowAsync(
        int tenantId, int leadId, string slug, int flowId,
        FlowGraphV2 graph, FlowNodeV2 triggerNode, CancellationToken ct)
    {
        var syntheticChatId = $"welcome_{Interlocked.Decrement(ref _welcomeChatCounter)}";
        var sessionId = -1;

        try
        {
            // phone is null here — the welcome trigger has the lead_id in its
            // log line for correlation, but we deliberately don't load the
            // phone from leads (extra DB round-trip in the hot path; lead_id is
            // enough to join in any downstream analytics query).
            sessionId = await _repo.CreateSessionAsync(
                tenantId, syntheticChatId, phone: null, currentNode: "v2_active", ct);

            var state = new SessionStateV2
            {
                CurrentNodeId = triggerNode.Id,
                Status = "active"
            };

            var result = await _engine.ExecuteAsync(graph, state, ct, tenantId: tenantId);

            var finalStatus = result.IsTerminal
                ? (result.NeedsHandoff ? "handed_off" : (result.ErrorCode != null ? "error" : "completed"))
                : "completed";

            await _repo.EndSessionAsync(sessionId, finalStatus, ct);

            _logger.SystemInfo(
                $"TriggerWelcomeFlowJob: tenant={tenantId} flow={flowId} slug={slug} lead={leadId} " +
                $"status={finalStatus} messages={result.Messages.Count} path={state.ExecutionPath.Count}");

            // Paket B-META: lifecycle welcome-sent hop. Fires ONLY when the
            // welcome flow actually emitted at least one outbound message —
            // Q-approved literal "mesaj atildi" semantic over IsTerminal (too
            // aggressive: includes error branches) and finalStatus==completed
            // (too conservative: misses valid partial-success runs). Transport
            // failures are logged as INV-AT-072 inside the client and returned
            // as false; we intentionally don't retry here because the welcome
            // message already reached the user and the next inbound engagement
            // triggers its own lifecycle events.
            if (ShouldDispatchWelcomeSent(result))
            {
                try
                {
                    await _lifecycleClient.SendWelcomeSentAsync(tenantId, leadId, ct);
                }
                catch (HttpRequestException ex)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.AutomationLifecycleHopFailed}] TriggerWelcomeFlowJob: " +
                        $"lifecycle welcome-sent transport (tenant={tenantId}, lead={leadId}): {ex.Message}");
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.AutomationLifecycleHopFailed}] TriggerWelcomeFlowJob: " +
                        $"lifecycle welcome-sent timeout (tenant={tenantId}, lead={leadId}): {ex.Message}");
                }
            }
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob execution DB error: " +
                $"tenant={tenantId} flow={flowId} slug={slug} lead={leadId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob: " +
                $"execution cancelled tenant={tenantId} flow={flowId} slug={slug} lead={leadId}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob execution error: " +
                $"tenant={tenantId} flow={flowId} slug={slug} lead={leadId}: {ex.Message}");
            await TryEndSessionOnErrorAsync(sessionId);
        }
    }

    /// <summary>
    /// Paket B-META: AC2 invariant — welcome_sent lifecycle dispatch fires iff
    /// the welcome flow actually emitted at least one outbound message. Public
    /// static so Chatinbox.Automation.Tests can pin the semantic without setting
    /// up the full FlowEngineV2 + AutomationRepository mock graph (both sealed,
    /// not NSubstitute-mockable). Callers that already have an EngineStepResult
    /// should prefer this overload over inline <c>result.Messages.Count &gt; 0</c>
    /// so the decision stays in one place.
    ///
    /// Null-safe: a null result (caller bug / serialization gap) returns false
    /// rather than throwing — fire-and-forget lifecycle hop must never crash
    /// the welcome job's cleanup path.
    /// </summary>
    public static bool ShouldDispatchWelcomeSent(EngineStepResult? result)
    {
        if (result is null) return false;
        return result.Messages.Count > 0;
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
                $"[{ErrorCodes.AutomationWelcomeFlowDispatchFailed}] TriggerWelcomeFlowJob: " +
                $"secondary EndSession failed for session {sessionId}: {ex.Message}");
        }
    }
}
