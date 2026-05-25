# Browser ↔ VoiceRuntime WebSocket Protocol — F0 + F0.5

> **Status:** F0 frozen (2026-05-24). F0.5 Chunks A (handshake gate) + B (server-side context fetch) + C (function calling + ToolExecutor) + D (browser dropdown + HUD + URL-bridge JWT) shipped 2026-05-26. Chunk E (CORS allowlist + redeploy) in progress 2026-05-26.
> **Endpoint:** `wss://<host>/ws/voice/microphone`
> **Spec ref:** [SPEC-008 §4](../../specs/voice-flow-builder.md), [FEAT-VFB F0 plan](../../plans/20260523-feat-vfb-f0-poc.json), [F0.5 plan](../../plans/20260525-feat-vfb-f0-5-tenant-aware-voice-test.json)

---

## Handshake

```
F0 legacy (microphone sales demo, sysadmin self-test):
GET /ws/voice/microphone?token=<sysadmin JWT>&locale=tr-TR
GET /ws/voice/microphone?dev=1&locale=tr-TR    (development bypass, only when Jwt:SecretKey empty)

F0.5 tenant-aware Voice Test (AD-21, sysadmin impersonates a tenant):
GET /ws/voice/microphone?token=<sysadmin JWT>&tenant_id=<target>&flow_id=<flow>&locale=tr-TR
```

### Query parameters

| Name        | Required        | Notes |
|-------------|-----------------|-------|
| `token`     | prod (yes)      | JWT issued by Invekto.Backend. F0.5 mode: `tenant_id` claim must be `0` (sysadmin); non-sysadmin callers are rejected with `INV-VR-020`. |
| `dev`       | dev only        | `dev=1` enables bypass when running with `ASPNETCORE_ENVIRONMENT=Development` AND empty `Jwt:SecretKey`. Only valid in F0 legacy mode. |
| `tenant_id` | F0.5 mode       | **Presence-based mode switch** — once the query key is present (even empty), strict F0.5 validation applies. Positive integer; `0` rejected (self-impersonation) with `INV-VR-013`; non-int with `INV-VR-011`. |
| `flow_id`   | F0.5 mode       | Required whenever `tenant_id` is present (presence-based). Positive integer; missing/invalid yields `INV-VR-012`. |
| `locale`    | optional        | BCP-47, default `tr-TR`. Used to set Realtime voice instructions language. |

### F0.5 Lifecycle (Chunks A + B, server-side, before audio loops start)

1. **Origin gate** — `Cors:AllowedOrigins` match required.
2. **JWT validate** — `token` must be a sysadmin JWT (`tenant_id == 0`). Non-sysadmin → `INV-VR-020`.
3. **Mode detection** — presence of `tenant_id` OR `flow_id` query keys triggers strict F0.5 validation chain (`INV-VR-011/012/013`).
4. **WS accept** — `101 Switching Protocols`.
5. **Context fetch (Chunk B, F0.5 only):** VoiceRuntime issues two short-lived (5min) service JWTs and fetches the impersonation target in parallel:
   - `GET /api/ops/tenants` (Backend, admin-scope JWT `JwtGenerator.GenerateToken(0, "admin", "voice_runtime", 5min)`) → tenant lookup by id.
   - `GET /api/v1/flows/{tenantId}/{flowId}` (Automation, per-tenant service JWT `JwtGenerator.GenerateServiceToken(targetTenantId)`).
   - Browser-supplied tenant/flow display values are NEVER trusted (AD-25 defense-in-depth) — VoiceRuntime always re-fetches authoritative `tenant_name` + `sector` + `flow_name`.
6. **Failure handling** — any fetch fail / 404 / not found sends a single error control frame (`type:"error", code, message`) and closes the WS with `1011 InternalServerError`. See "Reserved error codes" below for the code mapping.
7. **Context build** — `VoiceTestContext` populated; `descriptor.ProviderMetadata` enriched with `tenant_name`, `sector`, `flow_name` (audit trail); `InstructionsBuilder.Build(ctx)` previewed in jsonl. Chunk C wires the rendered instructions into `session.update`.
8. **Realtime connect + audio loops start** (existing F0 flow).

