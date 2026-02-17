# Session Memory

> Son session'dan kalan context. Her session başında oku.

## Last Update

- **Date:** 2026-02-17
- **Status:** Phase 1 ✅ + GR-2.1 ✅ + WA-1~3,5,6 ✅ + PKT-1 ✅ + PKT-2 ✅ + PKT-3 ✅ + PKT-4 ✅ + PKT-5A ✅ + PKT-5B ✅ + PKT-6A ✅ + PKT-6B ✅. **12 Paket Stratejisi** aktif (v5.2).
- **Last Task:** PKT-6B Niche Business Logic — DONE. 7 GR: Outbound e-ticaret/klinik trigger'lar, iade cevirme v1+v2, lead management v2 (CRUD+scoring+pipeline), agent assist e-ticaret (order card+escalation note), negatif yorum kurtarma. 21 dosya. Codex 5-chunk review, 3 fix round (tenant_id, typed catches, SQL concat, NpgsqlException endpoints), iteration=2 FORCE PASS.
- **Next Task:** PKT-6C Niche Health Expansion (5 GR: Tedavi Sonrasi Takip, Google Yorum+Referans, Medikal Turizm Lead, Proactive Review Rescue, Multilingual Medical Tourism). Phase 3B devam.
- **Strateji:** Overhead %60 azaltma. 12 paket. Her paket: 1 interview + 1 plan + sıralı dev + 1 build + 1 Codex review.
- **v5.1 (2026-02-15):** PKT-6 (19 GR, ~80 item) → PKT-6A/6B/6C olarak bölündü. Codex PASS olasılığı artırmak için.
- **v5.2 (2026-02-17):** PKT-5 → PKT-5A/5B olarak bölündü (5A: Integrations+Outbound+Compliance, 5B: Ads+Dashboard+Randevu).

### ✅ TAMAMLANDI: WhatsApp Analytics WA-1 + WA-2 (2026-02-14)

**Plan:** `arch/plans/20260214-whatsapp-analytics.json` (status: DONE)
**Kaynak:** ebrumoda.com 2.1M satir WhatsApp konusmalari (giyim e-ticaret)

**WA-1 DONE** - Temizlik + Threading:
- 01_cleaner.py: 2,143,682 → 2,131,477 satir
- 02_threader.py: 164,741 konusma (sale rate fix: confirmed 17.4% + offered 42.3%)
- 03_stats.py: metadata.json (10 agent, 5141 urun, peak hours 11-15)

**WA-2 DONE** - NLP Pipeline:
- 04_intent_classifier.py: 12 intent, keyword-first + Claude Haiku hybrid (87MB, 1M satir)
- 05_faq_extractor.py: 128K Q&A cifti, 4939 kume (MiniBatchKMeans, 24MB+4MB)
- 06_sentiment_analyzer.py: 3-level sentiment per-conversation (5.3MB, 164K satir)
- 07_product_analyzer.py: 5141 urun, 1231 fiyat, price/code separation (8MB)
- utils/claude_client.py: Shared typed exceptions (ClaudeAPIError/ClaudeParseError)
- Keyword-only mode (API key yok): ~49% intent unknown, ~60% sentiment skipped

### Execution Queue — 10 Paket Stratejisi (v5.1, 2026-02-15)

> **Karar:** Tekli GR döngüsü → 10 paket. Overhead %60 azalır.
> **Detay:** `arch/active-work.md` — paket detayları ve GR-paket eşleşmesi
> **v5.1:** PKT-6 → PKT-6A/6B/6C (Codex PASS olasılığı artırmak için)

| # | Paket | İçerik | Durum |
|---|-------|--------|-------|
| 1 | **PKT-1 AI Upgrade** | GR-2.2 + GR-2.3 (Agent Assist v2 + Multi-lang) | ✅ PASS (iter 3, FORCE PASS) |
| 2 | **PKT-2 Sağlık Core** | GR-2.4 + GR-2.6 (Randevu + KVKK) | ✅ PASS (iter 1, FORCE PASS) |
| 3 | **PKT-3 Ops Dashboard** | GR-2.5 + WA-4 (Dashboard + BI) | ✅ PASS (iter 1, FORCE PASS) |
| 4 | **PKT-4 WA Analytics** | WA-6 (NLP stages 4-7 + proxy) | ✅ PASS (iter 7) |
| 5A | **PKT-5A Platform Infra** | Phase 3A: Integrations (:7106), Kargo mock, Outbound v2, Opt-in, Compliance | ✅ PASS (iter 2, FORCE PASS) |
| 5B | **PKT-5B Platform UI+Adv** | Phase 3A kalan: Ads Attribution, Dashboard, Randevu Advanced | ✅ PASS (iter 4, FORCE PASS) |
| 6A | **PKT-6A Niche Foundation** | Phase 3B: Intent + Onboarding + Voice AI (7 GR) | ✅ PASS (iter 1) |
| 6B | **PKT-6B Niche Business Logic** | Phase 3B: Outbound + İade + Lead + Yorum (7 GR) | ✅ PASS (iter 2, FORCE PASS) |
| 6C | **PKT-6C Niche Health Expansion** | Phase 3B: Sağlık + Review Rescue + Multilingual (5 GR) | ⬜ Bekliyor |
| 7 | **PKT-7 Visual AI** | Phase 3C (Visual Search + Size/Fit, :7111) | ⬜ Bekliyor |
| 8 | **PKT-8 Face AI** | Phase 3D (Face Analysis, :7110) | ⬜ Bekliyor |

