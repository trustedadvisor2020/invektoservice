# FEAT-VFB F0 PoC — Browser Smoke Test Senaryoları (Adım Adım)

> **Slug:** `20260523-feat-vfb-f0-poc` | **Tarih:** 2026-05-24 | **Test ortami:** Q laptop, Chrome 130+, `http://localhost:7115/voice-poc.html` | **Mod:** dev=1 bypass (localhost) | **Ses:** Türkçe TR-TR

Bu dosya F0 PoC live smoke test senaryolarını tam adım-adım sistem akışıyla sıralar. Her senaryoda **Q'nun davranışı + browser tarafı + VoiceRuntime tarafı + OpenAI Realtime tarafı** ayrı satırlarda. Q test ederken her adımı işaretleyebilir, takıldığı yeri bildirebilir.

---

## STANDART AKIŞ (her senaryoda ortak, 1-22 numaralı adımlar)

Aşağıdaki adımlar **TÜM senaryolarda** aynı şekilde gerçekleşir. Spesifik senaryolar (1-15) bu standart akış üstüne **fark/override** olarak yazılmıştır.

### A. Setup (her test öncesi — 1 kez)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **S1** | Q | `http://localhost:7115/voice-poc.html` aç (Chrome) | Sayfa yüklenir, "🎤 Mikrofonu Başlat" buton aktif |
| **S2** | Q | "Mikrofonu Başlat" tıkla | Browser mic izni popup |
| **S3** | Q | "İzin Ver" seç | `getUserMedia` başarılı, status "Hazır" → "Bağlı, konuşabilirsin" |
| **S4** | Browser | `AudioContext({sampleRate: 48000})` create | `audioCtx.sampleRate === 48000` (donanım kabul etmezse fail-fast `INV-VR-009`) |
| **S5** | Browser | `audioCtx.audioWorklet.addModule('audio-worklet.js')` | Worklet load, MediaStreamSource + AudioWorkletNode bağlanır |
| **S6** | Browser | WS connect: `ws://localhost:7115/ws/voice/microphone?dev=1&locale=tr-TR` | Backend Origin check (localhost OK), dev bypass kabul (`Jwt:SecretKey` boş), 101 Switching Protocols |
| **S7** | VoiceRuntime | `HandleMicrophoneWsAsync` accept WS | Session ID `f0-{guid}`, `MicrophoneCallSession` open, `sessionCts` linked to `ctx.RequestAborted` |
| **S8** | VoiceRuntime | `RealtimeApiClient.ConnectAsync()` → `wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview` | WS 101, `Authorization: Bearer $env:OPENAI_API_KEY` + `OpenAI-Beta: realtime=v1` |
| **S9** | VoiceRuntime | `SendSessionUpdateAsync(DefaultConfig)` | Realtime session config: `modalities=["audio","text"]`, `voice=alloy`, `input_audio_format=pcm16`, `output_audio_format=pcm16`, `turn_detection.type=semantic_vad`, `instructions=Türkçe AI asistan` |
| **S10** | VoiceRuntime | 3 paralel loop başlat: `BrowserRxLoop`, `BrowserTxLoop`, `VoiceToRealtimeForwardLoop` | Async tasks running |

