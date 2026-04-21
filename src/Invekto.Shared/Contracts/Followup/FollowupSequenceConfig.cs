using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Followup;

/// <summary>
/// Tenant-scoped follow-up sequence configuration. Maps 1:1 to a row of
/// <c>event_followup_sequences</c>. The <see cref="Stages"/> array is persisted as the
/// table's <c>stages</c> JSONB column.
///
/// Wire contract:
///   GET  /api/v1/followup/sequences          → returns array of these for the tenant.
///   PUT  /api/v1/followup/sequences          → upserts by (tenant_id from JWT, slug).
///
/// Validation (FollowupSequenceValidator):
///   - Slug non-empty, matches ^[a-z0-9][a-z0-9_-]{0,63}$ — DB unique per tenant.
///   - Stages count between 1 and 5 (cap, INV-MK-053).
///   - Cumulative DelayDays across stages &lt;= 30 (days OR minutes per test mode).
///   - AbSplitPercent in [0,100].
/// </summary>
public sealed class FollowupSequenceConfig
{
    /// <summary>
    /// Server-assigned identity from <c>event_followup_sequences.id</c>. NULL on create
    /// requests; populated on read responses.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Tenant-scoped sequence name (e.g. <c>post-roadshow</c>). Unique per tenant via
    /// DB <c>uq(tenant_id, slug)</c>.
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Ordered stage list. Index 0 fires first (offset from EnqueueAsync time);
    /// subsequent stages offset from the previous stage's scheduled time.
    /// </summary>
    [JsonPropertyName("stages")]
    public List<FollowupStageConfig> Stages { get; set; } = new();

    /// <summary>
    /// Percentage of leads (0-100) routed to the drip group. Remainder become the
    /// control group (zero <c>event_followup_runs</c> rows). Default 50.
    /// </summary>
    [JsonPropertyName("ab_split_percent")]
    public int AbSplitPercent { get; set; } = 50;

    /// <summary>
    /// Whether the sequence is live. FALSE rejects EnqueueAsync with INV-MK-054.
    /// Operators set TRUE explicitly via the Dashboard editor.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Server-managed audit timestamps (read-only on the wire).</summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>Server-managed audit timestamps (read-only on the wire).</summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
