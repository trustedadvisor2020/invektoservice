using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Voice;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Audio;
using Invekto.VoiceRuntime.Metrics;
using Invekto.VoiceRuntime.Providers;
using Invekto.VoiceRuntime.Realtime;

namespace Invekto.VoiceRuntime.Endpoints;

/// <summary>
/// F0 PoC WebSocket endpoint: /ws/voice/microphone
///
/// Pipeline (per WS connection) — F0 uses RAW PCM16 48k LE (no Opus codec):
///   Browser → WS binary (PCM16 48k LE 20ms = 1920 bytes) → SileroVad (48→16 inline)
///                                                         → PcmResampler 48→24
///                                                         → Realtime input_audio_buffer.append (base64)
///   Realtime response.audio.delta (PCM16 24k base64) → PcmResampler 24→48
///                                                    → PCM16 48k LE bytes → MicrophoneCallSession.SendOutgoing
///                                                    → WS binary
///   Realtime transcript events → WS text frames (JSON: transcript_user / transcript_bot / latency / error)
///
/// F2 will replace raw PCM with Opus codec (AD-3 canonical) for Toniva PBX integration.
/// OpusCodec class + Concentus dependency are kept in the codebase to keep the F2 wiring drop-in.
///
/// JWT-gated in prod. Dev environment: bypass if Jwt:SecretKey empty AND ?dev=1 query.
/// </summary>
public static class VoicePocEndpoints
{
    private const int Pcm48kFrameSamples = 960;     // 20ms @ 48kHz
    private const int Pcm48kFrameBytes = Pcm48kFrameSamples * 2;  // 1920 bytes PCM16 LE

    private static readonly JsonSerializerOptions ControlJsonOpts = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    public static void MapVoicePocEndpoints(this WebApplication app)
    {
        app.MapGet("/ws/voice/microphone", HandleMicrophoneWsAsync);
    }

    private static async Task HandleMicrophoneWsAsync(
        HttpContext ctx,
        JsonLinesLogger logger,
        SileroVad vad,
        LatencyTracker latency,
        RealtimeSessionFactory realtimeFactory,
        MicrophoneCallProvider provider,
        JwtValidator jwt,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: WebSocket upgrade required");
            return;
        }

