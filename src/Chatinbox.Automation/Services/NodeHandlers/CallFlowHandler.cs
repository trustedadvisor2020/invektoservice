namespace Chatinbox.Automation.Services.NodeHandlers;

/// <summary>
/// Call another flow (sub-flow) and wait for it to complete.
/// First visit: validates flow_id, returns CallSubFlow for orchestrator dispatch.
/// Resume (after sub-flow completes): orchestrator sets __sub_flow_completed, handler follows "completed" edge.
/// Pure handler — orchestrator handles sub-flow loading, call stack, and variable mapping.
/// </summary>
public sealed class CallFlowHandler : INodeHandler
{
    public string NodeType => "action_call_flow";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        // Resume path: orchestrator set __sub_flow_completed after sub-flow returned
        if (ctx.State.Variables.ContainsKey("__sub_flow_completed"))
        {
            ctx.State.Variables.Remove("__sub_flow_completed");

            // Check if sub-flow ended with error
            var hasError = ctx.State.Variables.ContainsKey("__sub_flow_error");
            if (hasError)
            {
                ctx.State.Variables.Remove("__sub_flow_error");
                return Task.FromResult(new NodeResult
                {
                    Action = NodeAction.Continue,
                    OutputHandle = "error"
                });
            }

            return Task.FromResult(new NodeResult
            {
                Action = NodeAction.Continue,
                OutputHandle = "completed"
            });
        }

        // First visit: validate flow_id and signal orchestrator
        var flowIdStr = node.GetData("flow_id");
        if (string.IsNullOrEmpty(flowIdStr) || !int.TryParse(flowIdStr, out _))
        {
            return Task.FromResult(new NodeResult
            {
                Action = NodeAction.Continue,
                OutputHandle = "error",
                VariableUpdates = new Dictionary<string, string>
                {
                    ["__call_flow_error"] = "Gecersiz veya eksik flow_id"
                }
            });
        }

        return Task.FromResult(new NodeResult
        {
            Action = NodeAction.CallSubFlow
        });
    }
}
