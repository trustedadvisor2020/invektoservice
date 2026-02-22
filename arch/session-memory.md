# Session Memory

> Son session'dan kalan context. Her session basinda oku.
> **Paket tracking icin:** `tracking/README.md`

## Last Update

- **Date:** 2026-02-23
- **Status:** Template ingestion E2E test tamamlandi. 4 phase (Extract→Review→Publish→Adopt) basariyla gecti. 1 bug fix (GetInt32 cluster_label type mismatch). Knowledge redeployed.
- **Last Task:** Template ingestion E2E test — (1) Ebrumoda WA analysis seed data (1 analysis, 8 FAQ cluster, 41 intent), (2) Extract: 16 suggestion (8 FAQ + 8 intent), (3) Review: 14 approved + 1 rejected + 1 pending, (4) Publish: 14/14 published, (5) Adopt: 7 intent adopted (tenant 5050), 7 FAQ skipped, (6) Bug fix: TemplateExtractorService.cs GetInt32 for cluster_label
- **Next Task:** Bug fix commit (GetInt32) + Codex review
- **Strateji:** 12 Paket Stratejisi v5.2

## Current State

### Ports

| Service | Port | Status |
|---------|------|--------|
| Backend | 5000 | Active |
| ChatAnalysis | 7101 | Active |
| Appointments | 7102 | Implemented |
| Knowledge | 7104 | Implemented |
| AgentAI | 7105 | Implemented |
| Integrations | 7106 | Implemented |
| Outbound | 7107 | Implemented |
| Automation | 7108 | Implemented |
| WhatsAppAnalytics | 7109 | Implemented |
| FaceAnalysis | 7110 | Planned (PKT-8) |
| VisualSearch | 7111 | Planned (PKT-7) |
| Marketing | 7112 | Implemented |

### Deploy

