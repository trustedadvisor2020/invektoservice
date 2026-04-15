using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 3-B1: Dashboard UI -> Backend -> Invekto.Integrations proxy transport.
/// Tenant JWT is forwarded verbatim (Authorization: Bearer ...); Integrations /api/v1/ endpoints
/// re-validate the same JWT via their JwtAuth middleware and derive tenant_id there.
/// Upstream response (status + raw JSON body) is returned untransformed so upstream error envelopes
/// (INV-INT-*) reach the UI intact — no silent re-wrapping.
/// </summary>
public interface IZohoProxyClient
{
    Task<ZohoProxyResult> ForwardAsync(
        HttpMethod method,
        string pathAndQuery,
        string? bearerToken,
        CancellationToken ct = default);
}

/// <summary>Upstream response projection: status code + raw JSON body (or empty).</summary>
public sealed record ZohoProxyResult(int StatusCode, string Body, string ContentType);
