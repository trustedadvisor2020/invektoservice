namespace Chatinbox.Shared.Contracts.Voice;

/// <summary>
/// Abstraction for live voice call providers (FEAT-VFB).
/// F0: MicrophoneCallProvider (browser WebRTC) only.
/// F2: TonivaPbxProvider (gRPC bidi from C++ PJSIP hook) + MockProvider added.
/// F3: WhatsAppCallProvider (Meta Cloud Calling API webhook) added.
///
/// Each provider streams 48kHz mono Opus 20ms frames in both directions.
/// VoiceRuntime is provider-agnostic: same FlowEngineV2 nodes (F2) work across all channels.
/// </summary>
public interface IVoiceCallProvider
{
    /// <summary>
    /// Provider kind tag for routing/logging. Persisted in call_sessions.provider (F2).
    /// </summary>
    VoiceCallProviderKind Kind { get; }

    /// <summary>
    /// Open a new live voice session for the given call descriptor.
    /// Returns a session handle which yields incoming audio frames and accepts outgoing audio frames.
    /// </summary>
    Task<IVoiceCallSession> OpenSessionAsync(VoiceCallDescriptor descriptor, CancellationToken ct);
}

/// <summary>
/// Active voice call session. Bidirectional audio stream + signaling.
/// Lifecycle: OpenSessionAsync → IncomingFrames consumed + SendOutgoingFrameAsync called → DisposeAsync.
/// </summary>
public interface IVoiceCallSession : IAsyncDisposable
{
    /// <summary>
    /// Unique session id (also used as call_sessions.id in F2 DB).
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Async stream of inbound Opus 20ms frames from the user side.
    /// Provider-specific: browser mic, Toniva RTP, WhatsApp Cloud media WS.
    /// </summary>
    IAsyncEnumerable<OpusFrame> IncomingFrames { get; }

    /// <summary>
    /// Send an outbound Opus 20ms frame (bot voice) to the user.
    /// </summary>
    ValueTask SendOutgoingFrameAsync(OpusFrame frame, CancellationToken ct);

    /// <summary>
    /// Signal a barge-in event to the provider (e.g., browser flushes pending playback queue).
    /// Realtime API gets `response.cancel` separately by VoiceRuntime.
    /// </summary>
    ValueTask SignalBargeInAsync(CancellationToken ct);

    /// <summary>
    /// Request human transfer (F2: `voice_transfer` node). F0 no-op.
    /// </summary>
    ValueTask TransferToHumanAsync(TransferRequest request, CancellationToken ct);
}