### Error responses (pre-accept)

| HTTP | Code | Meaning |
|------|------|---------|
| 400  | —    | Not a WebSocket upgrade request |
| 400  | INV-VR-011 | F0.5 `tenant_id` missing or non-positive integer |
| 400  | INV-VR-012 | F0.5 `flow_id` missing or non-positive integer |
| 400  | INV-VR-013 | F0.5 `tenant_id=0` (sysadmin self-impersonation forbidden) |
| 401  | INV-VR-004 | Missing/invalid JWT (prod) or dev bypass not allowed |
| 403  | INV-VR-020 | F0.5 caller JWT is not sysadmin (`tenant_id != 0`) — Voice Test ops-only |
| 403  | —          | Origin not in `Cors:AllowedOrigins` |

### Error responses (post-accept, via control frame + WS close 1011)

| Control code | Meaning | Recovery |
|--------------|---------|----------|
| INV-VR-014   | Backend `/api/ops/tenants` HTTP error (5xx/network/auth) | Retry handshake; check Backend health |
| INV-VR-015   | Automation `/api/v1/flows/{tid}/{fid}` HTTP error | Retry handshake; check Automation health |
| INV-VR-018   | Service JWT mint failed | Check `Jwt:SecretKey` config — corruption indicator |
| INV-VR-021   | Target tenant not found in active list (deleted/inactive after dropdown render) | Refresh tenant dropdown |
| INV-VR-022   | Target flow not found for tenant (deleted/renamed after dropdown render) | Refresh flow dropdown |

---

## Frame Format

After handshake the connection carries two frame types:

### Binary frames — audio payload

Both directions use the **same format**: raw PCM16 LE 48kHz mono, 20ms frames = **1920 bytes per frame** (960 Int16 samples).

- **Browser → Server:** microphone audio (captured by AudioWorklet at 48k mono, 20ms accumulation buffer).
- **Server → Browser:** bot voice (Realtime response.audio.delta after PcmResampler 24→48 upsample).

> F2 note: When Opus is enabled (config flag), this format will be Opus packet bytes instead. Frame size becomes 1-1275 bytes (Opus variable-length).

### Text frames — control JSON

UTF-8 JSON objects, snake_case fields. Frame type determined by `type` field.

---

## Control Messages — Server → Browser

### `ready`

Sent once after Realtime session is created.

```json
{ "type": "ready", "session_id": "f0-3a2b1c..." }
```

### `transcript_user`

User speech transcription completed (Whisper round-trip).

```json
{ "type": "transcript_user", "text": "Saç ekimi fiyatlarınız nedir?" }
```

### `transcript_bot`

Streaming bot response transcript (per token delta).

```json
{ "type": "transcript_bot", "delta": "Saç ekimi" }
```

### `first_byte`

First audio chunk delivered from Realtime (latency milestone).

```json
{ "type": "first_byte", "elapsed_ms": 820 }
```

`elapsed_ms` = time from user speech end → first bot audio byte (sub-saniye target <1000ms).

### `barge_in`

User interrupted bot. TTS cancellation acknowledged.

```json
{ "type": "barge_in", "elapsed_ms": 240 }
```

`elapsed_ms` = time from barge-in detected → TTS stop signal (target <250ms).

### `response_done`

Realtime finished current response. Bot is silent until user speaks again.

```json
{ "type": "response_done" }
```

### `tool_call_started` (F0.5 Chunk C, AD-29)

Model emitted `response.function_call_arguments.done` — execution about to begin. Frame fires
**immediately** so the UI can show a "çalışıyor" rozet during slow KB roundtrips (up to 5s).

```json
{
  "type": "tool_call_started",
  "call_id": "call_abc123",
  "name": "search_knowledge_base",
  "args_preview": "{\"query\":\"saç ekimi dinlenme\",\"top_k\":3}"
}
```

`args_preview` is truncated to ≤120 chars with `...` suffix when longer (full args go to jsonl).
Newlines collapsed to spaces. Privacy: search query is also audible in the user transcript, so
no hashing required.

### `tool_call_completed` (F0.5 Chunk C, AD-29)

