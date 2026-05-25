# SPEC-008: Voice Flow Builder (FEAT-VFB)

> **Spec ID:** SPEC-008 | **Paket:** FEAT-VFB | **Risk:** HIGH
> **Yazar:** Claude (Q briefing) | **Son Guncelleme:** 2026-05-23 | **Durum:** APPROVED (Q onay 2026-05-23, OQ-1..OQ-7 resolved, F0 PoC microphone-based, Smart-Turn v2 added)

## 1. Intent (Ne & Neden)

Q ifadesi (2026-05-17): "INSE'de WhatsApp ve PBX'den gelen aramalari flow builder ile full entegre sekilde cevap verecek bir sisteme ihtiyacim var. Flow builderden yonetilebilmeli, cok hizli ve dogru cevap verebilmeli, telefonu insana baglayabilmeli ve tam cagri gecmisi tutmali. WhatsApp inbound sesli aramalar da dahil, bir tek ses notu degil."

Bu spec; uc farkli **canli ses kanalini** mevcut `FlowEngineV2` uzerinden tek bir flow modeline baglar:

| Kanal | Tip | Latency Profili | Mevcut Durum |
|-------|-----|------------------|----------------|
| WhatsApp text mesaji | async metin | n/a | **MEVCUT** (Automation Program.cs:465) |
| WhatsApp ses notu (voice note, push-to-talk) | async ses dosyasi | tek-seferlik 1-3sn | **EKSIK** (Program.cs:504 audio filter) |
| WhatsApp Business Calling (inbound canli arama) | senkron RTP duplex | sub-saniye barge-in | **YOK** |
| PBX gelen arama (Toniva/MicroSIP) | senkron SIP/RTP duplex | sub-saniye barge-in | **YOK** |

