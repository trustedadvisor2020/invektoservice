# FEAT-ICB — INMA Chat Bridge (Backend Support for Chat UI v3)

> **Paket ID:** FEAT-ICB | **Risk:** MEDIUM-HIGH (cross-service, contract-heavy)
> **Durum:** **BACKLOG** (Dent Adavista pilot bitince başlanacak)
> **Yazar:** Q | **Olusturuldu:** 2026-04-20 | **Kaynak:** `C:\CRMs\InvektoWebsite\invektochat\source\chat-design-expert-v3.html` gap analizi

---

## 1. Intent (Ne & Neden)

INMA (WapCRM) chat operator UI'nı `chat-design-expert-v3.html` mock'una göre baştan kuruyor. Mock'taki **1-38 arasındaki tüm inbox/message/composer özellikleri ve tüm diğer UI işleri INMA tarafında** yapılacak. Invekto (INSE) görevi: **bu UI'nin ihtiyaç duyacağı backend API + servis + event yayınını sağlamak**.

Bu paket, 23 yeni backend modülünün tek yerden takip edilmesi için oluşturuldu. Dent Adavista pilot tamamlandıktan sonra kritik sıraya göre implement edilecek.

**Referans:** Feature inventory + gap analizi session context'inde (2026-04-20). Mock'ta 86 özellik tespit edildi, Invekto mevcut durumu ~%32 (16 HAS + 12 PARTIAL).

---

## 2. Dent Adavista / Mevcut FEAT-* ile Overlap Analizi

Bu paketin maddeleri **bağımsız değil** — mevcut DRAFT/IMPLEMENTED feature'larla kesişimler var. Bazıları blocker, bazıları ortak altyapı.

### 2.1 Güçlü Overlap (blocker veya ortak altyapı)

| INMA Chat Bridge Maddesi | Mevcut Feature | İlişki | Etki |
|---|---|---|---|
| **B7** Flow runtime API (active-flow, takeover, sub-chats) | **FM-1a** Flow Versioning (DONE) + **FM-1b** Flow Monitor Sayfası (PLANNED) + **FM-1c** Monitor AI Chat (PLANNED) | FM-1a backend'i (flow_executions, flow_versions) B7'nin temeli. FM-1b/1c tamamlanmadan B7 yapılırsa **rework riski**. | **FM-1b + FM-1c önce bitmeli**, B7 onların üzerine inşa edilmeli |
| **B12** Customer 360 (custom_fields dahil) | **FEAT-TFM** Tenant Field Mapping (DRAFT) | FEAT-TFM, INMA 10-field semantic overlay'i kuruyor. B12 customer 360 endpoint'i `custom_fields` dönerken TFM semantic isimlerle dönmeli. | **FEAT-TFM blocker** — B12 onun contract'ını tüketir |
| **B8** Quick chat templates (operator /sablon) | **FEAT-WTP** Welcome Template Pack (IMPLEMENTED review pending) + **FEAT-DMP** Dynamic Message Placeholder (DRAFT) | WTP = auto-sent welcome/FAQ rotation. B8 = operator'ın manuel seçtiği hazır cevap. Aynı `template_catalog` tablosu, farklı amaç. DMP ise `{{cf1}}` substitution — B8 template'lerinde **DMP substitution'ı kullanılmalı**. | WTP için yeni `template_type='operator_quickreply'` enum + DMP substitution pipeline'ı reuse |
| **B11** Blocked contacts | **FEAT-J2** Opt-Out INMA Sync (PLANNING, revised 2026-04-20) | J2, outbound'da `status='blocked'` + MessageCategory işliyor. B11 inbound filter — aynı `blocked_contacts` tablosu kullanılabilir. | J2'nin blocked_contacts şemasını genişleterek B11 ekle (ayrı tablo **açma**) |
| **B13** Sticky note (visitor note) | **FEAT-TFM** veya mevcut visitor table | Visitor profiline serbest not — muhtemelen INMA Customer.Note field'ı zaten var | **FEAT-DMP** doğrulaması: INMA `note` placeholder listesinde var. Muhtemelen yeni kolon GEREKMEZ, INMA field'ı okunur/yazılır |

### 2.2 Orta Overlap (ortak contract/infra)

