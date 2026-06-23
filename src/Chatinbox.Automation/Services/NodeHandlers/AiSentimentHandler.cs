namespace Chatinbox.Automation.Services.NodeHandlers;

/// <summary>
/// AI sentiment detection node. Wait for user input, then classify via Claude Haiku (production)
/// or MockSentimentAnalyzer (simulation).
/// 2 output handles: positive, negative.
/// Variables set: sentiment_result, sentiment_score, sentiment_summary.
/// </summary>
public sealed class AiSentimentHandler : INodeHandler
{
    private readonly SentimentAnalyzer _sentimentAnalyzer;
    private readonly MockSentimentAnalyzer _mockSentimentAnalyzer;

    public string NodeType => "ai_sentiment";

    public AiSentimentHandler(SentimentAnalyzer sentimentAnalyzer, MockSentimentAnalyzer mockSentimentAnalyzer)
    {
        _sentimentAnalyzer = sentimentAnalyzer;
        _mockSentimentAnalyzer = mockSentimentAnalyzer;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Check if we have pending input for this node (user already typed)
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            var userInput = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            return await AnalyzeAndRoute(node, ctx, userInput, ct);
        }

        // First visit — wait for user input
        ctx.Logger.StepInfo(
            $"AiSentiment '{node.GetData("label", node.Id)}': waiting for user input",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" }
        };
    }

    private async Task<NodeResult> AnalyzeAndRoute(FlowNodeV2 node, ExecutionContext ctx, string userInput, CancellationToken ct)
    {
        var label = node.GetData("label", node.Id);
        var threshold = ParseThreshold(node.GetData("threshold"));

        string sentiment;
        double score;
        string summary;

        if (ctx.IsSimulation)
        {
            var mockResult = _mockSentimentAnalyzer.Detect(userInput);
            sentiment = mockResult.Sentiment;
            score = mockResult.Score;
            summary = $"Mock: keyword '{mockResult.MatchedKeyword}'";
        }
        else
        {
            var result = await _sentimentAnalyzer.AnalyzeAsync(userInput, ct);
            if (result != null)
            {
                sentiment = result.Sentiment;
                score = result.Score;
                summary = result.Summary;
            }
            else
            {
                // Graceful degradation: treat as neutral/negative
                sentiment = "neutral";
                score = 0.5;
                summary = "Sentiment analysis failed — defaulting to neutral";
            }
        }

        // Route: score >= threshold → positive, otherwise negative
        var isPositive = score >= threshold && sentiment != "negative";
        var handle = isPositive ? "positive" : "negative";

        var variables = new Dictionary<string, string>
        {
            ["sentiment_result"] = sentiment,
            ["sentiment_score"] = score.ToString("F2"),
            ["sentiment_summary"] = summary
        };

        ctx.Logger.StepInfo(
            $"AiSentiment '{label}': sentiment={sentiment}, score={score:F2}, " +
            $"threshold={threshold:F2}, handle={handle}, simulation={ctx.IsSimulation}",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = handle,
            VariableUpdates = variables
        };
    }

    private static double ParseThreshold(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return Math.Clamp(v, 0.0, 1.0);

        return 0.5;
    }
}
