namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Request body for POST /api/internal/zoho/sync (Invekto.Integrations, shared-secret auth).
/// Fired by Invekto.Backend lifecycle hooks (AttributionService, FlowEngine) in Adim 3 P2.
/// </summary>
public sealed class ZohoSyncRequest
{
    /// <summary>Invekto tenant_id (multi-tenant scope).</summary>
    public required int TenantId { get; init; }

    /// <summary>Gunes lifecycle event (welcome_sent / engaged / qualified / offer_sent / closed_won / deposit_paid / closed_lost).</summary>
    public required string GunesEvent { get; init; }

    /// <summary>Opaque Gunes-side lead identifier. Used as sync_log idempotency key with (tenant_id, gunes_event).</summary>
    public required string GunesLeadId { get; init; }

    /// <summary>Known Zoho CRM Lead id if already resolved. When null, LeadFields.LastName MUST be supplied and a new Zoho Lead is created.</summary>
    public string? ZohoLeadId { get; init; }

    /// <summary>Required when ZohoLeadId is null: Zoho CRM Lead creation fields (LastName mandatory).</summary>
    public ZohoLeadFields? LeadFields { get; init; }
}

/// <summary>
/// Minimal Zoho Lead fields used when Gunes side is the source of truth for the lead record.
/// </summary>
public sealed class ZohoLeadFields
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Company { get; init; }
}
