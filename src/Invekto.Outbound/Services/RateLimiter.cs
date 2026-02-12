using System.Collections.Concurrent;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

/// <summary>
/// Tenant-based in-memory rate limiter. Tracks messages per minute per tenant.
/// When limit is exceeded, messages stay in the DB queue (not rejected).
/// Thread-safe, register as singleton.
/// </summary>
public sealed class RateLimiter
{
    private readonly int _defaultMessagesPerMinute;
    private readonly JsonLinesLogger _logger;

    // Track per-tenant send timestamps (sliding window)
    private readonly ConcurrentDictionary<int, TenantWindow> _windows = new();

    public RateLimiter(int defaultMessagesPerMinute, JsonLinesLogger logger)
    {
        _defaultMessagesPerMinute = defaultMessagesPerMinute;
        _logger = logger;
    }

    /// <summary>
    /// Check if tenant can send a message right now.
    /// Returns true if within rate limit, false if should wait.
    /// </summary>
    public bool TryAcquire(int tenantId)
    {
        var window = _windows.GetOrAdd(tenantId, _ => new TenantWindow());
        return window.TryAcquire(_defaultMessagesPerMinute);
    }

    /// <summary>
    /// Get how many messages tenant has sent in the current minute window.
    /// </summary>
    public int GetCurrentCount(int tenantId)
    {
        if (_windows.TryGetValue(tenantId, out var window))
            return window.GetCurrentCount();
        return 0;
    }

    private sealed class TenantWindow
    {
        private readonly object _lock = new();
        private readonly Queue<DateTime> _timestamps = new();

        public bool TryAcquire(int maxPerMinute)
        {
            lock (_lock)
            {
                PurgeOld();
                if (_timestamps.Count >= maxPerMinute)
                    return false;

                _timestamps.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        public int GetCurrentCount()
        {
            lock (_lock)
            {
                PurgeOld();
                return _timestamps.Count;
            }
        }

        private void PurgeOld()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                _timestamps.Dequeue();
        }
    }
}
