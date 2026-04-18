using System.Collections.Concurrent;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW: per-API-key sliding-window limiter (100 req/60s default). In-memory
/// only; v2 switches to a Redis-backed counter once Backend scales horizontally.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/> + per-bucket lock.
///
/// Memory-safety for a public endpoint: the intake path rate-limits on the RAW
/// api key before any DB lookup (brute-force shield), so invalid/random keys do
/// hit the dictionary. Two guards keep memory bounded:
///   (1) <c>MaxTrackedKeys</c> cap — once reached, new unknown keys are denied
///       with a full-window retry-after (no bucket allocated). The cap check is
///       serialized via <c>lock (_buckets)</c> + double-checked insert so
///       concurrent traffic cannot overshoot.
///   (2) <see cref="Sweep"/> / <see cref="SweepNow"/> removes drained buckets.
///       Program.cs registers <see cref="SweepNow"/> as a Hangfire recurring
///       job (every 5 minutes) so brute-force dictionary inflation can only
///       persist for up to one sweep interval before reclaim.
/// </summary>
public sealed class ApiKeyRateLimiter
{
    /// <summary>Soft cap on distinct keys tracked at once. 10k = ~320KB at 100 DateTime/bucket.</summary>
    public const int MaxTrackedKeys = 10_000;

    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _buckets = new();

    public ApiKeyRateLimiter(int limit = 100, int windowSeconds = 60)
    {
        _limit = limit;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    /// <summary>
    /// Records an attempt against <paramref name="apiKey"/> at <paramref name="now"/>
    /// and returns the result. When <see cref="RateLimitCheck.Allowed"/> is false,
    /// <see cref="RateLimitCheck.RetryAfterSeconds"/> is the clamped wait (&gt;=1s,
    /// &lt;= window) until the oldest in-window request ages out.
    /// </summary>
    public RateLimitCheck TryAcquire(string apiKey, DateTime now)
    {
        // When the dictionary is saturated, refuse to allocate new buckets. Existing
        // known-key buckets still work (they resolve via TryGetValue below); only
        // unseen keys are denied until the window drains bucket slots.
        //
        // Admission is serialized on the dictionary itself so the Count check and
        // the insert form one atomic decision — a ConcurrentDictionary without
        // this lock can let two threads both see Count<cap and both insert,
        // overshooting the cap by the concurrent thread count. Hot path (known
        // key) skips this lock and uses the per-bucket lock below.
        if (!_buckets.TryGetValue(apiKey, out var bucket))
        {
            lock (_buckets)
            {
                if (!_buckets.TryGetValue(apiKey, out bucket))
                {
                    if (_buckets.Count >= MaxTrackedKeys)
                        return new RateLimitCheck(false, (int)_window.TotalSeconds);

                    bucket = new Queue<DateTime>();
                    _buckets[apiKey] = bucket;
                }
            }
        }

        var cutoff = now - _window;

        lock (bucket)
        {
            while (bucket.Count > 0 && bucket.Peek() <= cutoff)
                bucket.Dequeue();

            if (bucket.Count >= _limit)
            {
                var oldest = bucket.Peek();
                var retryAfter = (oldest + _window) - now;
                var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                if (seconds < 1) seconds = 1;
                if (seconds > (int)_window.TotalSeconds) seconds = (int)_window.TotalSeconds;
                return new RateLimitCheck(false, seconds);
            }

            bucket.Enqueue(now);
        }

        return new RateLimitCheck(true, 0);
    }

    /// <summary>
    /// Hangfire-callable entry that calls <see cref="Sweep"/> with wall-clock
    /// UtcNow. Kept parameterless so RecurringJob.AddOrUpdate can target it
    /// without serialization gymnastics around DateTime.
    /// </summary>
    public void SweepNow() => Sweep(DateTime.UtcNow);

    /// <summary>
    /// Sweeps expired buckets. O(Count) scan; intended to be called from a low-
    /// frequency maintenance path (Hangfire recurring job wired in Program.cs)
    /// so the hot <see cref="TryAcquire"/> path stays lock-light. Returns the
    /// number of buckets removed for observability.
    /// </summary>
    public int Sweep(DateTime now)
    {
        var cutoff = now - _window;
        var removed = 0;
        foreach (var kvp in _buckets)
        {
            var bucket = kvp.Value;
            lock (bucket)
            {
                while (bucket.Count > 0 && bucket.Peek() <= cutoff)
                    bucket.Dequeue();

                if (bucket.Count == 0)
                {
                    // Defensive: ConcurrentDictionary.TryRemove with matching value
                    // avoids racing with a concurrent re-entry that swapped the bucket.
                    if (((ICollection<KeyValuePair<string, Queue<DateTime>>>)_buckets)
                        .Remove(kvp))
                        removed++;
                }
            }
        }
        return removed;
    }
}

public readonly record struct RateLimitCheck(bool Allowed, int RetryAfterSeconds);
