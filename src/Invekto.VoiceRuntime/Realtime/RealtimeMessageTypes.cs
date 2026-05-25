using System.Text.Json;
using System.Text.Json.Serialization;

namespace Invekto.VoiceRuntime.Realtime;

/// <summary>
/// OpenAI Realtime API event payloads (subset used in F0 PoC).
/// Reference: https://platform.openai.com/docs/api-reference/realtime
///
/// Naming follows OpenAI's snake_case envelope (System.Text.Json with JsonPropertyName).
/// </summary>
public sealed record RealtimeEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string? EventId = null
);

// ── Client → Server events ───────────────────────────────────────────

public sealed record SessionUpdateEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("session")] SessionConfig Session
)
{
    public static SessionUpdateEvent Create(SessionConfig session) =>
        new("session.update", session);
}

public sealed record SessionConfig(
    // GA migration (2026-05-07):
    // - "type" required = "realtime" (speech-to-speech) or "transcription"
    // - "modalities" REMOVED (GA auto-emits audio+text for realtime sessions)
    // - "voice"/"input_audio_format"/"output_audio_format"/"input_audio_transcription"/"turn_detection"
    //   all moved UNDER session.audio.{input,output} nested objects
    // - "temperature" + "max_response_output_tokens" REMOVED from session level
    //   (GA: pass these per-response via response.create event if needed; F0 default behavior)
    //
    // F0.5 Chunk C (AD-23): optional Tools array enables function calling. When null the GA wire
    // payload omits the field entirely (DefaultIgnoreCondition.WhenWritingNull on serializer), so
    // F0 legacy sessions keep their original shape — only F0.5 mode sends tools[].
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("instructions")] string Instructions,
    [property: JsonPropertyName("audio")] SessionAudioConfig Audio,
    [property: JsonPropertyName("tools")] IReadOnlyList<ToolDefinition>? Tools = null
);

/// <summary>
/// OpenAI Realtime GA function tool definition (sent inside session.update.tools[]).
/// Names MUST be snake_case (OpenAI validation). Description is English-only (AD-23: model alignment).
/// Parameters is a JSON Schema object (kept as JsonElement so the caller can build the schema in C#
/// or load it from disk without forcing a record-per-tool here).
/// </summary>
public sealed record ToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters
)
{
    public static ToolDefinition Function(string name, string description, JsonElement parameters) =>
        new("function", name, description, parameters);
}

public sealed record SessionAudioConfig(
    [property: JsonPropertyName("input")] SessionAudioInputConfig Input,
    [property: JsonPropertyName("output")] SessionAudioOutputConfig Output
);

public sealed record SessionAudioInputConfig(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("transcription")] InputAudioTranscriptionConfig? Transcription,
    [property: JsonPropertyName("turn_detection")] TurnDetectionConfig? TurnDetection
);

public sealed record SessionAudioOutputConfig(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("voice")] string Voice
);

public sealed record InputAudioTranscriptionConfig(
    [property: JsonPropertyName("model")] string Model = "whisper-1",
    [property: JsonPropertyName("language")] string? Language = null
);

public sealed record TurnDetectionConfig(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("eagerness")] string? Eagerness = null,
    [property: JsonPropertyName("threshold")] double? Threshold = null,
    [property: JsonPropertyName("prefix_padding_ms")] int? PrefixPaddingMs = null,
    [property: JsonPropertyName("silence_duration_ms")] int? SilenceDurationMs = null,
    [property: JsonPropertyName("create_response")] bool? CreateResponse = null
);

public sealed record InputAudioBufferAppendEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("audio")] string AudioBase64
)
{
    public static InputAudioBufferAppendEvent Create(string base64Pcm16) =>
        new("input_audio_buffer.append", base64Pcm16);
}

public sealed record ResponseCancelEvent(
    [property: JsonPropertyName("type")] string Type
)
{
    public static ResponseCancelEvent Instance { get; } = new("response.cancel");
}

// F0.5 Chunk C — function calling outbound events ────────────────────

/// <summary>
/// `conversation.item.create` with a function_call_output item: returns tool execution result to
/// the model after ToolExecutor finishes (success or structured error JSON). Output field is the
/// raw JSON string the model will read — caller is responsible for serializing the payload.
/// </summary>
public sealed record ConversationItemCreateEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("item")] ConversationItem Item
)
{
    public static ConversationItemCreateEvent FunctionCallOutput(string callId, string outputJson) =>
        new("conversation.item.create", new ConversationItem(
            Type: "function_call_output",
            CallId: callId,
            Output: outputJson));
}

public sealed record ConversationItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("call_id")] string? CallId,
    [property: JsonPropertyName("output")] string? Output
);

/// <summary>
/// `response.create` triggers the model to resume after a function_call_output item is appended.
/// GA accepts an optional `response` config override; F0.5 sends the bare event (model continues
/// with the active session.update settings).
/// </summary>
public sealed record ResponseCreateEvent(
    [property: JsonPropertyName("type")] string Type
)
{
    public static ResponseCreateEvent Instance { get; } = new("response.create");
}

// ── Server → Client events (parsed via Type discriminator) ───────────

public sealed record SessionCreatedEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("session")] SessionInfo Session
);

public sealed record SessionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("model")] string Model
);

public sealed record InputAudioBufferSpeechStartedEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("audio_start_ms")] long AudioStartMs,
    [property: JsonPropertyName("item_id")] string? ItemId
);

public sealed record InputAudioBufferSpeechStoppedEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("audio_end_ms")] long AudioEndMs,
    [property: JsonPropertyName("item_id")] string? ItemId
);

public sealed record ResponseAudioDeltaEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("response_id")] string ResponseId,
    [property: JsonPropertyName("delta")] string DeltaBase64
);

public sealed record ResponseAudioTranscriptDeltaEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("response_id")] string ResponseId,
    [property: JsonPropertyName("delta")] string Delta
);

public sealed record InputAudioTranscriptionCompletedEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("transcript")] string Transcript
);

public sealed record ResponseDoneEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("response_id")] string ResponseId
);

// F0.5 Chunk C — function calling inbound events ─────────────────────

/// <summary>
/// `response.function_call_arguments.delta` — streaming JSON args fragment. Multiple deltas with
/// the same call_id accumulate into the final args string. ToolExecutor buffers per call_id
/// (AD-31 ConcurrentDictionary defense) until the matching .done arrives.
/// </summary>
public sealed record ResponseFunctionCallArgumentsDeltaEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("response_id")] string? ResponseId,
    [property: JsonPropertyName("item_id")] string? ItemId,
    [property: JsonPropertyName("output_index")] int? OutputIndex,
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("delta")] string Delta
);

/// <summary>
/// `response.function_call_arguments.done` — final args + tool name. Triggers ToolExecutor.
/// `arguments` is the COMPLETE JSON string (model may also emit it incrementally via deltas; both
/// paths must agree — see AD-31 buffer invariant).
/// </summary>
public sealed record ResponseFunctionCallArgumentsDoneEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("response_id")] string? ResponseId,
    [property: JsonPropertyName("item_id")] string? ItemId,
    [property: JsonPropertyName("output_index")] int? OutputIndex,
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments
);

public sealed record RealtimeErrorEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("error")] RealtimeErrorDetails Error
);

public sealed record RealtimeErrorDetails(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("param")] string? Param,
    [property: JsonPropertyName("event_id")] string? EventId
);
