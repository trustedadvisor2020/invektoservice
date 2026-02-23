namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Send a text message with {{variable}} substitution.
/// When data.wait_for_input is true: pauses execution and waits for user reply.
/// Two-phase execution (same pattern as MessageMenuHandler):
///   Phase 1 (first visit): send message, return WaitForInput
///   Phase 2 (user replied): PendingInput matches this node, return Continue
/// </summary>
public sealed class MessageTextHandler : INodeHandler
{
    public string NodeType => "message_text";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var rawText = node.GetData("text");
        var message = ctx.Evaluator.Substitute(rawText, ctx.State.Variables);
        var waitForInput = node.GetData("wait_for_input") == "true";

        if (!waitForInput)
        {
            // Fire-and-forget: send message, continue to next node
            return Task.FromResult(new NodeResult
            {
                MessageText = message,
                Action = NodeAction.Continue,
                OutputHandle = null
            });
        }

        // Phase 2: user already replied to this wait point
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            // User's reply is already in __last_input (set by orchestrator).
            // Also expose as user_input for {{user_input}} template compatibility.
            var userReply = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            ctx.State.Variables["user_input"] = userReply;

            return Task.FromResult(new NodeResult
            {
                MessageText = null, // Don't re-send the prompt
                Action = NodeAction.Continue,
                OutputHandle = null
            });
        }

        // Phase 1: first visit — send message and wait for user reply
        return Task.FromResult(new NodeResult
        {
            MessageText = message,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput
            {
                Type = "text",
                Options = null
            }
        });
    }
}
