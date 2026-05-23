namespace Invekto.VoiceRuntime.Realtime;

/// <summary>
/// Factory + per-call config snapshot for OpenAI Realtime sessions.
/// Reads from appsettings.OpenAI section, produces a SessionConfig payload.
/// </summary>
public sealed class RealtimeSessionFactory
{
    public string Endpoint { get; }
    public string Model { get; }
    public string ApiKey { get; }
    public SessionConfig DefaultConfig { get; }
    public int MaxConcurrentSessions { get; }

    public RealtimeSessionFactory(IConfiguration config)
    {
        Endpoint = config["OpenAI:RealtimeEndpoint"] ?? "wss://api.openai.com/v1/realtime";
        Model = config["OpenAI:RealtimeModel"] ?? "gpt-4o-realtime-preview";

        // AD-20: API key environment-variable-ONLY policy. No appsettings fallback (plaintext
        // key in config files is FORBIDDEN at every stage — F0/F2/F3/F4). Empty key is allowed
        // at boot (deferred fail to first session via INV-VR-002), not at runtime.
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        MaxConcurrentSessions = config.GetValue<int>("OpenAI:MaxConcurrentSessions", 3);

        var voice = config["OpenAI:Voice"] ?? "alloy";
        var inFmt = config["OpenAI:InputAudioFormat"] ?? "pcm16";
        var outFmt = config["OpenAI:OutputAudioFormat"] ?? "pcm16";
        var instructions = config["OpenAI:Instructions"]
            ?? "Sen Invekto'nun Türkçe sesli AI asistanısın. Kısa, doğal, samimi konuşursun.";

        var turnDetType = config["OpenAI:TurnDetection:Type"] ?? "semantic_vad";
        var eagerness = config["OpenAI:TurnDetection:Eagerness"];

        var turnDetection = turnDetType switch
        {
            "none" => null,
            "semantic_vad" => new TurnDetectionConfig(
                Type: "semantic_vad",
                Eagerness: eagerness ?? "medium",
                CreateResponse: true),
            _ /* "server_vad" */ => new TurnDetectionConfig(
                Type: "server_vad",
                Threshold: 0.5,
                PrefixPaddingMs: 300,
                SilenceDurationMs: 500,
                CreateResponse: true)
        };

        DefaultConfig = new SessionConfig(
            Modalities: new[] { "audio", "text" },
            Instructions: instructions,
            Voice: voice,
            InputAudioFormat: inFmt,
            OutputAudioFormat: outFmt,
            InputAudioTranscription: new InputAudioTranscriptionConfig("whisper-1"),
            TurnDetection: turnDetection,
            Temperature: 0.8,
            MaxResponseOutputTokens: "inf"
        );
    }
}
