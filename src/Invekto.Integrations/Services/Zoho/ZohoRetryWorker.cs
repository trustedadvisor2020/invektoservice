using System;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Invekto.Integrations.Services.Zoho;

/// <summary>
/// Adim 3 Paket 2: Retry worker for zoho_sync_log.
/// Contract (per plan AC4):
///   - 5 minute tick (cheap; row-level lock via SELECT ... FOR UPDATE SKIP LOCKED keeps horizontal scale safe).
///   - Candidates: status='failed' AND updated_at &lt; NOW()-INTERVAL '10 minutes' AND attempt_count &lt; 2.
///   - Max 2 total attempts (inline call from Backend = attempt 1; worker = attempt 2).
///   - Exponential backoff and &gt;2 attempts are explicitly deferred to a later packet.
/// Worker calls IZohoSyncService.SyncAsync which internally calls BeginAttemptAsync (attempt++)
/// and records terminal success/failed — worker itself only orchestrates scheduling.
/// </summary>
public sealed class ZohoRetryWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private const int BatchLimit = 50;
    private const string RequestId = "zoho-retry-worker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JsonLinesLogger _logger;

    public ZohoRetryWorker(IServiceScopeFactory scopeFactory, JsonLinesLogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the worker does not race DB warmup on service start.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn($"[INV-INT-125] ZohoRetryWorker tick DB error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn($"[INV-INT-125] ZohoRetryWorker tick DI/scope error: {ex.Message}");
            }

            try { await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<ZohoSyncLogRepository>();
        var syncService = scope.ServiceProvider.GetRequiredService<IZohoSyncService>();

        // Per-tenant iteration preserves the tenant_id scoping rule on every DB query below.
        var tenants = await logRepo.ListTenantsWithRetryCandidatesAsync(ct).ConfigureAwait(false);
        if (tenants.Count == 0) return;

        var totalProcessed = 0;
        foreach (var tenantId in tenants)
        {
            if (ct.IsCancellationRequested) return;

            var candidates = await logRepo
                .ListRetryCandidatesForTenantAsync(tenantId, BatchLimit, ct)
                .ConfigureAwait(false);
            if (candidates.Count == 0) continue;

            totalProcessed += candidates.Count;

            foreach (var c in candidates)
            {
                if (ct.IsCancellationRequested) return;

                // Reconstruct the request. LeadFields is null on retry — the first attempt either
                // already created the Zoho Lead (zoho_lead_id populated) or the mapping / blueprint
                // preconditions failed for reasons unrelated to lead fields (INV-INT-120/121/122).
                var request = new ZohoSyncRequest
                {
                    TenantId    = c.TenantId,
                    ZohoEvent    = c.ZohoEvent,
                    SourceLeadId = c.SourceLeadId,
                    ZohoLeadId  = c.ZohoLeadId,
                    LeadFields  = null,
                };

                try
                {
                    var response = await syncService.SyncAsync(request, ct).ConfigureAwait(false);
                    if (response.Success)
                    {
                        _logger.StepInfo(
                            $"ZohoRetryWorker retry success: id={c.Id} tenant={c.TenantId} event={c.ZohoEvent}", RequestId);
                    }
                    else
                    {
                        _logger.SystemWarn(
                            $"[{response.ErrorCode ?? "INV-INT-125"}] ZohoRetryWorker retry failed: id={c.Id} tenant={c.TenantId} event={c.ZohoEvent} attempt={response.AttemptCount} msg={response.ErrorMessage}");
                    }
                }
                // Typed catches ONLY. ZohoSyncService catches its own Zoho/HTTP failures and returns
                // a failed ZohoSyncResponse; reaching catch here means input validation or DB-layer
                // throwing past the service boundary.
                catch (ArgumentException ex)
                {
                    _logger.SystemWarn(
                        $"[INV-GEN-003] ZohoRetryWorker validation error: id={c.Id} tenant={c.TenantId} event={c.ZohoEvent} err={ex.Message}");
                }
                catch (NpgsqlException ex)
                {
                    _logger.SystemWarn(
                        $"[INV-INT-125] ZohoRetryWorker DB error: id={c.Id} tenant={c.TenantId} event={c.ZohoEvent} err={ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemWarn(
                        $"[INV-INT-125] ZohoRetryWorker invariant error: id={c.Id} tenant={c.TenantId} event={c.ZohoEvent} err={ex.Message}");
                }
            }
        }

        if (totalProcessed > 0)
            _logger.StepInfo($"ZohoRetryWorker tick: {totalProcessed} candidates across {tenants.Count} tenants", RequestId);
    }
}
