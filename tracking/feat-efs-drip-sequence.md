# P5 FEAT-EFS Drip Sequence — Tracking

> **Slug:** `20260425-feat-efs-drip-sequence` | **Roadmap:** P5 FAZ 3 | **Risk:** MEDIUM
> **Plan JSON:** `arch/plans/20260425-feat-efs-drip-sequence.json` (verdict=PASS iter 4)
> **Status:** **DONE (code, commit bekliyor)** — 2026-04-21 18:50 UTC

## Scope

Event Follow-Up Sequence — Hangfire-orchestrated N-stage post-event drip nurture with deterministic A/B control group, per-stage audit row, execution-time opt-out guard, tenant test_mode for fast smoke, Dashboard SPA editor.

## Interview Answers (2026-04-21 15:55 UTC)

- **Q1 Schema:** Roadmap (dedicated tables `event_followup_sequences` + `event_followup_runs`) — spec §4 JSONB-in-tenant_settings stale.
- **Q2 Orchestrator:** Marketing (:7112) yeni FollowupOrchestrator + Hangfire setup (Shared HangfireSetup reuse, queue=`marketing-followup`).
- **Q3 Opt-out race guard:** Execution-time (Hangfire job başında DB check + Marketing orchestrator pre-flight).
- **Q4 Test param:** `tenant_settings.efs_test_mode BOOLEAN` — TRUE iken delay_days → dakika.
- **+Q5 (discovered):** Spec vs roadmap schema mismatch resolved via Q1 answer.

## Acceptance Criteria

| # | Criterion | Status |
|---|-----------|--------|
| AC1 | Tenant config CRUD via Marketing GET/PUT + Backend SPA proxy + SPA editor, cap enforcement (max 5 stage / 30 unit) → INV-MK-053 | **MET** (code) |
| AC2 | 4 trigger HTTP contract `/api/internal/followup/trigger` (X-Internal-Service-Token + service JWT); per-stage Hangfire BackgroundJob.Schedule + event_followup_runs row | **MET** (code) |
| AC3 | Execution-time opt-out dual signal: inma_optout_outbox.event_type OR followup_state.opted_out_at → INV-MK-052 + status=skipped_optout + audit | **MET** (code, iter 3 dual-signal fix) |
| AC4 | A/B deterministic SHA256(tenant_id\|lead_id\|sequence_id)%100; control group ZERO rows; leads.followup_ab_group persisted | **MET** (code) |
| AC5 | Test mode flag unit switch: validator + orchestrator.DelayFor FromMinutes/FromDays coupled | **MET** (code) |
| AC6 | Build PASS; jwtRequiredPrefixes audit; 3-tier auth probe smoke (NoAuth/BadJWT/ValidJWT) | **Build PASS ✅** / smoke pending deploy |
| AC7 | Frontend wrapError helper (no fabricated INV-FE-*); Fragment+sibling tr row error; existing-data guard | **MET** (code) |

## Deliverables

### Migration
- `arch/db/migrations/029-efs-followup-sequence.sql` — event_followup_sequences + event_followup_runs tables + 4 ALTER columns + 3 hot-path indexes + 1 partial unique race guard + grants
- `arch/db/marketing.sql` canonical source-of-truth mirror (iter 2 CQ11 fix)

### Shared Contracts (5 new files)
- `FollowupTriggerReason` enum (NoReplyWelcome | OfferDeclined | OfferTimeout | OnHold)
- `FollowupRunStatus` enum + wire-values helpers (Scheduled/Sent/SkippedOptout/SkippedDisabled/Failed)
- `FollowupStageConfig` DTO
- `FollowupSequenceConfig` DTO
- `FollowupTriggerRequest` + `FollowupTriggerResponse` (single-envelope pattern)

