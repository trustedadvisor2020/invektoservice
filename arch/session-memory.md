# Session Memory

> Son session'dan kalan context. Her session başında oku.

## Last Update

- **Date:** 2026-02-12
- **Status:** Ops tamamlandı - tüm servisler production'da çalışıyor
- **Last Task:** Production deploy doğrulama. Backend:5000 (external OK), Automation:7108 (external OK), ChatAnalysis:7101 (localhost-only OK), AgentAI:7105 (localhost-only OK). Firewall kuralları tasarıma uygun.

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
| Outbound | 7107 | Reserved (Phase 1) |
| Automation | 7108 | Implemented (GR-1.1) |
| Simulator | 4500 | Dev-only tool (Node.js) |

### Deploy
- **Script:** `dev-to-invekto-services.bat`
- **Protokol:** FTPES (explicit TLS)
- **FTP Host:** services.invekto.com
- **Sunucu Yapi:** `E:\Invekto\Backend\current\`, `E:\Invekto\ChatAnalysis\current\`, `E:\Invekto\Automation\current\`, `E:\Invekto\AgentAI\current\`
- **Sunucu Domain:** services.invekto.com
- **Sunucu Root:** `E:\Invekto\` (Backend, ChatAnalysis, scripts, logs)
- **Service Manager:** NSSM (`E:\nssm.exe`)
- **Servisler:** InvektoBackend, InvektoChatAnalysis, InvektoAutomation, InvektoAgentAI, InvektoDeployWatcher (auto-start, auto-restart)
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
- [ ] **GR-1.3 Outbound Service** (Port 7107) - Broadcast, trigger, template engine, rate limiting

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
└── Invekto.Backend/          # Backend API (Port 5000)
    ├── Dashboard/            # React/TS Ops Dashboard
    ├── Middleware/            # Traffic logging + JWT auth
    └── Services/             # ChatAnalysisClient, AutomationClient, AgentAIClient
```

---

## Context for Next Session

Sonraki session'da:
1. **GR-1.3 Invekto.Outbound** mikroservisi (Port 7107) implement et
2. Scope: Broadcast/toplu mesaj, trigger engine, template engine, rate limiting, opt-out, delivery tracking
3. DB tablolari: outbound_templates, outbound_messages, outbound_optouts (roadmap-phases.md'de tanimli)
4. Mimari: Backend proxy pattern (Main App -> Backend:5000 -> Outbound:7107, localhost-only)
5. Mevcut pattern'leri takip et: Automation + AgentAI servisleri referans
6. Firewall: localhost-only (remoteip=127.0.0.1) - Backend proxy uzerinden erisim
7. Deploy sonrasi: outbound.sql calistir, appsettings.Production.json doldur, NSSM servis kur

---

## Notes

- Stage-0 dokümanı: `invekto_stage0_kurulum_adimlari.txt`
- Full system dokümanı: `invekto_microservice_system_plan.txt`
- Error codes: `arch/errors.md` ve `Invekto.Shared/Constants/ErrorCodes.cs`
