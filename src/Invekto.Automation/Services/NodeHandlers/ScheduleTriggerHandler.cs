namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Schedule trigger entry point handler.
/// Activated when CronSchedulerService fires at the configured cron time.
/// Sets schedule_fired_at variable, auto-chains to next node.
/// </summary>
public sealed class ScheduleTriggerHandler : INodeHandler
{
    public string NodeType => "schedule_trigger";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var variables = new Dictionary<string, string>
        {
            ["schedule_fired_at"] = DateTime.UtcNow.ToString("O")
        };

        return Task.FromResult(new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = null,
            VariableUpdates = variables
        });
    }
}
