using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Voice;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Audio;
using Invekto.VoiceRuntime.Clients;
using Invekto.VoiceRuntime.Metrics;
using Invekto.VoiceRuntime.Providers;
using Invekto.VoiceRuntime.Realtime;
using Invekto.VoiceRuntime.Tools;

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

    // OpenAI Realtime voice VALIDATION allowlist for the browser voice picker (?voice=X). The
    // handshake validates against this before overriding the session voice so an arbitrary/unknown
    // string is never forwarded to the API (unknown/empty → keep the appsettings default). This is
    // the full set of 10 and matches voice-poc.js ALLOWED_VOICES; the <select id="voiceSelect">
    // exposes a curated SUBSET of these for UX — the subset stays ⊆ this list but need not equal it.
    private static readonly HashSet<string> AllowedVoices = new(StringComparer.Ordinal)
    { "alloy", "ash", "ballad", "coral", "echo", "sage", "shimmer", "verse", "marin", "cedar" };

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
        TenantInfoClient tenantClient,
        FlowInfoClient flowClient,
        SearchKnowledgeBaseTool searchKnowledgeBaseTool,
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

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);

        // Chunk B (AD-22 + AD-25): server-side authoritative tenant + flow lookup. Browser
        // dropdown values are display-only; the names/sectors used downstream MUST come from
        // a fresh service-JWT-authenticated fetch so a tampered ?tenant_id cannot inject a
        // forged prompt. F0 legacy mode skips this (no impersonation context to validate).
        VoiceTestContext? voiceContext = null;
        var providerMetadata = new Dictionary<string, string>
        {
            ["flow_id"] = flowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["caller_tenant_id"] = callerTenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["impersonation"] = f05Mode ? "voice_test_f05" : "f0_legacy"
        };

        if (f05Mode)
        {
            var (ctxResult, ctxError) = await FetchVoiceTestContextAsync(
                tenantClient, flowClient, targetTenantId, flowId, callerTenantId, logger, sessionId, sessionCts.Token);

            if (ctxError != null)
            {
                await SendErrorControlFrameAsync(ws, ctxError.Value.Code, ctxError.Value.Message, logger, sessionId, sessionCts.Token);
                await CloseWsAsync(ws, WebSocketCloseStatus.InternalServerError, "context fetch failed", logger, sessionId);
                logger.SystemWarn($"[{ctxError.Value.Code}] [VoicePoc/{sessionId}] F0.5 context fetch aborted: {ctxError.Value.Message}");
                return;
            }
            if (ctxResult == null)
            {
                // Defensive guard: FetchVoiceTestContextAsync returns exactly one of (Context, Error)
                // non-null on every code path. If both are null here, the helper has been misused —
                // surface as INV-VR-014 rather than crashing on null dereference.
                logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [VoicePoc/{sessionId}] FetchVoiceTestContextAsync returned (null, null) — invariant violated");
                await SendErrorControlFrameAsync(ws, ErrorCodes.VoiceRuntimeTenantFetchFailed, "Beklenmeyen icsel hata. Lutfen tekrar deneyin.", logger, sessionId, sessionCts.Token);
                await CloseWsAsync(ws, WebSocketCloseStatus.InternalServerError, "context invariant violated", logger, sessionId);
                return;
            }

            voiceContext = ctxResult;
            providerMetadata["tenant_name"] = voiceContext.TenantName;
            providerMetadata["sector"] = voiceContext.Sector;
            providerMetadata["flow_name"] = voiceContext.FlowName;
            logger.SystemInfo($"[VoicePoc/{sessionId}] F0.5 context built: tenant='{voiceContext.TenantName}'({voiceContext.Sector}) flow='{voiceContext.FlowName}'");
        }

        // F0.5 mode: VoiceCallDescriptor.TenantId = TARGET tenant (impersonation target). Caller is
        // sysadmin (0) by gate above; downstream service-JWT mints will use targetTenantId.
        // F0 mode: descriptor.TenantId = 0 (sysadmin self), no impersonation, generic appsettings instructions.
        // ProviderMetadata records mode + flow_id + caller_tenant_id (and Chunk B context fields
        // when F0.5) for audit/log without changing record schema.
        var descriptor = new VoiceCallDescriptor(
            SessionId: sessionId,
            TenantId: targetTenantId,
            Locale: locale,
            CallerIdHash: null,
            StartedAt: DateTimeOffset.UtcNow,
            ProviderMetadata: providerMetadata);

        logger.SystemInfo($"[VoicePoc/{sessionId}] WS opened (mode={(f05Mode ? "f05" : "f0")}, target_tenant={targetTenantId}, flow={flowId}, caller_tenant={callerTenantId}, locale={locale}, dev_bypass={devBypass})");
        await using var voiceSession = (MicrophoneCallSession)await provider.OpenSessionAsync(descriptor, sessionCts.Token);
        var vadState = vad.CreateSession();
        var turnTiming = latency.CreateTurnTiming(sessionId);

        await using var realtime = new RealtimeApiClient(
            realtimeFactory.Endpoint, realtimeFactory.ApiKey, realtimeFactory.Model, sessionId, logger);

        // Chunk C (AD-23/24/29/30/31): F0.5 sessions override the factory's generic instructions
        // with the per-tenant InstructionsBuilder output and attach the function tools[] array.
        // F0 legacy keeps DefaultConfig untouched (Tools=null → field omitted on the wire).
        SessionConfig sessionConfig;
        IReadOnlyDictionary<string, IVoiceTool>? tools = null;
        if (f05Mode && voiceContext != null)
        {
            var instructions = InstructionsBuilder.Build(voiceContext);
            var toolDefinitions = new List<ToolDefinition>
            {
                ToolDefinition.Function(
                    searchKnowledgeBaseTool.Name,
                    searchKnowledgeBaseTool.Description,
                    searchKnowledgeBaseTool.Parameters)
            };
            sessionConfig = realtimeFactory.DefaultConfig with
            {
                Instructions = instructions,
                Tools = toolDefinitions
            };
            tools = new Dictionary<string, IVoiceTool>(StringComparer.Ordinal)
            {
                [searchKnowledgeBaseTool.Name] = searchKnowledgeBaseTool
            };
        }
        else
        {
            sessionConfig = realtimeFactory.DefaultConfig;
        }

        // Voice picker (browser ?voice=X): override the session's output voice when the requested
        // voice is in the allowlist. Unknown/empty keeps the appsettings default already baked into
        // DefaultConfig. Applies to both F0 and F0.5 configs. Nested record `with` rewrites only the
        // Audio.Output.Voice leaf, leaving formats/transcription/turn-detection untouched.
        var requestedVoice = ctx.Request.Query["voice"].ToString();
        if (AllowedVoices.Contains(requestedVoice))
        {
            sessionConfig = sessionConfig with
            {
                Audio = sessionConfig.Audio with
                {
                    Output = sessionConfig.Audio.Output with { Voice = requestedVoice }
                }
            };
            logger.StepInfo($"[VoicePoc/{sessionId}] voice override applied: {requestedVoice}", "-");
        }

        var orchestrator = new VoicePocOrchestrator(
            ws, voiceSession, realtime, vad, vadState, turnTiming, latency, logger, sessionId);

        // CQ6 iter 2 fix: own the ToolExecutor IDisposable so its CancellationTokenRegistration is
        // explicitly disposed at session end (instead of relying on the token's automatic
        // unregistration once the callback fires). `using` ensures Dispose runs even on early throw.
        ToolExecutor? toolExecutor = null;
        try
        {
            await realtime.ConnectAsync(sessionCts.Token);
            await realtime.SendSessionUpdateAsync(sessionConfig, sessionCts.Token);

            toolExecutor = orchestrator.WireRealtimeHandlers(tools, targetTenantId, sessionCts.Token);
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
            // CQ6 iter 2 fix: explicitly dispose the ToolExecutor so its CancellationTokenRegistration
            // unregisters BEFORE the session token's automatic cleanup runs. Dispose is idempotent
            // (Interlocked guard), so a redundant call from a future code path stays safe.
            toolExecutor?.Dispose();
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
    /// Chunk B (AD-25): parallel fetch tenant + flow via per-call service JWTs. Both tasks are
    /// started concurrently and observed via Task.WhenAll — if either throws, the other is still
    /// awaited via its Task handle so no in-flight HTTP request is left unobserved (CQ6).
    /// Returns (context, null) on success or (null, error) on any fetch/validate failure.
    /// Error tuple carries the INV-VR code + Turkish actionable message for the WS control frame.
    /// </summary>
    private static async Task<(VoiceTestContext? Context, (string Code, string Message)? Error)> FetchVoiceTestContextAsync(
        TenantInfoClient tenantClient,
        FlowInfoClient flowClient,
        int targetTenantId,
        int flowId,
        int callerTenantId,
        JsonLinesLogger logger,
        string sessionId,
        CancellationToken ct)
    {
        var tenantTask = tenantClient.GetTenantAsync(targetTenantId, ct);
        var flowTask = flowClient.GetFlowAsync(targetTenantId, flowId, ct);

        // Observe BOTH tasks even if either faults — Task.WhenAll surfaces only the first exception,
        // but the underlying tasks complete and any subsequent failures become observable here.
        try
        {
            await Task.WhenAll(tenantTask, flowTask);
        }
        catch (TenantInfoFetchException) { /* inspected per-task below */ }
        catch (FlowInfoFetchException) { /* inspected per-task below */ }
        catch (OperationCanceledException) { throw; }

        if (tenantTask.IsFaulted)
        {
            // Unwrap aggregate; we know the inner is TenantInfoFetchException because GetTenantAsync
            // only throws that type. Defensive cast back to inspect ErrorCode.
            var ex = tenantTask.Exception?.InnerException as TenantInfoFetchException;
            var code = ex?.ErrorCode ?? ErrorCodes.VoiceRuntimeTenantFetchFailed;
            return (null, (code, "Firma bilgisi servisi yanit vermedi. Birkac saniye sonra tekrar deneyin."));
        }
        if (flowTask.IsFaulted)
        {
            var ex = flowTask.Exception?.InnerException as FlowInfoFetchException;
            var code = ex?.ErrorCode ?? ErrorCodes.VoiceRuntimeFlowFetchFailed;
            return (null, (code, "Akis bilgisi servisi yanit vermedi. Birkac saniye sonra tekrar deneyin."));
        }

        var tenant = tenantTask.Result;
        var flow = flowTask.Result;

        if (tenant == null)
        {
            logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeTenantNotFound}] [VoicePoc/{sessionId}] target tenant {targetTenantId} not in active list");
            return (null, (ErrorCodes.VoiceRuntimeTenantNotFound, $"Firma {targetTenantId} aktif degil. Listeyi yenileyip tekrar deneyin."));
        }
        if (flow == null)
        {
            logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeFlowNotFound}] [VoicePoc/{sessionId}] target flow {flowId} not found for tenant {targetTenantId}");
            return (null, (ErrorCodes.VoiceRuntimeFlowNotFound, $"Akis {flowId} bulunamadi. Listeyi yenileyip tekrar deneyin."));
        }

        var context = new VoiceTestContext(
            TargetTenantId: targetTenantId,
            TenantName: tenant.TenantName ?? string.Empty,
            Sector: tenant.Sector ?? string.Empty,
            FlowId: flowId,
            FlowName: flow.FlowName ?? string.Empty,
            CallerTenantId: callerTenantId,
            ImpersonationMode: "voice_test_f05");

        return (context, null);
    }

    /// <summary>
    /// Sends a single JSON control frame (text) to the browser before WS close. Best-effort
    /// delivery: WS exceptions during error-path send are logged at WARN (not swallowed) so the
    /// operator can still diagnose double-failures from jsonl logs (CQ2).
    /// </summary>
    private static async Task SendErrorControlFrameAsync(WebSocket ws, string code, string message, JsonLinesLogger logger, string sessionId, CancellationToken ct)
    {
        if (ws.State != WebSocketState.Open)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] SendErrorControlFrame skipped — ws state={ws.State} (code={code})");
            return;
        }
        try
        {
            var json = JsonSerializer.Serialize(new { type = "error", code, message }, ControlJsonOpts);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        catch (WebSocketException ex)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] SendErrorControlFrame WS error (code={code}): {ex.WebSocketErrorCode} {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] SendErrorControlFrame invalid state (code={code}): {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] SendErrorControlFrame cancelled (code={code})");
        }
    }

    /// <summary>
    /// Best-effort WS close — swallows WS state exceptions, logs at WARN.
    /// </summary>
    private static async Task CloseWsAsync(WebSocket ws, WebSocketCloseStatus status, string description, JsonLinesLogger logger, string sessionId)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(status, description, CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] CloseWs WS error: {ex.WebSocketErrorCode} {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            logger.SystemWarn($"[VoicePoc/{sessionId}] CloseWs invalid state: {ex.Message}");
        }
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
        // Barge-in suppression: after the user interrupts, OpenAI keeps streaming a few hundred ms
        // of audio.delta for the cancelled response before it honors response.cancel. Forwarding
        // those makes the bot "keep talking" after the user cut in. While this flag is set we DROP
        // outbound audio deltas; it is cleared when the cancelled response's response.done arrives
        // (and defensively on the user's speech_stopped), so the NEXT response plays normally.
        private volatile bool _suppressBotAudio;
        // True while OpenAI has a response in progress (response.created → response.done). Used to
        // gate the function-calling response.create so it never fires while a response is active
        // (OpenAI rejects that with INV-VR-001 "Conversation already has an active response").
        private volatile bool _responseActive;

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

        /// <summary>
        /// Wires Realtime → orchestrator event callbacks. When <paramref name="tools"/> is non-null
        /// (F0.5 mode) a ToolExecutor is instantiated, bound to OnFunctionCallArguments*, and
        /// RETURNED so the caller (HandleMicrophoneWsAsync) can dispose it at session end (CQ6
        /// iter 2 fix — explicit IDisposable ownership). F0 legacy mode returns null — function
        /// call events go to OnUnknownEvent (fine — F0 session.update sends no tools).
        /// </summary>
        public ToolExecutor? WireRealtimeHandlers(IReadOnlyDictionary<string, IVoiceTool>? tools, int impersonationTenantId, CancellationToken sessionCt)
        {
            ToolExecutor? executor = null;
            if (tools != null)
            {
                executor = new ToolExecutor(
                    realtime: _realtime,
                    tools: tools,
                    impersonationTenantId: impersonationTenantId,
                    sessionId: _sessionId,
                    logger: _logger,
                    sendHudFrame: SendControlAsync,
                    isResponseActive: () => _responseActive,
                    sessionCt: sessionCt);
                var executorLocal = executor; // capture for lambda

                _realtime.OnFunctionCallArgumentsDelta += executorLocal.OnArgumentsDelta;
                _realtime.OnFunctionCallArgumentsDone += evt =>
                {
                    // Fire-and-forget on the thread pool: the Realtime receive loop must not block
                    // on the tool's HTTP roundtrip + Realtime write-back. ToolExecutor.OnArgumentsDoneAsync
                    // owns its own defensive outer try/catch (CQ2/CQ5/CQ12 fix), so the only thing
                    // this wrapper has to swallow is the OperationCanceledException raised when the
                    // session token fires mid-flight (CQ2 iter 2 fix — `when` filter on
                    // sessionCt.IsCancellationRequested so unrelated OCEs still surface in logs).
                    _ = Task.Run(async () =>
                    {
                        try { await executorLocal.OnArgumentsDoneAsync(evt); }
                        catch (OperationCanceledException) when (sessionCt.IsCancellationRequested) { /* session ended */ }
                        catch (InvalidOperationException ex)
                        {
                            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [VoicePoc/{_sessionId}] tool executor invalid state (call_id={evt.CallId}, tool={evt.Name}): {ex.Message}");
                        }
                    });
                };
            }
            // continues below with the existing Realtime-handler wiring (unchanged)

            _realtime.OnSpeechStarted += startEvt =>
            {
                if (_botSpeaking)
                {
                    var bargeEvt = _turn.StampBargeInDetected();
                    _latency.RecordEvent(bargeEvt);
                    // Set synchronously so audio.delta events arriving on the very next pump tick are
                    // dropped immediately (the Task.Run below only handles the async cancel/drain/notify).
                    _suppressBotAudio = true;
                    _botSpeaking = false;
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
                    // Drop lingering audio from a response the user just barged in on (see _suppressBotAudio).
                    if (_suppressBotAudio) return;
                    if (!_botSpeaking)
                    {
                        var fb = _turn.StampTtsFirstByteToUser();
                        _latency.RecordEvent(fb);
                        _botSpeaking = true;
                        _ = SendControlAsync(new { type = "first_byte", elapsed_ms = fb.ElapsedMs }, CancellationToken.None);
                    }

                    var pcm24k = PcmResampler.Base64ToPcm(delta.DeltaBase64);
                    var pcm48k = PcmResampler.Upsample24To48(pcm24k);

                    // Emit the WHOLE upsampled delta as a single frame. The browser's handleAudio accepts
                    // any buffer size and chains buffers sample-accurately, so fixed 20ms re-framing is
                    // unnecessary here. The previous fixed-frame loop only sent COMPLETE 960-sample frames
                    // and DROPPED the trailing remainder (pcm48k.Length % 960 samples) on every delta —
                    // OpenAI sends variable-size deltas, so that lost a slice of audio at each delta
                    // boundary, producing the periodic waveform discontinuity heard as crackling. Sending
                    // the full buffer is lossless and removes that artifact.
                    var bytes = new byte[pcm48k.Length * 2];
                    for (int i = 0; i < pcm48k.Length; i++)
                    {
                        var s = pcm48k[i];
                        bytes[i * 2] = (byte)(s & 0xFF);
                        bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                    var frame = new OpusFrame(bytes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Interlocked.Increment(ref _seq));
                    _ = _session.SendOutgoingFrameAsync(frame, CancellationToken.None);
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

            _realtime.OnResponseCreated += () => _responseActive = true;

            _realtime.OnResponseDone += doneEvt =>
            {
                _botSpeaking = false;
                _responseActive = false;
                // response.done is the AUTHORITATIVE end-of-audio signal for a response — including a
                // cancelled one (OpenAI emits response.done with status=cancelled after honoring
                // response.cancel). Reopening the audio gate here (and ONLY here) guarantees every
                // lingering cancelled-response audio.delta has already been seen+dropped before the
                // NEXT response can play. (An earlier attempt also cleared this on speech_stopped,
                // but that races AHEAD of response.done and could let stale audio slip through —
                // Codex CQ9. response.done after cancel is reliable, so this single gate is correct.)
                _suppressBotAudio = false;
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

            return executor; // CQ6 iter 2 fix: hand ownership of the IDisposable up to the caller.
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
