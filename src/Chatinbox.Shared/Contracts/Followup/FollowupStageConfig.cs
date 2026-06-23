using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Followup;

/// <summary>
/// Single stage within an <see cref="FollowupSequenceConfig"/>. Stored as a JSON object
/// inside <c>event_followup_sequences.stages</c>; the array order IS the stage order.
///
/// Validation (FollowupSequenceValidator):
///   - <see cref="DelayDays"/> &gt; 0 and &lt;= 30 (cap; in test mode interpreted as minutes)
///   - <see cref="TemplateSlug"/> non-empty, matches ^[a-z0-9][a-z0-9_-]{0,63}$
///   - cumulative delay across all stages &lt;= 30 (days OR minutes per test mode)
/// </summary>
public sealed class FollowupStageConfig
{
    /// <summary>
    /// Days from the previous stage's send time (or from EnqueueAsync time for stage 0).
    /// When <c>tenant_settings.efs_test_mode = TRUE</c> this value is reinterpreted as
    /// MINUTES so a 3/7/14 day pilot can be smoke-tested in 24 minutes total.
    /// </summary>
    [JsonPropertyName("delay_days")]
    public int DelayDays { get; set; }

    /// <summary>
    /// Template lookup slug (resolved by Marketing's template repository or remote
    /// template registry — implementation hooks into the existing template_catalog
    /// pattern used by FEAT-WTP). Required.
    /// </summary>
    [JsonPropertyName("template_slug")]
    public string TemplateSlug { get; set; } = string.Empty;

    /// <summary>
    /// Optional rotation/group tag — when multiple template variants share a group, the
    /// orchestrator may rotate. NULL or empty = single template. Mirrors the FEAT-WTP
    /// <c>template_group</c> rotation contract.
    /// </summary>
    [JsonPropertyName("template_group")]
    public string? TemplateGroup { get; set; }
}
