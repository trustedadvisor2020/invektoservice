using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services;

/// <summary>
/// Background service that periodically deletes expired translation cache entries.
/// Runs every 1 hour. 7-day TTL enforced by TranslationCacheRepository.
/// </summary>
public sealed class TranslationCleanupService : IHostedService, IDisposable
{
    private readonly TranslationCacheRepository _cache;
    private readonly JsonLinesLogger _logger;
    private Timer? _timer;
    private int _isRunning;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    public TranslationCleanupService(TranslationCacheRepository cache, JsonLinesLogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("TranslationCleanupService starting (interval: 1h)");
        _timer = new Timer(OnTimerElapsed, null, InitialDelay, CleanupInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("TranslationCleanupService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private async void OnTimerElapsed(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0) return;
        try
        {
            var deleted = await _cache.DeleteExpiredAsync();
            if (deleted > 0)
                _logger.SystemInfo($"TranslationCleanup: deleted {deleted} expired cache entries");
        }
        catch (OperationCanceledException) { _logger.SystemInfo("TranslationCleanup: cancelled during shutdown"); }
        catch (ObjectDisposedException) { _logger.SystemInfo("TranslationCleanup: service disposed"); }
        catch (NpgsqlException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cleanup failed: {ex.Message}", "-");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }
}