### B. Q konuşma → Bot cevap (her senaryo için bu döngü tekrarlanır)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **T1** | Q | Mikrofona konuş (örnek: "Saç ekimi fiyatları nedir?") | Ses analog dalga |
| **T2** | Browser | `getUserMedia` PCM samples → `AudioWorkletNode.process()` | 128-sample chunks her 2.67ms |
| **T3** | Browser | AudioWorklet 960-sample (20ms) buffer doldurur → PCM16 LE → `port.postMessage(ArrayBuffer)` | 1920 byte frame her 20ms |
| **T4** | Browser | Main thread `ws.send(arrayBuffer)` binary frame | WS binary 1920 byte |
| **T5** | VoiceRuntime | `BrowserRxLoop` 1920-byte binary al → `OpusFrame(payload, ts, seq)` → `session.PushIncomingAsync()` | Bounded `Channel<OpusFrame>(200)` DropOldest |
| **T6** | VoiceRuntime | `VoiceToRealtimeForwardLoop` frame oku → `byte[] → short[] PCM48k` (LE decode) | 960 sample PCM48k |
| **T7** | VoiceRuntime | `SileroVad.ProcessFrame48k(vadState, pcm48k)` → 48→16 downsample 3:1 → 320 sample PCM16k buffer'a ekle | Buffer 512-sample window'a ulaşınca ONNX inference |
| **T8** | VoiceRuntime | Silero inference: `probability` 0.0..1.0 | >0.5 = speech, <0.5 = silence |
| **T9** | VoiceRuntime | Speech onset (önceki silence → şu an speech): `_userSpeaking=true`, `turn.StampSpeechStart()` | `LatencyEvent UserSpeechStart` jsonl log + HUD "Dinleniyor" |
| **T10** | VoiceRuntime | `PcmResampler.Downsample48To24(pcm48k)` → 480 sample PCM24k | 24kHz native Realtime |
| **T11** | VoiceRuntime | `PcmResampler.PcmToBase64(pcm24k)` → string | base64 |
| **T12** | VoiceRuntime | `RealtimeApiClient.SendAudioAsync(base64)` → `input_audio_buffer.append` event | WS text JSON to OpenAI |
| **T13** | OpenAI Realtime | Audio buffer accumulate, semantic_vad continuous evaluate | Henüz turn ended algılamadı |
| **T14** | Q | Konuşmayı bitir (örnek: "...nedir?" → 200ms+ silence) | Ses durdu |
| **T15** | VoiceRuntime | Silero VAD silence detect (200ms boş window) → `_userSpeaking=false`, `turn.StampSpeechEnd()` | `LatencyEvent UserSpeechEnd` |
| **T16** | OpenAI Realtime | Semantic VAD turn ended → otomatik `response.create` | İçeride STT+LLM+TTS pipeline başlar |
| **T17** | OpenAI Realtime | `input_audio_buffer.speech_started` + `input_audio_buffer.speech_stopped` events | VoiceRuntime callback'leri tetiklenir |
| **T18** | OpenAI Realtime | LLM (gpt-4o-realtime) cevap üretir + TTS streaming başlar | `response.audio.delta` events başlar (base64 PCM24k chunks) |
| **T19** | VoiceRuntime | `OnAudioDelta(delta)` callback: **ilk delta'da** `turn.StampTtsFirstByteToUser()` → `LatencyEvent TtsFirstByteToUser` + browser "first_byte" text frame | HUD `İlk byte: {ms}` güncellenir |
| **T20** | VoiceRuntime | Her delta için: `Base64ToPcm` → `Upsample24To48` → PCM16 LE byte[] → `OpusFrame(payload)` → `session.SendOutgoingFrameAsync()` | Outbound bounded channel |
| **T21** | VoiceRuntime | `BrowserTxLoop` outgoing frame oku → `SemaphoreSlim _sendLock` (concurrent SendAsync race önler) → `ws.SendAsync(binary)` | 1920 byte frame her 20ms |
| **T22** | Browser | binary frame al → `Int16Array` decode → `AudioBuffer create` → `AudioBufferSourceNode` queue → speaker playback | Q bot sesini duyar |

### C. Cleanup (her test sonu)

| # | Aktör | Eylem | Beklenen |
|---|-------|-------|----------|
| **C1** | OpenAI Realtime | `response.audio_transcript.delta` events — full Türkçe transcript | VoiceRuntime "transcript_bot" text WS → browser logTranscript bubble |
| **C2** | OpenAI Realtime | `response.done` event | VoiceRuntime "response_done" → browser HUD reset |
| **C3** | Q | "Durdur" tıkla (veya tarayıcı yenile) | WS close client-side |
| **C4** | VoiceRuntime | `ctx.RequestAborted` tetiklenir → `sessionCts.Cancel()` → 3 loop OperationCanceledException swallow | Cleanup başlar |
| **C5** | VoiceRuntime | `await using RealtimeApiClient.DisposeAsync()` → Realtime WS close + 2s WhenAny timeout + IDisposable cleanup | OpenAI WS Bye |
| **C6** | VoiceRuntime | `await using MicrophoneCallSession.DisposeAsync()` → Channels TryComplete | Resource leak yok |
| **C7** | Browser | `state.micStream.getTracks().forEach(t => t.stop())` + `audioCtx.close()` | Mic indicator söner |

---