Tool execution finished — fires after `function_call_output` + `response.create` have been
**enqueued for delivery** to the Realtime WebSocket send loop (local channel buffer). The frame
is SUPPRESSED entirely when either enqueue raises an OperationCanceledException (session ending)
or InvalidOperationException (Realtime client disposed) — the browser keeps the "started" rozet
in that case so it never shows a misleading "completed" badge for a payload the model could not
receive. Browser updates the same rozet with duration + result count.

> **Honesty note (F4 backlog):** The current contract guarantees LOCAL enqueue success, not
> server-side ACK from OpenAI / model. F4 will gate this frame on the upstream `response.created`
> event so the rozet flips to "completed" only after the model has actually started a new
> response — providing true end-to-end truth correlation. F0.5 + F2 + F3 keep the enqueue-based
> guarantee because all other outbound Realtime events follow the same pattern.

```json
{
  "type": "tool_call_completed",
  "call_id": "call_abc123",
  "name": "search_knowledge_base",
  "duration_ms": 420,
  "result_count": 3,
  "status": "ok",
  "error_code": null
}
```

Error variant (`status: "error"`):

```json
{
  "type": "tool_call_completed",
  "call_id": "call_xyz789",
  "name": "search_knowledge_base",
  "duration_ms": 5042,
  "result_count": 0,
  "status": "error",
  "error_code": "INV-VR-017"
}
```

Possible `error_code` values: `INV-VR-016` (args parse / executor exception), `INV-VR-017`
(KB HTTP/timeout — model gets `kb_unavailable` JSON), `INV-VR-023` (unknown tool name).

### `error`

Surface for unrecoverable errors. Browser shows toast + status indicator.

```json
{
  "type": "error",
  "code": "INV-VR-003",
  "message": "OpenAI Realtime rate limit hit",
  "realtime_code": "rate_limit_exceeded"
}
```

`code` is canonical INV-VR-* from `arch/errors.md`. `realtime_code` is the upstream OpenAI code if available.

---

## Control Messages — Browser → Server (F0 reserved)

F0 PoC does not require browser-originated control messages. Future:

```json
{ "type": "ping" }
{ "type": "stop" }
{ "type": "user_metadata", "name": "Q", "tenant_locale_override": "en-US" }
```

Server logs but does not act on these in F0.

---

## Sequence Diagram (canonical happy path)

```
Browser                                  VoiceRuntime                  OpenAI Realtime
   │                                          │                              │
   │ WS GET /ws/voice/microphone?dev=1        │                              │
   │─────────────────────────────────────────→│                              │
   │           (101 Switching Protocols)      │                              │
   │←─────────────────────────────────────────│                              │
   │                                          │ WSS connect (auth header)    │
   │                                          │─────────────────────────────→│
   │                                          │ session.update               │
   │                                          │─────────────────────────────→│
   │ TEXT { "type":"ready", ... }             │                              │
   │←─────────────────────────────────────────│                              │
   │                                          │                              │
   │ BIN  [PCM16 LE 1920 bytes] (20ms frame)  │                              │
   │─────────────────────────────────────────→│ input_audio_buffer.append    │
   │ BIN  [PCM16 LE 1920 bytes]               │ (resample 48→24, base64)     │
   │─────────────────────────────────────────→│─────────────────────────────→│
   │ ...                                      │                              │
   │                                          │ input_audio_buffer           │
   │                                          │  .speech_started             │
   │                                          │←─────────────────────────────│
   │                                          │                              │
   │ (user stops speaking, VAD silence 200ms) │                              │
   │                                          │ input_audio_buffer           │
   │                                          │  .speech_stopped             │
   │                                          │←─────────────────────────────│
   │                                          │ (semantic_vad → response.create auto) │
   │                                          │                              │
   │ TEXT { "type":"transcript_user", ... }   │                              │
   │←─────────────────────────────────────────│                              │
   │                                          │ response.audio.delta         │
   │                                          │←─────────────────────────────│
   │ TEXT { "type":"first_byte", ... }        │                              │
   │←─────────────────────────────────────────│                              │
   │ BIN  [PCM16 LE 1920 bytes] (bot voice)   │                              │
   │←─────────────────────────────────────────│                              │
   │ TEXT { "type":"transcript_bot", ... }    │                              │
   │←─────────────────────────────────────────│                              │
   │ ... (audio stream continues)             │                              │
   │                                          │ response.done                │
   │                                          │←─────────────────────────────│
   │ TEXT { "type":"response_done" }          │                              │
   │←─────────────────────────────────────────│                              │
   │                                          │                              │
   │ WS Close (browser stops mic)             │                              │
   │─────────────────────────────────────────→│ WS close to OpenAI           │
   │                                          │─────────────────────────────→│
```

