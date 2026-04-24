# Paket A — Dent Plan Tier Upgrade (baslangic→kurumsal) + S6 Re-Smoke

> **Slug:** `20260424-dent-plan-tier-upgrade`
> **Plan:** [`arch/plans/20260424-dent-plan-tier-upgrade.json`](../arch/plans/20260424-dent-plan-tier-upgrade.json)
> **Risk:** LOW | **Scope:** Ops-only | **Build:** N/A | **Deploy:** Yok (Q UI üzerinden Backend Ops PUT endpoint)
> **Status:** DONE+SMOKED_PARTIAL (plan_guard PASS, VCP meeting_link DEFERRED to B-VCP-DOCTORS)
> **Kickoff:** 2026-04-24 11:00 UTC
> **Close:** 2026-04-24 11:25 UTC
> **Motivation:** Post-P9 Dent Pilot FlowBuilder Wiring AC6 DEFERRED (S6 booking 403 INV-AUTH-005 — Dent `plan_tier='baslangic'` Appointments feature yok). Pilot go-live öncesi tier upgrade zorunlu. Tier seçimi: kurumsal (Marketing:[on] dahil — FEAT-EFS P5/P10 SMOKED için zorunlu).

## Q Intent

"Paket A başla. Tier seçimi: kurumsal (Recommended) — profesyonel Marketing içermiyor, EFS drip için zorunlu. UPDATE method: Q Dashboard UI'dan upgrade. Auth: appsettings.Production.json'dan oku."

## Interview Gates (6/6 onaylı)

1. **Tier:** plan_definitions matrix evaluate (4 row) → profesyonel Marketing key YOK → kurumsal zorunlu (Marketing+Appointments+Outbound basic+premium+Knowledge basic+premium+AgentAI on+premium).
2. **Method:** Q Dashboard UI'dan upgrade — Backend Ops PUT endpoint built-in `planCache.Invalidate(tenant)` tetiklendi (`Program.cs:6798`).
3. **Auth (smoke):** appsettings.Production.json Jwt:SecretKey + Issuer + Audience oku → prod sunucuda PowerShell HMAC-SHA256 JWT gen → curl POST localhost:7102.
4. **Smoke scope:** plan_guard PASS + booking 201 kanıtı yeterli; VCP meeting_link populate AYRI bug (doctors tablo schema drift), Paket A scope dışı.
5. **Cleanup:** smoke artifacts full purge (appointment row + hangfire job + state + queue + slot defensive FALSE revert) + pilot config preserve.
6. **VCP scope:** doctors tablo eksikliği yeni paket B-VCP-DOCTORS'a escalate.

## Pre-Smoke State (baseline)