Hedef: ucu de ayni `chatbot_flows` modelinden tetiklenir, ayni `FlowEngineV2.cs` calistirir, **uc kanal icin de tek bir Voice Flow Builder UI'i** vardir. Telefon insana baglanabilir (`transfer_to_human` node), tum aramalar transcript + intent + outcome ile `call_sessions` tablosuna yazilir, Dashboard'dan oynatilabilir.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | WhatsApp ses notu gelir, VoiceAI transcribe eder, FlowEngineV2 ayni text path'inde calisir, bot text cevap doner | `automation jsonl` `[VOICE-NOTE→TEXT]` + `flow_runs.trigger_kind='wa_voice_note'` |
| AC-2 | WhatsApp Calling inbound: 2 saniye icinde KVKK consent anonsu, sonra bot dialog | live test + `call_sessions.consent_played_at - started_at < 2000ms` |
| AC-3 | PBX (Toniva) inbound: ayni KVKK consent + bot dialog akisi | live test (Q'nun MicroSIP build) + `call_sessions.provider='toniva_pbx'` |
| AC-4 | Sub-saniye barge-in: kullanici TTS sirasinda konusursa, TTS 250ms icinde durur | E2E test + `voice_turns.barge_in_at - tts_start_at < 250ms` ortalama |
| AC-5 | `voice_transfer` node Toniva tarafina REFER yollar, kuyrukta musait agent yoksa Hangfire `CallbackRequestJob` SMS+WA mesaji enqueue eder | `hangfire.job_id` + `callback_requests` row |
| AC-6 | Dashboard `/calls` listesi: filtre (tenant/tarih/kanal/outcome), detayda transcript + intent timeline + recording playback | manual UI + cross-tenant 403 (smoke) |
| AC-7 | Flow Builder UI'da 5 yeni node tipi: `voice_trigger`, `voice_say`, `voice_collect`, `voice_transfer`, `channel_condition` | SPA test + `chatbot_flows.nodes[].type` enum |
| AC-8 | `IVoiceCallProvider` abstraction: 3 implementation (Toniva, WhatsApp Calling, Mock), tenant `tenant_settings.voice_call_provider` ile secer | DI smoke + provider enum |
| AC-9 | KVKK opt-out: DTMF "9" veya "iptal" intent'i → anons + `call_sessions.opt_out=true` + transcript korunmaz (TTL 24h) | live test + 24h sonra DB SELECT (transcript NULL) |
| AC-10 | Latency budget per tenant Dashboard'da: avg/p95 first-token, avg/p95 barge-in reaction, monthly billing chars+sec | `/reports/voice-perf` widget + Hangfire daily aggregation |

## 3. Architectural Decisions

| # | Karar | Neden | Codex Notu |
|---|-------|-------|------------|
| AD-1 | Yeni mikroservis **`VoiceRuntime`** (:7115), VoiceAI (:7114) ile birlesmez | VoiceAI batch transcribe (Whisper HTTP file upload), VoiceRuntime bidirectional WebSocket+RTP stream — runtime model + dependency seti farkli (libopus, gRPC server, WebRTC stack) | EXPECTED: CQ4 (servis ayirma yerine genisletme) skip |
| AD-2 | `IVoiceCallProvider` Shared.Contracts.Voice altinda, 3 implementation runtime icinde | FEAT-VCP pattern'i (video provider abstraction) | — |
| AD-3 | Audio codec: **Opus 48kHz mono 20ms frame** her provider icin canonical, transcoding provider tarafinda | OpenAI Realtime + Deepgram + ElevenLabs hepsi Opus native; G.711 → Opus transcode WhatsApp Calling tarafinda zaten yapiliyor | EXPECTED: CQ7 codec diversity skip |
| AD-4 | AI pipeline **hibrit**: kritik audio path OpenAI Realtime (audio-in → reasoning → audio-out), paralel hafif intent classifier (GPT-4o-mini her 2sn partial transcript'te) | Q karari (2026-05-17 interview). Sub-saniye latency + flow tarafinda intent gerekli. Paralel cagri Realtime'i bloklamiyor | EXPECTED: CQ3 "2 LLM cagrisi maliyet" — bilincli, ROI hesabi `9. Cost Model`'de |
| AD-5 | Transcript at-rest **at-rest encryption**: AES-GCM, KMS key tenant-scoped, KVKK m.6 ozel nitelikli veri (saglik sektoru) | Pilot tenant'lar saglik klinikleri | EXPECTED: CQ11 "transcript plaintext kolonu" — sifreli kolon ek tablo |
| AD-6 | WhatsApp Calling **gradual rollout**: feature flag `voice_calling_enabled` per tenant, ilk pilot Dent Adavista | Cloud API Beta, behavior degisebilir | — |
| AD-7 | Toniva tarafi PJSIP hook + agent queue **bu paketin kapsami DISI** (Q paralel yapacak) | Q karari (interview Soru-2). gRPC contract bu spec'te tanimlanir, C++ implementation Toniva tarafinda | EXPECTED: missing-impl false-positive skip — contract bu repoda |
| AD-8 | Barge-in: full-duplex degil, **yarim-duplex VAD-driven** | VAD (Voice Activity Detection) kullanici sesini algilarsa TTS'i 250ms icinde keser. Tam full-duplex (overlap) F2'de degil, F3'te | — |
| AD-9 | Recording **opsiyonel** ve tenant ayari (`tenant_settings.voice_recording_enabled`). KVKK consent anonsunda recording oldugu da soylenir, opt-out icin DTMF "9" calisir | Saglik sektoru recording riskli, tenant kararı | — |
| AD-10 | `call_sessions` ayri tablo, `leads.last_call_at` denormalize edilir (RI insight engine hizi icin) | RI 7 insight motoru zaten denormalize alanlarla calisiyor (`leads.last_message_at` precedent) | — |
| AD-11 | **F0 PoC mikrofon-based** (Mock+WAV degil). Browser WebRTC getUserMedia → VoiceRuntime WS → Realtime API → browser hoparlor. Test sayfasi `wwwroot/voice-poc.html`. | Q karari (2026-05-23 interview): "microfonla telefon gorusmesi gibi yapalim". Musteri demo'su icin de ayni altyapi kullanilir, Mock provider F2'de hala test icin var | EXPECTED: scope-creep CQ12 skip — F0 deliverable revize, Q-approved |
| AD-12 | **Smart-Turn v2** (HuggingFace `pipecat-ai/smart-turn-v2`, BSD-2, ONNX) Silero VAD ustune semantic EOT katmani. F0'da OpenAI `turn_detection.type=semantic_vad` beta test edilir, yetmezse F2'de Smart-Turn devreye girer | Q feedback (2026-05-23): "cumle ortada kalmis duraksama da olabilir, VAD yetmez". 3-katmanli endpoint pipeline (VAD + semantic + adaptive timeout). 35ms ek latency, $0 cost (self-host ONNX) | EXPECTED: CQ7 model diversity skip — F0 OpenAI native, F2 fallback HF |
| AD-13 | **TTS tenant-selectable** (`tenant_settings.tts_provider` enum: `realtime` default / `azure_neural_tr` premium TR / `elevenlabs_clone` voice clone premium). Outage fallback Deepgram Nova-3 + GPT-4o-mini + Azure Neural pipeline (config-driven swap) | Q karari (2026-05-23 interview): saglik tenant'lar Azure Neural TR daha dogal ses ister, premium tier voice clone. Realtime default sub-saniye, Azure premium kalite tradeoff acik | EXPECTED: CQ3 "3 TTS provider maliyet" skip — tenant-scope, sadece secili olan devre, cost 9. bolumde |
| AD-14 | **Voice flow editor mevcut `/flow-builder`'a node palette extension** (ayri sayfa DEGIL). channel_condition cross-channel, voice node'lar palette icinde grupli | Q karari (2026-05-23 interview): operator tek editör ogrenmeli, mevcut UX devami | — |
| AD-15 | **DB ownership: VoiceRuntime (:7115) tek yazici**. `call_sessions` + `voice_turns` + `callback_requests` migration sahibi VoiceRuntime. Dashboard Backend HTTP'le sorgular (read-only proxy) | Q karari (2026-05-23 interview): mikroservis izolasyon — live audio orchestrator zaten her event'i biliyor, Backend hop latency overhead. FEAT-VFB-OWNERSHIP precedent | — |
| AD-16 | **Realtime API outage fallback: HARD-FAIL → REFER + callback** (config-driven Deepgram pipeline F2 hazir ama default DEVRE DISI). Outage'da "sizi temsilciye yonlendiriyorum" anonsu + Toniva REFER + agent yoksa Hangfire callback (SMS+WA) | Q karari (2026-05-23 interview): pilot doneminde musteri "bekletme" istemiyor. Hybrid pipeline kod hazir ama opt-in, F4'te aktive edilir | EXPECTED: CQ8 "kod hazir ama disable" skip — config flag tenant-scope, opt-in F4 |
| AD-17 | **F0 microphone provider Toniva PROTOCOL clone**. WS endpoint Opus 48kHz mono 20ms frame ayni Toniva gRPC sema. Browser test sayfasi gercek pilot demo'da da kullanilir (sales tool) | Q strategic (2026-05-23): satis donesinde musteri laptop browser'da "konus bot ile" deneyimini gosterir. F0 boyca sales-ready | — |
| AD-18 | **VoiceRuntime ↔ Automation FlowEngineV2 sadece HTTP/SignalR ile konusur** (mikroservis izolasyon invariant). FlowBridge VoiceRuntime icinde icsel bir bileendir; FlowEngineV2'ye dogrudan ProjectReference YASAK. F2'de Automation tarafinda yeni internal endpoint (`POST /api/internal/voice/inject-intent` + SignalR `voice_turn_completed` event) acilir; VoiceRuntime HttpClient ile cagirir. | Mevcut isolation kurali (CLAUDE.md "Microservice isolation"): hicbir servis baska servisi `using Invekto.X` ile referans alamaz, sadece `Invekto.Shared` araci. Codex chunk-1 CQ9 FAIL bu boundary'i acikca yazmadigimiz icin patladi | EXPECTED: F2 plan JSON'da CQ9 kapsamlı kontrolu (HttpClient + endpoint kontrat) |
| AD-19 | **F0 WS lifecycle invariant'lari:** (a) Browser refresh / WS close → `HttpContext.RequestAborted` CancellationToken'a baglı `sessionCts` tetiklenir → `await using RealtimeApiClient.DisposeAsync()` + `await using MicrophoneCallSession.DisposeAsync()` zincirleme calisir, 2sn timeout ile `Task.WhenAny` bekler, sonra force close. (b) OpenAI Realtime cold-start 5sn asarsa `TaskCanceledException` → `INV-VR-001` user message + WS 1011 close. (c) Realtime rate-limit (429 / WS close 1013) → `INV-VR-003` + browser TEXT `{type:"error", code:"INV-VR-003"}` + circuit breaker 30sn (F2'de devreye). (d) Tüm async loop'lar (`BrowserRxLoop` / `BrowserTxLoop` / `VoiceToRealtimeForwardLoop` + Realtime `SendLoop` / `ReceiveLoop`) `OperationCanceledException`'i swallow eder, başka exception'lar `jsonl ERROR` + INV-VR-* code ile log'lanir. (e) `MicrophoneCallSession` bounded `Channel<OpusFrame>` (cap=200, `DropOldest`) — resource leak yok, backpressure DropOldest ile silently absorb. | Codex chunk-1 Q3 FAIL — F0 lifecycle policy yazılı değildi. Bu AD codify eder. Kod tarafi `VoicePocEndpoints.HandleMicrophoneWsAsync` + `RealtimeApiClient.DisposeAsync()` + `MicrophoneCallSession.DisposeAsync()` bu invariant'lari implement eder | EXPECTED: kodu okuyarak verify edilebilir, /rev iter-1 Q3 PASS |
| AD-20 | **F0 deploy / secret yonetim policy:** (a) `silero_vad.onnx` (~2.3MB, MIT, SHA256 `1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3`) git'e koyulmaz (`.gitignore` `src/Invekto.VoiceRuntime/Models/*.onnx`). Q laptop + F2 prod NSSM deploy'da `Models/README.md` PowerShell `Invoke-WebRequest` script ile indirilir; SHA256 verify zorunlu. Eksik dosya → boot-time `INV-VR-008` fail-fast (Program.cs:80-90). (b) **API key environment-variable-only policy** — `appsettings*.json` plaintext key tutmak YASAK (F0/F2/F3/F4 hepsi). Detayli kurulum komutlari (kullanici scope env var + NSSM service env var) `src/Invekto.VoiceRuntime/Models/README.md` runbook icindedir; SPEC ozet düzeyde policy belirler. F4 backlog: Windows DPAPI veya Azure Key Vault rotasyon. (c) ~~F0 sadece Q laptop'unda calisir; prod deploy F2 PBX MVP paketinin scope'undadir.~~ **2026-05-25 OVERRIDE:** F0 prod deploy yapildi (commit `8311c83c` + `56cab707`), `https://voice.invekto.com:8443` LIVE. F2 PBX MVP F0 prod altyapisi uzerinden gelistirilir. | Codex chunk-1 Q4 FAIL — model checksum belirsiz + secret mgmt belirsizdi. SHA256 artik FROZEN, env-var-only policy CANONICAL | EXPECTED: Models/README.md + Program.cs:81-91 birlikte verify edilebilir, /rev iter-1 Q4 PASS |
| AD-21 | **F0.5 Voice Test tenant impersonation pattern** — Voice Test sayfasi tenant=0 sysadmin JWT + WS query `?tenant_id=X&flow_id=Y` ile simulate. F2 PBX prod VoiceRuntime bu pattern'i KULLANMAYACAK (JWT'den tenant alir, query param impersonation kapali; feature flag `Voice:AllowTenantImpersonation`). | Q karari (2026-05-25): super.invekto.com Voice Test dropdown ile tenant sec, mikrofona konus, KB sorgula. Test/prod ayrimi net | EXPECTED: WS endpoint query param tenant_id kabul ediyor — F2'de feature flag kapatacak |
| AD-22 | **VoiceRuntime cross-service auth: JwtGenerator.GenerateServiceToken(targetTenantId)** — Automation `AiFaqHandler` pattern clone. Service JWT 5dk expire, claims: `tenant_id` (target), `user_id=0`, `role=service`, `source=voice_runtime`. Backend ops endpoint icin browser Dashboard JWT direct call (CORS). | Mevcut Shared.Auth.JwtGenerator AiFaqHandler'da production-tested. Automation/Knowledge `/api/v1/*` endpoint'leri tenant_id-bound JWT zorunlu | EXPECTED: VoiceRuntime'ta JwtGenerator DI'da kayitli, her tool call fresh mint |
| AD-23 | **F0.5 Function calling: tek tool `search_knowledge_base(query, top_k=3)`** OpenAI Realtime GA shape `session.update.tools[]`. Top_k server-side clamped [1, 10]. F2'de `transfer_to_human`, `collect_field`, `voice_dtmf` toollari eklenir. | Q karari (2026-05-25): MVP scope. Knowledge mevcut /search endpoint yeter, yeni endpoint YOK | EXPECTED: Tek tool minimal API |
| AD-24 | **F0.5 System prompt template generic + tenant.tenant_name + tenant.sector + flow.flow_name** enjekte. Per-flow `voice_persona` kolonu F2 Migration 050'ye saklı. | Q karari (2026-05-25): DB schema degisikligi yok, hizli iterasyon | EXPECTED: Flow voice_persona kolonu kullanilmiyor — F2 backlog |
| AD-25 | **F0.5 Browser dropdown defense-in-depth** — Browser Dashboard JWT ile DIRECT Backend `/api/ops/tenants` + Automation `/api/v1/automation/flows/{tid}` cagirir (CORS allowed origins'a `voice.invekto.com:8443` eklenir). VoiceRuntime context'i kendi service-JWT'siyle yeniden ceker (browser data trust edilmez, server-side authoritative). | Source-of-truth garantisi | EXPECTED: Cift fetch (browser UI + VoiceRuntime authoritative) intentional |
| AD-26 | **Asterisk-based AudioSocket (Mod A) F2 scope'undan CIKARILDI** — Q karari 2026-05-25: "Asterisk yok. Hep SIP uzerinden gelip gidecek bilgiler." Türkiye pazarinda hedef musterilerin PBX'leri Asterisk-based degil; Mod A'nin %90 pazar varsayimi GECERSIZ. Mevcut spec (satir 99-197) referans/legacy bilgi olarak kalir, F2 implementation icin AKTIF DEGIL. | Q karari, 2026-05-25 19:15 | EXPECTED: F2 plan JSON'da Mod A/AudioSocket/Asterisk implementation YOK |
| AD-27 | **F2 ana SIP scope = Mod B Direct SIP UA (SIPSorcery .NET 8 NuGet, BSD-3, ~5.6K star production-grade)** — VoiceRuntime peer/trunk olarak musterinin SIP PBX'ine (Cisco CUCM, Avaya, 3CX Standard, Mitel, herhangi SIP-compliant) register. Inbound INVITE → cevaplama → RTP UDP audio bidi → BYE. DTMF RFC 4733 (RTP telephone-event) + SIP INFO fallback. SIP REFER ile insan transfer. Codec: Opus narrowband 8kHz tercih, G.711 µ-law/A-law fallback (Concentus mevcut). | Q karari (2026-05-25): Mod B tek SIP path. SIPSorcery production-tested EU telecom providers. NAT-friendly (UDP keepalive 30sn re-REGISTER) | EXPECTED: F2 plan JSON'da SIPSorcery NuGet + RTP handler + DTMF + REFER. AD-3 codec=Opus canonical sürdürülür ama 8k narrowband fallback kabul edilir |
| AD-28 | **Mod C Toniva → opsiyonel/Q internal, F2 zorunlu DEGIL** — gRPC proto v1 FROZEN kalir (`arch/contracts/voice/toniva-bridge.proto`, commit b78e8126). Müşterilere Toniva pazarlanmaz — SIP standardı yeterli. Q kendi private kullaniminda Toniva test edebilir (gRPC contract hala canli) ama F2 deliverable Mod B SIP UA. | Q karari (2026-05-25): SIP standardi yeterli, Toniva ek karmaşıklık | EXPECTED: F2 plan JSON'da TonivaPbxProvider implementation OPTIONAL — Q'nun ek talebine bagli |

## 4. Service Architecture

```
                  ┌──────────────────────────────────────────────┐
                  │  Toniva/MicroSIP (C++, paralel Q development)│
                  │  • PJSIP pjmedia hook (RTP frame export)      │
                  │  • Agent queue + REFER routing                │
                  └──────────────┬───────────────────────────────┘
                                 │ gRPC bidi (Opus@20ms, signaling)
                                 ▼
                       ┌─────────────────────┐
                       │  Invekto.VoiceRuntime│  Port 7115 (yeni)
                       │  ────────────────────│
                       │  • IVoiceCallProvider│
                       │      ├─ TonivaPbx     │
                       │      ├─ WhatsAppCall  │
                       │      └─ Mock          │
                       │  • VadDetector       │
                       │  • RealtimeApiClient │ ◀──── OpenAI gpt-4o-realtime
                       │  • IntentClassifier  │ ◀──── GPT-4o-mini (paralel)
                       │  • TtsStreamRouter   │ ◀──── (Realtime audio out)
                       │  • FlowBridge        │ ────▶ FlowEngineV2 (Automation)
                       │  • CallSessionStore  │
                       └──────────┬───────────┘
                                  │ HTTP+SignalR
                                  ▼
                  ┌───────────────────────────────────┐
                  │ Invekto.Automation (:7108)        │
                  │  FlowEngineV2.cs                   │
                  │  • Yeni node types:                │
                  │      voice_trigger / voice_say /   │
                  │      voice_collect / voice_transfer│
                  │      channel_condition             │
                  │  • Cross-channel intent inject     │
                  └─────────────┬─────────────────────┘
                                │
                                ▼
                  ┌──────────────────────────────────┐
                  │ Invekto.Backend (:5000)          │
                  │ + Dashboard SPA                   │
                  │  • /api/v1/voice/* proxy          │
                  │  • /calls liste + detay sayfasi  │
                  │  • /reports/voice-perf widget    │
                  └──────────────────────────────────┘

WhatsApp Cloud API ──webhook── Invekto.Backend ──── (voice note: VoiceAI batch)
                  ──media ws── Invekto.VoiceRuntime ── (calling: live stream)
```

### Servis sorumluluk tablosu

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| **VoiceRuntime** (yeni :7115) | Live audio orchestration, RTP/WS, Realtime API, VAD, barge-in, call_sessions write | **Yeni servis** |
| VoiceAI (:7114) | WhatsApp ses notu icin batch Whisper transcribe | Minor: yeni endpoint `/api/v1/voice/transcribe-and-inject` (FlowEngine'a metin enjekte) |
| Automation (:7108) | FlowEngineV2 yeni 5 node tipi + cross-channel intent injection + voice context | Major: yeni node handler'lar + flow schema migration |
| Backend (:5000) | `/calls` REST + Dashboard SPA + WhatsApp Calling webhook + Toniva gRPC client (orchestration) | Major: yeni endpoint grubu + SPA pages |
| Integrations (:7106) | WhatsApp Cloud Calling API webhook ingest (`call.connect`, `call.terminate`, `call.permission_action`) | Major: yeni adapter |
| Outbound (:7107) | TTS audio dosyalari (outbound aramalarda — F3 scope disi) | Hicbir sey (F1-F2'de degismez) |
| Marketing (:7112) | RI Insight Engine 8. modul "Voice AI Performance" | Minor: yeni job + widget endpoint |

## 5. Flow Builder Yeni Node'lar

`chatbot_flows.nodes[].type` enum extension:

| Node Type | Tetiklenme/Eylem | Config (JSON) | Cikis Edge'leri |
|-----------|------------------|----------------|------------------|
| `voice_trigger` | Entry: kanal-filtreli baslat | `{ channels: ["wa_call", "pbx", "wa_voice_note"], consent_text_key, opt_out_keyword }` | `next` |
| `voice_say` | TTS chunk soyle | `{ text: "{{placeholder}}", voice_id, ssml, interruptible: true }` | `done`, `interrupted` |
| `voice_collect` | Kullaniciyi dinle, intent veya transcript yakala | `{ timeout_sec, silence_end_ms, expected_intents: [...], allow_dtmf }` | per intent + `timeout`, `no_match` |
| `voice_transfer` | Insana bagla | `{ target: "queue:sales" \| "extension:101" \| "team:dent_eu", warm_handoff_text, fallback_callback: true }` | `transferred`, `queue_full`, `callback_scheduled` |
| `channel_condition` | Kosul: hangi kanal? | `{ branches: { wa_call: nodeA, pbx: nodeB, wa_voice_note: nodeC, wa_text: nodeD } }` | her kanal icin edge |

**Mevcut node'lar genisleme:**
- `ai_faq` → `voice_collect` ile zincirleme calisir; transcript metin gibi kullanilir
- `action_send_message` → `voice_say` ile aritmetik benzer ama TTS streaming farkli

## 6. KVKK & Consent

| Adim | Kim | Detay |
|------|-----|-------|
| 1. Anons | Bot (TTS) | Cagri baslangic, ilk 2 sn: "Bu gorusme yapay zeka tarafindan karsilaniyor. Kalite icin kaydedilebilir. Cikis icin 9 tusuna basin veya iptal deyin." |
| 2. Onay | Kullanici | DTMF "9" veya "iptal" intent'i → `voice_trigger` exit `opt_out` edge → human transfer veya disconnect |
| 3. Recording | Tenant ayari | `tenant_settings.voice_recording_enabled` true ise S3-uyumlu storage (Minio/AWS S3, KMS encrypted), retention `tenant_settings.voice_recording_retention_days` default 90 |
| 4. Transcript | Her zaman | At-rest AES-GCM, 24h sonra opt_out=true ise NULL'a celinir (Hangfire `VoiceOptOutPurgeJob`) |
| 5. Veri ihlali yanit | Operator | Dashboard'dan `DELETE /calls/{id}` (KVKK m.7 silme hakki) |

**Saglik sektoru ozel notu:** Saglik konusan tenant'larda (`tenant_registry.industry_vertical='health'`) transcript ozel nitelikli veri sayilir, recording **DEFAULT KAPALI** olur (opt-in).

## 7. Latency Budget

### Sub-saniye barge-in hedefi: **first-byte TTS < 870ms p95**

| Hop | Beklenen | Notlar |
|-----|----------|--------|
| SIP/WA Calling → Toniva/Cloud edge | 30ms | network |
| Toniva/Cloud → VoiceRuntime gRPC/WS | 20ms | LAN/inter-region |
| Silero VAD silence detect → "ses var/yok" sinyal | 200ms | aggressive endpointing, ONNX CPU 5ms inference (her 30ms karar) |
| **Smart-Turn v2 semantic EOT check** | **30-50ms** | **AD-12 (F2'de devre)**. CPU ONNX, "cumle bitti mi?" binary classifier. F0'da OpenAI native `turn_detection.type=semantic_vad` beta. |
| OpenAI Realtime first-token | 250-400ms | gpt-4o-realtime ortalama |
| TTS first-byte (Realtime audio-out icinde) | 50-100ms | tek pakette geliyor |
| VoiceRuntime → istemci ses | 50ms | network |
| **Toplam first-byte** | **630-870ms** | hedef sub-saniye, +35ms Smart-Turn ek |
| **Barge-in reaction** | **150-250ms** | VAD detect → TTS stop signal (Realtime `response.cancel`) |

**3-katmanli endpoint pipeline (AD-12):**

1. **Silero VAD** (her 30ms): "Ses var mi?" → ses YOK ise susma sayaci baslar
2. **Smart-Turn v2** (200ms susma sonrasi): "Cumle semantic olarak bitti mi?" → EVET → Realtime'a "response.create"; HAYIR → adaptive timeout
3. **Adaptive timeout** (filler/soru ipucu): "yani/sey/acaba" → 1500ms bekle; soru tonu → 400ms; conjunction → 1000ms; varsayilan → 700ms

**Eger gercek olcum 1.2sn'i asarsa fallback:** "Pre-greeting padding" — bot anlamli olmayan ama dogal duran ilk 300ms ("Hımm, anladım...") soyler, gercek cevap arkasindan gelir. Kullanici sub-saniye hisseder.

### F0 PoC microphone-based latency profili (AD-11)

| Hop | Beklenen |
|-----|----------|
| Browser mic capture (getUserMedia + AudioWorklet Opus encode) | 30ms |
| Browser → VoiceRuntime WSS (LAN local) | 5-10ms |
| Opus decode (Concentus C#) | 2ms |
| Silero VAD inference (ONNX) | 5ms |
| Realtime API WS hop (cloud, region) | 100-150ms |
| Realtime first-token | 250-400ms |
| Realtime → VoiceRuntime → browser → speaker | 80ms |
| **F0 hedef p95** | **<1000ms** |

F0 hedef p95 PBX kosulundan yumusak (Toniva codec transcode hop yok ama browser overhead var). 1000ms gectigi durumlar PBX'te 700-800ms olur.

## 8. Faz Plani

| Faz | Sure | Scope | Cikti |
|-----|------|-------|--------|
| **F0 PoC (microphone-based)** | **5-7 gun** | **AD-11/AD-17:** Yeni servis `Invekto.VoiceRuntime` (:7115) skelet (csproj + Program.cs + DI + JWT + JsonLines + /health) + WS endpoint `/ws/voice/microphone` + Concentus Opus codec + Silero VAD ONNX (`Models/silero_vad.onnx` MIT) + OpenAI Realtime WS client (bidi PCM16 24kHz) + Latency instrumentation (t0 VAD silence / t1 first-byte / t2 barge-in) + Browser test sayfasi (`wwwroot/voice-poc.html` getUserMedia + AudioWorklet Opus + HUD + transcript live) + Toniva gRPC contract v1 **freeze** (proto only, impl F2) + F0 endpoint authorization: Q-only JWT (tenant=0 superadmin) + Realtime semantic_vad beta test (F0 native turn detection) | F0 deliverable: Q laptop browser canli mikrofon → bot ses cevap + p95 latency raporu (`arch/reports/feat-vfb-f0-latency-report.md`) + go/no-go karar + Toniva proto donduruldu (Q paralel C++ baslayabilir) + sales-ready demo sayfasi |
| **F1 WA Voice Note** | 1 hafta | Automation Program.cs:504 audio filter relax + VoiceAI yeni endpoint + FlowEngineV2 text inject (yeni node yok, mevcut path) | WhatsApp ses notlari bot tarafindan cevaplaniyor (text reply) |
| **F0.5 Tenant-aware Voice Test** | 4-5 gun | **AD-21..25:** Voice Test sayfasi tenant + flow dropdown + WS handshake ?tenant_id=X&flow_id=Y impersonation + dinamik instructions (tenant.name + sector + flow.name) + function calling tek tool `search_knowledge_base` + JwtGenerator service token + 3 HTTP client (TenantInfo/FlowInfo/KnowledgeSearch) + CORS allowed origins (Knowledge/Automation/Backend). F2 onkosul: function calling + cross-service auth + dinamik context battle-tested. | Q super.invekto.com Voice Test → tenant + flow sec → mikrofon konus → KB sorgula → bot Türkce dogal cevap |
| **F2 PBX Live (SIP UA, SIPSorcery) MVP** | 3-4 hafta | **AD-26/27/28: Asterisk YOK kararı (2026-05-25)** — Mod A AudioSocket iptal, **Mod B Direct SIP UA ana scope** (SIPSorcery .NET 8 NuGet BSD-3). VoiceRuntime SIP UA: PBX peer/trunk register (UDP keepalive 30sn) + inbound INVITE cevaplama + RTP UDP audio (codec Opus narrowband 8k tercih, G.711 µ-law fallback) + DTMF RFC 4733 + SIP REFER transfer + Windows Firewall RTP port range 10000-20000. **AD-12 Smart-Turn v2 ekle** + 5 yeni flow node + KVKK consent + `call_sessions`/`voice_turns`/`callback_requests` tablo (Migration 050, **VoiceRuntime sahibi AD-15**) + `IVoiceCallProvider` (SipUaProvider + MicrophoneProvider F0'dan + Mock) + Dashboard `/calls` v1 + handoff (SIP REFER) + **AD-13 TTS tenant-selectable** (Azure Neural TR + ElevenLabs Pro DI) + **AD-16 outage hard-fail REFER+callback**. Mod C (Toniva gRPC) opsiyonel/Q internal. | Tek tenant (Dent veya Q-test), tek flow, gercek SIP arama bot cevap |
| **F3 WA Calling + Multi-tenant** | 2 hafta | WhatsAppCallProvider implementation + Integrations webhook + feature flag rollout + multi-tenant smoke | WhatsApp inbound canli arama bot tarafindan cevaplaniyor |
| **F4 Optim & Analytics** | 1 hafta | Full-duplex barge-in (overlap) + RI Insight Engine 8. modul + Dashboard `/reports/voice-perf` + cost/billing widget + **opsiyonel hybrid pipeline aktive** (Deepgram + GPT-4o-mini + Azure Neural, AD-16 config flag) + **backlog B-VFB-SELFHOST** trigger (Faster-Whisper Turbo + XTTS-v2, KVKK premium, gate: 15+ premium tenant) | Production-grade |

**Pre-req:**
- **F0 baslangici (TAMAM 2026-05-23):** OpenAI Realtime API key (Q tier upgrade 1-2 saat, interview Tur-1).
- **F1 sonu (Toniva paralel start):** Q taraf Toniva pjmedia hook + agent queue + REFER routing C++ kismi baslar. gRPC contract Faz F0'da dondurulur, Q C++ proto'yu okur ve paralel gelistirir.
- **F2 baslangici:** Toniva C++ taraf F2 baslamadan en az `MicroSIP-LIVE-RTP-EXPORT` chunk'i hazir olmali (Mock provider F2 boyunca fallback).

## 9. Cost Model (per tenant, 100 cagri/ay 3dk ortalama)

| Kalem | Birim | 100 cagri × 3dk | Aylik |
|-------|-------|------------------|--------|
| OpenAI Realtime audio-in | $0.06/dk | 300 dk | $18 |
| OpenAI Realtime audio-out | $0.24/dk | 240 dk (bot 80% sure) | $57.60 |
| GPT-4o-mini intent (paralel, 1 cagri = ~20 turn × ~500 token in/out) | $0.15/1M in + $0.60/1M out | 100 × 10k token in + 5k out | $1.50 |
| ElevenLabs Turbo TTS (Realtime kullanmazsa fallback) | $0.18/1k char | 100k char | $18 |
| AWS S3 recording storage | $0.023/GB/ay | 30GB | $0.69 |
| **Toplam (Realtime path)** | | | **~$77.10/100 cagri** |
| **Toplam (fallback Deepgram + GPT-4o-mini + ElevenLabs)** | | | **~$42/100 cagri** |

**Pricing onerisi:** Tenant icin "voice add-on" SKU — $99/ay 500 dk dahil + $0.20/ek dk. Cost ratio 4-5x healthy margin.

## 10. Contract References

| Contract | Dosya | Olusturulacak Faz |
|----------|-------|--------------------|
| `IVoiceCallProvider` arayuzu | `Invekto.Shared/Contracts/Voice/IVoiceCallProvider.cs` | F0 |
| `VoiceTypes` (Opus frame DTO, latency event DTO) | `Invekto.Shared/Contracts/Voice/VoiceTypes.cs` | F0 |
| Toniva gRPC proto **frozen** | `arch/contracts/voice/toniva-bridge.proto` | F0 (impl F2) |
| Browser ↔ VoiceRuntime WS protocol | `arch/contracts/voice/voice-runtime-ws.md` | F0 |
| Realtime API session config | `arch/contracts/voice/openai-realtime-session.json` | F0 |
| WhatsApp Calling webhook event'leri | `arch/contracts/voice/whatsapp-calling.json` | F3 |
| Flow node schema extension | `arch/contracts/voice/voice-flow-nodes.json` | F2 |
| Voice runtime API | `arch/contracts/voice/voice-runtime-api.json` | F2 |
| DB Schema | `arch/db/voice.sql` (yeni dosya) | F2 |
| Error codes | `arch/errors.md` INV-VR-001..010 (F0), INV-VR-011..040 (F2), INV-VR-041..050 (F3) | per faz |

## 11. Scope Boundaries

### In Scope
- WhatsApp ses notu (async)
- WhatsApp Business Calling inbound (canli, Beta)
- Toniva/MicroSIP PBX inbound (canli)
- Flow Builder UI'da 5 yeni node tipi
- KVKK consent + opt-out DTMF/intent
- Recording (opsiyonel, tenant ayari, KMS encrypted)
- Insana baglanma (REFER + agent queue + callback fallback)
- Transcript + intent timeline + Dashboard playback
- Sub-saniye barge-in (yarim-duplex F2, full-duplex F4)
- RI Insight Engine 8. modul ("Voice AI Performance")

### Out of Scope (Explicit)
- **Outbound aramalar** (bot → musteri) — ayri paket (FEAT-VFB-OUTBOUND backlog)
- **Conference / 3-way call** — backlog
- **Real-time language translation in-call** — backlog
- **Voice biometric authentication** — backlog
- **Toniva agent UI** — Q taraf, ayri proje
- **WhatsApp outbound video** — FEAT-VCP scope'unda
- **SMS/IVR DTMF-only menu** — modern voice flow tasarimina aykiri, eklenmiyor

### Degismeyen Alanlar (Pre-existing)
- FEAT-DMP `{{lead.name}}` placeholder substitution — voice node'larda ayni resolver kullanilir (text → TTS once)
- FEAT-TFM field mapping — voice_collect intent extraction ayni tenant field'ina yazar
- FEAT-MCC campaign template — `{{campaign.cities_human}}` voice'ta da gecerli
- FlowEngineV2.cs cekirdek logic — sadece yeni node handler'lar eklenir, mevcut text path **degismez** (regression riski 0)
- VoiceAI :7114 mevcut `/api/v1/voice/transcribe` — deprecate edilmez, F1'de yeni endpoint **ek olarak** gelir
- INMA chatoperation bridge — dokunulmaz (FEAT-J2 + DMP precedent)

## 12. Risk & Mitigation

| Risk | Olasilik | Etki | Mitigation |
|------|----------|------|------------|
| Toniva PJSIP hook gecikmesi (Q paralel work) | MED | F2 blocker | F0 PoC + gRPC contract early-freeze + Toniva-side dev kickoff F1 ile paralel basla |
| OpenAI Realtime API ucretlendirme degisikligi / sunset | LOW | High cost or rewrite | Hibrit pipeline'da (AD-4) fallback (Deepgram + GPT-4o + ElevenLabs) zaten implement — swap config-driven |
| WhatsApp Calling Beta API breaking change | MED | F3 paketi yeniden yazim | Feature flag (`voice_calling_enabled`) tenant bazinda kapatilabilir, F3 bagimsiz fazda |
| KVKK uyumsuzluk (transcript leak, recording yanlis tenant) | LOW | High legal | Tenant-scoped KMS key + cross-tenant 403 unit test + `INV-VR-AUTH-*` error codes |
| Sub-saniye gerceklesemez (>1.5sn) | MED | Q hayalkırıklığı | F0 PoC erken olcum + AD-8 "pre-greeting padding" fallback |
| 2-LLM (Realtime + intent classifier) maliyet patlamasi | LOW | Margin erozyonu | Cost model (sekzyon 9) tenant pricing'i destekliyor; intent classifier sadece partial transcript birikince calisir (her 2sn, her token degil) |
| Saglik sektoru ozel nitelikli veri kacagi | LOW | High legal | Saglik vertical icin recording DEFAULT KAPALI (AD-9) + transcript opt-out 24h purge (AC-9) |
| Latent: leads.last_call_at denormalize raceconditon | LOW | RI insight yanlis | UPSERT pattern + advisory lock (FEAT-LIW precedent) |
| Toniva tarafi C++ build cikti farkli (Q tek developer) | MED | F2 deploy gecikme | gRPC contract early-freeze (F0), versioning `voice/v1`, Toniva tarafi tek-yon backward compat |

## 13. Open Questions — RESOLVED (Q interview 2026-05-23)

- **OQ-1 → AD-13:** **TTS tenant-secimli** (`tenant_settings.tts_provider` enum: `realtime` default / `azure_neural_tr` premium / `elevenlabs_clone` premium plus). Voice clone F2'de DI (ElevenLabs Pro $99/ay + $1100/yil clone), F4'te self-host alternatif (XTTS-v2 B-VFB-SELFHOST backlog).
- **OQ-2 (DEFERRED to F2):** Dashboard'da agent musaitlik widget — F2 sonu MVP'de salt-okunur log + sayac. Full agent UI Toniva tarafi (Q scope disi).
- **OQ-3 (DEFERRED to F2):** Callback fallback **hem SMS hem WA** (Outbound :7107 zaten her ikisini destekliyor). KVKK opt-out durumunda sadece SMS. Detay F2 plan'inda.
- **OQ-4 → AD-15:** **VoiceRuntime (:7115) tek yazici sahip**. `call_sessions` + `voice_turns` + `callback_requests` migration sahibi VoiceRuntime. Dashboard Backend HTTP proxy okur.
- **OQ-5 → AD-14:** **Mevcut `/flow-builder` node palette extension**. 5 voice node mevcut editor'de gruplandirilir. Ayri sayfa YOK.
- **OQ-6 (KARAR):** Saglik tenant recording **DEFAULT KAPALI** (AD-9 zaten). Audit metadata (`caller_id_hash`, `intent_count`, `duration_sec`, `transfer_outcome`) **HER ZAMAN** tutulur (KVKK m.10 isleme gerekceleri icin minimum bilgi). Full transcript opt-in.
- **OQ-7 → AD-16:** **Realtime API outage hard-fail → REFER + Hangfire callback**. Hybrid Deepgram pipeline kod F2'de hazir ama config flag ile **DEVRE DISI** default. F4'te aktive edilir (`tenant_settings.voice_outage_fallback_pipeline=true`).

### Yeni Open Questions (F2/F3 oncesi sorulacak)

- **OQ-8 (F2 oncesi):** Smart-Turn v2 ONNX model file boyutu ~50MB. Git'e mi LFS'e mi koyacagiz, yoksa runtime'da Hugging Face'den indirip cache'lemek mi? (Bandwith vs deploy hizi)
- **OQ-9 (F2 oncesi):** ElevenLabs voice clone tenant onboarding flow nasil? Doktor 15dk ses kaydi nereden alacak (Dashboard upload mu, telefonla mi)? KVKK aydinlatma metni?
- **OQ-10 (F3 oncesi):** Meta WhatsApp Business Calling API allowlist application zamanlamasi — F2 baslangic'ta basvurmali mi (4 hafta onay suresi) yoksa F3 baslangicta mi (yetismez)?
- **OQ-11 (F4 oncesi):** Self-host backlog B-VFB-SELFHOST activation gate'i (15+ premium tenant veya $300+/ay LLM spend) gercek tenant sayisini hangi noktada gozlemleyecegiz?

## 14. Deliverable Ozeti

- **Migration:** 1 yeni (`arch/db/migrations/050-voice-runtime-bootstrap.sql`) — `call_sessions`, `voice_turns`, `callback_requests`, `tenant_settings.voice_*` kolonlari, denormalize `leads.last_call_at`
- **Yeni Servis:** `Invekto.VoiceRuntime` (:7115) — ~3500 LoC tahmini (RTP/WS handlers + provider impls + Realtime client + VAD + DB write)
- **Genisleyen Servisler:** Automation (5 yeni node handler ~400 LoC), VoiceAI (1 endpoint ~80 LoC), Backend (8 endpoint + SPA ~600 LoC), Integrations (WA Calling adapter ~200 LoC), Marketing (RI 8. modul ~250 LoC)
- **SPA pages:** `/calls` + `/calls/{id}` + `/flow-builder` voice node palette + `/settings/voice-runtime` + `/reports/voice-perf`
- **Errors:** ~50 yeni kod `INV-VR-001..050`
- **Test:** F0 PoC latency raporu + F1-F4 her birinin AC verification scripti
- **Doc:** Bu spec + 5 contract dosyasi + `tracking/feat-vfb-voice-flow-builder.md` master tracking

---

**SPEC DURUMU:** APPROVED (Q onay 2026-05-23). F0 plan JSON `arch/plans/20260523-feat-vfb-f0-poc.json` DONE; /auto workflow F0 implementation kapsami su anda kod aşamasında.
