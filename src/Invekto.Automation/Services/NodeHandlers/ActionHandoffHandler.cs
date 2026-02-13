namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Terminal node: hand off conversation to human agent.
/// Generates summary from template, ends session.
/// </summary>
public sealed class ActionHandoffHandler : INodeHandler
{
    public string NodeType => "action_handoff";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var summaryTemplate = node.GetData("summary_template", "Musteri temsilci ile gorusme talep etti");
        var summary = ctx.Evaluator.Substitute(summaryTemplate, ctx.State.Variables);

        // Store handoff summary in variables for orchestrator to use
        var updates = new Dictionary<string, string>
        {
            ["__handoff_summary"] = summary
        };

        return Task.FromResult(new NodeResult
        {
            MessageText = null, // No message to customer — orchestrator sends handoff callback
            Action = NodeAction.Terminal,
            VariableUpdates = updates
        });
    }
}
