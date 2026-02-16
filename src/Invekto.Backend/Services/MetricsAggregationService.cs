using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services;

/// <summary>
/// PKT-3 GR-2.5: Background service that periodically aggregates
/// auto_reply_log + chat_sessions into daily_metrics / daily_intent_metrics.
/// Runs every 5 minutes. UPSERT ensures idempotency (safe to re-run).
/// Aggregates today + yesterday (yesterday catches late-arriving data).
/// </summary>
public sealed class MetricsAggregationService : IHostedService, IDisposable
{
    private readonly AnalyticsRepository _repo;
    private readonly JsonLinesLogger _logger;
    private Timer? _timer;
    private int _isRunning; // 0=idle, 1=running (Interlocked for overlap prevention)

    /// <summary>
    /// Aggregation interval (5 minutes).
    /// </summary>
    private static readonly TimeSpan AggregationInterval = TimeSpan.FromMinutes(5);

    public MetricsAggregationService(AnalyticsRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("MetricsAggregationService starting (interval: 5min)");
        // Start after 30 seconds delay (let other services warm up), then every 5 minutes
        _timer = new Timer(OnTimerElapsed, null, TimeSpan.FromSeconds(30), AggregationInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("MetricsAggregationService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private async void OnTimerElapsed(object? state)
    {
        // Prevent overlapping runs
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            // Get all tenants with automation data
            List<int> tenantIds;
            try
            {
                tenantIds = await _repo.GetTenantIdsWithAutoReplyDataAsync();
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.SystemWarn($"MetricsAggregation: DB error fetching tenant list ({ErrorCodes.MetricsAggregationFailed}): {ex.Message}");
                return;
            }

            if (tenantIds.Count == 0)
            {
                return;
            }

            var aggregated = 0;
            foreach (var tenantId in tenantIds)
            {
                try
                {
                    // Aggregate yesterday (catch late-arriving data)
                    await _repo.UpsertDailyMetricsAsync(tenantId, yesterday);
                    await _repo.UpsertDailyIntentMetricsAsync(tenantId, yesterday);

                    // Aggregate today
                    await _repo.UpsertDailyMetricsAsync(tenantId, today);
                    await _repo.UpsertDailyIntentMetricsAsync(tenantId, today);

                    aggregated++;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    _logger.SystemWarn($"MetricsAggregation: DB error for tenant {tenantId} ({ErrorCodes.MetricsAggregationFailed}): {ex.Message}");
                    // Continue with next tenant - one tenant failure should not stop others
                }
            }

            if (aggregated > 0)
            {
                _logger.SystemInfo($"MetricsAggregation: completed for {aggregated}/{tenantIds.Count} tenants (dates: {yesterday}, {today})");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("MetricsAggregation: cancelled during shutdown");
        }
        catch (ObjectDisposedException)
        {
            _logger.SystemInfo("MetricsAggregation: service disposed during shutdown");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"MetricsAggregation: service state error ({ErrorCodes.MetricsAggregationFailed}): {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }
}
