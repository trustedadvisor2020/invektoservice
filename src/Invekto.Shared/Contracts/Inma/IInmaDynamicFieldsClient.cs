using Invekto.Shared.Contracts.Inma.Dtos;

namespace Invekto.Shared.Contracts.Inma;

/// <summary>
/// FEAT-DMP: client for INMA <c>POST https://cxapi.wapcrm.net/api/dynamicfields</c>.
/// Returns the tenant-scoped available placeholder list (5 fixed + active cf1..cf10).
/// Tenant scope is derived from the <c>X-CIB-SecretKey</c> header.
/// </summary>
public interface IInmaDynamicFieldsClient
{
    /// <summary>
    /// Fetch the placeholder allowlist for a tenant. Callers distinguish two outcomes:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Empty-but-successful list \u2014 tenant has no active placeholders
    ///     (no CF1..CF10 configured in INMA). Valid state, caller should treat as
    ///     "no fields to offer in the picker".</description>
    ///   </item>
    ///   <item>
    ///     <description>Non-empty list \u2014 returned exactly as INMA sent it.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Every failure path \u2014 timeout, network error, malformed JSON, WapCRM envelope
    /// <c>Status:false</c> or missing \u2014 throws <see cref="InmaDynamicFieldsFetchException"/>
    /// so callers (<see cref="Invekto.Shared.Services.InmaDynamicFieldsCache"/> + Backend proxy)
    /// can map it to a 503 surface rather than silently returning "no fields". Programmer
    /// errors (null/whitespace <paramref name="secretKey"/>) throw <see cref="ArgumentException"/>.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<InmaDynamicField>> GetFieldsAsync(
        int tenantId,
        string secretKey,
        CancellationToken ct = default);
}