- **Deploy:** MCP `server-deploy` tool (SSH/SFTP, atomik: stop -> zip -> upload -> extract -> config restore -> start -> health)
- **Sunucu:** services.invekto.com, `C:\Invekto\{Service}\current\`
- **Service Manager:** NSSM (`C:\Invekto\nssm.exe`)
- **.NET Runtime:** ASP.NET Core 8.0.23

### Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | .NET 8 Minimal API |
| Microservice | .NET 8 Minimal API + Windows Service |
| Shared | .NET 8 Class Library |
| Frontend | React 18 + TypeScript + Vite |
| DB | PostgreSQL 16 + pgvector |
| Logging | JSON Lines (custom) |

### Project Structure

```
src/
├── Invekto.Shared/           # DTOs, constants, logging, auth
├── Invekto.Backend/          # Port 5000 - API + Dashboard + FlowBuilder SPA
├── Invekto.ChatAnalysis/     # Port 7101 - Claude Haiku chat analysis
├── Invekto.Appointments/     # Port 7102 - Randevu + TreatmentLifecycle
├── Invekto.Knowledge/        # Port 7104 - RAG + pgvector + PDF
├── Invekto.AgentAI/          # Port 7105 - AI reply suggestion
├── Invekto.Integrations/     # Port 7106 - HB, kargo, external APIs
├── Invekto.Outbound/         # Port 7107 - Broadcast + campaigns + triggers
├── Invekto.Automation/       # Port 7108 - Chatbot + FlowEngine v2
├── Invekto.WhatsAppAnalytics/ # Port 7109 - NLP pipeline C# port
└── Invekto.Marketing/        # Port 7112 - Reviews, referrals, tourism
```

## Recent Decisions

| Date | Decision | Reason |
|------|----------|--------|
| 2026-02-17 | PKT-5 split (5A/5B) | Codex review boyutu azaltma |
| 2026-02-17 | PKT-9/10 eklendi | Guzellik + Egitim niche'leri |
| 2026-02-18 | Deploy-watcher kaldirildi | SSH/MCP deploy yeterli |
| 2026-02-18 | Tracking konsolidasyonu | tracking/ klasoru, gereksiz dosyalar silindi |
| 2026-02-20 | TriggerTypes HashSet altyapisi | FlowGraphV2'de hardcoded trigger_start yerine extensible set |
| 2026-02-20 | 4 yeni node tipi (16 toplam) | SE 170 senaryo analizi ~80 senaryoda ihtiyac gosterdi |
| 2026-02-20 | INMA SSO hotfix | FB iframe localStorage/sessionStorage mismatch + 401 token wipe guard |
| 2026-02-20 | Webhook + CronScheduler | POST /api/v1/webhooks/ (no auth, Q karar), CronSchedulerService (Cronos, 60s timer) |
| 2026-02-20 | SuperAdmin message_log | Yeni PG tablo, webhook hook, ops endpoint, React sayfa. tenant_id=0 = superadmin |
| 2026-02-20 | INMA SSO exchange hotfix | InmaAuth:SecretKey olmadan exchange decode-only fallback, Dashboard token exchange |
| 2026-02-20 | Tenant isolation altyapisi | TenantRepositoryBase, SuperAdmin impersonate (Option C), Firmalar sayfasi |
| 2026-02-20 | 3 arch dokumani | tenant-isolation.md, auth-architecture.md, ops-dashboard-convention.md |
| 2026-02-20 | S2 Satis Asistani canli | Tenant 5050 (TestEticaret): 9-node v2 flow, WapCRM callback bridge (userID=12), E2E WhatsApp mesaj cevap dogrulandi |
| 2026-02-20 | INMA JWT CompanyCode fix | CompanyId (INMA internal=11) ≠ CompanyCode (tenant_id=5050). Backend exchange + Dashboard getSession/exchangeInmaToken duzeltildi, deploy edildi |
| 2026-02-21 | Flow Builder auto-layout | Nodes position olmadan gelince (0,0) stack oluyordu — BFS auto-layout eklendi |
| 2026-02-21 | Dashboard decode-only JWT fallback | inmaJwtValidator NULL ise exchange gibi decode-only Path C ile CompanyCode claim okunuyor |
| 2026-02-21 | daily_metrics tables created | backend-metrics.sql production'da calistirildi (daily_metrics + daily_intent_metrics) |
| 2026-02-22 | AI Chat Panel ("AI ile Gelistir") | Flow editor'de AI sohbet paneli. Mevcut wizard altyapisi reuse (edit-mode system prompt + flow_config param). Mutual exclusion with simulation. Codex iter 1, FORCE PASS (CQ1/CQ5 false positive). |
| 2026-02-22 | Working Hours Settings UI + API | Dashboard Ayarlar'a mesai saatleri section. GET/PUT /api/v1/settings/working-hours, JSONB merge, HH:mm + timezone + days_off validation. Codex FORCE PASS (CQ2/CQ3/CQ5 pre-existing false positive, CoVe 4/4 PASS). |
| 2026-02-22 | Instance Filtering & Multi-Flow Routing | tenant_instances tablosu, 1 instance = max 1 flow, backward compat (kayit yoksa eski davranis), webhook filter (disabled/unassigned = log+ignore), ActivateFlowAsync artik diger flow'lari kapatmiyor. |
| 2026-02-22 | Platform Evrim Katmanlari (Roadmap) | 6 akilil altyapi katmani roadmap'e eklendi: Musteri Hafizasi, Template Marketplace, Bilesik Olay Motoru, Voice AI, Gateway, Extension API. Phase 2-7 entegrasyonu, bagimllik haritasi, revenue etkisi, 6 yeni teknik risk, 3 yeni revenue driver. |
| 2026-02-23 | Template System (Sablon Sistemi) | Knowledge 7104 genisletildi: 5 tip (FAQ/Message/Intent/Flow/Scenario), 3 katman (Platform>Sector>Tenant), pgvector cosine similarity, suggestion queue, superadmin review UI, tenant onboarding. 19 dosya, 4243 insertion. Codex 5-chunk, 4 iter, PASS. |

## Q Pending Operational Tasks

- [ ] Knowledge: pgvector extension kur, knowledge.sql calistir, deploy
- [ ] WhatsAppAnalytics: deploy + NSSM servis
- [ ] Integrations: deploy + NSSM servis
- [ ] Marketing: Claude:ApiKey appsettings.Production.json'a ekle
- [ ] Appointments: appointments-v2.sql + appointments-v3.sql calistir
- [ ] Attribution: attribution.sql calistir
- [ ] Outbound: campaign + consent migration calistir
- [x] Backend: backend-metrics.sql calistir (2026-02-21 tamamlandi)
- [x] Template System: template-catalog.sql calistir (2026-02-23 — 7 tablo zaten mevcut)
- [x] Instance Routing: tenant-instances.sql calistir (2026-02-23 — tablo + index zaten mevcut)
- [x] Backend + Knowledge + Automation deploy (2026-02-23 — 10/10 HEALTHY)
- [x] Instance Routing: E2E test (6 senaryo, 6/6 PASS — 2026-02-23)
- [x] Welcome endpoint: JSON format fix (plain text → defensive parse, 2026-02-23)
- [x] Flow editor: double-JSON-encoded node data fix (options/cases/intents, 2026-02-23)

## Notes

- Error codes: `arch/errors.md`
- DB schemas: `arch/db/`
- API contracts: `arch/contracts/`
- Lessons: `arch/lessons-learned.md`
