namespace Chatinbox.Shared.Contracts.TenantFieldMapping;

/// <summary>
/// FEAT-DMP default DI binding. Returns null for every placeholder so the caller falls
/// back to raw INMA key allowlist behaviour. FEAT-TFM replaces this binding when it ships.
/// </summary>
public sealed class NullTenantFieldMappingResolver : ITenantFieldMappingResolver
{
    public Task<string?> ResolveToInmaKeyAsync(int tenantId, string placeholder, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    /// <summary>No-op: NullResolver has no cache to invalidate.</summary>
    public void Invalidate(int tenantId) { /* intentional no-op */ }
}
