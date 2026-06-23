using Chatinbox.Outbound.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Inma;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// FEAT-J2: Drains the <c>inma_optout_outbox</c> table by pushing each pending
/// row to INMA's POST /api/optout (or /api/optin) and persisting the resulting
/// status transition.
///
/// Pattern: Timer-based <see cref="IHostedService"/> identical to
/// <see cref="MessageSenderService"/>. Hangfire integration is deferred to G7
/// (IN_PROGRESS) — a timer-driven worker keeps FEAT-J2 self-contained inside
/// Outbound while reusing a proven operational pattern (interval, interlocked
/// overlap guard, graceful shutdown).
///
/// Terminal-success transitions: StatusCode '0' or '909' (idempotent replay).
/// Terminal-failure transitions: StatusCode '908' (INMA contact-not-found) or
/// exceeding MaxAttempts. Everything else (network/5xx/PARSE-FAIL) increments
/// attempts and stays 'pending' so the next tick retries.
/// </summary>
public sealed class InmaOptOutSyncJob : IHostedService, IDisposable
{
    private readonly OutboundRepository _repository;
    private readonly IInmaContactOptOutClient _client;
    private readonly JsonLinesLogger _logger;
    private readonly InmaOptOutSyncOptions _options;

    private Timer? _timer;
    private int _isProcessing;
    private CancellationTokenSource? _cts;

    public InmaOptOutSyncJob(
        OutboundRepository repository,
        IInmaContactOptOutClient client,
        JsonLinesLogger logger,
        InmaOptOutSyncOptions options)
    {
        _repository = repository;
        _client = client;
        _logger = logger;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo(
            $"InmaOptOutSyncJob starting (mode={_options.Mode}, batch={_options.BatchSize}, intervalMs={_options.IntervalMs})");

        // Recover rows left in 'processing' by a crashed worker from a prior run
        // (stale > 5 × tick interval to avoid racing a healthy concurrent instance).
        try
        {
            var staleSeconds = Math.Max(300, (_options.IntervalMs / 1000) * 5);
            var recovered = await _repository.RecoverStuckProcessingAsync(staleSeconds, cancellationToken);
            if (recovered > 0)
                _logger.SystemInfo($"InmaOptOutSyncJob recovered {recovered} stuck 'processing' rows");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn($"InmaOptOutSyncJob recover on start failed (DB unavailable): {ex.Message}");
        }

        _timer = new Timer(Tick, null, _options.IntervalMs, _options.IntervalMs);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("InmaOptOutSyncJob stopping (graceful shutdown)");
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

            var batch = await _repository.FetchPendingOutboxBatchAsync(
                _options.BatchSize, _options.MaxAttempts, ct);
            if (batch.Count == 0) return;

            foreach (var row in batch)
            {
                if (ct.IsCancellationRequested) break;
                await ProcessRowAsync(row, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown; kept observable so ops logs show
            // when drain ticks were interrupted by a service stop.
            _logger.SystemInfo("InmaOptOutSyncJob tick cancelled (shutdown in progress)");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // DB unavailable — drop this tick; next one retries.
            _logger.SystemError($"InmaOptOutSyncJob tick DB error: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.SystemError($"InmaOptOutSyncJob tick timeout: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    private async Task ProcessRowAsync(OutboxRow row, CancellationToken ct)
    {
        var request = new InmaOptOutRequest
        {
            IdentifierType = "phone",
            Identifier = row.Phone,
            InstanceID = row.InstanceId,
            Scope = row.Scope,
            Reason = row.Reason,
            Source = row.Source,
        };

        InmaOptOutResult result;
        try
        {
            result = row.EventType == "opt_in"
                ? await _client.PushOptInAsync(request, ct)
                : await _client.PushOptOutAsync(request, ct);
        }
        // Transport-level faults coming out of the INMA HTTP client (Http impl) —
        // treat as transient so the row bounces back to 'pending' for retry.
        catch (HttpRequestException ex)
        {
            await MarkClientExceptionAsync(row, "NETWORK", ex.Message, ct);
            return;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout surfaces as TaskCanceledException when the request CT
            // was NOT user-cancelled; distinguishing it from a graceful shutdown lets
            // us treat it as a transient failure and retry.
            await MarkClientExceptionAsync(row, "TIMEOUT", ex.Message, ct);
            return;
        }
        catch (System.Text.Json.JsonException ex)
        {
            await MarkClientExceptionAsync(row, "PARSE-FAIL", ex.Message, ct);
            return;
        }

        // NoOp fallback — park until Mode is flipped back to Http.
        if (result.StatusCode == "SKIPPED-NOOP")
        {
            await _repository.MarkOutboxSkippedNoOpAsync(row.Id, ct);
            _logger.SystemInfo(
                $"[{ErrorCodes.InmaOptOutSyncSkippedNoOp}] outbox row skipped (NoOp mode): id={row.Id}, tenant={row.TenantId}, event={row.EventType}");
            return;
        }

        // Terminal success: 0 or 909 (idempotent replay).
        if (result.IsSuccessOrAlreadyOut)
        {
            await _repository.MarkOutboxProcessedAsync(row.Id, result.StatusCode, ct);
            if (result.AlreadyOptedOut)
            {
                _logger.SystemInfo(
                    $"INMA opt-out idempotent replay: id={row.Id}, tenant={row.TenantId}, phone={row.Phone}");
            }
            return;
        }

        // INMA 908 contact-not-found: retrying is pointless.
        if (result.StatusCode == "908")
        {
            await _repository.MarkOutboxFailedAsync(
                row.Id, "908", result.Message, isFinal: true, _options.MaxAttempts, ct);
            _logger.SystemWarn(
                $"[{ErrorCodes.InmaOptOutSyncFailed}] INMA 908 contact-not-found: id={row.Id}, tenant={row.TenantId}, phone={row.Phone}");
            return;
        }

        // Transient: network / 5xx / parse fail → retry next tick.
        await _repository.MarkOutboxFailedAsync(
            row.Id, result.StatusCode, result.Message, isFinal: false, _options.MaxAttempts, ct);
        _logger.SystemWarn(
            $"INMA opt-out push transient failure: id={row.Id}, tenant={row.TenantId}, code={result.StatusCode}, http={result.HttpStatusCode}, attempts={row.Attempts + 1}");
    }

    private async Task MarkClientExceptionAsync(OutboxRow row, string sentinel, string message, CancellationToken ct)
    {
        _logger.SystemError(
            $"[{ErrorCodes.InmaOptOutSyncFailed}] INMA client threw ({sentinel}): id={row.Id}, tenant={row.TenantId}, err={message}");
        await _repository.MarkOutboxFailedAsync(
            row.Id, sentinel, message, isFinal: false, _options.MaxAttempts, ct);
    }
}

/// <summary>
/// FEAT-J2: Configuration bound from <c>InmaAuth:OptOutSync</c>.
/// </summary>
public sealed class InmaOptOutSyncOptions
{
    /// <summary>"Http" (default post-INMA teslim) or "NoOp" (emergency kill-switch).</summary>
    public string Mode { get; set; } = "Http";

    /// <summary>INMA base URL; defaults to public cxapi host.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net";

    /// <summary>X-CIB-SecretKey header value.</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>Rows fetched per tick.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Retry budget per row before status flips to 'failed'.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Per-request HTTP timeout.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Internal timer interval (ms). Equivalent of a 1-minute cron.</summary>
    public int IntervalMs { get; set; } = 60_000;
}