| Madde | İlişki |
|---|---|
| **Grup C** SignalR event yayınları | `ChatHub` mevcut. Yeni `InvektoEventsHub` veya mevcut hub'a event ekleme — **UP0** (unified platform P0) çözümü ile hizalanmalı |
| **Grup D** Infra (OpenAPI, rate limit, idempotency) | UP0'da zaten SSO + tenant sync var. Token model UP0'dan türetilmeli |
| **B14** Reports API (FRT/resolution/CSAT) | **PKT-4 WA Analytics** (DONE) ile benzer aggregate pattern — materialized view + daily job |

### 2.3 Blockerlar (üstünde çalışılamaz)

1. **UP0 (INMA-INSE Unified Platform P0)** — JWT public key INMA-blocked, UP0.2/0.3/0.5 bekliyor. **Tüm paket UP0 tamamlandıktan sonra** cross-service authorize edebilir.
2. **Dent Adavista pilot** — BLOCKED (UP0 + FEAT-*). Dent live olmadan bu paketin prioriti yok; Q'nun açık direktifi: "dentadavista bitince başlayacağız".

### 2.4 Overlap Karar Matrisi

| Durum | Karar |
|---|---|
| FM-1b + FM-1c tamamlanmadan B7'ye girme | **HAYIR** — rework riski yüksek |
| FEAT-TFM'i B12 implement öncesi tamamla | **EVET** — contract dependency |
| B8 template için yeni tablo açma | **HAYIR** — `template_catalog.type` extension ile yet |
| B11 için yeni blocked_contacts tablosu | **HAYIR** — FEAT-J2'nin şemasını genişlet |
| B13 sticky note için yeni kolon | **ŞÜPHELİ** — önce INMA Customer.Note sınırını test et, yetmezse kendi tablosunu aç |

---

## 3. Scope — 23 Backend Modülü

> **Not:** Maddeler mock'taki feature numarasına (#1-86) göre referans tutar. INMA UI tarafı **kapsam dışı** — bu paket sadece Invekto API + servis + event.

### Grup A — Mevcut API'ler (sadece dokümantasyon + contract yayını)

INMA direkt tüketebilir; Invekto'da kod yazılmayacak, sadece `arch/contracts/` altında contract dosyası + Swagger + örnek payload.

| ID | Kabiliyet | Endpoint | Kullanım |
|---|---|---|---|
| A1 | AI cevap önerileri | `POST /api/v1/agent-assist/suggest` | Feature 38, 45 |
| A2 | Çeviri | `TranslationService` | Feature 28 |
| A3 | Sentiment | ChatAnalysis webhook | Feature 44 |
| A4 | Intent (Mercek) | `AiIntentHandler` | Feature 46 |
| A5 | Handoff/transfer | `ActionHandoffHandler` | Feature 66 |
| A6 | Flow list | `GET /api/v1/flows` | Feature 42 |
| A7 | Presence | SignalR `OperatorStatusChanged` | Feature 6 |
| A8 | Typing | `ChatHub.Typing()` | Feature 7 (bidirectional broadcast gerekli) |
| A9 | İç notlar | Mevcut notes endpoint | Feature 19 |
| A10 | Custom fields | Tenant-based | Feature 53 (FEAT-TFM ile genişleyecek) |
| A11 | Tags | `tags` CRUD | Feature 4, 54 |

### Grup B — Yeni API'ler (implement edilecek)

#### B1 — Conversation Enrichment API
**Karşılığı:** feature 5, 8, 9, 50, 55
**İçerik:**
- `GET /api/v1/conversations?view=mine|unread|pending|closed&channel={id}&tags[]=...`
- Response: last_message preview, unread_count, pinned_at, archived_at, is_verified, active_flow, ai_sentiment, customer_type
- DB: `conversations` tablosuna `pinned_at`, `archived_at`, `is_verified` ALTER
- Trigger: `messages` insert → `conversations` denormalized fields update
- Index: `(tenant_id, status, assignee_id)`, `(tenant_id, archived_at IS NULL)`

**Bağımlılık:** Yok (ilk yapılacak)

#### B2 — Pin/Archive/Unread Toggle
**Karşılığı:** feature 8, 9
**İçerik:** `PATCH /api/v1/conversations/{id}` body: `{ pinned?, archived?, read? }`
**Bağımlılık:** B1

#### B3 — Media Upload + Extended Message Types
**Karşılığı:** feature 12-16
**İçerik:**
- `POST /api/v1/media/upload` (multipart) → `{ media_id, url, mime_type, size, virus_scan }`
- `messages` tablosu: `type`, `media_id`, `reply_to_id`, `edited_at`, `waveform_peaks jsonb`
- Medya storage: **KARAR GEREKLİ** — S3 / MinIO self-hosted / INMA storage
- AV scan pipeline (ClamAV)
- Waveform extraction background job (voice)

