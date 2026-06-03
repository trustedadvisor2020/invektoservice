namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 1A gating + bounds for contact lists. Bound from appsettings
/// "ContactList" section. Production-safe default: DISABLED.
///
/// Gating is intentionally SEPARATE from BulkSendOptions (a tenant may manage
/// lists without bulk-send enabled, or vice versa), but the same pilot-allowlist
/// shape is reused so ops can keep one mental model.
/// </summary>
public sealed class ContactListOptions
{
    public const string SectionName = "ContactList";

    /// <summary>Master kill-switch. Default false = endpoints return INV-OB-046.</summary>
    public bool Enabled { get; set; }

    /// <summary>Pilot tenant allowlist. Empty = no tenant may use contact lists.</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>
    /// Hard cap on TOTAL records a single list may hold. An import that would push the
    /// list over this is rejected (INV-OB-048) — NO auto-partition (Codex plan review).
    /// </summary>
    public int MaxRecordsPerList { get; set; } = 50_000;

    /// <summary>Absolute ceiling on rows accepted in a single import request body.</summary>
    public int MaxImportRows { get; set; } = 50_000;

    /// <summary>Max raw phones accepted in a single records/exists probe.</summary>
    public int MaxExistsProbe { get; set; } = 50_000;

    /// <summary>Cap on invalid-row samples echoed back to the operator.</summary>
    public int InvalidSampleSize { get; set; } = 20;

    public bool IsTenantAllowed(int tenantId) => Enabled && AllowedTenantIds.Contains(tenantId);
}
