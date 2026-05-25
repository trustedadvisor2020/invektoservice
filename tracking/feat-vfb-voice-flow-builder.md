# FEAT-VFB: Voice Flow Builder

> **Slug:** feat-vfb | **Spec:** [SPEC-008](../arch/specs/voice-flow-builder.md) | **Mod:** ACTIVE-F0 (PROD-DEPLOYED)
> **Versiyon:** v2.0 | **Olusturma:** 2026-05-17 | **Son Guncelleme:** 2026-05-25 (F0 prod deploy — `InvektoVoiceRuntime` NSSM service Running on :7115, voice.invekto.com URL reverse proxy Q manuel adım)
> **Risk:** HIGH | **Sure tahmini:** 7-9 hafta (F0-F4 toplam)
> **Onkosul:** Faz A demo toparlama DONE (su an A6 son), Toniva-tarafi paralel work hazirligi (Q) → 2026-05-23: **Q karari F1 sonu Toniva C++ basla, F0 microphone-only**

## F0 Production Deploy (2026-05-25 18:03 UTC+3)

**AD-20 override:** F0 başlangıçta "sadece Q laptop'unda, F2'de NSSM register" olarak planlanmıştı. Q kararı 2026-05-25: müşteri demo + mikrofon test sayfası iteration için prod'a deploy. Otomatik 9 adım completed.

| Adım | Sonuç |
|------|-------|
| 1. `dotnet publish -c Release` | 0 error / 5 warning (Opus obsolete + Shared async, F0-known) |
| 2. Zip publish output (106.88 MB; silero_vad.onnx + Microsoft.ML.OnnxRuntime runtimes + Shared.dll + wwwroot voice-poc) | OK |
| 3. Server folder: `C:\Invekto\VoiceRuntime\{current,previous,incoming}` | Created |
| 4. Upload zip + Expand-Archive → current\ | OK; .exe + Models\silero_vad.onnx + wwwroot\voice-poc.html doğrulandı |
| 5. `appsettings.Production.json` (Backend Jwt copy + Cors `voice.invekto.com` + `app.invekto.com` + ListenPort 7115) | OK; HasSecret=True Issuer=InvektoServis Audience=InvektoServis CorsCount=2 |
| 6. NSSM install `InvektoVoiceRuntime` + AppDir + DisplayName + AppRotate + AppEnvironmentExtra=`ASPNETCORE_ENVIRONMENT=Production` | OK |
| 7. Start-Service → Running; `/health` 200 ok; `/ready` 200 degraded (missing OPENAI_API_KEY, beklenen) | OK |
| 8. `/voice-poc.html` static serve 200 (2087 bytes) | OK |
| 9. Backend Jwt cross-service token validation pattern | Issuer/Audience/SecretKey aynı → tenant=0 JWT VoiceRuntime'da geçerli |

