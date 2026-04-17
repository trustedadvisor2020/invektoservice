# Mevcut INSE Servislerinde "İnsan Hissi" + FAQ Multi-Language — Research & Plan

> **Tarih:** 2026-04-17
> **Kapsam:** Madde 1 ("Robot Değil İnsan Hissi") + Madde 2 ("FAQ Multi-Language Fallback")
> **Durum:** DRAFT — Q onayı bekliyor, plan JSON henüz yazılmadı.
> **Bağlam:** Adavista pilot omurgası (business-initiated akış) çalışırken mevcut runtime'ı insansı hale getirmek. INMA J1/J2/J-HSM/J-WND cevabı gelmeden başlayabilir çünkü bu paketler tamamen INSE-side.

---

## 1. Yönetici Özeti

İki ayrı ama komşu iyileştirme ailesi:

1. **Madde 1 "İnsan Hissi"** — Tek mesajlık büyük balonlar, anında cevap, sabit delay = robot hissi. Chunk + jitter + (mümkünse) typing indicator ile natural yapılır.
2. **Madde 2 "FAQ Multilang"** — Translation altyapısı çok iyi (18 dil, Gemma+Claude dual, DB-cached). FAQ akışında kullanılmıyor. AiFaqHandler tenant-authored dildeki cevabı ham gönderiyor; lead'in preferred_locale'i yok. 4 küçük noktadan dikiş yeter.

**Ortak AHA:** İki paket de *yeni altyapı gerektirmiyor*; mevcut güçlü parçaları (TemplateRotationService, TranslationService, LanguageDetector) runtime pipeline'a bağlamak yeterli.

**Öneri:** 3 paket — HFM-1 (Natural Send) → HFM-2 (Locale FAQ) → HFM-3 (Tone Matrix, post-pilot). Pilot için HFM-1 + HFM-2 yeter.

---

## 2. Mevcut INSE Kod Denetimi

### 2.1 Madde 1 — "Human Feel" Yetenekleri

| Yetenek | Durum | Dosya | Not |
|---------|:-----:|-------|-----|
| WA typing indicator (`typing_on`) | ❌ | `Outbound/MessageSenderService.cs` | Plain callback; INMA'ya typing_on bypass isteği (J-TYPE) yok |
| Response delay | ✅ primitif | `Automation/Services/NodeHandlers/ActionDelayHandler.cs` | Sabit `seconds` 1-300, jitter yok, length-aware değil |
| Message chunking | ❌ | `Automation/Services/NodeHandlers/MessageTextHandler.cs` | Tek balon, tek callback |
| Welcome A/B rotation | ✅ prod-ready | `Shared/Services/HashBasedTemplateRotationService.cs` + `MessageTextHandler.cs` L99-138 | FNV-1a deterministik; contact+node hash |
| FAQ cevap round-robin | ❌ | — | FEAT-WTP AC-3 DRAFT, faq_rotation_state JSONB planlı |
| Persona/tone tutarlılığı | ❌ | — | 46 template arası drift denetimi yok |
| Emoji policy | ❌ | — | Template serbest |
| Turn-taking / typing awareness | ⚠️ | `MessageTextHandler.cs` | wait_for_input var, user typing bilinmiyor |
| AiIntent clarify cümle doğallığı | ⚠️ | `AiIntentHandler.cs` L66-107, 170-298 | 15+ hardcoded TR cümlesi |
| 24h window guard | ❌ | — | J-WND INMA'dan bekleniyor |
| Rate limiter | ✅ | `Outbound/Services/RateLimiter.cs` | Tenant-bazlı, korunacak |

