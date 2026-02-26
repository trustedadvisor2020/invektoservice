namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Checks tenant working hours and branches the flow.
/// Output handles: "within_hours" (mesai ici) / "outside_hours" (mesai disi).
/// No per-node config — uses tenant settings_json working_hours.
/// </summary>
public sealed class LogicWorkingHoursHandler : INodeHandler
{
    private readonly WorkingHoursChecker _workingHours;

    public string NodeType => "logic_working_hours";

    public LogicWorkingHoursHandler(WorkingHoursChecker workingHours)
    {
        _workingHours = workingHours;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var (isWithinHours, _) = await _workingHours.CheckAsync(ctx.TenantId, ct);

        ctx.Logger.StepInfo(
            $"LogicWorkingHours '{node.GetData("label", node.Id)}': tenantId={ctx.TenantId}, within={isWithinHours}",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = isWithinHours ? "within_hours" : "outside_hours"
        };
    }
}
