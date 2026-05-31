using Invekto.Shared.Logging;

namespace Invekto.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F-VR-E (spec §5): confidence bands for KB retrieval routing. Thresholds are
/// config-driven (appsettings <c>Knowledge:ConfidenceHigh</c> / <c>Knowledge:ConfidenceMedium</c>)
/// so they can be tuned on prod without a rebuild — the same config-driven pattern as the
/// turn_detection params. Defaults follow the canonical spec: high ≥ 0.80, medium 0.55–0.79,
/// low &lt; 0.55.
///
/// Bands apply ONLY to SEMANTIC scores (pgvector cosine similarity, ~0–1). Keyword-fallback
/// scores (ts_rank / trigram) live on a different scale, so SearchKnowledgeBaseTool does NOT
/// threshold them — thresholding keyword hits would wrongly drop valid results and tank recall
/// exactly when semantic search is already unavailable.
/// </summary>
public sealed record KbConfidenceOptions(double HighThreshold, double MediumThreshold)
{
    public const double DefaultHigh = 0.80;
    public const double DefaultMedium = 0.55;

    /// <summary>
    /// Builds thresholds from configuration with spec defaults. Guards against misconfiguration
    /// (medium ≥ high, or out of [0,1]) that would route every answer into the wrong band — falls
    /// back to the canonical defaults with a WARN rather than silently misbehaving.
    /// </summary>
    public static KbConfidenceOptions FromConfig(IConfiguration config, JsonLinesLogger logger)
    {
        var high = config.GetValue("Knowledge:ConfidenceHigh", DefaultHigh);
        var medium = config.GetValue("Knowledge:ConfidenceMedium", DefaultMedium);

        if (!(high > medium) || high > 1.0 || medium < 0.0)
        {
            logger.SystemWarn($"[KbConfidenceOptions] invalid thresholds high={high} medium={medium}; " +
                $"falling back to defaults {DefaultHigh}/{DefaultMedium}");
            return new KbConfidenceOptions(DefaultHigh, DefaultMedium);
        }
        return new KbConfidenceOptions(high, medium);
    }

    /// <summary>Returns "high" | "medium" | "low" for a semantic top score.</summary>
    public string BandFor(double topScore) =>
        topScore >= HighThreshold ? "high"
        : topScore >= MediumThreshold ? "medium"
        : "low";
}
