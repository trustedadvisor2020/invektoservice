using Invekto.Automation.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Knowledge-powered FAQ + document search node.
/// Calls Knowledge service semantic search (pgvector) instead of keyword-based FaqMatcher.
/// On FAQ match: auto-sends FAQ answer as message.
/// On chunk match: summarizes via Claude Haiku, then sends as message.
/// On Knowledge down or no match: routes to no_match (graceful degradation).
/// 2 output handles: matched, no_match.
/// Variables set: faq_answer, faq_confidence, faq_question, faq_source.
///
/// HFM-2: when the matched answer's language differs from the lead's
/// <see cref="ExecutionContext.LeadPreferredLocale"/>, the handler posts a translation
/// hop to the Backend translation service and returns the localized text. Any hop
/// failure degrades gracefully to the original answer (logged INV-AT-064).
/// Fallback chain: lead.preferred_locale → 'en' default → raw (untranslated).
/// </summary>
public sealed class AiFaqHandler : INodeHandler
{
    private readonly KnowledgeSearchClient _searchClient;
    private readonly ChunkSummarizer _chunkSummarizer;
    private readonly MockFaqMatcher _mockFaqMatcher;
    private readonly JwtGenerator _jwtGenerator;
    private readonly TranslationHopClient? _translationHop;
    // FEAT-WTP: rotation state read/write — nullable so legacy tests/sim still construct handler.
    private readonly AutomationRepository? _repo;

    private const int DefaultTopK = 3;
    private const double DefaultMinConfidence = 0.65;
    private const string DefaultFallbackLocale = "en";

    public string NodeType => "ai_faq";

    public AiFaqHandler(
        KnowledgeSearchClient searchClient,
        ChunkSummarizer chunkSummarizer,
        MockFaqMatcher mockFaqMatcher,
        JwtGenerator jwtGenerator,
        TranslationHopClient? translationHop = null,
        AutomationRepository? repo = null)
    {
        _searchClient = searchClient;
        _chunkSummarizer = chunkSummarizer;
        _mockFaqMatcher = mockFaqMatcher;
        _jwtGenerator = jwtGenerator;
        _translationHop = translationHop;
        _repo = repo;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Check if we have pending input for this node
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            var userInput = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            return await MatchAndRoute(node, ctx, userInput, ct);
        }

        // Reuse __last_input from preceding node (e.g. ai_intent -> ai_faq same-message chain)
        if (ctx.State.Variables.TryGetValue("__last_input", out var prev) && !string.IsNullOrWhiteSpace(prev))
        {
            ctx.Logger.StepInfo(
                $"AiFaq '{node.GetData("label", node.Id)}': reusing __last_input from preceding node",
                ctx.RequestId);
            return await MatchAndRoute(node, ctx, prev, ct);
        }

