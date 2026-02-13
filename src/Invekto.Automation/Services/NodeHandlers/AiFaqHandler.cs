namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// FAQ keyword match node. Wait for user input, then search tenant's FAQ entries
/// via FaqMatcher (production/DB) or MockFaqMatcher (simulation).
/// On match: auto-sends FAQ answer as message + stores in variables.
/// 2 output handles: matched, no_match.
/// Variables set: faq_answer, faq_confidence, faq_question.
/// </summary>
public sealed class AiFaqHandler : INodeHandler
{
    private readonly FaqMatcher _faqMatcher;
    private readonly MockFaqMatcher _mockFaqMatcher;

    public string NodeType => "ai_faq";

    public AiFaqHandler(FaqMatcher faqMatcher, MockFaqMatcher mockFaqMatcher)
    {
        _faqMatcher = faqMatcher;
        _mockFaqMatcher = mockFaqMatcher;
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

        // First visit -- wait for user input
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
        var minConfidence = ParseConfidence(node.GetData("min_confidence"), 0.3);

        string? answer = null;
        string? matchedQuestion = null;
        double confidence = 0;

        if (ctx.IsSimulation)
        {
            // Simulation: use MockFaqMatcher (no DB access)
            var mockResult = _mockFaqMatcher.Match(userInput);
            if (mockResult != null)
            {
                answer = mockResult.Answer;
                matchedQuestion = $"Mock: keyword '{mockResult.MatchedKeyword}'";
                confidence = mockResult.Confidence;
            }
        }
        else
        {
            // Production: use FaqMatcher (DB query, tenant-scoped)
            var result = await _faqMatcher.FindMatchAsync(ctx.TenantId, userInput, ct);
            if (result != null)
            {
                answer = result.Answer;
                matchedQuestion = result.MatchedQuestion;
                confidence = result.Confidence;
            }
        }

        var isMatched = answer != null && confidence >= minConfidence;
        var handle = isMatched ? "matched" : "no_match";

        var variables = new Dictionary<string, string>
        {
            ["faq_answer"] = answer ?? "",
            ["faq_confidence"] = confidence.ToString("F2"),
            ["faq_question"] = matchedQuestion ?? ""
        };

        ctx.Logger.StepInfo(
            $"AiFaq '{label}': matched={isMatched}, confidence={confidence:F2}, " +
            $"minConfidence={minConfidence:F2}, handle={handle}, simulation={ctx.IsSimulation}",
            ctx.RequestId);

        // If matched, auto-send FAQ answer as message
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