        // WS Origin gate (CSRF defense). In Development, accept localhost / 127.0.0.1 / null Origin.
        // In Production, require explicit Cors:AllowedOrigins match. Block unmatched.
        var origin = ctx.Request.Headers["Origin"].ToString();
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var originAllowed = env.IsDevelopment()
            ? string.IsNullOrEmpty(origin) || origin.Contains("localhost", StringComparison.OrdinalIgnoreCase) || origin.Contains("127.0.0.1") || allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase))
            : allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
        if (!originAllowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: Origin not allowed");
            logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc] WS handshake rejected: origin '{origin}' not in allowed list");
            return;
        }

        // Auth: prod requires JWT (?token=... query for browser); dev allows ?dev=1 when secret empty.
        var jwtSecret = config["Jwt:SecretKey"] ?? "";
        var devBypass = env.IsDevelopment() && string.IsNullOrWhiteSpace(jwtSecret) && ctx.Request.Query["dev"] == "1";
        int callerTenantId = 0;

        if (!devBypass)
        {
            var token = ctx.Request.Query["token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: token query parameter required");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc] WS handshake rejected: missing token query");
                return;
            }
            try
            {
                var (tenantContext, error) = jwt.ValidateToken(token);
                if (tenantContext is null)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: {error ?? "invalid token"}");
                    logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc] WS handshake rejected: {error ?? "invalid token"}");
                    return;
                }
                callerTenantId = tenantContext.TenantId;

                // AD-21 (F0.5): Voice Test impersonation gate — caller must be sysadmin (tenant=0).
                // Non-sysadmin tenants are rejected with INV-VR-020 (the test page is opsOnly).
                // F2 PBX production VoiceRuntime will use a different endpoint path where caller
                // tenant_id is the operating tenant (no impersonation).
                if (callerTenantId != 0)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeImpersonationGateFailed}: Voice Test requires sysadmin (tenant=0); got tenant={callerTenantId}");
                    logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeImpersonationGateFailed}] [VoicePoc] WS handshake rejected: non-sysadmin caller tenant={callerTenantId} (Voice Test opsOnly)");
                    return;
                }
            }
            catch (System.Security.SecurityException ex)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: token validation security error");
                logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc] JWT security exception: {ex.Message}");
                return;
            }
            catch (ArgumentException ex)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}: token format invalid");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc] JWT argument invalid: {ex.Message}");
                return;
            }
        }

        // AD-21 (F0.5): Impersonation target query params — ?tenant_id=X&flow_id=Y.
        // The session runs AS IF caller is tenant_id=X using flow_id=Y.
        //
        // Backward-compat: when NEITHER tenant_id NOR flow_id is present, fall back to the
        // legacy F0 microphone mode (target=0 sysadmin self, no flow context, generic instructions
        // from appsettings). This keeps the existing voice-poc.html sales-demo path working
        // until Chunk D ships the tenant + flow dropdowns.
        //
        // When tenant_id IS present, the F0.5 strict validation chain applies:
        //  (1) Parse fail / non-integer → INV-VR-011
        //  (2) tenant_id == 0 (self-impersonation of sysadmin) → INV-VR-013
        //  (3) tenant_id < 0 (negative) → INV-VR-011
        //  (4) flow_id must accompany tenant_id (positive integer) → INV-VR-012
        // PRESENCE-based mode detection (not value-based): once a client sends EITHER `tenant_id`
        // OR `flow_id` query KEY (even with empty value like `?tenant_id=`), strict F0.5 validation
        // applies. This prevents Chunk D dropdown UI from silently falling back to legacy F0 when
        // user has not yet chosen a tenant — empty value still produces actionable INV-VR-011/012.
        var hasTenantKey = ctx.Request.Query.ContainsKey("tenant_id");
        var hasFlowKey = ctx.Request.Query.ContainsKey("flow_id");
        var targetTenantIdRaw = ctx.Request.Query["tenant_id"].ToString();
        var flowIdRaw = ctx.Request.Query["flow_id"].ToString();
        int targetTenantId = 0;
        int flowId = 0;
        bool f05Mode = false;
        if (hasTenantKey || hasFlowKey)
        {
            f05Mode = true;
            if (!int.TryParse(targetTenantIdRaw, out targetTenantId))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeTenantIdMissingOrInvalid}: tenant_id query parameter required (positive integer)");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeTenantIdMissingOrInvalid}] [VoicePoc] WS handshake rejected: tenant_id missing/non-integer (got '{targetTenantIdRaw}')");
                return;
            }
            if (targetTenantId == 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeSelfImpersonationRejected}: tenant_id=0 (sysadmin impersonate yasak)");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeSelfImpersonationRejected}] [VoicePoc] WS handshake rejected: self-impersonation tenant_id=0");
                return;
            }
            if (targetTenantId < 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeTenantIdMissingOrInvalid}: tenant_id must be positive (got {targetTenantId})");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeTenantIdMissingOrInvalid}] [VoicePoc] WS handshake rejected: tenant_id negative ({targetTenantId})");
                return;
            }
            if (!int.TryParse(flowIdRaw, out flowId) || flowId <= 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync($"{ErrorCodes.VoiceRuntimeFlowIdMissingOrInvalid}: flow_id query parameter required (positive integer)");
                logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeFlowIdMissingOrInvalid}] [VoicePoc] WS handshake rejected: flow_id missing/invalid (got '{flowIdRaw}')");
                return;
            }
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var sessionPrefix = f05Mode ? "f05" : "f0";
        var sessionId = $"{sessionPrefix}-{Guid.NewGuid():N}";
        var locale = ctx.Request.Query["locale"].ToString();
        if (string.IsNullOrWhiteSpace(locale)) locale = "tr-TR";

        // F0.5 mode: VoiceCallDescriptor.TenantId = TARGET tenant (impersonation target). Caller is
        // sysadmin (0) by gate above; downstream service-JWT mints will use targetTenantId.
        // F0 mode: descriptor.TenantId = 0 (sysadmin self), no impersonation, generic appsettings instructions.
        // ProviderMetadata records mode + flow_id + caller_tenant_id for audit/log without changing record schema.
        var descriptor = new VoiceCallDescriptor(
            SessionId: sessionId,
            TenantId: targetTenantId,
            Locale: locale,
            CallerIdHash: null,
            StartedAt: DateTimeOffset.UtcNow,
            ProviderMetadata: new Dictionary<string, string>
            {
                ["flow_id"] = flowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["caller_tenant_id"] = callerTenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["impersonation"] = f05Mode ? "voice_test_f05" : "f0_legacy"
            });

        logger.SystemInfo($"[VoicePoc/{sessionId}] WS opened (mode={(f05Mode ? "f05" : "f0")}, target_tenant={targetTenantId}, flow={flowId}, caller_tenant={callerTenantId}, locale={locale}, dev_bypass={devBypass})");

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        await using var voiceSession = (MicrophoneCallSession)await provider.OpenSessionAsync(descriptor, sessionCts.Token);
        var vadState = vad.CreateSession();
        var turnTiming = latency.CreateTurnTiming(sessionId);

        await using var realtime = new RealtimeApiClient(
            realtimeFactory.Endpoint, realtimeFactory.ApiKey, realtimeFactory.Model, sessionId, logger);

        var orchestrator = new VoicePocOrchestrator(
            ws, voiceSession, realtime, vad, vadState, turnTiming, latency, logger, sessionId);

        try
        {
            await realtime.ConnectAsync(sessionCts.Token);
            await realtime.SendSessionUpdateAsync(realtimeFactory.DefaultConfig, sessionCts.Token);

            orchestrator.WireRealtimeHandlers();
            await orchestrator.SendControlAsync(new { type = "ready", session_id = sessionId }, sessionCts.Token);

            var browserRxTask = orchestrator.BrowserRxLoopAsync(sessionCts.Token);
            var browserTxTask = orchestrator.BrowserTxLoopAsync(sessionCts.Token);
            var voiceFwdTask = orchestrator.VoiceToRealtimeForwardLoopAsync(sessionCts.Token);

            await Task.WhenAny(browserRxTask, browserTxTask, voiceFwdTask);
        }
        catch (InvalidOperationException ex)
        {
            await orchestrator.SendControlAsync(new { type = "error", code = ExtractErrorCode(ex.Message), message = ex.Message }, CancellationToken.None);
            logger.SystemError($"[VoicePoc/{sessionId}] Session failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Expected on WS close
        }
        catch (WebSocketException ex)
        {
            logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc/{sessionId}] WS error: {ex.WebSocketErrorCode} {ex.Message}");
        }
        finally
        {
            sessionCts.Cancel();
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "session end", CancellationToken.None);
            }
            catch (WebSocketException closeEx)
            {
                logger.SystemWarn($"[VoicePoc/{sessionId}] WS close error (best effort): {closeEx.WebSocketErrorCode} {closeEx.Message}");
            }
            catch (InvalidOperationException closeEx)
            {
                logger.SystemWarn($"[VoicePoc/{sessionId}] WS close invalid state (best effort): {closeEx.Message}");
            }
            logger.SystemInfo($"[VoicePoc/{sessionId}] WS closed");
        }
    }

    private static string ExtractErrorCode(string message)
    {
        if (message.StartsWith("INV-VR-"))
        {
            var idx = message.IndexOf(':');
            return idx > 0 ? message[..idx] : "INV-VR-001";
        }
        return ErrorCodes.VoiceRuntimeRealtimeConnectionFailed;
    }

    /// <summary>
    /// Per-session orchestrator binding three concurrent pipelines + Realtime event callbacks.
    /// Audio payload format: raw PCM16 LE 48kHz mono 20ms frames (1920 bytes per frame).
    /// </summary>
    private sealed class VoicePocOrchestrator
    {
        private readonly WebSocket _ws;
        private readonly MicrophoneCallSession _session;
        private readonly RealtimeApiClient _realtime;
        private readonly SileroVad _vad;
        private readonly VadSessionState _vadState;
        private readonly TurnTiming _turn;
        private readonly LatencyTracker _latency;
        private readonly JsonLinesLogger _logger;
        private readonly string _sessionId;
        private int _seq;
        private bool _userSpeaking;
        private bool _botSpeaking;

        // WebSocket only supports ONE concurrent SendAsync. BrowserTxLoop (binary audio) and
        // SendControlAsync (text JSON) can both fire from different callbacks/loops, so we
        // serialize all outbound sends through this semaphore.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public VoicePocOrchestrator(
            WebSocket ws, MicrophoneCallSession session, RealtimeApiClient realtime,
            SileroVad vad, VadSessionState vadState,
            TurnTiming turn, LatencyTracker latency, JsonLinesLogger logger, string sessionId)
        {
            _ws = ws; _session = session; _realtime = realtime;
            _vad = vad; _vadState = vadState;
            _turn = turn; _latency = latency; _logger = logger; _sessionId = sessionId;
        }

        public void WireRealtimeHandlers()
        {
            _realtime.OnSpeechStarted += startEvt =>
            {
                if (_botSpeaking)
                {
                    var bargeEvt = _turn.StampBargeInDetected();
                    _latency.RecordEvent(bargeEvt);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _realtime.SendResponseCancelAsync(CancellationToken.None);
                            await _session.SignalBargeInAsync(CancellationToken.None);
                            var stop = _turn.StampBargeInTtsStopped();
                            _latency.RecordEvent(stop);
                            await SendControlAsync(new { type = "barge_in", elapsed_ms = stop.ElapsedMs }, CancellationToken.None);
                            _botSpeaking = false;
                        }
                        catch (OperationCanceledException) { /* session ended */ }
                        catch (InvalidOperationException ex)
                        {
                            _logger.SystemWarn($"[VoicePoc/{_sessionId}] barge-in cancel invalid state: {ex.Message}");
                        }
                        catch (WebSocketException ex)
                        {
                            _logger.SystemWarn($"[VoicePoc/{_sessionId}] barge-in cancel WS error: {ex.WebSocketErrorCode} {ex.Message}");
                        }
                    });
                }
            };

            _realtime.OnAudioDelta += delta =>
            {
                try
                {
                    if (!_botSpeaking)
                    {
                        var fb = _turn.StampTtsFirstByteToUser();
                        _latency.RecordEvent(fb);
                        _botSpeaking = true;
                        _ = SendControlAsync(new { type = "first_byte", elapsed_ms = fb.ElapsedMs }, CancellationToken.None);
                    }

                    var pcm24k = PcmResampler.Base64ToPcm(delta.DeltaBase64);
                    var pcm48k = PcmResampler.Upsample24To48(pcm24k);

                    // Re-frame to 20ms (960 samples = 1920 bytes PCM16 LE) — Realtime may send variable-size chunks
                    for (int offset = 0; offset + Pcm48kFrameSamples <= pcm48k.Length; offset += Pcm48kFrameSamples)
                    {
                        var bytes = new byte[Pcm48kFrameBytes];
                        for (int i = 0; i < Pcm48kFrameSamples; i++)
                        {
                            var s = pcm48k[offset + i];
                            bytes[i * 2] = (byte)(s & 0xFF);
                            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                        }
                        var frame = new OpusFrame(bytes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Interlocked.Increment(ref _seq));
                        _ = _session.SendOutgoingFrameAsync(frame, CancellationToken.None);
                    }
                }
                catch (FormatException ex)
                {
                    _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeOpusEncodeFailed}] [VoicePoc/{_sessionId}] audio_delta base64 parse failed: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeOpusEncodeFailed}] [VoicePoc/{_sessionId}] audio_delta arg invalid: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeOpusEncodeFailed}] [VoicePoc/{_sessionId}] audio_delta invalid state: {ex.Message}");
                }
            };

            _realtime.OnAudioTranscriptDelta += t =>
            {
                _ = SendControlAsync(new { type = "transcript_bot", delta = t.Delta }, CancellationToken.None);
            };

            _realtime.OnUserTranscriptCompleted += t =>
            {
                _ = SendControlAsync(new { type = "transcript_user", text = t.Transcript }, CancellationToken.None);
            };

            _realtime.OnResponseDone += doneEvt =>
            {
                _botSpeaking = false;
                _ = SendControlAsync(new { type = "response_done" }, CancellationToken.None);
            };

            _realtime.OnRealtimeError += err =>
            {
                _ = SendControlAsync(new
                {
                    type = "error",
                    code = ErrorCodes.VoiceRuntimeRealtimeConnectionFailed,
                    realtime_code = err.Error.Code,
                    message = err.Error.Message
                }, CancellationToken.None);
            };
        }

        public async Task BrowserRxLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8 * 1024];
            using var accumulator = new MemoryStream();
            try
            {
                while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    accumulator.SetLength(0);
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        accumulator.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var pcmBytes = accumulator.ToArray();
                        if (pcmBytes.Length != Pcm48kFrameBytes)
                        {
                            _logger.SystemWarn($"[VoicePoc/{_sessionId}] Unexpected binary frame size {pcmBytes.Length} (expected {Pcm48kFrameBytes}); dropping");
                            continue;
                        }
                        var frame = new OpusFrame(pcmBytes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Interlocked.Increment(ref _seq));
                        await _session.PushIncomingAsync(frame, ct);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Control message from browser (start/stop/ping)
                        var text = Encoding.UTF8.GetString(accumulator.ToArray());
                        _logger.SystemInfo($"[VoicePoc/{_sessionId}] browser control: {text}");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Normal browser close
            }
            catch (WebSocketException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc/{_sessionId}] BrowserRxLoop WS error: {ex.WebSocketErrorCode} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc/{_sessionId}] BrowserRxLoop invalid state: {ex.Message}");
            }
        }

        public async Task VoiceToRealtimeForwardLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var frame in _session.IncomingFrames.WithCancellation(ct))
                {
                    var pcmBytes = frame.Payload.ToArray();
                    if (pcmBytes.Length != Pcm48kFrameBytes) continue;

                    // PCM16 LE bytes → short[]
                    var pcm48k = new short[Pcm48kFrameSamples];
                    for (int i = 0; i < Pcm48kFrameSamples; i++)
                        pcm48k[i] = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));

                    // Silero VAD: speech onset / offset detection
                    var prob = _vad.ProcessFrame48k(_vadState, pcm48k);
                    if (prob.HasValue)
                    {
                        var isSpeech = prob.Value > 0.5f;
                        if (isSpeech && !_userSpeaking)
                        {
                            _userSpeaking = true;
                            _latency.RecordEvent(_turn.StampSpeechStart());
                        }
                        else if (!isSpeech && _userSpeaking)
                        {
                            _userSpeaking = false;
                            var end = _turn.StampSpeechEnd();
                            _latency.RecordEvent(end);
                            _latency.RecordEvent(_turn.StampRealtimeRequestSent());
                        }
                    }

                    // Resample 48k → 24k, send to Realtime
                    var pcm24k = PcmResampler.Downsample48To24(pcm48k);
                    var base64 = PcmResampler.PcmToBase64(pcm24k);
                    await _realtime.SendAudioAsync(base64, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [VoicePoc/{_sessionId}] VoiceToRealtime WS error: {ex.WebSocketErrorCode} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeRealtimeConnectionFailed}] [VoicePoc/{_sessionId}] VoiceToRealtime invalid state: {ex.Message}");
            }
        }

        public async Task BrowserTxLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var frame in _session.OutgoingFrames.WithCancellation(ct))
                {
                    if (_ws.State != WebSocketState.Open) break;
                    var payload = frame.Payload.ToArray();
                    await _sendLock.WaitAsync(ct);
                    try
                    {
                        await _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct);
                    }
                    finally { _sendLock.Release(); }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc/{_sessionId}] BrowserTxLoop WS error: {ex.WebSocketErrorCode} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [VoicePoc/{_sessionId}] BrowserTxLoop invalid state: {ex.Message}");
            }
        }

        public async Task SendControlAsync(object payload, CancellationToken ct)
        {
            try
            {
                if (_ws.State != WebSocketState.Open) return;
                var json = JsonSerializer.Serialize(payload, ControlJsonOpts);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _sendLock.WaitAsync(ct);
                try
                {
                    await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
                }
                finally { _sendLock.Release(); }
            }
            catch (OperationCanceledException) { /* session ended */ }
            catch (WebSocketException ex)
            {
                _logger.SystemWarn($"[VoicePoc/{_sessionId}] SendControl WS error: {ex.WebSocketErrorCode} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn($"[VoicePoc/{_sessionId}] SendControl invalid state: {ex.Message}");
            }
        }
    }
}
