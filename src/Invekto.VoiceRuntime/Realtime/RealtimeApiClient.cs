using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.VoiceRuntime.Realtime;

/// <summary>
/// WebSocket client for OpenAI Realtime API.
/// Wraps ClientWebSocket with auth headers, bidirectional event channels, and typed dispatch.
///
/// One instance per voice call (per-session).
///
/// Lifecycle: ConnectAsync → SendSessionUpdate → background loops run → SendAudio/SendCancel/RegisterHandlers
///            → DisposeAsync (closes WS + cancels loops).
/// </summary>
public sealed class RealtimeApiClient : IAsyncDisposable
{
    // OpenAI Realtime API moved to GA on 2026-05-07; the OpenAI-Beta header is no longer
    // sent. Authorization Bearer is the only required header now.

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly JsonLinesLogger _logger;
    private readonly string _sessionTag;

    private ClientWebSocket? _ws;
    private Channel<string>? _outgoing;
    private CancellationTokenSource? _loopCts;
    private Task? _sendLoopTask;
    private Task? _recvLoopTask;

    public event Action<InputAudioBufferSpeechStartedEvent>? OnSpeechStarted;
    public event Action<InputAudioBufferSpeechStoppedEvent>? OnSpeechStopped;
    public event Action<ResponseAudioDeltaEvent>? OnAudioDelta;
    public event Action<ResponseAudioTranscriptDeltaEvent>? OnAudioTranscriptDelta;
    public event Action<InputAudioTranscriptionCompletedEvent>? OnUserTranscriptCompleted;
    public event Action<ResponseDoneEvent>? OnResponseDone;
    public event Action<RealtimeErrorEvent>? OnRealtimeError;
    public event Action<string>? OnUnknownEvent;

