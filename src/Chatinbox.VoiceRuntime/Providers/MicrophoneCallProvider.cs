using System.Threading.Channels;
using Chatinbox.Shared.Contracts.Voice;
using Chatinbox.Shared.Logging;

namespace Chatinbox.VoiceRuntime.Providers;

/// <summary>
/// F0 PoC provider — browser WebRTC microphone → VoiceRuntime WS bridge.
/// MicrophoneCallSession is a duplex pipe; the WS endpoint pushes inbound frames and
/// pulls outbound frames. The orchestrator (VoicePocEndpoints) bridges this with Realtime API.
///
/// AD-17: Microphone provider mirrors the Toniva gRPC framing (Opus 48kHz mono 20ms),
/// so the same orchestration code path serves both F0 (browser) and F2 (PBX).
/// </summary>
public sealed class MicrophoneCallProvider : IVoiceCallProvider
{
    private readonly JsonLinesLogger _logger;

    public MicrophoneCallProvider(JsonLinesLogger logger) => _logger = logger;

    public VoiceCallProviderKind Kind => VoiceCallProviderKind.Microphone;

    public Task<IVoiceCallSession> OpenSessionAsync(VoiceCallDescriptor descriptor, CancellationToken ct)
    {
        _logger.SystemInfo($"[MicrophoneCallProvider] Opening session {descriptor.SessionId} (tenant={descriptor.TenantId}, locale={descriptor.Locale})");
        IVoiceCallSession session = new MicrophoneCallSession(descriptor, _logger);
        return Task.FromResult(session);
    }
}

internal sealed class MicrophoneCallSession : IVoiceCallSession
{
    private readonly VoiceCallDescriptor _descriptor;
    private readonly JsonLinesLogger _logger;
    private readonly Channel<OpusFrame> _incoming;
    private readonly Channel<OpusFrame> _outgoing;
    private bool _disposed;

    public string SessionId => _descriptor.SessionId;

    public IAsyncEnumerable<OpusFrame> IncomingFrames => _incoming.Reader.ReadAllAsync();

    /// <summary>
    /// Internal: WS handler pulls outbound frames to deliver to browser.
    /// </summary>
    internal IAsyncEnumerable<OpusFrame> OutgoingFrames => _outgoing.Reader.ReadAllAsync();

    public MicrophoneCallSession(VoiceCallDescriptor descriptor, JsonLinesLogger logger)
    {
        _descriptor = descriptor;
        _logger = logger;
        _incoming = Channel.CreateBounded<OpusFrame>(new BoundedChannelOptions(200) // ~4 sec @ 20ms
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _outgoing = Channel.CreateBounded<OpusFrame>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>
    /// WS handler invokes this when a frame arrives from the browser.
    /// </summary>
    internal ValueTask PushIncomingAsync(OpusFrame frame, CancellationToken ct)
        => _incoming.Writer.WriteAsync(frame, ct);

    public ValueTask SendOutgoingFrameAsync(OpusFrame frame, CancellationToken ct)
        => _outgoing.Writer.WriteAsync(frame, ct);

    public ValueTask SignalBargeInAsync(CancellationToken ct)
    {
        // Drain pending outbound frames so the browser stops playing the previous response.
        while (_outgoing.Reader.TryRead(out _)) { }
        _logger.SystemInfo($"[MicrophoneCallSession/{SessionId}] barge-in signaled (outbound drained)");
        return ValueTask.CompletedTask;
    }

    public ValueTask TransferToHumanAsync(TransferRequest request, CancellationToken ct)
    {
        // F0: no-op (no PBX behind this provider). Logged for parity with F2 Toniva.
        _logger.SystemInfo($"[MicrophoneCallSession/{SessionId}] TransferToHuman called (target={request.Target}) — F0 no-op");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _incoming.Writer.TryComplete();
        _outgoing.Writer.TryComplete();
        _logger.SystemInfo($"[MicrophoneCallSession/{SessionId}] disposed");
        return ValueTask.CompletedTask;
    }
}