### Marketing service (:7112)
- `Invekto.Marketing.csproj` — Hangfire.AspNetCore + Hangfire.PostgreSql pkg refs
- `FollowupSequenceRepository` — CRUD + tenant settings + dual-signal opt-out read + run history
- `FollowupSequenceValidator` — typed exception, cap enforcement
- `FollowupSequenceCache` — single-flight CT-safe (CT.None factory + WaitAsync(ct))
- `FollowupAbGroupAssigner` — SHA256 deterministic
- `FollowupStageJob` — Hangfire handler (AutomaticRetry=0, opt-out + enabled + stage_index guards)
- `FollowupOrchestrator` — 4 triggers + typed catches (NpgsqlException, PostgresException 23505, BackgroundJobClientException)
- `FollowupEndpoints` — 4 endpoints (GET/PUT sequences, GET runs, POST internal trigger with FixedTimeEquals + JWT validation)
- `Program.cs` — DI + AddInvektoHangfire (queue=marketing-followup, enableScheduler=false) + EnsureJobStorageInitialized + MapFollowupEndpoints

### Automation (:7108)
- `MarketingFollowupClient` — X-Internal-Service-Token + per-call service JWT + single inline retry + ALL upstream fails INV-MK-057
- `NoReplyCheckJob` — simplified Hangfire handler, Marketing collision guard idempotent (chatops_messages dependency removed iter 2)
- `Program.cs` — DI registration

### Backend (:5000)
- `MarketingFollowupProxyClient` — Backend→Marketing tenant-scoped hop, FollowupSequenceListEnvelope (test_mode + no_reply_threshold_days forward)
- `TenantFollowupSequenceEndpoints` — 3 SPA-facing endpoints (GET sequences/PUT sequences/GET runs), 4xx forward verbatim + 5xx → INV-MK-057
- `Program.cs` — AddHttpClient + jwtRequiredPrefixes += `/api/v1/tenant/followup/` + MapTenantFollowupSequenceEndpoints

### Dashboard SPA
- `types/followupSequence.ts` — TS DTOs with test_mode + no_reply_threshold_days
- `hooks/useFollowupSequence.ts` — opt-in (enabled bool), module-cache single-flight, wrapError canonical, exposes testMode + noReplyThresholdDays
- `components/FollowupSequence/FollowupStageRow.tsx` — Fragment + sibling tr per-row error
- `pages/settings/FollowupSequenceSettingsPage.tsx` — editor with TEST MODE banner, threshold hint, stage list, A/B slider, client-side cap validation
- `lib/api.ts` — 3 new methods + type re-exports
- `App.tsx` — lazy-loaded route `/settings/followup-sequence`
- `pages/SettingsPage.tsx` — entry card
- Vite output: `FollowupSequenceSettingsPage-BBB3hJ7E.js` chunk 11.13 KB / 3.71 KB gzip

## Error Codes

| Code | Purpose | HTTP |
|------|---------|------|
| INV-MK-050 | Sequence config invalid (slug/stages/template_slug shape) | 400 |
| INV-MK-051 | Logical absence (sequence/stage/lead deleted mid-flight) | 404 |
| INV-MK-052 | Opt-out skipped at execution-time guard | 200 (no-op success) |
| INV-MK-053 | Cap exceeded (max 5 stage / max 30 unit window) | 400 |
| INV-MK-054 | Sequence disabled at trigger time | 409 |
| INV-MK-055 | Lead already has active scheduled sequence (idempotency) | 409 |
| INV-MK-056 | FollowupStorageUnavailable (NpgsqlException transient) | 503 |
| INV-MK-057 | FollowupUpstreamUnavailable (Backend/Automation→Marketing transport) | 502 |
| INV-MK-058 | Reserved: deferred follow-up paket inbound-reply pre-check (unreferenced in MVP) | — |

## Codex Review Arc

