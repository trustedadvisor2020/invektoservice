# FEAT-PHOTO Wire-up Patch — Tracking

**Slug:** `20260428-feat-photo-wireup-patch`
**Status:** REVIEW (post-build, pre-`/rev`)
**Risk:** MEDIUM (Codex iter 1 CQ5 reclassification — Program.cs runtime behavior + endpoint routing + Hangfire scheduling + DB queries + Shared constants)
**Created:** 2026-04-28 22:30 UTC
**Parent:** [`20260427-feat-photo-request-flow`](20260427-feat-photo-request-flow.md) — DONE+DEPLOYED+VERIFIED commit `1da0da6`

## 1. Amaç

FEAT-PHOTO parent paket'in 7 deploy blocker'ını runtime'a bağlayan post-deploy
scope-correction patch'i. Parent paket (commit `1da0da6`) compiled-in code
(PhotoInboundHandler, PhotoEndpoints, InmaInboundMediaEndpoint, PhotoRequestService,
3 Hangfire jobs, LeadDetailPage+PhotoTab) + Migration 034 prod canlı, ama runtime
wire-up YOK (orphan queue + Program.cs Map yok + Dashboard route yok). Bu patch
9 wire-up edit + 1 PNG asset ile foto akışını fiilen aktif eder.

## 2. Q Interview (3/3 gate)

Q approved 2026-04-28 22:25 UTC via AskUserQuestion:

| # | Soru | Cevap |
|---|------|-------|
| G1 | Queue strategy (orphan queue) | `[Queue("automation")]` rename — G7 single-queue-per-service topology |
| G2 | Lead lookup mechanism | Phone lookup post-book + graceful skip (E.164 normalization caller responsibility) |
| G3 | dental-angles.png host | InvektoWebsite repo `public/static/` |