---

## Barge-in Sub-flow

When the browser keeps sending audio while bot voice is being delivered:

1. Server's `OnAudioDelta` sets `_botSpeaking = true` and timestamps `TtsFirstByteToUser`.
2. New incoming PCM frames are decoded → VAD prob > 0.5 → if `_userSpeaking` flips false→true, `StampSpeechStart` is recorded.
3. **OR** Realtime's own `input_audio_buffer.speech_started` arrives → server checks `_botSpeaking`. If true:
   - Sends `response.cancel` to Realtime
   - Calls `MicrophoneCallSession.SignalBargeInAsync()` (drains outbound queue → browser stops receiving bot audio)
   - Stamps `BargeInTtsStopped` and emits TEXT `{ "type": "barge_in", "elapsed_ms": <n> }`

F0 target: `barge_in.elapsed_ms < 500ms` (browser overhead allowed). F2 PBX path: `<250ms` (spec AC-4).

---

## Browser acquisition & UI (F0.5 Chunk D)

The Voice Test page (`https://voice.invekto.com:8443/voice-poc.html`) is opened from a
Dashboard wrapper on `https://app.invekto.com`. Because the two share no localStorage
origin, the page acquires its Dashboard JWT through a one-shot URL bridge and uses the
same token for both the dropdown fetches and the WS handshake.

### Token acquisition (URL-bridge, AD-33)

1. Dashboard wrapper opens `https://voice.invekto.com:8443/voice-poc.html?token=<JWT>`.
2. Inline `<script>` in `voice-poc.html` reads `URLSearchParams.get("token")`, assigns
   it to `window.INVEKTO_VOICE_JWT` (page-lifetime global), and immediately calls
   `history.replaceState(null, "", location.pathname)` to strip the token from the
   address bar (browser history / refresh leak prevention).
3. `voice-poc.js` reads `window.INVEKTO_VOICE_JWT` for two consumers:
   - Bearer header on dropdown fetches (`/api/ops/tenants`, `/api/v1/flows/{tenantId}`).
   - `?token=<JWT>` query parameter on the WS handshake.
4. If the token is missing the page renders a disabled state (toast `INV-VR-CLIENT-001`,
   both dropdowns disabled, Mic button disabled). No localStorage fallback —
   `localStorage` is per-origin and not shared with `app.invekto.com`.

### Cross-origin dropdown fetch (CORS prerequisite, AD-35 / Chunk E)

Both dropdown fetches are cross-origin (`voice.invekto.com:8443` → backend origins).
Backend (`/api/ops/tenants`) and Automation (`/api/v1/flows/{tenantId}`) MUST allow the
Voice Test origin via the `VoicePocCors` named policy:

```csharp
// Backend / Automation Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("VoicePocCors", policy => policy
        .WithOrigins("https://voice.invekto.com:8443")
        .WithMethods("GET")
        .AllowAnyHeader());
});
// Pipeline: app.UseCors() MUST be before app.UseJwtAuth(...) so preflight OPTIONS
// requests reach the CORS middleware before the auth gate rejects them.

// Endpoint binding (explicit, no global default policy):
app.MapGet("/api/ops/tenants", ...).RequireCors("VoicePocCors");
app.MapGet("/api/v1/flows/{tenantId:int}", ...).RequireCors("VoicePocCors");
```

Knowledge is **not** in the allowlist for F0.5 — the `search_knowledge_base` tool runs
inside VoiceRuntime with a service JWT (server-to-server, no browser preflight). It will
be added when the F4 voice-analytics dashboard widgets begin calling Knowledge directly.

