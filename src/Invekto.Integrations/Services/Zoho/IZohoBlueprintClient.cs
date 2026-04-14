// Adim 3 Paket 1: Zoho Blueprint API abstraction.
using System.Threading;
using System.Threading.Tasks;

namespace Invekto.Integrations.Services.Zoho;

/// <summary>
/// Read Blueprint metadata (transitions) + execute transitions for the Leads module.
/// Blueprint-only policy: when a tenant has no Blueprint configured for Leads, callers
/// MUST surface INV-INT-121 to the user; silent fallback to field update is forbidden.
/// </summary>
public interface IZohoBlueprintClient
{
    /// <summary>
    /// Returns Blueprint transitions available for the given Lead record. Cached 10 min per (tenant, lead).
    /// Throws InvalidOperationException(INV-INT-121) when the tenant has no Leads Blueprint.
    /// </summary>
    Task<IReadOnlyList<ZohoBlueprintTransition>> GetLeadTransitionsAsync(
        int tenantId,
        string zohoLeadId,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a Blueprint transition on the given Lead. Throws InvalidOperationException
    /// with INV-INT-122 (transition not found), INV-INT-119 (rate limit), or INV-INT-125 (other infrastructure failure).
    /// On 401 the token cache is invalidated and the call is retried once.
    /// </summary>
    Task ExecuteTransitionAsync(
        int tenantId,
        string zohoLeadId,
        string transitionId,
        CancellationToken ct = default);
}

public sealed record ZohoBlueprintTransition(
    string TransitionId,
    string Name,
    string? NextState);
