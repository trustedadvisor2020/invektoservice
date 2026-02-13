namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Set a session variable. Evaluates value_expression with {{variable}} substitution.
/// Auto-chain after setting.
/// </summary>
public sealed class SetVariableHandler : INodeHandler
{
    public string NodeType => "utility_set_variable";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var variableName = node.GetData("variable_name");
        var expression = node.GetData("value_expression");

        var evaluatedValue = ctx.Evaluator.Substitute(expression, ctx.State.Variables);

        ctx.Logger.StepInfo(
            $"SetVariable '{node.GetData("label", node.Id)}': {variableName} = '{evaluatedValue}'",
            ctx.RequestId);

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null,
            VariableUpdates = new Dictionary<string, string>
            {
                [variableName] = evaluatedValue
            }
        });
    }
}
