namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Outbound trigger entry point handler.
/// Activated when Outbound service initiates a proactive campaign flow.
/// Sets campaign_id variable, auto-chains to next node.
/// </summary>
public sealed class OutboundTriggerHandler : INodeHandler
{
    public string NodeType => "outbound_trigger";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Inject campaign info into session variable if configured
        var campaignVar = node.GetData("campaign_variable", "campaign_id");
        Dictionary<string, string>? variables = null;

        if (ctx.State.Variables.TryGetValue("__outbound_campaign_id", out var campaignId))
        {
            variables = new Dictionary<string, string>
            {
                [campaignVar] = campaignId
            };
        }

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null,
            VariableUpdates = variables
        });
    }
}
