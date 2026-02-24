using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Benchmark;

/// <summary>
/// Tiered classification: Gemini 2.5 Flash (primary) + Claude Haiku (escalation).
/// If Flash confidence < threshold, escalate to Haiku and take the higher confidence result.
/// Effective cost: ~$1.8/1K threads (80% single-model, 20% escalated).
/// </summary>
public sealed class TieredClassifierService : ILlmClient
{
    private readonly ILlmClient _primary;   // Gemini 2.5 Flash
    private readonly ILlmClient _fallback;  // Claude Haiku
    private readonly JsonLinesLogger _logger;
    private readonly float _escalationThreshold;

    public string ModelName => "tiered(flash+haiku)";
    public bool IsAvailable => _primary.IsAvailable; // At minimum primary must work

    public TieredClassifierService(
        ILlmClient primary,
        ILlmClient fallback,
        JsonLinesLogger logger,
        float escalationThreshold = 0.80f)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
        _escalationThreshold = escalationThreshold;
    }

    public async Task<string?> ClassifyAsync(string systemPrompt, string userContent, CancellationToken ct)
    {
        // Step 1: Primary model (Gemini 2.5 Flash)
        var primaryResult = await _primary.ClassifyAsync(systemPrompt, userContent, ct);
        if (primaryResult == null)
        {
            // Primary failed — try fallback directly
            if (_fallback.IsAvailable)
            {
                _logger.SystemInfo("[TieredClassifier] Primary failed, escalating to fallback");
                return await _fallback.ClassifyAsync(systemPrompt, userContent, ct);
            }
            return null;
        }

        // Parse to check confidence
        var parsed = LlmClassification.Parse(primaryResult);
        if (parsed == null)
        {
            // Couldn't parse — escalate
            if (_fallback.IsAvailable)
            {
                _logger.SystemInfo("[TieredClassifier] Primary parse failed, escalating");
                return await _fallback.ClassifyAsync(systemPrompt, userContent, ct);
            }
            return primaryResult; // Return raw, let caller handle
        }

        // Step 2: Check confidence threshold
        if (parsed.Confidence >= _escalationThreshold)
        {
            // High confidence — use primary result
            return primaryResult;
        }

        // Step 3: Escalate to fallback (Claude Haiku)
        if (!_fallback.IsAvailable)
            return primaryResult; // No fallback available, use primary anyway

        _logger.SystemInfo($"[TieredClassifier] Low confidence ({parsed.Confidence:F2} < {_escalationThreshold:F2}), escalating");
        var fallbackResult = await _fallback.ClassifyAsync(systemPrompt, userContent, ct);
        if (fallbackResult == null)
            return primaryResult; // Fallback failed, keep primary

        var fallbackParsed = LlmClassification.Parse(fallbackResult);
        if (fallbackParsed == null)
            return primaryResult; // Fallback parse failed

        // Take the higher confidence result
        return fallbackParsed.Confidence > parsed.Confidence ? fallbackResult : primaryResult;
    }
}
