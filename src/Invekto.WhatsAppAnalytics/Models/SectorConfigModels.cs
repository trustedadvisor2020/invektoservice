namespace Invekto.WhatsAppAnalytics.Models;

/// <summary>
/// wa_sector_config row.
/// </summary>
public sealed class SectorConfig
{
    public int Id { get; set; }
    public string SectorKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? LabelOverrides { get; set; }
    public string? CustomPrompt { get; set; }
    public float? BenchmarkF1 { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
