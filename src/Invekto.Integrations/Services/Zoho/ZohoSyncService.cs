// Adim 3 Paket 1: Source -> Zoho sync orchestration (mapping -> blueprint transition -> log).
// Always returns ZohoSyncResponse; exceptions are caught, mapped to INV-INT-xxx codes, and persisted on zoho_sync_log.
// Terminal failure policy: attempt_count >= 3 -> completed_at set (retry worker stops trying).
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Contracts.Zoho;
using Microsoft.Extensions.Logging;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoSyncService : IZohoSyncService
{
    private const int MaxAttempts = 3;

    private readonly ZohoSyncLogRepository _logRepo;
    private readonly IZohoStageMappingService _mappingService;
    private readonly IZohoBlueprintClient _blueprintClient;
    private readonly IZohoLeadClient _leadClient;
    private readonly ILogger<ZohoSyncService> _logger;

    public ZohoSyncService(
        ZohoSyncLogRepository logRepo,
        IZohoStageMappingService mappingService,
        IZohoBlueprintClient blueprintClient,
        IZohoLeadClient leadClient,
        ILogger<ZohoSyncService> logger)
    {
        _logRepo         = logRepo;
        _mappingService  = mappingService;
        _blueprintClient = blueprintClient;
        _leadClient      = leadClient;
        _logger          = logger;
    }

    public async Task<ZohoSyncResponse> SyncAsync(ZohoSyncRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.TenantId <= 0) throw new ArgumentException("INV-GEN-003: TenantId is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ZohoEvent)) throw new ArgumentException("INV-GEN-003: ZohoEvent is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceLeadId)) throw new ArgumentException("INV-GEN-003: SourceLeadId is required", nameof(request));

        var logId = await _logRepo.BeginAttemptAsync(
            request.TenantId, request.ZohoEvent, request.SourceLeadId, request.ZohoLeadId, ct).ConfigureAwait(false);

        var currentRow = await _logRepo.GetAsync(request.TenantId, logId, ct).ConfigureAwait(false);
        var attemptCount = currentRow?.AttemptCount ?? 1;

        try
        {
            var transitionId = await _mappingService
                .ResolveTransitionIdAsync(request.TenantId, request.ZohoEvent, ct)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(transitionId))
            {
                return await FailAsync(
                    logId, attemptCount,
                    ZohoErrorCodes.StageMappingNotConfigured,
                    $"No Zoho stage mapping configured for tenant {request.TenantId} event '{request.ZohoEvent}'. Configure via Dashboard -> Entegrasyonlar -> Zoho CRM.",
                    terminal: true, // mapping won't self-heal via retry; user action required
                    ct).ConfigureAwait(false);
            }

            var zohoLeadId = request.ZohoLeadId;
            if (string.IsNullOrEmpty(zohoLeadId))
            {
                if (request.LeadFields is null || string.IsNullOrWhiteSpace(request.LeadFields.LastName))
                {
                    return await FailAsync(
                        logId, attemptCount,
                        ZohoErrorCodes.LeadNotFound,
                        "ZohoLeadId omitted and LeadFields.LastName missing; cannot resolve Zoho Lead.",
                        terminal: true,
                        ct).ConfigureAwait(false);
                }

                zohoLeadId = await _leadClient.CreateAsync(request.TenantId, request.LeadFields, ct).ConfigureAwait(false);
            }

            // Pre-check: confirm Blueprint is configured for this lead and the mapped transition exists.
            // This surfaces INV-INT-121 (blueprint missing) and INV-INT-122 (transition not found) before
            // we attempt the mutating PUT, instead of relying on Zoho returning 404 with ambiguous semantics.
            var transitions = await _blueprintClient
                .GetLeadTransitionsAsync(request.TenantId, zohoLeadId, ct)
                .ConfigureAwait(false);

            bool transitionExists = false;
            for (int i = 0; i < transitions.Count; i++)
            {
                if (string.Equals(transitions[i].TransitionId, transitionId, StringComparison.Ordinal))
                {
                    transitionExists = true;
                    break;
                }
            }

            if (!transitionExists)
            {
                return await FailAsync(
                    logId, attemptCount,
                    ZohoErrorCodes.BlueprintTransitionNotFound,
                    $"Zoho Blueprint transition '{transitionId}' is not currently available for lead {zohoLeadId} (tenant {request.TenantId}). Re-run stage mapping discovery in the Dashboard.",
                    terminal: true,
                    ct).ConfigureAwait(false);
            }

            await _blueprintClient
                .ExecuteTransitionAsync(request.TenantId, zohoLeadId, transitionId, ct)
                .ConfigureAwait(false);

            await _logRepo.MarkSuccessAsync(logId, zohoLeadId, transitionId, ct).ConfigureAwait(false);

            return new ZohoSyncResponse
            {
                Success          = true,
                SyncLogId        = logId,
                Status           = "success",
                ZohoLeadId       = zohoLeadId,
                ZohoTransitionId = transitionId,
                AttemptCount     = attemptCount
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            // Zoho client layer surfaces failures as InvalidOperationException with "INV-INT-xxx: ..." prefix.
            var (code, message) = ExtractCode(ex.Message);
            var terminal = IsTerminal(code) || attemptCount >= MaxAttempts;
            _logger.LogWarning(ex,
                "Zoho sync failed for tenant {TenantId} event {Event} lead {LeadId}: {Code}",
                request.TenantId, request.ZohoEvent, request.SourceLeadId, code);
            return await FailAsync(logId, attemptCount, code, message, terminal, ct).ConfigureAwait(false);
        }
        // Typed catches only: infrastructure errors (DB, transport, serialization) are recorded against the
        // attempt so the retry worker can evaluate them; anything else (programmer error, OOM, etc.) bubbles up.
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.LogError(ex, "Zoho sync DB failure for tenant {TenantId}", request.TenantId);
            return await FailAsync(logId, attemptCount, ZohoErrorCodes.SyncInfrastructureError,
                "Database error during Zoho sync: " + ex.Message,
                attemptCount >= MaxAttempts, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Zoho sync transport failure for tenant {TenantId}", request.TenantId);
            return await FailAsync(logId, attemptCount, ZohoErrorCodes.SyncInfrastructureError,
                "Transport error calling Zoho: " + ex.Message,
                attemptCount >= MaxAttempts, ct).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Zoho sync response parse failure for tenant {TenantId}", request.TenantId);
            return await FailAsync(logId, attemptCount, ZohoErrorCodes.SyncInfrastructureError,
                "Zoho response could not be parsed: " + ex.Message,
                attemptCount >= MaxAttempts, ct).ConfigureAwait(false);
        }
    }

    private async Task<ZohoSyncResponse> FailAsync(
        long logId, int attemptCount, string errorCode, string errorMessage, bool terminal, CancellationToken ct)
    {
        await _logRepo.MarkFailedAsync(logId, errorCode, errorMessage, terminal, ct).ConfigureAwait(false);
        return new ZohoSyncResponse
        {
            Success      = false,
            SyncLogId    = logId,
            Status       = "failed",
            ErrorCode    = errorCode,
            ErrorMessage = errorMessage,
            AttemptCount = attemptCount
        };
    }

    private static (string Code, string Message) ExtractCode(string fullMessage)
    {
        // Messages use format "INV-INT-xxx: detail..." throughout Zoho clients.
        var colon = fullMessage.IndexOf(':');
        if (colon > 0 && colon < 16 && fullMessage.StartsWith("INV-INT-", StringComparison.Ordinal))
            return (fullMessage.Substring(0, colon), fullMessage.Substring(colon + 1).TrimStart());
        return (ZohoErrorCodes.SyncInfrastructureError, fullMessage);
    }

    /// <summary>User-action errors should not be retried by the background worker.</summary>
    private static bool IsTerminal(string code) =>
        code is ZohoErrorCodes.StageMappingNotConfigured
             or ZohoErrorCodes.BlueprintNotConfigured
             or ZohoErrorCodes.BlueprintTransitionNotFound
             or ZohoErrorCodes.ConnectionNotFound
             or ZohoErrorCodes.Disconnected
             or ZohoErrorCodes.DecryptionFailed
             or ZohoErrorCodes.RegionNotConfigured;
}
