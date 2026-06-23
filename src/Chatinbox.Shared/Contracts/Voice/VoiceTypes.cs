namespace Chatinbox.Shared.Contracts.Voice;

/// <summary>
/// Provider kind enum (FEAT-VFB).
/// </summary>
public enum VoiceCallProviderKind
{
    Microphone = 1,    // F0: Browser WebRTC test
    TonivaPbx = 2,     // F2: gRPC bidi from C++ PJSIP hook
    WhatsAppCall = 3,  // F3: Meta Cloud Calling API
    Mock = 4           // F2: WAV replay for integration tests
}

/// <summary>
/// Inbound call descriptor (provider-agnostic). Carries enough context to start a flow (F2).
/// F0: minimal — sessionId + tenantId placeholder + locale.
/// </summary>
public sealed record VoiceCallDescriptor(
    string SessionId,
    int TenantId,
    string Locale,                 // BCP-47 e.g. "tr-TR"
    string? CallerIdHash,          // SHA256(phone) — KVKK pseudonymization, F2'de hash
    DateTimeOffset StartedAt,
    Dictionary<string, string>? ProviderMetadata = null
);

/// <summary>
/// Opus-encoded 20ms audio frame. Codec: Opus 48kHz mono, frame size 960 samples.
/// Payload is raw Opus packet bytes (no container, no RTP header).
/// </summary>
public sealed record OpusFrame(
    ReadOnlyMemory<byte> Payload,
    long TimestampMs,
    int SequenceNumber
);

/// <summary>
/// Human transfer request (F2 voice_transfer node). F0: not used.
/// </summary>
public sealed record TransferRequest(
    string Target,                 // "queue:sales" | "extension:101" | "team:dent_eu"
    string? WarmHandoffText,       // Optional TTS announcement before transfer
    bool FallbackCallbackOnQueueFull = true
);

/// <summary>
/// Latency measurement event emitted by VoiceRuntime LatencyTracker.
/// jsonl per-event + rolling window for /metrics endpoint.
/// </summary>
public sealed record LatencyEvent(
    string SessionId,
    int TurnNumber,
    DateTimeOffset Timestamp,
    LatencyEventKind Kind,
    int ElapsedMs,
    Dictionary<string, string>? Tags = null
);

public enum LatencyEventKind
{
    UserSpeechStart = 1,           // VAD detects speech-on
    UserSpeechEnd = 2,             // VAD detects 200ms silence
    SemanticEotConfirmed = 3,      // F2: Smart-Turn v2 confirms turn ended
    RealtimeRequestSent = 4,       // VoiceRuntime sent response.create to Realtime
    RealtimeFirstByteReceived = 5, // First response.audio.delta from Realtime
    TtsFirstByteToUser = 6,        // First Opus frame delivered to user (browser/PBX)
    BargeInDetected = 7,           // VAD detects user speech while TTS playing
    BargeInTtsStopped = 8,         // Realtime response.cancel acknowledged
    SessionEnd = 9
}
