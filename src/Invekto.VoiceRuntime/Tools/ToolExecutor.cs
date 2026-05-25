using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Realtime;

namespace Invekto.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F0.5 Chunk C: orchestrates Realtime function call lifecycle.
///
/// Wire-up (per WS session, owned by VoicePocOrchestrator):
///   realtime.OnFunctionCallArgumentsDelta += executor.OnArgumentsDelta
///   realtime.OnFunctionCallArgumentsDone  += executor.OnArgumentsDone   (fire-and-forget Task)
///
/// Per call_id flow:
///   .delta x N  → append to ConcurrentDictionary<call_id, StringBuilder> (AD-31 defense)
///   .done       → resolve tool by name, emit tool_call_started HUD, execute under 5sn timeout,
///                 send function_call_output + response.create, emit tool_call_completed HUD.
///
/// Failure modes (all return structured function_call_output so session stays alive):
///   - Unknown tool name           → INV-VR-023 structured error JSON
///   - Args JSON parse fail        → INV-VR-016 structured error JSON (inside SearchKnowledgeBaseTool)
///   - Tool execute exception      → INV-VR-016 structured error JSON
///   - 5sn timeout                 → INV-VR-017 structured error JSON (kb_unavailable shape)
///   - WS already closed mid-send  → log WARN, suppress completed HUD (browser keeps "started" rozet)
///
/// IDisposable: dispose unregisters the session-end CancellationToken callback and clears the
/// args buffer dictionary. Owner (VoicePocOrchestrator) is responsible for calling Dispose when
/// the WS session ends — typically via a `using` block in HandleMicrophoneWsAsync.
/// </summary>
public sealed class ToolExecutor : IDisposable
{
    private static readonly JsonSerializerOptions ErrorJsonOpts = new(JsonSerializerDefaults.Web);

    private const int ToolExecutionTimeoutMs = 5000;
    private const int ArgsPreviewMaxChars = 120;
    // CQ10 fix: reserve 3 chars for the "..." suffix so the truncated string + ellipsis stays
    // ≤120 chars total (was 123 = 120 + "..." before).
    private const int ArgsPreviewTruncateBudget = ArgsPreviewMaxChars - 3;
    // CQ6 fix: per-session cap on outstanding .delta buffers. The plan's intentional_exclusions
    // restricts max 1 in-flight tool call, and OpenAI's spec aligns; this cap is 8x defense
    // against model misbehavior streaming many call_ids without ever sending a .done (which would
    // otherwise leak a StringBuilder per orphaned call_id for the lifetime of the WS session).
    private const int MaxConcurrentBuffers = 8;

    private readonly RealtimeApiClient _realtime;
    private readonly IReadOnlyDictionary<string, IVoiceTool> _tools;
    private readonly int _impersonationTenantId;
    private readonly string _sessionId;
    private readonly JsonLinesLogger _logger;
    private readonly Func<object, CancellationToken, Task> _sendHudFrame;
    private readonly CancellationToken _sessionCt;
    private readonly CancellationTokenRegistration _sessionEndCleanup;
    private int _disposed;

    // AD-31 defense: per call_id buffer. OpenAI Realtime spec says max 1 in-flight, but a model
    // misbehavior (interleaved deltas across two call_ids in one response) would silently corrupt
    // a single shared buffer. Per-key isolation makes the failure observable instead.
    private readonly ConcurrentDictionary<string, StringBuilder> _argBuffers = new(StringComparer.Ordinal);

    public ToolExecutor(
        RealtimeApiClient realtime,
        IReadOnlyDictionary<string, IVoiceTool> tools,
        int impersonationTenantId,
        string sessionId,
        JsonLinesLogger logger,
        Func<object, CancellationToken, Task> sendHudFrame,
        CancellationToken sessionCt)
    {
        _realtime = realtime;
        _tools = tools;
        _impersonationTenantId = impersonationTenantId;
        _sessionId = sessionId;
        _logger = logger;
        _sendHudFrame = sendHudFrame;
        _sessionCt = sessionCt;
        // CQ6 fix: when the WS session closes, drop every outstanding args buffer so orphans
        // (delta arrived, .done never came) do not survive the session lifetime. The registration
        // is also disposed explicitly by Dispose() so the callback handle does not linger when
        // the token outlives the executor (CQ6 iter 2 fix — IDisposable hygiene).
        _sessionEndCleanup = sessionCt.Register(() => _argBuffers.Clear());
    }

