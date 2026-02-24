using System.Text.Json;

namespace Invekto.WhatsAppAnalytics.Models;

/// <summary>
/// wa_benchmark_jobs row.
/// </summary>
public sealed class BenchmarkJob
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Status { get; set; } = "pending";
    public string DatabaseName { get; set; } = "";
    public int? InstanceId { get; set; }
    public int SampleSize { get; set; } = 200;
    public int? ActualSample { get; set; }
    public string? ConfigJson { get; set; }
    public string? StageProgress { get; set; }
    public string? ResultsSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// wa_benchmark_results row.
/// </summary>
public sealed class BenchmarkResult
{
    public long Id { get; set; }
    public int BenchmarkId { get; set; }
    public int TenantId { get; set; }
    public string ConversationId { get; set; } = "";
    public int MessageCount { get; set; }
    public string? ThreadTextMasked { get; set; }
    public string? KeywordLabel { get; set; }
    public string? ClaudeHaikuLabel { get; set; }
    public float? ClaudeHaikuConfidence { get; set; }
    public string? ClaudeHaikuEvidence { get; set; }
    public string? ClaudeSonnetLabel { get; set; }
    public float? ClaudeSonnetConfidence { get; set; }
    public string? ClaudeSonnetEvidence { get; set; }
    public string? GeminiFlashLabel { get; set; }
    public float? GeminiFlashConfidence { get; set; }
    public string? GeminiFlashEvidence { get; set; }
    public string? GeminiProLabel { get; set; }
    public float? GeminiProConfidence { get; set; }
    public string? GeminiProEvidence { get; set; }
    public string? Gemini3FlashLabel { get; set; }
    public float? Gemini3FlashConfidence { get; set; }
    public string? Gemini3FlashEvidence { get; set; }
    public string? TieredLabel { get; set; }
    public float? TieredConfidence { get; set; }
    public string? TieredEvidence { get; set; }
    public string? GroundTruthLabel { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Queued benchmark job for background processing.
/// </summary>
public sealed class BenchmarkProcessJob
{
    public int BenchmarkId { get; set; }
    public int TenantId { get; set; }
    public string DatabaseName { get; set; } = "";
    public int? InstanceId { get; set; }
    public int SampleSize { get; set; }
    public int MinMessages { get; set; } = 6;
    public int MaxMessages { get; set; } = 200;
    public string[] Models { get; set; } = Array.Empty<string>();
}

/// <summary>
/// API request DTO for starting a benchmark.
/// </summary>
public sealed class BenchmarkStartRequest
{
    public string Database { get; set; } = "";
    public int InstanceId { get; set; }
    public int SampleSize { get; set; } = 200;
    public int? MinMessages { get; set; }
    public int? MaxMessages { get; set; }
    public string[]? Models { get; set; }
}

/// <summary>
/// Parsed LLM classification output.
/// </summary>
public sealed class LlmClassification
{
    public string Label { get; set; } = "";
    public float Confidence { get; set; }
    public string Evidence { get; set; } = "";

    private static readonly string[] ValidLabels =
    {
        "sale", "appointment_booked", "offered", "no_sale",
        "no_response", "abandoned", "return_or_complaint"
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Parse LLM response text into classification. Returns null on failure.
    /// Handles markdown code blocks and whitespace.
    /// </summary>
    public static LlmClassification? Parse(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;

        var text = responseText.Trim();

        // Strip markdown code block
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();
        }

        // Find JSON object bounds
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        var json = text[start..(end + 1)];

        try
        {
            var parsed = JsonSerializer.Deserialize<LlmClassification>(json, JsonOpts);
            if (parsed == null || string.IsNullOrEmpty(parsed.Label)) return null;

            // Normalize label
            parsed.Label = parsed.Label.Trim().ToLowerInvariant();

            // Map common aliases
            parsed.Label = parsed.Label switch
            {
                "return" => "return_or_complaint",
                "complaint" => "return_or_complaint",
                "booked" => "appointment_booked",
                "appointment" => "appointment_booked",
                _ => parsed.Label
            };

            // Validate label
            if (!ValidLabels.Contains(parsed.Label)) return null;

            // Clamp confidence
            parsed.Confidence = Math.Clamp(parsed.Confidence, 0f, 1f);

            return parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Sampled thread from MSSQL for benchmark.
/// </summary>
public sealed class SampledThread
{
    public string ConversationId { get; set; } = "";
    public List<ThreadMessage> Messages { get; set; } = new();
    public string MaskedText { get; set; } = "";
    public int MessageCount => Messages.Count;
}

/// <summary>
/// Single message within a sampled thread.
/// </summary>
public sealed class ThreadMessage
{
    public string Text { get; set; } = "";
    public string SenderType { get; set; } = ""; // "CUSTOMER" or "ME"
    public string AgentName { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Benchmark metrics result for a single model.
/// </summary>
public sealed class ModelMetrics
{
    public string ModelName { get; set; } = "";
    public double Accuracy { get; set; }
    public double MacroF1 { get; set; }
    public int Total { get; set; }
    public int Classified { get; set; }
    public Dictionary<string, LabelMetrics> PerLabel { get; set; } = new();
    public Dictionary<string, int> LabelDistribution { get; set; } = new();
}

/// <summary>
/// Per-label precision/recall/F1.
/// </summary>
public sealed class LabelMetrics
{
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
    public int Support { get; set; }
}

/// <summary>
/// Ground truth update request.
/// </summary>
public sealed class GroundTruthRequest
{
    public List<GroundTruthEntry> Labels { get; set; } = new();
}

public sealed class GroundTruthEntry
{
    public string ConversationId { get; set; } = "";
    public string Label { get; set; } = "";
}
