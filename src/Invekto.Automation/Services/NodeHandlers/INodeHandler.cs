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
    /// <summary>True when running inside SimulationEngine (e.g. action_delay skips real wait).</summary>
    public bool IsSimulation { get; init; }
    /// <summary>Tenant ID for tenant-scoped services (e.g. FaqMatcher DB query). 0 = unknown.</summary>
    public int TenantId { get; init; }

    /// <summary>PKT-6A: DB-loaded tenant intent names from Knowledge service. Null = use IntentDetector defaults.</summary>
    public string[]? TenantIntents { get; init; }

    /// <summary>PKT-6A: Tenant confidence threshold from settings_json. Used as fallback by AiIntentHandler.</summary>
    public double TenantConfidenceThreshold { get; init; } = 0.5;

    /// <summary>
    /// G3: Stable contact identifier (chatId or phone) for deterministic template A/B rotation.
    /// Null/empty = unknown contact → rotation falls back to node-only hash.
    /// </summary>
    public string? ContactKey { get; init; }

    /// <summary>
    /// HFM-2: resolved preferred locale for the current lead (ISO 639-1 or empty).
    /// Orchestrator populates from leads.preferred_locale before engine execution.
    /// Null/empty → fallback chain ('en' default → raw text) applied by handlers.
    /// </summary>
    public string? LeadPreferredLocale { get; init; }

    /// <summary>
    /// FEAT-INMA-PIPELINE-V2 C4: INMA customer id for the current contact (preferred write key for the
    /// 'Set Customer Status' action). Populated from the triggering customer.selection_changed event when
    /// the flow was status-triggered; null in other paths (the action falls back to <see cref="Phone"/>).
    /// </summary>
    public int? CustomerId { get; init; }

    /// <summary>
    /// FEAT-INMA-PIPELINE-V2 C4: the current contact's phone (digits) — the 'Set Customer Status' action's
    /// fallback write key when <see cref="CustomerId"/> is absent. Populated by the inbound orchestrator
    /// (sender phone), the status-trigger job (event phone), and welcome/cron (lead phone) where available.
    /// </summary>
    public string? Phone { get; init; }
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

    /// <summary>
    /// G6: When Action=WaitPersist, the UTC timestamp at which the flow should resume.
    /// Orchestrator persists the session snapshot + this timestamp to flow_execution_state.
    /// FlowWaitResumerService polls and resumes at/after this time.
    /// </summary>
    public DateTimeOffset? WaitResumeAt { get; init; }

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
    Terminal,
    /// <summary>Call a sub-flow. Engine returns to orchestrator for dispatch.</summary>
    CallSubFlow,
    /// <summary>G6: Persist session + pause for long wait (restart-safe). Orchestrator writes flow_execution_state row; resumer service re-enters flow at/after WaitResumeAt.</summary>
    WaitPersist
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