**Manuel adımlar TAMAMLANDI (Q + otomasyon hibrit, 2026-05-25 18:25 UTC+3):**
1. ✅ DNS A record: `voice.invekto.com → 213.238.172.214` (Q manuel tanımladı, dev PC'den DNS resolve OK)
2. ✅ TLS cert: `C:\Invekto\certs\star.invekto.com.pfx` (mevcut wildcard cert, Backend ile aynı) — Kestrel HTTPS :8443 doğrudan kullanıyor
3. ✅ Reverse proxy: **GEREK YOK** — Backend pattern (Kestrel direct TLS :443) sürdürüldü. VoiceRuntime kendi Kestrel'inde :8443 HTTPS dinliyor. Cloudflare proxy YOK (DNS-only, 8443 serbest), IIS/nginx YOK.
4. ✅ OPENAI_API_KEY: Knowledge servisinin appsettings'inden alındı (sk-proj-... 164 char) → NSSM AppEnvironmentExtra'ya set + service restart → `/ready=ready` (degraded değil)
5. ✅ Firewall: `New-NetFirewallRule` TCP :8443 inbound allow (Profile Any)
6. ✅ Cors:AllowedOrigins port-aware: `["https://voice.invekto.com", "https://voice.invekto.com:8443", "https://app.invekto.com"]` (WS Origin gate match)
7. ✅ Public test (dev PC → voice.invekto.com:8443): TCP open + `/health` ok + `/voice-poc.html` 200/2087 bytes

**Public URL:** `https://voice.invekto.com:8443/voice-poc.html?token={tenant=0 JWT}` — JWT token Dashboard login'inden alınır (cookie veya localStorage). Q DevTools ile alıp URL'e ekleyerek test eder, ya da Dashboard'a "Voice Test" linki eklemek sonraki paket.

## Karar Trail (Q interview, 2026-05-17)

| Soru | Cevap | Sonuc |
|------|-------|--------|
| 3. madde (yarim mesaj) | "telefonu insana bagla VE tam cagri gecmisi" + ek oneriler bekleniyor | `voice_transfer` node + `call_sessions` tablosu + Dashboard `/calls` sayfasi spec'e dahil |
| PBX kopru | Toniva/MicroSIP uzerinden | gRPC bidi stream + PJSIP pjmedia hook (C++ Q paralel) |
| Hiz hedefi | Sub-saniye barge-in | OpenAI Realtime API kritik path, AD-4 hibrit pipeline |
| Sira | Faz A bittikten **hemen sonra** | FEAT-INMA-PIPELINE-V2 C2/C3/C4 BLOCKED chunk'lari arka plana, bu paket Faz B'den **once** baslar |
| AI pipeline | Hibrit (Realtime + paralel intent classifier) | gpt-4o-realtime audio i/o + GPT-4o-mini paralel intent (her 2sn partial transcript) |
| Toniva iscligi | Q paralel yapacak (interview G2) | Bu paket Invekto-only; gRPC contract early-freeze F0'da |
| Yeni bilgi (2026-05-17 sonra) | WhatsApp inbound canli aramalar da dahil (sadece ses notu degil) | Provider list 3'e cikti: TonivaPbx + WhatsAppCall + WhatsAppVoiceNote |

## Karar Trail (Q customer prospect interview, 2026-05-23)

Musteri adayi geldi: "PBX'e gelen aramalari AI ile cevaplamak istiyoruz. Onceki firma yavasti, dusuk maliyet + hizli olsun." Q satis hedefi → F0 PoC kodlamasi hemen baslar.

### Interview Tur 1 (4 kritik karar)

| Soru | Cevap | Sonuc |
|------|-------|--------|
| OpenAI Realtime API key | Hesap var, Realtime tier aktive edilecek (1-2 saat is) | F0 hemen baslar paralel |
| Toniva PJSIP hook (C++) zamanlama | F1 sonunda baslat (1-2 hafta gecikme riski kabul) | Mock provider F2 boyunca, MicrophoneProvider F0/sales-demo, gRPC contract F0'da frozen |
| TTS provider stratejisi | Tenant-secimli (Realtime / Azure Neural / ElevenLabs) | **AD-13**: `tenant_settings.tts_provider` enum + F2 DI 3 impl. Cost matrix tracking. |
| Voice flow editor UI | Mevcut `/flow-builder` node palette ekle | **AD-14**: 5 voice node mevcut editor'de gruplandirilir, ayri sayfa YOK |

### Interview Tur 2 (Open Questions + provider research)

| Soru | Cevap | Sonuc |
|------|-------|--------|
| OQ-4 DB ownership | VoiceRuntime (:7115) tek yazici | **AD-15**: Migration 050 sahibi VoiceRuntime, Dashboard Backend HTTP proxy read |
| OQ-7 Realtime outage davranis | Hemen REFER + callback (hybrid pipeline kod hazir, DEVRE DISI default) | **AD-16**: Hard-fail config flag, F4'te opt-in aktive |
| Provider key durumu (ElevenLabs/Azure/AWS S3/Meta WA) | "Bu servisler icin arastirma yap, maliyet/fayda analizi" | TTS provider matrix cikartildi: Realtime $0.30/dk (sub-saniye) vs Azure Neural TR $0.14/dk (1.2sn) vs ElevenLabs clone $99/ay+$1100/yil. F2'de DI hepsi. |
| F0 PoC scope (Mock+WAV vs gercek mikrofon) | "microfonla telefon gorusmesi gibi yapalim" | **AD-11/AD-17**: Browser WebRTC + WS + Realtime → 5-7 gun F0. Sales-ready demo sayfasi. |
| HuggingFace bilesenler | Silero VAD (F0), Faster-Whisper (F1 opsiyonel), XTTS-v2 (F4 backlog B-VFB-SELFHOST) | F0'da Silero VAD + F4'te self-host gate (15+ premium tenant) |
| Endpoint detection (VAD yetersiz, "cumle ortada kalmis duraksama") | 3-katmanli pipeline: Silero VAD + Smart-Turn v2 + adaptive timeout | **AD-12**: F0'da OpenAI native semantic_vad beta, F2'de Smart-Turn v2 HuggingFace ONNX (+35ms latency, $0 cost) |
| Azure TR daha iyi+ucuz mi | "Dezavantajlari var mi" | 5 dezavantaj: +300-500ms latency, robotik barge-in, 3x outage, no natural filler, tool call hop. Sub-saniye hedefi i Realtime ile, Azure premium TR tenant opt-in. |

## Mimari Karar Ozeti (2026-05-23 sonrasi)

| AD | Karar | Faz |
|----|-------|-----|
| AD-11 | F0 PoC mikrofon-based (Mock+WAV degil) | F0 |
| AD-12 | Smart-Turn v2 (HF ONNX) F2'de Silero VAD ustune semantic EOT | F2 |
| AD-13 | TTS tenant-selectable enum (realtime/azure_neural_tr/elevenlabs_clone) | F2 |
| AD-14 | Voice node mevcut /flow-builder palette extension | F2 |
| AD-15 | VoiceRuntime tek yazici, Backend HTTP proxy read | F2 (Migration 050) |
| AD-16 | Realtime outage hard-fail REFER+callback (hybrid kod hazir, default DEVRE DISI) | F2 (kod) / F4 (opt-in) |
| AD-17 | Microphone provider Toniva protocol clone (sales-demo da kullanir) | F0 |

## Backlog Activated

| Slug | Aciklama | Gate |
|------|----------|------|
| B-VFB-SELFHOST | HuggingFace self-host stack (Faster-Whisper Turbo + XTTS-v2 voice clone, KVKK premium tier, cost optim) | 15+ premium tenant VEYA $300+/ay LLM spend |

## Faz Plani

### F0 — PoC Microphone-based & Contract Freeze (5-7 gun)

**Plan:** `arch/plans/20260523-feat-vfb-f0-poc.json` (Q customer demo motivasyonu)

- [ ] Invekto.VoiceRuntime servis skelet (csproj + Program.cs + DI + JWT + JsonLines + /health + Concentus NuGet + Microsoft.ML.OnnxRuntime NuGet)
- [ ] `IVoiceCallProvider` Shared.Contracts.Voice + MicrophoneCallProvider impl
- [ ] WS endpoint `/ws/voice/microphone` (JWT-gated, Q tenant=0 superadmin only)
- [ ] OpusCodec (Concentus C# managed decode/encode 48kHz mono 20ms)
- [ ] SileroVad (Microsoft.ML.OnnxRuntime + `Models/silero_vad.onnx` MIT, CPU inference ~5ms)
- [ ] PcmResampler (48kHz Opus → 24kHz Realtime native)
- [ ] RealtimeApiClient (System.Net.WebSockets, WSS wss://api.openai.com/v1/realtime?model=gpt-4o-realtime, audio-in/out PCM16 24kHz, session config turn_detection=semantic_vad beta)
- [ ] LatencyTracker (t0 VAD silence, t1 first-byte audio, t2 barge-in → jsonl + /metrics endpoint p50/p95/p99)
- [ ] Browser test sayfasi `wwwroot/voice-poc.html` + `voice-poc.js` + `audio-worklet.js` (getUserMedia 48kHz mono + AudioWorklet Opus encode WebCodecs API + WS client + Web Audio API playback with jitter buffer + HUD live latency + transcript live render)
- [ ] Toniva-bridge gRPC proto v1 (`arch/contracts/voice/toniva-bridge.proto` FROZEN F0)
- [ ] Browser ↔ VoiceRuntime WS protocol doc (`arch/contracts/voice/voice-runtime-ws.md`)
- [ ] OpenAI Realtime session config snapshot (`arch/contracts/voice/openai-realtime-session.json`)
- [ ] arch/errors.md INV-VR-001..010 + ErrorCodes.cs mirror
- [ ] Latency olcum scriptli 10 senaryo × 3 round → `arch/reports/feat-vfb-f0-latency-report.md`
- [ ] Q go/no-go karari (sub-saniye p95 < 1000ms F0 hedef)
- [ ] InvektoServis.sln Invekto.VoiceRuntime projesi ekle

**Exit:** Latency raporu PASS (p95 < 1000ms) + Q live demo testi PASS + Codex iter ≤ 2 + 3 contract dosyasi DONE + Toniva gRPC proto FROZEN (Q paralel C++ basla sinyal)

### F1 — WhatsApp Voice Note (1 hafta)
- [ ] Plan JSON: `arch/plans/20260520-feat-vfb-f1-wa-voice-note.json`
- [ ] Automation Program.cs:504 audio type filter relax (text-only → text+voice_note)
- [ ] VoiceAI yeni endpoint: `POST /api/v1/voice/transcribe-and-inject` (transcribe → callback Automation FlowEngineV2)
- [ ] FlowEngineV2: `trigger_kind='wa_voice_note'` distinguishable + log `[VOICE-NOTE→TEXT]`
- [ ] Error codes INV-VR-001..010 (audio download, format, transcribe failure, FlowEngine inject failure)
- [ ] Pilot smoke: Dent tenant WhatsApp test telefonundan ses notu → bot text cevap

**Exit:** AC-1 PASS + Codex iter ≤ 2 + deploy + smoke + 0 regression Automation text path

### F2 — PBX Live (Toniva) MVP (3-4 hafta)
- [ ] Plan JSON: `arch/plans/20260527-feat-vfb-f2-pbx-live-mvp.json`
- [ ] Yeni servis `Invekto.VoiceRuntime` (:7115) — proje skelet + DI + Hangfire + JsonLines + Auth middleware (FEAT-DBBK precedent)
- [ ] Migration 050: `call_sessions` + `voice_turns` + `callback_requests` + `tenant_settings.voice_*` + `leads.last_call_at`
- [ ] `IVoiceCallProvider` (Shared) + TonivaPbxProvider + MockProvider
- [ ] VAD (Silero veya WebRTC VAD) integration
- [ ] OpenAI Realtime API client (WebSocket, bidi audio)
- [ ] Paralel intent classifier (GPT-4o-mini her 2sn partial transcript)
- [ ] FlowEngineV2: 5 yeni node handler (`voice_trigger`, `voice_say`, `voice_collect`, `voice_transfer`, `channel_condition`)
- [ ] KVKK consent flow + DTMF "9" + opt-out 24h purge job
- [ ] `voice_transfer` → Toniva REFER + agent queue + callback fallback (Hangfire `CallbackRequestJob`)
- [ ] Dashboard SPA `/calls` liste + detay (transcript timeline + intent + recording playback)
- [ ] Dashboard SPA `/flow-builder` voice node palette + property panel
- [ ] Backend proxy endpoint'ler (Backend → VoiceRuntime)
- [ ] Tenant ayarlari `/settings/voice-runtime` (provider secimi, recording, retention, consent text)
- [ ] Error codes INV-VR-011..040
- [ ] Pilot smoke: Q'nun test Toniva line → 5 sn KVKK consent + bot dialog + handoff REFER

**Exit:** AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9 PASS + Codex iter ≤ 3 + deploy + 1 tenant smoke

### F3 — WhatsApp Calling + Multi-tenant (2 hafta)
- [ ] Plan JSON: `arch/plans/20260624-feat-vfb-f3-wa-calling.json`
- [ ] `WhatsAppCallProvider` implementation
- [ ] Integrations adapter: WA Cloud Calling webhook (`call.connect`, `call.terminate`, `call.permission_action`)
- [ ] Media stream WebSocket handler
- [ ] Feature flag `voice_calling_enabled` per tenant
- [ ] Migration 051: feature flag kolonu + audit log
- [ ] 2 tenant pilot smoke (Dent + 2. tenant Q seciminde)
- [ ] Error codes INV-VR-041..050

**Exit:** AC-10 PASS + Codex iter ≤ 2 + multi-tenant deploy

### F4 — Optim & Analytics (1 hafta)
- [ ] Full-duplex barge-in (overlap audio frames)
- [ ] RI Insight Engine 8. modul "Voice AI Performance" (Marketing servisi)
- [ ] Dashboard `/reports/voice-perf` widget (avg/p95 latency, intent accuracy, transfer rate, cost)
- [ ] Hangfire daily aggregation job
- [ ] Cost/billing widget

**Exit:** Production-grade + Codex iter ≤ 2

## Onkosullar (External)

| Item | Sahip | Hedef Tarih | Durum |
|------|-------|-------------|--------|
| Toniva PJSIP pjmedia hook (RTP frame export) | Q | F2 baslamadan once | NOT STARTED |
| Toniva agent queue altyapisi | Q | F2 baslamadan once | NOT STARTED |
| Toniva REFER routing | Q | F2 sonu | NOT STARTED |
| OpenAI Realtime API key (production tier) | Q provision | F0 baslamadan once | NOT REQUESTED |
| Meta WhatsApp Calling API allowlist | Q (BM submit) | F3 baslamadan 4 hafta once | NOT REQUESTED |
| ElevenLabs Turbo API key (fallback) | Q | F2'de opsiyonel | NOT REQUESTED |
| AWS S3 bucket (recording storage) | Q | F2'de opsiyonel | NOT REQUESTED |

## Open Questions (Q'ya sorulacak)

OQ-1..OQ-7 → bkz. [SPEC-008 sekzyon 13](../arch/specs/voice-flow-builder.md#13-open-questions-drafttan-approveda-gecmeden-once-qya-sorulacak)

## Latent Risks

- **Toniva tek developer (Q):** F2 blocker olabilir. Mitigation: gRPC contract early-freeze, Mock provider F2 boyunca calisir, Toniva tarafi gecikme F3'e kayar
- **WhatsApp Calling Beta status:** API breaking change durumunda F3 yeniden yazim — feature flag ile izole, geri donus kolay
- **Sub-saniye barge-in olcum risikli:** F0 PoC olcum 1.5sn+ verirse Q ile yeniden konusulur (pre-greeting padding fallback AD-8'de var ama UX hayalkırıklığı olabilir)
- **Microservice izolasyon ihlali olasiligi:** VoiceRuntime ↔ Automation arasi FlowEngineV2 inject HTTP/SignalR uzerinden — direkt referans YASAK (CLAUDE.md). Shared.Contracts.Voice araci.

## Bagimlilik Zinciri

```
Faz A demo toparlama (A6 DONE 2026-05-12)
  │
  └─ (Q kullanici karari 2026-05-17): FEAT-VFB once
        │
        ├─ F0 PoC (3-5 gun) ────────────────────────┐
        │                                           │
        ├─ F1 WA Voice Note (1 hafta) ──────────┬── │ → Toniva PJSIP hook (Q paralel)
        │                                       │   │
        ├─ F2 PBX Live MVP (3-4 hafta) ─────────┤   │
        │                                       │   │
        ├─ F3 WA Calling + Multi (2 hafta) ─────┤   │ → Meta BM submit (Q paralel)
        │                                       │   │
        └─ F4 Optim & Analytics (1 hafta)        │
                                                │
FEAT-INMA-PIPELINE-V2 C2/C3/C4 (BLOCKED INMA)  ─┘
  │
  └─ INMA contract gelince paralel devam, FEAT-VFB durdurmaz
```

## Codex Audit Notlari

Plan JSON yazilirken bilincli kararlar:
- AD-1 (yeni servis VoiceRuntime) — CQ4 false positive bekleniyor (servis genisletme onerisi yerine ayri yeni servis)
- AD-3 (Opus codec canonical) — CQ7 codec diversity skip
- AD-4 (hibrit 2-LLM) — CQ3 cost concern, ROI sekzyon 9'da
- AD-5 (AES-GCM transcript) — CQ11 plaintext kolonu skip
- AD-7 (Toniva C++ scope disi) — missing-impl false-positive skip (contract bu repoda)
- Cross-channel intent injection (text path UNTOUCHED, AD: zero-regression guarantee) — pre-existing pattern skip

## Smoke Plani (her faz icin)

| Faz | Smoke senaryo | Sure |
|-----|---------------|------|
| F0 | 1 dakikalik test cagri Mock provider + Realtime API + Q olcum aleti | 30dk |
| F1 | Dent tenant WA test telefonundan 3 ses notu (FAQ + intent + ozel chars) | 45dk |
| F2 | Q test Toniva line → 5 sn KVKK consent + 3 farkli flow (welcome + FAQ + handoff) + DB call_sessions + Dashboard /calls oynat | 2 saat |
| F3 | 2 tenant WA Calling smoke (Dent + 2. tenant) — 3 cagri her tenant | 2 saat |
| F4 | RI widget verisi + cost report + full-duplex stress (10 paralel cagri) | 1 saat |