| Iter | Verdict | Blockers | Resolution |
|------|---------|----------|------------|
| 0 | FAIL | 10 CQ + 3 CoVe | JSONB overwrite (CQ9 critical), catch(Exception) 3 yerde, error code taksonomi karışık, chatops_messages schema proof gap, TODO false positive, frontend testMode hardcoded, schema source-of-truth mirror eksik, stale allowed_files entry |
| 1 | FAIL | 6 CQ + 3 CoVe | JSONB merge fix, typed catches (most), INV-MK-056/057/058 eklendi, test_mode backend envelope, silent NpgsqlException closed, plan allowed_files NoReplyCheckJob path |
| 2 | FAIL | 6 CQ + 1 CoVe | FollowupOrchestrator.cs:180-194 kalan catch(Exception) iter 1'de kaçmış, chatops_messages tamamen kaldırıldı, arch/db/marketing.sql mirror, FollowupStageJob INV-MK-056 |
| 3 | FAIL | 1 CQ + 0 CoVe | MarketingFollowupClient (Automation side) INV-MK-050 → INV-MK-057, FollowupStageJob unused cache removed, INV-MK-058 reserved, GET proxy 4xx forward (PUT parity), GetLeadFollowupStateAsync dual-signal |
| **4** | **PASS** | **0 blocker** | **plan JSON allowed_files self-inclusion + diff artifact paths** |

Total: 5 iter, ~70K tokens per review. Pattern consistent with FEAT-DMP 6 iter precedent.

## Deploy Scope (pending, next session)

- **Migration 029** — automatic pre-publish via `/deploy` command
- **Marketing (:7112)** — new Hangfire runtime + DI + endpoints; FIRST deploy using `InternalServices:SharedSecret` key (lessons 2026-04-20 FEAT-VCP Chunk B: peer-service config mirror pre-deploy)
- **Automation (:7108)** — MarketingFollowupClient + NoReplyCheckJob DI
- **Backend (:5000)** — proxy client + endpoints + jwtRequiredPrefixes + SPA chunk

## Deploy Smoke Checklist (next session)

- [ ] 10/10 HEALTHY post-deploy
- [ ] 3-tier auth probe: Marketing `/api/v1/followup/sequences` (NoAuth/BadJWT/ValidJWT)
- [ ] 3-tier auth probe: Backend `/api/v1/tenant-settings/followup-sequence` (same)
- [ ] jwtRequiredPrefixes audit: `/api/v1/tenant/followup/` prod Program.cs
- [ ] Binary freshness: Marketing DLL + Backend SPA chunk `FollowupSequenceSettingsPage-*.js` in index.html
- [ ] DB SELECT verify: `tenant_settings` columns + 2 new tables + 4 indexes + 3 FKs
- [ ] Dent tenant (18173130) test sequence create via Dashboard + verify PUT round-trip

## Patterns Honored (lessons P1-P4)

- Single-flight cache CT-safe (CT.None factory + WaitAsync(ct)) — FEAT-TFM iter 3 lesson
- TenantContext read never scalar "TenantId" — lessons 2026-04-21
- NO `.RequireAuthorization()` — custom JWT middleware lesson
- wrapError helper (preserve errorCode verbatim + INV-OB-037 fallback) — P4 lesson
- Fragment + sibling tr per-row error — P3 CQ5+CQ10 lesson
- Existing-data guard disabled UI — P3 CQ9 lesson
- jwtRequiredPrefixes registration audit — AUTH-HOTFIX lesson
- Typed catches only — AUTH-HOTFIX + iter 1 lesson
- Peer-service config mirror pre-deploy audit — FEAT-VCP Chunk B lesson (deploy pending)

## References

- Plan JSON: `arch/plans/20260425-feat-efs-drip-sequence.json`
- Diff artifacts: `arch/plans/diffs/20260425-feat-efs-drip-sequence{,-iter1,-iter2,-iter3,-iter4,-1-schema,-2-marketing,-3-consumers,-A,-B}.diff`
- Spec (stale vs roadmap): `arch/features/event-followup-sequence.md`
- Roadmap: `tracking/pilot-launch-roadmap.md` P5 row
- Lessons: `arch/lessons-learned.md` (5 new entries)