**Kritik gözlem:** MessageSenderService tek-callback-tek-mesaj mimarisi chunking için uyumlu (her chunk ayrı queued_message row'u olur), **ek yapısal değişim gerekmez**. Chunk planlaması `MessageTextHandler`'da yapılır, kuyruğa N ayrı mesaj girer.

### 2.2 Madde 2 — FAQ Multilang Yetenekleri

| Yetenek | Durum | Dosya | Not |
|---------|:-----:|-------|-----|
| Script-tabanlı dil tespiti (13 dil) | ✅ | `Shared/Services/LanguageDetector.cs` | Unicode aralıkları; TR özel karakter; `%0.02` threshold |
| AI-powered lang detection (fallback) | ✅ | `Backend/Services/TranslationService.DetectLanguageCodeAsync` | Gemma primary → Claude fallback → heuristik |
| Translation engine (18 dil) | ✅ | `Backend/Services/TranslationService.cs` | Gemma 4 27b-it primary + Claude Haiku fallback |
| Translation cache (tenant-scoped, hash) | ✅ | `Backend/Services/TranslationCacheRepository.cs` | SHA-based, batch lookup mevcut |
| Batch translate endpoint | ✅ | `TranslationService.TranslateBatchAsync` | 50 msg/batch; cache hit miss istatistiği |
| FAQ answer post-match translation | ❌ | `Automation/Services/NodeHandlers/AiFaqHandler.cs` L105-128 | `answer` Knowledge.Answer ham; translation layer yok |
| `leads.preferred_locale` kolonu | ❌ | — | FEAT-WTP AC-4 scope'unda, henüz schema yok |
| Locale-aware fallback chain | ❌ | — | lead → tenant.locale_default → en → raw akışı yok |
| "Switching language" UX pattern | ❌ | — | Hiç bildirim yok = sessiz geçiş OK |
| AiIntent prompts i18n | ❌ | `AiIntentHandler.cs` + `IntentDetector.cs` | BuildConversationalPrompt %100 Türkçe; clarify mesajları hardcoded |
| MockIntentDetector synonym (dental) | ⚠️ | `MockIntentDetector.cs` L38-42 | "diş" synonym var; 9 dilde yok (simulation kırılgan) |
| MockFaqMatcher locale | ❌ | `MockFaqMatcher.cs` L11-19 | 6 Türkçe keyword; EN lead için fallback yok |
| RTL (Arabic) WA render | N/A | — | WA natively handle eder |
| Dental vocabulary pre-compute | ❌ | — | 324 string pre-translate yok |

**Kritik gözlem:** Translation servis altyapısı aşırı güçlü **ama runtime flow'da çağrılmıyor**. Dikiş noktası sadece AiFaqHandler + leads.preferred_locale + 8 core prompt'un i18n'i.

---

## 3. Dış Kaynaklı Araştırma (Tier-1)

### 3.1 WA Typing Indicator Kısıtları

- **Hard 25sn timeout** — mesajı hazırlayıp göndermek için buçuk bitirmeden typing_on kaybolur → sessizlik
- **Beta status** (Twilio Oct 2025, Meta Cloud API'de var) — prod reliability risk
- **HIPAA/PCI eligible DEĞİL** — medical compliance zorluğu (dental klinik için önemsiz; WA Business zaten HIPAA değil)
- INMA wrapper'ı bu endpoint'i expose etmezse **INSE'nin elinde iki yol**: (a) INMA'dan J-TYPE isteme, (b) Meta Cloud API'ye direkt fallback

**Kaynaklar:**
- [Meta Cloud API Typing Indicators](https://developers.facebook.com/docs/whatsapp/cloud-api/typing-indicators/)
- [Twilio Typing Indicator Public Beta Guide](https://www.courier.com/blog/how-to-use-whatsapp-typing-indicators-on-twilio-public-beta-guide)
- [Twilio WhatsApp GA Update Oct 2025](https://www.twilio.com/en-us/blog/products/launches/launch-whatsapp-faster-with-twilio--senders-api-ga--plus-typing-)

### 3.2 Response Delay & Chunking Research

- **Uzun mesaj → skim/ignore.** WA best practice: ~134 char / mesaj (≈ 20-30 kelime) (ActiveCampaign, Uptail guides)
- **"Faster is not always better"** — ECIS 2018 Gnewuch et al.; 2025 UserExperience.org replication: instant response = robot hissi, ama aşırı yavaş = unresponsive
- **Age-dependent:** genç kullanıcı instant sever, yaşlı kullanıcı delay sever (PMC 2025 Yang et al.)
- **2025 Utrecht bulgusu:** bazı kullanıcı "AI gibi davran" tercih eder; human-mimicry uncanny valley riski — abartılı typing delay = karşı-etki
- **Voice latency <800ms** önerilir; text için 1-3s "think time" makul

**Sonuç:** Fixed delay (ActionDelayHandler'daki `seconds`) yetmez; formül gerekli:
```
think_delay_ms = clamp(600 + 300*log(msg_length), 600, 1800)
inter_chunk_delay_ms = clamp(1200 + 80*chunk_char_count, 1200, 3500)
total_cap = 8000ms (WA typing_on 25s timeout'a yaklaşma, UX'te çok uzun bekleme)
jitter ±15%
```

**Kaynaklar:**
- [The effects of response time on chatbot interaction (2025)](https://pmc.ncbi.nlm.nih.gov/articles/PMC11846305/)
- [UserExperience.org — Chatbot Response Delays (Jan 2025)](https://userexperience.org/blog/2025/01/03/chatbot-delay/)
- [Opposing Effects of Response Time (BISE 2022)](https://link.springer.com/article/10.1007/s12599-022-00755-x)
- [Faster Is Not Always Better (ECIS 2018)](https://aisel.aisnet.org/ecis2018_rp/113/)

### 3.3 Dental/Medical Translation Quality

- **Claude Turkish medical:** Grok'tan daha doğru, %84 dental accuracy (karşılaştırmalı PLOS One 2025)
- **Gemma multilingual medical:** Google Cloud 2025 guide — MCP-based fine-tuning gerekir; out-of-the-box dental terim kaymaları var
- **Klinik guideline uyum:** hiçbir chatbot %100 güvenilir değil; fiyat/güvenlik/xray gibi sık soruları **template-authored answer** ile vermek, translation'a bırakmamak önerilir
- **Dil başı doğruluk farkı** büyük — EN queries > TR > AR > others

**Pilot kuralı:** Dent FAQ cevapları (fiyat, lokasyon, güvenlik) tenant tarafından **yazılmış static text** kalır; sadece target locale'e translate edilir. LLM hallucination yok.

**Kaynaklar:**
- [LLM dentistry accuracy comparison (PLOS One 2025)](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0317423)
- [Build multilingual chatbots with Gemma (Google Cloud 2025)](https://cloud.google.com/blog/products/ai-machine-learning/build-multilingual-chatbots-with-gemini-gemma-and-mcp)
- [Language differences in AI chatbot reliability (2025)](https://www.researchgate.net/publication/398786976)

---

## 4. Gap Matrix (Özet)

| Madde | Problem | Mevcut | Gap | Sırasıyla Paket |
|-------|---------|--------|-----|-----------------|
| 1.1 | Tek-balon robot hissi | MessageTextHandler tek callback | Chunk + jitter + think_delay | HFM-1 |
| 1.2 | Sabit delay mekanik | ActionDelayHandler fixed seconds | Length-aware formula + ±15% jitter | HFM-1 |
| 1.3 | Typing indicator yok | — | İlk fazda platform-agnostic "pre_think_delay", J-TYPE INMA'ya ileride | HFM-1 (minimal) |
| 1.4 | Welcome rotation zaten güçlü | HashBasedTemplateRotationService | — | (bypass) |
| 1.5 | 46 template tone drift | — | tone_tag column + tone-scoped rotation | HFM-3 (post-pilot) |
| 1.6 | AiIntent clarify TR hardcoded | 15+ literal | Resource-based i18n (8 core prompt) | HFM-2 |
| 2.1 | FAQ cevap ham dönüyor | AiFaqHandler.MatchAndRoute | Post-match translation hook | HFM-2 |
| 2.2 | lead.preferred_locale kolonu yok | — | Migration + detect + persist | HFM-2 |
| 2.3 | Fallback chain yok | — | lead → tenant → "en" → raw | HFM-2 |
| 2.4 | "Switching language" UX | — | Sessiz geçiş (research kanıtlı) | (no-op, by design) |
| 2.5 | Dental vocabulary pre-cache | Translation cache var ama warm değil | Warmup endpoint (ops-level) | HFM-2 opsiyonel |

---

## 5. AHA Moments (öneri — Q bunları onaylasın/kessin)

### AHA-1 — "Chunk + Delay Tek Node, İki Formül"
Delay ve send'i ayrı node yapmayı bırakmak. `message_text` node'una:
- `text_chunks: string[]` (operator 2-3 kısa cümle yazar)
- `pre_think_delay_ms: int?` (otomatik hesaplanabilir)

Engine jittered delay + art arda N callback yapar. Operator "engineering" yapmaz.

### AHA-2 — "Typing Indicator Pilot'ta Bypass"
INMA'ya J-TYPE (5. istek) açmak yerine **chunk-arası jitter** zaten "yazıyor" hissini verir (user iki balon arası 1.5-3sn bekliyor). Typing_on endpoint'i ileride eklenebilir — pilot için NICE-TO-HAVE.

### AHA-3 — "FAQ Cevabı Post-Match Translate, Tenant Text Korunur"
Knowledge'taki tenant-authored answer **değiştirilmez** (hallucination riski). Sadece translate edilir, cache'lenir. 80%+ cache hit oranı beklenir (aynı 12 intent × 3 variant × 9 dil = 324 string, tenant başına).

### AHA-4 — "Sessiz Dil Geçişi > Açık Bildirim"
"I'll respond in English now" awkward pattern'i kullanmıyoruz. Research kanıtlı: sessiz geçiş doğal hissettirir. İlk mesajda detect edilen dil `preferred_locale` olarak persist, ikinci mesajdan itibaren lead manuel değiştirene kadar sticky.

### AHA-5 — "Tone Matrix Sonraya, Pilot'ta 46 Template Manuel Doğrulama"
Voice drift denetimi otomatize etmek pilot-kritik değil. Pilot öncesi Q + Dent operatör 46 template'i gözle okur. HFM-3 post-pilot paketi — operatör disiplini mekanizması oluşturur.

### AHA-6 — "AiIntent Prompts i18n — 8 Core Cümle Yeterli"
Tüm AiIntentHandler'ı i18n etmek aşırı. Sadece 8 cümle pilot için lazım:
1. greeting ("Merhaba! İsminizi öğrenebilir miyim?")
2. name_retry ("İsminizi duyamadım...")
3. intent_ask ("{name}, size nasıl yardımcı olabilirim?")
4. confirm ("{name}, şunu mu demek istiyorsunuz: {summary}?")
5. clarify_low ("{name}, biraz daha açıklar mısınız?")
6. denied ("Anlıyorum {name}. Peki, tam olarak...?")
7. max_attempts_reached ("...")
8. off_hours

9 dil × 8 cümle = 72 string — tek JSON dosyası, runtime overhead sıfır.

---

## 6. Implementation Plan — 3 Paket

### Paket HFM-1: Natural Send Pipeline (MEDIUM, ~2-3g)

**Odak:** Chunk + jitter + think_delay

**Acceptance Criteria**
| # | Kriter | Doğrulama |
|---|--------|-----------|
| AC-1 | `message_text` node'una `text_chunks: string[]` field eklenir | Flow config save/load roundtrip |
| AC-2 | Chunks present ise engine her chunk arasında jittered delay (1.2-3.5s ±15%) çalıştırır | Unit test: formula consistency |
| AC-3 | `pre_think_delay_ms` veya yokluğu durumunda auto-compute (600-1800ms, length-aware) | Unit test: log curve |
| AC-4 | Toplam chunk süresi 8s cap'lenir (WA typing_on timeout emniyeti) | Unit test: long chunk list trimmed |
| AC-5 | Simulation mode'da delay skip + "chunk 1/3 sim" info | Sim engine output |
| AC-6 | Geriye uyumluluk: `text` var chunks yoksa eski davranış korunur | Regression test |
| AC-7 | Rate limiter + retry her chunk için korunur (callback path unchanged) | Integration test MessageSenderService |

**Files (estimate)**
- `src/Invekto.Automation/Services/NodeHandlers/MessageTextHandler.cs` (chunk loop)
- `src/Invekto.Shared/Services/MessageChunkPlanner.cs` (NEW — unit testable)
- `src/Invekto.Automation/Services/AutomationOrchestrator.cs` (dispatch N callbacks)
- `arch/specs/natural-send-pipeline.md` (NEW)
- `arch/errors.md` (INV-AT-062)

**Risk:** MEDIUM — rate limiter semantics (her chunk ayrı tenant quota eat ediyor)
**Dependencies:** Bağımsız — bugün başlayabilir
**Codex risk:** chunking değişikliği Codex CQ3 (scope) test edebilir; spec iyi yazılmalı

---

### Paket HFM-2: Locale-Aware FAQ + Preferred Locale (MEDIUM, ~2g)

**Odak:** FAQ cevapları ve AiIntent clarify prompts lead'in dilinde

**Acceptance Criteria**
| # | Kriter | Doğrulama |
|---|--------|-----------|
| AC-1 | `leads.preferred_locale VARCHAR(5)` kolonu (nullable, indexed) | Migration 018 |
| AC-2 | Lead ilk message'ında `LanguageDetector.Detect` + AI detect combine edilip preferred_locale upsert | Integration test: TR mesaj → "tr", EN mesaj → "en" |
| AC-3 | AiFaqHandler matched answer'ı `answer.language` ≠ `preferred_locale` ise `TranslationService.TranslateAsync` ile çevirir | Integration test: EN lead + TR authored answer → EN response |
| AC-4 | Fallback chain: lead.preferred_locale → tenant.locale_default → "en" → raw (no translate) | Unit test: 4 senaryo |
| AC-5 | AiIntent 8 core prompt resource JSON'lardan locale-based yüklenir (tr/en/ar/ru/de/fr/es/pt/nl/it) | Unit test: all locales rendered |
| AC-6 | Switching language user'a bildirilmez (silent) — sticky preferred_locale | Manuel test |
| AC-7 | Translation cache hit ratio diagnostic endpoint (`GET /ops/translation/stats`) | Smoke test |

**Files (estimate)**
- `arch/db/migrations/018-leads-preferred-locale.sql` (NEW)
- `src/Invekto.Automation/Services/NodeHandlers/AiFaqHandler.cs` (translation hook)
- `src/Invekto.Automation/Services/NodeHandlers/AiIntentHandler.cs` (resource-based prompts)
- `src/Invekto.Automation/Resources/IntentPrompts.{tr,en,ar,ru,de,fr,es,pt,nl,it}.json` (10 NEW)
- `src/Invekto.Automation/Services/IntentPromptLoader.cs` (NEW)
- `src/Invekto.Automation/Services/AutomationOrchestrator.cs` (preferred_locale detect+upsert path)
- `src/Invekto.Backend/Services/TranslationService.cs` (diagnostic endpoint)
- `arch/errors.md` (INV-AT-063)

**Risk:** MEDIUM — translation latency hot-path (50-150ms per miss; target 80% cache hit)
**Dependencies:** FEAT-WTP AC-4 ile overlap (preferred_locale) — ya birlikte ya HFM-2 önce deliver eder
**Codex risk:** translation fallback chain'e tenant isolation check — her tenant kendi cache

---

### Paket HFM-3: Persona Guardrails + Tone Matrix (LOW, ~1g) — POST-PILOT

**Odak:** 46 template arası voice drift + operator disiplin

**Acceptance Criteria** (özet)
- `template_catalog.tone_tag VARCHAR(20)` (`warm` | `professional` | `lowpressure` | `celebratory`)
- Template upload UI zorunlu dropdown
- Tone-scoped rotation (aynı tag içinden pick)
- Dashboard tone distribution raporu
- Orphan check: her welcome/faq/opt-out için min 1 template her tone
- Emoji policy: config-driven whitelist/blacklist per tenant

**Files:** `019-template-catalog-tone-tag.sql`, `Knowledge/Services/TemplateRepository.cs`, `Automation/MessageTextHandler.cs` (tone scope), `Backend/wwwroot/app/src/pages/TemplateLibraryPage.tsx`, `arch/specs/tone-matrix.md`

**Risk:** LOW — additive, opt-in, geriye uyumlu

---

## 7. Pilot Yeterlilik Matrisi

| Paket | Pilot için gerekli? | Neden |
|-------|:-------------------:|-------|
| HFM-1 Natural Send | ✅ **EVET** | Robot hissi pilot'ta en görünür problem |
| HFM-2 Locale FAQ | ✅ **EVET** | Dent pilot EN odaklı + 9 dil fallback gereken bir yapıda |
| HFM-3 Tone Matrix | ❌ HAYIR | Operatör manuel doğrulama pilot için yeterli |

**Minimum:** HFM-1 + HFM-2 = ~4-5g iş. Paralel yürütülebilir.

---

## 8. Known Unknowns / Açık Sorular

1. **Preferred_locale kolonu kimin paketine ait?** FEAT-WTP AC-4 içinde geçiyor ama henüz schema yok. HFM-2 teslim ederse FEAT-WTP'den scope azalır — Q karar.
2. **Chunk arası delay tenant rate limiter quota'sına düşer mi?** Düşmeli (her chunk ayrı mesaj). Ama tenant QPS düşükse 3 chunk'ın 2 saniyede 3 slot alması kötü. Solution: Outbound'a "chunk_group_id" pass et, rate limiter grouping logic'i.
3. **J-TYPE INMA'ya açılsın mı, açılmasın mı?** AHA-2'de "sonraya" dedim. Q bunu ek istek olarak kickoff brief'e eklemeyi tercih ederse ok.
4. **AiIntent 8 prompt resource formatı** — JSON mı, RESX mi, YAML mı? .NET 8 JSON en kolay; loader cache singleton.
5. **TranslationService'te per-tenant per-locale cache warmup** pilot öncesi çalıştırılsın mı? 324 string × tenant × önceden compute → pilot ilk saat latency düşer. Opsiyonel, ops-level admin endpoint.

---

## 9. Q'ya Karar Soruları (plan JSON yazmadan önce)

1. **3 paket ayrışması onaylanıyor mu?** HFM-1 / HFM-2 / HFM-3 sınırları uygun mu yoksa HFM-1+HFM-2 tek paket (LARGE) mi?
2. **HFM-3 post-pilot tamam mı?** Yoksa operatör güveni için pilot-öncesi mi?
3. **AHA-1 (chunk field format)** — `text_chunks: string[]` mi, yoksa mevcut `text` içinde özel separator (`\n---\n`) mi? İkincisi migration-free; Q tercihi?
4. **HFM-2 için INMA J-TYPE** — pilot öncesi istensin mi? Yoksa typing_on olmadan chunk-jitter yeterli mi?
5. **preferred_locale** FEAT-WTP'den alıp HFM-2'ye devredilsin mi?
6. **Dental vocab warmup** (AC7 HFM-2 ek) eklensin mi yoksa on-demand cache yeter mi?

---

**Next action:** Q onay verince her paket için ayrı `arch/plans/YYYYMMDD-{slug}.json` yazılacak. Auto workflow devreye girer, Codex review süreci başlar.