## SENARYOLAR 1-15 (her biri Standart Akış'a override)

> Her senaryo **B (T1-T22)** standart akışını izler. Aşağıda **farklı/spesifik adımlar** + **kabul kriteri** listelenir. Q test sırasında ilgili `T` adımını işaretler.

---

### Senaryo 1 — Sade Soru (baseline)

**Cümle:** "Saç ekimi fiyatları nedir?"

| Adım | Beklenen Davranış |
|------|-------------------|
| T1-T13 | Standart konuşma akışı, ~2-3sn süre |
| T14-T16 | Silence detect 200ms + semantic_vad turn ended → response.create |
| T19 | **first_byte_ms ≤ 1000ms p50, ≤ 1500ms p95** |
| T19-T22 | Bot cevabı: "Saç ekimi fiyatları seans sayısına ve klinik konumuna göre değişir, ortalama 1500-3000 EUR aralığındadır" gibi 5-10sn Türkçe TTS |
| C1 | `transcript_bot` Türkçe doğru render |

**Kabul:** first_byte<1000ms + intent doğru (fiyat aralığı içeren cevap) + Türkçe doğal aksent.

---

### Senaryo 2 — Uzun Multi-Question

**Cümle:** "Saç ekimi yaptırmak istiyorum, kaç seans, ne kadar sürer, fiyat ne olur?"

| Adım | Beklenen Davranış |
|------|-------------------|
| T1-T13 | Standart, ~5-6sn konuşma süresi |
| T18 | LLM 3 alt-soruyu sıralı işler |
| T19-T22 | Bot cevabı: 3 numaralı veya bullet listesi: "(1) Tek seans yeterli, (2) 4-6 saat, (3) 1500-3000 EUR" |
| C1 | transcript_bot 3 numarayı içerir |

**Kabul:** 3 alt-soruya tek tek cevap (atlanmış veya kaynaşmış değil) + first_byte ≤ 1200ms.

---

### Senaryo 3 — Barge-In (kritik concurrency test)

**Davranış:** Bot fiyat söylerken Q kelime arasında "Dur, indirim var mı?" diye keser.

