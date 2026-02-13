namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// If/else branching. Evaluates condition using ExpressionEvaluator.
/// 2 output handles: true_handle, false_handle.
/// Auto-chain (no user input needed).
/// </summary>
public sealed class LogicConditionHandler : INodeHandler
{
    public string NodeType => "logic_condition";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var variable = node.GetData("variable");
        var operatorType = node.GetData("operator");
        var value = node.GetData("value");

        var result = ctx.Evaluator.EvaluateCondition(variable, operatorType, value, ctx.State.Variables);

        ctx.Logger.StepInfo(
            $"LogicCondition '{node.GetData("label", node.Id)}': {variable} {operatorType} {value} = {result}",
            ctx.RequestId);

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = result ? "true_handle" : "false_handle"
        });
    }
}