> PKT-1~4 = Phase 2 tamamlama. PKT-5~8 = Phase 3 tamamlama (feedback'e göre revize edilebilir).
> **Bağımlılık:** PKT-5 → PKT-6A (bağımsız) | PKT-5 → PKT-6B (Integrations) | PKT-6B → PKT-6C

### ✅ TAMAMLANDI: Idea Phase Entegrasyonu (2026-02-14)

**5 idea dokümanı roadmap phase'lerine entegre edildi (v4.5):**

| # | Dosya | Fikir | Phase | GR |
|---|-------|-------|-------|-----|
| 1 | `ideas/voice-message-ai.md` | Sesli Mesaj AI | **Phase 3B** | GR-3.23 |
| 2 | `ideas/face-analysis-ai.md` | Yüz Analizi AI | **Phase 3D** (yeni) | GR-3D.1-3D.5 |
| 3 | `ideas/size-fit-ai.md` | Beden/Ölçü AI | **Phase 3C** | GR-3C.8 |
| 4 | `ideas/review-rescue-ai.md` | Olumsuz Yorum Önleme | **Phase 3B** | GR-3.24 |
| 5 | `ideas/multilingual-medical-tourism.md` | Çok Dilli Medikal Turizm | **Phase 3B** | GR-3.25 |

**Yapılan güncellemeler:**
- `ideas/phases/phase-3b.md` → 3 yeni GR eklendi (3.23, 3.24, 3.25) ✅
- `ideas/phases/phase-3c.md` → GR-3C.8 Size/Fit AI eklendi ✅
- `ideas/phases/phase-3d.md` → Yeni phase oluşturuldu (5 GR, port 7110) ✅
- `ideas/phases/README.md` → Genel Durum + Mikro Servis Doğuş Haritası güncellendi ✅
- `ideas/roadmap.md` → Phase Planı + Mikro Servis Haritası güncellendi ✅
- 5 idea dosyasına Roadmap Referansı eklendi ✅

**Sinerji haritası:**
```
Voice AI (tüm sektörler) ← temel altyapı
  ├── Face Analysis (estetik, 3D) + Multilingual (medikal turizm, 3B) → birlikte çalışır
  └── VPS (e-ticaret, 3C) + Size AI (e-ticaret, 3C) → birlikte çalışır
Review Rescue (e-ticaret, 3B) → GR-3.8/3.16 proaktif genişletme
```

---

## Current State

### Active Features
- **Stage-0 Scaffold:** Backend + ChatAnalysis microservice calisir durumda
- **Health Endpoints:** `/health`, `/ready` tum servislerde
- **Ops Endpoint:** Backend `/ops` - servis durumlarini gosterir
- **JSON Lines Logger:** `Invekto.Shared.Logging.JsonLinesLogger`
- **Chat Analysis:** WapCRM'den sohbet cekme + Claude Haiku ile sentiment/kategori analizi
  - Endpoint: POST `/api/v1/analyze` (phoneNumber, instanceId)
- **GR-1.9 Integration Bridge (Phase 1):**
  - JWT auth middleware (shared HMAC-SHA256 key, /api/v1/webhook/ prefix)
  - Webhook receiver: POST `/api/v1/webhook/event` (202 Accepted, async)
  - Tenant verify: GET `/api/v1/tenant/verify` (JWT health check)
  - Async callback client: MainAppCallbackClient (3x retry, exponential backoff)
  - PostgreSQL connection factory (NpgsqlDataSource, pooling)
  - DB schema: `arch/db/tenant-registry.sql`
  - API contracts: `arch/contracts/integration-webhook.json`, `integration-callback.json`
- **GR-1.1 Automation Service (Port 7108):**
  - Menu-based chatbot engine (welcome -> menu -> action)
  - FAQ automation (keyword match, tenant bazli)
  - Claude Haiku intent detection (bagimsiz, 5 intent)
  - Mesai disi oto-cevap (tenant_registry.settings_json)
  - Human handoff (confidence threshold + AI ozet)
  - Chat session state tracking (PostgreSQL, restart-safe)
  - DB schema: `arch/db/automation.sql` (chatbot_flows, faq_entries, chat_sessions, auto_reply_log)
  - Flow contract: `arch/contracts/automation-flow.json`
  - Error codes: INV-AT-001 ~ INV-AT-010, INV-AT-011 ~ INV-AT-017, INV-AT-021 ~ INV-AT-024
  - **Phase 3a (FlowEngine v2):**
    - Node Handler Registry: INodeHandler strategy pattern (12 handlers: trigger_start, message_text, message_menu, logic_condition, logic_switch, action_delay, utility_set_variable, action_handoff, utility_note, ai_intent, ai_faq, action_api_call)
    - FlowGraphV2: Immutable pre-computed graph (O(1) node/edge lookup, O(1) incoming check)
    - FlowEngineV2: Pure graph executor (no side-effects), auto-chain + wait-for-input + terminal
    - ExpressionEvaluator: {{variable}} substitution + condition eval (regex 100ms timeout, max 50 vars)
    - FlowValidator: Orphan/dead-end/required-field/edge-consistency/cycle detection
    - FlowMigrator: v1 menu → v2 graph + auto-layout + warnings
    - AutomationOrchestrator: v1/v2 version dispatch, v2 session state in session_data JSONB
    - Endpoints: POST /api/v1/flows/validate, POST /api/v1/flows/{tenantId}/{flowId}/migrate-v1
- **GR-1.2 AgentAI Service (Port 7105):**
  - Sync API: AI reply suggestion + intent detection
  - Claude Haiku integration (reply generation + JSON parse)
  - Template engine: `{{variable}}` substitution + HTML sanitization
  - Per-agent feedback learning (accept/edit/reject -> son 20 interaction prompt'a enjekte)
  - Backend proxy pattern (Main App -> Backend:5000 -> AgentAI:7105, 15s timeout)
  - Graceful degradation (timeout -> INV-AA-005/504, failure -> INV-AA-002/500)
  - JWT tenant_id header/claim mismatch protection (403)
  - DB log failure -> response warning (non-fatal)
  - DB schema: `arch/db/agentai.sql` (suggest_reply_log)
  - API contract: `arch/contracts/agentai-suggest.json`
  - Error codes: INV-AA-001 ~ INV-AA-006

- **GR-1.3 Outbound Service (Port 7107):**
  - Broadcast/bulk messaging (max 1000 recipients, async queue)
  - Event-based trigger engine (webhook -> template -> send)
  - Template engine: `{{variable}}` substitution with missing var detection
  - Tenant-based sliding window rate limiter (in-memory, configurable msg/min)
  - Opt-out management (STOP/DUR/İPTAL keyword detection)
  - Delivery status tracking (sent/delivered/failed/read counters)
  - Background IHostedService message sender (batch dequeue, FOR UPDATE SKIP LOCKED)
  - Backend proxy pattern (Main App -> Backend:5000 -> Outbound:7107, localhost-only)
  - DB schema: `arch/db/outbound.sql` (outbound_templates, outbound_broadcasts, outbound_messages, outbound_optouts)
  - API contract: `arch/contracts/outbound-broadcast.json`
  - Error codes: INV-OB-001 ~ INV-OB-010

- **GR-2.1 Knowledge Service Phase A+B (Port 7104):**
  - RAG altyapisi (pgvector semantic search + PostgreSQL FTS keyword fallback)
  - OpenAI text-embedding-3-large (3072 dim) embeddings
  - WA-3 NLP data import (FAQ clusters, intent patterns, product catalog, sentiment)
  - FAQ CRUD (create, read, update, delete, list with category/lang filter)
  - Retrieval API (combined FAQ+chunk search, source references, keyword fallback)
  - Embedding generation endpoint (batch processing)
  - **Phase B:** PDF upload + PdfPig chunking (512-token/50-overlap, page boundary tracking)
  - **Phase B:** DocumentProcessingService (BackgroundService, ConcurrentQueue, restart recovery)
  - **Phase B:** Backend proxy (9 endpoints, JWT bridge: BasicAuth->JwtGenerator->Bearer)
  - **Phase B:** Dashboard Knowledge UI (DocumentUpload, DocumentList, FaqManager, FaqEditModal)
  - 8 DB tablosu: documents, chunks, faqs, tags, document_tags, intent_patterns, product_catalog, conversation_sentiments
  - DB schema: `arch/db/knowledge.sql`
  - API contracts: `arch/contracts/knowledge-import.json`, `knowledge-search.json` (v2.0), `knowledge-faq.json`
  - Error codes: INV-KN-001 ~ INV-KN-015
  - Shared: JwtGenerator (service-to-service token), TrafficLogging + JwtAuth middleware
  - Backend entegrasyonu: KnowledgeClient (full proxy), DependencyMap, TestPanel, health check
  - Deploy: firewall, restart-services, install-services, deploy-watcher, appsettings.Production
  - Test suite: `arch/deploy/test-knowledge.bat` (JWT auto-gen, 6 phase, CRUD + search + error senaryolari)
- **GR-2.4 + GR-2.6 Appointments + KVKK (Port 7102, PKT-2):**
  - Invekto.Appointments: Haftalik slot CRUD, randevu booking/cancel/list, available-slots
  - IHostedService ReminderSchedulerService: T-48h + T-2h reminders via Outbound triggers
  - Booking logic: slot validation, past date check, day_of_week match, max_bookings, confirmation via Outbound
  - KVKK Minimum 5 servise: Automation disclaimer (SendMessage only), AgentAI Warning field, Outbound disclaimer (broadcast + trigger), Knowledge medical doc tag, Backend photo block (health tenant + image/*)
  - KvkkHelper (Shared): IsHealthTenant(), AppendDisclaimerIfHealth()
  - Opt-in: Health tenant ilk mesajda riza mesaji (chat_sessions.session_data JSONB)
  - DB schema: `arch/db/appointments.sql` (appointment_slots, appointments)
  - Error codes: INV-AP-001~010, INV-KN-016
  - Backend proxy: AppointmentsClient + 9 proxy endpoints
  - Deploy: install-services, restart-services, firewall (7102 localhost-only), deploy-watcher

- **WA-5/6 WhatsApp Analytics Phase A (Port 7109):**
  - Full C# port of Python pipeline stages 1-3 (cleaner, threader, stats)
  - Streaming CSV parser (100K chunks, BOM detect, quoted fields)
  - Turkish text normalization (TransliterateTurkish for ASCII-safe regex)
  - SHA256 dedup (5s window), 25 outcome regex patterns (priority order)
  - IAsyncEnumerable conversation grouping (streaming, no RAM blowup)
  - Background processing (IHostedService + ConcurrentQueue, one-at-a-time)
  - Restart recovery (GUID filename + 30min stale timeout + FOR UPDATE SKIP LOCKED)
  - REST API: POST /upload, GET/DELETE /analyses, GET /metadata
  - DB schema: `arch/db/whatsapp-analytics.sql` (10 tables)
  - Error codes: INV-WA-001 ~ INV-WA-015
  - Plan: `arch/plans/20260214-wa-analytics-phaseA.json`

- **Flow Builder (Phase 1+2 - SPA UI + API + Auth):**
  - n8n-style visual drag-drop chatbot flow editor
  - React 18 + TypeScript + Vite + TailwindCSS + React Flow (xyflow) + Zustand
  - Konum: `src/Invekto.Backend/FlowBuilder/` (bagimsiz SPA)
  - Serve: Backend:5000/flow-builder/ (build output -> wwwroot/flow-builder/)
  - Dev: localhost:3002/flow-builder/ (Vite proxy -> Backend:5000)
  - Contract v2: Node/edge graph model (12 node type destegi)
  - Phase 1: 5 node type, drag-drop canvas, property panel, undo/redo, edge deletion
  - Phase 2 (Multi-flow + API + Auth):
    - DB: chatbot_flows flow_id SERIAL PK, multi-flow per tenant, partial unique is_active
    - Automation: 7 CRUD endpoints (list, get, create, update, delete, activate, deactivate)
    - Backend: FlowBuilderClient proxy, JWT login (API key from tenant_registry.settings_json)
    - SPA: react-router-dom, LoginPage (tenant_id + api_key), FlowListPage (full CRUD), FlowEditorPage (API load/save)
    - Auth: sessionStorage JWT, 8h expiry, AuthContext + useAuth hook
    - Error codes: INV-AT-006 ~ INV-AT-010 (flow validation, not found, activation conflict, invalid version, invalid API key)
  - Build: .NET 0 errors, tsc 0 errors, vite build OK (JS 423KB gzip 136KB)

### Tech Stack
| Component | Technology |
|-----------|------------|
| Backend | .NET 8 Minimal API |
| Microservice | .NET 8 Minimal API + Windows Service |
| Shared | .NET 8 Class Library |
| Logging | JSON Lines (custom) |

### Ports
| Service | Port | Status |
|---------|------|--------|
| Backend | 5000 | Active |
| ChatAnalysis | 7101 | Active |
| AgentAI | 7105 | Implemented (GR-1.2) |
| Integrations | 7106 | Implemented (GR-3.4 PKT-5A) |
| Outbound | 7107 | Implemented (GR-1.3) |
| Knowledge | 7104 | Implemented (GR-2.1 Phase A+B) |
| Appointments | 7102 | Implemented (GR-2.4 PKT-2) |
| Automation | 7108 | Implemented (GR-1.1) |
| WhatsAppAnalytics | 7109 | Implemented (WA-5/6 Phase A) |
| VisualSearch | 7111 | Planned (Phase 3C, PKT-7) — ~~7109~~ çakışma fix |
| FaceAnalysis | 7110 | Planned (Phase 3D, PKT-8) |
| Simulator | 4500 | Dev-only tool (Node.js) |
| FlowBuilder | 3002 | Dev-only SPA (Vite, serve via Backend:5000) |

### Deploy
- **Script:** `dev-to-invekto-services.bat`
- **Protokol:** FTPES (explicit TLS)
- **FTP Host:** services.invekto.com
- **Sunucu Yapi:** `E:\Invekto\Backend\current\`, `E:\Invekto\ChatAnalysis\current\`, `E:\Invekto\Automation\current\`, `E:\Invekto\AgentAI\current\`, `E:\Invekto\Outbound\current\`, `E:\Invekto\Knowledge\current\`, `E:\Invekto\Appointments\current\`, `E:\Invekto\WhatsAppAnalytics\current\`
- **Sunucu Domain:** services.invekto.com
- **Sunucu Root:** `E:\Invekto\` (Backend, ChatAnalysis, scripts, logs)
- **Service Manager:** NSSM (`E:\nssm.exe`)
- **Servisler:** InvektoBackend, InvektoChatAnalysis, InvektoAutomation, InvektoAgentAI, InvektoOutbound, InvektoKnowledge, InvektoAppointments, InvektoWhatsAppAnalytics, InvektoDeployWatcher (auto-start, auto-restart)
- **Deploy Watcher:** `E:\Invekto\scripts\deploy-watcher.ps1` (flag-based stop/start)
- **.NET Runtime:** ASP.NET Core 8.0.23 (`C:\Program Files\dotnet`)
- **PostgreSQL:** localhost:5432 / invekto DB (pgAdmin ile yonetim)

### Pending Work
- [x] ~~Chat Analysis gerçek iş mantığı~~ (Tamamlandı - WapCRM + Claude)
- [x] ~~Ops sayfası genişletme~~ (Tamamlandı - /ops/errors, /ops/slow, /ops/search)
- [x] ~~GR-1.9 Integration Bridge~~ (Tamamlandı - JWT, webhook, callback, PostgreSQL)
- [x] ~~Q: PostgreSQL kur~~ (Tamamlandi - invekto DB, pgAdmin)
- [x] ~~Staging deploy testi~~ (Tamamlandi - FTPES + health OK)
- [x] ~~appsettings.Production.json~~ (Tamamlandi - Backend + ChatAnalysis)
- [x] ~~Windows Service kurulumu~~ (Tamamlandi - NSSM, auto-start, auto-restart)
- [x] ~~Q: JWT claims dogrula (Main App token yapisi)~~ (Tamamlandi)
- [x] ~~Q: tenant-registry.sql calistir~~ (Tamamlandi)
- [ ] Callback URL per-request: MainAppCallbackClient zaten destekliyor
- [x] ~~GR-1.1 Chatbot/Flow Builder~~ (Tamamlandi - Invekto.Automation servisi)
- [x] ~~Deploy scripts~~ (Tamamlandi - install-services, restart-services, firewall, deploy-watcher)
- [x] ~~Automation Dashboard entegrasyonu~~ (Tamamlandi - HealthCard, DependencyMap, TestPanel, AutomationClient)
- [x] ~~Simulator Tool~~ (Tamamlandi - tools/simulator/, Port 4500, Codex 3 iter PASS)
- [x] ~~GR-1.2 AgentAI Service~~ (Tamamlandi - Port 7105, Codex 2 iter PASS + Q FORCE PASS)
- [x] ~~Simulator Backend Proxy Architecture~~ (Tamamlandi - Backend proxy, E2E scenarios, health checker, Codex 5 iter PASS)
- [x] ~~Q: agentai.sql calistir~~ (Tamamlandi)
- [x] ~~Q: AgentAI appsettings.Production.json doldur~~ (Tamamlandi)
- [x] ~~Q: AgentAI deploy + Windows Service kurulumu~~ (Tamamlandi - InvektoAgentAI SERVICE_RUNNING)
- [x] ~~GR-1.3 Outbound Service~~ (Tamamlandi - Port 7107, Codex 3 iter PASS, deployed NSSM)
- [x] ~~Q: Outbound deploy~~ (Tamamlandi - outbound.sql, appsettings.Production.json, NSSM servis)
- [x] ~~Flow Builder Phase 1~~ (Tamamlandi - SPA scaffold, canvas, 5 node, drag-drop, property panel, UI test OK)
- [x] ~~Flow Builder Phase 2~~ (Tamamlandi - Multi-flow DB, CRUD, Backend proxy, JWT login, SPA auth, Codex 3 iter FORCE_PASS, committed)
- [x] ~~Q: automation.sql migration~~ (Tamamlandi - chatbot_flows multi-flow PK degisikligi)
- [x] ~~Q: tenant_registry flow_builder_api_key~~ (Tamamlandi)
- [x] ~~Flow Builder Phase 2.5~~ (Tamamlandi - AHA #6 Kopya, #2 Kirmizi Kenar, #1 Canli Onizleme. Codex 2 iter PASS)
- [x] ~~Flow Builder Phase 3a~~ (Tamamlandi - FlowEngine v2, Validator, Migrator, 5 NodeHandler, ExpressionEvaluator. Codex 3 iter Q FORCE PASS)
- [x] ~~Flow Builder Phase 3b~~ (Tamamlandi - SimulationEngine, MockFaq/Intent, SPA Chat Panel, AHA #4 Tek Tikla Test. Codex 3 iter PASS)
- [x] ~~Flow Builder Phase 3c~~ (Tamamlandi - Validation UI, Variable Inspector, AHA #3 Ghost Path, AHA #5 Saglik Skoru. 20 dosya +746 -195. Codex 3 iter PASS)
- [x] ~~Flow Builder Phase 4a~~ (Tamamlandi - 4 pure logic node: logic_condition, logic_switch, action_delay, utility_set_variable. Codex 3 iter Q FORCE PASS)
- [x] ~~Flow Builder Phase 4b~~ (Tamamlandi - ai_intent, ai_faq, action_api_call. 27 dosya +1516 -104. Codex 4 iter PASS)
- [x] ~~Flow Builder Phase 5~~ (Tamamlandi - deploy script SPA build step, Codex 3 iter Q FORCE PASS)
- [x] ~~WA-3 + GR-2.1 Phase A~~ (Tamamlandi - Knowledge Service core, 22 dosya +2615, Codex 5 iter PASS)
- [x] ~~GR-2.1 Phase B~~ (Tamamlandi - PDF upload, chunking, combined search, Dashboard UI. 27 dosya +13413. Codex 3 iter PASS)
- [ ] Q: knowledge.sql calistir (PostgreSQL)
- [ ] Q: pgvector extension kur (CREATE EXTENSION vector)
- [ ] Q: Knowledge appsettings.Production.json secret'lari doldur (JWT, PG password, OpenAI key)
- [ ] Q: Knowledge deploy + NSSM servis kurulumu
- [x] ~~WA-5/6 Phase A~~ (Tamamlandi - Invekto.WhatsAppAnalytics Port 7109, 20 dosya +5417, Codex 4 iter PASS)
- [ ] Q: whatsapp-analytics.sql calistir (PostgreSQL)
- [ ] Q: WhatsAppAnalytics appsettings.Production.json secret'lari doldur
- [ ] Q: WhatsAppAnalytics deploy + NSSM servis kurulumu

> **Phase 3 Plan:** `arch/plans/20260213-flow-builder-phase3.json` | **Roadmap:** `arch/docs/flow-builder-roadmap.md`
> **AHA Moments (2026-02-13):** 7 iyilestirme roadmap'e entegre edildi (Phase 2.5, 3b, 3c, 5)

### Known Issues
- (Henüz yok)

---

## Recent Decisions

| Date | Decision | Reason |
|------|----------|--------|
| 2026-02-01 | Mikro servis mimarisi | Bagimsiz deploy, olceklenebilirlik |
| 2026-02-02 | .NET 8 stack | Windows Service native, backend ile ayni ekosistem |
| 2026-02-02 | Stage-0 once | Full system yerine hizli MVP |
| 2026-02-08 | .NET 8 devam (Node.js degil) | Solo founder, mevcut pattern, minimum surtuhnme |
| 2026-02-08 | Webhook push + async callback | Main App -> InvektoServis: webhook, InvektoServis -> Main App: async POST callback |
| 2026-02-08 | Shared JWT key (HMAC-SHA256) | Basit, her iki taraf ayni key ile validate |
| 2026-02-08 | PostgreSQL yeni servisler icin | Ana app SQL Server, yeni servisler PostgreSQL, tenant_id (int) eslestirme |
| 2026-02-08 | Basit retry (3x + backoff) | Phase 1 icin yeterli, queue yok |
| 2026-02-08 | FTPES deploy (E:\Invekto\) | Sunucu TLS zorunlu, E: diski Invekto icin ayrildi |
| 2026-02-09 | Automation kendi Claude cagrisi | ChatAnalysis bagimsiz, mikroservis izolasyonu |
| 2026-02-09 | Ayri faq_entries tablosu | Flow config'den izole, temiz CRUD |
| 2026-02-09 | PostgreSQL chat_sessions | In-memory yerine DB (restart-safe) |
| 2026-02-09 | Mesai saati tenant_registry.settings_json | Tum servisler erisebilir |
| 2026-02-11 | AgentAI ayri mikroservis | Automation'dan bagimsiz, kendi Claude cagrisi |
| 2026-02-11 | Sync API (Backend proxy) | Main App -> Backend -> AgentAI, 15s timeout |
| 2026-02-11 | Per-agent feedback learning | Son 20 interaction Claude prompt'a enjekte, kisisel oneri |
| 2026-02-11 | Async feedback (fire-and-forget) | Agent accept/edit/reject sonrasi POST, response beklenmez |
| 2026-02-11 | Backend proxy for simulator | Tum simulator trafigi Backend:5000 uzerinden, internal servisler localhost-only |
| 2026-02-12 | Outbound ayri mikroservis | Broadcast + trigger engine, Port 7107, Backend proxy pattern |
| 2026-02-12 | In-memory rate limiter | Tenant bazli sliding window, queue (reject degil), configurable msg/min |
| 2026-02-12 | FOR UPDATE SKIP LOCKED | Message dequeue icin safe concurrency, batch 10 |
| 2026-02-12 | Stop keyword detection | STOP, DUR, IPTAL, CIKIS - exact match on trimmed uppercase |
| 2026-02-12 | Flow Builder bagimsiz SPA | InvektoServices'te, iframe ile multi-app embed, postMessage auth |
| 2026-02-12 | React Flow + Zustand | n8n-style visual editor, xyflow ekosistemi, temporal undo/redo |
| 2026-02-12 | Contract v2 node/edge graph | v1 backward-compatible, version field ile ayrim, 12 node type |
| 2026-02-12 | Handle renk ayirimi | Input=Blue, Output=Green (gorsel netlik) |
| 2026-02-12 | Fan-out execution | Bir output birden fazla input'a baglanabilir, hepsi sirali calisir |
| 2026-02-12 | Multi-flow per tenant | chatbot_flows flow_id SERIAL PK, tenant basina N flow (lisansa bagli), max 1 aktif (partial unique) |
| 2026-02-12 | API key login | tenant_registry.settings_json flow_builder_api_key -> JWT (8h expiry), Main App proxy degil |
| 2026-02-12 | Backend JWT proxy | Backend:5000 /api/v1/flow-builder/* -> Automation:7108 /api/v1/flows/*, localhost-only |
| 2026-02-12 | react-router-dom SPA routing | /flow-builder/login, /, /editor/:flowId - BrowserRouter basename="/flow-builder" |
| 2026-02-13 | AHA #5 list endpoint: Secenek A | List endpoint'e flow_config eklenecek (Phase 3c'de). SPA-side health score hesaplama icin graph yapisi gerekli |
| 2026-02-13 | v2 contract JSON schema | `arch/contracts/automation-flow-v2.json` olusturuldu. TypeScript tek kaynak degil, JSON schema Phase 3a backend icin referans |
| 2026-02-13 | v1/v2 session kolon stratejisi | v2 session state session_data JSONB'de, current_node kolonu v1 backward compat icin kalir. ALTER TABLE gerekmez |

---

## Project Structure

```
src/
├── Invekto.Shared/           # Shared contracts, DTOs, logging
│   ├── Auth/                 # GR-1.9: JWT validation
│   ├── Constants/
│   ├── Data/                 # GR-1.9: PostgreSQL connection
│   ├── DTOs/
│   │   ├── AgentAI/          # GR-1.2: Suggest/Feedback DTOs
│   │   ├── Appointments/     # GR-2.4: Slot/Appointment/Reminder DTOs
│   │   ├── ChatAnalysis/
│   │   ├── Outbound/          # GR-1.3: Broadcast/Template/Webhook DTOs
│   │   └── Integration/      # GR-1.9: Webhook/Callback DTOs
│   ├── Integration/          # GR-1.9: Callback client
│   └── Logging/
├── Invekto.ChatAnalysis/     # Microservice (Port 7101)
├── Invekto.AgentAI/          # GR-1.2: AI Agent Assist (Port 7105)
│   ├── Data/                # AgentAIRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # ReplyGenerator, TemplateEngine, AgentProfileBuilder
├── Invekto.Knowledge/        # GR-2.1: Knowledge Service RAG (Port 7104)
│   ├── Data/                # KnowledgeConnectionFactory, KnowledgeRepository
│   └── Services/            # ImportService, EmbeddingService, RetrievalService, PdfChunkingService, DocumentProcessingService
├── Invekto.Appointments/     # GR-2.4: Appointment Engine (Port 7102)
│   ├── Data/                # AppointmentsRepository
│   └── Services/            # ReminderSchedulerService (IHostedService)
├── Invekto.Automation/       # GR-1.1: Chatbot/Flow Builder (Port 7108)
│   ├── Data/                # AutomationRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # FlowEngine, IntentDetector, FaqMatcher, WorkingHoursChecker, AutomationOrchestrator
├── Invekto.Outbound/         # GR-1.3: Broadcast & Trigger Engine (Port 7107)
│   ├── Data/                # OutboundRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # BroadcastOrchestrator, TriggerProcessor, MessageSenderService, TemplateEngine, OptOutManager, RateLimiter
├── Invekto.WhatsAppAnalytics/ # WA-5/6: WhatsApp Analytics Pipeline (Port 7109)
│   ├── Data/                # AnalyticsConnectionFactory, AnalyticsRepository
│   ├── Models/              # AnalysisJob, CleanedMessage, Conversation
│   └── Services/            # CsvStreamReader, TextNormalizer, PipelineOrchestrator, AnalysisProcessingService
│       └── Pipeline/        # CleanerService (Stage 1), ThreaderService (Stage 2), StatsService (Stage 3)
└── Invekto.Backend/          # Backend API (Port 5000)
    ├── Dashboard/            # React/TS Ops Dashboard
    ├── FlowBuilder/          # React Flow SPA (Dev:3002, Serve:/flow-builder/)
    │   └── src/              # nodes/, components/, panels/, store/, types/, lib/, pages/
    ├── Middleware/            # Traffic logging + JWT auth
    └── Services/             # ChatAnalysisClient, AutomationClient, AgentAIClient, OutboundClient, FlowBuilderClient, KnowledgeClient, AppointmentsClient
```

---

## Context for Next Session

### PKT-6B Niche Business Logic TAMAMLANDI (2026-02-17)

**Plan:** `arch/plans/20260217-pkt6b1-niche-business.json` (status: DONE)
**Scope:** 7 GR — Outbound e-ticaret/klinik trigger'lar, iade cevirme v1+v2, lead management v2, agent assist e-ticaret, negatif yorum kurtarma.
**Stats:** 21 dosya. Codex 5-chunk review, 3 fix round, iteration=2 FORCE PASS.

**GR-3.7 + GR-3.11 Outbound Trigger'lar:**
- 4 e-ticaret trigger (order_delivered, return_follow_up, b2b_alert, review_prep)
- 2 klinik trigger (checkup_reminder, birthday_message)
- ConsentManager batch consent check entegrasyonu

**GR-3.8 + GR-3.17 Iade Cevirme v1+v2:**
- ReturnDeflectionService: intent algilama, neden siniflandirma, aksiyon routing
- Tenant sabit kuponlar (coupon_configs settings_json), stok kontrol (Integrations)
- Conversion tracking, follow-up scheduler, basari orani metrikleri

**GR-3.13 Lead Management v2:**
- LeadRepository: CRUD + scoring + pipeline status + follow-up tracking
- const SQL with `(@param IS NULL OR col = @param)` pattern (SQL concat yok)
- Hot leads API, funnel endpoint, activity log
- 6 endpoint'e NpgsqlException catch eklendi (Codex iter 2 fix)

**GR-3.3 Agent Assist E-ticaret:**
- OrderCardService: Integrations'tan siparis karti cekme
- EscalationNoteService: eskalasyon notu olusturma + kaydetme
- Typed catches (InvalidOperationException, ArgumentException)

**GR-3.16 Negatif Yorum Kurtarma:**
- Review alert webhook (IntegrationsRepository)
- Auto-message flow (T+0 empati + T+48h follow-up)
- Recovery tracking (recovery_status, recovery_attempt)
- const SQL `(@status IS NULL OR recovery_status = @status)` pattern

**Codex Review (3 Fix Round):**
- Round 1: tenant_id filter (GetPendingFollowUpsAsync) + typed catches (ReturnDeflection, EscalationNote)
- Round 2: SQL string concat → const SQL (GetReviewAlertsAsync, ListLeadsAsync)
- Round 3: NpgsqlException catches for 6 lead endpoints in Backend/Program.cs
- FORCE PASS: Remaining UNKNOWN verdicts from chunking artifacts (all CQ1-CQ8 PASS)

**Q Operational Tasks (PKT-6B):**
- [ ] pkt6b1-niche-business.sql calistir (PostgreSQL)

### PKT-6A Niche Foundation TAMAMLANDI (2026-02-17)

**Plan:** `arch/plans/20260217-pkt6a-niche-foundation.json` (status: DONE)
**Scope:** 7 GR — DB-driven intent mimarisi, Knowledge→Automation bridge, 22 sektor intent seed, B2B/VIP lead detection, auto-tagging, onboarding seed API.
**Stats:** 16 dosya +918/-28. Codex 2-chunk review, iteration=1 PASS.

**GR-3.1 Intent Genisleme + Oto. Etiketleme:**
- KnowledgeIntentClient: Knowledge Service'ten sektor intent pattern'leri cekme
- AiIntentHandler: DB-driven intent match (confidence threshold fallback chain)
- ApplyTag callback via AutomationOrchestrator

**GR-3.2 B2B / VIP Lead Tespiti:**
- VipDetectionService: Siparis hacmi, mesaj frekansi, anahtar kelime analizi
- Sales webhook notification (high-value lead alert)

**GR-3.5 + GR-3.9 + GR-3.10 + GR-3.12 Onboarding + Sektor Intent:**
- OnboardingService: Tenant sektor bazli seed data (intent + FAQ + flow template)
- Knowledge endpoint: Sektor intent pattern CRUD
- 22 intent seed (eticaret/dis/estetik)

**Q Operational Tasks (PKT-6A):**
- [ ] pkt6a-niche.sql calistir (PostgreSQL)
- [ ] pkt6a-niche-seeds.sql calistir (seed data)

### PKT-5B Platform UI+Adv TAMAMLANDI (2026-02-17)

**Plan:** `arch/plans/20260217-pkt5b-platform-ui-adv.json` (status: DONE)
**Commit:** `93d2392` — feat(pkt5b): Ads Attribution + Dashboard Expansion + Appointments Advanced (22 dosya +2863/-55)

**GR-3.14 Ads Attribution:**
- UTM/Meta click webhook capture (conversation_started event)
- AttributionRepository: lead_attributions CRUD, ad_costs CRUD, summary/CPL queries
- AttributionService: null-safe UTM extraction, typed catches
- 7 JWT-auth attribution endpoints + 3 ops analytics endpoints

**GR-3.18 Dashboard Genişletme:**
- CampaignPanel (gerçek campaign_stats data), AttributionPanel, PlaceholderPanel (İade/Yorum)
- AnalyticsPage: 3 bağımsız error-bounded fetch (Promise.all yerine)
- api.ts: getAttributionSummary, getCostPerLead, getCampaignStats

**GR-3.19 Randevu Advanced:**
- Waitlist CRUD + WaitlistService (IHostedService, 5min expiry timer, cancel-flow notification)
- No-show stats (configurable threshold via settings_json)
- Service pricing CRUD (COALESCE update pattern)
- Doctor slot filtering, ICalendarSyncService interface + MockCalendarSyncService

**Quality (Codex 4 iter):**
- SQL string concat → const parameterized queries (5 methods refactored)
- Typed catches throughout (NpgsqlException, JsonException, HttpRequestException, FormatException)
- HTTP disposables managed (using var), fire-and-forget with ContinueWith logging
- DateOnly.Parse → FormatException try-catch + 400 response (8 endpoints)

**Q Not:** Phase 3A (PKT-5A + PKT-5B) tamamlandı. Sunucu taşıma sonrası deploy yapılacak.

### Q Operational Tasks (PKT-5B)

- [ ] appointments-v2.sql migration çalıştır (waitlist, service_pricing tabloları)
- [ ] attribution.sql migration çalıştır (lead_attributions, ad_costs tabloları)
- [ ] Deploy sonrası: Attribution webhook'u Main App'ten conversation_started event'i ile tetiklenecek

### PKT-5A Platform Infra TAMAMLANDI (2026-02-17)

**Plan:** `arch/plans/20260217-pkt5a-platform-infra.json` (status: DONE)
**Commit:** `d1e28bc` — feat(pkt5a): Platform Infra - 5 GR (33 dosya +3445/-14)

**GR-3.4 Integrations Service (:7106):**
- Yeni Invekto.Integrations mikro servisi (port 7106)
- HepsiburadaClient: Order sync, listing fetch, order status update
- OrderSyncService (IHostedService): Periodic sync across all active accounts
- IntegrationsRepository: Account CRUD, order cache, sync state tracking
- Backend proxy: IntegrationsClient + proxy endpoints

**GR-3.6 Kargo Mock:**
- ShipmentTrackingService: Mock kargo takip (HB, Trendyol, N11 provider desteği)
- Shipment status webhook callbacks

**GR-3.15 Outbound v2:**
- Campaign engine: CampaignOrchestrator, CampaignSenderService (IHostedService)
- ListCampaignsAsync: SQL conditions list pattern (injection-safe)
- UpdateCampaignStatsAsync: tenant_id filtered
- BatchInsertAuditTrailAsync: NpgsqlBatch bulk insert (GR-3.29)

**GR-3.26 Opt-in Framework:**
- ConsentManager: Marketing consent check (batch query)
- Broadcast filtering: opt-out + consent double-gate
- GetPhonesWithoutMarketingConsentAsync batch query

**GR-3.29 Compliance Delta:**
- ComplianceHelper: GetRetentionDays, data deletion flow
- ExecuteDataDeletionAsync: typed catches (NpgsqlException)
- UpdateDeletionRequestAsync: tenant_id WHERE clause
- Batch audit trail for compliance (NpgsqlBatch)

**Codex Review:** 3 iterations (split review: Part1 Integrations 137KB + Part2 Outbound/Shared 209KB). iter 2 FORCE PASS (circular false positives on CQ1/CQ5).

**Q Not:** Sunucu taşıma planlanıyor, deploy sonra yapılacak.

### Q Operational Tasks (PKT-5A)

- [ ] integrations.sql çalıştır (PostgreSQL)
- [ ] Integrations appsettings.Production.json oluştur (JWT, PG password)
- [ ] Integrations deploy + NSSM servis kurulumu (InvektoIntegrations, port 7106)
- [ ] outbound.sql migration çalıştır (campaign + consent tables)
- [ ] compliance.sql migration çalıştır (deletion_requests + audit updates)

### PKT-3 Ops Dashboard TAMAMLANDI (2026-02-16)

**Plan:** `arch/plans/20260216-pkt3-ops-dashboard.json` (status: DONE)
**Commit:** `63543d4` — feat(pkt3): Ops Dashboard - GR-2.5 Automation Analytics + WA-4 BI Dashboard

**GR-2.5 Automation Dashboard:**
- MetricsAggregationService (IHostedService, 5min timer, Interlocked overlap prevention)
- daily_metrics + daily_intent_metrics tables (UPSERT idempotent, FK to tenant_registry)
- AnalyticsRepository: 8 query + 3 aggregation methods (tenant_id WHERE clause on ALL)
- 4 automation endpoints: /tenants, /automation/summary, /automation/trends, /automation/intents
- React: MetricCards (color-coded deflection), DeflectionChart (recharts AreaChart), IntentTable

**WA-4 BI Dashboard:**
- Direct SQL query on wa_analyses, wa_conversations (same PostgreSQL instance)
- 4 WA endpoints: /wa/analyses, /wa/summary, /wa/agents, /wa/trends
- React: WaTrendsChart (recharts BarChart), WaAgentTable (conversion rate color coding)
- COUNT(*) FILTER for efficient in-DB outcome breakdowns

**Frontend:**
- AnalyticsPage: tenant dropdown, date range filters, usePolling, Promise.all parallel fetch
- Layout nav: BarChart3 icon, /analytics route in ProtectedRoute

**Stats:** 17 dosya +2052/-1. Codex CQ 8/8 PASS, CoVe manual verified, Q FORCE PASS.

### Q Operational Tasks (Analytics)

- [ ] backend-metrics.sql çalıştır (PostgreSQL) — daily_metrics + daily_intent_metrics tables
- [ ] Verify MetricsAggregationService starts (check Backend logs for "MetricsAggregationService starting")

### PKT-2 Sağlık Core TAMAMLANDI (2026-02-16)

**Plan:** `arch/plans/20260215-pkt2-saglik-core.json` (status: DONE)
**Commit:** `e994e29` — feat(pkt2): Saglik Core - GR-2.4 Randevu Motoru + GR-2.6 KVKK Minimum

**GR-2.4 Randevu Motoru:**
- Yeni Invekto.Appointments mikro servisi (port 7102) - 10 yeni dosya
- Haftalık slot CRUD (day_of_week, start/end time, max_bookings, doctor_id nullable)
- Booking: availability check, past date check, day_of_week match, max_bookings, confirmation via Outbound trigger
- Cancellation: status update, Outbound notification
- Available slots query: date range, active slots, remaining capacity
- ReminderSchedulerService (IHostedService, 5dk timer, Interlocked overlap prevention):
  - T-48h: appointment_date = CURRENT_DATE + 2
  - T-2h: appointment_date = CURRENT_DATE AND start_time <= LOCALTIME + 2h
  - Outbound trigger event: appointment_reminder / appointment_confirmation
- Backend proxy: AppointmentsClient + 9 proxy endpoints
- DB: appointment_slots + appointments (partial indexes for reminders)

**GR-2.6 KVKK Minimum (5 servis):**
- KvkkHelper (Shared): IsHealthTenant(), AppendDisclaimerIfHealth(), HealthDisclaimer, AgentAIWarning
- Automation: SendCallbackAsync disclaimer (SendMessage only, not HandoffToHuman)
- Automation: Opt-in (health tenant ilk mesaj → rıza, session_data JSONB)
- AgentAI: SuggestReplyResponse.Warning field (JsonIgnore WhenWritingNull)
- Outbound: BroadcastOrchestrator + TriggerProcessor disclaimer
- Knowledge: Medical document kvkk_medical_content tag (metadata_json)
- Backend: Photo block (health tenant + image/* MIME → 403 INV-KN-016)

**Stats:** 31 dosya +9006/-15. Codex iter 1, Q FORCE PASS (CQ3 false positive, CQ4 architectural decision). 8/8 CoVe PASS.

### Q Operational Tasks (Appointments)

- [ ] appointments.sql çalıştır (PostgreSQL)
- [ ] Appointments appsettings.Production.json oluştur (JWT, PG password, Outbound URL)
- [ ] Appointments deploy + NSSM servis kurulumu (InvektoAppointments, port 7102)
- [ ] Outbound validEvents'e appointment_confirmation + appointment_reminder ekle (varsa kontrol et)

### WA-5/6 Phase A TAMAMLANDI (2026-02-15)

**Plan:** `arch/plans/20260214-wa-analytics-phaseA.json` (status: DONE)
**Commit:** `18f387f` — feat(wa-analytics): Phase A - Core pipeline stages 1-3, CSV upload, PostgreSQL schema

**Scope:**
- Invekto.WhatsAppAnalytics mikro servisi (port 7109) - 19 dosya +2644 LOC
- Full C# port of Python pipeline stages 1-3 (cleaner, threader, stats)
- Streaming CSV parser (100K chunks), Turkish text normalization (TransliterateTurkish)
- SHA256 dedup, 25 ASCII-only outcome regex patterns
- IAsyncEnumerable conversation grouping (no RAM blowup)
- Background processing (IHostedService + ConcurrentQueue)
- Restart recovery (GUID filename + 30min stale timeout + FOR UPDATE SKIP LOCKED)
- REST API (upload, CRUD, metadata query)
- 10-table PostgreSQL schema, 15 error codes (INV-WA-001~015)
- Codex 4 iter PASS (recovery mechanism refined through 4 iterations)

**Deferred (Phase B+):**
- NLP Stages 4-7 (intent, FAQ, sentiment, product)
- Query layer + Backend proxy + deploy infra
- WA-4 Dashboard UI

### Knowledge Service Phase A+B TAMAMLANDI (2026-02-15)

**Plan:** `arch/plans/20260214-knowledge-service.json` (status: DONE)
**Phase A Commit:** `385d3e0` — feat(knowledge): Phase A Knowledge Service - RAG, pgvector search, WA-3 NLP import
**Phase B Commit:** `89bbe72` — feat(knowledge): Phase B - PDF upload, chunking, combined search, Dashboard UI

**Phase A scope (DONE):**
- Invekto.Knowledge mikro servisi (port 7104) - 22 dosya +2615
- WA-3 NLP data import (FAQ clusters, intent, product, sentiment)
- RAG retrieval (pgvector semantic + FTS keyword fallback)
- FAQ CRUD + embedding generation
- Shared middleware refactor (TrafficLogging + JwtAuth → Invekto.Shared)
- Codex 5 iter PASS

**Phase B scope (DONE):**
- PDF upload + PdfPig chunking (512-token/50-overlap, page boundary tracking)
- DocumentProcessingService (BackgroundService, ConcurrentQueue, restart recovery)
- Combined FAQ+chunk semantic/keyword search with source references
- Backend proxy (9 endpoints, JWT bridge: BasicAuth->JwtGenerator->Bearer, 30s timeout)
- Dashboard Knowledge UI (DocumentUpload, DocumentList polling, FaqManager CRUD, FaqEditModal)
- JwtGenerator flexible overload (FlowBuilder login dedup)
- Search contract v2.0 (sourceType discriminator for mixed results)
- Error codes INV-KN-011~015
- 27 dosya +13413/-449, Codex 3 iter PASS

**Deferred (Phase B scope disi):**
- Tags UI CRUD
- SSE import progress
- Embedding cache (LRU)
- Bulk FAQ import via CSV

### Q Operational Tasks (Knowledge)

- [ ] knowledge.sql calistir (PostgreSQL)
- [ ] pgvector extension kur
- [ ] appsettings.Production.json secret'lari doldur
- [ ] Knowledge deploy + NSSM servis kurulumu
- [ ] test-knowledge.bat ile E2E test

---

## Notes

- Stage-0 dokümanı: `invekto_stage0_kurulum_adimlari.txt`
- Full system dokümanı: `invekto_microservice_system_plan.txt`
- Error codes: `arch/errors.md` ve `Invekto.Shared/Constants/ErrorCodes.cs`
