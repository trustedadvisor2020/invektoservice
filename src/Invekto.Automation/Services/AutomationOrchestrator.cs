using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Automation.Services;

/// <summary>
/// Orchestrates the full message processing pipeline:
/// v1: Working hours check -> Flow engine -> FAQ match -> Intent detection -> Callback.
/// v2: Working hours check -> FlowEngineV2 (pure graph executor) -> Side-effect layer.
/// Version dispatch: flow_config.version field (1 or missing = v1, 2 = v2).
/// Thread-safe, register as singleton.
/// </summary>
public sealed class AutomationOrchestrator
{
    private readonly AutomationRepository _repo;
    private readonly FlowEngine _flowEngine;
    private readonly FlowEngineV2 _flowEngineV2;
    private readonly FaqMatcher _faqMatcher;
    private readonly IntentDetector _intentDetector;
    private readonly WorkingHoursChecker _workingHours;
    private readonly MainAppCallbackClient _callbackClient;
    private readonly KnowledgeIntentClient _knowledgeIntentClient;
    private readonly VipDetectionService _vipDetection;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    // GR-2.6: KVKK health tenant cache (tenant_id -> isHealthTenant)
    private readonly ConcurrentDictionary<int, bool> _healthTenantCache = new();

    public AutomationOrchestrator(
        AutomationRepository repo,
        FlowEngine flowEngine,
        FlowEngineV2 flowEngineV2,
        FaqMatcher faqMatcher,
        IntentDetector intentDetector,
        WorkingHoursChecker workingHours,
        MainAppCallbackClient callbackClient,
        KnowledgeIntentClient knowledgeIntentClient,
        VipDetectionService vipDetection,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _flowEngine = flowEngine;
        _flowEngineV2 = flowEngineV2;
        _faqMatcher = faqMatcher;
        _intentDetector = intentDetector;
        _workingHours = workingHours;
        _callbackClient = callbackClient;
        _knowledgeIntentClient = knowledgeIntentClient;
        _vipDetection = vipDetection;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Process an incoming message through the full automation pipeline.
    /// Returns true if processing completed (success or graceful failure).
    /// </summary>
    public async Task<bool> ProcessMessageAsync(
        TenantContext tenant,
        WebhookMessage message,
        string requestId,
        string? callbackUrl,
        string? instanceId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var tenantId = tenant.TenantId;
        var chatId = message.ChatId ?? "";
        var phone = message.FromContact;
        var messageText = message.Body ?? "";

        try
        {
            // 1. Get active flow config: instance-based routing or legacy single-flow
            JsonDocument? flowDoc;
            bool isActive;

            int rootFlowId = 0;
            var resolvedInstanceId = instanceId ?? string.Empty;
            var hasInstanceConfig = resolvedInstanceId.Length > 0 && await _repo.HasInstanceRecordsAsync(tenantId, ct);
            if (hasInstanceConfig)
            {
                // Multi-flow routing: get flow assigned to this instance
                var (instFlowDoc, instIsActive, instFlowId) = await _repo.GetFlowByInstanceAsync(tenantId, resolvedInstanceId, ct);
                flowDoc = instFlowDoc;
                isActive = instIsActive;
                rootFlowId = instFlowId;

                if (flowDoc == null || !isActive)
                {
                    _logger.StepWarn($"No active flow for instance {instanceId} (tenant {tenantId}), handing off", requestId);
                    await SendHandoffAsync(requestId, tenantId, chatId, message.Time,
                        "Bu hat icin aktif akis yok", 0, callbackUrl, ct);
                    return true;
                }
            }
            else
            {
                // Legacy: single-flow routing (first active flow)
                var (legacyDoc, legacyActive, legacyFlowId) = await _repo.GetFlowAsync(tenantId, ct);
                flowDoc = legacyDoc;
                isActive = legacyActive;
                rootFlowId = legacyFlowId;
            }

            if (flowDoc == null || !isActive)
            {
                _logger.StepWarn($"No active flow for tenant {tenantId}, handing off to human", requestId);
                await SendHandoffAsync(requestId, tenantId, chatId, message.Time,
                    "Chatbot akisi tanimlanmamis, mesaj temsilciye yonlendiriliyor", 0, callbackUrl, ct);
                return true;
            }

            // Version dispatch: check flow_config.version
            var isV2 = false;
            using (flowDoc)
            {
                if (flowDoc.RootElement.TryGetProperty("version", out var vProp) &&
                    vProp.ValueKind == JsonValueKind.Number && vProp.GetInt32() == 2)
                {
                    isV2 = true;
                }

                if (isV2)
                {
                    // v2 path: pure engine + orchestrator side-effects
                    return await ProcessV2MessageAsync(
                        flowDoc, rootFlowId, tenantId, chatId, phone, messageText,
                        message.Time, requestId, callbackUrl, resolvedInstanceId, sw, ct);
                }
            }

            // ============ v1 path (unchanged) ============
            var flow = await _flowEngine.GetActiveFlowAsync(tenantId, ct);
            if (flow == null)
            {
                _logger.StepWarn($"No active v1 flow for tenant {tenantId}, handing off", requestId);
                await SendHandoffAsync(requestId, tenantId, chatId, message.Time,
                    "Chatbot akisi tanimlanmamis", 0, callbackUrl, ct);
                return true;
            }

            // 2. Check working hours (v1)
            var (isWithinHours, offHoursMsg) = await _workingHours.CheckAsync(tenantId, ct);
            if (!isWithinHours)
            {
                var offReply = offHoursMsg ?? flow.OffHoursMessage ?? "Su anda mesai saatleri disindayiz. En kisa surede size donus yapacagiz.";
                sw.Stop();

                await SendCallbackAsync(requestId, tenantId, chatId, message.Time,
                    CallbackActions.SendMessage, offReply, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

                await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, offReply,
                    "off_hours", null, null, (int)sw.ElapsedMilliseconds, ct);

                _logger.StepInfo("Off-hours auto reply sent", requestId, sw.ElapsedMilliseconds);
                return true;
            }

            // 3. Get or create chat session (v1)
            var session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
            if (session == null)
            {
                await _repo.CreateSessionAsync(tenantId, chatId, phone, "welcome", ct);
                session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
            }

            // 4. Process through v1 flow engine
            var action = _flowEngine.ProcessInput(flow, session, messageText);

            switch (action.Type)
            {
                case FlowActionType.ShowWelcome:
                case FlowActionType.ShowMenu:
                case FlowActionType.StaticReply:
                case FlowActionType.UnknownInput:
                    sw.Stop();
                    await SendCallbackAsync(requestId, tenantId, chatId, message.Time,
                        CallbackActions.SendMessage, action.ReplyText!, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

                    var replyType = action.Type switch
                    {
                        FlowActionType.ShowWelcome => "welcome",
                        FlowActionType.ShowMenu => "menu",
                        FlowActionType.StaticReply => "menu",
                        _ => "menu"
                    };
                    await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, action.ReplyText,
                        replyType, null, null, (int)sw.ElapsedMilliseconds, ct);

                    if (session != null)
                        await _repo.UpdateSessionAsync(session.Id, action.NextNode, null, ct);

                    return true;

                case FlowActionType.FaqSearch:
                    return await HandleFaqSearchAsync(requestId, tenantId, chatId, message.Time,
                        phone, messageText, flow, session, callbackUrl, sw, ct);

                case FlowActionType.IntentDetection:
                    return await HandleIntentDetectionAsync(requestId, tenantId, chatId, message.Time,
                        phone, messageText, flow, session, callbackUrl, sw, ct);

                case FlowActionType.Handoff:
                    sw.Stop();
                    await SendHandoffAsync(requestId, tenantId, chatId, message.Time,
                        "Musteri temsilci ile gorusme talep etti", sw.ElapsedMilliseconds, callbackUrl, ct);

                    await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                        "handoff", null, null, (int)sw.ElapsedMilliseconds, ct);

                    if (session != null)
                        await _repo.EndSessionAsync(session.Id, "handed_off", ct);

                    return true;

                default:
                    _logger.SystemWarn($"Unknown flow action type: {action.Type}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.StepError($"Message processing failed: {ex.Message}", requestId, sw.ElapsedMilliseconds);

            // Send error callback so caller knows what went wrong (instead of silent timeout)
            try
            {
                var errorCallback = new OutgoingCallback
                {
                    RequestId = requestId,
                    Action = CallbackActions.Error,
                    TenantId = tenantId,
                    ChatId = chatId,
                    SequenceId = message.Time,
                    Data = new CallbackData { ErrorMessage = $"Processing error: {ex.Message}" },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
                await _callbackClient.SendCallbackAsync(errorCallback, callbackUrl, ct);
            }
            catch (Exception callbackEx)
            {
                _logger.SystemWarn($"Failed to send error callback: {callbackEx.Message}");
            }

            return false;
        }
    }

    /// <summary>
    /// Process a message through the v2 pure engine + side-effect layer.
    /// FlowEngineV2 is pure (no DB/HTTP). This method handles all side-effects.
    /// Supports sub-flow dispatch via CallStack in SessionStateV2.
    /// </summary>
    private async Task<bool> ProcessV2MessageAsync(
        JsonDocument flowDoc, int rootFlowId, int tenantId, string chatId, string? phone, string messageText,
        long sequenceId, string requestId, string? callbackUrl, string instanceId, Stopwatch sw, CancellationToken ct)
    {
        // 1. Build immutable graph (root flow)
        var graph = FlowGraphV2.Build(flowDoc);
        if (graph == null)
        {
            _logger.StepError($"[{ErrorCodes.AutomationInvalidFlowConfig}] Failed to build v2 graph for tenant {tenantId}", requestId);
            await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                "v2 akis konfigurasyonu gecersiz", sw.ElapsedMilliseconds, callbackUrl, ct);
            return true;
        }

        // 2. Check working hours
        var (isWithinHours, offHoursMsg) = await _workingHours.CheckAsync(tenantId, ct);
        if (!isWithinHours)
        {
            var offReply = offHoursMsg ?? graph.Settings.OffHoursMessage
                ?? "Su anda mesai saatleri disindayiz. En kisa surede size donus yapacagiz.";
            sw.Stop();

            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, offReply, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);
            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, offReply,
                "off_hours", null, null, (int)sw.ElapsedMilliseconds, ct);

            _logger.StepInfo("Off-hours auto reply sent (v2)", requestId, sw.ElapsedMilliseconds);
            return true;
        }

        // 3. Get or create session + restore v2 state
        var session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
        SessionStateV2 state;
        var currentFlowId = rootFlowId;

        if (session == null)
        {
            // New conversation: create session, start from trigger_start
            if (graph.TriggerStart == null)
            {
                _logger.StepError($"v2 flow has no trigger_start for tenant {tenantId}", requestId);
                await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                    "v2 akis baslangic noktasi bulunamadi", sw.ElapsedMilliseconds, callbackUrl, ct);
                return true;
            }

            state = new SessionStateV2 { CurrentNodeId = graph.TriggerStart.Id };
            await _repo.CreateSessionAsync(tenantId, chatId, phone, "v2_active", ct);
            session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
        }
        else
        {
            // Returning user: deserialize v2 state from session_data
            state = DeserializeV2State(session?.SessionData);
            if (state == null)
            {
                state = new SessionStateV2
                {
                    CurrentNodeId = graph.TriggerStart?.Id ?? ""
                };
            }

            // Guard: if session's current node no longer exists in the active flow graph
            // (e.g. tenant changed flow while session was active), restart from trigger
            if (state.CallStack.Count == 0
                && !state.ActiveFlowId.HasValue
                && !string.IsNullOrEmpty(state.CurrentNodeId)
                && !graph.NodesById.ContainsKey(state.CurrentNodeId))
            {
                _logger.StepInfo($"Flow graph changed: node '{state.CurrentNodeId}' missing. Resetting to trigger start for tenant {tenantId}.", requestId);
                state.CurrentNodeId = graph.TriggerStart?.Id ?? "";
            }

            // Sub-flow resume: if session was paused inside a sub-flow, load that flow's graph
            if (state.ActiveFlowId.HasValue)
            {
                var subFlowDetail = await _repo.GetFlowByIdAsync(tenantId, state.ActiveFlowId.Value, ct);
                if (subFlowDetail != null)
                {
                    var subGraph = FlowGraphV2.Build(subFlowDetail.FlowConfigJson);
                    if (subGraph != null)
                    {
                        graph = subGraph;
                        currentFlowId = state.ActiveFlowId.Value;
                    }
                    else
                    {
                        _logger.SystemWarn($"Sub-flow {state.ActiveFlowId.Value} graph build failed, resetting to root");
                        state.CallStack.Clear();
                        state.ActiveFlowId = null;
                        state.CurrentNodeId = graph.TriggerStart?.Id ?? "";
                    }
                }
                else
                {
                    _logger.SystemWarn($"Sub-flow {state.ActiveFlowId.Value} not found, resetting to root");
                    state.CallStack.Clear();
                    state.ActiveFlowId = null;
                    state.CurrentNodeId = graph.TriggerStart?.Id ?? "";
                }
            }

            state.Variables["__last_input"] = messageText;

            // Reset keyword: restart flow from beginning
            if (graph.Settings.ResetKeywords.Contains(messageText.Trim()))
            {
                _logger.StepInfo($"User triggered flow reset via keyword '{messageText.Trim()}'", requestId);
                state = new SessionStateV2 { CurrentNodeId = graph.TriggerStart?.Id ?? "" };
            }
        }

