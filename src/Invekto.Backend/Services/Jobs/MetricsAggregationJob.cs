using Hangfire;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services.Jobs;

/// <summary>
/// G7 Faz 4: Hangfire recurring job replacing <c>MetricsAggregationService</c> (PKT-3 GR-2.5).
/// Aggregates auto_reply_log + chat_sessions into daily_metrics / daily_intent_metrics for
/// today + yesterday (yesterday catches late-arriving data). UPSERT ensures idempotency.
///
/// Queue: <c>backend</c>. Recurring id: <c>backend:metrics-aggregation</c> (cron */5 min).
/// Per-tenant NpgsqlException is caught and logged (row-level isolation — one tenant failure
/// does not stop others). Top-level failures bubble to Hangfire AutomaticRetry + INV-JOB-005.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class MetricsAggregationJob
{
    private readonly AnalyticsRepository _repo;
    private readonly JsonLinesLogger _logger;

    public MetricsAggregationJob(AnalyticsRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            var tenantIds = await _repo.GetTenantIdsWithAutoReplyDataAsync();
            if (tenantIds.Count == 0)
                return;

            var aggregated = 0;
            foreach (var tenantId in tenantIds)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await _repo.UpsertDailyMetricsAsync(tenantId, yesterday);
                    await _repo.UpsertDailyIntentMetricsAsync(tenantId, yesterday);
                    await _repo.UpsertDailyMetricsAsync(tenantId, today);
                    await _repo.UpsertDailyIntentMetricsAsync(tenantId, today);
                    aggregated++;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.MetricsAggregationFailed}] MetricsAggregationJob: DB error for tenant {tenantId}: {ex.Message}");
                }
            }

            if (aggregated > 0)
                _logger.SystemInfo(
                    $"MetricsAggregationJob: completed for {aggregated}/{tenantIds.Count} tenants (dates: {yesterday}, {today})");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo(
                $"[{ErrorCodes.MetricsAggregationFailed}] MetricsAggregationJob: cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire AutomaticRetry + INV-JOB-005.
    }
}
