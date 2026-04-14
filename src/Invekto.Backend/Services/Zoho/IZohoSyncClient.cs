using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 2: Backend -> Invekto.Integrations typed HTTP client for POST /api/internal/zoho/sync.
/// Caller-supplied TenantId + shared-secret header (see ZohoSyncEndpoints.InternalTokenHeader).
/// Single attempt; retry is the Integrations ZohoRetryWorker's responsibility via zoho_sync_log.
/// </summary>
public interface IZohoSyncClient
{
    Task<ZohoSyncResponse?> SyncAsync(ZohoSyncRequest request, CancellationToken ct = default);
}
