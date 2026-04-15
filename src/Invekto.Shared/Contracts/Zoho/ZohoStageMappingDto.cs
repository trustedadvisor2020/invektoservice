namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Per-tenant mapping: lifecycle event -> Zoho Blueprint transition.
/// Source: zoho_stage_mappings table. Used by Dashboard UI (Adim 3 P3) and SyncOrchestrator.
/// </summary>
public sealed class ZohoStageMappingDto
{
    /// <summary>Lifecycle event identifier.</summary>
    public required string ZohoEvent { get; init; }

    /// <summary>Zoho Blueprint transition id (discovered via GET /crm/v6/settings/blueprint).</summary>
    public required string ZohoTransitionId { get; init; }

    /// <summary>Human-readable transition label (UI convenience; not authoritative).</summary>
    public string? ZohoTransitionName { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Upsert payload for Dashboard UI (PUT /api/v1/zoho/stage-mappings).
/// Entire tenant mapping set replaced atomically.
/// </summary>
public sealed class ZohoStageMappingUpsertRequest
{
    // TenantId is intentionally NOT exposed: the endpoint resolves tenant from the caller's JWT
    // (defense in depth — body.tenant_id would be overridable).
    public required IReadOnlyList<ZohoStageMappingEntry> Mappings { get; init; }
}

public sealed class ZohoStageMappingEntry
{
    public required string ZohoEvent { get; init; }
    public required string ZohoTransitionId { get; init; }
    public string? ZohoTransitionName { get; init; }
}

/// <summary>Envelope for GET/PUT /api/v1/zoho/stage-mappings (consistent API response shape).</summary>
public sealed class ZohoStageMappingListResponse
{
    public required IReadOnlyList<ZohoStageMappingDto> Mappings { get; init; }
}

/// <summary>Envelope for GET /api/v1/zoho/sync-log/failed-count (Dashboard badge).</summary>
public sealed class ZohoSyncFailedCountResponse
{
    public required int FailedCount { get; init; }
}
