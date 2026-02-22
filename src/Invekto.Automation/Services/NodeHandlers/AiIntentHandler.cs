using System.Text.Json;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// AI intent detection node with conversational multi-phase flow.
/// Phases: (1) ask name → (2) detect intent → (3) confirm suggested intent.
/// Features:
///   - Asks customer name, validates nonsensical input
///   - Addresses customer by name throughout
///   - Continues conversation until intent is clear (doesn't give up)
///   - "Bunu mu demek istiyorsunuz?" confirmation for medium-confidence intents
/// 2 output handles: high_confidence, low_confidence.
/// Variables set: customer_name, detected_intent, intent_confidence, intent_summary.
/// Internal variables (__prefix): __intent_phase, __intent_attempt, __intent_history,
///   __suggested_intent, __suggested_confidence, __suggested_summary.
/// </summary>
public sealed class AiIntentHandler : INodeHandler
{
    private readonly IntentDetector _intentDetector;
    private readonly MockIntentDetector _mockIntentDetector;
    private readonly ILogger<AiIntentHandler> _logger;

    /// <summary>Max clarification rounds before graceful low_confidence fallback.</summary>
    private const int MaxIntentAttempts = 5;

    public string NodeType => "ai_intent";

    public AiIntentHandler(IntentDetector intentDetector, MockIntentDetector mockIntentDetector, ILogger<AiIntentHandler> logger)
    {
        _intentDetector = intentDetector;
        _mockIntentDetector = mockIntentDetector;
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Returning user: pending input means user typed while we were waiting
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            var userInput = GetVar(ctx, "__last_input");
            var phase = GetVar(ctx, "__intent_phase", "awaiting_name");

            return phase switch
            {
                "awaiting_name" => HandleNamePhase(node, ctx, userInput),
                "awaiting_intent" => await HandleIntentPhase(node, ctx, userInput, ct),
                "awaiting_confirm" => await HandleConfirmPhase(node, ctx, userInput, ct),
                _ => await HandleIntentPhase(node, ctx, userInput, ct)
            };
        }

        // First visit — start conversation
        var shouldAskName = !string.Equals(
            node.GetData("ask_name", "true"), "false", StringComparison.OrdinalIgnoreCase);

        // If name is already known (e.g. from CRM/previous node), skip name phase
        var existingName = GetVar(ctx, "customer_name");
        if (shouldAskName && !string.IsNullOrEmpty(existingName))
            shouldAskName = false;

        if (shouldAskName)
        {
            var greeting = node.GetData("greeting_message");
            if (string.IsNullOrWhiteSpace(greeting))
                greeting = "Merhaba! Size yardımcı olabilmem için isminizi öğrenebilir miyim?";

            ctx.Logger.StepInfo(
                $"AiIntent '{node.GetData("label", node.Id)}': asking customer name",
                ctx.RequestId);

            return new NodeResult
            {
                MessageText = greeting,
                Action = NodeAction.WaitForInput,
                PendingInput = new PendingInput { Type = "text" },
                VariableUpdates = new Dictionary<string, string>
                {
                    ["__intent_phase"] = "awaiting_name"
                }
            };
        }

        // Skip name phase → go to intent detection
        var name = existingName ?? "";
        var askMsg = string.IsNullOrEmpty(name)
            ? "Size nasıl yardımcı olabilirim?"
            : $"{name}, size nasıl yardımcı olabilirim?";

