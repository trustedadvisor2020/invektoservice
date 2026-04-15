// Adim 3 Paket 1: Zoho CRM Lead API abstraction.
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Integrations.Services.Zoho;

/// <summary>
/// Thin wrapper over Zoho CRM /crm/v6/Leads endpoints. Scope in Adim 3 P1 is read-only
/// (Get by id) + minimal create; full CRUD arrives with inbound Zoho sync in Adim 4.
/// </summary>
public interface IZohoLeadClient
{
    /// <summary>
    /// Returns the Zoho Lead with the given id or throws InvalidOperationException(INV-INT-123) if missing.
    /// </summary>
    Task<ZohoLeadRecord> GetAsync(int tenantId, string zohoLeadId, CancellationToken ct = default);

    /// <summary>
    /// Creates a Lead in Zoho using the supplied fields. Returns the new Zoho Lead id.
    /// </summary>
    Task<string> CreateAsync(int tenantId, ZohoLeadFields fields, CancellationToken ct = default);
}

/// <summary>Minimal Zoho Lead projection (only fields the sync pipeline needs).</summary>
public sealed record ZohoLeadRecord(string Id, string? FullName, string? Email, string? Phone);
