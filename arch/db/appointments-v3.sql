-- =============================================================
-- Invekto.Appointments v3 Database Schema (Treatment Lifecycle)
-- Service: Invekto.Appointments (port 7102)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-3.20: Tedavi Sonrasi Takip
-- GR-3.41: Tedavi Plani Onay Akisi
-- GR-3.43: Tedavi Oncesi Hazirlik Talimatlari
-- Depends on: tenant-registry.sql, appointments.sql
-- =============================================================

-- =============================================================
-- treatment_followups: Lifecycle instances
-- Each row = one active follow-up chain for a patient.
-- lifecycle_type determines the step schedule.
-- Status lifecycle: active -> completed | cancelled | escalated
-- =============================================================

CREATE TABLE IF NOT EXISTS treatment_followups (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    lifecycle_type      VARCHAR(30) NOT NULL,           -- 'post_treatment', 'plan_approval', 'pre_op'
    patient_phone       VARCHAR(20) NOT NULL,
    patient_name        VARCHAR(200) NOT NULL,
    treatment_type      VARCHAR(100),                   -- e.g. 'implant', 'botox', 'dis_beyazlatma'
    appointment_id      BIGINT,                         -- nullable FK to appointments table
    reference_date      TIMESTAMPTZ NOT NULL,           -- event date (treatment/plan-sent/appointment)
    status              VARCHAR(20) NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_followup_lifecycle_type CHECK (lifecycle_type IN ('post_treatment', 'plan_approval', 'pre_op')),
    CONSTRAINT chk_followup_status CHECK (status IN ('active', 'completed', 'cancelled', 'escalated'))
);

-- Tenant + active lifecycle lookup
CREATE INDEX IF NOT EXISTS idx_followups_tenant_status
    ON treatment_followups (tenant_id, status) WHERE status = 'active';

-- Tenant + type filter
CREATE INDEX IF NOT EXISTS idx_followups_tenant_type
    ON treatment_followups (tenant_id, lifecycle_type);

-- Patient phone lookup (history)
CREATE INDEX IF NOT EXISTS idx_followups_tenant_phone
    ON treatment_followups (tenant_id, patient_phone);

-- =============================================================
-- treatment_followup_steps: Scheduled steps within a lifecycle
-- Each row = one scheduled message in the follow-up chain.
-- Scheduler queries: sent_at IS NULL AND scheduled_at <= NOW()
-- =============================================================

CREATE TABLE IF NOT EXISTS treatment_followup_steps (
    id                  SERIAL PRIMARY KEY,
    followup_id         INTEGER NOT NULL REFERENCES treatment_followups(id) ON DELETE CASCADE,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    step_order          SMALLINT NOT NULL,               -- 1-based step index
    step_key            VARCHAR(50) NOT NULL,            -- 'check_in', 'control_questions', 'booking_offer', etc.
    message_template    TEXT NOT NULL,
    scheduled_at        TIMESTAMPTZ NOT NULL,
    sent_at             TIMESTAMPTZ,
    patient_responded   BOOLEAN NOT NULL DEFAULT FALSE,
    response_text       TEXT,
    complaint_detected  BOOLEAN NOT NULL DEFAULT FALSE,
    escalated           BOOLEAN NOT NULL DEFAULT FALSE,
    escalation_target   VARCHAR(50),                     -- 'doctor', 'supervisor', null
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_step_order CHECK (step_order >= 1)
);

-- Scheduler: pending steps (due and not yet sent, parent lifecycle active)
CREATE INDEX IF NOT EXISTS idx_followup_steps_pending
    ON treatment_followup_steps (scheduled_at, sent_at)
    WHERE sent_at IS NULL;

-- Followup lookup (all steps for a lifecycle)
CREATE INDEX IF NOT EXISTS idx_followup_steps_followup
    ON treatment_followup_steps (followup_id, step_order);

-- Tenant isolation on steps
CREATE INDEX IF NOT EXISTS idx_followup_steps_tenant
    ON treatment_followup_steps (tenant_id);

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_treatment_followups_updated_at
    BEFORE UPDATE ON treatment_followups
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON treatment_followups TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON treatment_followup_steps TO invekto;
GRANT USAGE, SELECT ON SEQUENCE treatment_followups_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE treatment_followup_steps_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. treatment_followups: Created via POST /api/v1/lifecycle/start.
--    lifecycle_type determines step schedule (post_treatment, plan_approval, pre_op).
--    reference_date = treatment completion date / plan sent date / appointment date.
-- 2. treatment_followup_steps: Created in batch when lifecycle starts.
--    scheduled_at = reference_date +/- offset_hours from LifecycleStepDefinitions.
--    TreatmentLifecycleService processes due steps every 5 minutes.
-- 3. Step processing: send message via Outbound trigger API, mark sent_at.
--    If patient responds, update patient_responded + response_text.
--    If complaint detected, set complaint_detected + escalate to doctor.
-- 4. Lifecycle completion: all steps sent + final step deadline passed.
--    If no response after final step, escalate (plan_approval -> supervisor).
-- =============================================================
