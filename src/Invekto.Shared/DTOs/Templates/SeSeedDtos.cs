using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Templates;

/// <summary>
/// Result from SE scenario seed operation.
/// </summary>
public sealed class SeSeedResult
{
    [JsonPropertyName("total_inserted")]
    public int TotalInserted { get; set; }

    [JsonPropertyName("scenarios")]
    public int Scenarios { get; set; }

    [JsonPropertyName("faqs")]
    public int Faqs { get; set; }

    [JsonPropertyName("intents")]
    public int Intents { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    [JsonPropertyName("by_sector")]
    public Dictionary<string, int> BySector { get; set; } = new();

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }
}
