namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Send a text message with {{variable}} substitution. Auto-chains to next node.
/// </summary>
public sealed class MessageTextHandler : INodeHandler
{
    public string NodeType => "message_text";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var rawText = node.GetData("text");
        var message = ctx.Evaluator.Substitute(rawText, ctx.State.Variables);

        return Task.FromResult(new NodeResult
        {
            MessageText = message,
            Action = NodeAction.Continue,
            OutputHandle = null
        });
    }
}