        ctx.Logger.StepInfo(
            $"AiIntent '{node.GetData("label", node.Id)}': skipping name, asking intent",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = askMsg,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" },
            VariableUpdates = new Dictionary<string, string>
            {
                ["__intent_phase"] = "awaiting_intent",
                ["__intent_attempt"] = "0"
            }
        };
    }

    // ============================================================
    // Phase: Name collection & validation
    // ============================================================

    private NodeResult HandleNamePhase(FlowNodeV2 node, ExecutionContext ctx, string userInput)
    {
        var rawName = IntentDetector.ExtractName(userInput);
        var (isValid, message) = IntentDetector.ValidateName(rawName);

        if (!isValid)
        {
            ctx.Logger.StepInfo(
                $"AiIntent '{node.GetData("label", node.Id)}': name rejected: '{rawName}'",
                ctx.RequestId);

            return new NodeResult
            {
                MessageText = message,
                Action = NodeAction.WaitForInput,
                PendingInput = new PendingInput { Type = "text" },
                VariableUpdates = new Dictionary<string, string>
                {
                    ["__intent_phase"] = "awaiting_name"
                }
            };
        }

        // Capitalize with Turkish locale
        var name = CapitalizeTurkish(rawName);

        ctx.Logger.StepInfo(
            $"AiIntent '{node.GetData("label", node.Id)}': name accepted: '{name}'",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = $"{name}, size nasıl yardımcı olabilirim?",
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" },
            VariableUpdates = new Dictionary<string, string>
            {
                ["__intent_phase"] = "awaiting_intent",
                ["__intent_attempt"] = "0",
                ["customer_name"] = name
            }
        };
    }

    // ============================================================
    // Phase: Intent detection (conversational, multi-turn)
    // ============================================================

    private async Task<NodeResult> HandleIntentPhase(
        FlowNodeV2 node, ExecutionContext ctx, string userInput, CancellationToken ct)
    {
        var label = node.GetData("label", node.Id);
        var threshold = ParseThreshold(node.GetData("confidence_threshold"), ctx);
        var customIntents = ParseIntents(node.GetData("intents"), ctx);
        var customerName = GetVar(ctx, "customer_name");

        // Track attempts
        var attempt = int.TryParse(GetVar(ctx, "__intent_attempt", "0"), out var a) ? a : 0;
        attempt++;

        if (attempt > MaxIntentAttempts)
        {
            ctx.Logger.StepInfo(
                $"AiIntent '{label}': max attempts ({MaxIntentAttempts}) reached, routing to low_confidence",
                ctx.RequestId);

            return BuildRouteResult("unknown", 0, "Maksimum deneme asildi", "low_confidence");
        }

        string? detectedIntent = null;
        double confidence = 0;
        string summary = "";
        string? clarifyQuestion = null;

        if (ctx.IsSimulation)
        {
            // Simulation: keyword-based detection with custom intent support
            var mockResult = _mockIntentDetector.Detect(userInput, customIntents);
            if (mockResult != null)
            {
                detectedIntent = mockResult.Intent;
                confidence = mockResult.Confidence;
                summary = $"Mock: keyword '{mockResult.MatchedKeyword}'";
            }
            else
            {
                clarifyQuestion = string.IsNullOrEmpty(customerName)
                    ? "Ne hakkında yardımcı olabilirim? Örneğin kargo, fiyat, randevu gibi konularda sorularınızı yanıtlayabilirim."
                    : $"{customerName}, ne hakkında yardımcı olabilirim? Örneğin kargo, fiyat, randevu gibi konularda sorularınızı yanıtlayabilirim.";
            }
        }
        else
        {
            // Production: Claude Haiku with conversation history
            var history = GetHistory(ctx);
            var result = await _intentDetector.DetectConversationalAsync(
                userInput,
                string.IsNullOrEmpty(customerName) ? "Müşteri" : customerName,
                customIntents,
                history.Count > 0 ? history : null,
                ct);

            if (result != null)
            {
                detectedIntent = result.Intent;
                confidence = result.Confidence;
                summary = result.Summary;
                clarifyQuestion = result.ClarifyQuestion;
            }
        }

        // HIGH confidence → route immediately
        if (confidence >= threshold && detectedIntent != null)
        {
            ctx.Logger.StepInfo(
                $"AiIntent '{label}': high confidence intent={detectedIntent}, confidence={confidence:F2}",
                ctx.RequestId);

            return BuildRouteResult(detectedIntent, confidence, summary, "high_confidence");
        }

        // MEDIUM confidence → "bunu mu demek istiyorsunuz?"
        if (detectedIntent != null && confidence >= threshold * 0.5)
        {
            var confirmMsg = clarifyQuestion
                ?? (string.IsNullOrEmpty(customerName)
                    ? $"Şunu mu demek istiyorsunuz: {summary}?"
                    : $"{customerName}, şunu mu demek istiyorsunuz: {summary}?");

            var history = GetHistory(ctx);
            history.Add(new ConversationTurn { Role = "user", Content = userInput });
            history.Add(new ConversationTurn { Role = "assistant", Content = confirmMsg });

            ctx.Logger.StepInfo(
                $"AiIntent '{label}': medium confidence intent={detectedIntent} ({confidence:F2}), asking confirmation",
                ctx.RequestId);

            return new NodeResult
            {
                MessageText = confirmMsg,
                Action = NodeAction.WaitForInput,
                PendingInput = new PendingInput { Type = "text" },
                VariableUpdates = new Dictionary<string, string>
                {
                    ["__intent_phase"] = "awaiting_confirm",
                    ["__intent_attempt"] = attempt.ToString(),
                    ["__suggested_intent"] = detectedIntent,
                    ["__suggested_confidence"] = confidence.ToString("F2"),
                    ["__suggested_summary"] = summary,
                    ["__intent_history"] = SerializeHistory(history)
                }
            };
        }

        // LOW confidence → ask clarifying question, stay in intent phase
        var askMsg = clarifyQuestion
            ?? (string.IsNullOrEmpty(customerName)
                ? "Biraz daha açıklar mısınız?"
                : $"{customerName}, biraz daha açıklar mısınız?");

        var hist = GetHistory(ctx);
        hist.Add(new ConversationTurn { Role = "user", Content = userInput });
        hist.Add(new ConversationTurn { Role = "assistant", Content = askMsg });

        ctx.Logger.StepInfo(
            $"AiIntent '{label}': low confidence ({confidence:F2}), asking clarification (attempt {attempt})",
            ctx.RequestId);

        return new NodeResult
        {
            MessageText = askMsg,
            Action = NodeAction.WaitForInput,
            PendingInput = new PendingInput { Type = "text" },
            VariableUpdates = new Dictionary<string, string>
            {
                ["__intent_phase"] = "awaiting_intent",
                ["__intent_attempt"] = attempt.ToString(),
                ["__intent_history"] = SerializeHistory(hist)
            }
        };
    }

    // ============================================================
    // Phase: Confirm suggested intent ("bunu mu demek istiyorsunuz?")
    // ============================================================

    private async Task<NodeResult> HandleConfirmPhase(
        FlowNodeV2 node, ExecutionContext ctx, string userInput, CancellationToken ct)
    {
        var label = node.GetData("label", node.Id);
        var confirmation = IntentDetector.ParseConfirmation(userInput);

        if (confirmation == true)
        {
            // User confirmed the suggested intent
            var intent = GetVar(ctx, "__suggested_intent", "unknown");
            var conf = double.TryParse(
                GetVar(ctx, "__suggested_confidence", "0.5"),
                System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0.5;
            var summary = GetVar(ctx, "__suggested_summary");

            ctx.Logger.StepInfo(
                $"AiIntent '{label}': user confirmed intent={intent}",
                ctx.RequestId);

            return BuildRouteResult(intent, conf, summary, "high_confidence");
        }

        if (confirmation == false)
        {
            // User denied → back to intent detection
            var customerName = GetVar(ctx, "customer_name");
            var askMsg = string.IsNullOrEmpty(customerName)
                ? "Anlıyorum. Peki, tam olarak ne konuda yardımcı olabilirim?"
                : $"Anlıyorum {customerName}. Peki, tam olarak ne konuda yardımcı olabilirim?";

            var history = GetHistory(ctx);
            history.Add(new ConversationTurn { Role = "user", Content = userInput });
            history.Add(new ConversationTurn { Role = "assistant", Content = askMsg });

            ctx.Logger.StepInfo(
                $"AiIntent '{label}': user denied suggestion, asking again",
                ctx.RequestId);

            return new NodeResult
            {
                MessageText = askMsg,
                Action = NodeAction.WaitForInput,
                PendingInput = new PendingInput { Type = "text" },
                VariableUpdates = new Dictionary<string, string>
                {
                    ["__intent_phase"] = "awaiting_intent",
                    ["__intent_history"] = SerializeHistory(history)
                }
            };
        }

        // Can't parse as yes/no → treat as a new intent message
        ctx.Logger.StepInfo(
            $"AiIntent '{label}': unclear confirmation, treating as new intent input",
            ctx.RequestId);

        return await HandleIntentPhase(node, ctx, userInput, ct);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static NodeResult BuildRouteResult(
        string intent, double confidence, string summary, string handle)
    {
        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = handle,
            VariableUpdates = new Dictionary<string, string>
            {
                ["detected_intent"] = intent,
                ["intent_confidence"] = confidence.ToString("F2"),
                ["intent_summary"] = summary
            }
        };
    }

    private static string GetVar(ExecutionContext ctx, string key, string defaultValue = "")
        => ctx.State.Variables.TryGetValue(key, out var v) ? v : defaultValue;

    private List<ConversationTurn> GetHistory(ExecutionContext ctx)
    {
        if (!ctx.State.Variables.TryGetValue("__intent_history", out var json) || string.IsNullOrWhiteSpace(json))
            return new();

        try
        {
            return JsonSerializer.Deserialize<List<ConversationTurn>>(json) ?? new();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("Corrupted intent history JSON, resetting: {Error}", ex.Message);
            return new();
        }
    }

    private static string SerializeHistory(List<ConversationTurn> history)
    {
        // Keep last 10 turns to avoid token bloat
        var trimmed = history.Count > 10 ? history.GetRange(history.Count - 10, 10) : history;
        return JsonSerializer.Serialize(trimmed);
    }

    private static string CapitalizeTurkish(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        return culture.TextInfo.ToTitleCase(name.ToLower(culture));
    }

    /// <summary>
    /// Fallback chain: node config > tenant settings > 0.5 default.
    /// </summary>
    private static double ParseThreshold(string? raw, ExecutionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return Math.Clamp(v, 0.0, 1.0);

        return ctx.TenantConfidenceThreshold;
    }

    /// <summary>
    /// Fallback chain: node config intents > ctx.TenantIntents (from Knowledge DB) > null (use defaults).
    /// </summary>
    private static string[]? ParseIntents(string? rawJson, ExecutionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(rawJson) && rawJson != "[]")
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var intents = doc.RootElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToArray();
                if (intents.Length > 0)
                    return intents;
            }
            catch (JsonException ex)
            {
                ctx.Logger.SystemWarn($"AiIntent: intents JSON parse failed: {ex.Message}, raw={rawJson}");
            }
        }

        return ctx.TenantIntents;
    }
}
