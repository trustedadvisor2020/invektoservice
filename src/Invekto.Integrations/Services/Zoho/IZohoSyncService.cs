// Adim 3 Paket 1: sync service abstraction - consumed by ZohoSyncEndpoints.
// P2 adds retry BackgroundService on top of the same service surface.
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Integrations.Services.Zoho;

public interface IZohoSyncService
{
    /// <summary>
    /// Runs a single source -> Zoho sync attempt end-to-end:
    /// 1) Open zoho_sync_log row (new or existing failed/pending), 2) resolve stage mapping,
    /// 3) resolve Zoho Lead id (create if missing + fields supplied), 4) execute blueprint transition,
    /// 5) mark log success/failed. Always returns a ZohoSyncResponse; exceptions caught internally.
    /// </summary>
    Task<ZohoSyncResponse> SyncAsync(ZohoSyncRequest request, CancellationToken ct = default);
}
