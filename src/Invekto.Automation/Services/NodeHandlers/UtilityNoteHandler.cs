namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Developer-only visual comment on canvas. Not executed by engine.
/// No-op: produces no message, auto-chains if outgoing edge exists.
/// </summary>
public sealed class UtilityNoteHandler : INodeHandler
{
    public string NodeType => "utility_note";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null
        });
    }
}
