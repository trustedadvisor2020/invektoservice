-- Migration 031: B-VCP-DOCTORS — doctors table bootstrap for VideoMeetingCreationJob.
--
-- Context:
-- AppointmentsRepository.GetAppointmentVideoRowAsync (src/Invekto.Appointments/
-- Data/AppointmentsRepository.cs:1199-1202) SQL references a `doctors` table via
-- LEFT JOIN to project `doctor_name` onto AppointmentVideoRow. The table was
-- RESERVED for GR-3.19 (see arch/db/appointments.sql:22,50,111-113,153 — now
-- updated in the same packet to reflect this bootstrap) but was never
-- materialized. PostgreSQL parse-time resolution (parse_relation.c /
-- parserOpenTable) requires the table to exist even for LEFT JOIN.
--
-- Latent blast radius: Dent Adavista became first tenant to exercise the
-- Appointments booking → VideoMeetingCreationJob path during 2026-04-24 Paket A
-- S6 re-smoke. hangfire.state jobid=38169 logged:
--   Npgsql.PostgresException 42P01 "relation \"doctors\" does not exist"
--   POSITION: 558 file=parse_relation.c routine=parserOpenTable
-- → `meeting_link` + `video_reminder_24h_job_id` + `video_reminder_1h_job_id`
--   stayed NULL; retry loop blocked (2 cycles observed before cleanup truncate).
--
-- Scope (minimal bootstrap, interview gate G1 2026-04-24 Q-approved):
-- * CREATE TABLE doctors with minimal practitioner directory columns (id,
--   tenant_id, name, is_active, created_at, updated_at) + UNIQUE(tenant_id, name)
--   constraint for deterministic idempotency (Codex iter 0 CQ2 feedback —
--   seed ON CONFLICT path requires unique guarantee).
-- * NO foreign key from appointment_slots.doctor_id → doctors.id. Legacy data
--   integrity kept permissive; GR-3.19 full schema (specialty, license_no,
--   calendar_id, schedule_template_id) + FK addition deferred to post-pilot
--   follow-up packet.
-- * Partial index for tenant-scoped active-doctor lookup.
-- * Idempotent Dent seed (tenant_id=18173130): 1 row "Dr. Dent Adavista" via
--   INSERT ... ON CONFLICT (tenant_id, name) DO NOTHING RETURNING id pattern
--   (deterministic single-row guarantee; fallback SELECT when ON CONFLICT
--   suppresses RETURNING). Post-P9 wiring seeded 4 Dent slots (defensive
--   is_active=FALSE, doctor_id=NULL); this backfills all NULL slots.
-- * DO $verify$ postcondition block enforces 4 assertions (INV-SEED-009..012)
--   with actionable root-cause + next-step guidance in RAISE EXCEPTION text
--   (Codex iter 0 CQ12 feedback). V11 relaxed from hardcoded count=4 to
--   generic "WHERE doctor_id IS NULL expected 0" invariant (Codex iter 0 CQ9
--   feedback — remove tenant-specific mutable-data coupling).
--
-- Post-migration deploy step (Codex iter 0 CQ8 + CoVe Q3 feedback):
-- * Appointments service NSSM restart via `invekto-ops server-exec Restart-Service
--   Invekto-Appointments` — guarantees Npgsql connection pool flush +
--   prepared statement cache invalidation. Runtime safety no longer relies on
--   per-connection plan cache hypothesis; cold start forces fresh SQL parse.
-- * HEALTHY /health check post-restart before S6 re-smoke.
--
-- Related:
--   * Canonical mirror      : arch/db/doctors.sql (NEW)
--   * Appointments schema   : arch/db/appointments.sql (comment sync — doctors
--                             reference update lines 22,50,111-113,153)
--   * Plan                  : arch/plans/20260424-dent-doctors-bootstrap.json
--   * Tracking              : tracking/20260424-dent-doctors-bootstrap.md
--   * Error codes           : arch/errors.md INV-SEED-009..012
--   * Bug surface           : arch/session-memory.md 2026-04-24 11:25 UTC (Paket A close)
--                             + hangfire.state 38169 trace (already purged)
--   * Consumer pattern      : appointments.sql:22,50 doctor_id INTEGER NULL columns
--     + AppointmentsRepository.cs:1199-1202 LEFT JOIN + IsDBNull guard (line 1230-1231)

CREATE TABLE IF NOT EXISTS doctors (
    id          SERIAL PRIMARY KEY,
    tenant_id   INTEGER NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    name        VARCHAR(200) NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_doctors_tenant_name UNIQUE (tenant_id, name)
);

-- Tenant-scoped active-doctor lookup (GR-3.19 resolver forward-compat).
CREATE INDEX IF NOT EXISTS idx_doctors_tenant_active
    ON doctors (tenant_id)
    WHERE is_active = TRUE;

-- Prod role is `invekto` (lesson 2026-04-18, not `invekto_app`).
GRANT ALL ON doctors TO invekto;
GRANT USAGE, SELECT ON SEQUENCE doctors_id_seq TO invekto;

-- =============================================================================
-- Dent Adavista pilot seed.
-- Deterministic idempotency via UNIQUE(tenant_id, name) + ON CONFLICT DO NOTHING
-- (Codex iter 0 CQ2 feedback — previous IF NOT EXISTS / SELECT LIMIT 1 pattern
-- was nondeterministic under duplicate-row state).
-- Tek-doktor pilot varsayımı (interview gate G2 2026-04-24). Multi-doctor
-- senaryosu GR-3.19 scope'undadır — burada kapsanmaz.
-- =============================================================================
DO $$
DECLARE
    v_doctor_id  INTEGER;
    v_slot_count INTEGER;
