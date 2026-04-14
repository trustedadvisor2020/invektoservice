using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Backend.Data;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 2: Fire-and-forget dispatcher for Gunes lifecycle events -> Zoho sync.
/// Contract: NEVER throws back to the caller. Captures IServiceScopeFactory so the outbound
/// HTTP call outlives the originating HTTP request (we do NOT bind to ctx.RequestAborted —
/// a client disconnect must not cancel the Zoho sync).
/// </summary>
public sealed class ZohoLifecycleDispatcher
{
    private static readonly TimeSpan BackgroundTimeout = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JsonLinesLogger _logger;

    public ZohoLifecycleDispatcher(IServiceScopeFactory scopeFactory, JsonLinesLogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fires Zoho sync on the thread pool. Returns immediately. Unknown pipeline_status -> no-op.
    /// </summary>
    public void DispatchLeadStatusChange(int tenantId, int leadId, string pipelineStatus)
    {
        var gunesEvent = LeadStatusEventMap.Resolve(pipelineStatus);
        if (gunesEvent is null)
        {
            // Audit trail: pipeline_status outside Zoho scope (not a failure, but must be visible).
            _logger.SystemWarn(
                $"[INV-GEN-003] Zoho sync skipped: pipeline_status '{pipelineStatus}' is not mapped to a gunes_event. tenant={tenantId} lead={leadId}");
            return;
        }

        _ = Task.Run(() => RunAsync(tenantId, leadId, gunesEvent));
    }

    private async Task RunAsync(int tenantId, int leadId, string gunesEvent)
    {
        using var cts = new CancellationTokenSource(BackgroundTimeout);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var leadRepo = scope.ServiceProvider.GetRequiredService<LeadRepository>();
            var lead = await leadRepo.GetLeadAsync(tenantId, leadId, cts.Token).ConfigureAwait(false);

            var request = new ZohoSyncRequest
            {
                TenantId = tenantId,
                GunesEvent = gunesEvent,
                GunesLeadId = leadId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ZohoLeadId = null,
                LeadFields = lead is null ? null : new ZohoLeadFields
                {
                    FirstName = null,
                    LastName  = string.IsNullOrWhiteSpace(lead.Name) ? $"Lead#{leadId}" : lead.Name,
                    Email     = lead.Email,
                    Phone     = lead.Phone,
                    Company   = null,
                },
            };

            var client = scope.ServiceProvider.GetRequiredService<IZohoSyncClient>();
            var response = await client.SyncAsync(request, cts.Token).ConfigureAwait(false);

            if (response is null)
            {
                // Transport failure already logged by ZohoSyncClient as INV-INT-127.
                return;
            }

            if (!response.Success)
            {
                _logger.SystemWarn(
                    $"[{response.ErrorCode ?? "INV-INT-127"}] Zoho sync failed: tenant={tenantId} event={gunesEvent} lead={leadId} attempt={response.AttemptCount} msg={response.ErrorMessage}");
            }
        }
        // Typed catches ONLY (project policy). Each enumerated type matches a real failure mode
        // reachable from: LeadRepository.GetLeadAsync (NpgsqlException), dispatcher timeout
        // (OperationCanceledException), scope/DI resolution (InvalidOperationException), and
        // defensive JsonException in case upstream Shared DTO serialization throws before the
        // inner HttpClient catches cover it.
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle DB error: tenant={tenantId} event={gunesEvent} lead={leadId} err={ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle timeout: tenant={tenantId} event={gunesEvent} lead={leadId} err={ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle DI/scope error: tenant={tenantId} event={gunesEvent} lead={leadId} err={ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle serialization error: tenant={tenantId} event={gunesEvent} lead={leadId} err={ex.Message}");
        }
    }
}
