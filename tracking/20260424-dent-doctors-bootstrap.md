# Paket B — B-VCP-DOCTORS: Doctors Table Bootstrap + Dent Pilot Seed

> **Slug:** `20260424-dent-doctors-bootstrap`
> **Plan:** [`arch/plans/20260424-dent-doctors-bootstrap.json`](../arch/plans/20260424-dent-doctors-bootstrap.json)
> **Migration:** [`arch/db/migrations/031-doctors-bootstrap.sql`](../arch/db/migrations/031-doctors-bootstrap.sql)
> **Canonical mirror:** [`arch/db/doctors.sql`](../arch/db/doctors.sql)
> **Risk:** MEDIUM | **Scope:** SQL schema bootstrap + Dent seed | **Build:** N/A | **Deploy:** Migration-only (no service redeploy)
> **Status:** IN_PROGRESS → target DONE+DEPLOYED+SMOKED
> **Kickoff:** 2026-04-24 14:45 UTC
> **Motivation:** Paket A S6 re-smoke sırasında `42P01 relation "doctors" does not exist` latent schema/code drift yüzeye çıktı. Dent ilk gerçek Appointments tenant'ı oldu; VideoMeetingCreationJob meeting_link populate hop FAIL. Paket C öncesi kapatılması zorunlu (pilot meeting_link populate hop kritik).

## Bug Context (Paket A'dan aktarıldı)

**Trigger:** 2026-04-24 11:17:46Z fresh booking POST /api/v1/appointments/book slot_id=1 tenant=18173130 → 201 confirmed appointment.id=1.

**Failure:**

```
hangfire.state jobid=38169
  11:17:47.011Z Enqueued    queue=appointments
  11:17:47.031Z Processing  serverid=win-6ltncjgrrc3:9632
  11:17:47.126Z Failed
    exception_type: Npgsql.PostgresException
    message: "42P01: relation \"doctors\" does not exist"
    POSITION: 558
    file: parse_relation.c
    routine: parserOpenTable
```

**Stack:**
- `AppointmentsRepository.GetAppointmentVideoRowAsync` line 1210/1213
- `VideoMeetingCreationJob.RunAsync` line 90

**SQL (src/Invekto.Appointments/Data/AppointmentsRepository.cs:1199-1202):**
```sql
SELECT ..., s.doctor_id, d.name AS doctor_name
FROM appointments a
INNER JOIN appointment_slots s ON s.id = a.slot_id
LEFT JOIN doctors d ON d.id = s.doctor_id AND d.tenant_id = a.tenant_id
WHERE a.tenant_id = @tid AND a.id = @id
```

**Root cause (Paket A bug report):**
- `doctors` tablosu hem prod DB'de hem `arch/db/migrations/` altında YOK.
- `arch/db/appointments.sql` satır 22/50/111-113/153 `doctor_id` için "Nullable (future GR-3.19)" comment — tablo reserve edildi ama hiç materialize edilmedi.
- PostgreSQL parse-time (parse_relation.c parserOpenTable) tablo varlığını `LEFT JOIN` için bile zorunlu tutar.
- Dent ilk gerçek Appointments tenant'ı — önceki tenant'lar Appointments feature'ı aktive etmediği için code path hiç tetiklenmemişti. **Latent first-tenant execution path reveal.**

**Retry observed:**
- Hangfire retry attempt 1 of 10 scheduled +60sec
- 2 cycles observed (38169 11:18:27Z second fail) before Paket A cleanup truncate
- meeting_link NULL + video_reminder_24h_job_id NULL + video_reminder_1h_job_id NULL kaldı

## Q Intent

"B-VCP-DOCTORS önce (Recommended). Migration 031 + SQL guard + redeploy + re-smoke. Recommended gerekçesi: pilot meeting_link populate hop kritik, Paket C onsuz da S4 smoke aynı blokeli."

## Interview Gates (9 gate)