BEGIN
    INSERT INTO doctors (tenant_id, name, is_active)
    VALUES (18173130, 'Dr. Dent Adavista', TRUE)
    ON CONFLICT (tenant_id, name) DO NOTHING
    RETURNING id INTO v_doctor_id;

    -- ON CONFLICT DO NOTHING suppresses RETURNING when conflict fires; fallback
    -- SELECT retrieves the existing row (guaranteed single row by UNIQUE constraint).
    IF v_doctor_id IS NULL THEN
        SELECT id INTO v_doctor_id
          FROM doctors
         WHERE tenant_id = 18173130
           AND name      = 'Dr. Dent Adavista';
    END IF;

    -- Backfill Dent slots (only those with doctor_id IS NULL — idempotent rerun safe).
    UPDATE appointment_slots
       SET doctor_id  = v_doctor_id,
           updated_at = now()
     WHERE tenant_id  = 18173130
       AND doctor_id IS NULL;

    GET DIAGNOSTICS v_slot_count = ROW_COUNT;

    RAISE NOTICE '[B-VCP-DOCTORS] Dent doctor_id=% slots_backfilled=%',
        v_doctor_id, v_slot_count;
END
$$;

-- =============================================================================
-- Postcondition verify block — fails the transaction loudly on any divergence.
-- Codes mirror arch/errors.md INV-SEED-009..012 (deployment-time assertions,
-- no ErrorCodes.cs mirror per SEED namespace convention from post-P9 wiring).
-- Exception messages include actionable why + next-step guidance (Codex iter 0
-- CQ12 feedback). V11 generic "no NULL doctor_id remaining" invariant replaces
-- previous fragile hardcoded count=4 (CQ9 feedback).
-- =============================================================================
DO $verify$
DECLARE
    v_table_exists     BOOLEAN;
    v_dent_doctors     INTEGER;
    v_dent_slots_null  INTEGER;
    v_grant_ok         BOOLEAN;
BEGIN
    -- V9: doctors table exists
    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'doctors'
    ) INTO v_table_exists;
    IF NOT v_table_exists THEN
        RAISE EXCEPTION '[INV-SEED-009] doctors table not created. '
            'Root cause: CREATE TABLE IF NOT EXISTS silently skipped (schema '
            'drift or transaction abort). Next step: verify information_schema.'
            'tables for public.doctors absence + re-run migration manually, '
            'inspect for preceding DDL failure in server log.';
    END IF;

    -- V10: Dent has exactly 1 doctor row post-seed (UNIQUE constraint guarantees
    -- at most 1; seed INSERT ... ON CONFLICT DO NOTHING guarantees at least 1
    -- when re-run). Divergence => data drift outside migration scope.
    SELECT COUNT(*) INTO v_dent_doctors
    FROM doctors WHERE tenant_id = 18173130;
    IF v_dent_doctors <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-010] Dent doctor count=% expected 1. '
            'Root cause: tenant_registry drift OR previous manual seed row with '
            'different name broke UNIQUE (tenant_id, name) assumption. Next step: '
            'SELECT id, name FROM doctors WHERE tenant_id=18173130 + reconcile '
            '(keep "Dr. Dent Adavista", remove others or rename) + re-run migration.',
            v_dent_doctors;
    END IF;

    -- V11: All Dent slots are backfilled (generic invariant — no tenant-specific
    -- slot-count coupling per Codex iter 0 CQ9 feedback). Post-P9 wiring seeds
    -- 4 slots; whether that count changes over time, the invariant "no NULL
    -- doctor_id for Dent post-seed" remains a valid schema health assertion.
    SELECT COUNT(*) INTO v_dent_slots_null
    FROM appointment_slots
    WHERE tenant_id = 18173130 AND doctor_id IS NULL;
    IF v_dent_slots_null > 0 THEN
        RAISE EXCEPTION '[INV-SEED-011] Dent slots with doctor_id=NULL count=% '
            'expected 0 (all slots should be backfilled to Dent placeholder doctor). '
            'Root cause: seed DO block UPDATE skipped OR post-seed manual slot '
            'INSERT without doctor_id. Next step: SELECT id, doctor_id FROM '
            'appointment_slots WHERE tenant_id=18173130 AND doctor_id IS NULL + '
            'manually UPDATE doctor_id to Dent seed id (SELECT id FROM doctors '
            'WHERE tenant_id=18173130 AND name=''Dr. Dent Adavista'') + re-run verify.',
            v_dent_slots_null;
    END IF;

    -- V12: GRANT ALL for invekto role present on doctors (prod role grant drift
    -- guard — lesson 2026-04-18 prod role is `invekto` not `invekto_app`).
    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name    = 'doctors'
          AND grantee       = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok;
    IF NOT v_grant_ok THEN
        RAISE EXCEPTION '[INV-SEED-012] GRANT ALL to invekto not applied on doctors. '
            'Root cause: GRANT statement silently skipped OR role rename drift. '
            'Next step: verify `SELECT rolname FROM pg_roles WHERE rolname=''invekto''` '
            'exists + manually re-run `GRANT ALL ON doctors TO invekto; '
            'GRANT USAGE, SELECT ON SEQUENCE doctors_id_seq TO invekto;` + '
            're-run verify.';
    END IF;

    RAISE NOTICE '[B-VCP-DOCTORS] postcondition verify PASS '
        '(table=ok, dent_doctors=1, dent_slots_null=0, grant=ok)';
END
$verify$;
