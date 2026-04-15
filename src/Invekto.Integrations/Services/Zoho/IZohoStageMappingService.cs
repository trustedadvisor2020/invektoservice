// Adim 3 Paket 1: Zoho stage mapping service abstraction.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Integrations.Services.Zoho;

public interface IZohoStageMappingService
{
    Task<IReadOnlyList<ZohoStageMappingDto>> ListAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Returns the transition id mapped to the given lifecycle event or null if unmapped.</summary>
    Task<string?> ResolveTransitionIdAsync(int tenantId, string zohoEvent, CancellationToken ct = default);

    /// <summary>Atomic replace of all tenant mappings. tenantId comes from authenticated JWT, never the body.</summary>
    Task ReplaceAsync(int tenantId, ZohoStageMappingUpsertRequest request, CancellationToken ct = default);
}