## 3. Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | 3 photo job [Queue("automation")] rename + grep dogrulama 'Queue("photo-request-' 0 match | DONE — `PhotoRequestDispatchJob.cs` + `PhotoRequestReminderJob.cs` + `PhotoEscalationJob.cs` |
| AC2 | Automation Program.cs DI: IPhotoRequestService Singleton + 3 jobs Scoped | DONE — line 198-209 |
| AC3 | Backend Program.cs DI + Map (IPhotoInboundHandler, MapInmaInboundMediaEndpoint, MapPhotoEndpoints) | DONE — DI line 432-437; Map line 8807-8819 (post-`/ops/zoho/callback`, pre-SPA fallback) |
| AC4 | Backend `/api/v1/webhook/event` ana ingest media-route hop | DONE — line 1898-1953 (post-MessageLog, pre-Automation forward; fire-and-forget Task.Run dedup'i PhotoInboundHandler katmaninda) |
| AC5 | Slot booking trigger — Backend `/api/v1/appointments/book` proxy expanded | DONE — proxy generic helper'dan ozel handler'a cevrildi (line 4673+); microservice isolation korunuyor (Appointments degil Backend tetikliyor — Backend.csproj G7 SCHEDULER HOST EXCEPTION) |
| AC6 | ~~AppointmentsRepository.FindLeadIdByPhoneAsync helper~~ DROPPED | Lookup logic moved to Backend `/appointments/book` proxy inline SQL (mikro servis isolation) — Appointments csproj sadece Invekto.Shared ref'inde kalir |
| AC7 | Dashboard SPA App.tsx /leads/:id route + LeadDetailRoute wrapper | DONE — `LeadDetailRoute` useParams int parse + invalid id graceful redirect; SPA build chunk `LeadDetailPage-CqcRVXYP.js` |
| AC8 | dental-angles.png placeholder PNG (InvektoWebsite repo) | DONE — `c:/CRMs/InvektoWebsite/public/static/dental-angles.png` (13457 bytes) PowerShell System.Drawing 800×500 |

## 4. Files Changed

```
src/Invekto.Automation/Services/Jobs/PhotoRequestDispatchJob.cs   [Queue rename]
src/Invekto.Automation/Services/Jobs/PhotoRequestReminderJob.cs   [Queue rename]
src/Invekto.Automation/Services/Jobs/PhotoEscalationJob.cs        [Queue rename]
src/Invekto.Automation/Program.cs                                 [DI: IPhotoRequestService + 3 jobs]
src/Invekto.Backend/Program.cs                                    [DI + Map + webhook media hop + appointments/book proxy]
src/Invekto.Shared/Constants/ErrorCodes.cs                        [INV-AT-086 PhotoRequestLeadResolveSkip]
arch/errors.md                                                    [INV-AT-086 canonical mirror]
src/Invekto.Backend/Dashboard/src/App.tsx                         [/leads/:id route + LeadDetailRoute wrapper + useParams import]
src/Invekto.Backend/wwwroot/app/**                                [SPA Vite rebuild — LeadDetailPage-CqcRVXYP.js + index hash + 35 chunk rotated]
arch/plans/20260428-feat-photo-wireup-patch.json                  [plan]
tracking/20260428-feat-photo-wireup-patch.md                      [this file]
tracking/README.md                                                [pending: master tracking row]
arch/lessons-learned.md                                           [pending: 2 entries — orphan queue + microservice isolation slot booking]
```

**Cross-repo:** `c:/CRMs/InvektoWebsite/public/static/dental-angles.png` (manual commit InvektoWebsite repo + sonraki website deploy)

## 5. Microservice Isolation Decisions

İlk denemede `Invekto.Appointments/Program.cs` slot booking handler'ında dogrudan
`Invekto.Automation.Services.Jobs.PhotoRequestDispatchJob` referansi yapilmisti
— hook ihlali ve CLAUDE.md "Servisler arasi dogrudan referans YASAK" kuralina
aykiri. Düzeltme:

1. Appointments tarafindaki trigger geri alindi (sadece yorum birakildi).
2. AppointmentsRepository.FindLeadIdByPhoneAsync helper'i geri alindi.
3. Trigger Backend'in `/api/v1/appointments/book` proxy'sine tasindi
   (Backend.csproj zaten Invekto.Automation PrivateAssets="all" ref'i tasiyor —
   G7 SCHEDULER HOST EXCEPTION).
4. Backend proxy expanded: generic AppointmentsProxyPost helper'dan ozel handler'a
   cevrildi; post-201'de patient_phone parse + leads tablosu inline SQL lookup +
   BackgroundJob.Schedule.

Sonuc: Appointments servisi `Invekto.Shared` haricinde hicbir cross-service ref
tasimaz; mikroservis izolasyonu korunur.

## 6. Build

- .NET solution: PASS, 0 errors, 37 warnings (pre-existing)
- SPA Vite build: PASS, 5.25s, `LeadDetailPage-CqcRVXYP.js` chunk produced

## 7. Deploy Steps

1. SPA assets zaten `wwwroot/app/` icinde (build sonrasi yenilenmis)
2. Backend MCP `invekto-ops server-deploy` → InvektoBackend NSSM restart
3. Automation MCP `invekto-ops server-deploy` → InvektoAutomation NSSM restart
4. Appointments **deploy DEGISIKLIK YOK** — Appointments code degismedi (sadece 1
   yorum bloku); ancak Hangfire job DLL'i Backend uzerinden cagrilacak, Backend
   restart yeterli
5. dental-angles.png → InvektoWebsite repo commit + ayri website deploy
6. Smoke: 10/10 service /health HEALTHY + endpoint 401 gates + photo ingest curl

## 8. Smoke Plan (post-deploy)

| # | Adim | Beklenen |
|---|------|----------|
| S1 | `curl http://localhost:5000/health` | HEALTHY |
| S2 | `curl -X POST .../api/v1/inma/inbound/media` (no auth) | 401 INV-AUTH-006 |
| S3 | `curl -X GET .../api/v1/leads/123/photos` (no JWT) | 401 INV-AUTH gate |
| S4 | Hangfire dashboard inspect: queue='automation' lists PhotoRequestDispatchJob registration | OK |
| S5 | Test slot booking via Dashboard → log `PhotoRequestDispatchJob scheduled` veya `PhotoRequestLeadResolveSkip` | Ya schedule edilir ya graceful skip |
| S6 | Browser `/leads/1` (Dashboard) | LeadDetailPage render (PhotoTab init load) |
| S7 | `https://invekto.com/static/dental-angles.png` | 200 OK + PNG content (post-website-deploy) |

## 9. Rollback

- Queue rename: revert PhotoRequest{Dispatch,Reminder,Escalation}Job.cs `[Queue]`
  attributes back to `photo-request-*`
- Automation DI: remove 4 lines (IPhotoRequestService + 3 jobs)
- Backend DI/Map/webhook hop: remove DI line + Map calls + webhook foreach + proxy
  expansion (~150 lines)
- Dashboard route: remove `LeadDetailRoute` + Route + useParams import
- ErrorCodes INV-AT-086: keep (constant addition is backward compatible)
- PNG: keep or remove (orphan asset, harmless)

## 10. Codex Review Hazırlığı

Risk LOW (wire-up only). Hedef: tek iter PASS (CODEX UTANSIN). Verification questions
plan JSON `verification_questions` icinde Q1..Q4 hazir.
