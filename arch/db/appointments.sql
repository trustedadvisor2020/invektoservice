-- =============================================================
-- Invekto.Appointments Database Schema
-- Service: Invekto.Appointments (port 7102)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-2.4: Randevu Motoru (Core)
-- KVKK: Sadece isim, telefon, randevu bilgisi saklanir.
--        Tibbi kayit, teshis, tedavi detayi SAKLANMAZ (veri minimizasyonu).
-- =============================================================

-- Depends on: tenant-registry.sql (tenant_registry table)

-- =============================================================
-- appointment_slots: Weekly recurring slot definitions
-- One slot = one bookable time window per day_of_week.
-- doctor_id nullable for future GR-3.19 (doktor bazli slot yonetimi).
-- =============================================================

CREATE TABLE IF NOT EXISTS appointment_slots (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    doctor_id           INTEGER,                           -- Nullable (future GR-3.19)
    day_of_week         SMALLINT NOT NULL,                 -- 0=Sunday, 1=Monday ... 6=Saturday
    start_time          TIME NOT NULL,
    end_time            TIME NOT NULL,
    max_bookings        INTEGER NOT NULL DEFAULT 1,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_slot_day_of_week CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT chk_slot_time_order CHECK (start_time < end_time),
    CONSTRAINT chk_slot_max_bookings CHECK (max_bookings >= 1)
);

-- Tenant + active day lookup (slot list for a tenant's given weekday)
CREATE INDEX IF NOT EXISTS idx_appointment_slots_tenant_day
    ON appointment_slots (tenant_id, day_of_week) WHERE is_active = TRUE;

-- =============================================================
-- appointments: Individual bookings
-- Each appointment references a slot and a specific date.
-- Status lifecycle: confirmed -> completed | cancelled | no_show
-- =============================================================

CREATE TABLE IF NOT EXISTS appointments (
    id                  BIGSERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    slot_id             INTEGER NOT NULL REFERENCES appointment_slots(id),
    doctor_id           INTEGER,                           -- Nullable (future GR-3.19)
    patient_name        VARCHAR(200) NOT NULL,
    patient_phone       VARCHAR(20) NOT NULL,
    appointment_date    DATE NOT NULL,
    start_time          TIME NOT NULL,
    end_time            TIME NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'confirmed',
    reminder_48h_sent   BOOLEAN NOT NULL DEFAULT FALSE,
    reminder_2h_sent    BOOLEAN NOT NULL DEFAULT FALSE,
    cancel_reason       VARCHAR(500),
    notes               VARCHAR(500),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Status lifecycle: confirmed -> completed | cancelled | no_show
    CONSTRAINT chk_appointment_status CHECK (status IN ('confirmed', 'cancelled', 'completed', 'no_show'))
);

-- Tenant + date range lookup (list appointments for a tenant on a date)
CREATE INDEX IF NOT EXISTS idx_appointments_tenant_date
    ON appointments (tenant_id, appointment_date, start_time);

-- Slot + date availability check (count confirmed for a given slot on a date)
CREATE INDEX IF NOT EXISTS idx_appointments_slot_date
    ON appointments (slot_id, appointment_date) WHERE status = 'confirmed';

-- Scheduler: pending 48h reminders (appointment_date in 2 days, not yet sent)
CREATE INDEX IF NOT EXISTS idx_appointments_reminder_48h
    ON appointments (appointment_date, reminder_48h_sent)
    WHERE status = 'confirmed' AND reminder_48h_sent = FALSE;

-- Scheduler: pending 2h reminders (today's appointments, not yet sent)
CREATE INDEX IF NOT EXISTS idx_appointments_reminder_2h
    ON appointments (appointment_date, start_time, reminder_2h_sent)
    WHERE status = 'confirmed' AND reminder_2h_sent = FALSE;

-- Patient phone lookup (for cancel/history by phone)
CREATE INDEX IF NOT EXISTS idx_appointments_patient_phone
    ON appointments (tenant_id, patient_phone);

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_appointment_slots_updated_at
    BEFORE UPDATE ON appointment_slots
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_appointments_updated_at
    BEFORE UPDATE ON appointments
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON appointment_slots TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON appointments TO invekto;
GRANT USAGE, SELECT ON SEQUENCE appointment_slots_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE appointments_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. appointment_slots: Weekly recurring definitions. One row per
--    (tenant_id, day_of_week, start_time, end_time) combination.
--    max_bookings controls how many appointments can overlap in that slot.
-- 2. appointments: Individual bookings tied to a slot + specific date.
--    Booking validates: slot is_active, date not in past, day_of_week match,
--    confirmed count < max_bookings.
-- 3. Reminders: ReminderSchedulerService checks reminder_48h_sent and
--    reminder_2h_sent flags every 5 minutes. POSTs to Outbound trigger API
--    with event='appointment_reminder'.
-- 4. doctor_id: Nullable in both tables. Reserved for GR-3.19 (doctor-based
--    slot management, specialist vs general). Currently always NULL.
-- 5. KVKK data minimization: Only name, phone, date/time stored.
--    No medical records, diagnosis, treatment details, or patient photos.
-- =============================================================
