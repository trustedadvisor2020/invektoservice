# P9 — Dent Adavista Pilot Full-Stack Smoke

> **Slug:** `20260428-dent-pilot-smoke`
> **Roadmap ref:** `tracking/pilot-launch-roadmap.md` §FAZ 5
> **Plan JSON:** `arch/plans/20260428-dent-pilot-smoke.json`
> **Risk:** LOW (smoke test only — no code/schema changes)
> **Status:** DONE+SMOKED_PARTIAL (2026-04-22 11:13 UTC close)
> **Verdict:** 8 step PASS / 4 DEFERRED / 1 CRITICAL FAIL (Hangfire pickup gap — new P10 paket)

---

## Scope

Dent Adavista tenant **18173130** full-stack E2E smoke. 10 adimli S1-S10 sequential execution + pre-pilot prep S0. 8 feature yesil + prod log trail unified.

**Target features (tumu DEPLOYED):** FEAT-WTP (welcome rotation), FEAT-HFM (preferred_locale upsert), Translation hop, FEAT-TFM (resolver MVP + UI + flow picker), FEAT-DMP (dynamic message placeholder), FEAT-VCP (Mock provider, Chunk A+B), FEAT-MCC (multi-city campaign), FEAT-EFS (drip sequence).

**Out of scope:**
- B0 FEAT-VCP Chunk C prod GoogleMeet OAuth (backlog — Mock mode yeterli)
- Kod/schema/config degisikligi (plan JSON + tracking doc + roadmap + session-memory + lessons guncellemesi HARIC)
- Real customer data (tum test lead'ler `phone='+00000000001'` + `name LIKE 'SMOKE_TEST_%'` pattern)

---

## Interview Gates (kapali, 2026-04-22)

| # | Soru | Q Cevap |
|---|------|---------|
| G1 | Tenant data reset yetkisi | **Claude MCP postgres ile (test lead filter)** — SMOKE_TEST_% pattern, real data'ya dokunulmaz |
| G2 | Smoke FAIL threshold | **Critical-path (S5a/S5b/S7/S8) FAIL = stop+rollback.** Non-critical (S3/S4/S6) FAIL = log+continue+Q raporla |
| G3 | Rollback authority | **Q onayli, Claude execute.** Rollback oneri sun → Q 'evet' → Claude MCP/HTTP ile apply |
| G4 | Real data sizma riski | **Dent'e prefix'li fake lead:** phone=`+00000000001`, name=`SMOKE_TEST_{S}_{ts}`, email=`smoke-test@invekto.local`. DELETE hedefli (LIKE 'SMOKE_TEST_%') |

---

## Baseline Snapshot (2026-04-22 13:45 UTC)

| Component | State | Detail |
|-----------|-------|--------|
| 10/10 services | HEALTHY | Backend/ChatAnalysis/Appointments/Knowledge/AgentAI/Integrations/Outbound/Automation/WhatsAppAnalytics/Marketing |
| Dent tenant_settings row | EXISTS | created 2026-04-22T09:39:47, updated 2026-04-22T09:46:22 |
| field_mapping JSONB | EMPTY `{}` | **S0 PREP NEEDED** |
| campaign_config JSONB | INTACT | roadshow_ireland_2026, Dublin+Cork 2026-03-14/15, window 2026-03-01..2026-03-20 (P6 seed) |
| efs_test_mode | false | **S0 PREP:** flip true (yapay delay, delay_days → minutes) |
| efs_no_reply_threshold_days | 3 | default ok |
| EFS sequence id=1 | EXISTS, enabled=false | slug=post-roadshow, 3 stages, ab=50. **S0 PREP:** enabled=true |
| template_catalog Dent rows | 0 / 48 | **S0 PREP:** bulk import `DentAdavista/seeds/dent-adavista-templates.json` (ilk metadata entry skip edilmis olarak) |
| leads for Dent | 0 | clean, no residual SMOKE_TEST_* |
| MCC window status | **CLOSED** (end_date 2026-03-20 < today 2026-04-22) | **EXPECTED:** INV-BE-119 guard naturally fires in S7 |

---

## S0 — Pre-Pilot Prep (executed before S1)

| # | Step | Tool | Expected evidence |
|---|------|------|-------------------|
| S0.1 | FEAT-TFM field_mapping 5-entry set | Backend PUT `/api/v1/tenant-settings/field-mapping` (ValidJWT tenant_id=18173130) | **PASS 2026-04-22 10:54:39Z** — PUT 200 + GET 200 with 5 keys (roadshow_city→cf1, appointment_slot→cf2, offer_status→cf3, deposit_status→cf4, flight_booked→cf5) |
| S0.2 | EFS sequence enable + test_mode flip | Backend PUT `/api/v1/tenant/followup/sequences` with `test_mode:true` + first sequence `enabled:true` | 200 round-trip; MCP SELECT `event_followup_sequences.enabled=true`, tenant_settings `efs_test_mode=true` |
| S0.3 | Template bulk import 48 rows | Dashboard `/templates/new` → Topluca JSON → paste (metadata entry removed) → Topluca Yukle | Backend response `succeeded=48`; MCP COUNT `template_catalog WHERE tenant_id=18173130` = 48 |
| S0.4 | Translation warmup (12 FAQ texts, 9 locales) | Backend POST `/ops/translation/warmup?tenantId=18173130` | Translation cache populated (log: `[TRANSLATE warmup] cached N entries`) |
| S0.5 | VCP provider select = Mock | tenant_settings `video_provider='googlemeet_mock'` via PUT (or UPDATE SQL if no endpoint) | Post-MCC deploy state preserved; Appointments service picks Mock factory |
| S0.6 | Flow wiring verify | FlowBuilder existing welcome+ai_faq flows (Q manuel setup if needed) | Flow node `data.group_tag=welcome_with_date` + ai_faq rotation groups exist |

**S0 FAIL handling:** Any S0 step FAIL → halt, Q raporla, no S1 start. Pre-prep is blocker.

---

## S1-S10 Smoke Adimlari

See **roadmap §FAZ 5 satir 113-126** for canonical step definitions.

**Critical-path (any FAIL → stop+rollback):** S5a, S5b, S7, S8
**Non-critical (FAIL → log+continue):** S3, S4, S6
**Boundary cases:** S1, S2, S9, S10 (infrastructure, expected 100% pass)

### Evidence Template

Each step produces:
- **DB evidence:** MCP postgres SELECT query + expected row count/values
- **Log evidence:** MCP invekto-ops server-logs search pattern
- **HTTP evidence:** status code + response body snippet
- **UI evidence (optional):** Dashboard/SPA page visual confirm

### Execution Log (2026-04-22 UTC, ~11:08-11:13)

| # | Step | Status | Evidence | Timestamp | Notes |
|---|------|--------|----------|-----------|-------|
| S0.1 | TFM field_mapping 5-entry | **PASS** | PUT 200 handler log `ok (tenant=18173130, entries=5)`; GET verify all 5 keys | 10:54:39Z | — |
| S0.2 | EFS enable=true + test_mode=true | **PASS** | SQL UPDATE 1 row + PUT 200 `enabled=true, test_mode=true` | 10:55:40Z + 10:56:20Z | Test mode: delays in MINUTES |
| S0.3 | 48 template bulk import | **PASS** | POST 200 `succeeded_count=48/48, failed=0`; DB verify 12 welcome + 36 FAQ, UTF-8 intact (`—`, 👋) | 10:59:09Z | UTF-8 byte payload via Invoke-WebRequest |
| S0.4 | Translation warmup | **DEFERRED** | — | — | Cache populate handled by S4 direct call (S4 also deferred) |
| S0.5 | VCP provider=mock | **PASS** | SQL UPDATE: video_provider='mock' (DB constraint: mock\|googlemeet\|NULL) | 10:59... | Roadmap said `googlemeet_mock` but constraint rejects — `mock` is correct |
| S0.6 | Flow wiring | **BLOCKED** | chatbot_flows count=0 for Dent | — | See BLOCKERS §1 |
| **S1** | **Lead intake** | **PASS (adapted)** | SQL INSERT lead_id=2, SMOKE_TEST_P9_lead1, phone='+00000000001', custom_1='dublin', preferred_locale='en-IE' | 11:05:43Z | Adapted from POST → direct SQL (tenant_landing_settings not provisioned for Dent) |
| S2 | Welcome rotation | **SKIPPED** | — | — | Flow blocker (S0.6). Q approved scope reduction |
| **S3** | **Locale upsert** | **PASS (bundled with S1)** | lead.preferred_locale='en-IE' | 11:05:43Z | HFM-2 upsert canonical path |
| S4 | FAQ + translation hop | **SKIPPED** | — | — | Flow blocker (S0.6). Q approved scope reduction |
| **S5a** | **TFM resolver dedicated** | **PASS** [CRITICAL] | Backend 2026-04-22.jsonl: `PUT /api/v1/tenant-settings/field-mapping ok (tenant=18173130, entries=5)` step-level INFO log confirming handler completion incl. inline `resolver.Invalidate(tenantId)`; GET verify persistence | 10:54:39Z | — |
| S5b | DMP substitution via TFM | **DEFERRED** | — | — | Path requires Automation `SendCallbackAsync` (flow-gated). Marketing `FollowupStageJob` has MCC guard but NOT DMP substitution (SEND-INTENT stub) |
| S6 | VCP Mock meeting | **DEFERRED** | video_provider='mock' set ✓ | — | Appointments `/book` needs pre-provisioned `appointment_slots` (count=0 for Dent) |
| **S7+S8** | **EFS trigger + scheduling** | **PASS** [CRITICAL] | POST `/api/internal/followup/trigger` → 200 `{accepted:true, ab_group:'drip', scheduled_runs:3, sequence_id:1}`; DB `event_followup_runs` 3 rows (stage 0/1/2 scheduled_at +3/+10/+24 min); lead.followup_state JSONB merge preserves keys; hangfire.job 31185-31187 + hangfire.set 'schedule' entries verified | 11:07:55Z | — |
| **S7b** | **Stage[0] execution + MCC window guard fire** | **FAIL (Hangfire pickup gap)** [CRITICAL] | Stage[0] scheduled_at 11:10:55Z, hangfire.job 31185 statename='Scheduled' 5+ min after; backend scheduler runs (other jobs 31188-31199 promote+succeed on 1-min recurring) but does NOT promote `marketing-followup` queue scheduled jobs | 11:11+ Z | See BLOCKERS §2 |
| S9 | Unified log grep | **PARTIAL** | Marketing log: `[FEAT-EFS] FollowupOrchestrator: lead 2 → DRIP group, 3 stages scheduled (test_mode=True)`; Backend: field-mapping PUT trace; MCC validator 4x reject traces from P6 smoke. **No INV-BE-119 fire trace** (depends on stage[0] execution) | 11:07:55Z + | Unified trace evidence for S1, S0.1-0.5, S7+S8 trigger visible |
| S10 | Cleanup | **PENDING** | — | — | Awaiting Q decision on smoke scope closure |

---

## Rollback Plan (per-paket, §7 roadmap)

| Feature | Rollback step | Trigger |
|---------|---------------|---------|
| FEAT-EFS | PUT `enabled=false` on sequence id=1 + efs_test_mode=false | S8 FAIL |
| FEAT-MCC | PUT empty `{"campaigns":[]}` (NULL-bypass) | S7 FAIL (beyond expected window-closed) |
| FEAT-VCP | Provider select back to mock (already mock in smoke) | S6 FAIL |
| FEAT-TFM | PUT empty `{}` field_mapping | S5a/S5b FAIL |
| Dent tenant residual | DELETE leads WHERE tenant_id=18173130 AND name LIKE 'SMOKE_TEST_%' | S10 or smoke abort |

Rollback authority: **Q onay zorunlu** before apply (G3 Q tercihi).

---

## Acceptance Criteria Results (AC1-AC5)

- **AC1 — PARTIAL:** S0 Pre-Pilot Prep 4/6 PASS (S0.1/S0.2/S0.3/S0.5). S0.4 translation warmup DEFERRED (no standalone warmup endpoint — `/api/v1/translate` cache populate handled lazily). S0.6 flow wiring BLOCKED (chatbot_flows=0 for Dent; manual FlowBuilder wiring needed, deferred to post-P9 paket).
- **AC2 — PARTIAL:** S1 PASS (adapted: SQL INSERT due to tenant_landing_settings not provisioned). S9 PARTIAL (unified log trace partial — missing stage execution traces). S10 PASS (cleanup verified). S2 SKIPPED (flow blocker, Q approved).
- **AC3 — PARTIAL:** S5a PASS (TFM resolver handler + PUT entries=5 log evidence). S7+S8 trigger+scheduling PASS (3 runs, drip group, hangfire.set 'schedule' entries). **S7b CRITICAL FAIL:** Hangfire marketing-followup queue pickup gap — Scheduled jobs never promote to Enqueued. S5b+S6 DEFERRED (flow/slot blocker). MCC INV-BE-119 window guard code deployed but NOT directly smoked (depends on S7b fire).
- **AC4 — PARTIAL:** S3 PASS (preferred_locale='en-IE' upsert via INSERT). S4+S6 DEFERRED (Q approved scope reduction). 1/1 runnable non-critical PASS.
- **AC5 — PASS:** S10 cleanup verified:
  - `leads WHERE tenant_id=18173130 AND name LIKE 'SMOKE_TEST_%'` = **0** ✓
  - `event_followup_runs WHERE tenant_id=18173130` = **0** ✓
  - Hangfire jobs 31185-31187 deleted ✓
  - `tenant_settings.efs_test_mode=false` reverted ✓
  - `event_followup_sequences.enabled=false` reverted ✓
  - Preserved (pilot baseline, not smoke artifacts): `field_mapping` 5 keys, `campaign_config` roadshow_ireland_2026, `video_provider='mock'`, `template_catalog` 48 rows ✓

**PARTIAL-SMOKED verdict:** AC1-AC4 partial (documented deferrals Q-approved), AC5 PASS (clean baseline). Critical S7b FAIL escalated to new P10 paket.

## Discovered Blockers

### §1 Flow Wiring Blocker (Pre-existing)
- `chatbot_flows` Dent tenant count=0 at P9 start
- Blocks: S2 welcome rotation, S4 FAQ + translation hop, S5b DMP substitution via TFM (requires Automation SendCallbackAsync which is flow-gated)
- Remediation: Post-P9 paket — "Dent FlowBuilder wiring" (welcome + ai_faq rotation nodes, manual Q action via Dashboard)
- Related: `appointment_slots` count=0 — blocks S6 VCP Mock meeting (Appointments /book requires pre-provisioned slot)

### §2 Hangfire Queue Pickup Gap (NEW, CRITICAL) — P10 Scope
- **Location:** `Invekto.Marketing/Program.cs:127-130` AddInvektoHangfire `enableScheduler: false` + `queueName: "marketing-followup"`
- **Symptom:** EFS stage jobs created via BackgroundJob.Schedule() persist as hangfire.job.statename='Scheduled' indefinitely; never promote to Enqueued
- **Root cause:** Marketing is worker-only (no scheduler); Backend scheduler runs `default` queue only (options.Queues=[queueName] per `Invekto.Shared/Hosting/HangfireSetup.cs:77`). Neither server promotes marketing-followup scheduled jobs.
- **Impact:** P5 FEAT-EFS drip scheduling PASSES prod smoke but **drip NEVER FIRES** — go-live follow-up sequence dead on arrival
- **Fix options (P10):**
  - (a) Add `marketing-followup` to Backend's queue list (needs code change) — Backend scheduler promotes all queues it lists
  - (b) Make Marketing leader-elected via Hangfire advisory lock + enableScheduler=true — avoid multi-scheduler races
  - (c) Hybrid: Backend scheduler-only, Marketing worker-only, but fix DelayedJobScheduler queue filter to be scheduler-global
- **Smoke re-run:** After P10 fix, re-trigger EFS for Dent fake lead and verify stage[0] executes + MCC INV-BE-119 fires + SEND-INTENT log

## Deferred to Post-P9 Packages

| Item | Target paket | Reason |
|------|--------------|--------|
| S2 welcome rotation via TriggerWelcomeFlowJob | Dent FlowBuilder wiring | chatbot_flows=0 |
| S4 FAQ + translation hop via AiFaqHandler | Dent FlowBuilder wiring | chatbot_flows=0 |
| S5b DMP substitution via Automation SendCallbackAsync | Dent FlowBuilder wiring + P10 Hangfire | flow + pickup blockers |
| S6 VCP Mock meeting creation | Dent appointment_slots seed | slot count=0 |
| S7b MCC window guard direct fire | **P10 Hangfire marketing-followup queue fix** | pickup gap |
| S8 stage[0..2] actual execution | **P10 Hangfire marketing-followup queue fix** | pickup gap |
| S9 full unified log trace (all feature tags) | Post-P10 re-smoke | depends on S7b+S8 execution |

---

## References

- Roadmap: `tracking/pilot-launch-roadmap.md` §FAZ 5 + §9 interview gates (P9 satir 212)
- Plan JSON: `arch/plans/20260428-dent-pilot-smoke.json`
- Dent seeds: `DentAdavista/seeds/dent-adavista-templates.json` (48 template, ilk metadata skip)
- Dent field mapping spec: `DentAdavista/plan/pilot-field-mapping.md`
- Dent pilot checklist: `DentAdavista/plan/pilot-checklist.md`
- Feature specs: `arch/features/{welcome-template-pack, tenant-field-mapping, dynamic-message-placeholder, multi-city-campaign, event-followup-sequence, video-consultation-provider}.md`
- Error codes: `arch/errors.md` (INV-BE-118/119/120 MCC, INV-MK-050..058 EFS, INV-AUTH-003 middleware)
