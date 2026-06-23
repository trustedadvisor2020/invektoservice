using System.Collections.Concurrent;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// In-memory sliding-window rate limiter.
///
/// Keyed by (tenant_id, instance_id). The bridge path calls <see cref="TryAcquire(int)"/>
/// (mapped to instance 0) and behaves exactly as before. The cxapi path
/// (FEAT-PROJELER / cxapi PR-3a) calls <see cref="TryAcquire(int,int)"/> because the WapCRM
/// rate limit is per instance, and applies a cooldown via <see cref="ApplyCooldown"/> when cxapi
/// returns a 301/302 rate-limit, so subsequent dequeues for that instance back off.
///
/// When a limit is exceeded the message stays queued (not rejected). Thread-safe; singleton.
/// </summary>
public sealed class RateLimiter
{
    private readonly int _defaultMessagesPerMinute;
    private readonly JsonLinesLogger _logger;
    private readonly Func<DateTime> _utcNow;

    // Track per-(tenant, instance) send timestamps + cooldown.
    private readonly ConcurrentDictionary<(int TenantId, int InstanceId), TenantWindow> _windows = new();

    /// <param name="utcNow">Clock seam (defaults to <see cref="DateTime.UtcNow"/>); injected in tests for deterministic cooldown checks without real sleeps.</param>
    public RateLimiter(int defaultMessagesPerMinute, JsonLinesLogger logger, Func<DateTime>? utcNow = null)
    {
        _defaultMessagesPerMinute = defaultMessagesPerMinute;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Bridge (tenant-only) path — unchanged semantics, keyed (tenant, instance=0).
    /// Returns true if within rate limit, false if it should wait.
    /// </summary>
    public bool TryAcquire(int tenantId) => TryAcquire(tenantId, 0);

    /// <summary>
    /// cxapi path — per (tenant, WapCRM instance). Returns false while an instance cooldown
    /// (from a 301/302) is active OR the sliding-window limit is hit.
    /// </summary>
    public bool TryAcquire(int tenantId, int instanceId)
    {
        var window = _windows.GetOrAdd((tenantId, instanceId), _ => new TenantWindow());
        return window.TryAcquire(_defaultMessagesPerMinute, _utcNow());
    }

    /// <summary>
    /// Block (tenant, instance) acquisitions until <paramref name="cooldown"/> elapses — used after a
    /// cxapi 301/302 rate-limit (honoring Retry-After). A non-positive cooldown is a no-op.
    /// </summary>
    public void ApplyCooldown(int tenantId, int instanceId, TimeSpan cooldown)
    {
        if (cooldown <= TimeSpan.Zero) return;
        var window = _windows.GetOrAdd((tenantId, instanceId), _ => new TenantWindow());
        window.ApplyCooldown(_utcNow() + cooldown);
    }

    /// <summary>How many messages this tenant (bridge window, instance 0) has sent in the current minute.</summary>
    public int GetCurrentCount(int tenantId)
    {
        if (_windows.TryGetValue((tenantId, 0), out var window))
            return window.GetCurrentCount(_utcNow());
        return 0;
    }

    private sealed class TenantWindow
    {
        private readonly object _lock = new();
        private readonly Queue<DateTime> _timestamps = new();
        private DateTime _cooldownUntil = DateTime.MinValue;

        public bool TryAcquire(int maxPerMinute, DateTime now)
        {
            lock (_lock)
            {
                if (now < _cooldownUntil)
                    return false;

                PurgeOld(now);
                if (_timestamps.Count >= maxPerMinute)
                    return false;

                _timestamps.Enqueue(now);
                return true;
            }
        }

        public void ApplyCooldown(DateTime until)
        {
            lock (_lock)
            {
                if (until > _cooldownUntil)
                    _cooldownUntil = until;
            }
        }

        public int GetCurrentCount(DateTime now)
        {
            lock (_lock)
            {
                PurgeOld(now);
                return _timestamps.Count;
            }
        }

        private void PurgeOld(DateTime now)
        {
            var cutoff = now.AddMinutes(-1);
            while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                _timestamps.Dequeue();
        }
    }
}
