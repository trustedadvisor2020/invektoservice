namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 0 gating config (Codex: feature-flagged, allowlisted, hard-capped).
/// Bound from appsettings "BulkSend" section. Production-safe defaults: DISABLED.
/// </summary>
public sealed class BulkSendOptions
{
    public const string SectionName = "BulkSend";

    /// <summary>Master kill-switch. Default false = endpoints return INV-OB-039.</summary>
    public bool Enabled { get; set; }

    /// <summary>Pilot tenant allowlist. Empty = no tenant may use bulk send.</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>Kademeli hard cap on deduped valid recipients per campaign (Codex §3).</summary>
    public int MaxRecipientsPerCampaign { get; set; } = 50;

    /// <summary>
    /// Absolute ceiling on RAW input rows accepted before parse/dedup, so an oversized
    /// payload is rejected early (not parsed fully into memory). Codex CQ6.
    /// </summary>
    public int MaxInputRows { get; set; } = 5000;

    /// <summary>Absolute ceiling on raw csv_text length (chars) to bound the initial split.</summary>
    public int MaxInputChars { get; set; } = 1_000_000;

    /// <summary>Chunk size fed to the existing /broadcast/send engine (its own max is 1000).</summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>Sample size returned in the preview for operator eyeballing.</summary>
    public int PreviewSampleSize { get; set; } = 10;

    /// <summary>
    /// A job stuck in 'confirming' with no child broadcasts past this many minutes is treated as a
    /// failed-and-unrecovered confirm and self-healed to 'failed' on the next status read.
    /// </summary>
    public int ConfirmingStaleMinutes { get; set; } = 10;

    public bool IsTenantAllowed(int tenantId) => Enabled && AllowedTenantIds.Contains(tenantId);
}
