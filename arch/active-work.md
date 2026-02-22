# Active Work

> Devam eden isler. Session basinda `session-memory.md` ile birlikte oku.

## Execution Queue

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | tenant-instances.sql production'da calistir | PENDING | Q ops - SQL + DROP INDEX |
| 2 | Automation deploy | PENDING | tenant_instances routing icin gerekli |
| 3 | Backend deploy | PENDING | Instance filtering icin gerekli |
| 4 | Instance yonetimi E2E test | BLOCKED | #1-3 tamamlanmali |
| 5 | Multi-flow routing test | BLOCKED | #1-3 tamamlanmali |
| 6 | Template System SQL run | PENDING | template-catalog.sql production'da calistir (Q ops) |
| 7 | Knowledge deploy | PENDING | pgvector + template endpoints |
| 8 | Backend deploy | PENDING | template proxy + SPA rebuild |

## Recently Completed

| Date | Task |
|------|------|
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
