using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;

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
    private readonly JsonLinesLogger _logger;
    private readonly int _intervalMs;

    private Timer? _timer;
    private int _isProcessing; // 0 = idle, 1 = processing (interlocked)
    private CancellationTokenSource? _cts;

    public MessageSenderService(
        OutboundRepository repository,
        RateLimiter rateLimiter,
        MainAppCallbackClient callbackClient,
        JsonLinesLogger logger,
        int intervalMs = 1000)
    {
        _repository = repository;
        _rateLimiter = rateLimiter;
        _callbackClient = callbackClient;
        _logger = logger;
        _intervalMs = intervalMs;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo($"MessageSenderService starting (interval={_intervalMs}ms)");
        _timer = new Timer(ProcessQueue, null, _intervalMs, _intervalMs);
        return Task.CompletedTask;
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
            Thread.Sleep(100);
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

            foreach (var msg in messages)
            {
                if (ct.IsCancellationRequested) break;

                // Check rate limit per tenant
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
                ChatId = 0, // Outbound doesn't have a chat context
                SequenceId = msg.Id,
                Data = new CallbackData
                {
                    MessageText = msg.MessageText,
                    Phone = msg.RecipientPhone,
                    BroadcastId = msg.BroadcastId,
                    OutboundMessageId = msg.Id
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
