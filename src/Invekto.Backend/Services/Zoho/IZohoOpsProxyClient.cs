using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 3-C: Super-admin ops -> Integrations /api/internal/ops/zoho/* proxy transport.
/// Differs from IZohoProxyClient (P3-B1, JWT forward): here the caller is the super-admin
/// Basic-Auth/JWT-admin session, NOT a tenant JWT. Trust boundary is the InternalServices:SharedSecret
/// header (X-Internal-Service-Token). Upstream response bytes returned verbatim.
/// </summary>
public interface IZohoOpsProxyClient
{
    Task<ZohoProxyResult> ForwardAsync(
        HttpMethod method,
        string pathAndQuery,
        string? jsonBody,
        CancellationToken ct = default);
}
