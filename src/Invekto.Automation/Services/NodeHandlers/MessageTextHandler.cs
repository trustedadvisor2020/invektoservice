using System.Text.Json;
using Invekto.Shared.Constants;
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
///
/// HFM-1 (hybrid chunk resolution):
///   1. <c>data.text_chunks</c> (JSON array) explicit chunks → planner + sentinel emit
///   2. legacy <c>data.text</c> with <c>"\n\n"</c> paragraph breaks → auto-split soft opt-in
///   3. legacy <c>data.text</c> single balloon (unchanged)
///
/// Chunked output is serialized into a sentinel-prefixed payload so the engine's
/// <c>List&lt;string&gt; Messages</c> stream carries it unchanged. Orchestrator detects
/// the prefix and dispatches one callback per chunk with pre-delays from
/// <see cref="IMessageChunkPlanner"/>.
/// </summary>
public sealed class MessageTextHandler : INodeHandler
{
    /// <summary>
    /// Prefix that marks a chunked message emission. Must start with an ASCII control
    /// character so it cannot collide with natural text. Consumers match the full prefix.
    /// </summary>
    public const string ChunkSentinel = "\u001EHFM1_CHUNKS\u001E";

    private readonly ITemplateRotationService _rotation;
    private readonly IMessageChunkPlanner _chunkPlanner;
    private readonly JsonLinesLogger _logger;

    public MessageTextHandler(
        ITemplateRotationService rotation,
        IMessageChunkPlanner chunkPlanner,
        JsonLinesLogger logger)
    {
        _rotation = rotation;
        _chunkPlanner = chunkPlanner;
        _logger = logger;
    }

    public string NodeType => "message_text";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var rawText = ResolveTemplate(node, ctx, out var variantIndex, out var variantCount);
        var resolvedChunks = ResolveChunks(node, rawText);

        // Substitute variables on each chunk (or single text) so the planner operates on final lengths.
        List<string> finalChunks;
        if (resolvedChunks != null)
        {
            finalChunks = new List<string>(resolvedChunks.Count);
            foreach (var c in resolvedChunks)
            {
                var rendered = ctx.Evaluator.Substitute(c, ctx.State.Variables);
                if (!string.IsNullOrWhiteSpace(rendered))
                    finalChunks.Add(rendered);
            }
        }
        else
        {
            finalChunks = new List<string> { ctx.Evaluator.Substitute(rawText, ctx.State.Variables) };
        }

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

        // HFM-1: build the message payload. Single chunk → plain string (legacy).
        // Multiple chunks → sentinel+JSON so the orchestrator can dispatch with delays.
        string messageText;
        if (finalChunks.Count <= 1)
        {
            messageText = finalChunks.Count == 1 ? finalChunks[0] : "";
        }
        else
        {
            var schedule = _chunkPlanner.Plan(finalChunks);
            messageText = EncodeChunkPayload(schedule);
        }

        if (!waitForInput)
        {
            return Task.FromResult(new NodeResult
            {
                MessageText = string.IsNullOrEmpty(messageText) ? null : messageText,
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
            MessageText = string.IsNullOrEmpty(messageText) ? null : messageText,
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
    /// a warning is logged with INV-AT-057 so it is visible in ops.
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
                _logger.SystemWarn($"[{ErrorCodes.AutomationTemplateVariantInvalid}] message_text node {node.Id} has invalid text_variants JSON, falling back to text: {ex.Message}");
            }
        }

        return node.GetData("text");
    }

    /// <summary>
    /// HFM-1 hybrid chunk resolution. Returns null when no chunking is requested
    /// (caller treats the raw text as a single balloon — legacy path).
    /// </summary>
    private List<string>? ResolveChunks(FlowNodeV2 node, string resolvedText)
    {
        var chunksRaw = node.GetData("text_chunks");
        if (!string.IsNullOrWhiteSpace(chunksRaw))
        {
            try
            {
                using var doc = JsonDocument.Parse(chunksRaw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var chunks = new List<string>(doc.RootElement.GetArrayLength());
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                                chunks.Add(s);
                        }
                    }

                    if (chunks.Count > 0)
                        return chunks;
                }
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationChunkScheduleInvalid}] message_text node {node.Id} has invalid text_chunks JSON, falling back to text: {ex.Message}");
            }
        }

        // Soft opt-in: double-newline in plain text → auto-split (operators who already
        // author paragraph breaks get chunking for free, no schema change needed).
        if (!string.IsNullOrEmpty(resolvedText) && resolvedText.Contains("\n\n", StringComparison.Ordinal))
        {
            var parts = resolvedText.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var list = new List<string>(parts.Length);
                foreach (var p in parts)
                {
                    var trimmed = p.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        list.Add(trimmed);
                }
                if (list.Count > 1)
                    return list;
            }
        }

        return null;
    }

    /// <summary>
    /// Encode a chunk schedule as <c>{sentinel}{json}</c>. The orchestrator detects the sentinel
    /// and deserializes back into <see cref="ChunkStep"/> entries for delay-aware dispatch.
    /// </summary>
    public static string EncodeChunkPayload(IReadOnlyList<ChunkStep> schedule)
    {
        var payload = new List<object>(schedule.Count);
        for (var i = 0; i < schedule.Count; i++)
            payload.Add(new { t = schedule[i].Text, d = schedule[i].PreDelayMs });

        return ChunkSentinel + JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Decode a chunk payload emitted by <see cref="EncodeChunkPayload"/>.
    /// Returns null when the input is not a chunked payload or the JSON is malformed
    /// (caller dispatches as a single legacy message).
    /// </summary>
    /// <summary>
    /// Decode a chunk payload emitted by <see cref="EncodeChunkPayload"/>.
    /// Returns null when the input is not a chunked payload OR the JSON is malformed.
    /// When malformed, a callback is invoked so the orchestrator can log INV-AT-062 and
    /// still dispatch the raw text as a single legacy message (never blocks delivery).
    /// </summary>
    public static IReadOnlyList<ChunkStep>? TryDecodeChunkPayload(string? messageText, Action<string>? onDecodeError = null)
    {
        if (string.IsNullOrEmpty(messageText) || !messageText.StartsWith(ChunkSentinel, StringComparison.Ordinal))
            return null;

        var json = messageText[ChunkSentinel.Length..];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                onDecodeError?.Invoke($"chunk payload root is not a JSON array (kind={doc.RootElement.ValueKind})");
                return null;
            }

            var steps = new List<ChunkStep>(doc.RootElement.GetArrayLength());
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var text = el.TryGetProperty("t", out var tp) && tp.ValueKind == JsonValueKind.String
                    ? tp.GetString() : null;
                var delay = el.TryGetProperty("d", out var dp) && dp.ValueKind == JsonValueKind.Number
                    ? dp.GetInt32() : 0;
                if (!string.IsNullOrEmpty(text))
                    steps.Add(new ChunkStep(text, Math.Max(0, delay)));
            }

            if (steps.Count == 0)
            {
                onDecodeError?.Invoke("chunk payload decoded but produced zero steps");
                return null;
            }
            return steps;
        }
        catch (JsonException ex)
        {
            onDecodeError?.Invoke($"chunk payload JSON parse failed: {ex.Message}");
            return null;
        }
    }
}
