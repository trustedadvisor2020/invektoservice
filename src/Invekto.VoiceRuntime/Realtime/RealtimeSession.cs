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
        // GA migration (2026-05-07): gpt-4o-realtime-preview → gpt-realtime (GA production model).
        // Cheaper alternative: gpt-realtime-mini. Override via appsettings OpenAI:RealtimeModel.
        Model = config["OpenAI:RealtimeModel"] ?? "gpt-realtime";

        // AD-20: API key environment-variable-ONLY policy. No appsettings fallback (plaintext
        // key in config files is FORBIDDEN at every stage — F0/F2/F3/F4). Empty key is allowed
        // at boot (deferred fail to first session via INV-VR-002), not at runtime.
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        MaxConcurrentSessions = config.GetValue<int>("OpenAI:MaxConcurrentSessions", 3);

        var voice = config["OpenAI:Voice"] ?? "alloy";
        var inFmt = MapAudioFormat(config["OpenAI:InputAudioFormat"] ?? "pcm16");
        var outFmt = MapAudioFormat(config["OpenAI:OutputAudioFormat"] ?? "pcm16");
        var instructions = config["OpenAI:Instructions"]
            ?? "Sen Invekto'nun TÜRKÇE sesli AI asistanısın. ÇOK ÖNEMLİ: HER ZAMAN ve SADECE Türkçe konuş — başka hiçbir dilde (İngilizce, Korece, Almanca, vb.) cevap verme, kullanıcı başka dilde konuşsa bile Türkçe yanıtla. Kısa, doğal, samimi konuş. Anlamadığında 'Tekrar eder misiniz?' de.";

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

        // GA migration: voice/format/transcription/turn_detection all live under session.audio.{input,output}
        DefaultConfig = new SessionConfig(
            Type: "realtime",
            Model: Model,
            Instructions: instructions,
            Audio: new SessionAudioConfig(
                Input: new SessionAudioInputConfig(
                    Format: inFmt,
                    // Whisper language=tr hard hint — Türkçe kısa cümlede auto-detect Korece/Çince/İspanyolca sanmasını önler.
                    Transcription: new InputAudioTranscriptionConfig("whisper-1", Language: "tr"),
                    TurnDetection: turnDetection
                ),
                Output: new SessionAudioOutputConfig(
                    Format: outFmt,
                    Voice: voice
                )
            )
        );
    }

    // GA Realtime API expects session.audio.{input,output}.format as an object { type, rate },
    // not the legacy "pcm16" string. Map the appsettings codec id (kept for backward-compat) to
    // the GA object. pcm16 is 16-bit little-endian PCM at 24 kHz; G.711 variants are 8 kHz.
    private static AudioFormatConfig MapAudioFormat(string codec) => codec switch
    {
        "pcm16" => new AudioFormatConfig("audio/pcm", 24000),
        "g711_ulaw" => new AudioFormatConfig("audio/pcmu", 8000),
        "g711_alaw" => new AudioFormatConfig("audio/pcma", 8000),
        _ => new AudioFormatConfig("audio/pcm", 24000)
    };
}
