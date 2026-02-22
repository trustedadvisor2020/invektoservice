# Active Work

> Devam eden isler. Session basinda `session-memory.md` ile birlikte oku.

## Execution Queue

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | Instance yonetimi E2E test | PENDING | SQL + deploy tamamlandi, test kalir |
| 2 | Multi-flow routing test | PENDING | #1 ile birlikte |
| 3 | InmaJwtValidator CompanyCode alignment | PENDING | Validated path CompanyId→CompanyCode |
| 4 | Template ingestion E2E test | PENDING | Ebrumoda verisi ile extraction |

## Recently Completed

| Date | Task |
|------|------|
| 2026-02-23 | Demo Bot — Invekto Demo Bot flow (20 node, 31 edge), BaseNode right outputs, MockIntentDetector synonym expansion, 5 FAQ entry |
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
