# Session Memory

> Son session'dan kalan context. Her session basinda oku.
> **Paket tracking icin:** `tracking/README.md`

## Last Update

- **Date:** 2026-04-13
- **Status:** DONE. INMA↔INSE Unification Platform projesi kuruldu + UP0.1 (contract reorg) + UP0.6 (feature flag service) committed. Codex iteration=0 PASS (2/2 paket).
- **Last Task:** (1) Dent Adavista müşteri analizi + tam plan (10 faz, DentAdavista/plan/). (2) INMA↔INSE unification platform kurumsal dokümantasyonu (arch/platform/inma-inse-unification/): topology, gap-matrix (42 feature), INMA feature audit (12 var, 30 yok/kısmi), INMA team kickoff brief (Angular 19 kod örnekleri + veri akış diyagramları ile), roadmap (P0/P1/P2), inse-current-state. (3) UP0.1 MVP: Backend inline WapCrm DTO'ları (WapCrmInstanceDto, WapCrmApiEnvelope, WapCrmRawInstance, WapCrmSettings) → Invekto.Shared/Contracts/Inma/Dtos/ canonical namespace. Codex PASS iter=0 (commit d4e753c). (4) UP0.6: IFeatureFlagService + InMemoryFeatureFlagService (IMemoryCache, 5dk TTL, closed-by-default, per-tenant+user key). DI registration + inma /exchange + /login endpoint'lerinde SetFeatures hook. Codex PASS iter=0 (commit 1ddb495). (5) Decisions.md: custom fields → INMA'nın 10 tenant field'ı kullanılacak (G4 iptal, 4-5g kazanç). Kanallar → INMA üzerinden WA+IG+Telegram (Email yok).
- **Files Changed:** ADD: DentAdavista/plan/*.md (README, phase-0 to phase-9, customer-info, decisions, phase-0-audit-report, unified-platform-architecture), arch/platform/inma-inse-unification/{README, inma-feature-audit, gap-matrix, roadmap, inma-team-kickoff-brief, inse-current-state}.md, src/Invekto.Shared/Contracts/Inma/{README.md, Dtos/WapCrmInstance.cs, Dtos/WapCrmSettings.cs}, src/Invekto.Shared/Services/{IFeatureFlagService, InMemoryFeatureFlagService}.cs, arch/plans/20260413-up01-inma-contract-reorg.json, arch/plans/20260413-up06-feature-flag-service.json. EDIT: src/Invekto.Backend/{Program.cs (DI + exchange/login hooks), Data/InstanceRepository.cs, Data/TenantRegistryRepository.cs}.
- **Build:** PASS (0 errors).
- **Next Task:** G3 Template A/B rotation (1-2g, Dent için kritik) veya UP0.1b (mevcut Shared WapCrmMessage + IncomingWebhookEvent DTO'larını Contracts/Inma altına taşı, 10+ caller etkilenir).

## Execution Queue

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | G3 Template A/B rotation | PENDING | 1-2g, Dent 46 welcome+FAQ varyantı için |
| 2 | UP0.1b Shared DTO consolidation | PENDING | WapCrmMessage + IncomingWebhookEvent Contracts/Inma'ya, 10+ caller using update |
| 3 | G7 Hangfire migration | PENDING | 5-7g platform yatırımı — ReminderSchedulerService replace |
| 4 | G6 Flow state persistence | PENDING | 2-3g — 1.5 gün wait restart-safe |
| 5 | UP0.2 SSO doğrulama + role map tamamlama | PENDING (INMA blocked) | JWT public key lazım |
| 6 | UP0.3 Tenant lifecycle handler | PENDING (INMA blocked) | INMA tenant.created event |
| 7 | UP0.5 IInmaSendClient | PENDING (INMA partial) | Outbound send, J1/J4 bekliyor |
| 8 | Dent Adavista pilot Faz 1-9 | BLOCKED | Platform P0 sonrası |
| 9 | PKT-13 Faz 1 Lead Scoring | PENDING | Marketing (Dent ile ilgisiz) |

## Recently Completed

| Date | Task |
|------|------|
| 2026-04-13 | UP0.6 (feature flag service): IFeatureFlagService + InMemoryFeatureFlagService (Shared), IMemoryCache 5dk TTL, closed-by-default, per-(tenant,user) keyed. Backend DI + /inma/auth/exchange ve /login hooks. Codex PASS iter=0 (12/12 CQ, 3/3 CoVe). Commit 1ddb495. |
| 2026-04-13 | UP0.1 MVP (INMA contract reorg): Backend inline WapCrm DTO'ları Invekto.Shared.Contracts.Inma.Dtos namespace'ine taşındı. 3 yeni dosya, 3 Backend dosya using update. Codex PASS iter=0 (12/12 CQ, 3/3 CoVe). Commit d4e753c. |
| 2026-04-13 | INMA↔INSE Unification Platform kurumsal dokümanı: arch/platform/inma-inse-unification/ (README, topology, inma-feature-audit 42 feature, gap-matrix, roadmap P0-P2, inma-team-kickoff-brief Angular 19 kod örnekleriyle, inse-current-state). Dent Adavista pilot planı 10 faz. INMA 10 custom field kullanılacak → G4 iptal. |
| 2026-04-11 | Multi-theme consolidation: (1) Translation AI language detect (Gemma/Claude) + 13-language Unicode LanguageDetector + DTO `message` field (INMA compat) + Backend handler bug fix. (2) ClaudeWizardService truncated-JSON repair fallback + action_ecommerce guidance. (3) WhatsAppAnalytics minimal API `[FromServices]` DI fix (mass endpoint update). (4) LoginPage super-admin quick button cleanup. (5) review-policy → v3.2 (TONIVA-aligned). (6) Plan JSON status DONE backfill (pkt12 rescue, pkt11 voiceai, pkt12 f4 dashboard). (7) Instinct infra scaffold (.claude/agents + commands + hooks + arch/instincts.md + patterns.json). Build PASS. |
| 2026-04-07 | Simulation Turkish char fix: MockIntentDetector tr-TR CultureInfo + Türkçe keyword'ler + diş sektörü synonym genişletme. AiIntentHandler dynamic fallback (custom intent'lerden örnek). |
| 2026-04-07 | Flow Template Gallery: 182 SE senaryo → FlowConfigV2 converter + static data. Dashboard galeri sayfası (/flow-templates) + FlowListPage şablon modal + FlowEditorPage AI wizard auto-open. SE AI PROMPT sekmesi. |
| 2026-04-06 | Translation Gemma 4 migration + LLM anti-leak patch. TranslationService: Gemma 4 primary (Google AI Studio) + Haiku fallback. System prompt hardening: chain-of-thought sızıntısı fix, devrik çeviri fix, 4 servise anti-leak kuralları (WebChat, Automation, Marketing, Backend). 4 servis deploy. |
| 2026-04-01 | HOTFIX: Translate 401 fix + tenant auto-provision. JWT middleware bypass, inma_code kolonu (migration 009), ResolveTranslateTenantAsync (string code → auto-provision tenant_registry), 3x Backend deploy. Voila tenant_id=14120748 otomatik oluşturuldu. |
| 2026-03-27 | PKT-11 Faz 1: VoiceAI MVP. Invekto.VoiceAI microservice (port 7114), Whisper API STT, VoiceTranscriptionService orchestrator, Backend proxy + health + discovery. 15 files, 1161 insertions. Codex iter 1 Q FORCE PASS (CQ5 fixed, CQ11/Q1/CQ10/CQ12 false positive). |
| 2026-03-27 | PKT-12 Faz 4: Dashboard. RescueDashboardPage (KPI cards, risk table inline edit, template CRUD), api.ts 8 methods + 7 interfaces, /rescue route, sidebar. 5 files, 742 insertions. Codex iter 1 Q FORCE PASS. Full deploy (Backend+Marketing+Automation HEALTHY). Migration 008 run. |
| 2026-03-27 | PKT-12 Faz 3: Follow-Up Scheduler. RescueFollowUpService (4h timer), T+24h satisfaction + T+48h review redirect, Marketing endpoints, migration 008. 9 files, 580 insertions. Codex iter 1 Q FORCE PASS |
| 2026-03-26 | PKT-12 Faz 1+2 committed (34cca86): Risk Scoring Engine + Rescue Action Engine. 7 files, 318 insertions. Codex iter 3 Q FORCE PASS |
| 2026-03-26 | PKT-11/12/13 full scope planlama: interview + plan onay. Sıra: PKT-12→11→13 |
| 2026-03-26 | INMA Chat Analysis 401 fix: IP whitelist (91.151.84.79) + IPv6-mapped normalize (::ffff: prefix). Production hotfix + kalıcı kod fix |
| 2026-03-05 | FM-1c Monitor AI Chat: MONITOR mode system prompt, execution_detail SSE param, MonitorAiPanel (streaming + accept/reject + proactive), INV-AT-050/051/052. Codex iter 2 manual PASS |
| 2026-03-05 | FM-1b Flow Monitor Page: 3-panel layout, cross-flow endpoint, 4 filters, 5s polling, Zustand store, FlowMonitorPage. Codex iter 1 FORCE PASS. Backend + Automation deploy |
| 2026-03-05 | FM-1a Flow Versioning Backend: migration 007, repo CRUD, save hook, 3 API endpoints, 3 proxy routes, Toolbar version badge. Codex iter 1 PASS. Backend + Automation deploy |
| 2026-03-05 | Dashboard sidebar V3 format (section headers, teal active, slate colors, Inter font, Outfit headings) + 4x production deploy |
| 2026-03-05 | WebChat ops 502 fix (InternalApiKey config) + Dashboard tab title + logo font size |
| 2026-03-05 | WebChat perf fix (AI delay 0, DB combine, fire-and-forget) + widget JS fix + SDD Faz 1-3 + cross-project SDD |
| 2026-03-04 | Wizard auto-apply edit mode — AI Destek flowconfig auto-apply + system prompt. Codex PASS iter=0 |
| 2026-03-04 | Translation INMA echo-back contract + Postman + Dashboard widget + 2x Backend deploy |
| 2026-03-03 | QNB VPos 3DPay — tenant_payments, QnbVPosService, 3 endpoints, PaymentPage. Commit 6b2c57a |
| 2026-03-03 | ikas E-Commerce — IEcommerceProvider, IkasProvider, 6 endpoints, action_ecommerce node |
| 2026-03-03 | Faz 1 Paket 2 — SuperAdmin API, Quota, Cache Invalidation |
| 2026-03-02 | Faz 1 Paket 1 — Plan Permission System |
| 2026-03-02 | RI Cross-Service Integration — Knowledge sync, Outbound rescue, Template CRUD |
| 2026-03-02 | Knowledge Website Indexing + WebChat Automation Webhook |
| 2026-03-01 | RI-8 Optimizasyon + RI-7 Tenant Onboarding + RI Faz 3-6 |
| 2026-02-28 | RI-Faz2 Nightly Batch İlk Çalışma |
| 2026-02-27 | RI-Faz2 GATE-2 FULL PASS |
| 2026-02-24 | Dashboard UI Redesign + FlowBuilder Wizard UX |

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
| 2026-03-02 | RI Cross-Service Integration | P1-P4 complete: Knowledge sync, Outbound rescue trigger, Template CRUD UI (12 WA endpoints + Dashboard page), Marketing sync. 17 files, 1751 insertions. 6 error codes. Codex 3 iter FORCE PASS. |
| 2026-03-02 | Knowledge Website Indexing | WebScrapingService (sitemap+robots.txt+HtmlAgilityPack), SSRF guard (IPv4+IPv6+link-local+DNS resolution), PdfChunkingService.ChunkText (DRY), typed exceptions. Codex Q FORCE PASS. |
| 2026-03-02 | WebChat Automation Webhook | AutomationWebhookClient fire-and-forget, DB-driven per-widget flow mapping (webchat_widget_configs), 5min ConcurrentDictionary cache, 3 hook points. Codex Q FORCE PASS. |
| 2026-03-02 | Knowledge Website Indexing | POST /documents/website, WebScrapingService (sitemap+robots+HtmlAgilityPack scrape), SSRF guard (IsPrivateAddress public static, IPv4+IPv6+IPv4-mapped+DNS rebinding), PdfChunkingService.ChunkText DRY, embedding loop zero-progress break. Codex 6 iter FORCE PASS. |
| 2026-03-01 | NightlyBatch AutoDiscovery | WaClient.Management.Companies'den aktif, süresi dolmamış tenant'ları otomatik keşfet. Config overrides DatabaseName ile eşleşir (3 mevcut tenant korunuyor). Companies.ID = TenantId, sector = "genel". SqlException + InvalidOperationException typed catch, INV-WA-024. Codex PASS iter 2. |
| 2026-03-02 | Faz 1 Paket 1 | plan_definitions tablosu + TenantPlanCache + FeatureGuardMiddleware. 3 seed tier (baslangic, profesyonel, kurumsal). Codex PASS. |
| 2026-03-03 | Faz 1 Paket 2 | SuperAdmin API: Plan CRUD (5 ep), Tenant Plan (2 ep), Cache Invalidate (1 ep). TenantUsageService (ConcurrentDictionary + periodic DB sync). tenant_usage tablosu, 5 error code (INV-BE-080..084). Automation message quota + max_flows guard. Codex 3 iter FORCE PASS (CQ2/CQ5 contradiction). Migration 004 run, Backend + Automation deployed + 7/8 AC verified. |
| 2026-03-03 | ikas E-Commerce | IEcommerceProvider interface, IkasProvider (OAuth2 client_credentials + GraphQL), IkasTokenManager (per-tenant SemaphoreSlim cache), IkasGraphQlClient (401 retry). 6 REST endpoint (Integrations). action_ecommerce flow node (Automation, 8 ops). DB: integration_accounts + orders_cache + cargo_tracking_events + products_cache migration. GraphQL field fix: hasNextPage→hasNext, stock→stocks, packages→orderPackages. Codex 4 iter, FORCE PASS. Tenant 5050 ikas credentials DB'ye eklendi. |
| 2026-03-03 | QNB VPos 3DPay | tenant_payments tablosu (005-migration) + QnbVPosService (SHA1 hash, auto-submit HTML) + PaymentDtos + 3 endpoint (POST initiate JWT, POST callback public, GET history ops) + Dashboard PaymentPage (payments.regex.pro adapte) + App.tsx /payment route + api.ts initiatePayment. INV-BE-085..089. Codex FORCE PASS iter=3 (bank callback no-JWT by design, CoVe 4/4). Migration + appsettings (QnbVPos section) + Backend deploy HEALTHY. |

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
