using System.Text.Json;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Multi-way branching. Matches session variable against case values.
/// N+1 output handles: case_1..case_N + default.
/// Auto-chain (no user input needed).
/// </summary>
public sealed class LogicSwitchHandler : INodeHandler
{
    public string NodeType => "logic_switch";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var variableName = node.GetData("variable");
        var actualValue = ctx.State.Variables.TryGetValue(variableName, out var v) ? v : "";
        var defaultHandle = node.GetData("default_handle_id", "default");

        var matchedHandle = ResolveCase(node, actualValue, defaultHandle, ctx);

        ctx.Logger.StepInfo(
            $"LogicSwitch '{node.GetData("label", node.Id)}': {variableName}='{actualValue}' -> handle={matchedHandle}",
            ctx.RequestId);

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = matchedHandle
        });
    }

    private static string ResolveCase(FlowNodeV2 node, string actualValue, string defaultHandle, ExecutionContext ctx)
    {
        var casesJson = node.GetData("cases");
        if (string.IsNullOrEmpty(casesJson))
        {
            ctx.Logger.StepWarn(
                $"LogicSwitch '{node.GetData("label", node.Id)}': No cases defined, falling back to default handle.",
                ctx.RequestId);
            return defaultHandle;
        }

        try
        {
            using var doc = JsonDocument.Parse(casesJson);
            foreach (var c in doc.RootElement.EnumerateArray())
            {
                var caseValue = c.TryGetProperty("value", out var cv) ? cv.GetString() ?? "" : "";
                var handleId = c.TryGetProperty("handle_id", out var h) ? h.GetString() ?? "" : "";

                if (string.Equals(actualValue, caseValue, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(handleId))
                {
                    return handleId;
                }
            }
        }
        catch (JsonException ex)
        {
            ctx.Logger.StepWarn(
                $"LogicSwitch '{node.GetData("label", node.Id)}': Invalid cases JSON, falling back to default. Error: {ex.Message}",
                ctx.RequestId);
        }

        return defaultHandle;
    }
}
