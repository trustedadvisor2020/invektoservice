using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Services;

/// <summary>
/// Background service that dequeues messages and sends them via Main App callback.
/// Respects tenant-based rate limits. Graceful shutdown via CancellationToken.
/// </summary>
public sealed class MessageSenderService : IHostedService, IDisposable
{
    private readonly OutboundRepository _repository;
    private readonly RateLimiter _rateLimiter;
    private readonly MainAppCallbackClient _callbackClient;
    private readonly WapCrmSendClient _wapCrmSendClient;
    private readonly CxapiSendOptions _cxapiOptions;
    private readonly JsonLinesLogger _logger;
    private readonly int _intervalMs;

    private Timer? _timer;
    private int _isProcessing; // 0 = idle, 1 = processing (interlocked)
    private CancellationTokenSource? _cts;

    public MessageSenderService(
        OutboundRepository repository,
        RateLimiter rateLimiter,
        MainAppCallbackClient callbackClient,
        WapCrmSendClient wapCrmSendClient,
        CxapiSendOptions cxapiOptions,
        JsonLinesLogger logger,
        int intervalMs = 1000)
    {
        _repository = repository;
        _rateLimiter = rateLimiter;
        _callbackClient = callbackClient;
        _wapCrmSendClient = wapCrmSendClient;
        _cxapiOptions = cxapiOptions;
        _logger = logger;
        _intervalMs = intervalMs;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo($"MessageSenderService starting (interval={_intervalMs}ms)");

        // FEAT-PROJELER / cxapi (PR-3a) startup recovery, BEFORE arming the timer. Route-scoped so the
        // bridge is untouched. (1) cxapi rows leased but crashed pre-POST ('sending') -> requeue (never
        // POSTed, safe). (2) cxapi rows crashed mid-POST (stale 'posting') -> 'ambiguous' (manual/ops,
        // delivery unknown) + complete the affected broadcasts. Non-fatal: a startup-time DB failure is
        // logged and the worker still arms (recovery retried on the next restart).
        try
        {
            await _repository.ResetStrandedCxapiSendingAsync(cancellationToken);
            var sweptBroadcasts = await _repository.SweepStrandedPostingAsync(_cxapiOptions.StalePostingMinutes, cancellationToken);
            foreach (var bid in sweptBroadcasts)
                await TryCompleteBroadcastAsync(bid, cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            // The recovery touches only Postgres. A startup DB hiccup leaves the stranded rows safely
            // non-terminal (cxapi 'sending'/'posting' are never duplicated) and they are recovered on the
            // next restart — so this is logged and non-fatal, not silently lost.
            _logger.SystemError($"[cxapi-send] startup recovery failed (non-fatal, retried next start): {ex.Message}");
        }

        _timer = new Timer(ProcessQueue, null, _intervalMs, _intervalMs);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("MessageSenderService stopping (graceful shutdown)");
        _timer?.Change(Timeout.Infinite, 0);
        _cts?.Cancel();

        // Wait for current processing to finish (max 10s)
        var waitCount = 0;
        while (Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1 && waitCount < 100)
        {
            await Task.Delay(100, cancellationToken);
            waitCount++;
        }

        // Reset any messages stuck in 'sending' back to 'queued'
        try
        {
            await _repository.ResetSendingMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.SystemError($"Failed to reset stale sending messages: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async void ProcessQueue(object? state)
    {
        // Prevent overlapping processing
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
            return;

        try
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            if (ct.IsCancellationRequested) return;

            // Dequeue a small batch
            var messages = await _repository.DequeueMessagesAsync(10, ct);
            if (messages.Count == 0) return;

            // FEAT-PROJELER / cxapi (PR-3a, Codex P0 #8): batch-load WapCRM creds for the DISTINCT
            // cxapi-route tenants in this batch — one query, no N+1. Empty list -> empty dict (no query).
            var cxapiTenantIds = messages
                .Where(m => m.SendRoute == "wapcrm_cxapi")
                .Select(m => m.TenantId)
                .Distinct()
                .ToList();
            var cxapiCreds = await _repository.GetWapCrmSettingsBatchAsync(cxapiTenantIds, ct);

            foreach (var msg in messages)
            {
                if (ct.IsCancellationRequested) break;

                if (msg.SendRoute == "wapcrm_cxapi")
                {
                    // Per (tenant, instance) rate limit — honors a cxapi 301/302 cooldown.
                    if (!_rateLimiter.TryAcquire(msg.TenantId, msg.InstanceId ?? 0))
                    {
                        await _repository.UpdateMessageStatusAsync(msg.Id, "queued", ct: ct);
                        continue;
                    }

                    await SendViaCxapiAsync(msg, cxapiCreds, ct);
                }
                else
                {
                    // Bridge path — unchanged (tenant-only rate limit).
                    if (!_rateLimiter.TryAcquire(msg.TenantId))
                    {
                        // Put back to queued (rate limited - will retry next cycle)
                        await _repository.UpdateMessageStatusAsync(msg.Id, "queued", ct: ct);
                        _logger.SystemInfo($"Rate limited: tenant={msg.TenantId}, message={msg.Id}, requeued");
                        continue;
                    }

                    await SendMessageAsync(msg, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.SystemError($"MessageSenderService error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    private async Task SendMessageAsync(QueuedMessage msg, CancellationToken ct)
    {
        try
        {
            var callback = new OutgoingCallback
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Action = CallbackActions.SendMessage,
                TenantId = msg.TenantId,
                ChatId = "", // Outbound doesn't have a chat context
                SequenceId = msg.Id,
                Data = new CallbackData
                {
                    MessageText = msg.MessageText,
                    Phone = msg.RecipientPhone,
                    BroadcastId = msg.BroadcastId,
                    OutboundMessageId = msg.Id,
                    // FEAT-J2: outbound broadcasts are marketing by definition. Triggered via
                    // BroadcastId != null; INMA applies opt-out check server-side when this
                    // category reaches /api/chatoperation (double-guard with INSE opt-out).
                    MessageCategory = msg.BroadcastId.HasValue ? "marketing" : null,
                    // FEAT-DMP: forward placeholder list recorded at broadcast-create time.
                    // Bridge flips wapPayload.dynamicMessage=true when this is non-empty.
                    DynamicFields = msg.DynamicFields,
                },
                ProcessingTimeMs = 0,
                Timestamp = DateTime.UtcNow
            };

            var success = await _callbackClient.SendCallbackAsync(callback, ct: ct);

            if (success)
            {
                // Mark as sent - delivery status will come via webhook later
                await _repository.UpdateMessageStatusAsync(msg.Id, "sent", ct: ct);

                if (msg.BroadcastId.HasValue)
                    await _repository.IncrementBroadcastCounterAsync(msg.BroadcastId.Value, "sent", ct);

                // Check if broadcast is complete
                if (msg.BroadcastId.HasValue)
                    await TryCompleteBroadcastAsync(msg.BroadcastId.Value, ct);
            }
            else
            {
                await _repository.UpdateMessageStatusAsync(
                    msg.Id, "failed", failedReason: "Callback to Main App failed after retries", ct: ct);

                if (msg.BroadcastId.HasValue)
                    await _repository.IncrementBroadcastCounterAsync(msg.BroadcastId.Value, "failed", ct);

                if (msg.BroadcastId.HasValue)
                    await TryCompleteBroadcastAsync(msg.BroadcastId.Value, ct);

                _logger.SystemError(
                    $"[{ErrorCodes.OutboundMessageSendCallbackFailed}] Message send failed: " +
                    $"id={msg.Id}, tenant={msg.TenantId}, phone={msg.RecipientPhone}");
            }
        }
        catch (Exception ex)
        {
            _logger.SystemError($"SendMessage exception: id={msg.Id}, error={ex.Message}");
            await _repository.UpdateMessageStatusAsync(
                msg.Id, "failed", failedReason: $"Exception: {ex.Message}", ct: ct);

            if (msg.BroadcastId.HasValue)
            {
                await _repository.IncrementBroadcastCounterAsync(msg.BroadcastId.Value, "failed", ct);
                await TryCompleteBroadcastAsync(msg.BroadcastId.Value, ct);
            }
        }
    }

    /// <summary>
    /// FEAT-PROJELER / cxapi (PR-3a) send path. Idempotent CAS state machine:
    ///   1. Validate creds (defensive: a 'sending' row with vanished creds -> 'failed' INV-OB-062, no POST).
    ///   2. CAS 'sending' -> 'posting' (Codex P0-1): if it misses (reset/raced) DO NOT POST.
    ///   3. POST via WapCrmSendClient (typed outcome; throws only on caller-cancel).
    ///   4. Map outcome (CxapiOutcomeMapper) + persist atomically (status + provider_* + counter, CAS on 'posting').
    /// Only RateLimited requeues (+ (tenant,instance) cooldown); timeout/transport -> 'ambiguous', never retried.
    /// </summary>
    private async Task SendViaCxapiAsync(QueuedMessage msg, Dictionary<int, WapCrmSettings> creds, CancellationToken ct)
    {
        var instanceId = msg.InstanceId ?? 0;
        creds.TryGetValue(msg.TenantId, out var wap);
        var secretKey = wap?.SecretKey;

        // Defensive: creds may have vanished OR the tenant may have changed its default wapcrm instance
        // between broadcast-create and send (rare race). The stamped instance_id is IMMUTABLE; if the
        // CURRENT settings no longer match it (different/absent instance), the live secret/userId may
        // belong to a DIFFERENT instance — never POST a mismatched instance+credential pair. The row is
        // still 'sending' (not yet posted) -> fail it directly (INV-OB-062), no POST. Secret never logged.
        // string.IsNullOrWhiteSpace is [NotNullWhen(false)], so on fall-through secretKey is non-null.
        var instanceMismatch = wap?.InstanceId is int liveInstance && liveInstance != instanceId;
        if (instanceId <= 0 || wap is null || string.IsNullOrWhiteSpace(secretKey) || wap.UserId <= 0 || instanceMismatch)
        {
            var reason = instanceMismatch
                ? $"cxapi stamped instance {instanceId} no longer matches tenant default {wap?.InstanceId} (settings changed after broadcast-create)"
                : "cxapi route misconfigured (missing instance/secret/userId)";
            _logger.SystemError(
                $"[{ErrorCodes.CxapiRouteMisconfigured}] cxapi send misconfigured: tenant={msg.TenantId}, message={msg.Id}, instance={instanceId} ({reason})");
            var (_, failBid) = await _repository.MarkCxapiOutcomeAsync(
                msg.Id, msg.TenantId, "failed", providerStatusCode: null, providerStatus: null, providerRequestId: null,
                providerErrorMessage: $"[{ErrorCodes.CxapiRouteMisconfigured}] {reason}",
                attemptCount: msg.AttemptCount, counterColumn: "failed", fromStatus: "sending", ct: CancellationToken.None);
            if (failBid.HasValue) await TryCompleteBroadcastAsync(failBid.Value, ct);
            return;
        }

        var attempt = msg.AttemptCount + 1;

        // CAS 'sending' -> 'posting' immediately before the POST (Codex P0-1). A miss means a concurrent
        // shutdown reset the row to 'queued' (or it was claimed elsewhere) -> MUST NOT POST.
        if (!await _repository.SetMessagePostingAsync(msg.Id, msg.TenantId, attempt, CancellationToken.None))
        {
            _logger.SystemWarn($"[cxapi-send] posting CAS missed (raced/reset), skipping POST: message={msg.Id}");
            return;
        }

        WapCrmSendResult result;
        try
        {
            result = await _wapCrmSendClient.SendPlainTextAsync(new WapCrmSendRequest
            {
                TenantId = msg.TenantId,
                InstanceId = instanceId,
                UserId = wap.UserId,
                SecretKey = secretKey,
                ChatPhoneNumber = msg.RecipientPhone,
                MessageText = msg.MessageText
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-POST: leave the row 'posting'. ResetSendingMessages won't touch it; the next
            // startup sweep resolves it to 'ambiguous' (delivery unknown). Do NOT classify here.
            throw;
        }

        var decision = CxapiOutcomeMapper.Map(result.Outcome, attempt, _cxapiOptions.MaxSendAttempts);

        // Tag terminal-failure outcomes with their registered INV code so the codes actually surface in
        // failed_reason AND the structured log (not dead constants): provider status=false / rate-exhausted
        // -> INV-OB-064; timeout/transport (delivery unknown) -> INV-OB-065. 'sent' and the RateLimited
        // requeue ('queued') carry no error code.
        var outcomeCode = decision.Status switch
        {
            "failed" => ErrorCodes.CxapiProviderRejected,
            "ambiguous" => ErrorCodes.CxapiSendAmbiguous,
            _ => (string?)null
        };
        var persistedError = outcomeCode != null
            ? $"[{outcomeCode}] {result.ProviderErrorMessage ?? result.Outcome.ToString()}"
            : result.ProviderErrorMessage;

        // CancellationToken.None: the message was POSTed, so the outcome MUST be persisted even if
        // shutdown started — otherwise an actually-sent message would later be mis-swept to 'ambiguous'.
        var (applied, broadcastId) = await _repository.MarkCxapiOutcomeAsync(
            msg.Id, msg.TenantId, decision.Status,
            providerStatusCode: result.ProviderStatusCode,
            providerStatus: result.Outcome is WapCrmSendOutcome.Submitted or WapCrmSendOutcome.ProviderFailed
                ? result.ProviderStatus : null,
            providerRequestId: result.ProviderRequestId,
            providerErrorMessage: persistedError,
            attemptCount: attempt,
            counterColumn: decision.CounterColumn,
            // PR-3b-1: persist the captured wamid (Submitted only; null otherwise -> COALESCE no-op) so a
            // later ack (InstanceMessageID == this wamid) resolves to this exact tenant-scoped row.
            externalMessageId: result.ProviderMessageId,
            fromStatus: "posting",
            ct: CancellationToken.None);

        if (!applied)
        {
            // The row was no longer 'posting' (e.g. swept to 'ambiguous' on a concurrent startup) — do not
            // double-count or complete.
            _logger.SystemWarn($"[cxapi-send] outcome CAS missed (row no longer 'posting'): message={msg.Id}, outcome={result.Outcome}");
            return;
        }

        if (decision.ApplyCooldown)
        {
            var cooldown = result.RetryAfter ?? TimeSpan.FromMilliseconds(_cxapiOptions.DefaultCooldownMs);
            _rateLimiter.ApplyCooldown(msg.TenantId, instanceId, cooldown);
        }

        var logLine =
            $"[cxapi-send]{(outcomeCode != null ? $" [{outcomeCode}]" : "")} tenant={msg.TenantId}, instance={instanceId}, " +
            $"message={msg.Id}, broadcast={msg.BroadcastId}, outcome={result.Outcome}, status={decision.Status}, " +
            $"attempt={attempt}, http={result.HttpStatusCode}, providerCode={result.ProviderStatusCode}";
        if (outcomeCode != null)
            _logger.SystemWarn(logLine);
        else
            _logger.SystemInfo(logLine);

        // RateLimited requeue is not terminal; everything else may complete the broadcast.
        if (!decision.Requeue && broadcastId.HasValue)
            await TryCompleteBroadcastAsync(broadcastId.Value, ct);
    }

    private async Task TryCompleteBroadcastAsync(Guid broadcastId, CancellationToken ct)
    {
        try
        {
            if (await _repository.IsBroadcastCompleteAsync(broadcastId, ct))
            {
                await _repository.UpdateBroadcastStatusAsync(broadcastId, "completed", ct);
                _logger.SystemInfo($"Broadcast completed: {broadcastId}");
            }
        }
        catch (Exception ex)
        {
            _logger.SystemError($"Error checking broadcast completion: {broadcastId}, {ex.Message}");
        }
    }
}
