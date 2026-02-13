using System.Text.Json;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// AI intent detection node. Wait for user input, then classify via Claude Haiku (production)
/// or MockIntentDetector (simulation).
/// 2 output handles: high_confidence, low_confidence.
/// Variables set: detected_intent, intent_confidence, intent_summary.
/// </summary>
public sealed class AiIntentHandler : INodeHandler
{
    private readonly IntentDetector _intentDetector;
    private readonly MockIntentDetector _mockIntentDetector;

    public string NodeType => "ai_intent";

    public AiIntentHandler(IntentDetector intentDetector, MockIntentDetector mockIntentDetector)
    {
        _intentDetector = intentDetector;
        _mockIntentDetector = mockIntentDetector;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Check if we have pending input for this node (user already typed)
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            var userInput = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            return await DetectAndRoute(node, ctx, userInput, ct);
        }

        // First visit -- wait for user input
        ctx.Logger.StepInfo(
            $"AiIntent '{node.GetData("label", node.Id)}': waiting for user input",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" }
        };
    }

    private async Task<NodeResult> DetectAndRoute(FlowNodeV2 node, ExecutionContext ctx, string userInput, CancellationToken ct)
    {
        var label = node.GetData("label", node.Id);
        var threshold = ParseThreshold(node.GetData("confidence_threshold"), 0.5);
        var customIntents = ParseIntents(node.GetData("intents"), ctx);

        string? detectedIntent = null;
        double confidence = 0;
        string summary = "";

        if (ctx.IsSimulation)
        {
            // Simulation: use MockIntentDetector (no API call)
            var mockResult = _mockIntentDetector.Detect(userInput);
            if (mockResult != null)
            {
                detectedIntent = mockResult.Intent;
                confidence = mockResult.Confidence;
                summary = $"Mock: keyword '{mockResult.MatchedKeyword}'";
            }
        }
        else
        {
            // Production: use Claude Haiku via IntentDetector
            var result = await _intentDetector.DetectAsync(userInput, customIntents, ct);
            if (result != null)
            {
                detectedIntent = result.Intent;
                confidence = result.Confidence;
                summary = result.Summary;
            }
        }

        // Route based on confidence
        var isHighConfidence = confidence >= threshold && detectedIntent != null;
        var handle = isHighConfidence ? "high_confidence" : "low_confidence";

        var variables = new Dictionary<string, string>
        {
            ["detected_intent"] = detectedIntent ?? "unknown",
            ["intent_confidence"] = confidence.ToString("F2"),
            ["intent_summary"] = summary
        };

        ctx.Logger.StepInfo(
            $"AiIntent '{label}': intent={detectedIntent ?? "none"}, confidence={confidence:F2}, " +
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

    private static double ParseThreshold(string? raw, double fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, 0.0, 1.0)
            : fallback;
    }

    private static string[]? ParseIntents(string? rawJson, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson == "[]")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var intents = doc.RootElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToArray();
            return intents.Length > 0 ? intents : null;
        }
        catch (JsonException ex)
        {
            ctx.Logger.SystemWarn($"AiIntent: intents JSON parse failed: {ex.Message}, raw={rawJson}");
            return null;
        }
    }
}
