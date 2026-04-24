-- doctors — Minimal practitioner directory for per-tenant appointment routing.
--
-- Source-of-truth canonical mirror for arch/db/migrations/031-doctors-bootstrap.sql.
-- Keep in sync on every structural change (schema drift guard, lesson 2026-04-21
-- FEAT-EFS Codex iter 2 CQ11 — arch/db/marketing.sql canonical mirror precedent).
--
-- History:
-- * 2026-04-24 Packet B-VCP-DOCTORS (migration 031): minimal bootstrap created
--   to unblock Dent Adavista first-tenant VideoMeetingCreationJob execution
--   path (42P01 "relation doctors does not exist" surfaced during Paket A
--   S6 re-smoke). Previously RESERVED in arch/db/appointments.sql comments
--   (lines 22,50,111-113,153) for GR-3.19 but never materialized; those
--   comments were synced in the same packet to reflect migration 031 bootstrap.
--
-- Bootstrap schema scope (2026-04-24, Q interview G1 approved):
--   id SERIAL PK, tenant_id INT NOT NULL REFERENCES tenant_registry ON DELETE CASCADE,
--   name VARCHAR(200) NOT NULL, is_active BOOL DEFAULT TRUE,
--   created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ,
--   CONSTRAINT uq_doctors_tenant_name UNIQUE (tenant_id, name) — deterministic
--     idempotency guarantee for seed (Codex iter 0 CQ2 feedback; ON CONFLICT
--     requires unique anchor).
--
-- Intentional exclusions (deferred to post-pilot GR-3.19 full packet):
--   * NO FK from appointment_slots.doctor_id → doctors.id. Legacy data
--     tolerance; enforcing the FK now would fail on orphan rows without a
--     backfill sweep across all tenants. GR-3.19 packet adds FK + orphan
--     resolution sweep together.
--   * NO specialty / license_no / email / phone / calendar_id /
--     schedule_template_id columns. Dent pilot uses one placeholder doctor
--     row; domain richness postponed.
--   * NO waitlist.doctor_id FK update. Same reasoning (waitlist is
--     currently stub per appointments-v2.sql; GR-3.19 reworks it).
--
-- Partial index idx_doctors_tenant_active for O(log N) tenant-scoped active
-- doctor lookup (forward-compat with GR-3.19 slot resolver).
--
-- Consumers (as of 2026-04-24):
--   * AppointmentsRepository.GetAppointmentVideoRowAsync — LEFT JOIN doctors d
--     ON d.id = s.doctor_id AND d.tenant_id = a.tenant_id for doctor_name
--     projection onto AppointmentVideoRow (VideoMeetingCreationJob +
--     VideoReminderJob consumption path).
--   * (future GR-3.19) appointment_slots.doctor_id → doctors.id resolver +
--     waitlist matcher + per-doctor schedule templates.

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