    /// <summary>
    /// Buffer the incoming JSON args fragment. Safe to call concurrently across call_ids.
    /// CQ6 fix: rejects new call_ids when the per-session buffer count is at MaxConcurrentBuffers
    /// — this caps the memory cost of model misbehavior (deltas without matching .done). Drops are
    /// non-fatal: the model will receive its function_call_output via the .done path if it ever
    /// arrives because .done carries the canonical args string.
    /// </summary>
    public void OnArgumentsDelta(ResponseFunctionCallArgumentsDeltaEvent evt)
    {
        if (string.IsNullOrEmpty(evt.CallId)) return;
        if (!_argBuffers.ContainsKey(evt.CallId) && _argBuffers.Count >= MaxConcurrentBuffers)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [ToolExecutor/{_sessionId}] arg buffer cap reached ({MaxConcurrentBuffers}); dropping delta for new call_id={evt.CallId}");
            return;
        }
        var buf = _argBuffers.GetOrAdd(evt.CallId, static _ => new StringBuilder());
        // StringBuilder.Append is not thread-safe on its own; lock per-buffer is overkill since
        // a single call_id's deltas arrive serially over the WS receive loop. The ConcurrentDictionary
        // only protects the key→buffer lookup across different call_ids.
        buf.Append(evt.Delta);
    }

    /// <summary>
    /// Fire-and-forget invoker. The caller (orchestrator) schedules this on the thread pool so the
    /// Realtime receive loop is not blocked while we await an HTTP roundtrip + Realtime write.
    ///
    /// Iter 2 (CQ5/CQ12 fix): all catches are now SPECIFIC TYPED catches — no `catch (Exception)`
    /// even with filter. The set covers every exception class ExecuteOneCallAsync can throw across
    /// its call chain (JsonException from JsonDocument.Parse, InvalidOperationException from
    /// Realtime client state guards, ArgumentException from JwtGenerator validation). Each lands
    /// in DefenseFallbackAsync which emits an INV-VR-016 structured fallback so the model never
    /// freezes waiting for a function_call_output that never arrives.
    /// </summary>
    public async Task OnArgumentsDoneAsync(ResponseFunctionCallArgumentsDoneEvent evt)
    {
        try
        {
            await ExecuteOneCallAsync(evt);
        }
        catch (OperationCanceledException) when (_sessionCt.IsCancellationRequested)
        {
            // Session closing — orchestrator is tearing down; nothing to send back.
        }
        catch (JsonException ex)
        {
            await DefenseFallbackAsync(evt, ex);
        }
        catch (InvalidOperationException ex)
        {
            await DefenseFallbackAsync(evt, ex);
        }
        catch (ArgumentException ex)
        {
            await DefenseFallbackAsync(evt, ex);
        }
    }

    private async Task DefenseFallbackAsync(ResponseFunctionCallArgumentsDoneEvent evt, Exception ex)
    {
        _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [ToolExecutor/{_sessionId}] unexpected {ex.GetType().Name} in tool dispatch (call_id={evt.CallId}, tool={evt.Name}): {ex.Message}");
        var fallbackJson = BuildErrorJson("tool_execute_failed", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
            "Arac calistirilirken icsel bir hata olustu; musteriye 'kisaca bilgi alip donebilir miyim' diyerek yonlendir.");
        try
        {
            await CompleteCallAsync(evt.CallId, evt.Name, fallbackJson, resultCount: 0, status: "error",
                errorCode: ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed, durationMs: 0);
        }
        catch (OperationCanceledException) when (_sessionCt.IsCancellationRequested)
        {
            // Session closing while we tried the fallback send — orchestrator tearing down.
        }
        catch (InvalidOperationException sendEx)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [ToolExecutor/{_sessionId}] fallback CompleteCallAsync invalid state (call_id={evt.CallId}): {sendEx.Message}");
        }
    }

    private async Task ExecuteOneCallAsync(ResponseFunctionCallArgumentsDoneEvent evt)
    {
        // The .done event carries the canonical args string; the buffer was a defensive accumulator
        // (AD-31) used only when .done arrives with an empty Arguments field — which OpenAI is
        // allowed to do once streaming completes. Either way we drop the buffer to release memory.
        _argBuffers.TryRemove(evt.CallId, out var buffered);
        var argsJson = !string.IsNullOrEmpty(evt.Arguments)
            ? evt.Arguments
            : (buffered?.ToString() ?? string.Empty);

        var argsPreview = ArgsPreview(argsJson);

        await SendHudAsync(new
        {
            type = "tool_call_started",
            call_id = evt.CallId,
            name = evt.Name,
            args_preview = argsPreview
        });

        // Defense (AD-29): even when HUD send fails, continue with the execute path. The model is
        // still waiting for function_call_output; skipping it would freeze the conversation.

        if (!_tools.TryGetValue(evt.Name, out var tool))
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeUnknownToolName}] [ToolExecutor/{_sessionId}] unknown tool name '{evt.Name}' (call_id={evt.CallId}); responding with structured error");
            var errorJson = BuildErrorJson("unknown_tool", ErrorCodes.VoiceRuntimeUnknownToolName,
                $"'{evt.Name}' adli arac tanimli degil; lutfen sorunuzu yeniden ifade edin.");
            await CompleteCallAsync(evt.CallId, evt.Name, errorJson, resultCount: 0, status: "error",
                errorCode: ErrorCodes.VoiceRuntimeUnknownToolName, durationMs: 0);
            return;
        }

        // Linked CTS: session cancel (browser close) AND per-call 5sn budget both abort the tool.
        using var execCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCt);
        execCts.CancelAfter(ToolExecutionTimeoutMs);
        var stopwatch = Stopwatch.StartNew();
        ToolExecutionResult result;
        try
        {
            result = await tool.ExecuteAsync(_impersonationTenantId, argsJson, execCts.Token);
        }
        catch (OperationCanceledException) when (!_sessionCt.IsCancellationRequested)
        {
            // Per-call timeout fired (not session close). Map to kb_unavailable shape so the model
            // speaks the Turkish fallback uniformly with KnowledgeSearchException path.
            stopwatch.Stop();
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [ToolExecutor/{_sessionId}] tool '{evt.Name}' timed out after {ToolExecutionTimeoutMs}ms (call_id={evt.CallId})");
            var timeoutJson = BuildErrorJson("kb_unavailable", ErrorCodes.VoiceRuntimeKnowledgeToolFailed,
                $"Arac {ToolExecutionTimeoutMs}ms icinde yanit vermedi; musteriye 'su an bilgi bankama ulasamiyorum' diyerek yonlendir.");
            await CompleteCallAsync(evt.CallId, evt.Name, timeoutJson, resultCount: 0, status: "error",
                errorCode: ErrorCodes.VoiceRuntimeKnowledgeToolFailed, durationMs: (int)stopwatch.ElapsedMilliseconds);
            return;
        }
        catch (OperationCanceledException)
        {
            // Session close — orchestrator will tear down. Do NOT attempt to send anything.
            _logger.SystemInfo($"[ToolExecutor/{_sessionId}] tool '{evt.Name}' cancelled by session close (call_id={evt.CallId})");
            return;
        }
        // Iter 2 (CQ5/CQ12 fix): explicit typed catches per exception class instead of `Exception` filter.
        catch (JsonException ex)
        {
            await EmitToolExecuteFailureAsync(evt, ex, stopwatch);
            return;
        }
        catch (InvalidOperationException ex)
        {
            await EmitToolExecuteFailureAsync(evt, ex, stopwatch);
            return;
        }
        catch (ArgumentException ex)
        {
            await EmitToolExecuteFailureAsync(evt, ex, stopwatch);
            return;
        }
        stopwatch.Stop();

        _logger.SystemInfo($"[ToolExecutor/{_sessionId}] [ToolCall] {evt.Name} call_id={evt.CallId} status={result.Status} results={result.ResultCount} duration={stopwatch.ElapsedMilliseconds}ms");
        await CompleteCallAsync(evt.CallId, evt.Name, result.OutputJson, result.ResultCount, result.Status,
            result.ErrorCode, (int)stopwatch.ElapsedMilliseconds);
    }

    private async Task EmitToolExecuteFailureAsync(ResponseFunctionCallArgumentsDoneEvent evt, Exception ex, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [ToolExecutor/{_sessionId}] tool '{evt.Name}' threw {ex.GetType().Name}: {ex.Message} (call_id={evt.CallId})");
        var errJson = BuildErrorJson("tool_execute_failed", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
            "Arac calistirilirken icsel bir hata olustu; musteriye 'kisaca bilgi alip donebilir miyim' diyerek yonlendir.");
        await CompleteCallAsync(evt.CallId, evt.Name, errJson, resultCount: 0, status: "error",
            errorCode: ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed, durationMs: (int)stopwatch.ElapsedMilliseconds);
    }

    private async Task CompleteCallAsync(string callId, string name, string outputJson, int resultCount,
        string status, string? errorCode, int durationMs)
    {
        // CQ10 fix: Realtime payload first so the model is unblocked, THEN emit the HUD frame.
        // Documented contract (voice-runtime-ws.md): tool_call_completed fires AFTER
        // function_call_output + response.create are sent. Iter 2 (CQ1/CQ10 fix): track whether
        // BOTH sends succeeded and SUPPRESS the completed HUD frame when either fails — the browser
        // keeps the "started" rozet (no false "completed" badge for a payload the model never got).
        bool realtimeSendOk = false;
        try
        {
            await _realtime.SendFunctionCallOutputAsync(callId, outputJson, _sessionCt);
            await _realtime.SendResponseCreateAsync(_sessionCt);
            realtimeSendOk = true;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn($"[ToolExecutor/{_sessionId}] function_call_output send cancelled (call_id={callId}) — session ending");
        }
        catch (InvalidOperationException ex)
        {
            // Realtime client disposed or WS closed between dispatch and send.
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [ToolExecutor/{_sessionId}] function_call_output send invalid state (call_id={callId}): {ex.Message}");
        }

        if (!realtimeSendOk)
        {
            // Iter 2 (CQ1/CQ10 fix): do NOT emit tool_call_completed when the model never received
            // function_call_output / response.create. Emitting it would tell the browser the call
            // succeeded while the conversation is actually stuck — a misleading UX state. The
            // session is going down (logs already captured the failure for ops diagnostics).
            return;
        }

        await SendHudAsync(new
        {
            type = "tool_call_completed",
            call_id = callId,
            name,
            duration_ms = durationMs,
            result_count = resultCount,
            status,
            error_code = errorCode
        });
    }

    private async Task SendHudAsync(object payload)
    {
        try
        {
            await _sendHudFrame(payload, _sessionCt);
        }
        catch (OperationCanceledException) { /* session ending — HUD best-effort */ }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[ToolExecutor/{_sessionId}] HUD frame send invalid state: {ex.Message}");
        }
    }

    private static string BuildErrorJson(string error, string code, string message)
    {
        var payload = new { error, code, message };
        return JsonSerializer.Serialize(payload, ErrorJsonOpts);
    }

    private static string ArgsPreview(string args)
    {
        if (string.IsNullOrEmpty(args)) return string.Empty;
        // Inline whitespace/newlines so the rozet stays one-liner; trim to the privacy limit
        // (the args string is also visible in jsonl for full debugging).
        // CQ10 fix: budget the "..." suffix INSIDE the 120-char cap (was 120 + "..." = 123 chars,
        // exceeding the documented contract). Truncation point is ArgsPreviewMaxChars - 3.
        var flat = args.Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length <= ArgsPreviewMaxChars ? flat : flat[..ArgsPreviewTruncateBudget] + "...";
    }

    /// <summary>
    /// Iter 2 (CQ6 fix): dispose the CancellationTokenRegistration so the callback handle does not
    /// retain a reference to this executor instance past WS session end. Idempotent via Interlocked
    /// guard — orchestrator may dispose more than once (e.g., once on session-end and once via
    /// `using` block) and we want both to be safe.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _sessionEndCleanup.Dispose();
        _argBuffers.Clear();
    }
}
