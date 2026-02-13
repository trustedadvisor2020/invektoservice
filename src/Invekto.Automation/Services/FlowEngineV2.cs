using Invekto.Automation.Services.NodeHandlers;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Pure v2 graph-based flow execution engine.
/// NO side-effects: no DB, no HTTP, no callbacks. Returns EngineStepResult.
/// Orchestrator handles side-effects (IMP-8).
/// Thread-safe, register as singleton.
/// </summary>
public sealed class FlowEngineV2
{
    private readonly Dictionary<string, INodeHandler> _handlers;
    private readonly ExpressionEvaluator _evaluator;
    private readonly JsonLinesLogger _logger;

    /// <summary>Max nodes to visit in a single step (prevent runaway auto-chains).</summary>
    private const int MaxChainDepth = 50;

    public FlowEngineV2(
        IEnumerable<INodeHandler> handlers,
        ExpressionEvaluator evaluator,
        JsonLinesLogger logger)
    {
        _handlers = handlers.ToDictionary(h => h.NodeType, h => h, StringComparer.Ordinal);
        _evaluator = evaluator;
        _logger = logger;
    }

    /// <summary>
    /// Execute the flow from the current session state.
    /// For new sessions: state.CurrentNodeId should be the trigger_start node ID.
    /// For returning users (with pending input): state has PendingInput + __last_input variable.
    /// Returns accumulated messages and updated state.
    /// </summary>
    public async Task<EngineStepResult> ExecuteAsync(
        FlowGraphV2 graph, SessionStateV2 state, CancellationToken ct)
    {
        var messages = new List<string>();
        var currentNodeId = state.CurrentNodeId;
        var chainDepth = 0;

        while (!string.IsNullOrEmpty(currentNodeId))
        {
            ct.ThrowIfCancellationRequested();

            // Chain depth protection
            if (chainDepth++ > MaxChainDepth)
            {
                _logger.SystemWarn($"Auto-chain depth exceeded {MaxChainDepth} at node {currentNodeId}");
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    ErrorCode = ErrorCodes.AutomationMaxLoopExceeded,
                    ErrorMessage = $"Sonsuz dongu limiti asildi, node: {currentNodeId}"
                };
            }

            // Loop counter check (per-node visit count)
            if (!state.LoopCounters.TryGetValue(currentNodeId, out var visits))
                visits = 0;
            visits++;
            state.LoopCounters[currentNodeId] = visits;

            if (visits > graph.Settings.MaxLoopCount)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationMaxLoopExceeded}] Node {currentNodeId} visited {visits} times (max {graph.Settings.MaxLoopCount})");
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    ErrorCode = ErrorCodes.AutomationMaxLoopExceeded,
                    ErrorMessage = $"Sonsuz dongu limiti asildi, node: {currentNodeId}"
                };
            }

            // Resolve node
            if (!graph.NodesById.TryGetValue(currentNodeId, out var node))
            {
                _logger.SystemWarn($"Node '{currentNodeId}' not found in graph");
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    ErrorCode = ErrorCodes.AutomationUnknownNodeType,
                    ErrorMessage = $"Node bulunamadi: {currentNodeId}"
                };
            }

            // Find handler
            if (!_handlers.TryGetValue(node.Type, out var handler))
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationUnknownNodeType}] No handler for node type '{node.Type}'");
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    ErrorCode = ErrorCodes.AutomationUnknownNodeType,
                    ErrorMessage = $"Desteklenmeyen node tipi: {node.Type}"
                };
            }

            // Execute handler
            NodeResult result;
            try
            {
                var ctx = new NodeHandlers.ExecutionContext
                {
                    Graph = graph,
                    State = state,
                    Evaluator = _evaluator,
                    Logger = _logger,
                    RequestId = "-"
                };
                result = await handler.ExecuteAsync(node, ctx, ct);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                // IMP-5: Error recovery — node error → session=error + handoff
                _logger.SystemWarn($"[{ErrorCodes.AutomationNodeExecutionFailed}] Node {currentNodeId} ({node.Type}) execution failed: {ex.Message}");
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    NeedsHandoff = true,
                    ErrorCode = ErrorCodes.AutomationNodeExecutionFailed,
                    ErrorMessage = $"Node calisma hatasi ({currentNodeId}): {ex.Message}"
                };
            }

            // Track execution path
            state.ExecutionPath.Add(currentNodeId);
            state.CurrentNodeId = currentNodeId;

            // Collect message
            if (!string.IsNullOrEmpty(result.MessageText))
                messages.Add(result.MessageText);

            // Apply variable updates
            if (result.VariableUpdates != null)
            {
                foreach (var (key, value) in result.VariableUpdates)
                    state.Variables[key] = value;
            }

            // Handle error from node
            if (result.IsError)
            {
                state.Status = "error";
                return new EngineStepResult
                {
                    Messages = messages,
                    State = state,
                    IsTerminal = true,
                    NeedsHandoff = true,
                    ErrorCode = result.ErrorCode,
                    ErrorMessage = result.ErrorMessage
                };
            }

            // Decide next action
            switch (result.Action)
            {
                case NodeAction.Continue:
                    // Follow outgoing edge
                    var edges = graph.GetOutgoingEdges(currentNodeId, result.OutputHandle);
                    if (edges.Count == 0 && result.OutputHandle != null)
                    {
                        // Try default edge if handle-specific not found
                        edges = graph.GetOutgoingEdges(currentNodeId);
                    }

                    if (edges.Count == 0)
                    {
                        // Dead-end: no outgoing edges, session completed
                        state.Status = "completed";
                        state.PendingInput = null;
                        return new EngineStepResult
                        {
                            Messages = messages,
                            State = state,
                            IsTerminal = true
                        };
                    }

                    // Follow first matching edge (fan-out: sequential execution)
                    var nextEdge = edges[0];
                    var nextNode = graph.GetTargetNode(nextEdge);
                    if (nextNode == null)
                    {
                        state.Status = "completed";
                        state.PendingInput = null;
                        return new EngineStepResult
                        {
                            Messages = messages,
                            State = state,
                            IsTerminal = true
                        };
                    }

                    currentNodeId = nextNode.Id;
                    state.PendingInput = null;
                    break;

                case NodeAction.WaitForInput:
                    // Pause execution, store pending input
                    state.PendingInput = result.PendingInput != null
                        ? new PendingInputState
                        {
                            Type = result.PendingInput.Type,
                            Options = result.PendingInput.Options,
                            NodeId = currentNodeId
                        }
                        : null;
                    return new EngineStepResult
                    {
                        Messages = messages,
                        State = state,
                        IsTerminal = false
                    };

                case NodeAction.Terminal:
                    // Session ends (handoff, explicit end)
                    var needsHandoff = node.Type == "action_handoff";
                    if (needsHandoff)
                        state.Status = "handed_off";
                    else
                        state.Status = "completed";
                    state.PendingInput = null;
                    return new EngineStepResult
                    {
                        Messages = messages,
                        State = state,
                        IsTerminal = true,
                        NeedsHandoff = needsHandoff,
                        HandoffSummary = state.Variables.TryGetValue("__handoff_summary", out var hs) ? hs : null
                    };
            }
        }

        // Should not reach here — no node to execute
        state.Status = "completed";
        return new EngineStepResult
        {
            Messages = messages,
            State = state,
            IsTerminal = true
        };
    }
}

/// <summary>
/// Result of a single engine step (may include multiple auto-chained node executions).
/// </summary>
public sealed class EngineStepResult
{
    /// <summary>All messages collected during auto-chain. Orchestrator joins with '\n\n'.</summary>
    public required List<string> Messages { get; init; }
    /// <summary>Updated session state (serialize to session_data JSONB).</summary>
    public required SessionStateV2 State { get; init; }
    /// <summary>True if session ended (completed, error, handoff).</summary>
    public bool IsTerminal { get; init; }
    /// <summary>True if session should be handed off to human agent.</summary>
    public bool NeedsHandoff { get; init; }
    /// <summary>Handoff summary (from ActionHandoffHandler).</summary>
    public string? HandoffSummary { get; init; }
    /// <summary>Error code if engine stopped due to error.</summary>
    public string? ErrorCode { get; init; }
    /// <summary>Error message for logging.</summary>
    public string? ErrorMessage { get; init; }
}
