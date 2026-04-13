using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Send a text message with {{variable}} substitution.
/// Supports deterministic A/B rotation via optional <c>data.text_variants</c> (JSON array of strings).
/// When <c>text_variants</c> is a non-empty array, <see cref="ITemplateRotationService"/> picks
/// one variant using hash(contactKey + nodeId). Fallback to <c>data.text</c> otherwise.
///
/// When data.wait_for_input is true: pauses execution and waits for user reply.
/// Two-phase execution (same pattern as MessageMenuHandler):
///   Phase 1 (first visit): send message, return WaitForInput
///   Phase 2 (user replied): PendingInput matches this node, return Continue
/// </summary>
public sealed class MessageTextHandler : INodeHandler
{
    private readonly ITemplateRotationService _rotation;
    private readonly JsonLinesLogger _logger;

    public MessageTextHandler(ITemplateRotationService rotation, JsonLinesLogger logger)
    {
        _rotation = rotation;
        _logger = logger;
    }

    public string NodeType => "message_text";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var rawText = ResolveTemplate(node, ctx, out var variantIndex, out var variantCount);
        var message = ctx.Evaluator.Substitute(rawText, ctx.State.Variables);
        var waitForInput = node.GetData("wait_for_input") == "true";

        // Carry variant selection into session variables so the execution log trace builder
        // can attach variant_index/variant_count to this node's node_trace entry.
        Dictionary<string, string>? variableUpdates = null;
        if (variantCount > 1)
        {
            variableUpdates = new Dictionary<string, string>
            {
                [$"__variant_index:{node.Id}"] = variantIndex.ToString(),
                [$"__variant_count:{node.Id}"] = variantCount.ToString()
            };
        }

        if (!waitForInput)
        {
            // Fire-and-forget: send message, continue to next node
            return Task.FromResult(new NodeResult
            {
                MessageText = message,
                Action = NodeAction.Continue,
                OutputHandle = null,
                VariableUpdates = variableUpdates
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
            },
            VariableUpdates = variableUpdates
        });
    }

    /// <summary>
    /// Resolve message text — prefer <c>text_variants</c> (JSON array) via rotation service,
    /// fall back to the legacy single <c>text</c> field when variants are missing/invalid.
    /// Invalid JSON is swallowed (silent fallback) to guarantee the customer still gets a message;
    /// a warning is logged with INV-AT-053 so it is visible in ops.
    /// </summary>
    private string ResolveTemplate(FlowNodeV2 node, ExecutionContext ctx, out int variantIndex, out int variantCount)
    {
        variantIndex = 0;
        variantCount = 0;

        var variantsRaw = node.GetData("text_variants");
        if (!string.IsNullOrWhiteSpace(variantsRaw))
        {
            try
            {
                using var doc = JsonDocument.Parse(variantsRaw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var variants = new List<string>(doc.RootElement.GetArrayLength());
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrEmpty(s))
                                variants.Add(s);
                        }
                    }

                    if (variants.Count > 0)
                    {
                        variantCount = variants.Count;
                        variantIndex = _rotation.PickVariantIndex(ctx.ContactKey, node.Id, variantCount);
                        return variants[variantIndex];
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[INV-AT-057] message_text node {node.Id} has invalid text_variants JSON, falling back to text: {ex.Message}");
            }
        }

        return node.GetData("text");
    }
}
