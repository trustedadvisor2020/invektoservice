# Active Work

> Devam eden isler. Session basinda `session-memory.md` ile birlikte oku.

## Execution Queue

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | RI-3.1: Response Time Correlation | NEXT | LLM gerekmez, timestamp hesaplama |
| 2 | RI-3.3: Agent Leaderboard | NEXT | Agent bazli conversion, response time |
| 3 | RI-3.6: Follow-up Rescue Alerts | PLANNED | offered + 48h cevapsiz → rescue listesi |
| 4 | RI-3.2+3.4+3.5+3.7: Multi-extraction LLM call | PLANNED | 4 engine tek call |

## Recently Completed

| Date | Task |
|------|------|
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
