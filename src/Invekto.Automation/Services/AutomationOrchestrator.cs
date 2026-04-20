using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Automation.Services.NodeHandlers;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma.Webhooks;
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
    private readonly FlowWaitRepository _waitRepo;
    private readonly FlowEngine _flowEngine;
    private readonly FlowEngineV2 _flowEngineV2;
    private readonly KnowledgeSearchClient _knowledgeSearchClient;
    private readonly IntentDetector _intentDetector;
    private readonly WorkingHoursChecker _workingHours;
    private readonly MainAppCallbackClient _callbackClient;
    private readonly KnowledgeIntentClient _knowledgeIntentClient;
    private readonly VipDetectionService _vipDetection;
    private readonly ReviewRescueService _reviewRescue;
    private readonly JwtGenerator _jwtGenerator;
    private readonly BackendIntakeClient _backendIntake;
    private readonly TenantSettingsRepository _tenantSettings;
    private readonly JsonLinesLogger _logger;

    // GR-2.6: KVKK health tenant cache (tenant_id -> isHealthTenant)
    private readonly ConcurrentDictionary<int, bool> _healthTenantCache = new();

    // Perf: cache tenant intents + settings to avoid HTTP/DB on every message
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<int, (string[]? Intents, DateTime ExpiresAt)> _intentCache = new();
    private readonly ConcurrentDictionary<int, (string? SettingsJson, DateTime ExpiresAt)> _settingsCache = new();

    public AutomationOrchestrator(
        AutomationRepository repo,
        FlowWaitRepository waitRepo,
        FlowEngine flowEngine,
        FlowEngineV2 flowEngineV2,
        KnowledgeSearchClient knowledgeSearchClient,
        IntentDetector intentDetector,
        WorkingHoursChecker workingHours,
        MainAppCallbackClient callbackClient,
        KnowledgeIntentClient knowledgeIntentClient,
        VipDetectionService vipDetection,
        ReviewRescueService reviewRescue,
        JwtGenerator jwtGenerator,
        BackendIntakeClient backendIntake,
        TenantSettingsRepository tenantSettings,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _waitRepo = waitRepo;
        _flowEngine = flowEngine;
        _flowEngineV2 = flowEngineV2;
        _knowledgeSearchClient = knowledgeSearchClient;
        _intentDetector = intentDetector;
        _workingHours = workingHours;
        _callbackClient = callbackClient;
        _knowledgeIntentClient = knowledgeIntentClient;
        _vipDetection = vipDetection;
        _reviewRescue = reviewRescue;
        _jwtGenerator = jwtGenerator;
        _backendIntake = backendIntake;
        _tenantSettings = tenantSettings;
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

            // FEAT-LIW Chunk B: WA-direct lead intake hook. Every inbound that
            // makes it past flow routing (so handoff messages are excluded — no
            // lead row when chat won't run) gets registered with Backend. The
            // call is idempotent server-side: EnsureLeadForWaDirectAsync probes
            // the dup window in a single CTE and short-circuits when a recent
            // row exists, so this is safe to fire on every message — repeat
            // inbounds add no row churn beyond a tenant-scoped index seek.
            // Failure is best-effort: leadId stays null, INV-AT-070 is logged
            // by BackendIntakeClient, and the flow path proceeds unchanged so
            // the user still gets a chat reply. The Stopwatch already running
            // since line 89 captures any added latency in the existing
            // StepInfo emission downstream — no separate timing knob needed.
            int? waDirectLeadId = null;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var intakePayload = new Invekto.Shared.Contracts.Leads.WaDirectIntakeRequest
                {
                    TenantId = tenantId,
                    Phone = phone,
                    ProfileName = message.SenderName,
                    Referer = null, // INMA inbound payload doesn't currently carry ctwa_clid; left null intentionally so reports don't see synthetic attribution.
                    ReceivedAt = null // message.Time is a Unix epoch long with ambiguous unit (s vs ms across senders); Backend defaults to NOW() when null, which is good enough for the second-resolution intake_metadata snapshot.
                };
                var intakeResult = await _backendIntake.IntakeAsync(intakePayload, requestId, ct);
                waDirectLeadId = intakeResult?.LeadId;
                if (intakeResult != null)
                {
                    // Correlation log only — flow execution does not yet bind
                    // lead_id into FlowEngineV2 context (Chunk C will wire
                    // {{lead.*}} substitution); for now the audit trail in logs
                    // + intake_metadata is enough for ops to join. The
                    // non-null check here (instead of the indirect
                    // `waDirectLeadId.HasValue` we previously used) keeps the
                    // analyzer happy without any null-forgiving operator —
                    // intakeResult.IsNew / .WelcomeFlowEnqueued are now
                    // statically known to be safe accesses.
                    _logger.StepInfo(
                        $"WA-direct intake: tenant={tenantId} phone={phone} lead={intakeResult.LeadId} " +
                        $"isNew={intakeResult.IsNew} welcomeEnqueued={intakeResult.WelcomeFlowEnqueued}",
                        requestId);
                }
                else
                {
                    // Iter 2 fix for CQ1+CQ2: BackendIntakeClient already logs the
                    // transport-level reason (HTTP code / parse error / timeout)
                    // under INV-AT-070, but that log sits in Automation's logs
                    // without orchestrator context (tenant/phone/chat). This
                    // call-site warn ensures an ops query for "why was no lead
                    // created for chat X?" returns a single line with the full
                    // join key, even when the upstream log was rotated or the
                    // operator is grepping at the message-pipeline level.
                    _logger.StepWarn(
                        $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] WA-direct intake skipped: " +
                        $"tenant={tenantId} chat={chatId} phone={phone} (Backend intake returned no result; " +
                        $"chat reply path continues with leadId=null)",
                        requestId);
                }
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

            // G6: user reply during long wait → cancel pending wait row(s) so resumer won't fire later.
            // Typed catches per CODEX UTANSIN doktrini (no bare catch(Exception)).
            try
            {
                var cancelled = await _waitRepo.CancelPendingForChatAsync(tenantId, chatId, ct);
                if (cancelled > 0)
                    _logger.StepInfo($"G6: {cancelled} pending long-wait cancelled (user reply)", requestId);
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitPersistFailed}] CancelPendingForChatAsync DB error tenant={tenantId} chat={chatId}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitPersistFailed}] CancelPendingForChatAsync invalid state tenant={tenantId} chat={chatId}: {ex.Message}");
            }

            // Reset keyword: restart flow from beginning
            if (graph.Settings.ResetKeywords.Contains(messageText.Trim()))
            {
                _logger.StepInfo($"User triggered flow reset via keyword '{messageText.Trim()}'", requestId);
                state = new SessionStateV2 { CurrentNodeId = graph.TriggerStart?.Id ?? "" };
            }
        }

        // 4a. PKT-6A: Fetch tenant intents + settings (cached, parallel on miss)
        string[]? tenantIntents = null;
        double tenantConfidenceThreshold = 0.5;
        string? settingsJson = null;
        try
        {
            var now = DateTime.UtcNow;
            var intentsCached = _intentCache.TryGetValue(tenantId, out var ic) && ic.ExpiresAt > now;
            var settingsCached = _settingsCache.TryGetValue(tenantId, out var sc) && sc.ExpiresAt > now;

            if (intentsCached && settingsCached)
            {
                tenantIntents = ic.Intents;
                settingsJson = sc.SettingsJson;
            }
            else
            {
                var serviceJwt = _jwtGenerator.GenerateServiceToken(tenantId);
                var intentTask = intentsCached
                    ? Task.FromResult(ic.Intents)
                    : _knowledgeIntentClient.GetTenantIntentsAsync(tenantId, serviceJwt, ct);
                var settingsTask = settingsCached
                    ? Task.FromResult(sc.SettingsJson)
                    : _repo.GetTenantSettingsJsonAsync(tenantId, ct);

                await Task.WhenAll(intentTask, settingsTask);
                tenantIntents = intentTask.Result;
                settingsJson = settingsTask.Result;

                var expiry = now.Add(CacheTtl);
                if (!intentsCached)
                    _intentCache[tenantId] = (tenantIntents, expiry);
                if (!settingsCached)
                    _settingsCache[tenantId] = (settingsJson, expiry);
            }

            tenantConfidenceThreshold = ExtractConfidenceThreshold(settingsJson);
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Pre-flow enrichment failed for tenant {tenantId}: {ex.Message}");
        }

        // 4b. Execute pure engine (no streaming — messages sent in order after execution)
        var pathSnapshotCount = state.ExecutionPath.Count;
        // G3: stable contact key for deterministic template A/B rotation.
        // Priority: chatId -> phone -> "flow:{rootFlowId}" (per-flow fallback per plan AC3,
        // so unknown contacts at least vary across flows instead of all collapsing to one node-only bucket).
        string contactKey;
        if (!string.IsNullOrEmpty(chatId)) contactKey = chatId;
        else if (!string.IsNullOrEmpty(phone)) contactKey = phone;
        else contactKey = $"flow:{rootFlowId}";

        // HFM-2: resolve lead.preferred_locale (sticky). Detect + upsert when missing so
        // downstream handlers (AiFaqHandler translation hop, AiIntentHandler i18n prompts)
        // see the target language on the same message that triggered detection.
        var leadPreferredLocale = await ResolveLeadPreferredLocaleAsync(tenantId, phone, messageText, requestId, ct);

        var result = await _flowEngineV2.ExecuteAsync(graph, state, ct, tenantId: tenantId,
            tenantIntents: tenantIntents, tenantConfidenceThreshold: tenantConfidenceThreshold,
            onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);

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
                        onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);
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
                        onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);
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
                        onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);
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
                    onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);
                continue;
            }

            // Case 2: Sub-flow completed — pop call stack and resume parent
            if (result.IsTerminal && state.CallStack.Count > 0 && !result.NeedsHandoff && !result.NeedsAssignGroup)
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
                    onMessage: null, contactKey: contactKey, leadPreferredLocale: leadPreferredLocale);
                continue;
            }

            // No sub-flow action — exit loop
            break;
        }

        // G6: Long-wait persistence. Engine non-terminal + WaitRequest set → snapshot session to flow_execution_state, return without sending messages (if any were queued, they are still dispatched below for UX continuity).
        if (!result.IsTerminal && result.WaitRequest != null)
        {
            var waitOk = await PersistWaitAsync(
                result.State, result.WaitRequest, tenantId, currentFlowId, chatId, phone, instanceId, callbackUrl, requestId, ct);

            // Fire-and-forget exec log for trace visibility even when waiting
            _ = LogFlowExecutionAsync(
                state, result, graph, tenantId, rootFlowId, chatId, phone,
                instanceId, messageText, pathSnapshotCount, requestId);

            // Dispatch any queued pre-wait messages (e.g. "Mesajınızı aldık, 48 saat içinde dönüş yapacağız.")
            if (result.Messages.Count > 0)
            {
                sw.Stop();
                foreach (var msg in result.Messages)
                {
                    await DispatchMessageOrChunksAsync(requestId, tenantId, chatId, sequenceId,
                        msg, sw.ElapsedMilliseconds, callbackUrl, ct);
                }
            }

            // Persist session so returning user (before resume) sees waiting state.
            if (session != null)
            {
                var stateJson = SerializeV2State(result.State);
                await _repo.UpdateSessionAsync(session.Id, "v2_active", stateJson, ct);
            }

            if (!waitOk)
            {
                // Persist failed → degrade to handoff so user is not silently stuck.
                if (!sw.IsRunning) sw.Stop();
                _ = SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                    "Bekleme durumu kaydedilemedi", sw.ElapsedMilliseconds, callbackUrl, CancellationToken.None);
            }
            return true;
        }

        // 4c. Fire-and-forget execution log
        _ = LogFlowExecutionAsync(
            state, result, graph, tenantId, rootFlowId, chatId, phone,
            instanceId, messageText, pathSnapshotCount, requestId);

        // 5. Side-effects: send messages in order (sequential to preserve delivery order).
        // HFM-1: per-message dispatch detects chunk sentinel and applies inter-chunk
        // delays (insan hissi). Plain messages fall through to the legacy callback path.
        if (result.Messages.Count > 0)
        {
            sw.Stop();
            foreach (var msg in result.Messages)
            {
                await DispatchMessageOrChunksAsync(requestId, tenantId, chatId, sequenceId,
                    msg, sw.ElapsedMilliseconds, callbackUrl, ct);
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

        // action_assign_group: send assign_group callback, end session
        if (result.NeedsAssignGroup)
        {
            if (!sw.IsRunning) sw.Stop();
            var groupId = result.AssignGroupId ?? "";
            var summary = result.AssignGroupSummary ?? "Grup atamasi";

            if (!string.IsNullOrWhiteSpace(groupId))
            {
                _ = SendAssignGroupAsync(requestId, tenantId, chatId, sequenceId,
                    groupId, summary, sw.ElapsedMilliseconds, callbackUrl, CancellationToken.None)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.SystemWarn($"AssignGroup callback failed: {t.Exception?.InnerException?.Message}");
                    }, TaskScheduler.Default);
            }
            else
            {
                _logger.SystemWarn($"action_assign_group node has empty group_id for tenant {tenantId}, chat {chatId}");
            }

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

            // PKT-12: Review Rescue risk scoring (fire-and-forget)
            _ = _reviewRescue.ScoreAndProcessAsync(
                tenantId, phone, messageText, result.State.Variables,
                settingsJson, CancellationToken.None);
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
                // G3: attach template A/B variant info when MessageTextHandler recorded it
                if (state.Variables.TryGetValue($"__variant_index:{nodeId}", out var vIdx) &&
                    state.Variables.TryGetValue($"__variant_count:{nodeId}", out var vCnt))
                {
                    if (int.TryParse(vIdx, out var vIdxInt)) entry["variant_index"] = vIdxInt;
                    if (int.TryParse(vCnt, out var vCntInt)) entry["variant_count"] = vCntInt;
                }

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

    /// <summary>
    /// G6: Persist engine WaitRequest → flow_execution_state row.
    /// Returns true on success. Handler caller should degrade to handoff on false.
    /// </summary>
    private async Task<bool> PersistWaitAsync(
        SessionStateV2 state, WaitRequest waitReq,
        int tenantId, int flowId, string chatId, string? phone, string? instanceId,
        string? callbackUrl, string requestId, CancellationToken ct)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var clampedResume = waitReq.ResumeAt > now.Add(ActionWaitUntilHandler.MaxWait)
                ? now.Add(ActionWaitUntilHandler.MaxWait)
                : waitReq.ResumeAt;
            var maxWaitAt = now.Add(ActionWaitUntilHandler.MaxWait);

            var stateJson = SerializeV2State(state);
            var id = await _waitRepo.InsertPendingAsync(new PendingWaitRow
            {
                TenantId = tenantId,
                FlowId = flowId,
                ChatId = chatId,
                Phone = phone,
                InstanceId = string.IsNullOrEmpty(instanceId) ? null : instanceId,
                NodeId = waitReq.NodeId,
                ResumeAt = clampedResume,
                MaxWaitAt = maxWaitAt,
                SessionStateJson = stateJson,
                CallbackUrl = callbackUrl
            }, ct);

            _logger.StepInfo($"G6: wait persisted id={id} resume_at={clampedResume:o} node={waitReq.NodeId}", requestId);
            return id > 0;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitPersistFailed}] PersistWaitAsync DB error tenant={tenantId} chat={chatId}: {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitPersistFailed}] PersistWaitAsync state serialize failed tenant={tenantId} chat={chatId}: {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitPersistFailed}] PersistWaitAsync invalid state tenant={tenantId} chat={chatId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// G6: Resume a flow from a persisted wait row. Called by FlowWaitResumerService.
    /// Loads flow graph, deserializes session, executes engine from post-wait CurrentNodeId, dispatches messages/handoff.
    /// Returns true on success, false if unrecoverable (caller marks row failed).
    /// </summary>
    public async Task<bool> ResumeWaitAsync(DueWaitRow row, CancellationToken ct)
    {
        var requestId = $"resume-{row.Id}";
        var sw = Stopwatch.StartNew();
        try
        {
            var flowDetail = await _repo.GetFlowByIdAsync(row.TenantId, row.FlowId, ct);
            if (flowDetail == null)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Flow {row.FlowId} not found for resume row {row.Id}");
                return false;
            }

            var graph = FlowGraphV2.Build(flowDetail.FlowConfigJson);
            if (graph == null)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] Flow {row.FlowId} graph build failed for resume row {row.Id}");
                return false;
            }

            SessionStateV2? state;
            try
            {
                state = JsonSerializer.Deserialize<SessionStateV2>(row.SessionStateJson, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] session_state deserialize failed row {row.Id}: {ex.Message}");
                return false;
            }

            if (state == null || string.IsNullOrEmpty(state.CurrentNodeId))
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] row {row.Id} has null/empty CurrentNodeId after wait");
                return false;
            }

            if (!graph.NodesById.ContainsKey(state.CurrentNodeId))
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] row {row.Id} post-wait node '{state.CurrentNodeId}' no longer in graph (flow changed)");
                return false;
            }

            state.Status = "active";
            state.PendingInput = null;

            string contactKey;
            if (!string.IsNullOrEmpty(row.ChatId)) contactKey = row.ChatId;
            else if (!string.IsNullOrEmpty(row.Phone)) contactKey = row.Phone!;
            else contactKey = $"flow:{row.FlowId}";

            // HFM-2: resume path also respects lead.preferred_locale (read-only lookup,
            // no re-detect — wait resume has no new inbound message to detect from).
            var resumeLocale = await _repo.GetLeadPreferredLocaleAsync(row.TenantId, row.Phone ?? "", ct);

            var result = await _flowEngineV2.ExecuteAsync(graph, state, ct,
                tenantId: row.TenantId, tenantIntents: null, tenantConfidenceThreshold: 0.5,
                onMessage: null, contactKey: contactKey, leadPreferredLocale: resumeLocale);

            sw.Stop();

            var sequenceId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var msg in result.Messages)
            {
                await DispatchMessageOrChunksAsync(requestId, row.TenantId, row.ChatId, sequenceId,
                    msg, sw.ElapsedMilliseconds, row.CallbackUrl, ct);
            }

            if (result.Messages.Count > 0)
            {
                await _repo.LogAutoReplyAsync(row.TenantId, row.ChatId, row.Phone, "__wait_resume",
                    string.Join("\n\n", result.Messages), "v2_flow_resume", null, null, (int)sw.ElapsedMilliseconds, ct);
            }

            if (result.NeedsHandoff)
            {
                var summary = result.HandoffSummary ?? result.ErrorMessage ?? "wait-resume handoff";
                await SendHandoffAsync(requestId, row.TenantId, row.ChatId, sequenceId,
                    summary, sw.ElapsedMilliseconds, row.CallbackUrl, ct);
            }
            else if (result.NeedsAssignGroup && !string.IsNullOrWhiteSpace(result.AssignGroupId))
            {
                await SendAssignGroupAsync(requestId, row.TenantId, row.ChatId, sequenceId,
                    result.AssignGroupId!, result.AssignGroupSummary ?? "Grup atamasi",
                    sw.ElapsedMilliseconds, row.CallbackUrl, ct);
            }

            var session = await _repo.GetActiveSessionAsync(row.TenantId, row.ChatId, ct);
            if (session != null)
            {
                if (result.IsTerminal)
                {
                    var endStatus = result.NeedsHandoff || result.NeedsAssignGroup ? "handed_off"
                                  : (result.ErrorCode != null ? "error" : "completed");
                    await _repo.EndSessionAsync(session.Id, endStatus, ct);
                }
                else
                {
                    var stateJson = SerializeV2State(result.State);
                    await _repo.UpdateSessionAsync(session.Id, "v2_active", stateJson, ct);
                }
            }

            if (!result.IsTerminal && result.WaitRequest != null)
            {
                await PersistWaitAsync(
                    result.State, result.WaitRequest,
                    row.TenantId, row.FlowId, row.ChatId, row.Phone, row.InstanceId,
                    row.CallbackUrl, requestId, ct);
            }

            _logger.StepInfo($"G6: wait resumed id={row.Id} messages={result.Messages.Count} terminal={result.IsTerminal}", requestId, sw.ElapsedMilliseconds);
            return true;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] ResumeWaitAsync row {row.Id} DB error: {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] ResumeWaitAsync row {row.Id} JSON error: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] ResumeWaitAsync row {row.Id} invalid state: {ex.Message}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitResumeFailed}] ResumeWaitAsync row {row.Id} HTTP callback failed: {ex.Message}");
            return false;
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

        // Search Knowledge (semantic FAQ search — v1 compat)
        var serviceJwt = _jwtGenerator.GenerateServiceToken(tenantId);
        var searchResult = await _knowledgeSearchClient.SearchAsync(tenantId, messageText, 3, "faq_only", serviceJwt, ct);
        if (searchResult.Available && searchResult.Items.Count > 0)
        {
            var bestItem = searchResult.Items[0];
            if (bestItem.Score >= 0.3 && !string.IsNullOrEmpty(bestItem.Answer))
            {
                sw.Stop();
                var replyText = bestItem.Answer + "\n\nBaska bir sorunuz var mi? Ana menu icin '0' yazin.";
                await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                    CallbackActions.SendMessage, replyText, "faq_match", bestItem.Score, sw.ElapsedMilliseconds, callbackUrl, ct);

                await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, bestItem.Answer,
                    "faq", "faq_match", bestItem.Score, (int)sw.ElapsedMilliseconds, ct);

                return true;
            }
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

    /// <summary>
    /// HFM-2: resolve the lead's preferred locale for this message cycle.
    /// Flow: DB lookup → (if null AND message long enough) heuristic detect → upsert →
    /// re-read canonical value (race guard). Any DB failure degrades to null so
    /// downstream handlers fall back ('en' default).
    ///
    /// Race determinism: parallel channel messages (WA + IG + Telegram at the same ms)
    /// could each detect a different locale. The ON CONFLICT COALESCE pattern preserves
    /// the first successful insert; losing writers re-read and align with the winner.
    /// </summary>
    private async Task<string?> ResolveLeadPreferredLocaleAsync(
        int tenantId, string? phone, string messageText, string requestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var existing = await _repo.GetLeadPreferredLocaleAsync(tenantId, phone, ct);
        if (!string.IsNullOrEmpty(existing))
            return existing;

        if (string.IsNullOrWhiteSpace(messageText) || messageText.Length < 2)
            return null;

        var detected = LanguageDetector.Detect(messageText);
        if (string.IsNullOrEmpty(detected))
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationLocaleDetectFailed}] LanguageDetector returned empty for tenant={tenantId} phone={phone}");
            return null;
        }

        var inserted = await _repo.UpsertLeadPreferredLocaleAsync(tenantId, phone, detected, ct);

        // Race-safe canonical read: regardless of who won the ON CONFLICT race, the stored
        // value is authoritative. If the re-read fails (rare DB hiccup), fall back to the
        // freshly detected value so the current cycle still gets a locale.
        var canonical = await _repo.GetLeadPreferredLocaleAsync(tenantId, phone, ct);
        var resolved = !string.IsNullOrEmpty(canonical) ? canonical : detected;

        _logger.StepInfo(
            $"HFM-2 preferred_locale resolved tenant={tenantId} phone={phone} detected={detected} upserted={inserted} canonical={canonical ?? "(none)"} resolved={resolved}",
            requestId);
        return resolved;
    }

    /// <summary>
    /// HFM-1: dispatch one engine-emitted message. Detects the chunk sentinel prefix and,
    /// when present, fans out per-chunk callbacks with planner-computed pre-delays.
    /// Plain messages fall through to the legacy single-callback path unchanged.
    ///
    /// Malformed sentinel payloads (JSON parse failure, wrong root kind, zero steps) are
    /// logged with INV-AT-062 via the decode-error callback and fall back to legacy send
    /// of the RAW text — this would leak the sentinel prefix, so SendCallbackAsync also
    /// strips it defensively before invoking the callback client.
    /// </summary>
    private async Task DispatchMessageOrChunksAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string messageText, long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        var chunks = MessageTextHandler.TryDecodeChunkPayload(messageText,
            reason => _logger.SystemWarn(
                $"[{ErrorCodes.AutomationChunkScheduleInvalid}] chunk decode failed tenant={tenantId} chat={chatId}: {reason}"));
        if (chunks == null)
        {
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, messageText, null, null, processingTimeMs, callbackUrl, ct);
            return;
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            var step = chunks[i];
            if (step.PreDelayMs > 0)
            {
                try
                {
                    await Task.Delay(step.PreDelayMs, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // user cancelled / shutdown — propagate
                }
            }

            _logger.StepInfo(
                $"HFM-1 chunk {i + 1}/{chunks.Count} dispatch tenant={tenantId} chat={chatId} delayMs={step.PreDelayMs}",
                requestId);

            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, step.Text, null, null, processingTimeMs, callbackUrl, ct);
        }
    }

    private async Task<bool> SendCallbackAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string action, string messageText, string? intent, double? confidence,
        long processingTimeMs, string? callbackUrl, CancellationToken ct,
        string? eventName = null)
    {
        // HFM-1 defense-in-depth: if a caller hands us a raw chunk-sentinel payload (should
        // already have gone through DispatchMessageOrChunksAsync), strip the sentinel+JSON
        // wrapper so we never leak it to the customer. Log INV-AT-062 so the bypass path
        // is visible in ops — should be zero-rate at steady state.
        if (action == CallbackActions.SendMessage
            && !string.IsNullOrEmpty(messageText)
            && messageText.StartsWith(MessageTextHandler.ChunkSentinel, StringComparison.Ordinal))
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationChunkScheduleInvalid}] SendCallbackAsync received raw chunk sentinel — DispatchMessageOrChunksAsync was bypassed; stripping to prevent customer leak. tenant={tenantId} chat={chatId}");
            var decoded = MessageTextHandler.TryDecodeChunkPayload(messageText);
            messageText = decoded != null && decoded.Count > 0
                ? string.Join("\n\n", decoded.Select(s => s.Text))
                : messageText[MessageTextHandler.ChunkSentinel.Length..];
        }

        // GR-2.6.1: Append KVKK health disclaimer for SendMessage actions only
        var finalMessageText = messageText;
        if (action == CallbackActions.SendMessage && !string.IsNullOrEmpty(messageText))
        {
            var isHealth = await IsHealthTenantCachedAsync(tenantId, ct);
            finalMessageText = KvkkHelper.AppendDisclaimerIfHealth(messageText, isHealth);
        }

        // FEAT-J2: derive MessageCategory for SendMessage callbacks.
        //
        // Semantics (tenant-scoped, opt-in):
        //   enforce_message_category=FALSE (default) → MessageCategory stays null,
        //     INMA skips opt-out check, reactive replies flow as-is.
        //     This is the behaviour for all tenants today — breaking change risk = 0.
        //   enforce_message_category=TRUE (pilot tenants who run J2 strictly) →
        //     send_message without an event_name is rejected (INV-OB-031). Callers
        //     observe this as `SendCallbackAsync` returning false, which their
        //     existing failure branches already surface upstream (INV-INT-002
        //     callback delivery failure is reused for "callback not delivered" at
        //     the caller-visible boundary). INV-OB-031 is the authoritative log
        //     line tagging *why* the callback was dropped — ops dashboards can
        //     grep on that code to detect misconfigured flows before pilot
        //     enablement.
        string? messageCategory = null;
        if (action == CallbackActions.SendMessage)
        {
            var enforce = await _tenantSettings.GetEnforceMessageCategoryAsync(tenantId, ct);
            if (enforce)
            {
                if (string.IsNullOrEmpty(eventName))
                {
                    _logger.StepError(
                        $"[{ErrorCodes.MessageCategoryEnforcementFailed}] send_message rejected: enforce_message_category=TRUE requires event_name. tenant={tenantId} chat={chatId} action_item=tag_flow_send_message_nodes_with_event",
                        requestId, processingTimeMs);
                    return false;
                }
                messageCategory = TransactionalEventRegistry.IsTransactional(eventName)
                    ? "transactional"
                    : "marketing";
            }
            // enforce=FALSE: MessageCategory remains null (INMA back-compat skip).
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
                Confidence = confidence,
                MessageCategory = messageCategory,
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

    private async Task<bool> SendAssignGroupAsync(
        string requestId, int tenantId, string chatId, long sequenceId,
        string groupId, string aiSummary, long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        var callback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = CallbackActions.AssignGroup,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData
            {
                GroupId = groupId,
                AiSummary = aiSummary
            },
            ProcessingTimeMs = processingTimeMs
        };

        var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
        if (!delivered)
            _logger.StepError($"[{ErrorCodes.IntegrationCallbackFailed}] AssignGroup callback delivery failed: tenant={tenantId}, chat={chatId}, group={groupId}", requestId, processingTimeMs);
        return delivered;
    }
}
