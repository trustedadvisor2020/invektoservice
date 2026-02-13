namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Entry point handler. No message output, auto-chains to next node.
/// Sets initial variables (e.g. customer_phone if available).
/// </summary>
public sealed class TriggerStartHandler : INodeHandler
{
    public string NodeType => "trigger_start";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new NodeResult
        {
            MessageText = null, // trigger_start produces no message
            Action = NodeAction.Continue,
            OutputHandle = null // default outgoing edge
        });
    }
}
