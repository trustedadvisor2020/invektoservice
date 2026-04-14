using System.Collections.Generic;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 2: Invekto pipeline_status -> Gunes/Zoho gunes_event translation.
/// Backend-local (not Shared) — pipeline_status vocabulary is a Backend concern; Integrations
/// only consumes the AllowedEvents whitelist (welcome_sent/engaged/qualified/offer_sent/
/// closed_won/deposit_paid/closed_lost). Unknown pipeline_status -> null -> sync skipped.
/// </summary>
public static class LeadStatusEventMap
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["new"]          = "engaged",
            ["contacted"]    = "qualified",
            ["consultation"] = "offer_sent",
            ["appointment"]  = "offer_sent",
            ["patient"]      = "closed_won",
            ["lost"]         = "closed_lost",
        };

    /// <summary>Returns the mapped gunes_event or null when the pipeline_status is not in scope for Zoho sync.</summary>
    public static string? Resolve(string? pipelineStatus)
    {
        if (string.IsNullOrWhiteSpace(pipelineStatus)) return null;
        return Map.TryGetValue(pipelineStatus, out var evt) ? evt : null;
    }
}
