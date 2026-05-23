# Browser ↔ VoiceRuntime WebSocket Protocol — F0 PoC

> **Status:** F0 frozen (2026-05-24). F2 may add Opus encoding alongside (config-driven).
> **Endpoint:** `wss://<host>/ws/voice/microphone`
> **Spec ref:** [SPEC-008 §4](../../specs/voice-flow-builder.md), [FEAT-VFB F0 plan](../../plans/20260523-feat-vfb-f0-poc.json)

---

## Handshake

```
GET /ws/voice/microphone?token=<JWT>&locale=tr-TR
GET /ws/voice/microphone?dev=1&locale=tr-TR    (development bypass, only when Jwt:SecretKey empty)
```

### Query parameters

| Name      | Required        | Notes |
|-----------|-----------------|-------|
| `token`   | prod (yes)      | JWT issued by Invekto.Backend. Tenant claim drives `tenant_id` context. |
| `dev`     | dev only        | `dev=1` enables bypass when running with `ASPNETCORE_ENVIRONMENT=Development` AND empty `Jwt:SecretKey`. |
| `locale`  | optional        | BCP-47, default `tr-TR`. Used to set Realtime voice instructions language. |

### Error responses

| HTTP | Code | Meaning |
|------|------|---------|
| 400  | —    | Not a WebSocket upgrade request |
| 401  | INV-VR-004 | Missing/invalid JWT (prod) or dev bypass not allowed |

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
