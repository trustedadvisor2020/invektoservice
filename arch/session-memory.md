# Session Memory

> Son session'dan kalan context. Her session başında oku.

## Last Update

- **Date:** 2026-02-12
- **Status:** Flow Builder Phase 1 (SPA Scaffold + Canvas) TAMAMLANDI - UI test OK
- **Last Task:** Chatbot Flow Builder visual editor - Phase 1 complete. React Flow + Zustand + TailwindCSS SPA at `src/Invekto.Backend/FlowBuilder/`. 5 node types, drag-drop canvas, property panel, undo/redo, edge deletion, color-coded handles. Build PASS, Q UI test OK.

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
  - Error codes: INV-AT-001 ~ INV-AT-005
- **GR-1.11 AgentAI Service (Port 7105):**
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

- **Flow Builder (Phase 1 - SPA UI):**
  - n8n-style visual drag-drop chatbot flow editor
  - React 18 + TypeScript + Vite + TailwindCSS + React Flow (xyflow) + Zustand
  - Konum: `src/Invekto.Backend/FlowBuilder/` (bagimsiz SPA)
  - Serve: Backend:5000/flow-builder/ (build output -> wwwroot/flow-builder/)
  - Dev: localhost:3002/flow-builder/
  - Contract v2: Node/edge graph model (12 node type destegi)
  - Phase 1'de 5 node type: trigger_start, message_text, message_menu, action_handoff, utility_note
  - FlowCanvas: Drag-drop, self-connection prevention, Delete/Backspace node silme
  - Custom edge: Hover X button ile baglanti silme
  - Handle renkleri: Input=Blue, Output=Green
  - NodePalette: Kategorili sol sidebar (surukle-birak)
  - NodePropertyPanel: Secili node ozellikleri (type-specific editors)
  - FlowSettingsPanel: Genel flow ayarlari (off-hours, unknown input, timeout, etc.)
  - Toolbar: Flow adi/aciklama, undo/redo, save, dirty indicator
  - Zustand store: nodes, edges, selection, undo/redo (max 50 history)
  - Build: tsc 0 error, vite build OK (JS 368KB gzip 118KB)

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
| AgentAI | 7105 | Implemented (GR-1.11) |
| Integrations | 7106 | Reserved (Phase 2+) |
| Outbound | 7107 | Implemented (GR-1.3) |
| Automation | 7108 | Implemented (GR-1.1) |
| Simulator | 4500 | Dev-only tool (Node.js) |
| FlowBuilder | 3002 | Dev-only SPA (Vite, serve via Backend:5000) |

### Deploy
- **Script:** `dev-to-invekto-services.bat`
- **Protokol:** FTPES (explicit TLS)
- **FTP Host:** services.invekto.com
- **Sunucu Yapi:** `E:\Invekto\Backend\current\`, `E:\Invekto\ChatAnalysis\current\`, `E:\Invekto\Automation\current\`, `E:\Invekto\AgentAI\current\`, `E:\Invekto\Outbound\current\`
- **Sunucu Domain:** services.invekto.com
- **Sunucu Root:** `E:\Invekto\` (Backend, ChatAnalysis, scripts, logs)
- **Service Manager:** NSSM (`E:\nssm.exe`)
- **Servisler:** InvektoBackend, InvektoChatAnalysis, InvektoAutomation, InvektoAgentAI, InvektoOutbound, InvektoDeployWatcher (auto-start, auto-restart)
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
- [x] ~~GR-1.11 AgentAI Service~~ (Tamamlandi - Port 7105, Codex 2 iter PASS + Q FORCE PASS)
- [x] ~~Simulator Backend Proxy Architecture~~ (Tamamlandi - Backend proxy, E2E scenarios, health checker, Codex 5 iter PASS)
- [x] ~~Q: agentai.sql calistir~~ (Tamamlandi)
- [x] ~~Q: AgentAI appsettings.Production.json doldur~~ (Tamamlandi)
- [x] ~~Q: AgentAI deploy + Windows Service kurulumu~~ (Tamamlandi - InvektoAgentAI SERVICE_RUNNING)
- [x] ~~GR-1.3 Outbound Service~~ (Tamamlandi - Port 7107, Build PASS, /rev bekliyor)
- [x] ~~Flow Builder Phase 1~~ (Tamamlandi - SPA scaffold, canvas, 5 node, drag-drop, property panel, UI test OK)
- [ ] Flow Builder Phase 2: API + Backend entegrasyon (SPA fallback route, JWT proxy, Automation endpoints)
- [ ] Flow Builder Phase 3: FlowEngine v2 (graph executor, validator, migrator)
- [ ] Flow Builder Phase 4: Genisletilmis node'lar (logic, AI, api_call, delay, set_variable)
- [ ] Flow Builder Phase 5: iframe + polish (postMessage, auto-save, tema, keyboard shortcuts)

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

---

## Project Structure

```
src/
├── Invekto.Shared/           # Shared contracts, DTOs, logging
│   ├── Auth/                 # GR-1.9: JWT validation
│   ├── Constants/
│   ├── Data/                 # GR-1.9: PostgreSQL connection
│   ├── DTOs/
│   │   ├── AgentAI/          # GR-1.11: Suggest/Feedback DTOs
│   │   ├── ChatAnalysis/
│   │   ├── Outbound/          # GR-1.3: Broadcast/Template/Webhook DTOs
│   │   └── Integration/      # GR-1.9: Webhook/Callback DTOs
│   ├── Integration/          # GR-1.9: Callback client
│   └── Logging/
├── Invekto.ChatAnalysis/     # Microservice (Port 7101)
├── Invekto.AgentAI/          # GR-1.11: AI Agent Assist (Port 7105)
│   ├── Data/                # AgentAIRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # ReplyGenerator, TemplateEngine, AgentProfileBuilder
├── Invekto.Automation/       # GR-1.1: Chatbot/Flow Builder (Port 7108)
│   ├── Data/                # AutomationRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # FlowEngine, IntentDetector, FaqMatcher, WorkingHoursChecker, AutomationOrchestrator
├── Invekto.Outbound/         # GR-1.3: Broadcast & Trigger Engine (Port 7107)
│   ├── Data/                # OutboundRepository
│   ├── Middleware/           # Traffic logging + JWT auth
│   └── Services/            # BroadcastOrchestrator, TriggerProcessor, MessageSenderService, TemplateEngine, OptOutManager, RateLimiter
└── Invekto.Backend/          # Backend API (Port 5000)
    ├── Dashboard/            # React/TS Ops Dashboard
    ├── FlowBuilder/          # React Flow SPA (Dev:3002, Serve:/flow-builder/)
    │   └── src/              # nodes/, components/, panels/, store/, types/, lib/
    ├── Middleware/            # Traffic logging + JWT auth
    └── Services/             # ChatAnalysisClient, AutomationClient, AgentAIClient, OutboundClient
