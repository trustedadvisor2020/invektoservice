namespace Chatinbox.Automation.Services.NodeHandlers;

/// <summary>
/// Terminal node: assigns conversation to a specific INMA agent group.
/// Stores __assign_group_id and __assign_group_summary for orchestrator callback.
/// Pattern: mirrors ActionHandoffHandler.
/// </summary>
public sealed class ActionAssignGroupHandler : INodeHandler
{
    public string NodeType => "action_assign_group";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var groupId = node.GetData("group_id", "");
        var summaryTemplate = node.GetData("summary_template", "Musteri gruba yonlendirildi");
        var summary = ctx.Evaluator.Substitute(summaryTemplate, ctx.State.Variables);

        ctx.Logger.StepInfo(
            $"ActionAssignGroup '{node.GetData("label", node.Id)}': groupId={groupId}",
            ctx.RequestId);

        var updates = new Dictionary<string, string>
        {
            ["__assign_group_id"] = groupId,
            ["__assign_group_summary"] = summary
        };

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Terminal,
            VariableUpdates = updates
        });
    }
}