| Item | Value |
|------|-------|
| `tenant_registry.plan_tier` | `baslangic` (default) |
| `tenant_registry.features_json` | NULL (tier'dan miras) |
| Plan tier matrix | baslangic / profesyonel / kurumsal / test_tier (4 row) |
| `appointment_slots` (Dent) | 4 slots, all `is_active=FALSE` (post-P9 wiring defensive) |
| Last S6 attempt | 2026-04-22 14:13 UTC → 403 INV-AUTH-005 |

## Plan Definitions Feature Matrix (MCP Query)

| Tier | Display | Appointments | Marketing | Outbound | Knowledge | AgentAI |
|------|---------|--------------|-----------|----------|-----------|---------|
| `baslangic` | Başlangıç | ❌ | ❌ | ❌ | ❌ | ❌ |
| `profesyonel` | Profesyonel | ✅ on | ❌ **YOK** | basic | basic | on |
| **`kurumsal`** | **Kurumsal** | ✅ on | ✅ **on** | basic+premium | basic+premium | on+premium |
| `test_tier` | Test Tier Updated | — | — | — | — | — (is_active=false) |

**Karar:** Dent FEAT-EFS P5/P10 SMOKED Marketing servisini kullanıyor → kurumsal zorunlu seçim.

## Execution Timeline

| Time (UTC) | Step | Result |
|------------|------|--------|
| 11:00 | Paket kickoff, Q'ya tier seçimi sunuldu | kurumsal seçildi |
| 11:05 | Q'ya UPDATE method tercihi sunuldu | Q Dashboard UI'dan upgrade tercih |
| 11:15:35 | **Q UI plan upgrade tamamlandı** | tenant_registry.plan_tier='kurumsal', updated_at=2026-04-24T11:15:35.072Z |
| 11:16 | MCP postgres verify | plan_tier='kurumsal' ✅, features_json=NULL (tier'dan miras) |
| 11:16:30 | MCP server-download Backend appsettings.Production.json | Jwt:SecretKey + Issuer='InvektoServis' + Audience='InvektoServis' alındı |
| 11:17:00 | MCP postgres execute UPDATE appointment_slots SET is_active=TRUE WHERE id=1 | 1 row, slot1 (Sat 09-13) temp activate |
| 11:17:46.967 | MCP server-exec PowerShell JWT gen + curl POST `/api/v1/appointments/book` | **STATUS 201, RESPONSE {id:1, status:'confirmed'}** ✅ |
| 11:17:46.988 | hangfire.job 38169 enqueue VideoMeetingCreationJob | queue='appointments' |
| 11:17:47.011 | hangfire.state 38169 Enqueued | EnqueuedAt populated |
| 11:17:47.031 | hangfire.state 38169 Processing | ServerId=win-6ltncjgrrc3:9632 |
| 11:17:47.126 | **hangfire.state 38169 Failed** | **`42P01: relation "doctors" does not exist`** AppointmentsRepository.cs:1210 |
| 11:18:27 | hangfire.state 38169 retry attempt 2 | Same Failed |
| 11:25 | MCP postgres execute cleanup batch | 0 appointments, 0 hf jobs/state/queue, slot1 is_active=FALSE, plan_tier='kurumsal' preserved |

## Smoke Verification (S6 Re-Smoke Evidence)

### Plan Guard (AC3 PASS)

```
POST localhost:7102/api/v1/appointments/book
Authorization: Bearer <JWT tenant=18173130 role=admin iss=InvektoServis aud=InvektoServis>
Content-Type: application/json
{"slot_id":1,"patient_name":"SMOKE_TEST_S6_141746","patient_phone":"+00000000099","appointment_date":"2026-04-25","notes":"pilot S6 re-smoke post-plan-upgrade"}

→ HTTP 201
{"id":1,"status":"confirmed"}
```

| Önceki (post-P9 wiring) | Şimdi (post-Paket A) |
|-------------------------|----------------------|
| 403 INV-AUTH-005 "Bu özellik mevcut planinizda bulunmuyor: Appointments" | **201 confirmed** |

### VCP Meeting Link Populate (AC4 — NEW BUG, Paket A scope DIŞI)

`hangfire.state` jobid=38169 trace:

```
112470 11:17:47.011 Enqueued    queue=appointments
112471 11:17:47.031 Processing  serverid=win-6ltncjgrrc3:9632
112472 11:17:47.126 Failed      Npgsql.PostgresException: 42P01: relation "doctors" does not exist
                                POSITION: 558 file=parse_relation.c routine=parserOpenTable
                                stack: AppointmentsRepository.GetAppointmentVideoRowAsync line 1210/1213
                                       → VideoMeetingCreationJob.RunAsync line 90
112473 11:17:47.145 Scheduled   Retry attempt 1 of 10
112480 11:18:26.989 Enqueued    Triggered by DelayedJobScheduler
112481 11:18:27.014 Processing
112482 11:18:27.033 Failed      same Npgsql exception
112483 11:18:27.039 Scheduled   Retry attempt 2 of 10
```

**Root cause:** `AppointmentsRepository.cs:1199-1202`:

```csharp
SELECT a.id, ..., s.doctor_id, d.name AS doctor_name
FROM appointments a
INNER JOIN appointment_slots s ON s.id = a.slot_id
LEFT JOIN doctors d ON d.id = s.doctor_id AND d.tenant_id = a.tenant_id
WHERE a.tenant_id = @tid AND a.id = @id
```

PostgreSQL parse-time tablo varlığı zorunlu (LEFT JOIN bile olsa). `doctors` tablosu hem prod DB'de hem `arch/db/migrations/` altında YOK.

**Latent reveal pattern:** Dent ilk gerçek Appointments slot booking yapan tenant — daha önce hiç tetiklenmemiş bir code path.

**Escalate:** Yeni paket `B-VCP-DOCTORS` BACKLOG'a (Migration 031 doctors tablo + LEFT JOIN guard veya doctors_view fallback + Appointments redeploy + VCP re-smoke).

### Cleanup Verification (AC5 PASS)

| Item | Pre-cleanup | Post-cleanup |
|------|-------------|--------------|
| `appointments WHERE tenant_id=18173130` | 1 (id=1 SMOKE_TEST_S6_141746) | **0** ✅ |
| `hangfire.job WHERE id=38169` | 1 | **0** ✅ |
| `hangfire.state WHERE jobid=38169` | 4 (Enqueued/Processing/Failed×2/Scheduled×2) | **0** ✅ |
| `hangfire.jobqueue WHERE jobid=38169` | 0 (already pulled) | **0** ✅ |
| `appointment_slots WHERE id=1 → is_active` | TRUE (temp activate) | **FALSE** ✅ (defensive default restored) |
| `tenant_registry.plan_tier WHERE tenant_id=18173130` | kurumsal (Q UI upgrade) | **kurumsal** ✅ (PRESERVED — Q'nun değişikliği) |

**Pilot config preserve baseline match:** plan_tier=kurumsal (UPGRADED, intentional) + 4 slots all is_active=FALSE (post-P9 wiring defensive default restored) + 1 active flow + 36 inactive FAQ + 1 landing row + 48 templates + 5 TFM + MCC seed (post-P9 wiring intact, untouched by Paket A).

## Discovered Bug → Escalation: B-VCP-DOCTORS

**Paket başlığı:** "VCP Meeting Hop — `doctors` Tablo Schema/Code Drift Fix"

**Scope (önerilen):**
1. Migration 031 doctors tablo: `id SERIAL PRIMARY KEY, tenant_id INT NOT NULL, name VARCHAR NOT NULL, is_active BOOLEAN DEFAULT TRUE, created_at TIMESTAMPTZ DEFAULT NOW(), updated_at TIMESTAMPTZ DEFAULT NOW()` + `idx_doctors_tenant_id` + `arch/db/appointments.sql` mirror.
2. `AppointmentsRepository.cs:1199-1202` SQL guard analysis: (a) tablo CREATE + boş bırak → LEFT JOIN d row return etmez ama parse PASS, doctor_id NULL durumunda doctor_name NULL — minimum impact; (b) `doctors_view` fallback view → schema isolation; (c) SQL refactor `doctor_id IS NOT NULL` precondition. Q + Codex tartışır.
3. Backend.csproj Appointments PrivateAssets="all" pattern verify (G7 SCHEDULER HOST EXCEPTION refleks — P10'da Marketing için yapılmıştı, Appointments zaten var ama leak check).
4. Re-smoke S6: meeting_link populate verify + video_reminder_24h/1h_job_id NOT NULL verify.

**Risk:** MEDIUM (migration + SQL guard + service deploy + re-smoke).

**Acceptance criteria:** S6 booking 201 + meeting_link non-null (Mock provider deterministic SHA256 link) + 2 reminder Hangfire job ID set.

## Acceptance Criteria

| AC | Criterion | Status | Note |
|----|-----------|--------|------|
| AC1 | tenant_registry.plan_tier upgrade verified | ✅ PASS | Q UI 11:15:35Z, MCP query confirm |
| AC2 | Tier matrix evaluate + gerekçe Q'ya sunuldu | ✅ PASS | Marketing key gap profesyonel'da, kurumsal zorunlu |
| AC3 | S6 plan_guard kanıtı (booking 201, AC6 flip) | ✅ PASS | 403 INV-AUTH-005 → 201 confirmed |
| AC4 | VCP meeting_link bug raporu + escalate | ✅ PASS (bug discovered + escalated) | doctors tablo eksik, B-VCP-DOCTORS açıldı |
| AC5 | Smoke artifacts cleanup + pilot config preserve | ✅ PASS | 0 residual, plan_tier=kurumsal preserved |
| AC6 | Repo update + Codex iter 0 PASS + commit | ⏳ PENDING | Plan + tracking + roadmap + session-memory + lessons draft |

## Codex Review Queue

- **Protocol:** v5.1 LOW risk (verification_questions optional; aha_moments 5 required)
- **MCP tool:** `mcp__codex-review__codex_review` (single reviewer, LOW risk)
- **Expected CQ focus:** CQ7 (doc clarity), CQ8 (plan/code drift) — bug raporu (AC4) plan scope dışına çıkarsa false-positive risk; allowed_files'a code yok, sadece doc.

## Post-Paket Follow-ups (scope dışı)

- **B-VCP-DOCTORS:** doctors tablo migration 031 + AppointmentsRepository SQL guard + Appointments redeploy + S6 re-smoke (meeting_link + 2 reminder ID).
- **Paket C:** 46 welcome + 36 FAQ `[EDIT:*]` placeholder → ROADSHOW DocX gerçek içerik bind + faq_entries is_active=TRUE flip + chatbot_flows.flow_config.nodes[].data.text güncelle + post-bind S4 AI FAQ translation hop smoke.
- **Pricing/Sales clarification:** "Pilot scope (Appointments + Drip + MCC) için kurumsal plan zorunlu" pricing page + sales playbook revisit (pre-pilot enterprise satış konuşmasında downgrade sürprizi engellemek için).

## Changelog

| Date (UTC) | Action | Commit |
|------------|--------|--------|
| 2026-04-24 11:00 | Paket kickoff + interview gates 6/6 | - |
| 2026-04-24 11:15:35 | Q Dashboard UI plan upgrade (kurumsal) | - |
| 2026-04-24 11:17:46 | S6 booking smoke 201 PASS | - |
| 2026-04-24 11:17:47 | VCP meeting_link populate FAIL (doctors table missing) — bug discovered | - |
| 2026-04-24 11:25 | Cleanup verified, plan A close as DONE+SMOKED_PARTIAL | - |
| TBD | Plan JSON + tracking + roadmap + session-memory + lessons commit | - |
| TBD | Codex iter 0 PASS + commit master | - |