**Bağımlılık:** Storage kararı (Q'ya sorulacak)

#### B4 — Link Preview (Open Graph)
**Karşılığı:** feature 15
**İçerik:** `GET /api/v1/link-preview?url=` → OG scrape + cache (24h TTL) + SSRF koruması
**Bağımlılık:** Yok

#### B5 — Message Reactions
**Karşılığı:** feature 17
**İçerik:**
- `POST /api/v1/messages/{id}/reactions` body: `{ emoji }`
- `DELETE /api/v1/messages/{id}/reactions/{emoji}`
- Tablo: `message_reactions (message_id, user_id, emoji)`
- SignalR: `ReactionAdded` / `ReactionRemoved`

**Bağımlılık:** Yok

#### B6 — Edit + Delivery Ticks
**Karşılığı:** feature 21, 23
**İçerik:**
- `PATCH /api/v1/messages/{id}` body: `{ text }` → `edited_at` set, broadcast
- `PATCH /api/v1/messages/{id}/status` body: `{ status: delivered|read }`
- WA/IG/TG ack webhook'larını receive edip status update

**Bağımlılık:** Kanal webhook owner netleşmeli (INMA mı Invekto mu?) — Q'ya sorulacak

#### B7 — Flow Runtime API ⚠️
**Karşılığı:** feature 39, 40, 41, 43
**İçerik:**
- `GET /api/v1/conversations/{id}/active-flow` → step, total, waiting_for
- `POST /api/v1/flows/{exec_id}/takeover` → flow pause, assignee update
- `POST /api/v1/conversations/{id}/run-flow` body: `{ flow_id }`
- `GET /api/v1/flows/{id}/active-conversations` → sub-chats
- DB: `flow_executions.current_step`, `.waiting_for`, `.paused_at`
- SignalR: `FlowPaused` event

**Bağımlılık:** **FM-1b (Flow Monitor Sayfası) + FM-1c (Monitor AI Chat) tamamlanmalı** — overlap riski

#### B8 — Operator Quick Templates
**Karşılığı:** feature 33
**İçerik:**
- `GET/POST/PATCH/DELETE /api/v1/templates`
- Tenant-scoped, channel-specific opsiyonel, shortcut field (`/sablon_adi`)
- **Yeni tablo AÇMA** — `template_catalog.type='operator_quickreply'` enum extension

**Bağımlılık:** FEAT-WTP sonrası template_catalog altyapısı + FEAT-DMP substitution pipeline reuse

#### B9 — Reminders
**Karşılığı:** feature 68
**İçerik:**
- `POST /api/v1/reminders` body: `{ conversation_id, remind_at, note }`
- `GET /api/v1/reminders?from=now`
- Tablo: `reminders`
- **Hangfire** recurring/scheduled job (G7 migration'ı kullan)
- Zamanı gelince SignalR push → INMA toast

**Bağımlılık:** G7 Hangfire (Faz 1 IN_PROGRESS, bitmesi gerekli)

#### B10 — Notifications Feed
**Karşılığı:** feature 57
**İçerik:**
- `GET /api/v1/notifications?unread=true`
- `PATCH /api/v1/notifications/{id}` → mark read
- Event types: `new_message`, `reminder_triggered`, `mention`, `flow_waiting`, `handoff_requested`
- SignalR: `NotificationPushed`

**Bağımlılık:** B9 (reminder events), B7 (flow events), C grubu

#### B11 — Blocked Contacts
**Karşılığı:** feature 69
**İçerik:**
- `POST /api/v1/blocked-contacts` body: `{ contact_id, reason, channel? }`
- `GET /api/v1/blocked-contacts`
- Inbound message pipeline'a filter middleware

**Bağımlılık:** **FEAT-J2 şemasını genişlet** — yeni tablo açma

#### B12 — Customer 360 Aggregation ⚠️
**Karşılığı:** feature 50, 51
**İçerik:**
- `GET /api/v1/visitors/{id}/360` → orders (Ecom), tickets, zoho, ikas, sentiment_history, custom_fields
- Cross-service composition (Backend → Ecom/Ticket/Zoho parallel calls + merge)
- Cache 60sn

**Bağımlılık:** **FEAT-TFM** (custom_fields semantic overlay) — **blocker**

#### B13 — Sticky Note
**Karşılığı:** feature 52
**İçerik:**
- `GET /api/v1/visitors/{id}/note`
- `PATCH /api/v1/visitors/{id}/note` body: `{ text }`
- **İlk olarak** INMA Customer.Note field'ını test et; yetmezse kendi tablosu

**Bağımlılık:** FEAT-DMP ile Customer.Note contract doğrulaması

#### B14 — Reports API
**Karşılığı:** feature 61, 62, 63
**İçerik:**
- `GET /api/v1/reports/kpis?range=7|30|90` → total_conversations, FRT, resolution_time, CSAT, deltas
- `GET /api/v1/reports/conversations?range=30&page=1` → DevExtreme grid data
- FRT + resolution background job (Hangfire)
- CSAT survey tablosu
- Materialized view `v_daily_conversation_stats`

**Bağımlılık:** G7 Hangfire + mevcut PKT-4 WA Analytics pattern

#### B15 — Translation Batch + Language Detect
**Karşılığı:** feature 28 (genişletme)
**İçerik:**
- `POST /api/v1/translate/batch` body: `{ messages: [{id, text, target}] }`
- `GET /api/v1/messages/{id}/language` → `{ detected, confidence }`

**Bağımlılık:** Mevcut TranslationService'in üzerine

#### B16 — User Preferences Store (opsiyonel)
**Karşılığı:** feature 27, 77, 78 (cross-device sync)
**İçerik:**
- `GET/PATCH /api/v1/user/preferences` → theme, font_scale, msg_filter, panel_states
- Tek key-value store

**Bağımlılık:** Yok; INMA localStorage yeterli olabilir — **Q'nun kararı**

### Grup C — SignalR Event Yayınları

INMA realtime UI için Invekto'nun emit etmesi gereken event'ler:

| ID | Event | Trigger | Payload |
|---|---|---|---|
| C1 | `NewMessageAnalyzed` | Mesaj analiz tamamlandı | `{conversation_id, message_id, sentiment, intent, suggestions[]}` |
| C2 | `FlowStepChanged` | Flow step ilerledi | `{conversation_id, flow_id, step, waiting_for}` |
| C3 | `FlowPaused` | Takeover ile flow durdu | `{conversation_id, flow_id, paused_by}` |
| C4 | `ReminderTriggered` | Reminder zamanı | `{reminder_id, conversation_id, note}` |
| C5 | `SentimentChanged` | Konuşma sentiment değişti | `{conversation_id, old, new}` |
| C6 | `HandoffRequested` | Bot devir istedi | `{conversation_id, reason, summary}` |
| C7 | `MessageStatusUpdated` | WA/IG ack geldi | `{message_id, status}` |

**Altyapı:** Yeni `InvektoEventsHub` (operator/admin scope) veya mevcut `ChatHub` genişletme — UP0 ile hizalanmalı.

### Grup D — Infrastructure / Contract

| ID | Konu | Detay |
|---|---|---|
| D1 | OpenAPI / Swagger | Tüm B endpoint'leri için. `arch/contracts/inma-chat-bridge.json` tek dosya |
| D2 | API key / OAuth | Tenant-scoped token (UP0 JWT pattern'den türet) |
| D3 | Rate limiting | Per-tenant per-endpoint (translate: 1000/dk) |
| D4 | SignalR reconnect guidance | INMA dev için doc |
| D5 | Idempotency-Key header | POST endpoint'lerinde (messages, reactions, reminders) |

---

## 4. Öneri Faz Planı (Dent Adavista Sonrası)

> **Ön koşul:** Dent Adavista Stage 3 tamamlanmış, UP0 full DONE, FM-1b+1c tamamlanmış, FEAT-TFM implemented.

### Faz 1 — İnbox Kritik (INMA'nın inbox başlatması için blocker) — 2-3 hafta
- **B1** conversation enrichment
- **B2** pin/archive/unread toggle
- **B3** media upload + extended message types
- **B5** reactions
- **B6** edit + delivery ticks
- **C7** MessageStatusUpdated event

### Faz 2 — Flow & AI Köprüsü — 2-3 hafta
- **B7** flow runtime API (FM-1b/1c tamamlandıktan sonra)
- **B4** link preview
- **C1-C3, C5, C6** event yayınları

### Faz 3 — CRM & Şablon — 1-2 hafta
- **B8** operator quick templates (FEAT-WTP/DMP üzerine)
- **B12** customer 360 (FEAT-TFM üzerine)
- **B13** sticky note

### Faz 4 — Bildirim & Hatırlatma — 1-2 hafta
- **B9** reminders (G7 Hangfire üzerine)
- **B10** notifications feed
- **B11** blocked contacts (FEAT-J2 üzerine)
- **C4** ReminderTriggered event

### Faz 5 — Raporlama & İnce ayar — 2 hafta
- **B14** reports KPI + grid
- **B15** translate batch + language detect
- **B16** user preferences (eğer istenirse)
- **D1-D5** contract + infra

**Toplam:** ~8-12 hafta (Dent sonrası).

---

## 5. Açık Sorular (Q'ya)

1. **Media storage:** S3 / self-hosted MinIO / INMA kendi tarafında? (B3 tasarımını etkiler)
2. **WA/IG/TG webhook receiver:** INMA mı Invekto mu? (B6 delivery ack kaynağı)
3. **Ecommerce (Ikas) + Zoho agregasyon noktası:** Invekto'da mı INMA'da mı? (B12)
4. **Team chat (feature 65-67):** Kapsam dışı mı, yoksa ayrı bir paket olarak mı gelecek? (Bu pakete dahil DEĞİL şu an)
5. **B16 user preferences:** Cross-device sync gerekli mi, INMA localStorage yeterli mi?
6. **Sticky note (B13):** INMA Customer.Note alanı yeterli mi, yoksa Invekto'da ayrı tablo mu?

---

## 6. Scope Boundaries

### In Scope
- 23 backend modül (Grup B + C + D)
- Overlap'lı feature'lara EXTENSION (yeni tablo değil, mevcut şema genişletme)
- SignalR event yayınları
- OpenAPI contract + Swagger docs

### Out of Scope (Explicit)
- **Tüm UI işi** (INMA sorumluluğu)
- Team chat (feature 65-67) — ayrı paket
- Theme/font/mobile (feature 77-83) — tamamen UI
- Confetti, toast, right-click, cmdk, hover preview (feature 70-72, 84-86) — tamamen UI
- Kanal adapter implementation (WA/IG/TG webhook receiver) — karar bekliyor
- INMA tarafındaki storage implementation (B3 storage konumu karar bekliyor)

### Değişmeyen Alanlar (Pre-existing)
- `ChatHub` temel typing/presence akışı (genişletme olabilir, break YOK)
- `TranslationService` API contract (B15 additive)
- `AgentAIClient` suggestion contract
- `AiIntentHandler` intent API

---

## 7. Bağımlılık Grafiği

```
UP0 (INMA-INSE Unified) ──── DONE olmalı
    │
    ├── FEAT-TFM ───────────── B12 (customer 360)
    │
    ├── FEAT-WTP ───┐
    ├── FEAT-DMP ───┼────────── B8 (operator templates)
    │
    ├── FEAT-J2 ─────────────── B11 (blocked contacts)
    │
    ├── FM-1a (DONE) ──┐
    ├── FM-1b ─────────┼─────── B7 (flow runtime)
    ├── FM-1c ─────────┘
    │
    ├── G7 Hangfire ─────────── B9 (reminders), B14 (reports jobs)
    │
    └── Dent Adavista DONE ──── TÜM PAKET START CONDITION
```

---

## 8. Next Actions

Bu dosya dondurulmuş BACKLOG'dur. Dent Adavista Stage 3 complete olduğunda:

1. Q bu dosyayı açar, "Open Questions" bölümüne cevap verir
2. FM-1b + FM-1c + FEAT-TFM + FEAT-DMP + UP0 status re-check edilir (hepsi DONE mı?)
3. Faz 1 için detay plan JSON yazılır (`arch/plans/YYYYMMDD-feat-icb-f1.json`)
4. `tracking/README.md` master tabloya `FEAT-ICB` satırı eklenir ve durumu `PLANNING` yapılır
5. Interview → Plan → Dev akışı normal başlar

---

**Referans Dosyalar:**
- Kaynak mock: `C:\CRMs\InvektoWebsite\invektochat\source\chat-design-expert-v3.html`
- Mock feature inventory: bu session context (2026-04-20 konuşması)
- INMA API doc: `wapcrm-marketing-api.md` (repo root)
- Dent plan: `DentAdavista/plan/README.md`
- FEAT-TFM: `arch/features/tenant-field-mapping.md`
- FEAT-WTP: `arch/features/welcome-template-pack.md`
- FEAT-DMP: `arch/features/dynamic-message-placeholder.md`
- Flow Monitor: `tracking/fm-1a-flow-versioning.md`, `fm-1b-flow-monitor-page.md`, `fm-1c-monitor-ai-chat.md`
- UP0: `arch/platform/inma-inse-unification/`
