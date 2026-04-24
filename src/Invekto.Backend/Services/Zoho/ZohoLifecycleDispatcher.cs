using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 2: Fire-and-forget dispatcher for lifecycle events -> Zoho sync.
/// Contract: NEVER throws back to the caller. Captures IServiceScopeFactory so the outbound
/// HTTP call outlives the originating HTTP request (we do NOT bind to ctx.RequestAborted —
/// a client disconnect must not cancel the Zoho sync).
/// </summary>
public sealed class ZohoLifecycleDispatcher
{
    private static readonly TimeSpan BackgroundTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Paket B-META whitelist: events that <see cref="DispatchEvent"/> will forward
    /// to the Zoho sync pipeline. Prevents arbitrary strings (e.g. typo'd
    /// <c>"welcomee_sent"</c>) from reaching <see cref="ZohoSyncRequest"/> where
    /// they would silently disappear into an unknown-event log line at the
    /// Integrations service.
    ///
    /// Pipeline status flow still routes through <see cref="DispatchLeadStatusChange"/>
    /// + <see cref="LeadStatusEventMap"/>; this whitelist is for the direct-dispatch
    /// path only (Automation post-welcome hook currently; future lifecycle
    /// notifications to sit alongside as new handlers get added).
    /// </summary>
    private static readonly HashSet<string> AllowedDirectEvents =
        new(StringComparer.Ordinal)
        {
            "welcome_sent",
            "engaged",
            "qualified",
            "offer_sent",
            "deposit_paid",
            "closed_won",
            "closed_lost"
        };

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
        var zohoEvent = LeadStatusEventMap.Resolve(pipelineStatus);
        if (zohoEvent is null)
        {
            // Audit trail: pipeline_status outside Zoho scope (not a failure, but must be visible).
            _logger.SystemWarn(
                $"[{ErrorCodes.GeneralValidation}] Zoho sync skipped: pipeline_status '{pipelineStatus}' is not mapped to a zoho_event. tenant={tenantId} lead={leadId}");
            return;
        }

        _ = Task.Run(() => RunAsync(tenantId, leadId, zohoEvent));
    }

    /// <summary>
    /// Paket B-META: direct-dispatch entry used by lifecycle hooks that already
    /// know the target zoho_event (no pipeline_status translation needed).
    /// Triggered by <c>LifecycleInternalEndpoints /welcome-sent</c> when Automation
    /// reports a completed welcome flow.
    ///
    /// Unknown event (outside <see cref="AllowedDirectEvents"/>) → warn log + no-op
    /// so a caller bug with a typo'd event name is visible without polluting Zoho
    /// state. Task.Run fire-and-forget mirrors <see cref="DispatchLeadStatusChange"/>
    /// — callers never block on transport latency.
    /// </summary>
    public void DispatchEvent(int tenantId, int leadId, string zohoEvent)
    {
        if (string.IsNullOrWhiteSpace(zohoEvent) || !AllowedDirectEvents.Contains(zohoEvent))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.GeneralValidation}] Zoho DispatchEvent skipped: event '{zohoEvent}' not in allowlist. tenant={tenantId} lead={leadId}");
            return;
        }

        _ = Task.Run(() => RunAsync(tenantId, leadId, zohoEvent));
    }

    private async Task RunAsync(int tenantId, int leadId, string zohoEvent)
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
                ZohoEvent = zohoEvent,
                SourceLeadId = leadId.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                    $"[{response.ErrorCode ?? "INV-INT-127"}] Zoho sync failed: tenant={tenantId} event={zohoEvent} lead={leadId} attempt={response.AttemptCount} msg={response.ErrorMessage}");
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
                $"[INV-INT-127] Zoho lifecycle DB error: tenant={tenantId} event={zohoEvent} lead={leadId} err={ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle timeout: tenant={tenantId} event={zohoEvent} lead={leadId} err={ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle DI/scope error: tenant={tenantId} event={zohoEvent} lead={leadId} err={ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[INV-INT-127] Zoho lifecycle serialization error: tenant={tenantId} event={zohoEvent} lead={leadId} err={ex.Message}");
        }
    }
}
