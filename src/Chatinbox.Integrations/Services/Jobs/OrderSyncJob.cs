using Hangfire;
using Chatinbox.Integrations.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Integrations.Services.Jobs;

/// <summary>
/// G7 Faz 5: Hangfire recurring job replacing <c>OrderSyncService</c>.
/// Polls active integration accounts and upserts orders into orders_cache (7-day window).
///
/// Queue: <c>integrations</c>. Recurring id: <c>integrations:order-sync</c> (cron */5 min).
/// Per-account typed catches preserve row-level isolation (one marketplace failure does
/// not stop others). Top-level failures bubble to Hangfire AutomaticRetry + INV-JOB-005.
/// </summary>
[Queue("integrations")]
[DisableConcurrentExecution(timeoutInSeconds: 600)]
public sealed class OrderSyncJob
{
    private readonly IntegrationsRepository _repo;
    private readonly Dictionary<string, IMarketplaceProvider> _providers;
    private readonly JsonLinesLogger _logger;

    public OrderSyncJob(
        IntegrationsRepository repo,
        IEnumerable<IMarketplaceProvider> providers,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await ExecuteSyncAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("OrderSyncJob: cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire AutomaticRetry + INV-JOB-005.
    }

    private async Task ExecuteSyncAsync(CancellationToken ct)
    {
        var accounts = await _repo.GetActiveAccountsForSyncAsync();
        if (accounts.Count == 0)
            return;

        var synced = 0;
        var failed = 0;

        foreach (var (tenantId, provider, apiKeyEnc, apiSecretEnc, sellerId) in accounts)
        {
            if (ct.IsCancellationRequested) break;

            if (!_providers.TryGetValue(provider, out var marketplaceProvider))
            {
                if (provider.Contains("kargo", StringComparison.OrdinalIgnoreCase))
                    continue;

                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsProviderSyncFailed}] OrderSyncJob: unknown provider '{provider}' for tenant {tenantId}");
                continue;
            }

            try
            {
                var sinceDate = DateTime.UtcNow.AddDays(-7);
                var orders = await marketplaceProvider.FetchOrdersAsync(
                    apiKeyEnc ?? "", apiSecretEnc, sellerId, sinceDate, ct);

                foreach (var order in orders)
                {
                    await _repo.UpsertOrderAsync(
                        tenantId, provider, order.ExternalOrderId,
                        order.CustomerPhone, order.CustomerName,
                        order.TrackingCode, order.CargoProvider,
                        order.OrderStatus, order.TotalAmount, order.Currency,
                        order.OrderDataJson, ct);
                }

                await _repo.UpdateLastSyncAsync(tenantId, provider);
                synced++;
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsProviderSyncFailed}] OrderSyncJob: HTTP error syncing {provider} for tenant {tenantId}: {ex.Message}");
                await _repo.UpdateAccountStatusAsync(tenantId, provider, "error", ex.Message);
                failed++;
            }
            catch (TaskCanceledException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsProviderSyncFailed}] OrderSyncJob: timeout syncing {provider} for tenant {tenantId}: {ex.Message}");
                await _repo.UpdateAccountStatusAsync(tenantId, provider, "error", "Sync timeout");
                failed++;
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsProviderSyncFailed}] OrderSyncJob: DB error syncing {provider} for tenant {tenantId}: {ex.Message}");
                failed++;
            }
        }

        if (synced > 0 || failed > 0)
            _logger.SystemInfo($"OrderSyncJob: sync complete (synced={synced}, failed={failed}, total={accounts.Count})");
    }
}
