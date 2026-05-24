# FEAT-VFB — F0 Mikrofon → F2 Generic SIP PBX + F3 WhatsApp Calling Production Geçiş Rehberi

> **Spec:** [SPEC-008 Voice Flow Builder](../specs/voice-flow-builder.md) | **Faz haritası:** F0 (mic PoC, DONE) → F1 (WA voice note, 1hf) → **F2 Generic SIP PBX MVP (3-4hf)** → **F3 WA Calling multi-tenant (2hf)** → F4 optim (1hf)

Bu doküman F0 PoC'deki mikrofon ortamından gerçek production'a — **herhangi bir SIP PBX** (Asterisk, FreeSWITCH, FreePBX, 3CX, Issabel, Yeastar, Grandstream, Cisco CUCM, vs.) **+ WhatsApp Business Calling** — geçiş süreçlerini, kanalların adım adım akışlarıyla anlatır.

**Önemli scope değişikliği:** F2 paketi Toniva-specific gRPC bridge yerine **Generic SIP PBX integration** olarak genişletildi. Toniva (Q'nun kendi sistemi) sadece bir uygulama örneği. Müşterilerin Asterisk-based PBX'i (en yaygın), 3CX, FreePBX, Issabel ve diğerleri **out-of-the-box** desteklenir.

---

## TEMEL MİMARİ — Tek Orchestrator, 3 Provider Tipi

```
                  ┌─────────────────────────────────────────┐
                  │   Invekto.VoiceRuntime (:7115)          │
                  │   ─────────────────────────────────     │
                  │   VoicePocOrchestrator (per-call)       │
                  │   ├─ SileroVad + Smart-Turn v2 (F2)     │
                  │   ├─ RealtimeApiClient (WS bidi)        │
                  │   ├─ FlowBridge → Automation HTTP       │
                  │   └─ TtsRouter (Realtime / Azure /      │
                  │       ElevenLabs tenant-selectable)     │
                  └────────────┬────────────┬───────────────┘
                               │            │
              ┌────────────────┘            └───────────────────┐
              │                                                 │
   ┌──────────▼──────────┐                          ┌──────────▼──────────┐
   │ IVoiceCallProvider  │                          │ IVoiceCallProvider  │
   │ Microphone (F0)     │                          │ SipPbx (F2) /       │
   │ Browser WebRTC      │                          │ WhatsAppCall (F3)   │
   └─────────────────────┘                          └─────────────────────┘
                                                                │
                                  ┌─────────────────────────────┴─────────────────────────────┐
                                  │  SipPbx (F2) — 2 Mod                                       │
                                  │  ──────────────                                            │
                                  │  Mod A: AudioSocket Bridge (önerilen, en geniş uyum)       │
                                  │    • Asterisk / FreePBX / Issabel / 3CX                    │
                                  │    • TCP raw audio 8k/16k mono                             │
                                  │    • Müşteri dialplan'a 1 satır ekler                      │
                                  │                                                            │
                                  │  Mod B: Direct SIP UA (gelişmiş, native SIP)               │
                                  │    • SIPSorcery .NET (NuGet, BSD)                          │
                                  │    • VoiceRuntime SIP user agent                           │
                                  │    • PBX peer/trunk extension olarak ekler                 │
                                  │                                                            │
                                  │  Mod C: Toniva (Q kendi sistemi — opsiyonel)               │
                                  │    • gRPC bidi proto (proto FROZEN)                        │
                                  │    • Q'nun C++ Toniva implementation'ı                     │
                                  └─────────────────────────────────────────────────────────────┘
```

**Anahtar prensip:** `IVoiceCallProvider` + `IVoiceCallSession` Shared abstraction'ları sayesinde orchestrator **provider'dan habersizdir**. F0 mikrofon, F2 SIP PBX (3 mod), F3 WhatsApp aynı orchestrator'a aynı pattern'le bağlanır.

| Provider | F0 Microphone | F2 SIP PBX (Mod A) | F2 SIP PBX (Mod B) | F2 Toniva (Mod C) | F3 WhatsAppCall |
|----------|---------------|----|----|----|------------------|
| Transport | Browser WebSocket | TCP AudioSocket (raw PCM) | UDP RTP + UDP SIP | gRPC bidi | Meta Cloud Media WSS |
| PBX gereksinim | yok | Asterisk-based (Asterisk, FreePBX, Issabel, vs.) | Herhangi bir SIP PBX (peer/trunk register destekli) | Q'nun Toniva (C++ özel) | yok (Meta Cloud) |
| Müşteri kurulum effort | yok | **10 dk** (dialplan 1 satır) | 30 dk (SIP trunk config) | Q internal | yok |
| Audio codec | PCM16 48k LE | PCM 8k/16k mono (Asterisk SLIN16) | Opus/G.711 (codec nego) | Opus 48k mono 20ms | Opus 48k mono 20ms |
| NAT/Firewall | yok (LAN) | Outbound TCP only (NAT-friendly) | SIP+RTP UDP (NAT keepalive gerek) | yok (Q LAN) | Outbound WSS only |
| Call ID kaynağı | `f0-{guid}` | AudioSocket UUID v4 | SIP Call-ID header | gRPC `CallStart.call_id` | Meta `call_id` |
| Tenant kaynağı | JWT tenant=0 | Dialplan parametresi `tenant_id` | SIP X-Header `X-Invekto-Tenant-Id` | gRPC `CallStart.tenant_id` | Webhook `tenant_id` |
| Caller ID | yok | Dialplan `CALLERID(num)` | SIP From header | gRPC | Meta `call.from` |
| Hangup | WS close | AudioSocket FIN | SIP BYE | gRPC `Hangup` | Webhook `call.terminate` |
| Transfer (insana bağla) | no-op | Dialplan ARI redirect | SIP REFER | gRPC `AgentTransferRequest` | Webhook (Meta API TBD) |
| KVKK consent | yok | Zorunlu (anons + DTMF 9) | Zorunlu (anons + DTMF 9) | Zorunlu | Zorunlu (anons + intent "iptal") |

---

## YOL HARİTASI — F0 mikrofon → F2 + F3

| Faz | Süre | Kanal | Müşteri tarafı paralel iş | Invekto tarafı |
|-----|------|-------|---------------------------|----------------|
| **F0** | DONE | Browser mikrofon | Q live smoke + customer demo | VoiceRuntime skelet + Realtime + browser PoC + 15 senaryo |
| **F1** | 1hf | WhatsApp voice note (async) | — | Automation audio filter relax + VoiceAI transcribe-inject endpoint |
| **F2 (1)** | 0-3gun | Pre-req: AudioSocket implementation | — | `AudioSocketProvider` (TCP server :8090, raw PCM frame protocol) + `SipPbxProvider` IVoiceCallProvider |
| **F2 (2)** | 1hf | DB + KVKK + Recording | — | Migration 050 (call_sessions + voice_turns + callback_requests + tenant_settings.voice_*) + AES-GCM + S3 storage + KMS keys |
| **F2 (3)** | 1hf | Flow nodes + Dashboard | — | 5 voice node FlowEngineV2 + Dashboard /calls + /flow-builder voice palette + /settings/voice-runtime + TTS provider DI |
| **F2 (4)** | 0-2gun | Müşteri PBX onboarding doc | Müşteri Asterisk/FreePBX dialplan'a 1 satır ekler | "Voice AI extension setup guide" Türkçe Dashboard'da PDF |
| **F2 (5)** | 3gun | Pilot smoke | Müşteri test çağrı (Dent Adavista veya Q internal) | Tek tenant pilot smoke (gerçek inbound call) |
| **F2-opt** | 1hf (opsiyonel) | Mod B Direct SIP UA | — | SIPSorcery NuGet integration (müşteri PBX peer/trunk peer'i ister) |
| **F3 (1)** | 1hf | Pre-req: Q Meta BM submit | Meta Business Manager → WA Cloud Calling allowlist (4 hafta) | `WhatsAppCallProvider` impl |
| **F3 (2)** | 1hf | Meta webhook + media WS | — | Integrations adapter + media WebSocket + feature flag `voice_calling_enabled` |
| **F3 (3)** | 3gun | Multi-tenant smoke | Q 2 tenant test | Pilot smoke + tenant_settings.voice_call_provider enum |
| **F4** | 1hf | Optim + analytics | — | Full-duplex barge-in + RI Insight Engine 8. modul + Dashboard /reports/voice-perf + Hangfire daily aggregation |

---

## ADIM ADIM AKIŞLAR

### 1. F0 Microphone Akışı (REFERANS — DONE)

> Bkz. [feat-vfb-f0-smoke-scenarios.md](../reports/feat-vfb-f0-smoke-scenarios.md) "STANDART AKIŞ" bölümü S1-S10 + T1-T22 + C1-C7.

Bu akış F2/F3'te **VoicePocOrchestrator** olarak aynen kalır. Tek değişen: Browser yerine SIP PBX / WhatsApp provider.

---

### 2. F2 — Generic SIP PBX (Mod A: AudioSocket Bridge — ÖNERİLEN)

#### Genel bakış

**AudioSocket** Asterisk'in basit ama güçlü TCP audio protokolüdür. Asterisk-based PBX'lerin (FreePBX, Issabel, 3CX Pro, Yeastar S serisi, Grandstream UCM) hemen hemen hepsinde mevcut. Müşterinin yapması gereken **dialplan'a tek satır** eklemek:

```asterisk
; /etc/asterisk/extensions.conf veya FreePBX Misc Destinations
[invekto-voice-ai]
exten => _X.,1,NoOp(Voice AI inbound: ${CALLERID(num)})
 same => n,Set(__INVEKTO_TENANT_ID=18173130)         ; ← Müşteri tenant ID'sini buraya
 same => n,Set(__INVEKTO_LOCALE=tr-TR)
 same => n,Answer()
 same => n,AudioSocket(${UNIQUEID},voiceruntime.invekto.com:8090)
 same => n,Hangup()
```

**Tek satır gerçek değişiklik:** `AudioSocket(...)` çağrısı. Diğer satırlar header/setup. Müşterinin PBX admin'inin 10 dakikada eklemesi yeterli.

#### A. Pre-Call (müşteri kurulum)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **P1** | Müşteri PBX admin | Dashboard `/settings/voice-runtime` → Tenant: "PBX entegrasyonu" → Mod A seç → "Setup talimatlarını indir" (PDF) | Türkçe step-by-step PDF + dialplan snippet'i tenant_id ile özelleştirilmiş |
| **P2** | Müşteri PBX admin | Asterisk/FreePBX dialplan'a snippet'i yapıştır → `dialplan reload` veya FreePBX "Apply Config" | Dialplan extension aktif |
| **P3** | Müşteri PBX admin | Inbound route ekle: gelen DID/SIP trunk → `[invekto-voice-ai]` context | Telefon numarası AI'ya yönlendi |
| **P4** | Müşteri PBX admin | Test çağrısı (kendi cep telefonundan) | PBX log "AudioSocket connected to voiceruntime.invekto.com:8090" |
| **P5** | VoiceRuntime | `:8090` AudioSocket TCP listener (`AudioSocketProvider`) → connection accept | Test connection success |

#### B. Inbound Call (telefon çaldığı an)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **C1** | Caller | Müşteri DID numarasını arar | SIP INVITE PBX trunk'a |
| **C2** | PBX | Inbound route match → `[invekto-voice-ai]` dialplan execute | `Answer()` → caller'a 200 OK |
| **C3** | PBX | `AudioSocket(UUID, voiceruntime.invekto.com:8090)` execute | TCP connect VoiceRuntime |
| **C4** | PBX → VoiceRuntime | AudioSocket protocol handshake: Type 0x01 UUID frame (16 byte payload) | VoiceRuntime UUID al = call_id |
| **C5** | VoiceRuntime | TCP connection accept → `AudioSocketProvider.OpenSessionAsync(descriptor)` | descriptor: tenant_id (PBX env var'dan), caller_id_hash (CALLERID), locale, started_at, provider='sip_pbx_audiosocket' |
| **C6** | VoiceRuntime | `RealtimeApiClient.ConnectAsync` + tenant-specific session config | OpenAI WS open |
| **C7** | VoiceRuntime | `FlowBridge.LookupTriggerFlow(tenant_id, kanal='pbx')` HTTP → Automation | flow_id + welcome_node |
| **C8** | VoiceRuntime | KVKK consent metni `voice_say` → TTS audio out → AudioSocket frame back to PBX | Bot anonsu: "Bu görüşme yapay zeka tarafından karşılanıyor..." |
| **C9** | VoiceRuntime | `call_sessions` INSERT (consent_played_at, started_at, tenant_id, provider='sip_pbx_audiosocket', caller_id_hash, flow_id) | DB row + KVKK audit |

#### C. Live Conversation (browser akışıyla AYNI orchestrator)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **L1** | Caller | Konuşma → PBX RTP frame al → Asterisk PCM 8k/16k mono decode | PBX internal |
| **L2** | PBX | AudioSocket Type 0x10 frame: header (3 byte) + PCM 8k/16k payload (320 byte = 20ms @ 16k SLIN16) | TCP send → VoiceRuntime |
| **L3** | VoiceRuntime | `AudioSocketProvider.ReadFrameAsync` → `OpusFrame` (Opus encode opsiyonel, F0/F2 raw PCM kullanır) → `session.PushIncomingAsync()` | Bounded Channel(200) |
| **L4** | VoiceRuntime | `VoiceToRealtimeForwardLoop` (F0 ile aynı): PCM 16k → SileroVad (zaten 16k native) → Smart-Turn v2 EOT → Resample 16→24 → base64 → Realtime SendAudio | Standart pipeline |
| **L5** | OpenAI Realtime | LLM response.audio.delta stream (PCM 24k base64) | Standart |
| **L6** | VoiceRuntime | `OnAudioDelta`: base64 → PCM24k → Resample 24→16 → PCM 16k LE bytes → AudioSocket Type 0x10 frame (320 byte payload) | Outbound TCP |
| **L7** | PBX | AudioSocket frame al → PCM decode → RTP encode → caller'a stream | Bot sesi telefon hattında |
| **L8** | VoiceRuntime | Her turn için `voice_turns` INSERT | AES-GCM encrypted |

#### D. DTMF (KVKK opt-out + sayı tuşları)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **D1** | Caller | "9" tuşuna bas | PBX DTMF event |
| **D2** | PBX | AudioSocket Type 0x03 DTMF frame: header + key byte | TCP send |
| **D3** | VoiceRuntime | `AudioSocketProvider.ReadFrameAsync` Type 0x03 detect → `OnDtmf("9")` event | Opt-out trigger |
| **D4** | VoiceRuntime | `voice_trigger` exit `opt_out` edge → transfer or disconnect | KVKK consent withdraw |
| **D5** | VoiceRuntime | `call_sessions.opt_out=true` UPDATE + `VoiceOptOutPurgeJob` Hangfire enqueue (24h sonra transcript NULL) | KVKK retention |

#### E. Human Transfer (voice_transfer node)

> AudioSocket protokolü kendisi REFER desteklemez. İki çözüm:

| Çözüm | Detay |
|-------|-------|
| **E.1 — ARI Redirect (önerilen)** | VoiceRuntime → Asterisk ARI HTTP `POST /ari/channels/{channelId}/redirect?endpoint=PJSIP/agent_extension` |
| **E.2 — AudioSocket Hangup + Callback** | AudioSocket session close + Hangfire CallbackRequestJob enqueue |

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **T1** | VoiceRuntime | Bot anons: "Sizi temsilciye yönlendiriyorum" (TTS bitene kadar bekle) | Caller'a anons |
| **T2** | VoiceRuntime | (E.1 yolu) HTTP POST Asterisk ARI `/ari/channels/{UUID}/redirect?endpoint=PJSIP/agent_dent_eu` (BasicAuth ARI user/pass tenant_settings'te) | Asterisk redirect |
| **T3** | PBX | Caller channel agent extension'a redirect | SIP REFER veya Dial() |
| **T4** | VoiceRuntime | `call_sessions.transferred_at` UPDATE, `outcome='transferred'` | DB |
| **T2b** | VoiceRuntime | (E.2 alternatif — ARI yoksa) AudioSocket FIN + Hangfire CallbackRequestJob "5dk sonra SMS+WA" | Async callback |

#### F. Call End

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **H1** | Caller | Telefonu kapat | PBX BYE |
| **H2** | PBX | AudioSocket Type 0x00 Terminate frame send | TCP signal |
| **H3** | VoiceRuntime | `AudioSocketSession.DisposeAsync` → `RealtimeApiClient.DisposeAsync` | Cleanup |
| **H4** | VoiceRuntime | `call_sessions.ended_at`, `outcome='completed'`, recording S3 upload (varsa) | Final DB |

---

### 3. F2 — Generic SIP PBX (Mod B: Direct SIP UA — Gelişmiş)

> Mod A AudioSocket Asterisk-based PBX gerektiriyor. **Cisco, Avaya, 3CX Standard, Mitel** gibi non-Asterisk PBX'ler için VoiceRuntime'ın kendisi bir SIP user agent gibi davranır.

**Kütüphane:** [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) (.NET 8 native, BSD-3, ~5K GitHub star, production-grade). NuGet: `SIPSorcery` + `SIPSorceryMedia.Abstractions`.

#### Genel bakış

VoiceRuntime SIP peer/trunk olarak PBX'e register olur. Müşteri PBX admin'i bir SIP peer/extension yaratır (örnek user: `voiceai`, pass: tenant-specific JWT-derived).

```asterisk
; FreePBX PJSIP extension veya Trunk
[voiceai-trunk]
type=registration
transport=transport-udp
outbound_auth=voiceai-auth
client_uri=sip:tenant18173130@voiceruntime.invekto.com:5060
server_uri=sip:voiceruntime.invekto.com:5060
```

Veya PBX'ten gelen inbound INVITE direkt VoiceRuntime'a forward:

```asterisk
[from-internal]
exten => _9X.,1,Dial(PJSIP/${EXTEN}@voiceai-trunk)
```

#### A. Pre-Call (müşteri kurulum)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **P1** | Müşteri PBX admin | Dashboard `/settings/voice-runtime` → Mod B seç → "SIP peer credentials" indir (auth user/pass tenant-bazlı, JWT secret hash) | Credentials |
| **P2** | Müşteri PBX admin | PBX'te SIP peer/trunk ekle (PJSIP/sip.conf veya UI) | Peer config saved |
| **P3** | Müşteri PBX admin | Inbound route DID → SIP peer voiceai | Route active |
| **P4** | VoiceRuntime | SIP UA `:5060` listening (SIPSorcery `SIPTransport.AddSIPChannel(SIPUDPChannel)`) + REGISTER inbound peers acceptance | SIP listener up |

#### B. Inbound Call (SIP INVITE)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **C1** | Caller | Müşteri DID arar | PBX inbound |
| **C2** | PBX | Inbound route → voiceai SIP peer'a INVITE forward | SIP INVITE → VoiceRuntime:5060 |
| **C3** | VoiceRuntime | SIPSorcery `SIPUserAgent.OnIncomingCall` event | Auth check (SIP Digest), tenant identify (X-Invekto-Tenant-Id header VEYA From URI tenant_id pattern) |
| **C4** | VoiceRuntime | INVITE 200 OK SDP (Opus 48k mono offer + RTP/SAVP for SRTP optional) | Codec nego |
| **C5** | Caller PBX | ACK → RTP session start (UDP) | Audio path |
| **C6** | VoiceRuntime | `SipPbxProvider.OpenSessionAsync(descriptor)` → `SipPbxSession` (SIPSorcery RTPSession wraps audio I/O) | session object |
| **C7-C9** | VoiceRuntime | F2 Mod A C6-C9 ile **AYNI** (Realtime connect + flow lookup + KVKK consent + call_sessions INSERT) | Standart |

#### C-F. Live Conversation + DTMF + Transfer + Hangup

Mod A ile **paralel akış**, sadece transport farklı:
- **Audio:** RTP UDP (Opus veya G.711) yerine TCP AudioSocket
- **DTMF:** RFC 4733 RTP event (Type 101) veya SIP INFO message
- **Transfer:** SIP REFER (native, REFER+Replaces gerçek bridge transfer)
- **Hangup:** SIP BYE

Tüm orchestrator + flow + DB + KVKK adımları **AYNI**.

---

### 4. F2 — Toniva (Mod C: Q kendi sistemi — opsiyonel)

> Bu mod Q'nun kendi private Toniva PBX yapısı içindir. gRPC bidi proto v1 FROZEN (commit `b78e8126`'da repo'da). Müşterilere zorunlu değil — Q'nun internal test/own use case.

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **C1** | Q Toniva (C++) | gRPC `VoiceRuntimeBridge.StreamCall()` channel open | Bidi WS persistent |
| **C2** | Caller | Q'nun Toniva line'ını arar | Toniva internal |
| **C3** | Q Toniva (C++) | `ClientMessage.CallStart{call_id, tenant_id, caller_id_hash, locale, started_at, metadata}` send | VoiceRuntime al |
| **C4** | VoiceRuntime | `TonivaPbxProvider.OpenSessionAsync(descriptor)` | session |
| **L1...** | Q Toniva (C++) | PJSIP RTP → Opus encode → gRPC AudioFrame stream | bidirectional |

Detaylı akış Mod A/B ile **paralel**, sadece transport gRPC (Q'nun internal protocol).

---

### 5. F3 — WhatsApp Business Calling Inbound Voice Call (Production)

#### A. Pre-Call (Q + tenant config + Meta allowlist)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **P1** | Q | Meta Business Manager → WhatsApp Business Account → "Cloud API Voice Calls Beta" application submit | Meta review 2-4 hafta |
| **P2** | Q | Allowlist approval sonrası tenant'ın WABA phone_number_id için "Voice Calling" feature aktif | Meta dashboard "enabled" |
| **P3** | Q (Dashboard) | `/settings/voice-runtime` → `voice_call_provider = whatsapp_call`, `voice_calling_enabled = true` | tenant_settings UPDATE |
| **P4** | VoiceRuntime | Meta webhook registration: `https://invekto.com/api/webhook/meta/voice-calling/{tenantId}` | callback URL aktif |

#### B. Inbound Call (Meta call.connect webhook)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **C1** | Caller | WhatsApp app'inden tenant'ın WABA numarasını arar (sesli arama) | Meta Cloud routes |
| **C2** | Meta | POST webhook `call.connect`: `{tenant_id, call_id, from, to, started_at, media_url (WSS URL)}` | Invekto.Integrations endpoint al |
| **C3** | Invekto.Integrations | Webhook verify (X-Hub-Signature HMAC) + `voice_calling_enabled` feature flag | 200 OK ACK |
| **C4** | Invekto.Integrations | HTTP POST `Invekto.VoiceRuntime/api/voice/whatsapp/call-connect` (internal, Shared.Contracts.Voice) | VoiceRuntime al |
| **C5** | VoiceRuntime | `WhatsAppCallProvider.OpenSessionAsync(descriptor)` → `WhatsAppCallSession` | session |
| **C6** | VoiceRuntime | `media_url` ile Meta WebSocket connect (bidi audio stream) | Meta WSS open |
| **C7** | VoiceRuntime | F2 SIP PBX C7-C9 ile **AYNI** (Realtime + flow lookup + KVKK consent + DB) | Standart |

#### C. Live Conversation (PBX ile AYNI — sadece transport farklı)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **L1'** | Meta | Caller audio Meta WSS → Opus 48k 20ms binary frame | VoiceRuntime al |
| **L2'-L7'** | VoiceRuntime + OpenAI | F2 ile **AYNI** pipeline | Standart |
| **L8'** | VoiceRuntime | `voice_turns` INSERT | DB |

#### D. Flow Node (channel_condition fark)

`channel_condition` node tenant_id + provider check:
- `voice_call_provider = whatsapp_call` → wa_call edge
- `voice_call_provider = sip_pbx_audiosocket` → pbx edge
- `voice_call_provider = sip_pbx_direct` → pbx edge (aynı, sadece transport bilgi log için)
- `voice_call_provider = toniva` → pbx edge

Diğer node'lar (voice_say, voice_collect, voice_transfer) **aynı**.

#### E. Human Transfer (WhatsApp Beta limitations)

WhatsApp Calling Beta'da **SIP REFER yok**, transfer Meta API'de henüz dokümante değil. Fallback: callback.

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **T1** | VoiceRuntime | Bot: "Şu anda temsilciye bağlanamıyorum, sizi 5 dakika içinde geri arayacağız" | TTS |
| **T2** | VoiceRuntime | Hangfire `CallbackRequestJob` (channel='whatsapp', from=caller_id) | 5dk sonra WA text + SMS |

#### F. Call End

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **H1** | Caller | WhatsApp arama kapat | Meta call.terminate webhook |
| **H2-H4** | Invekto.Integrations → VoiceRuntime | F2 H2-H4 **AYNI** (Realtime close + DB UPDATE + recording S3 + KVKK) | Standart cleanup |

---

## ORTAK ALTYAPI (4 kanal için: F0 mic + F2 SIP A/B/C + F3 WABA)

### Realtime API connection pooling

Her çağrı için yeni `RealtimeApiClient` instance. Concurrent çağrı limiti tenant başına:
- **F0:** 3 concurrent (OpenAI Tier 1)
- **F2 prod Tier 2:** ~15 concurrent ($50 yüklendiyse 7 gün sonra)
- **F2 Tier 3+ scale:** ~50 concurrent ($100+ cumulative spend)

`tenant_settings.voice_max_concurrent_calls` enforce edilir; aşıldığında inbound call → meşgul tonu veya callback fallback.

### KVKK Compliance Tablosu

| Adım | F0 | F2 SIP (A/B/C) | F3 WABA |
|------|----|----|-----|
| Consent anonsu | yok | İlk 2sn TTS zorunlu | İlk 2sn TTS zorunlu |
| Opt-out trigger | yok | DTMF "9" veya "iptal" intent | "iptal" intent (DTMF Meta'da yok) |
| Recording | yok | tenant_settings.voice_recording_enabled (sağlık default FALSE) | Aynı |
| Transcript at-rest | plaintext (F0 dev) | AES-GCM (tenant KMS key) | AES-GCM |
| 24h opt-out purge | yok | Hangfire `VoiceOptOutPurgeJob` | Aynı |
| Operator delete | yok | Dashboard `DELETE /calls/{id}` (KVKK m.7) | Aynı |

### Cost Model

| Kalem | F0 mic | F2 SIP PBX | F3 WABA |
|-------|--------|--------|--------|
| OpenAI Realtime audio I/O | $0.30/dk | $0.30/dk | $0.30/dk |
| SIP trunk minute (müşteri sağlayıcı) | — | tenant SIP trunk (~$0.005-0.015/dk Turkcell/Vodafone/3CX vs.) | — |
| WhatsApp Cloud Calling | — | — | Meta Beta TBD (şu an free) |
| GPT-4o-mini intent classifier (paralel) | yok | $1.50/100çağrı (F2 active) | $1.50/100çağrı |
| Storage (recording S3) | yok | $0.023/GB/ay | Aynı |
| **Tahmini per call (3dk)** | **$0.90** | **~$0.95** | **~$0.90** |

**Tenant pricing (F2+ aktif):** $99/ay 500dk Realtime + $0.20/ek dk → cost ratio 5x healthy margin.

---

## RISK & MITIGATION

| Risk | F2 (SIP A) | F2 (SIP B) | F2 (Toniva) | F3 (WABA) | Mitigation |
|------|----|----|----|----|-----------|
| Müşteri PBX'i Asterisk-based değil | YÜKSEK | DÜŞÜK | — | — | Mod B (Direct SIP UA) opsiyonel; SIPSorcery non-Asterisk PBX'leri destekler |
| NAT/Firewall (UDP RTP) | yok (TCP) | YÜKSEK | yok (Q LAN) | yok (WSS outbound) | Mod A AudioSocket TCP outbound only (NAT-friendly, no STUN/TURN); Mod B'de SIP+RTP UDP NAT keepalive |
| OpenAI Realtime rate limit | ORTA | ORTA | ORTA | ORTA | tenant_settings.voice_max_concurrent_calls + Tier 2→3 upgrade |
| WhatsApp Calling Beta API breaking | — | — | — | YÜKSEK | Feature flag tenant-scoped; F3 izole, hızlı revert |
| KVKK transcript leak (sağlık) | YÜKSEK | YÜKSEK | YÜKSEK | YÜKSEK | AES-GCM per-tenant KMS + 24h opt-out purge + cross-tenant 403 unit test |
| Sub-saniye olmaz (>1.5sn) | ORTA | ORTA | ORTA | ORTA | AD-8 pre-greeting padding + Azure Neural TR fallback |
| Codec negotiation (Mod B Opus desteği) | yok | ORTA | yok | yok | SIPSorcery G.711 µ-law/A-law fallback (her PBX destekler) + transcoding overhead |
| Concurrent çağrı race | DÜŞÜK | DÜŞÜK | DÜŞÜK | DÜŞÜK | IVoiceCallSession bounded Channel + per-session orchestrator instance |

---

## SALES PITCH (müşteri prospect demo)

| Anahtar mesaj | Kanıt |
|---------------|-------|
| "Sub-saniye AI ses cevap" (önceki firma 2.5sn) | F0 latency report p95 < 1000ms ölçüm |
| **"PBX'iniz olsun olmasın, çalışırız"** | Mod A AudioSocket (10dk müşteri kurulumu, Asterisk-based PBX'lerin %90'ı destekler) + Mod B SIP UA (non-Asterisk PBX'ler) |
| **"PBX altyapınıza dokunmadan"** | Inbound INVITE forwarding tek satır dialplan + Invekto SIP peer; mevcut extension/queue/IVR korunur |
| "WhatsApp + PBX + Web 3 kanal tek altyapı" | IVoiceCallProvider abstraction |
| "KVKK uyumlu out-of-the-box" | AES-GCM transcript + opt-out DTMF "9" + 24h purge |
| "İnsana bağlanma garantisi" | voice_transfer + ARI redirect (Mod A) / SIP REFER (Mod B) / callback fallback |
| "Türkçe doğal ses" | Azure Neural TR premium tier ($30/ay add-on) |
| "Doktor sesi clone" (saç ekimi/estetik) | ElevenLabs voice clone tier ($99-1100/yıl) |
| "Dashboard'da her çağrı kayıtlı" | /calls liste + transcript + playback + intent timeline |

**Tahmini pilot fiyatlandırma:** $99/ay 500dk dahil + $0.20/ek dk → 1 küçük klinik ~$150 ortalama, 1 büyük klinik ~$500. Cost ratio 5x healthy margin.

---

## MÜŞTERİ ONBOARDING GUIDE (F2 Mod A — En Yaygın)

> Dashboard'da PDF olarak indirilebilir, müşteri PBX admin'e gönderilir. Adım adım, screenshot'lı.

### Asterisk / FreePBX / Issabel kurulum (10 dakika)

1. **Dashboard üzerinden tenant_id öğren** — `/settings/voice-runtime` → "Tenant ID: 18173130" (kopyala)
2. **PBX admin paneline gir** — FreePBX `Admin → Asterisk SIP Settings → PJSIP` veya CLI
3. **Dialplan extension ekle** — FreePBX: `Admin → Custom Destinations` → Goto `[invekto-voice-ai],s,1`
4. **Custom dialplan dosyası** — `/etc/asterisk/extensions_custom.conf`:
   ```asterisk
   [invekto-voice-ai]
   exten => s,1,NoOp(Invekto Voice AI inbound: ${CALLERID(num)})
    same => n,Set(__INVEKTO_TENANT_ID=18173130)
    same => n,Set(__INVEKTO_LOCALE=tr-TR)
    same => n,Answer()
    same => n,AudioSocket(${UNIQUEID},voiceruntime.invekto.com:8090)
    same => n,Hangup()
   ```
5. **Inbound route ata** — FreePBX `Inbound Routes` → DID Number "xxxxxxxxxx" → Destination "Custom Destinations: invekto-voice-ai"
6. **Dialplan reload** — CLI: `asterisk -rx "dialplan reload"` veya FreePBX UI "Apply Config"
7. **Test çağrı** — kendi telefondan müşteri DID'ini ara → 2sn içinde Türkçe KVKK anonsu duy
8. **PBX log kontrol** — `tail -f /var/log/asterisk/full | grep AudioSocket` → "AudioSocket connected" görmeli

### 3CX kurulum (15 dakika)

3CX Pro+ Edition AudioSocket destekler:
1. Management Console → `Voice Apps → CFD` (Call Flow Designer)
2. Yeni CFD aç → "ExternalCallControl" component drag-drop
3. Endpoint: `voiceruntime.invekto.com:8090` (TCP) + UUID auto-gen
4. Inbound rule → CFD'ye yönlendir
5. Test + log

### Issabel kurulum

Issabel FreePBX-based — FreePBX talimatı geçerli.

### Cisco CUCM / Avaya / Mitel kurulum

Mod B (Direct SIP UA) gerekli. Detaylı setup `voice-runtime SIP peer credentials` sayfasında.

---

**Sonraki adım:** Q F1 başlamadan önce müşteri pilot için AudioSocket Asterisk PBX'i olan bir tenant identify et (Dent Adavista FreePBX kullanıyor mu kontrol et). F1+F2 paralel, F2 sonu müşteri pilot smoke.
