// Adim 2 Paket B: token provider abstraction (lazy refresh + cache).
using System.Threading;
using System.Threading.Tasks;

namespace Invekto.Integrations.Services.Zoho;

public interface IZohoTokenProvider
{
    /// <summary>
    /// Returns a valid Zoho access token for the given tenant. Issues a refresh_token grant if cache is cold/expired.
    /// Throws InvalidOperationException with INV-INT-* code prefix when the tenant has no Zoho connection
    /// (INV-INT-115), refresh fails (INV-INT-114), or decryption fails (INV-INT-117).
    /// </summary>
    Task<string> GetAccessTokenAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Removes the cached access token (forces next call to refresh). Used on 401 from Zoho API.</summary>
    void InvalidateCache(int tenantId);
}
