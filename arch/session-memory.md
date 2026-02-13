# Session Memory

> Son session'dan kalan context. Her session başında oku.

## Last Update

- **Date:** 2026-02-14
- **Status:** Flow Builder Phase 4b DONE. AI/API nodes (ai_intent, ai_faq, action_api_call). 27 dosya +1516 -104. Codex 4 iter PASS.
- **Last Task:** Phase 4b committed (443dfca). 3 backend handlers + IntentDetector dynamic intents + SSRF validation + 3 SPA nodes + 3 property editors + graph-validator + flow-summarizer. Lessons learned kaydedildi.

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
- [ ] Flow Builder Phase 4b: AI/API node'lar (ai_intent, ai_faq, action_api_call)
- [ ] Flow Builder Phase 5: iframe + AHA #7 Trafik Heatmap + polish

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
    │   └── src/              # nodes/, components/, panels/, store/, types/, lib/, pages/
    ├── Middleware/            # Traffic logging + JWT auth
    └── Services/             # ChatAnalysisClient, AutomationClient, AgentAIClient, OutboundClient, FlowBuilderClient
```

---

## Context for Next Session

### Tum Bekleyen Isler TAMAMLANDI (2026-02-13)

- Flow Builder Phase 2: /rev FORCE_PASS (iter 3), committed (49879b6, 2066e07)
- GR-1.3 Outbound: /rev Codex 3 iter PASS, deployed (NSSM InvektoOutbound)
- Q: automation.sql migration calistirildi (chatbot_flows multi-flow PK)
- Q: tenant_registry.settings_json'a flow_builder_api_key eklendi
- Q: Outbound deploy tamamlandi (outbound.sql, appsettings.Production.json, NSSM servis)

### Phase 3 TAMAMLANDI (3a+3b+3c)

**Plan:** `arch/plans/20260213-flow-builder-phase3.json`
**Roadmap:** `arch/docs/flow-builder-roadmap.md`

**Phase 3a DONE** (74c9ffd): FlowEngine v2, 5 NodeHandlers, FlowValidator, FlowMigrator, ExpressionEvaluator, v1/v2 dispatch
**Phase 3b DONE** (aff97b3): SimulationEngine (ConcurrentDictionary, 30min TTL, IHostedService, tenant isolation), MockFaqMatcher, MockIntentDetector, SPA SimulationPanel (WhatsApp chat), AHA #4 Tek Tikla Test, active node highlight
**Phase 3c DONE** (f1dc4cf): Validation UI (FlowSettingsPanel "Akisi Dogrula" butonu), Variable Inspector (SimulationPanel Degiskenler tab), AHA #3 Ghost Path (path-enumerator DFS, purple highlight, dimming), AHA #5 Saglik Skoru (FlowValidator.CalculateHealthScore, HealthBadge, SQL conditional). Codex 3 iter: typed catches, SQL conditional, healthScore=0 on error.

### Phase 4a TAMAMLANDI

**Plan:** `arch/plans/20260214-flow-builder-phase4a.json`
**Phase 4a DONE**: 4 pure logic node handler (LogicConditionHandler, LogicSwitchHandler, ActionDelayHandler, SetVariableHandler) + IsSimulation flag + FlowValidator handle checks + 4 SPA node component + 4 property editor + graph-validator + flow-summarizer. 25 dosya +964 -42. Codex 3 iter Q FORCE PASS.

**Siradaki:** Phase 5 - Production integration (AutomationOrchestrator v2 dispatch, deploy, E2E test)

---

## Notes

- Stage-0 dokümanı: `invekto_stage0_kurulum_adimlari.txt`
- Full system dokümanı: `invekto_microservice_system_plan.txt`
- Error codes: `arch/errors.md` ve `Invekto.Shared/Constants/ErrorCodes.cs`
