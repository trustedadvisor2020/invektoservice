using Hangfire;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services.Jobs;

/// <summary>
/// G7 Faz 4: Hangfire recurring job replacing <c>TranslationCleanupService</c>.
/// Deletes expired translation cache entries (7-day TTL enforced by repository).
///
/// Queue: <c>backend</c>. Recurring id: <c>backend:translation-cleanup</c> (cron hourly).
/// </summary>
[Queue("backend")]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class TranslationCleanupJob
{
    private readonly TranslationCacheRepository _cache;
    private readonly JsonLinesLogger _logger;

    public TranslationCleanupJob(TranslationCacheRepository cache, JsonLinesLogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            var deleted = await _cache.DeleteExpiredAsync(ct);
            if (deleted > 0)
                _logger.SystemInfo($"TranslationCleanupJob: deleted {deleted} expired cache entries");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo(
                $"[{ErrorCodes.BackendTranslationCacheError}] TranslationCleanupJob: cancelled (graceful shutdown)");
        }
        // Other exceptions (incl. NpgsqlException) bubble to Hangfire AutomaticRetry + INV-JOB-005.
    }
}
