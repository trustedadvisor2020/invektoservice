namespace Chatinbox.Automation.Services.NodeHandlers;

/// <summary>
/// Webhook trigger entry point handler.
/// Activated when external system sends HTTP POST to webhook endpoint.
/// Sets payload variable, auto-chains to next node.
/// </summary>
public sealed class WebhookTriggerHandler : INodeHandler
{
    public string NodeType => "webhook_trigger";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Inject webhook payload into session variable if configured
        var payloadVar = node.GetData("payload_variable", "webhook_payload");
        Dictionary<string, string>? variables = null;

        if (ctx.State.Variables.TryGetValue("__webhook_payload", out var payload))
        {
            variables = new Dictionary<string, string>
            {
                [payloadVar] = payload
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
