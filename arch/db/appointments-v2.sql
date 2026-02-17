-- =============================================================
-- Invekto.Appointments v2 Database Schema (Advanced Features)
-- Service: Invekto.Appointments (port 7102)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-3.19: Randevu Motoru v2 Advanced (PKT-5B)
-- Depends on: appointments.sql (appointment_slots, appointments tables)
-- =============================================================

-- Depends on: tenant-registry.sql, appointments.sql

-- =============================================================
-- waitlist: Patients waiting for a cancelled slot
-- When an appointment is cancelled, WaitlistService checks
-- for matching waitlist entries and notifies via Outbound.
-- =============================================================

CREATE TABLE IF NOT EXISTS waitlist (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    patient_phone       VARCHAR(20) NOT NULL,
    patient_name        VARCHAR(200) NOT NULL,
    preferred_date      DATE,                               -- NULL = any date
    preferred_time      TIME,                               -- NULL = any time
    service_type        VARCHAR(100),                       -- treatment/service requested
    doctor_id           INTEGER,                            -- NULL = any doctor
    status              VARCHAR(20) NOT NULL DEFAULT 'waiting', -- waiting, notified, booked, expired, cancelled
    notified_at         TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ,                        -- auto-expire after N days
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_waitlist_status CHECK (status IN ('waiting', 'notified', 'booked', 'expired', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_waitlist_tenant_status
    ON waitlist (tenant_id, status) WHERE status = 'waiting';

CREATE INDEX IF NOT EXISTS idx_waitlist_tenant_phone
    ON waitlist (tenant_id, patient_phone);

CREATE INDEX IF NOT EXISTS idx_waitlist_tenant_doctor
    ON waitlist (tenant_id, doctor_id) WHERE doctor_id IS NOT NULL AND status = 'waiting';

-- =============================================================
-- service_pricing: Treatment/service price ranges
-- Used in booking flow to show estimated costs.
-- =============================================================

CREATE TABLE IF NOT EXISTS service_pricing (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    service_name        VARCHAR(200) NOT NULL,
    price_min           DECIMAL(12,2) NOT NULL,
    price_max           DECIMAL(12,2) NOT NULL,
    currency            VARCHAR(3) NOT NULL DEFAULT 'TRY',
    duration_minutes    INTEGER,                            -- estimated service duration
    description         TEXT,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_pricing_range CHECK (price_min <= price_max),
    CONSTRAINT chk_pricing_min CHECK (price_min >= 0),
    CONSTRAINT chk_pricing_duration CHECK (duration_minutes IS NULL OR duration_minutes > 0)
);

CREATE INDEX IF NOT EXISTS idx_pricing_tenant_active
    ON service_pricing (tenant_id) WHERE is_active = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS idx_pricing_tenant_service
    ON service_pricing (tenant_id, service_name) WHERE is_active = TRUE;

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_waitlist_updated_at
    BEFORE UPDATE ON waitlist
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_service_pricing_updated_at
    BEFORE UPDATE ON service_pricing
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON waitlist TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON service_pricing TO invekto;
GRANT USAGE, SELECT ON SEQUENCE waitlist_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE service_pricing_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. waitlist: Created when patient can't get desired slot.
--    When appointment cancelled -> WaitlistService finds matching
--    waitlist entries (same tenant, status=waiting, matching date/doctor)
--    -> sends Outbound notification -> status=notified.
--    Patient books -> status=booked. No response -> expires_at reached.
-- 2. service_pricing: CRUD from Dashboard. Active unique constraint
--    per (tenant_id, service_name) to prevent duplicates.
--    Soft-deactivate via is_active=false.
-- 3. No-show tracking uses existing appointments table (status='no_show').
--    Query: COUNT(*) WHERE patient_phone=X AND status='no_show' AND tenant_id=Y.
--    Threshold from tenant_registry.settings_json: "no_show_threshold" (default 2).
-- 4. Doctor-based slot filtering uses existing appointment_slots.doctor_id column.
--    available-slots?doctor_id=X filters slots by doctor.
-- =============================================================
