# Session Memory

> Son session'dan kalan context. Her session basinda oku.
> **Paket tracking icin:** `tracking/README.md`

## Last Update

- **Date:** 2026-03-01
- **Status:** RI-6 P2 TAMAMLANDI. RI Faz 3-6 tum isler bitti.
- **Last Task:** RI-6 P2: Revenue Intelligence React dashboard + 8 widget. RI-3 (7 engine), RI-4 (template mining), RI-5 (bulk + 9 sektor), RI-6 P1 (12 API), RI-6 P2 (dashboard) — hepsi DONE.
- **Production SQL:** wa_revenue_attribution (RI-3.4), wa_objection_map (RI-3.5), wa_quality_scores (RI-3.7) tablolari production PG'de olusturuldu.
- **Next Task:** RI-7 Tenant Onboarding Deneyimi (sektor paketi, checklist, benchmark karsilastirma) veya WhatsAppAnalytics deploy.
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
| 2026-02-23 | Ebrumoda Full Pipeline | 2.1M satirlik WA CSV → pipeline (Claude skip, keyword-only intent) → 3,159 template suggestion → 60 approved+published (12 intent + 48 FAQ), 3,099 rejected. FormOptions fix (128MB→500MB multipart limit). |
| 2026-02-23 | Flow Execution Log + Instance Bug Fix | flow_execution_log tablosu + Automation fire-and-forget logging + FlowLogPanel UI + instance-aware GetMessageStoryAsync. Codex main 4 iter FORCE PASS, fix 2 iter FORCE PASS. SQL + deploy (Backend + Automation HEALTHY). |
| 2026-02-26 | RI-Faz2 batch pipeline | BatchClassificationService + NightlyBatchJob + ConversationOutcomeRepository + SectorConfigRepository + multi-sector BenchmarkOrchestrator. 2 SQL schema (wa_conversation_outcomes, wa_sector_config). Codex 3-chunk, FORCE PASS. |
| 2026-02-26 | Cross-session cleanup | 30 files from 5+ sessions: Automation tx fix + tenant_id filter, Knowledge FAQ embedding + lang removal, Backend endpoint updates, Dashboard severity sync + FlowList + NodePropertyPanel + KnowledgePage error logging. |
| 2026-02-27 | gemini_pro removed from pipeline | Q directive: "gemini-3.1-pro-preview atla. birdaha kullanma." Too slow (~20min/200 threads) ve ek değer yok. 5-model pipeline: keyword → haiku → sonnet → gemini_flash → gemini_3_flash → tiered |
| 2026-02-27 | RI-Faz2 GATE-2 F1 PASS | 3 sektor tiered F1 >= 0.80: Saglik=0.9952, Moda=1.0, Gayrimenkul=1.0. GT=auto-accept (tiered→GT, tautological). Gemini Flash en güçlü bağımsız model (cross-sector avg F1=0.8432) |
| 2026-02-27 | RI-Faz2 GATE-2 FULL PASS | Batch pipeline E2E (job #2: 3 classified, 0 errors). NightlyBatch config: 3 tenant, RunHour=02:00, aktif. 4 endpoint çalışıyor. NSSM servis adı=InvektoWhatsAppAnalytics. |
| 2026-02-23 | Self-Service Onboarding | Sektor secimi (GET/PUT sector) + sablon benimseme (3 proxy route) + SettingsPage dropdown + TemplateLibraryPage dual-mode. Codex 2 iter FORCE PASS. Deploy + onboarding/status 500 hotfix. |

## Q Pending Operational Tasks

- [x] Knowledge: pgvector + deploy (2026-02-23 — servis RUNNING + HEALTHY)
- [x] WhatsAppAnalytics: deploy + NSSM (2026-02-23 — servis RUNNING + HEALTHY)
- [x] Integrations: deploy + NSSM (2026-02-23 — servis RUNNING + HEALTHY)
- [x] Marketing: Claude:ApiKey (2026-02-23 — zaten konfigüre edilmiş)
- [x] Appointments: appointments-v2.sql calistir (2026-02-23 — waitlist + service_pricing, v3 zaten mevcuttu)
- [x] Attribution: attribution.sql calistir (2026-02-23 — lead_attributions + ad_costs)
- [x] Outbound: campaign + consent migration (2026-02-23 — 5 tablo + lang columns)
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
