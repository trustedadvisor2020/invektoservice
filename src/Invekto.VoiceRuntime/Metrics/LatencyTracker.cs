using System.Collections.Concurrent;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Voice;
using Invekto.Shared.Logging;

namespace Invekto.VoiceRuntime.Metrics;

/// <summary>
/// Latency measurement for F0 PoC.
/// Per-session turn timing + rolling window p50/p95/p99 for /metrics endpoint.
///
/// AC4: 10 senaryo × 3 round → arch/reports/feat-vfb-f0-latency-report.md.
/// </summary>
public sealed class LatencyTracker
{
    private readonly JsonLinesLogger _logger;
    private readonly int _rollingWindowSize;
    private readonly int _warnP95ThresholdMs;
    private readonly ConcurrentQueue<int> _firstByteRollingWindow = new();
    private readonly ConcurrentQueue<int> _bargeInRollingWindow = new();
    private readonly object _windowLock = new();

    public LatencyTracker(JsonLinesLogger logger, IConfiguration config)
    {
        _logger = logger;
        _rollingWindowSize = config.GetValue<int>("Latency:RollingWindowSize", 100);
        _warnP95ThresholdMs = config.GetValue<int>("Latency:WarnP95ThresholdMs", 1500);
    }

    /// <summary>
    /// Create per-session turn timing state. Reset whenever a new call starts.
    /// </summary>
    public TurnTiming CreateTurnTiming(string sessionId) => new TurnTiming(sessionId);

    /// <summary>
    /// Record a latency event into rolling windows + jsonl log.
    /// </summary>
    public void RecordEvent(LatencyEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        _logger.SystemInfo($"[Latency] {json}");

        if (evt.Kind == LatencyEventKind.TtsFirstByteToUser)
        {
            UpdateWindow(_firstByteRollingWindow, evt.ElapsedMs);
            CheckP95Warning(_firstByteRollingWindow, "first_byte");
        }
        else if (evt.Kind == LatencyEventKind.BargeInTtsStopped)
        {
            UpdateWindow(_bargeInRollingWindow, evt.ElapsedMs);
            CheckP95Warning(_bargeInRollingWindow, "barge_in");
        }
    }

    private void UpdateWindow(ConcurrentQueue<int> window, int ms)
    {
        window.Enqueue(ms);
        lock (_windowLock)
        {
            while (window.Count > _rollingWindowSize && window.TryDequeue(out _)) { }
        }
    }

    private void CheckP95Warning(ConcurrentQueue<int> window, string label)
    {
        var p95 = Percentile(window, 0.95);
        if (p95 > _warnP95ThresholdMs)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeLatencyBudgetExceeded}] [Latency] {label} p95={p95}ms exceeds threshold {_warnP95ThresholdMs}ms (window n={window.Count})");
        }
    }

    public LatencyStats Snapshot()
    {
        var fb = _firstByteRollingWindow.ToArray();
        var bi = _bargeInRollingWindow.ToArray();
        return new LatencyStats(
            FirstByte: ComputeStats(fb),
            BargeIn: ComputeStats(bi));
    }

    private static StatBucket ComputeStats(int[] samples)
    {
        if (samples.Length == 0)
            return new StatBucket(0, 0, 0, 0, 0);
        return new StatBucket(
            Count: samples.Length,
            P50: Percentile(samples, 0.50),
            P95: Percentile(samples, 0.95),
            P99: Percentile(samples, 0.99),
            Max: samples.Max());
    }

    private static int Percentile(IEnumerable<int> source, double p)
    {
        var arr = source.ToArray();
        if (arr.Length == 0) return 0;
        Array.Sort(arr);
        var idx = (int)Math.Ceiling(p * arr.Length) - 1;
        idx = Math.Clamp(idx, 0, arr.Length - 1);
        return arr[idx];
    }
}

/// <summary>
/// Per-session turn timing — caller stamps key moments and computes deltas.
/// </summary>
public sealed class TurnTiming
{
    public string SessionId { get; }
    public int TurnNumber { get; private set; }
    private DateTimeOffset? _userSpeechStartAt;
    private DateTimeOffset? _userSpeechEndAt;
    private DateTimeOffset? _realtimeRequestSentAt;
    private DateTimeOffset? _firstAudioByteAt;
    private DateTimeOffset? _bargeInDetectedAt;

    public TurnTiming(string sessionId)
    {
        SessionId = sessionId;
        TurnNumber = 0;
    }

    public LatencyEvent StampSpeechStart()
    {
        TurnNumber++;
        _userSpeechStartAt = DateTimeOffset.UtcNow;
        _userSpeechEndAt = null;
        _realtimeRequestSentAt = null;
        _firstAudioByteAt = null;
        _bargeInDetectedAt = null;
        return new LatencyEvent(SessionId, TurnNumber, _userSpeechStartAt.Value, LatencyEventKind.UserSpeechStart, 0);
    }

    public LatencyEvent StampSpeechEnd()
    {
        _userSpeechEndAt = DateTimeOffset.UtcNow;
        var elapsed = _userSpeechStartAt is null ? 0 : (int)(_userSpeechEndAt.Value - _userSpeechStartAt.Value).TotalMilliseconds;
        return new LatencyEvent(SessionId, TurnNumber, _userSpeechEndAt.Value, LatencyEventKind.UserSpeechEnd, elapsed);
    }

    public LatencyEvent StampRealtimeRequestSent()
    {
        _realtimeRequestSentAt = DateTimeOffset.UtcNow;
        var elapsed = _userSpeechEndAt is null ? 0 : (int)(_realtimeRequestSentAt.Value - _userSpeechEndAt.Value).TotalMilliseconds;
        return new LatencyEvent(SessionId, TurnNumber, _realtimeRequestSentAt.Value, LatencyEventKind.RealtimeRequestSent, elapsed);
    }

    public LatencyEvent StampRealtimeFirstByte()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = _realtimeRequestSentAt is null ? 0 : (int)(now - _realtimeRequestSentAt.Value).TotalMilliseconds;
        return new LatencyEvent(SessionId, TurnNumber, now, LatencyEventKind.RealtimeFirstByteReceived, elapsed);
    }

    public LatencyEvent StampTtsFirstByteToUser()
    {
        _firstAudioByteAt = DateTimeOffset.UtcNow;
        var elapsed = _userSpeechEndAt is null ? 0 : (int)(_firstAudioByteAt.Value - _userSpeechEndAt.Value).TotalMilliseconds;
        return new LatencyEvent(SessionId, TurnNumber, _firstAudioByteAt.Value, LatencyEventKind.TtsFirstByteToUser, elapsed);
    }

    public LatencyEvent StampBargeInDetected()
    {
        _bargeInDetectedAt = DateTimeOffset.UtcNow;
        return new LatencyEvent(SessionId, TurnNumber, _bargeInDetectedAt.Value, LatencyEventKind.BargeInDetected, 0);
    }

    public LatencyEvent StampBargeInTtsStopped()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = _bargeInDetectedAt is null ? 0 : (int)(now - _bargeInDetectedAt.Value).TotalMilliseconds;
        return new LatencyEvent(SessionId, TurnNumber, now, LatencyEventKind.BargeInTtsStopped, elapsed);
    }
}

public sealed record LatencyStats(StatBucket FirstByte, StatBucket BargeIn);
public sealed record StatBucket(int Count, int P50, int P95, int P99, int Max);
