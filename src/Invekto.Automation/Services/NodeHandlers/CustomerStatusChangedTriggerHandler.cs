namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3a — entry point handler for the <c>customer_status_changed</c> trigger.
/// Fired by <c>TriggerCustomerStatusFlowJob</c> when INMA's <c>customer.selection_changed</c> event
/// (an explicit human 'user' agent change) has applied a new opaque <c>leads.customer_status</c>.
///
/// The dispatch job seeds the change context into session <c>Variables</c> under internal
/// <c>__</c>-prefixed keys before the engine runs; this handler surfaces them under the documented
/// flow-builder names so downstream nodes can reference {{customer_status_group}},
/// {{old_customer_status}}, {{new_customer_status}}, {{customer_status_changed_by}} (contract
/// arch/contracts/inma-customer-status-webhook.json c3_trigger). Pure (no DB/HTTP), sends no message,
/// auto-chains to the next node — mirrors <see cref="WebhookTriggerHandler"/>.
/// </summary>
public sealed class CustomerStatusChangedTriggerHandler : INodeHandler
{
    // Internal seed keys written by TriggerCustomerStatusFlowJob before engine execution.
    public const string SeedGroup = "__customer_status_group";
    public const string SeedOld = "__old_customer_status";
    public const string SeedNew = "__new_customer_status";
    public const string SeedChangedBy = "__customer_status_changed_by";

    // Public flow-builder variable names exposed to downstream nodes.
    public const string VarGroup = "customer_status_group";
    public const string VarOld = "old_customer_status";
    public const string VarNew = "new_customer_status";
    public const string VarChangedBy = "customer_status_changed_by";

    public string NodeType => "customer_status_changed";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        Surface(ctx, variables, SeedGroup, VarGroup);
        Surface(ctx, variables, SeedOld, VarOld);
        Surface(ctx, variables, SeedNew, VarNew);
        Surface(ctx, variables, SeedChangedBy, VarChangedBy);

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null,
            VariableUpdates = variables.Count > 0 ? variables : null
        });
    }

    private static void Surface(ExecutionContext ctx, Dictionary<string, string> target, string seedKey, string publicKey)
    {
        if (ctx.State.Variables.TryGetValue(seedKey, out var value))
            target[publicKey] = value;
    }
}
