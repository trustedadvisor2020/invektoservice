using Invekto.Shared.Auth;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Knowledge-powered FAQ + document search node.
/// Calls Knowledge service semantic search (pgvector) instead of keyword-based FaqMatcher.
/// On FAQ match: auto-sends FAQ answer as message.
/// On chunk match: summarizes via Claude Haiku, then sends as message.
/// On Knowledge down or no match: routes to no_match (graceful degradation).
/// 2 output handles: matched, no_match.
/// Variables set: faq_answer, faq_confidence, faq_question, faq_source.
/// </summary>
public sealed class AiFaqHandler : INodeHandler
{
    private readonly KnowledgeSearchClient _searchClient;
    private readonly ChunkSummarizer _chunkSummarizer;
    private readonly MockFaqMatcher _mockFaqMatcher;
    private readonly JwtGenerator _jwtGenerator;

    private const int DefaultTopK = 3;
    private const double DefaultMinConfidence = 0.65;

    public string NodeType => "ai_faq";

    public AiFaqHandler(
        KnowledgeSearchClient searchClient,
        ChunkSummarizer chunkSummarizer,
        MockFaqMatcher mockFaqMatcher,
        JwtGenerator jwtGenerator)
    {
        _searchClient = searchClient;
        _chunkSummarizer = chunkSummarizer;
        _mockFaqMatcher = mockFaqMatcher;
        _jwtGenerator = jwtGenerator;
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

        var variables = new Dictionary<string, string>
        {
            ["faq_answer"] = answer ?? "",
            ["faq_confidence"] = confidence.ToString("F2"),
            ["faq_question"] = matchedQuestion ?? "",
            ["faq_source"] = source
        };

        ctx.Logger.StepInfo(
            $"AiFaq '{label}': matched={isMatched}, confidence={confidence:F2}, " +
            $"minConfidence={minConfidence:F2}, handle={handle}, source={source}, simulation={ctx.IsSimulation}",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = isMatched ? answer : null,
            Action = NodeAction.Continue,
            OutputHandle = handle,
            VariableUpdates = variables
        };
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
