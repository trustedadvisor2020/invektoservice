namespace Invekto.Shared.Contracts.ClinicMetadata;

/// <summary>
/// FEAT-CLINIC-METADATA: tenant-scoped clinic metadata reader. Single resolver shared
/// across Backend (PUT invalidate, GET render) and Automation (substitution via
/// <c>ClinicTemplateApplier</c>).
///
/// Cache: 5dk TTL per tenant + single-flight (concurrent misses share one DB fetch).
/// Backend PUT calls <see cref="Invalidate"/> after a successful upsert so the next
/// resolver call reads fresh state. Multi-instance: invalidation runs on the receiving
/// Backend instance only; peer Automation instance picks up new state on its own 5dk
/// TTL expiry (eventual consistency MVP — same trade-off as FEAT-MCC
/// <see cref="Campaigns.DbTenantCampaignResolver"/> + FEAT-TFM
/// <see cref="TenantFieldMapping.DbTenantFieldMappingResolver"/>).
///
/// Failure semantics: any DB / JSON failure returns <see cref="ClinicMetadata.Empty"/>
/// + WARN log under INV-BE-125. Resolver fail must NOT crash the outbound path —
/// the empty metadata makes <c>{{clinic.*}}</c> + <c>{{team.*}}</c> substitution
/// graceful (empty-string per token), preserving outbound flow for clinic-agnostic
/// messages. The tenant operator sees an empty Dashboard editor which signals the issue.
/// </summary>
public interface ITenantClinicMetadataResolver
{
    /// <summary>Read the full clinic metadata for a tenant. Empty composite when row
    /// missing or columns unset — never null.</summary>
    Task<ClinicMetadata> GetAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Drop any cached metadata for the given tenant. Backend PUT calls this
    /// after a successful upsert so the next resolver call reads fresh state.</summary>
    void Invalidate(int tenantId);
}
