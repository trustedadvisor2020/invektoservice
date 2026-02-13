namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Pause execution for N seconds.
/// Production: real Task.Delay. Simulation: instant skip with info message.
/// Auto-chain after delay.
/// </summary>
public sealed class ActionDelayHandler : INodeHandler
{
    public string NodeType => "action_delay";

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var secondsRaw = node.GetData("seconds", "5");
        var seconds = int.TryParse(secondsRaw, out var s) ? Math.Clamp(s, 1, 300) : 5;

        if (ctx.IsSimulation)
        {
            // Instant skip in simulation -- show info message instead of waiting
            return new NodeResult
            {
                MessageText = $"Bekleme: {seconds}sn (simule edildi)",
                Action = NodeAction.Continue,
                OutputHandle = null
            };
        }

        // Production: real delay with cancellation support
        await Task.Delay(seconds * 1000, ct);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null
        };
    }
}