```

---

## Context for Next Session

### Flow Builder Phase 2: API + Backend Entegrasyon

Phase 1 (SPA scaffold + canvas + UI) tamamlandi. Siradaki:

**Plan dosyasi:** `C:\Users\taner\.claude\plans\abstract-moseying-hoare.md` (5-phase detayli plan)

**Phase 2 icerik (7-12 numarali adimlar):**

1. **Backend:5000 SPA fallback route** (`/flow-builder/`)
   - `Program.cs` L1279 civari: `/flow-builder/{**slug}` -> `wwwroot/flow-builder/index.html`
   - `npm run build` output'u `wwwroot/flow-builder/` dizinine gider

2. **Backend:5000 JWT prefix**
   - `Program.cs` L136 civari: `/api/v1/flow-builder/` prefix'i JWT korumasi altina al

3. **Backend:5000 proxy endpoint'leri**
   - `GET /api/v1/flow-builder/flows/{tenantId}` -> Automation:7108 GET flows
   - `PUT /api/v1/flow-builder/flows/{tenantId}` -> Automation:7108 PUT flows
   - `POST /api/v1/flow-builder/flows/{tenantId}/validate` -> Automation validate
   - `POST /api/v1/flow-builder/flows/{tenantId}/activate` -> Automation activate
   - `POST /api/v1/flow-builder/flows/{tenantId}/migrate-v1` -> Automation migrate
   - Yeni: `FlowBuilderClient.cs` (Backend -> Automation proxy)

4. **Automation:7108 yeni endpoint'ler**
   - `POST /api/v1/flows/{tenantId}/validate` - Flow graph validation (kaydetmeden)
   - `POST /api/v1/flows/{tenantId}/activate` - is_active toggle
   - `POST /api/v1/flows/{tenantId}/migrate-v1` - v1 -> v2 preview

5. **SPA API client** (`lib/api.ts`)
   - FlowBuilderApiClient class (JWT header, load/save flow)
   - Error handling (INV-AT-006 ~ INV-AT-010)

6. **Auth** (`lib/auth.ts`, `lib/iframe-bridge.ts`)
   - Standalone: Login sayfasi, sessionStorage token
   - iframe: postMessage ile JWT alma, origin validation

### Bekleyen diger isler
- GR-1.3 Outbound /rev tamamla (Codex review)
- Outbound deploy: outbound.sql, appsettings.Production.json, NSSM servis

---

## Notes

- Stage-0 dokümanı: `invekto_stage0_kurulum_adimlari.txt`
- Full system dokümanı: `invekto_microservice_system_plan.txt`
- Error codes: `arch/errors.md` ve `Invekto.Shared/Constants/ErrorCodes.cs`
