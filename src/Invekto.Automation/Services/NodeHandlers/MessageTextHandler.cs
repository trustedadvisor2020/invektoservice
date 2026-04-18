using System.Text.Json;
using Invekto.Automation.Services;
using Invekto.Shared.Auth;
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
///
/// FEAT-WTP (variant pool from Knowledge): when <c>data.group_tag</c> is set, the
/// handler fetches active+published tenant templates sharing that group_tag from the
/// Knowledge service and picks one deterministically by
/// <see cref="ITemplateRotationService"/>. State-free (same contact = same variant);
/// the rotation counter in leads.faq_rotation_state is NOT touched — that is reserved
/// for AiFaqHandler round-robin. Precedence: group_tag → text_variants → data.text.
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
    // FEAT-WTP: optional — tests and simulation can skip HTTP by passing null.
    private readonly KnowledgeSearchClient? _knowledgeClient;
    private readonly JwtGenerator? _jwtGenerator;

    public MessageTextHandler(
        ITemplateRotationService rotation,
        IMessageChunkPlanner chunkPlanner,
        JsonLinesLogger logger,
        KnowledgeSearchClient? knowledgeClient = null,
        JwtGenerator? jwtGenerator = null)
    {
        _rotation = rotation;
        _chunkPlanner = chunkPlanner;
        _logger = logger;
        _knowledgeClient = knowledgeClient;
        _jwtGenerator = jwtGenerator;
    }

    public string NodeType => "message_text";

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolved = await ResolveTemplateAsync(node, ctx, ct);
        var rawText = resolved.Text;
        var variantIndex = resolved.VariantIndex;
        var variantCount = resolved.VariantCount;
        var templateId = resolved.TemplateId;
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
            // FEAT-WTP: emit template_id when the pool came from the Knowledge catalog so
            // support can trace which row was rendered (grep [FEAT-WTP] in ops logs).
            if (templateId.HasValue)
                variableUpdates[$"__variant_template_id:{node.Id}"] = templateId.Value.ToString();
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
            return new NodeResult
            {
                MessageText = string.IsNullOrEmpty(messageText) ? null : messageText,
                Action = NodeAction.Continue,
                OutputHandle = null,
                VariableUpdates = variableUpdates
            };
        }

        // Phase 2: user already replied to this wait point
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            // User's reply is already in __last_input (set by orchestrator).
            // Also expose as user_input for {{user_input}} template compatibility.
            var userReply = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            ctx.State.Variables["user_input"] = userReply;

            return new NodeResult
            {
                MessageText = null, // Don't re-send the prompt
                Action = NodeAction.Continue,
                OutputHandle = null
            };
        }

        // Phase 1: first visit — send message and wait for user reply
        return new NodeResult
        {
            MessageText = string.IsNullOrEmpty(messageText) ? null : messageText,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput
            {
                Type = "text",
                Options = null
            },
            VariableUpdates = variableUpdates
        };
    }

    private readonly record struct ResolvedTemplate(string Text, int VariantIndex, int VariantCount, int? TemplateId);

    /// <summary>
    /// Resolve message text — precedence:
    ///   1. FEAT-WTP: <c>data.group_tag</c> → fetch tenant variant pool from Knowledge,
    ///      pick via rotation service. Empty pool / HTTP fail / simulation → next step.
    ///   2. G3: <c>data.text_variants</c> (JSON array) → rotation service pick.
    ///   3. Legacy: <c>data.text</c> (single balloon).
    /// Invalid JSON / transport failures are swallowed with a structured warning so the
    /// customer always receives SOME message.
    /// </summary>
    private async Task<ResolvedTemplate> ResolveTemplateAsync(
        FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        // Step 1: Knowledge-backed variant pool (FEAT-WTP).
        var groupTag = node.GetData("group_tag");
        if (!string.IsNullOrWhiteSpace(groupTag)
            && !ctx.IsSimulation
            && _knowledgeClient != null
            && _jwtGenerator != null
            && ctx.TenantId > 0)
        {
            // Prefer lead's preferred locale (HFM-2) but fall back to the node-declared
            // lang (explicit override) or no lang at all (let Knowledge return any match).
            var lang = !string.IsNullOrEmpty(ctx.LeadPreferredLocale)
                ? ctx.LeadPreferredLocale
                : node.GetData("lang");

            try
            {
                var jwt = _jwtGenerator.GenerateServiceToken(ctx.TenantId);
                var pool = await _knowledgeClient.FetchVariantPoolAsync(ctx.TenantId, groupTag, lang, jwt, ct);
                if (pool.Count > 0)
                {
                    var idx = _rotation.PickVariantIndex(ctx.ContactKey, node.Id, pool.Count);
                    var picked = pool[idx];
                    _logger.SystemInfo(
                        $"[FEAT-WTP] tenant={ctx.TenantId} node={node.Id} group_tag={groupTag} variant_index={idx}/{pool.Count} template_id={picked.Id}");
                    return new ResolvedTemplate(picked.Text, idx, pool.Count, picked.Id);
                }
                // Empty pool: caller supplied group_tag but Knowledge has no active+published
                // templates for it. Log INV-AT-066 once and fall through to text_variants.
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] empty pool tenant={ctx.TenantId} node={node.Id} group_tag={groupTag} lang={lang}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            // Note: KnowledgeSearchClient catches HTTP/JSON/timeout internally and returns
            // an empty list (see FetchVariantPoolAsync). We only catch here to defend
            // against unexpected exceptions leaking from JwtGenerator or the client itself.
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] pool fetch threw tenant={ctx.TenantId} node={node.Id} group_tag={groupTag}: {ex.Message}");
            }
        }

        // Step 2: inline text_variants (G3 backwards-compat).
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
                        var idx = _rotation.PickVariantIndex(ctx.ContactKey, node.Id, variants.Count);
                        return new ResolvedTemplate(variants[idx], idx, variants.Count, null);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationTemplateVariantInvalid}] message_text node {node.Id} has invalid text_variants JSON, falling back to text: {ex.Message}");
            }
        }

        // Step 3: legacy single text.
        return new ResolvedTemplate(node.GetData("text") ?? string.Empty, 0, 0, null);
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