        // 4a. PKT-6A: Fetch tenant intents from Knowledge (graceful degradation: null = defaults)
        string[]? tenantIntents = null;
        double tenantConfidenceThreshold = 0.5;
        string? settingsJson = null;
        try
        {
            var serviceJwt = _jwtGenerator.GenerateServiceToken(tenantId);
            tenantIntents = await _knowledgeIntentClient.GetTenantIntentsAsync(tenantId, serviceJwt, ct);
            settingsJson = await _repo.GetTenantSettingsJsonAsync(tenantId, ct);
            tenantConfidenceThreshold = ExtractConfidenceThreshold(settingsJson);
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Pre-flow enrichment failed for tenant {tenantId}: {ex.Message}");
        }

        // 4b. Execute pure engine (no streaming — messages sent in order after execution)
        var pathSnapshotCount = state.ExecutionPath.Count;
        var result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
            tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
            onMessage: null);

        // Sub-flow dispatch loop: handle CallSubFlow and sub-flow completion
        const int maxSubFlowDepth = 5;
        var subFlowLoopGuard = 0;
        const int maxSubFlowLoopIterations = 20; // prevent runaway loops (depth * 2 round-trips)

        while (subFlowLoopGuard++ < maxSubFlowLoopIterations)
        {
            // Case 1: Engine requests a sub-flow call
            if (result.SubFlowRequest != null)
            {
                if (state.CallStack.Count >= maxSubFlowDepth)
                {
                    _logger.SystemWarn($"Sub-flow call depth exceeded {maxSubFlowDepth} at node {result.SubFlowRequest.NodeId}");
                    state.Status = "error";
                    result = new EngineStepResult
                    {
                        Messages = result.Messages,
                        State = state,
                        IsTerminal = true,
                        NeedsHandoff = true,
                        ErrorCode = ErrorCodes.AutomationMaxLoopExceeded,
                        ErrorMessage = "Alt akis cagri derinligi limiti asildi"
                    };
                    break;
                }

                var callNode = graph.NodesById[result.SubFlowRequest.NodeId];
                var targetFlowIdStr = callNode.GetData("flow_id");
                if (!int.TryParse(targetFlowIdStr, out var targetFlowId))
                {
                    _logger.SystemWarn($"Invalid flow_id '{targetFlowIdStr}' in call_flow node {result.SubFlowRequest.NodeId}");
                    state.Variables["__sub_flow_error"] = "true";
                    state.Variables["__sub_flow_completed"] = "true";
                    result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
                        tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
                        onMessage: null);
                    continue;
                }

                // Load target sub-flow
                var subFlowDetail = await _repo.GetFlowByIdAsync(tenantId, targetFlowId, ct);
                if (subFlowDetail == null)
                {
                    _logger.SystemWarn($"Sub-flow {targetFlowId} not found for tenant {tenantId}");
                    state.Variables["__sub_flow_error"] = "true";
                    state.Variables["__sub_flow_completed"] = "true";
                    result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
                        tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
                        onMessage: null);
                    continue;
                }

                var subGraph = FlowGraphV2.Build(subFlowDetail.FlowConfigJson);
                if (subGraph == null || subGraph.TriggerStart == null)
                {
                    _logger.SystemWarn($"Sub-flow {targetFlowId} has invalid config or no trigger");
                    state.Variables["__sub_flow_error"] = "true";
                    state.Variables["__sub_flow_completed"] = "true";
                    result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
                        tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
                        onMessage: null);
                    continue;
                }

                // Parse input/output mapping from call_flow node data
                var inputMap = ParseVariableMap(callNode.GetData("input_map"));
                var outputMap = ParseVariableMap(callNode.GetData("output_map"));

                // Push parent context to call stack
                state.CallStack.Add(new FlowCallFrame
                {
                    FlowId = currentFlowId,
                    ReturnNodeId = result.SubFlowRequest.NodeId,
                    Variables = new Dictionary<string, string>(state.Variables),
                    ExecutionPath = new List<string>(state.ExecutionPath),
                    LoopCounters = new Dictionary<string, int>(state.LoopCounters),
                    OutputMap = outputMap
                });

                // Create sub-flow state with input mapping
                var subFlowVars = new Dictionary<string, string>();
                if (inputMap != null)
                {
                    foreach (var (parentVar, childVar) in inputMap)
                    {
                        if (state.Variables.TryGetValue(parentVar, out var val))
                            subFlowVars[childVar] = val;
                    }
                }

                state.CurrentNodeId = subGraph.TriggerStart.Id;
                state.Variables = subFlowVars;
                state.ExecutionPath = new List<string>();
                state.LoopCounters = new Dictionary<string, int>();
                state.PendingInput = null;
                state.ActiveFlowId = targetFlowId;
                state.Status = "active";

                graph = subGraph;
                currentFlowId = targetFlowId;

                _logger.StepInfo($"Entering sub-flow {targetFlowId} (depth {state.CallStack.Count})", requestId);

                result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
                    tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
                    onMessage: null);
                continue;
            }

            // Case 2: Sub-flow completed — pop call stack and resume parent
            if (result.IsTerminal && state.CallStack.Count > 0 && !result.NeedsHandoff)
            {
                var frame = state.CallStack[^1];
                state.CallStack.RemoveAt(state.CallStack.Count - 1);

                // Restore parent variables + apply output mapping
                var parentVars = new Dictionary<string, string>(frame.Variables);
                if (frame.OutputMap != null)
                {
                    foreach (var (childVar, parentVar) in frame.OutputMap)
                    {
                        if (state.Variables.TryGetValue(childVar, out var val))
                            parentVars[parentVar] = val;
                    }
                }

                // Check if sub-flow ended with error
                var subFlowHadError = result.ErrorCode != null;

                state.Variables = parentVars;
                state.ExecutionPath = frame.ExecutionPath;
                state.LoopCounters = frame.LoopCounters;
                state.CurrentNodeId = frame.ReturnNodeId;
                state.ActiveFlowId = state.CallStack.Count > 0 ? state.CallStack[^1].FlowId : (int?)null;
                state.PendingInput = null;
                state.Status = "active";

                if (subFlowHadError)
                    state.Variables["__sub_flow_error"] = "true";
                state.Variables["__sub_flow_completed"] = "true";

                // Load parent flow graph
                FlowGraphV2? parentGraph;
                if (state.CallStack.Count == 0 && frame.FlowId == rootFlowId)
                {
                    // Root flow — reuse the original flowDoc
                    parentGraph = FlowGraphV2.Build(flowDoc);
                }
                else
                {
                    var parentDetail = await _repo.GetFlowByIdAsync(tenantId, frame.FlowId, ct);
                    parentGraph = parentDetail != null ? FlowGraphV2.Build(parentDetail.FlowConfigJson) : null;
                }

                if (parentGraph == null)
                {
                    _logger.SystemWarn($"Parent flow {frame.FlowId} graph build failed after sub-flow return");
                    state.Status = "error";
                    result = new EngineStepResult
                    {
                        Messages = result.Messages,
                        State = state,
                        IsTerminal = true,
                        NeedsHandoff = true,
                        ErrorMessage = "Ust akis yuklenemedi"
                    };
                    break;
                }

                graph = parentGraph;
                currentFlowId = frame.FlowId;

                _logger.StepInfo($"Returning to parent flow {frame.FlowId} from sub-flow (depth {state.CallStack.Count})", requestId);

                result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
                    tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
                    onMessage: null);
                continue;
            }

            // No sub-flow action — exit loop
            break;
        }

        // 4c. Fire-and-forget execution log
        _ = LogFlowExecutionAsync(
            state, result, graph, tenantId, rootFlowId, chatId, phone,
            instanceId, messageText, pathSnapshotCount, requestId);

        // 5. Side-effects: send messages in order (sequential to preserve delivery order)
        if (result.Messages.Count > 0)
        {
            sw.Stop();
            foreach (var msg in result.Messages)
            {
                await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                    CallbackActions.SendMessage, msg, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);
            }

            var combinedMessage = string.Join("\n\n", result.Messages);
            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, combinedMessage,
                "v2_flow", null, null, (int)sw.ElapsedMilliseconds, ct);
        }

        // 6. Side-effects: handle terminal states (fire-and-forget for handoff callbacks)
        if (result.NeedsHandoff)
        {
            if (!sw.IsRunning) sw.Stop();
            var summary = result.HandoffSummary ?? result.ErrorMessage ?? "v2 flow handoff";
            _ = SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                summary, sw.ElapsedMilliseconds, callbackUrl, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.SystemWarn($"Handoff callback failed: {t.Exception?.InnerException?.Message}");
                }, TaskScheduler.Default);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "handed_off", ct);

            return true;
        }

        if (result.IsTerminal && result.ErrorCode != null)
        {
            if (!sw.IsRunning) sw.Stop();
            _ = SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                result.ErrorMessage ?? "v2 engine error", sw.ElapsedMilliseconds, callbackUrl, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.SystemWarn($"Error handoff callback failed: {t.Exception?.InnerException?.Message}");
                }, TaskScheduler.Default);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "error", ct);

            return true;
        }

        if (result.IsTerminal)
        {
            if (session != null)
                await _repo.EndSessionAsync(session.Id, "completed", ct);

            return true;
        }

        // 7. Side-effects: save session state for next message
        if (session != null)
        {
            var stateJson = SerializeV2State(result.State);
            await _repo.UpdateSessionAsync(session.Id, "v2_active", stateJson, ct);
        }

        // 8. PKT-6A: Post-flow side-effects (fire-and-forget, non-blocking)
        if (result.State.Variables.TryGetValue("detected_intent", out var detectedIntent)
            && !string.IsNullOrWhiteSpace(detectedIntent) && detectedIntent != "unknown")
        {
            _ = SendAutoTagCallbackAsync(requestId, tenantId, chatId, sequenceId,
                detectedIntent, callbackUrl, ct);
        }

        if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(messageText))
        {
            _ = _vipDetection.CheckAndRecordAsync(tenantId, phone, messageText, settingsJson, CancellationToken.None);
        }

        return true;
    }

    /// <summary>
    /// Parse a JSON variable map from node data. Format: {"sourceVar": "targetVar", ...}
    /// Returns null if empty/invalid.
    /// </summary>
    private static Dictionary<string, string>? ParseVariableMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return null;

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return map?.Count > 0 ? map : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Fire-and-forget: create or update flow_execution_log with node trace from this step.
    /// </summary>
    private async Task LogFlowExecutionAsync(
        SessionStateV2 state, EngineStepResult result, FlowGraphV2 graph,
        int tenantId, int flowId, string chatId, string? phone,
        string instanceId, string messageText, int pathSnapshotCount, string requestId)
    {
        try
        {
            // Build trace entries for nodes visited in THIS step only
            var newNodeIds = state.ExecutionPath.Skip(pathSnapshotCount).ToList();
            if (newNodeIds.Count == 0 && !state.ExecutionLogId.HasValue)
                return; // nothing to log

            var now = DateTime.UtcNow;
            var traceEntries = new List<object>(newNodeIds.Count);
            for (var i = 0; i < newNodeIds.Count; i++)
            {
                var nodeId = newNodeIds[i];
                string? nodeType = null, label = null;
                if (graph.NodesById.TryGetValue(nodeId, out var node))
                {
                    nodeType = node.Type;
                    label = node.GetData("label");
                }
                var entry = new Dictionary<string, object?>
                {
                    ["node_id"] = nodeId,
                    ["node_type"] = nodeType,
                    ["label"] = label,
                    ["entered_at"] = now.ToString("o"),
                    ["exit_handle"] = (object?)null,
                    ["duration_ms"] = (object?)null,
                };
                // First node: attach user input
                if (i == 0)
                    entry["user_input"] = messageText;
                // Last node: attach bot messages + variable snapshot
                if (i == newNodeIds.Count - 1)
                {
                    if (result.Messages.Count > 0)
                        entry["bot_messages"] = result.Messages;
                    entry["variables"] = new Dictionary<string, string>(state.Variables);
                }
                traceEntries.Add(entry);
            }

            var traceJson = JsonSerializer.Serialize(traceEntries, _jsonOptions);

            // Determine execution status
            var status = result.IsTerminal
                ? (result.NeedsHandoff ? "handed_off" : (result.ErrorCode != null ? "error" : "completed"))
                : (result.State.PendingInput != null ? "waiting" : "running");

            string? variablesJson = result.IsTerminal
                ? JsonSerializer.Serialize(state.Variables, _jsonOptions)
                : null;

            if (state.ExecutionLogId.HasValue)
            {
                // Existing log — append trace
                await _repo.UpdateExecutionLogAsync(
                    state.ExecutionLogId.Value, tenantId, traceJson, status,
                    variablesJson, result.ErrorMessage);
            }
            else
            {
                // New log — insert
                var logId = await _repo.CreateExecutionLogAsync(
                    tenantId, flowId, chatId, phone, instanceId, messageText, traceJson);
                state.ExecutionLogId = logId;

                // If terminal on first message, also set completed_at
                if (result.IsTerminal && logId > 0)
                {
                    await _repo.UpdateExecutionLogAsync(logId, tenantId, "[]", status, variablesJson, result.ErrorMessage);
                }
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationExecLogInsertFailed}] Execution log DB error for tenant {tenantId}: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationExecLogInsertFailed}] Execution log serialization error for tenant {tenantId}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationExecLogInsertFailed}] Execution log operation error for tenant {tenantId}: {ex.Message}");
        }
    }

    private SessionStateV2? DeserializeV2State(string? sessionDataJson)
    {
        if (string.IsNullOrEmpty(sessionDataJson) || sessionDataJson == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionStateV2>(sessionDataJson, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"v2 session state deserialize failed: {ex.Message}. Input length={sessionDataJson?.Length}");
            return null;
        }
    }

    private static string SerializeV2State(SessionStateV2 state)
    {
        return JsonSerializer.Serialize(state, _jsonOptions);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private async Task<bool> HandleFaqSearchAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string? phone, string messageText, FlowConfig flow, ChatSession? session,
        string? callbackUrl, Stopwatch sw, CancellationToken ct)
    {
        // If this is the first entry to FAQ mode, prompt user to ask their question
        if (session?.CurrentNode != "faq")
        {
            sw.Stop();
            var promptMsg = "Sorunuzu yazin, size en uygun cevabi bulayim. Ana menuye donmek icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, promptMsg, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

            if (session != null)
                await _repo.UpdateSessionAsync(session.Id, "faq", null, ct);

            return true;
        }

        // Search FAQs
        var faqMatch = await _faqMatcher.FindMatchAsync(tenantId, messageText, ct);
        if (faqMatch != null && faqMatch.Confidence >= 0.3)
        {
            sw.Stop();
            var replyText = faqMatch.Answer + "\n\nBaska bir sorunuz var mi? Ana menu icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, replyText, "faq_match", faqMatch.Confidence, sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, faqMatch.Answer,
                "faq", "faq_match", faqMatch.Confidence, (int)sw.ElapsedMilliseconds, ct);

            return true;
        }

        // No FAQ match -> fallback to intent detection
        return await HandleIntentDetectionAsync(requestId, tenantId, chatId, sequenceId,
            phone, messageText, flow, session, callbackUrl, sw, ct);
    }

    private async Task<bool> HandleIntentDetectionAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string? phone, string messageText, FlowConfig flow, ChatSession? session,
        string? callbackUrl, Stopwatch sw, CancellationToken ct)
    {
        // If first entry to intent mode, prompt user
        if (session?.CurrentNode != "intent" && session?.CurrentNode != "faq")
        {
            sw.Stop();
            var promptMsg = "Sorunuzu veya talebinizi yazin. Ana menuye donmek icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, promptMsg, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

            if (session != null)
                await _repo.UpdateSessionAsync(session.Id, "intent", null, ct);

            return true;
        }

        // Run Claude intent detection
        var intentResult = await _intentDetector.DetectAsync(messageText, null, ct);

        if (intentResult == null)
        {
            // AI failed -> handoff (graceful degradation)
            sw.Stop();
            _logger.StepWarn("Intent detection returned null, falling back to handoff", requestId, sw.ElapsedMilliseconds);

            await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                "Niyet algilama basarisiz, temsilciye yonlendiriliyor", sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                "handoff", null, null, (int)sw.ElapsedMilliseconds, ct);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "handed_off", ct);

            return true;
        }

        // Check confidence threshold
        if (intentResult.Confidence < flow.HandoffConfidenceThreshold)
        {
            sw.Stop();
            await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                $"Dusuk guven ({intentResult.Confidence:F2}): {intentResult.Summary}",
                sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                "handoff", intentResult.Intent, intentResult.Confidence, (int)sw.ElapsedMilliseconds, ct);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "handed_off", ct);

            return true;
        }

        // High confidence -> send auto-reply with suggest_reply (let agent review)
        sw.Stop();
        var suggestionText = $"[AI {intentResult.Intent} ({intentResult.Confidence:F2})]: {intentResult.Summary}";

        await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
            CallbackActions.SuggestReply, suggestionText, intentResult.Intent, intentResult.Confidence,
            sw.ElapsedMilliseconds, callbackUrl, ct);

        await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, suggestionText,
            "intent", intentResult.Intent, intentResult.Confidence, (int)sw.ElapsedMilliseconds, ct);

        if (session != null)
            await _repo.UpdateSessionAsync(session.Id, "menu", null, ct);

        return true;
    }

    private async Task<bool> SendCallbackAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string action, string messageText, string? intent, double? confidence,
        long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        // GR-2.6.1: Append KVKK health disclaimer for SendMessage actions only
        var finalMessageText = messageText;
        if (action == CallbackActions.SendMessage && !string.IsNullOrEmpty(messageText))
        {
            var isHealth = await IsHealthTenantCachedAsync(tenantId, ct);
            finalMessageText = KvkkHelper.AppendDisclaimerIfHealth(messageText, isHealth);
        }

        var callback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = action,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData
            {
                MessageText = action == CallbackActions.SendMessage ? finalMessageText : null,
                SuggestedReply = action == CallbackActions.SuggestReply ? messageText : null,
                Intent = intent,
                Confidence = confidence
            },
            ProcessingTimeMs = processingTimeMs
        };

        var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
        if (!delivered)
            _logger.StepError($"[{ErrorCodes.IntegrationCallbackFailed}] Callback delivery failed: action={action}, tenant={tenantId}, chat={chatId}", requestId, processingTimeMs);
        return delivered;
    }

    /// <summary>
    /// GR-2.6: Check if tenant is a health tenant with in-memory cache.
    /// Cache is per-process lifetime (ConcurrentDictionary). Acceptable for tenant settings
    /// that change rarely. Service restart clears cache.
    /// </summary>
    private async Task<bool> IsHealthTenantCachedAsync(int tenantId, CancellationToken ct)
    {
        if (_healthTenantCache.TryGetValue(tenantId, out var cached))
            return cached;

        var (settingsJson, sector) = await _repo.GetTenantHealthInfoAsync(tenantId, ct);
        var isHealth = KvkkHelper.IsHealthTenant(settingsJson, sector);
        _healthTenantCache.TryAdd(tenantId, isHealth);
        return isHealth;
    }

    /// <summary>
    /// PKT-6A: Send auto-tag callback to Backend for detected intent.
    /// Uses existing CallbackActions.ApplyTag + CallbackData.TagName. Fire-and-forget.
    /// </summary>
    private async Task SendAutoTagCallbackAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string intentName, string? callbackUrl, CancellationToken ct)
    {
        try
        {
            var tagName = $"intent:{intentName}";
            var callback = new OutgoingCallback
            {
                RequestId = requestId,
                Action = CallbackActions.ApplyTag,
                TenantId = tenantId,
                ChatId = chatId,
                SequenceId = sequenceId,
                Data = new CallbackData
                {
                    TagName = tagName,
                    Intent = intentName
                },
                ProcessingTimeMs = 0
            };

            var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
            if (!delivered)
                _logger.SystemWarn($"Auto-tag callback failed: tenant={tenantId}, chat={chatId}, tag={tagName}");
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Auto-tag callback error: tenant={tenantId}, chat={chatId}: {ex.Message}");
        }
    }

    /// <summary>
    /// PKT-6A: Extract confidence_threshold from tenant settings_json.
    /// Returns 0.5 default if not found or invalid.
    /// </summary>
    private static double ExtractConfidenceThreshold(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return 0.5;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("confidence_threshold", out var threshEl)
                && threshEl.ValueKind == JsonValueKind.Number)
            {
                var value = threshEl.GetDouble();
                return value is > 0 and <= 1.0 ? value : 0.5;
            }
        }
        catch (JsonException) { }

        return 0.5;
    }

    private async Task<bool> SendHandoffAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string aiSummary, long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        var callback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = CallbackActions.HandoffToHuman,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData
            {
                HandoffToHuman = true,
                AiSummary = aiSummary
            },
            ProcessingTimeMs = processingTimeMs
        };

        var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
        if (!delivered)
            _logger.StepError($"[{ErrorCodes.IntegrationCallbackFailed}] Handoff callback delivery failed: tenant={tenantId}, chat={chatId}", requestId, processingTimeMs);
        return delivered;
    }
}
