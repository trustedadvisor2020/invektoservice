using System.Text.Json;
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
    // Periodic stranded-'posting' recovery throttle. Read/written ONLY inside ProcessQueue, which is serialized
    // by _isProcessing, so a plain field is safe (no Interlocked). See TryRecoverStrandedAsync.
    private long _lastSweepTicks;  // Environment.TickCount64 at the last periodic sweep (0 = not yet)
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

        // The startup sweep above just ran; stamp it so the FIRST periodic sweep is one interval later
        // (TryRecoverStrandedAsync throttles on this), avoiding a redundant sweep right after boot.
        _lastSweepTicks = Environment.TickCount64;

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

            // Periodic stranded-'posting' recovery (throttled) so a stranded run heals to 'ambiguous' WITHOUT a
            // restart. Runs inside the _isProcessing guard => serialized with sends and covered by the typed
            // catches below; the finite HttpClient.Timeout backstop already prevents the send loop from wedging.
            await TryRecoverStrandedAsync(ct);

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

    /// <summary>
    /// FEAT-PROJELER / cxapi: periodic stranded-'posting' recovery, throttled to
    /// <see cref="CxapiSendOptions.RecoverySweepIntervalMs"/>. Mirrors the StartAsync startup sweep
    /// (<see cref="OutboundRepository.SweepStrandedPostingAsync"/>) but runs while the worker is UP, so a row
    /// crash-stranded mid-run resolves to 'ambiguous' (+ completes its broadcast) WITHOUT a service restart
    /// (incident 2026-06-12). Staleness-gated in SQL (only 'posting' older than StalePostingMinutes) → an
    /// in-flight POST is never touched. Called INSIDE the _isProcessing guard (serialized with sends), so it
    /// needs no own overlap guard; only the 'posting' sweep is periodic — the 'sending' reset stays
    /// startup-only (no staleness gate, would race a live send). A caller-cancellation (shutdown) and any
    /// unexpected throw propagate to ProcessQueue's typed catches; only the DB-transient case is handled here.
    /// </summary>
    private async Task TryRecoverStrandedAsync(CancellationToken ct)
    {
        var now = Environment.TickCount64;
        if (now - _lastSweepTicks < _cxapiOptions.RecoverySweepIntervalMs)
            return; // throttled — not yet due
        _lastSweepTicks = now;

        try
        {
            var swept = await _repository.SweepStrandedPostingAsync(_cxapiOptions.StalePostingMinutes, ct);
            foreach (var bid in swept)
                await TryCompleteBroadcastAsync(bid, ct);
            if (swept.Count > 0)
                _logger.SystemWarn(
                    $"[cxapi-send] periodic recovery swept {swept.Count} stranded 'posting' broadcast(s) to 'ambiguous' (no restart needed)");
        }
        catch (NpgsqlException ex)
        {
            // Recovery touches only Postgres; a transient DB hiccup is non-fatal — the row stays safely
            // 'posting' and recovery retries next interval. INV-OB-065 = the same stranded-posting/ambiguous
            // recovery domain as SweepStrandedPostingAsync's own marker. OperationCanceledException (shutdown)
            // and any unexpected exception are intentionally NOT caught here — ProcessQueue's typed catches own
            // those, keeping this to typed-only catches per codebase policy.
            _logger.SystemError(
                $"[{ErrorCodes.CxapiSendAmbiguous}] periodic recovery sweep failed (non-fatal, retried next interval): {ex.Message}");
        }

        // FEATURE C (migration 064): periodic stranded-'sending' recovery — a row claimed by the dequeue
        // worker but never POSTed (crash between claim and send, then restart mid-queue) is reset to 'queued'
        // WITHOUT a restart (the ungated startup reset in StartAsync runs only at boot). Staleness-gated on
        // claimed_at in SQL (StaleSendingMinutes), so a row claimed in this dispatch cycle is never reset, and
        // duplicate-safe because 'sending' is strictly pre-POST. Its OWN try/catch with a distinct marker
        // (INV-OB-096) so a sending-recovery DB hiccup never masks or is masked by the 'posting' sweep above.
        try
        {
            var resetCount = await _repository.SweepStrandedSendingAsync(_cxapiOptions.StaleSendingMinutes, ct);
            if (resetCount > 0)
                _logger.SystemWarn(
                    $"[cxapi-send] periodic recovery reset {resetCount} stranded 'sending' message(s) to 'queued' (no restart needed)");
        }
        catch (NpgsqlException ex)
        {
            // Non-fatal: the row stays safely 'sending' (pre-POST, never duplicated) and recovery retries next
            // interval. INV-OB-096 = the dedicated sending-recovery marker (kept apart from INV-OB-065's
            // posting/ambiguous domain). Shutdown-cancellation and unexpected throws propagate to ProcessQueue.
            _logger.SystemError(
                $"[{ErrorCodes.CxapiSendingRecoveryFailed}] periodic 'sending' recovery sweep failed (non-fatal, retried next interval): {ex.Message}");
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
        catch (OperationCanceledException)
        {
            // Graceful shutdown: propagate to ProcessQueue's OperationCanceledException handler instead of
            // marking the message 'failed' (and re-throwing a second OCE on the recovery DB writes) under a
            // cancelled token. The row stays 'sending' and the FEATURE C sweep re-queues it (audit Outbound-7).
            throw;
        }
        catch (Exception ex)
        {
            // SANCTIONED per-message dispatch resilience boundary (see arch/codex-context.md): a single poison
            // message must be marked 'failed' here so it neither aborts the whole dispatch cycle nor is retried
            // forever by the FEATURE C 'sending' recovery sweep. ex.Message is log-only / stored in failed_reason
            // (internal worker, no caller-facing leak). Shutdown cancellation already re-threw above.
            _logger.SystemError($"[{ErrorCodes.OutboundMessageSendCallbackFailed}] SendMessage exception: id={msg.Id}, error={ex.Message}");
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

        // ── PR-4: an HSM row (message_kind='wapcrm_template') sends the approved template via
        // template_ref + the RESOLVED per-recipient params snapshotted at preview. Parse the
        // snapshot BEFORE the posting CAS so a malformed row fails cleanly from 'sending'
        // (typed, no POST, never stranded). Plain-text rows take the PR-3a path unchanged. ──
        var isHsm = msg.MessageKind == "wapcrm_template";
        WapCrmTemplateSendRequest? templateRequest = null;
        if (isHsm)
        {
            var (parsed, parseError) = BuildTemplateRequest(msg, instanceId, wap.UserId, secretKey);
            if (parsed == null)
            {
                _logger.SystemError(
                    $"[{ErrorCodes.CxapiRouteMisconfigured}] HSM message snapshot invalid: tenant={msg.TenantId}, message={msg.Id} ({parseError})");
                var (_, hsmFailBid) = await _repository.MarkCxapiOutcomeAsync(
                    msg.Id, msg.TenantId, "failed", providerStatusCode: null, providerStatus: null, providerRequestId: null,
                    providerErrorMessage: $"[{ErrorCodes.CxapiRouteMisconfigured}] {parseError}",
                    attemptCount: msg.AttemptCount, counterColumn: "failed", fromStatus: "sending", ct: CancellationToken.None);
                if (hsmFailBid.HasValue) await TryCompleteBroadcastAsync(hsmFailBid.Value, ct);
                return;
            }
            templateRequest = parsed;
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
            // PR-4: route by the row's immutable message_kind. The state machine, outcome mapping,
            // wamid persistence and rate-limit cooldown below are IDENTICAL for both kinds.
            result = templateRequest != null
                ? await _wapCrmSendClient.SendTemplateAsync(templateRequest, ct)
                : await _wapCrmSendClient.SendPlainTextAsync(new WapCrmSendRequest
                {
                    TenantId = msg.TenantId,
                    InstanceId = instanceId,
                    UserId = wap.UserId,
                    SecretKey = secretKey,
                    ChatPhoneNumber = ToCxapiPhone(msg.RecipientPhone),
                    MessageText = msg.MessageText
                }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-POST: leave the row 'posting'. ResetSendingMessages won't touch it; the next
            // startup sweep resolves it to 'ambiguous' (delivery unknown). Do NOT classify here.
            throw;
        }
        catch (ArgumentException ex)
        {
            // Client-side contract guard fired (e.g. a media URL that slipped past the pre-parse).
            // Nothing was POSTed — resolve the 'posting' row to a typed terminal failure so it is
            // never stranded (claimed-job doctrine), then stop.
            _logger.SystemError(
                $"[{ErrorCodes.CxapiRouteMisconfigured}] cxapi send request invalid: tenant={msg.TenantId}, message={msg.Id} ({ex.Message})");
            var (_, argFailBid) = await _repository.MarkCxapiOutcomeAsync(
                msg.Id, msg.TenantId, "failed", providerStatusCode: null, providerStatus: null, providerRequestId: null,
                providerErrorMessage: $"[{ErrorCodes.CxapiRouteMisconfigured}] {ex.Message}",
                attemptCount: attempt, counterColumn: "failed", fromStatus: "posting", ct: CancellationToken.None);
            if (argFailBid.HasValue) await TryCompleteBroadcastAsync(argFailBid.Value, ct);
            return;
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

    /// <summary>
    /// PR-4: builds the typed template send request from an HSM row's snapshot columns.
    /// template_params is the FLAT resolved {paramKey: value} dict written at broadcast create
    /// (copied from the preview snapshot); template_header_media is {"url": ...}. Returns
    /// (null, reason) on a malformed snapshot — the caller fails the row typed, no POST.
    /// </summary>
    private static (WapCrmTemplateSendRequest? request, string? error) BuildTemplateRequest(
        QueuedMessage msg, int instanceId, int userId, string secretKey)
    {
        var slug = msg.TemplateRef?.Trim();
        if (string.IsNullOrEmpty(slug))
            return (null, "HSM row has no template_ref");

        List<WapCrmTemplateParamValue>? parameters = null;
        if (!string.IsNullOrEmpty(msg.TemplateParams))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(msg.TemplateParams);
                if (dict is { Count: > 0 })
                    parameters = dict
                        .Select(kv => new WapCrmTemplateParamValue { ParamKey = kv.Key, Value = kv.Value ?? string.Empty })
                        .ToList();
            }
            catch (JsonException ex)
            {
                return (null, $"template_params unreadable: {ex.Message}");
            }
        }

        WapCrmTemplateHeaderMedia? headerMedia = null;
        if (!string.IsNullOrEmpty(msg.TemplateHeaderMedia))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.TemplateHeaderMedia);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("url", out var urlEl)
                    && urlEl.ValueKind == JsonValueKind.String
                    && urlEl.GetString() is string url
                    && !string.IsNullOrWhiteSpace(url))
                {
                    headerMedia = new WapCrmTemplateHeaderMedia { Url = url };
                }
                else
                {
                    return (null, "template_header_media has no url");
                }
            }
            catch (JsonException ex)
            {
                return (null, $"template_header_media unreadable: {ex.Message}");
            }
        }

        return (new WapCrmTemplateSendRequest
        {
            TenantId = msg.TenantId,
            InstanceId = instanceId,
            UserId = userId,
            SecretKey = secretKey,
            ChatPhoneNumber = ToCxapiPhone(msg.RecipientPhone),
            TemplateId = slug,
            Parameters = parameters,
            HeaderMedia = headerMedia
        }, null);
    }

    // cxapi contract: chatPhoneNumber is "905XXXXXXXXX" WITHOUT the leading '+'
    // (wapcrm-api-integration-guide §2/§3.2); our rows store E.164 ("+905...").
    private static string ToCxapiPhone(string phone) => phone.TrimStart('+');

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