| # | Gate | Q answer / Claude decision | Rationale |
|---|------|---------------------------|-----------|
| G1 | Schema scope | **Minimal boilerplate + no FK + UNIQUE (tenant_id, name)** (Q-approved AskUserQuestion 14:42 + Codex iter 0 CQ2 feedback 15:08) | FK appointment_slots.doctor_id → doctors.id legacy orphan data riski (diğer tenant slot'ları NULL olmayan değer içerebilir sweep yapmadan). GR-3.19 full domain post-pilot packet. **UNIQUE constraint** deterministic seed idempotency anchor — ON CONFLICT requires uq_doctors_tenant_name. |
| G2 | Dent seed | **1 placeholder 'Dr. Dent Adavista' + 4 slot backfill via ON CONFLICT DO NOTHING RETURNING** (Q-approved + Codex iter 0 CQ2 fix) | ICS'te doctor_name görünür, VideoMeetingCreationJob'da GetDoctorName NULL dönmez. Tek-doktor pilot varsayımı yeterli. Seed pattern deterministic via UNIQUE + ON CONFLICT (önceki IF NOT EXISTS / SELECT LIMIT 1 nondeterministic idi). |
| G3 | Code change | **YOK** (Claude decision) | LEFT JOIN natural NULL-safe. AppointmentsRepository.cs:1230-1231 `reader.IsDBNull(15/16)` zaten handle ediyor. |
| G4 | Appointments redeploy | **NSSM restart post-migration, pre-smoke ZORUNLU** (Claude decision, **revised 2026-04-24 15:10 per Codex iter 0 CQ8 + CoVe Q3 feedback**) | Önceki 'Npgsql per-connection plan cache transparent' hypothesis runtime deterministic safety için yeterli evidence sağlamıyor (prepared statement + connection pool state retention riski). Cold restart garantili Npgsql pool flush + fresh SQL parse. Ops cost düşük (~3sn + HEALTHY verify). |
| G5 | 4 slot doctor_id UPDATE | **Tümü aynı Dent placeholder doctor** (Claude decision) | Tek-doktor pilot varsayımı (G2 implicit). Multi-doctor senaryosu GR-3.19 scope. |
| G6 | Re-smoke methodology | **Paket A cleanup sonrası temiz state → migration execute → Appointments NSSM restart + HEALTHY verify → slot1 temp activate → fresh JWT gen → curl POST /book → hangfire state poll → meeting_link + reminder job IDs verify → cleanup** (Claude decision, revised per G4 iter 1 fix) | Paket A pattern'ını mirror + ek Appointments restart step; ek verify: hangfire state 'Succeeded' confirm + meeting_link non-null + video_reminder_24h/1h_job_id non-null. |
| G7 | Risk seviyesi | **MEDIUM** (Claude decision) | Prod schema ADDITIVE + data seed + Appointments restart + integration re-smoke. ADDITIVE-only (DROP/ALTER destructive yok), rollback simple (DROP TABLE doctors CASCADE + slot doctor_id NULL reset). |
| G8 | G7 SCHEDULER HOST EXCEPTION verify | **Grep snapshot kanıtı, yeni satır yok** (Claude decision, AC7 verified=true) | Backend.csproj line 9 `<ProjectReference Include="..\Invekto.Appointments\Invekto.Appointments.csproj" PrivateAssets="all" />` VAR (grep 2026-04-24 14:42). P10 lesson (commits 22dba6a/ac390e7/a6ed4ea) pattern intact. |
| G9 | Error codes | **INV-SEED-009..012 + actionable why+next-step RAISE EXCEPTION messages** (Claude decision + **Codex iter 0 CQ12 fix**) | Deployment-time postcondition assertions. Post-P9 wiring INV-SEED-001..008 precedent. No ErrorCodes.cs mirror (SEED namespace convention). **V11 relaxed (CQ9 fix):** 'Dent slots WHERE doctor_id IS NULL expected 0' generic invariant replaces hardcoded `=4` — removes tenant-specific mutable data coupling. |

## Schema Design (Bootstrap)

### doctors table

```sql
CREATE TABLE IF NOT EXISTS doctors (
    id          SERIAL PRIMARY KEY,
    tenant_id   INTEGER NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    name        VARCHAR(200) NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_doctors_tenant_name UNIQUE (tenant_id, name)
);

CREATE INDEX IF NOT EXISTS idx_doctors_tenant_active
    ON doctors (tenant_id)
    WHERE is_active = TRUE;

GRANT ALL ON doctors TO invekto;
GRANT USAGE, SELECT ON SEQUENCE doctors_id_seq TO invekto;
```

**UNIQUE constraint** `uq_doctors_tenant_name` = deterministic idempotency anchor (Codex iter 0 CQ2 fix). Seed ON CONFLICT target.

### Intentional exclusions (GR-3.19 scope, post-pilot)

| Exclusion | Why |
|-----------|-----|
| FK `appointment_slots.doctor_id → doctors.id` | Legacy orphan data — diğer tenant slot'ları doctor_id NULL olmayan değer içerebilir sweep yapmadan. GR-3.19 packet FK + orphan resolution sweep birlikte. |
| FK `appointments.doctor_id → doctors.id` | Same reasoning. |
| FK `waitlist.doctor_id → doctors.id` | Waitlist stub (appointments-v2.sql); GR-3.19 rework. |
| `specialty` VARCHAR | Domain richness postponed. |
| `license_no` VARCHAR | GR-3.19 compliance scope. |
| `email` / `phone` VARCHAR | Doctor contact domain, GR-3.19 scope. |
| `calendar_id` VARCHAR | Per-doctor calendar integration, GR-3.19. |
| `schedule_template_id` INTEGER | Per-doctor slot template, GR-3.19. |
| Multi-doctor seed for Dent | Pilot single-doctor varsayımı yeterli. |
| Diğer tenant'lara placeholder seed | Dent (tenant_id=18173130) pilot scope. |

## Dent Seed (Migration 031 DO block)

```sql
DO $$
DECLARE
    v_doctor_id  INTEGER;
    v_slot_count INTEGER;
BEGIN
    INSERT INTO doctors (tenant_id, name, is_active)
    VALUES (18173130, 'Dr. Dent Adavista', TRUE)
    ON CONFLICT (tenant_id, name) DO NOTHING
    RETURNING id INTO v_doctor_id;

    -- ON CONFLICT DO NOTHING suppresses RETURNING; fallback SELECT (UNIQUE
    -- constraint guarantees single row).
    IF v_doctor_id IS NULL THEN
        SELECT id INTO v_doctor_id
          FROM doctors
         WHERE tenant_id = 18173130
           AND name      = 'Dr. Dent Adavista';
    END IF;

    UPDATE appointment_slots SET doctor_id = v_doctor_id, updated_at = now()
    WHERE tenant_id = 18173130 AND doctor_id IS NULL;

    GET DIAGNOSTICS v_slot_count = ROW_COUNT;
    RAISE NOTICE '[B-VCP-DOCTORS] Dent doctor_id=% slots_backfilled=%', v_doctor_id, v_slot_count;
END $$;
```

**Deterministic idempotency (Codex iter 0 CQ2 fix):** `UNIQUE (tenant_id, name)` constraint + `INSERT ... ON CONFLICT DO NOTHING RETURNING` pattern. İlk run: INSERT succeeds, RETURNING id populate. Rerun: ON CONFLICT fires, RETURNING suppressed, fallback SELECT fetches the guaranteed single row. Concurrent-safe (PostgreSQL serializes conflicting INSERTs via UNIQUE anchor). `UPDATE ... WHERE doctor_id IS NULL` yalnızca NULL slot'ları backfill.

## Postcondition Verify (DO $verify$)

| Code | Check | Expected |
|------|-------|----------|
| INV-SEED-009 | `information_schema.tables` doctors var mı | `true` |
| INV-SEED-010 | Dent doctor count (tenant_id=18173130) | `1` (UNIQUE constraint guarantees ≤1; seed guarantees ≥1 → exactly 1) |
| INV-SEED-011 | **Dent slots WHERE doctor_id IS NULL** (generic invariant, Codex iter 0 CQ9 fix) | `0` (all slots backfilled) |
| INV-SEED-012 | GRANT INSERT privilege_type for invekto on doctors | `true` |

Her check `RAISE EXCEPTION '[INV-SEED-0xx] <msg>'` + actionable why + next-step guidance ile fail-loud (Codex iter 0 CQ12 fix). Örn:

```
[INV-SEED-011] Dent slots with doctor_id=NULL count=N expected 0
  (all slots should be backfilled to Dent placeholder doctor).
  Root cause: seed DO block UPDATE skipped OR post-seed manual slot
  INSERT without doctor_id. Next step: SELECT id, doctor_id FROM
  appointment_slots WHERE tenant_id=18173130 AND doctor_id IS NULL +
  manually UPDATE doctor_id to Dent seed id + re-run verify.
```

**V11 değişiklik rationale (Codex iter 0 CQ9 feedback):** Önceki hardcoded `dent_slots_with_doctor_id=4` koruması post-P9 wiring'in 4-slot baseline'ına kilitliydi. Post-P9 wiring slot count değişse (ör. Q pilot sırasında ek slot eklerse) migration fail edecekti. Generic "no NULL doctor_id remaining" invariant count'a dayanmaz; seed correctness garantisi bozulmuyor.

## Execution Timeline

| Time (UTC) | Step | Result |
|------------|------|--------|
| 14:45 | Paket B kickoff, Q tercihi recommended (B-VCP-DOCTORS önce) | Interview gate G1+G2 Q-approved AskUserQuestion |
| 14:50 | Schema design + plan JSON + migration + canonical mirror + tracking + errors.md + roadmap update | Dosyalar hazır (7 file diff estimate) |
| —:— | `/rev` MCP Codex review iter 0 | PENDING |
| —:— | Commit pre-deploy bundle (master) | PENDING |
| —:— | Migration 031 prod execute via MCP invekto-postgres | PENDING |
| —:— | S6 fresh re-smoke (slot1 temp activate + booking + hangfire + meeting_link verify) | PENDING |
| —:— | Smoke artifacts cleanup + pilot config preserve verify | PENDING |
| —:— | Final commit (session-memory DONE + lessons +1 + roadmap DONE+SMOKED) | PENDING |

## Acceptance Criteria

1. **AC1 (file scaffold):** Migration 031 + canonical mirror doctors.sql + UNIQUE constraint + arch/db/appointments.sql comment sync + plan JSON + tracking + errors.md +4 entry + roadmap update NEW files, schema match. **Status: PENDING**
2. **AC2 (seed deterministic idempotency):** Migration DO block INSERT ... ON CONFLICT (tenant_id, name) DO NOTHING RETURNING + fallback SELECT + `WHERE doctor_id IS NULL` rerun-safe; DO $verify$ 4 postcondition with V11 generic invariant + actionable RAISE EXCEPTION guidance. **Status: PENDING**
3. **AC3 (prod execute + Appointments restart):** MCP invekto-postgres execute sonrası `[B-VCP-DOCTORS] postcondition verify PASS (table=ok, dent_doctors=1, dent_slots_null=0, grant=ok)` RAISE NOTICE + **MCP invekto-ops server-exec `Restart-Service Invekto-Appointments`** + /health HEALTHY 200 verify. **Status: PENDING**
4. **AC4 (booking 201):** Fresh POST /api/v1/appointments/book → 201 confirmed (Plan guard PASS re-kanıtlandı). **Status: PENDING**
5. **AC5 (meeting populate):** VideoMeetingCreationJob hangfire.state Succeeded + meeting_link non-null + meeting_provider='mock' + calendar_event_id non-null + video_reminder_24h/1h_job_id non-null + 2 reminder Scheduled jobs DB'de. **Status: PENDING**
6. **AC6 (cleanup + preserve):** Appointment+hangfire artifacts purge + slot1 is_active=FALSE revert + doctor seed PRESERVE (pilot config kalıcı: plan_tier=kurumsal + 4 slots + 1 doctor + 1 flow + 36 FAQ + 1 landing + 48 templates + 5 TFM + MCC seed). **Status: PENDING**
7. **AC7 (G7 invariant):** Backend.csproj line 9 Appointments PrivateAssets="all" grep snapshot. **Status: PASS (2026-04-24 14:42 verified)**
8. **AC8 (Codex iter 1 PASS):** MEDIUM risk 12/12 CQ + CoVe PASS hedef post iter 0 FAIL (CQ2 seed nondeterministic, CQ8 no-redeploy unproven, CQ9 V11 hardcoded, CQ11 appointments.sql drift, CQ12 raw exceptions — all fixed in iter 1). **Status: PENDING**

## Rollback Plan

Eğer migration 031 DO $verify$ RAISE EXCEPTION ile fail olursa:
1. Transaction otomatik ROLLBACK (PL/pgSQL exception → full rollback).
2. Analiz: NOTICE çıktısından hangi check fail ettiğini belirle (V9/V10/V11/V12).
3. Fix: Migration SQL update + tekrar MCP execute.

Eğer migration PASS ama re-smoke FAIL olursa:
1. Smoke artifacts cleanup (appointment + hangfire).
2. Log analiz (Appointments service jsonl, hangfire.state exception).
3. Eğer doctors tablo ile ilgili yeni bir 42P01 veya integrity hatası → bug raporu + migration 032 follow-up.
4. Eğer VCP Mock provider ile ilgili hata (meeting_link generation) → ayrı packet (B-VCP-MOCK-FIX).

Eğer paket tamamen rollback gerektirirse (production stability):
```sql
BEGIN;
UPDATE appointment_slots SET doctor_id = NULL, updated_at = now()
WHERE tenant_id = 18173130;
DROP TABLE doctors CASCADE;
COMMIT;
```

## Deliverables

- `arch/plans/20260424-dent-doctors-bootstrap.json` — bu paketin plan JSON'u.
- `arch/db/migrations/031-doctors-bootstrap.sql` — prod migration (CREATE TABLE + UNIQUE constraint + seed ON CONFLICT DO NOTHING + verify DO with actionable exceptions).
- `arch/db/doctors.sql` — canonical mirror (schema source-of-truth).
- `arch/db/appointments.sql` — comment sync (doctors bootstrap reference, line 16/22/50/153 Usage Notes §4; Codex iter 0 CQ11 fix — canonical source-of-truth drift resolution).
- `arch/errors.md` — INV-SEED-009..012 +4 entry.
- `tracking/20260424-dent-doctors-bootstrap.md` — bu dosya (interview + schema + smoke + cleanup).
- `tracking/pilot-launch-roadmap.md` — Paket B row Master Queue + B-VCP-DOCTORS backlog DONE cross-link + Progress 12→13/13.
- `arch/session-memory.md` — Last Update Paket B kickoff + DONE+SMOKED flip (post-smoke).
- `arch/lessons-learned.md` — +1 entry post-smoke (migration-only fix pattern + first-tenant execution path reveal + Codex iter 0→1 fix arc).

## References

- **Paket A bug report:** [`arch/session-memory.md`](../arch/session-memory.md) Last Update 2026-04-24 11:25 UTC.
- **Paket A plan:** [`arch/plans/20260424-dent-plan-tier-upgrade.json`](../arch/plans/20260424-dent-plan-tier-upgrade.json) AC4 (doctors schema drift escalation).
- **AppointmentsRepository SQL:** [`src/Invekto.Appointments/Data/AppointmentsRepository.cs`](../src/Invekto.Appointments/Data/AppointmentsRepository.cs) line 1199-1202 LEFT JOIN + line 1230-1231 IsDBNull guards.
- **appointments canonical schema:** [`arch/db/appointments.sql`](../arch/db/appointments.sql) doctor_id columns (lines 22/50).
- **Backend.csproj G7 pattern:** [`src/Invekto.Backend/Invekto.Backend.csproj`](../src/Invekto.Backend/Invekto.Backend.csproj) line 9 Appointments PrivateAssets="all".
- **P10 G7 SCHEDULER HOST EXCEPTION lesson:** commits 22dba6a, ac390e7, a6ed4ea — Marketing eklenmesi + Backend.csproj ProjectReference pattern intact.
- **Post-P9 FlowBuilder wiring precedent:** INV-SEED-001..008 deployment-time postcondition namespace (arch/errors.md:1363-1386).
- **FEAT-EFS canonical mirror lesson:** arch/db/marketing.sql (Codex iter 2 CQ11) — schema drift guard precedent.
