using Invekto.Shared.Logging;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Strategy interface for v2 flow node execution.
/// Each node type has its own handler (Phase 3a: 5 types, Phase 4: +7 types).
/// Handlers are PURE — no DB, no HTTP, no callbacks.
/// </summary>
public interface INodeHandler
{
    string NodeType { get; }
    Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct);
}

/// <summary>
/// Immutable context passed to every handler.
/// Contains graph, session state, and logger. No DB/HTTP references.
/// </summary>
public sealed class ExecutionContext
{
    public required FlowGraphV2 Graph { get; init; }
    public required SessionStateV2 State { get; init; }
    public required ExpressionEvaluator Evaluator { get; init; }
    public required JsonLinesLogger Logger { get; init; }
    public required string RequestId { get; init; }
}

/// <summary>
/// Result of a single node execution.
/// Engine uses this to decide: auto-chain, wait, or terminal.
/// </summary>
public sealed class NodeResult
{
    /// <summary>Message to send to customer. Null if no message (e.g. trigger_start, utility_note).</summary>
    public string? MessageText { get; init; }

    /// <summary>Next action: Continue (auto-chain), WaitForInput (pause), Terminal (end session).</summary>
    public required NodeAction Action { get; init; }

    /// <summary>
    /// Output handle to follow for next edge lookup.
    /// Null = default (single output). Non-null = specific handle (e.g. "opt_1" for menu).
    /// </summary>
    public string? OutputHandle { get; init; }

    /// <summary>Pending input descriptor when Action=WaitForInput.</summary>
    public PendingInput? PendingInput { get; init; }

    /// <summary>Variables to merge into session state.</summary>
    public Dictionary<string, string>? VariableUpdates { get; init; }

    /// <summary>If true, this node triggered an error (session should be set to error state).</summary>
    public bool IsError { get; init; }

    /// <summary>Error code (INV-AT-xxx) if IsError=true.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message for logging/handoff if IsError=true.</summary>
    public string? ErrorMessage { get; init; }
}

public enum NodeAction
{
    /// <summary>Immediately proceed to next node via outgoing edge.</summary>
    Continue,
    /// <summary>Pause execution, wait for user input (menu selection, text).</summary>
    WaitForInput,
    /// <summary>End session (handoff, error, or explicit end).</summary>
    Terminal
}

/// <summary>
/// Describes what input the engine is waiting for.
/// Stored in session_data for next message processing.
/// </summary>
public sealed class PendingInput
{
    public required string Type { get; init; } // "menu" or "text"
    public List<string>? Options { get; init; } // menu option keys
}
