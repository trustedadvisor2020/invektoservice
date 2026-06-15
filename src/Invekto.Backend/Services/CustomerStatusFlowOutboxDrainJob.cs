using Hangfire;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3 HARDENING — drains <c>customer_status_flow_outbox</c> by enqueuing one
/// <c>TriggerCustomerStatusFlowJob</c> (Automation queue) per claimed row. Replaces the removed inline
/// post-commit C3a enqueue: the dispatch intent is now written durably inside the C2 transaction, so a
/// crash or Hangfire-enqueue failure no longer loses a customer_status_changed flow trigger.
///
/// Pattern: timer-based <see cref="IHostedService"/> identical to Outbound's <c>InmaOptOutSyncJob</c>
/// (interval tick, interlocked overlap guard, graceful shutdown, startup stale-'processing' recovery).
/// Backend is the legitimate cross-service enqueue point (it already references the Automation job type via
/// PrivateAssets=all, the G7 scheduler-host exception) and is the Hangfire scheduler leader.
///
/// "processed" here means HANDED TO HANGFIRE — the drain leaves the row in 'processing'; the drained job
/// writes the terminal 'done' as an atomic claim BEFORE it runs the flow, so a re-enqueue (after stale
/// recovery) cannot double-fire the flow (exactly-once at start). An enqueue FAILURE bounces the row back to
/// 'pending' (INV-INM-008) until attempts hit MaxAttempts, then 'failed' (INV-INM-009, ops repair signal).
/// </summary>
public sealed class CustomerStatusFlowOutboxDrainJob : IHostedService, IDisposable
{
    private readonly CustomerStatusFlowOutboxRepository _repository;
    private readonly Hangfire.IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _logger;
    private readonly CustomerStatusFlowOutboxDrainOptions _options;

    private Timer? _timer;
    private int _isProcessing;
    private CancellationTokenSource? _cts;

    public CustomerStatusFlowOutboxDrainJob(
        CustomerStatusFlowOutboxRepository repository,
        Hangfire.IBackgroundJobClient jobs,
        JsonLinesLogger logger,
        CustomerStatusFlowOutboxDrainOptions options)
    {
        _repository = repository;
        _jobs = jobs;
        _logger = logger;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo(
            $"CustomerStatusFlowOutboxDrainJob starting (batch={_options.BatchSize}, maxAttempts={_options.MaxAttempts}, intervalMs={_options.IntervalMs})");

        // Recover rows left in 'processing' by a crashed worker from a prior run (stale > 5 × tick interval,
        // floor 300s, to avoid racing a healthy concurrent instance).
        try
        {
            var staleSeconds = Math.Max(300, (_options.IntervalMs / 1000) * 5);
            var recovered = await _repository.RecoverStuckProcessingAsync(staleSeconds, cancellationToken);
            if (recovered > 0)
                _logger.SystemInfo($"CustomerStatusFlowOutboxDrainJob recovered {recovered} stuck 'processing' rows");
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"CustomerStatusFlowOutboxDrainJob recover on start failed (DB unavailable): {ex.Message}");
        }

        _timer = new Timer(Tick, null, _options.IntervalMs, _options.IntervalMs);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("CustomerStatusFlowOutboxDrainJob stopping (graceful shutdown)");
        _timer?.Change(Timeout.Infinite, 0);
        _cts?.Cancel();

        var waited = 0;
        while (Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1 && waited < 300)
        {
            await Task.Delay(100, cancellationToken);
            waited++;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async void Tick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

        try
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            if (ct.IsCancellationRequested) return;

            var batch = await _repository.FetchPendingBatchAsync(_options.BatchSize, _options.MaxAttempts, ct);
            if (batch.Count == 0) return;

            foreach (var row in batch)
            {
                if (ct.IsCancellationRequested) break;
                await EnqueueRowAsync(row, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown; kept observable so ops logs show interrupted ticks.
            _logger.SystemInfo("CustomerStatusFlowOutboxDrainJob tick cancelled (shutdown in progress)");
        }
        catch (NpgsqlException ex)
        {
            // DB unavailable for the claim — drop this tick; the next one retries.
            _logger.SystemError($"CustomerStatusFlowOutboxDrainJob tick DB error: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.SystemError($"CustomerStatusFlowOutboxDrainJob tick timeout: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    private async Task EnqueueRowAsync(CustomerStatusOutboxRow row, CancellationToken ct)
    {
        try
        {
            // G7 cross-service enqueue (Backend references the Automation job type via PrivateAssets=all).
            // Leave the row 'processing' — the job writes the terminal 'done' as its atomic claim.
            _jobs.Enqueue<Invekto.Automation.Services.Jobs.TriggerCustomerStatusFlowJob>(
                j => j.ExecuteAsync(row.TenantId, row.EventId, row.LeadId, row.NewStatus, row.OldStatus,
                    row.FeatureGroupId, row.FeatureGroupName, row.ChangedBy, row.CustomerId, row.Phone,
                    CancellationToken.None));
        }
        catch (Hangfire.BackgroundJobClientException ex)
        {
            await MarkEnqueueFailedAsync(row, $"hangfire: {ex.Message}", ct);
        }
        catch (NpgsqlException ex)
        {
            await MarkEnqueueFailedAsync(row, $"db: {ex.Message}", ct);
        }
        catch (InvalidOperationException ex)
        {
            await MarkEnqueueFailedAsync(row, $"config: {ex.Message}", ct);
        }
    }

    private async Task MarkEnqueueFailedAsync(CustomerStatusOutboxRow row, string rawError, CancellationToken ct)
    {
        var exhausted = row.Attempts + 1 >= _options.MaxAttempts;
        var code = exhausted ? ErrorCodes.CustomerStatusFlowOutboxExhausted : ErrorCodes.InmaWebhookFlowEnqueueFailed;
        _logger.SystemWarn(
            $"[{code}] CustomerStatusFlowOutboxDrainJob enqueue failed (attempt {row.Attempts + 1}/{_options.MaxAttempts}, " +
            $"{(exhausted ? "FAILED" : "retry")}): outbox={row.Id} tenant={row.TenantId} event={row.EventId} lead={row.LeadId} — {rawError}");
        try
        {
            await _repository.MarkEnqueueFailedAsync(row.Id, rawError, _options.MaxAttempts, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.InmaWebhookFlowEnqueueFailed}] CustomerStatusFlowOutboxDrainJob: could not persist enqueue-failure " +
                $"for outbox={row.Id} tenant={row.TenantId} (left 'processing', stale recovery will re-drive): {ex.Message}");
        }
    }
}

/// <summary>FEAT-INMA-PIPELINE-V2 C3 HARDENING: configuration for the outbox drain (bound from InmaWebhook:FlowOutbox).</summary>
public sealed class CustomerStatusFlowOutboxDrainOptions
{
    /// <summary>Rows claimed per tick.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Enqueue-failure retry budget per row before status flips to 'failed' (INV-INM-009).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Drain timer interval (ms). 10s keeps status-change automation latency low without busy-polling.</summary>
    public int IntervalMs { get; set; } = 10_000;
}
