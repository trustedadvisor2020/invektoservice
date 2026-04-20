namespace Invekto.Shared.Contracts.TenantFieldMapping;

/// <summary>
/// FEAT-DMP ↔ FEAT-TFM forward-compat hook (interview Q2 Hybrid).
/// When tenant maps semantic names to INMA keys (e.g. <c>roadshow_city → cf1</c>),
/// the picker/validator calls this to resolve semantic placeholders to INMA keys.
///
/// FEAT-DMP ships with <see cref="NullTenantFieldMappingResolver"/> (no mappings).
/// FEAT-TFM will replace the DI binding with a tenant-aware implementation.
/// </summary>
public interface ITenantFieldMappingResolver
{
    /// <summary>
    /// Resolve a placeholder name (raw semantic token like <c>roadshow_city</c> or raw INMA
    /// key like <c>cf1</c>) to its canonical INMA key. Returns null when no mapping exists
    /// — caller then treats the token as a raw INMA key and runs allowlist validation.
    /// </summary>
    /// <param name="tenantId">Tenant scope (mappings are per-tenant).</param>
    /// <param name="placeholder">Raw token from <c>{{placeholder}}</c>, already trimmed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> ResolveToInmaKeyAsync(int tenantId, string placeholder, CancellationToken ct = default);
}