        // No input available -- wait for user input
        ctx.Logger.StepInfo(
            $"AiFaq '{node.GetData("label", node.Id)}': waiting for user input",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" }
        };
    }

    private async Task<NodeResult> MatchAndRoute(FlowNodeV2 node, ExecutionContext ctx, string userInput, CancellationToken ct)
    {
        var label = node.GetData("label", node.Id);
        var minConfidence = ParseConfidence(node.GetData("min_confidence"), DefaultMinConfidence);
        var searchSource = node.GetData("search_source", "all");

        string? answer = null;
        string? matchedQuestion = null;
        double confidence = 0;
        string source = "";

        if (ctx.IsSimulation)
        {
            // Simulation: use MockFaqMatcher (no DB/HTTP access)
            var mockResult = _mockFaqMatcher.Match(userInput);
            if (mockResult != null)
            {
                answer = mockResult.Answer;
                matchedQuestion = $"Mock: keyword '{mockResult.MatchedKeyword}'";
                confidence = mockResult.Confidence;
                source = "faq";
            }
        }
        else
        {
            // Production: call Knowledge service semantic search
            var jwt = _jwtGenerator.GenerateServiceToken(ctx.TenantId);
            var searchResult = await _searchClient.SearchAsync(
                ctx.TenantId, userInput, DefaultTopK, searchSource, jwt, ct);

            if (searchResult.Available && searchResult.Items.Count > 0)
            {
                var bestItem = searchResult.Items[0]; // Already sorted by score desc from Knowledge

                if (bestItem.SourceType == "faq" && !string.IsNullOrEmpty(bestItem.Answer))
                {
                    answer = bestItem.Answer;
                    matchedQuestion = bestItem.Question ?? "";
                    confidence = bestItem.Score;
                    source = "faq";
                }
                else if (bestItem.SourceType == "chunk" && !string.IsNullOrEmpty(bestItem.ChunkContent))
                {
                    confidence = bestItem.Score;
                    source = "chunk";
                    matchedQuestion = bestItem.DocumentTitle ?? "Dokuman";
                    if (bestItem.PageNumber.HasValue)
                        matchedQuestion += $" (sayfa {bestItem.PageNumber.Value})";

                    // Summarize chunk content via Claude for customer-friendly answer
                    var summary = await _chunkSummarizer.SummarizeAsync(bestItem.ChunkContent, userInput, ct);
                    answer = summary; // null if summarization failed
                }

                ctx.Logger.StepInfo(
                    $"AiFaq '{label}': Knowledge {searchResult.Method} search, " +
                    $"topResult={bestItem.SourceType}, score={bestItem.Score:F2}, " +
                    $"durationMs={searchResult.DurationMs}",
                    ctx.RequestId);
            }
            else if (!searchResult.Available)
            {
                ctx.Logger.StepInfo(
                    $"AiFaq '{label}': Knowledge unavailable ({searchResult.UnavailableReason}), routing no_match",
                    ctx.RequestId);
            }
        }

        var isMatched = answer != null && confidence >= minConfidence;
        var handle = isMatched ? "matched" : "no_match";

        // FEAT-WTP: rotation_group_tag override. When the flow node declares
        // data.rotation_group_tag AND the semantic search matched (so we know the intent fired),
        // replace the Knowledge-returned answer with a deterministic + round-robin pick from
        // template_catalog. Knowledge search still runs above for the matched/no_match routing
        // decision and for the attribution log.
        //
        // Fall-throughs (empty pool / HTTP fail / deps missing) keep the Knowledge answer —
        // the customer never sees a blank reply.
        var outboundAnswer = answer;
        int? rotationIndex = null;
        int? rotationCount = null;
        int? rotationTemplateId = null;
        string? rotationGroupTag = null;

        var declaredGroupTag = node.GetData("rotation_group_tag");
        if (isMatched
            && !string.IsNullOrWhiteSpace(declaredGroupTag)
            && !ctx.IsSimulation
            && _repo != null
            && !string.IsNullOrWhiteSpace(ctx.ContactKey))
        {
            var rotationResult = await ApplyRotationAsync(ctx, declaredGroupTag, ct);
            if (rotationResult != null)
            {
                outboundAnswer = rotationResult.Value.Text;
                rotationIndex = rotationResult.Value.Index;
                rotationCount = rotationResult.Value.Count;
                rotationTemplateId = rotationResult.Value.TemplateId;
                rotationGroupTag = declaredGroupTag;
            }
        }

        // HFM-2: post-match translation hook. Applied only on a matched freeform answer
        // (chunk summaries are translation targets too, intentional — operators author TR
        // FAQ content but EN/DE leads see their language). Graceful degradation on failure:
        // keep the original answer so the customer never sees a blank reply.
        if (isMatched && !string.IsNullOrEmpty(outboundAnswer) && _translationHop != null && !ctx.IsSimulation)
        {
            outboundAnswer = await MaybeTranslateAsync(outboundAnswer, ctx, ct) ?? outboundAnswer;
        }

        var variables = new Dictionary<string, string>
        {
            ["faq_answer"] = outboundAnswer ?? "",
            ["faq_confidence"] = confidence.ToString("F2"),
            ["faq_question"] = matchedQuestion ?? "",
            ["faq_source"] = source
        };
        if (rotationCount.HasValue && rotationCount.Value > 0)
        {
            variables["faq_variant_index"] = rotationIndex!.Value.ToString();
            variables["faq_variant_count"] = rotationCount.Value.ToString();
            variables["faq_rotation_group_tag"] = rotationGroupTag ?? "";
            if (rotationTemplateId.HasValue)
                variables["faq_variant_template_id"] = rotationTemplateId.Value.ToString();
        }

        ctx.Logger.StepInfo(
            $"AiFaq '{label}': matched={isMatched}, confidence={confidence:F2}, " +
            $"minConfidence={minConfidence:F2}, handle={handle}, source={source}, simulation={ctx.IsSimulation}" +
            (rotationCount.HasValue ? $", rotation={rotationIndex}/{rotationCount} gt={rotationGroupTag}" : ""),
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = isMatched ? outboundAnswer : null,
            Action = NodeAction.Continue,
            OutputHandle = handle,
            VariableUpdates = variables
        };
    }

    /// <summary>
    /// FEAT-WTP rotation application. Returns null when the rotation cannot be applied
    /// (empty pool / fetch failure / no ContactKey) so the caller keeps the Knowledge answer.
    /// Counter semantics: the persisted index is ALWAYS the NEXT one to emit. After picking
    /// variant at (persistedIdx % count), we persist (persistedIdx + 1) % count.
    /// Per-chat ordering (orchestrator's single-flight lock per (tenant, phone)) means we
    /// do not need a DB-side CAS.
    /// </summary>
    private async Task<RotationPick?> ApplyRotationAsync(
        ExecutionContext ctx, string groupTag, CancellationToken ct)
    {
        // Defensive guard — caller already checked _repo != null, but keep this check local
        // so future refactors do not silently re-introduce a null dereference.
        var repo = _repo;
        if (repo == null)
            return null;

        var lang = !string.IsNullOrEmpty(ctx.LeadPreferredLocale) ? ctx.LeadPreferredLocale : null;
        var jwt = _jwtGenerator.GenerateServiceToken(ctx.TenantId);

        var pool = await _searchClient.FetchVariantPoolAsync(ctx.TenantId, groupTag, lang, jwt, ct);
        if (pool.Count == 0)
        {
            // KnowledgeSearchClient already logged INV-AT-066 for transport issues; empty pool
            // is a configuration signal logged here so ops can distinguish missing-content from
            // transport failure.
            ctx.Logger.SystemWarn(
                $"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] empty pool tenant={ctx.TenantId} group_tag={groupTag} lang={lang ?? "(any)"}");
            return null;
        }

        // ContactKey may be phone or chatId. UpsertLeadFaqRotationState keys on phone; passing
        // the chatId form is still safe (rows are keyed (tenant_id, phone); phone=chatId means
        // we partition rotation state by chat instead of lead when phone is unavailable — the
        // deterministic rotation invariant is preserved).
        var contactKey = ctx.ContactKey ?? string.Empty;

        var persistedIdx = await repo.GetLeadFaqRotationIndexAsync(ctx.TenantId, contactKey, groupTag, ct);
        var bounded = persistedIdx % pool.Count;
        if (bounded < 0) bounded = 0;

        var picked = pool[bounded];
        var next = (bounded + 1) % pool.Count;

        // Best-effort write: failure is logged (INV-AT-061/067) inside the repo. Check the
        // return value so the success log tells ops whether the counter actually advanced;
        // even on false we still serve the picked variant (deterministic fallback) — the
        // only user-visible effect is that the SAME lead may see the same variant next time.
        var persisted = await repo.UpdateLeadFaqRotationStateAsync(ctx.TenantId, contactKey, groupTag, next, ct);

        if (persisted)
        {
            ctx.Logger.SystemInfo(
                $"[FEAT-WTP] rotation tenant={ctx.TenantId} phone={contactKey} group_tag={groupTag} " +
                $"picked_index={bounded}/{pool.Count} template_id={picked.Id} next={next} persisted=true");
        }
        else
        {
            // Repo already logged the specific INV-AT-061/067; surface the state advancement
            // failure here so the structured [FEAT-WTP] trail remains coherent for support.
            ctx.Logger.SystemWarn(
                $"[{ErrorCodes.AutomationFaqRotationStateUpdateFailed}] rotation counter not advanced " +
                $"tenant={ctx.TenantId} phone={contactKey} group_tag={groupTag} picked_index={bounded}/{pool.Count} " +
                $"template_id={picked.Id}; variant still served, lead may see same variant next time");
        }

        return new RotationPick(picked.Text, bounded, pool.Count, picked.Id);
    }

    private readonly record struct RotationPick(string Text, int Index, int Count, int TemplateId);

    /// <summary>
    /// HFM-2 fallback chain: lead.preferred_locale → 'en' default → raw.
    /// Returns the translated text when a hop occurred, null when translation was
    /// skipped (no locale, same locale) or failed (graceful degrade to original).
    /// </summary>
    private async Task<string?> MaybeTranslateAsync(string answer, ExecutionContext ctx, CancellationToken ct)
    {
        var targetLocale = ResolveTargetLocale(ctx.LeadPreferredLocale);
        if (targetLocale == null)
        {
            // No locale data at all → default to 'en'. If the answer is already English-ish
            // (Latin script, short), translation adds latency for no gain — skip by convention.
            targetLocale = DefaultFallbackLocale;
        }

        // Cheap heuristic short-circuit: if the first non-whitespace chars already look like
        // the target language (Latin for en/de/fr/es/pt/nl/it), we still call the service —
        // Gemma is idempotent for same-language input and the cache absorbs repeat hits.
        // The service itself returns input unchanged when source==target.

        ctx.Logger.StepInfo(
            $"AiFaq translate attempt: target={targetLocale}, leadLocale={ctx.LeadPreferredLocale ?? "(none)"}",
            ctx.RequestId);

        var translated = await _translationHop!.TranslateAsync(ctx.TenantId, answer, targetLocale, ctx.RequestId, ct);
        return translated;
    }

    private static string? ResolveTargetLocale(string? leadLocale)
    {
        if (string.IsNullOrWhiteSpace(leadLocale))
            return null;

        var trimmed = leadLocale.Trim();
        var dash = trimmed.IndexOf('-');
        if (dash > 0)
            trimmed = trimmed[..dash];

        return trimmed.Length == 2 ? trimmed.ToLowerInvariant() : null;
    }

    private static double ParseConfidence(string? raw, double fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, 0.0, 1.0)
            : fallback;
    }
}