### Selection persistence (AHA-4, AD-34)

After a successful dropdown fetch the page writes `voice-poc-tenant-id` and
`voice-poc-flow-id` into `localStorage` (same-origin, no cross-origin concern). On the
next load it parses each value with the strict pattern `/^[1-9][0-9]*$/` (rejects
`12abc`, `1e3`, `0x10`, leading zeros, negatives) and only restores the selection when
the value is still present in the freshly fetched options. Corrupted or stale values
fall back to `selectedIndex = 0` and the Mic button stays disabled until the user picks
explicitly.

### HUD rendering rules (AD-32, paired with `tool_call_*` frames)

The Voice Test workspace uses a sticky two-column layout (transcript on the left, tool
call panel on the right) and collapses to stacked above each other below 800px viewport
width. Tool call rozets are kept to the last 5 (DOM rolling cap, oldest pruned) to
mirror the existing transcript trim policy.

| Frame                  | DOM action                                                              |
|------------------------|-------------------------------------------------------------------------|
| `tool_call_started`    | Append rozet with state `pending` (yellow pulse), text = `name(args_preview)`. |
| `tool_call_completed`  | Locate rozet by `call_id`; flip to `ok` (green) or `error` (red); append `duration_ms` and `result_count`; on `status:"error"` show `error_code`. |
| `tool_call_completed` with no matching `call_id` | Log `INV-VR-CLIENT-006` (out-of-order or duplicate) and skip render — never spawn a stray "completed" rozet. |

A rozet that received only `tool_call_started` (no completion frame) is intentional —
see the Honesty note under `tool_call_completed` above. The browser does not
auto-collapse pending rozets to "error" on timeout; it leaves them visible so the user
can see that the model never finished delivering the function call output.

### Browser-side diagnostic codes (display-only)

`INV-VR-CLIENT-001..010` are browser-side display codes (see [arch/errors.md](../../errors.md))
used in toasts and `setStatus()` calls when client-side paths fail (token missing,
localStorage corrupted, fetch network error, AudioContext failure, AudioWorklet load
failure, etc.). They are disjoint from the server `INV-VR-001..023` range and never
leave the browser — server logs use the server codes for the same events when they
originate server-side.

---

## Reserved error codes

| Code | Triggered when |
|------|----------------|
| INV-VR-001 | Realtime WS connect failed (DNS, TLS, network) |
| INV-VR-002 | Realtime auth failed (key revoked, tier missing) |
| INV-VR-003 | Realtime rate limit / outage |
| INV-VR-004 | Browser WS handshake auth rejection |
| INV-VR-005 | Opus decode failed (F2 — Opus mode only) |
| INV-VR-006 | Opus encode failed (F2 — Opus mode only) |
| INV-VR-007 | Silero VAD inference exception |
| INV-VR-008 | VAD model file missing/corrupt |
| INV-VR-009 | Browser microphone permission denied |
| INV-VR-010 | F0 latency budget exceeded (non-fatal warning) |
| INV-VR-011 | F0.5 handshake `tenant_id` missing/invalid |
| INV-VR-012 | F0.5 handshake `flow_id` missing/invalid |
| INV-VR-013 | F0.5 handshake `tenant_id=0` (self-impersonation rejected) |
| INV-VR-014 | Backend `/api/ops/tenants` fetch failed (post-accept) |
| INV-VR-015 | Automation flow fetch failed (post-accept) |
| INV-VR-016 | Realtime function_call dispatch failed (Chunk C) |
| INV-VR-017 | Knowledge tool execution failed (Chunk C) |
| INV-VR-018 | Service JWT mint failed |
| INV-VR-019 | Tool `top_k` clamped (non-fatal log) |
| INV-VR-020 | F0.5 impersonation gate — non-sysadmin caller |
| INV-VR-021 | F0.5 target tenant not found in active list |
| INV-VR-022 | F0.5 target flow not found for tenant |
| INV-VR-023 | F0.5 Chunk C — model called an unregistered tool name (non-fatal, structured error sent) |
