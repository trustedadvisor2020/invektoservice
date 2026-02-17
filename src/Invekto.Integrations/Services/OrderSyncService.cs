using Invekto.Integrations.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Integrations.Services;

/// <summary>
/// Background service that periodically syncs orders from marketplace providers.
/// Polls active integration accounts and upserts orders into orders_cache.
/// </summary>
public sealed class OrderSyncService : IHostedService, IDisposable
{
    private readonly IntegrationsRepository _repo;
    private readonly Dictionary<string, IMarketplaceProvider> _providers;
    private readonly JsonLinesLogger _logger;
    private readonly int _syncIntervalMs;
    private Timer? _timer;
    private int _isRunning;

    public OrderSyncService(
        IntegrationsRepository repo,
        IEnumerable<IMarketplaceProvider> providers,
        JsonLinesLogger logger,
        int syncIntervalMs = 300_000) // default: 5 minutes
    {
        _repo = repo;
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _syncIntervalMs = syncIntervalMs;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo($"OrderSyncService starting (interval={_syncIntervalMs}ms, providers={string.Join(",", _providers.Keys)})");
        _timer = new Timer(ExecuteSync, null, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(_syncIntervalMs));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("OrderSyncService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void ExecuteSync(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            _logger.SystemWarn("OrderSyncService: previous sync still running, skipping");
            return;
        }

        try
        {
            var accounts = await _repo.GetActiveAccountsForSyncAsync();
            if (accounts.Count == 0)
                return;

            var synced = 0;
            var failed = 0;

            foreach (var (tenantId, provider, apiKeyEnc, apiSecretEnc, sellerId) in accounts)
            {
                if (!_providers.TryGetValue(provider, out var marketplaceProvider))
                {
                    // Not a marketplace provider (e.g., cargo), skip
                    if (provider.Contains("kargo", StringComparison.OrdinalIgnoreCase))
                        continue;

                    _logger.SystemWarn($"OrderSyncService: unknown provider '{provider}' for tenant {tenantId}");
                    continue;
                }

                try
                {
                    var sinceDate = DateTime.UtcNow.AddDays(-7); // sync last 7 days
                    var orders = await marketplaceProvider.FetchOrdersAsync(
                        apiKeyEnc ?? "", apiSecretEnc, sellerId, sinceDate, CancellationToken.None);

                    foreach (var order in orders)
                    {
                        await _repo.UpsertOrderAsync(
                            tenantId, provider, order.ExternalOrderId,
                            order.CustomerPhone, order.CustomerName,
                            order.TrackingCode, order.CargoProvider,
                            order.OrderStatus, order.TotalAmount, order.Currency,
                            order.OrderDataJson, CancellationToken.None);
                    }

                    await _repo.UpdateLastSyncAsync(tenantId, provider);
                    synced++;
                }
                catch (HttpRequestException ex)
                {
                    _logger.SystemWarn($"OrderSyncService: HTTP error syncing {provider} for tenant {tenantId}: {ex.Message}");
                    await _repo.UpdateAccountStatusAsync(tenantId, provider, "error", ex.Message);
                    failed++;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.SystemWarn($"OrderSyncService: timeout syncing {provider} for tenant {tenantId}: {ex.Message}");
                    await _repo.UpdateAccountStatusAsync(tenantId, provider, "error", "Sync timeout");
                    failed++;
                }
                catch (NpgsqlException ex) when (ex is Npgsql.NpgsqlException)
                {
                    _logger.SystemWarn($"OrderSyncService: DB error syncing {provider} for tenant {tenantId}: {ex.Message}");
                    failed++;
                }
            }

            if (synced > 0 || failed > 0)
                _logger.SystemInfo($"OrderSyncService: sync complete (synced={synced}, failed={failed}, total={accounts.Count})");
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"OrderSyncService: DB error in sync loop: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("OrderSyncService: sync cancelled");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