| Adım | Beklenen Davranış |
|------|-------------------|
| T1-T22 | Bot konuşurken (TTS playback aktif, `_botSpeaking=true`) |
| T6-T9 | Yeni Q konuşması Silero VAD ile detect → `_userSpeaking=true` |
| **B1** | VoiceRuntime `OnSpeechStarted` callback: `_botSpeaking=true` ise → `turn.StampBargeInDetected()` |
| **B2** | `_ = Task.Run(async () => { await _realtime.SendResponseCancelAsync(ct) })` → Realtime'a `response.cancel` event |
| **B3** | `await _session.SignalBargeInAsync(ct)` → outgoing channel drain (kalan TTS frame'ler atılır) |
| **B4** | `turn.StampBargeInTtsStopped()` → barge_in elapsed ms hesaplanır |
| **B5** | WS text "barge_in" → browser HUD `Barge-in: {ms}` güncel |
| **B6** | OpenAI Realtime önceki response iptal, yeni Q audio buffer'a accumulate |
| T13-T22 | "indirim var mı?" yeni response başlar |

**Kabul:** barge_in_ms ≤ 500ms (HUD'da görünür) + TTS gerçekten susar (kullanıcı ses kesilmesini duyar) + yeni soruya cevap içerikli.

---

### Senaryo 4 — Duraksama (semantic EOT testi)

**Cümle:** "Yani saç ekimi... *[1.5sn bekle]* ...ne kadar oluyor?"

| Adım | Beklenen Davranış |
|------|-------------------|
| T6-T9 | "Yani saç ekimi" sonrası Silero silence detect (200ms) |
| T15 | Silero `StampSpeechEnd` AMA bot henüz cevap üretmemeli |
| T13 | OpenAI Realtime semantic_vad — cümle bitmedi (yarım) → response.create TETİKLENMEZ |
| T6-T9 | "ne kadar oluyor?" konuşma yeniden başlar |
| T13-T16 | Kümülatif buffer üzerinden semantic_vad turn ended → response.create |
| T18-T22 | Bot fiyat aralığı verir |

**Kabul:** Bot 1.5sn duraksama esnasında cevap üretmeye **başlamaz**. Tam cümle tamamlandıktan sonra (1.5sn) cevap verir. Bu **AD-12 Smart-Turn v2 F2'de eklenecek** — F0'da OpenAI native `semantic_vad` test edilir.

---

### Senaryo 5 — Türkçe Özel Karakter

**Cümle:** "Şu işlem için bilgi alabilir miyim?"

| Adım | Beklenen Davranış |
|------|-------------------|
| C1 | OpenAI Realtime input transcript: "Şu işlem için bilgi alabilir miyim?" (Ş, İ, ç, ğ doğru) |
| T18 | LLM context: işlem ne, hangi tedavi? Genel info iste |
| T19-T22 | Bot cevap: "Tabii, hangi işlem hakkında bilgi almak istersiniz?" |

**Kabul:** Whisper transcript Türkçe karakter %100 doğru. Bot cevabı genel/açık (clarifying question) ya da kontekstli (önceki tur var ise referans verir).

---

### Senaryo 6 — Kısa Cümle

**Cümle:** "Fiyat nedir?"

| Adım | Beklenen Davranış |
|------|-------------------|
| T1-T13 | 1sn'lik kısa konuşma |
| T18 | LLM context boş, "fiyat" hangi konuda? |
| T19-T22 | Bot: "Hangi hizmetin fiyatını öğrenmek istersiniz?" (clarifying) |

**Kabul:** Bot generic "fiyatlar değişir" yerine **clarifying question** sorar (intent ambiguity handling).

---

### Senaryo 7 — Soru Tonu

**Cümle:** "Saç ekimi mi yaptırıyorsunuz?" (yükselen tonlama, role-reversal)

| Adım | Beklenen Davranış |
|------|-------------------|
| C1 | Whisper transcript "?" işaretli (soru tonu) |
| T18 | LLM role-confusion algılar: kullanıcı bot'a iş sorduğunu sanıyor |
| T19-T22 | Bot: "Ben bir AI asistanım, saç ekimi yapmıyorum ama klinik bilgisi verebilirim" |

**Kabul:** Bot kendi rolünü doğru tanır (role-reversal'a düşmez).

---

### Senaryo 8 — Conjunction Sonu (semantic EOT)

**Cümle:** "Saç ekimi ve fiyatları ve... seans sayısı?"

| Adım | Beklenen Davranış |
|------|-------------------|
| T14 | "ve... " sonrası 500ms silence |
| T13 | OpenAI semantic_vad — "ve" sonrası eksik cümle → response.create TETİKLENMEZ |
| T15-T16 | Yeni segment "seans sayısı?" gelir → semantic_vad turn ended |
| T18-T22 | Bot fiyat + seans 2'li cevap |

**Kabul:** Bot "ve..." sonrasında cevap üretmeye başlamaz (1+sn duraksamaya tolerans).

---

### Senaryo 9 — Sessizlik Öncesi

**Davranış:** Mikrofonu başlat → 5sn sus → "Merhaba?" de.

| Adım | Beklenen Davranış |
|------|-------------------|
| S1-S10 | Standart setup |
| T7-T8 | Silero VAD 5sn boyunca speech=false → response.create TETİKLENMEZ |
| Q | "Merhaba?" der |
| T9 | Speech onset detect, normal akış |
| T19-T22 | Bot karşılama: "Merhaba, size nasıl yardımcı olabilirim?" |

**Kabul:** Bot 5sn boş sessizlik için "Merhaba, oradamısınız?" gibi proaktif soru sormaz (F0 reactive only; F2'de optional proactive greeting timer eklenebilir).

---

### Senaryo 10 — Fragmented

**Cümle:** "Saç ekimi... yani... biraz bilgi"

| Adım | Beklenen Davranış |
|------|-------------------|
| T6-T9 | "Saç ekimi" → kısa silence → "yani" → kısa silence → "biraz bilgi" |
| T13 | semantic_vad fragmentleri tek tur olarak birleştirir |
| T18 | LLM context: kullanıcı saç ekimi hakkında general info istiyor |
| T19-T22 | Bot kısa info: "Saç ekimi FUE veya DHI yöntemiyle yapılır, klinikte 4-6 saat sürer" |

**Kabul:** Bot 3 fragment'i tek niyet olarak işler (3 ayrı cevap üretmez).

---

### Senaryo 11 — İnsan Transfer Talebi (intent classification)

**Cümle:** "Bir doktora bağlanabilir miyim?" veya "Sizi anlamadım, gerçek bir kişiyle konuşmak istiyorum"

| Adım | Beklenen Davranış |
|------|-------------------|
| T13 | Paralel GPT-4o-mini intent classifier (F2'de aktif, F0'da OpenAI Realtime kendi parsing) |
| T18 | Realtime LLM "transfer_to_human" intent tanır |
| T19-T22 | Bot: "Tabii, sizi temsilciye yönlendiriyorum, bir saniye lütfen" anonsu |
| **F2-ek** | `voice_transfer` flow node tetiklenir → Toniva REFER → agent queue (F0'da no-op, sadece transcript'te "yönlendiriyorum" görünür) |

**Kabul:** transcript_bot'ta "yönlendiriyorum/temsilci/bağlanıyorum" gibi transfer kelimesi var. F2'de gerçek REFER akışı eklenecek.

---

### Senaryo 12 — Çoklu Sıralı Intent (compound query)

**Cümle:** "Önce fiyatı, sonra randevu, en sonunda da iptal şartlarını söyler misiniz?"

| Adım | Beklenen Davranış |
|------|-------------------|
| C1 | Whisper transcript 3 intent açık: fiyat → randevu → iptal |
| T18 | LLM 3 step planning + sıra koruma |
| T19-T22 | Bot transcript_bot 3 ayrı bölüm: "(1) Fiyatlar...", "(2) Randevu için...", "(3) İptal şartları..." |

**Kabul:** Bot 3 alt-intent'i atlamadan + sıra koruyarak yanıtlar. Tek bölüm karışıklığı yok.

---

### Senaryo 13 — Empati / Şikayet (duygu testi)

**Cümle:** "Geçen sefer kötü bir deneyim yaşadım, sizinle çalışmak istemiyorum aslında" (üzgün/sinirli tonla)

| Adım | Beklenen Davranış |
|------|-------------------|
| C1 | Whisper transcript "kötü deneyim", "istemiyorum" negatif kelimeler |
| T18 | LLM duygu algılar (Realtime audio-in tonlama bilgisi de var) |
| T19-T22 | Bot tonlama yumuşak + empati cümlesi açılış: "Üzgünüm, anlıyorum, geçmişte yaşadıklarınız için..." sonra çözüm önerisi |
| **Önemli** | Bot **agresif/savunmacı tepki vermez** ("Ama biz iyi şirketiz" GİBİ DEĞİL) |

**Kabul:** Empati ön plana (ilk cümle), sonra çözüm önerisi. Bot Türkçe ses tonu **doğal/yumuşak** (Realtime audio-out aksent kalitesi kritik).

---

### Senaryo 14 — Sayısal Entity Extraction

**Cümle:** "3 Haziran Cuma günü saat 14:30'da, 3000 graft için randevu alabilir miyim?"

| Adım | Beklenen Davranış |
|------|-------------------|
| C1 | Whisper transcript: tarih (3 Haziran Cuma), saat (14:30), miktar (3000 graft) doğru transkript |
| T18 | LLM entity extraction: `{date: "2026-06-03", time: "14:30", graft_count: 3000}` |
| T19-T22 | Bot entity'leri **geri yansıtır**: "3 Haziran Cuma 14:30'da 3000 graft için randevu kaydediyorum, doğru mu?" |

**Kabul:** Bot cevabında 3 sayısal entity doğru geçer (Türkçe yazılış: "üç bin" yerine "3000" tutarlı). Tarih AM/PM karışıklığı yok (14:30 doğru).

---

### Senaryo 15 — Yarım Kalmış Cümle + Intent Reversal

**Davranış:** "Saç ekimi yaptırmak istiyorum ama..." *[konuşmayı kes, 3sn bekle]* *[sonra]* "...aslında daha sonra ararım"

| Adım | Beklenen Davranış |
|------|-------------------|
| T1-T13 | İlk yarı "Saç ekimi yaptırmak istiyorum ama..." |
| T14 | "ama..." sonrası 3sn silence |
| T13 | OpenAI semantic_vad: "ama" conjunction sonrası → response.create TETİKLENMEZ |
| T6-T9 | 3sn sonra "...aslında daha sonra ararım" konuşma yeniden başlar |
| T13-T16 | Kümülatif transcript "Saç ekimi yaptırmak istiyorum ama aslında daha sonra ararım" |
| T18 | LLM intent reversal algılar: ilk yarıda olumlu, "ama" + ikinci yarı negatif |
| T19-T22 | Bot kibar kapanış: "Tabii, sizi sonra bekleriz, iyi günler" |

**Kabul:** Bot 3sn duraksamada response üretmez (en zor edge case). İkinci yarı eklendiğinde **negatif intent'i** doğru algılar (saç ekimi info VERMEZ, kapanış cümlesi).

---

## Latency Hedefler (F0 Hedef AC4)

| Metrik | Hedef | Kabul edilebilir | FAIL |
|--------|-------|------------------|------|
| `first_byte_ms` p50 | <800ms | <1000ms | ≥1500ms |
| `first_byte_ms` p95 | <1000ms | <1500ms | ≥2000ms |
| `barge_in_ms` (senaryo 3) | <250ms | <500ms | ≥800ms |
| `intent_correctness` ortalama | ≥4.5/5 | ≥4.0/5 | <3.5/5 |
| `bot_voice_quality` ortalama (TR doğallık) | ≥4.0/5 | ≥3.5/5 | <3.0/5 |

**Sales-ready threshold:** `first_byte_ms` p95 < 1000ms ve `intent_correctness` ≥4.0 — bu eşik tutturulursa müşteri prospect demo'da kullanılır. Aşılırsa F2 öncesi root-cause analiz.

---

## Test Aracı

`/metrics/latency` endpoint'inden ham ölçüm al (token = Q superadmin JWT):

```powershell
Invoke-RestMethod "http://localhost:7115/metrics/latency?token=<SUPERADMIN_JWT>" | ConvertTo-Json -Depth 5
```

Her round sonu HUD'daki değerleri ve `/metrics/latency` snapshot'ını rapora yapıştır.

---

## Sıralı Test Akışı (Önerim)

1. **1-2-5-6** (baseline + kontrollü, 4 senaryo): sistemin temel çalışması doğrulanır
2. **4-8-10** (semantic EOT zorluk, 3 senaryo): duraksama/conjunction/fragmented — Realtime semantic_vad kalitesi netleşir
3. **3** (barge-in, 1 senaryo): concurrent send + cancel race testi (en yüksek riskli teknik path)
4. **11-12** (intent + multi-step, 2 senaryo): GPT-4o reasoning quality testi
5. **13** (empati, 1 senaryo): duygu/ton kalitesi — sales-demo'da en çok izlenecek senaryo
6. **14** (entity extraction, 1 senaryo): sayı/tarih accuracy
7. **15** (yarım cümle + reversal, 1 senaryo): edge case + context retention
8. **7-9** (role-reversal + sessizlik, 2 senaryo): defensive testler

Her round 1-2 dakika alır → **15 senaryo × 3 round ≈ 60-90 dakika** toplam smoke. Q tek oturumda yapabilir veya 3 oturuma bölebilir.

---

## Smoke FAIL Eylemleri

- Eğer `first_byte_ms` p95 > 1500ms ise → **AD-8 fallback "pre-greeting padding"** (bot ilk 300ms doğal duraksama sesi) F2'de aktive et, F0'da sadece raporla
- Eğer `intent_correctness` < 3.5 ise → OpenAI Realtime `instructions` prompt'unu güçlendir (RealtimeSession.cs satır 35-40)
- Eğer `barge_in_ms` > 800ms ise → SemaphoreSlim send lock race condition analizi (VoicePocOrchestrator)
- Eğer senaryo 13 empati toned değilse → `voice` parametresi (alloy → shimmer/coral test et)
- Eğer senaryo 4/8/15 (semantic EOT) FAIL ise → Smart-Turn v2 F2'de erken devreye alınmalı (Pipecat HuggingFace ONNX, 30ms ek latency)

---

**Önceki firma referansı:** Q müşteri prospect'i "önceki firma 2.5sn gecikme" diyor. Bu raporun değeri: F0 sayısal kanıt + 15 senaryo kapsamı → sales-demo'da "biz 800ms, onlar 2500ms — 3x hızlı" iddiası belgelenir.