    public RealtimeApiClient(string endpoint, string apiKey, string model, string sessionTag, JsonLinesLogger logger)
    {
        _endpoint = new Uri($"{endpoint}?model={Uri.EscapeDataString(model)}");
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _sessionTag = sessionTag;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeAuthFailed}] [Realtime/{_sessionTag}] OPENAI_API_KEY missing. Set OPENAI_API_KEY environment variable.");
            throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeRealtimeAuthFailed}: OPENAI_API_KEY not configured");
        }

        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        // GA migration (2026-05-07): no OpenAI-Beta header — Authorization Bearer only.

        try
        {
            await _ws.ConnectAsync(_endpoint, ct);
        }
        catch (WebSocketException ex)
        {
            // Status 401 → auth; 429 → rate limit; else → generic connection failure
            var errorCode = ex.WebSocketErrorCode switch
            {
                WebSocketError.NotAWebSocket => ErrorCodes.VoiceRuntimeRealtimeAuthFailed,
                _ => ErrorCodes.VoiceRuntimeRealtimeConnectionFailed
            };
            _logger.SystemError($"[{errorCode}] [Realtime/{_sessionTag}] WS connect failed: {ex.WebSocketErrorCode} {ex.Message}");
            throw new InvalidOperationException($"{errorCode}: Realtime WS connect failed", ex);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] WS connect HTTP error: {ex.Message}");
            throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}: Realtime WS connect HTTP failure", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] WS connect invalid state: {ex.Message}");
            throw;
        }

        // Bounded outgoing channel: 1000 events ≈ 20s of audio-append (50fps) — enough for realistic backpressure
        // without unbounded memory growth if Realtime stalls. DropOldest preserves recency for live audio.
        _outgoing = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _sendLoopTask = Task.Run(() => SendLoopAsync(_loopCts.Token));
        _recvLoopTask = Task.Run(() => ReceiveLoopAsync(_loopCts.Token));

        _logger.SystemInfo($"[Realtime/{_sessionTag}] WS connected: {_endpoint}");
    }

    public ValueTask SendSessionUpdateAsync(SessionConfig config, CancellationToken ct)
    {
        var evt = SessionUpdateEvent.Create(config);
        return EnqueueEventAsync(evt, ct);
    }

    public ValueTask SendAudioAsync(string base64Pcm16, CancellationToken ct)
    {
        var evt = InputAudioBufferAppendEvent.Create(base64Pcm16);
        return EnqueueEventAsync(evt, ct);
    }

    public ValueTask SendResponseCancelAsync(CancellationToken ct)
    {
        return EnqueueEventAsync(ResponseCancelEvent.Instance, ct);
    }

    private async ValueTask EnqueueEventAsync<T>(T evt, CancellationToken ct)
    {
        if (_outgoing is null) throw new InvalidOperationException("ConnectAsync must be called first");
        var json = JsonSerializer.Serialize(evt, JsonOpts);
        await _outgoing.Writer.WriteAsync(json, ct);
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}: SendLoop started before ConnectAsync");
        var outgoing = _outgoing ?? throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}: SendLoop started before channel init");
        try
        {
            await foreach (var json in outgoing.Reader.ReadAllAsync(ct))
            {
                if (ws.State != WebSocketState.Open)
                {
                    _logger.SystemWarn($"[Realtime/{_sessionTag}] WS not open in SendLoop (state={ws.State}); dropping message");
                    break;
                }
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (WebSocketException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] SendLoop WS error: {ex.WebSocketErrorCode} {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // WS in invalid state (already closed / never opened)
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] SendLoop invalid state: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}: ReceiveLoop started before ConnectAsync");
        var buffer = new byte[64 * 1024];
        using var accumulator = new MemoryStream();

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                accumulator.SetLength(0);

                do
                {
                    result = await ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.SystemInfo($"[Realtime/{_sessionTag}] WS close received: {result.CloseStatus} {result.CloseStatusDescription}");
                        return;
                    }
                    accumulator.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(accumulator.ToArray());
                DispatchEvent(json);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (WebSocketException ex)
        {
            var errorCode = ex.WebSocketErrorCode == WebSocketError.NotAWebSocket
                ? ErrorCodes.VoiceRuntimeRealtimeAuthFailed
                : ErrorCodes.VoiceRuntimeRealtimeConnectionFailed;
            _logger.SystemError($"[{errorCode}] [Realtime/{_sessionTag}] ReceiveLoop WS error: {ex.WebSocketErrorCode} {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] ReceiveLoop invalid state: {ex.Message}");
        }
    }

    private void DispatchEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl))
            {
                OnUnknownEvent?.Invoke(json);
                return;
            }
            var type = typeEl.GetString() ?? "";

            switch (type)
            {
                case "input_audio_buffer.speech_started":
                    var ss = JsonSerializer.Deserialize<InputAudioBufferSpeechStartedEvent>(json, JsonOpts);
                    if (ss is not null) OnSpeechStarted?.Invoke(ss);
                    break;
                case "input_audio_buffer.speech_stopped":
                    var se = JsonSerializer.Deserialize<InputAudioBufferSpeechStoppedEvent>(json, JsonOpts);
                    if (se is not null) OnSpeechStopped?.Invoke(se);
                    break;
                // GA migration: response.audio.delta → response.output_audio.delta
                //               response.audio_transcript.delta → response.output_audio_transcript.delta
                case "response.output_audio.delta":
                case "response.audio.delta": // legacy fallback (older preview not yet disabled)
                    var ad = JsonSerializer.Deserialize<ResponseAudioDeltaEvent>(json, JsonOpts);
                    if (ad is not null) OnAudioDelta?.Invoke(ad);
                    break;
                case "response.output_audio_transcript.delta":
                case "response.audio_transcript.delta": // legacy fallback
                    var atd = JsonSerializer.Deserialize<ResponseAudioTranscriptDeltaEvent>(json, JsonOpts);
                    if (atd is not null) OnAudioTranscriptDelta?.Invoke(atd);
                    break;
                case "conversation.item.input_audio_transcription.completed":
                    var ut = JsonSerializer.Deserialize<InputAudioTranscriptionCompletedEvent>(json, JsonOpts);
                    if (ut is not null) OnUserTranscriptCompleted?.Invoke(ut);
                    break;
                case "response.done":
                    var rd = JsonSerializer.Deserialize<ResponseDoneEvent>(json, JsonOpts);
                    if (rd is not null) OnResponseDone?.Invoke(rd);
                    break;
                case "error":
                    var err = JsonSerializer.Deserialize<RealtimeErrorEvent>(json, JsonOpts);
                    if (err is not null)
                    {
                        _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [Realtime/{_sessionTag}] Server error: {err.Error.Code} {err.Error.Message}");
                        OnRealtimeError?.Invoke(err);
                    }
                    break;
                default:
                    OnUnknownEvent?.Invoke(json);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[Realtime/{_sessionTag}] Failed to parse incoming event: {ex.Message} | json[0..200]={Truncate(json, 200)}");
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public async ValueTask DisposeAsync()
    {
        try
        {
            _loopCts?.Cancel();
            _outgoing?.Writer.TryComplete();

            if (_ws is not null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "session end", CancellationToken.None);
                }
                catch (WebSocketException ex)
                {
                    _logger.SystemWarn($"[Realtime/{_sessionTag}] CloseAsync WS error (best effort): {ex.WebSocketErrorCode} {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemWarn($"[Realtime/{_sessionTag}] CloseAsync invalid state (best effort): {ex.Message}");
                }
            }

            await Task.WhenAny(
                Task.WhenAll(_sendLoopTask ?? Task.CompletedTask, _recvLoopTask ?? Task.CompletedTask),
                Task.Delay(2000));

            _ws?.Dispose();
            _loopCts?.Dispose();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.SystemWarn($"[Realtime/{_sessionTag}] DisposeAsync already disposed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[Realtime/{_sessionTag}] DisposeAsync invalid state: {ex.Message}");
        }
    }
}
