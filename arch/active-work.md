# Active Work

> Devam eden isler. Session basinda `session-memory.md` ile birlikte oku.

## Execution Queue

| # | Task | Status | Notes |
|---|------|--------|-------|
| — | No active work | — | All packages done. Q to decide next task |

## Recently Completed

| Date | Task |
|------|------|
| 2026-03-03 | QNB VPos 3DPay — tenant_payments (005-migration), QnbVPosService (SHA1 hash + HTML form), 3 Backend endpoints, Dashboard PaymentPage + route + api.ts. INV-BE-085..089. Migration + config + Backend deploy HEALTHY. Commit 6b2c57a (FORCE PASS iter=3). |
| 2026-03-02 | Knowledge Website UI — DocumentUpload popup modal (PDF + Web Sitesi tabs), Backend proxy route, api.ts indexWebsite. Knowledge + Backend deployed HEALTHY. DB migration applied. |
| 2026-03-02 | Knowledge Website Indexing — WebScrapingService, SSRF guard (IPv4+IPv6+DNS), PdfChunkingService.ChunkText DRY, typed exceptions. Codex Q FORCE PASS. Knowledge deployed. |
| 2026-03-02 | WebChat Automation Webhook — AutomationWebhookClient, DB per-widget flow mapping, 3 hook points. Codex Q FORCE PASS. |
| 2026-03-02 | RI Cross-Service Integration — P1 Knowledge sync, P2 Outbound rescue trigger, P3 Template CRUD (12 WA endpoints + Dashboard page), P4 Marketing sync. 6 error codes (INV-BE-070/071/072, INV-WA-025/026/027). Codex 3 iter FORCE PASS. |
| 2026-03-01 | NightlyBatch AutoDiscovery — WaClient.Management'dan aktif tenant otomatik keşfi. Config overrides (3 tenant korunuyor). Companies.ID=TenantId, "genel" default sector. INV-WA-024 typed catch. Codex PASS iter 2. Deploy HEALTHY, autoDiscovery=True. |
| 2026-03-01 | RI-8 Optimizasyon — keyword pre-filter (14 regex, 3 outcome type, ModelVersion=keyword-v1), parallel MSSQL loading (4x concurrency via Parallel.ForEachAsync). Codex CQ PASS, CoVe FP FORCE PASS. Deploy WA HEALTHY. |
| 2026-03-01 | RI-7 Tenant Onboarding — OnboardingInsightService + GET /ri/onboarding + RiOnboardingPanel (checklist, quick start, benchmark comparison, sector overview). 60 onboarding steps seeded (12 sectors x 5). Deploy WA+Backend HEALTHY. |
| 2026-03-01 | RI Faz 3-6 — 7 insight engines, 12 sector template mining, 12 API endpoints, 8 React widgets, Revenue Intelligence dashboard. All deployed HEALTHY. |
| 2026-02-28 | RI-Faz2 Nightly Batch İlk Çalışma — 02:00 TR tetiklendi: EbruModa 500 classified (37dk), Hermest 500 classified (55dk), GoldenPartner 0 candidate. Toplam 1,003 outcome PG'de. Faz 2 tamamen kapatıldı. |
| 2026-02-27 | RI-Faz2 GATE-2 FULL PASS — Batch pipeline E2E (job #2: 3 classified, 0 errors, ~17s). NightlyBatch config aktif (3 tenant, 02:00). 4 endpoint verified. wa_conversation_outcomes + wa_sector_config tabloları + F1 değerleri production'da. |
| 2026-02-27 | RI-Faz2 Cross-Validation Benchmarks — Unicode surrogate fix deploy, gemini_pro removed (Q directive), 5 benchmarks (#18 Hermest, #19 Estethica, #29 EbruModa, #30 nevinkayamoda, #31 GoldenPartner), GT auto-accept, GATE-2 F1 PASS (3/3 sectors >= 0.80). Cross-sector avg F1=0.9984. |
| 2026-02-26 | RI-Faz2 Batch Pipeline + Cross-session Cleanup — 6 new WA files (batch/nightly/outcome/sector), 2 SQL schemas, multi-sector benchmark, cross-session fixes (KnowledgePage logging, tenant_id filter, FAQ embedding, lang removal, severity sync, FlowList, NodePropertyPanel). 52 files, 4 service deploy (all HEALTHY). |
| 2026-02-24 | FlowListPage + Sidebar Redesign — Gradient icon circles, search/filter, staggered animations, skeleton loading, timeAgo relative dates, modal X close, row layout, sidebar icon containers + brand active state + red hover logout. Deploy HEALTHY. |
| 2026-02-24 | Onboarding + Template Library UI/UX Redesign — OnboardingWizardPage (dark hero, SVG progress, timeline, accordion), TemplateLibraryPage (category-grouped, INTENT_TR, FAQ_TOPICS, examples), FAQ DB cleanup (39 unpublish, 9 kaldi), 4+ iterative deploy |
| 2026-02-24 | Dashboard UI Redesign — Glass morphism sidebar, Plus Jakarta Sans + DM Sans typography, root 18px, icon containers, Power logout icon, 6x iterative deploy |
| 2026-02-24 | FlowBuilder Wizard UX — (1) flow config extraction fix (```json + done content), (2) option buttons, (3) samimi loading text, (4) collapsible teknik detaylar. Codex PASS iter 0. |
| 2026-02-23 | Self-Service Onboarding — Sektor secimi + sablon benimseme self-service, Codex 2 iter FORCE PASS, deploy + onboarding/status 500 hotfix |
| 2026-02-23 | Tum Mesajlar Kanal — LEFT JOIN instance_name, Kanal filtre dropdown (tenant-aware) + tablo kolonu, /api/ops/channels endpoint |
| 2026-02-23 | Flow Execution Log + Instance Bug Fix — flow_execution_log DB + Automation logging + FlowLogPanel UI + instance-aware flow lookup + deploy (Backend + Automation HEALTHY) |
| 2026-02-23 | Template ingestion E2E test — 4 phase (Extract→Review→Publish→Adopt), Ebrumoda e-commerce data, 16 suggestion, 14 catalog, 7 adoption, 1 bug fix (GetInt32), Knowledge redeploy |
| 2026-02-23 | Demo Bot — Invekto Demo Bot flow (20 node, 31 edge), BaseNode right outputs, MockIntentDetector synonym expansion, 5 FAQ entry |
| 2026-02-23 | Instance routing + Multi-flow routing E2E test — 6 senaryo, 6/6 PASS |
| 2026-02-23 | InmaJwtValidator CompanyCode alignment — validated path CompanyCode first + CompanyId fallback, INV-AUTH-004 |
| 2026-02-23 | Flow editor fix — welcome endpoint parse + double-JSON node data + DB migration |
| 2026-02-23 | INMA SSO hotfix — 7 fix, full deploy, tenant 5050 calisiyor |
| 2026-02-23 | Full deploy — Backend + Knowledge + Automation, 10/10 HEALTHY |
| 2026-02-23 | Template System (Sablon Sistemi) — Codex PASS, 4 iter, 14 fix |
| 2026-02-22 | Platform Evrim Katmanlari roadmap |
| 2026-02-22 | Instance Filtering & Multi-Flow Routing |
| 2026-02-22 | Working Hours Settings UI + API |
| 2026-02-22 | AI Chat Panel ("AI ile Gelistir") |
| 2026-02-22 | Dashboard + FlowBuilder SPA merge |
| 2026-02-22 | OnboardingGuidePage (7 sekme, 30 aksiyon) |

## Blocked Items

| Item | Blocker |
|------|---------|
| Knowledge deploy | pgvector extension kurulumu (Q ops) |
| WhatsAppAnalytics deploy | NSSM servis kurulumu (Q ops) |
| Integrations deploy | NSSM servis kurulumu (Q ops) |
| Appointments SQL | v2 + v3 migration calistirilmali (Q ops) |
| Marketing deploy | Claude:ApiKey appsettings.Production.json'a eklenmeli (Q ops) |
| Outbound SQL | campaign + consent migration calistirilmali (Q ops) |
| Attribution SQL | attribution.sql calistirilmali (Q ops) |
