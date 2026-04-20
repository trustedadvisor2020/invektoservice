-- Migration 024: FEAT-VCP Chunk B — Appointments video meeting persistence
-- + tenant_settings.timezone + outbound_templates.trigger_event expansion.
--
-- Idempotent: ADD COLUMN IF NOT EXISTS, DROP CONSTRAINT IF EXISTS before re-ADD,
-- partial indexes with CREATE INDEX IF NOT EXISTS. Safe to re-run on partially
-- migrated tenants.
--
-- Canonical schema mirrors: arch/db/appointments.sql, arch/db/tenant-settings.sql,
-- arch/db/outbound.sql (column-for-column + constraint parity).
--
-- Related: arch/features/video-consultation-provider.md AC-1/AC-4, Chunk B plan
-- 20260419-feat-vcp-chunk-b-appointments-reminders.json AC-1.

BEGIN;

-- 1. appointments: 7 new columns for video meeting persistence + reminder tracking.
ALTER TABLE appointments
    ADD COLUMN IF NOT EXISTS meeting_link              TEXT NULL,
    ADD COLUMN IF NOT EXISTS meeting_provider          VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS calendar_event_id         TEXT NULL,
    ADD COLUMN IF NOT EXISTS video_reminder_24h_job_id TEXT NULL,
    ADD COLUMN IF NOT EXISTS video_reminder_1h_job_id  TEXT NULL,
    ADD COLUMN IF NOT EXISTS video_reminder_24h_sent_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS video_reminder_1h_sent_at  TIMESTAMPTZ NULL;

-- Guard meeting_provider values (mirrors tenant_settings.video_provider enum).
ALTER TABLE appointments DROP CONSTRAINT IF EXISTS chk_appointment_meeting_provider;
ALTER TABLE appointments ADD CONSTRAINT chk_appointment_meeting_provider
    CHECK (meeting_provider IS NULL OR meeting_provider IN ('mock', 'googlemeet'));

-- 2. tenant_settings: per-tenant timezone (Europe/Istanbul default for TR pilot).
ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS timezone VARCHAR(40) NOT NULL DEFAULT 'Europe/Istanbul';

-- 3. outbound_templates: expand trigger_event CHECK for 3 new video templates.
ALTER TABLE outbound_templates DROP CONSTRAINT IF EXISTS chk_trigger_event;
ALTER TABLE outbound_templates ADD CONSTRAINT chk_trigger_event
    CHECK (trigger_event IN (
        'manual',
        'new_lead',
        'payment_received',
        'appointment_reminder',
        'video_meeting_confirmed',
        'video_reminder_24h',
        'video_reminder_1h'
    ));

-- 4. Partial indexes: aid ops queries for pending video reminders (Hangfire
--    is the primary scheduler; these support troubleshooting and audit scans).
CREATE INDEX IF NOT EXISTS idx_appointments_video_reminder_24h_pending
    ON appointments (appointment_date, start_time)
    WHERE status = 'confirmed'
      AND meeting_link IS NOT NULL
      AND video_reminder_24h_sent_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_appointments_video_reminder_1h_pending
    ON appointments (appointment_date, start_time)
    WHERE status = 'confirmed'
      AND meeting_link IS NOT NULL
      AND video_reminder_1h_sent_at IS NULL;

-- 5. Re-assert role grants. ALTER TABLE / ADD COLUMN does not change column-level
--    privileges, but we re-run GRANT ALL here so (a) the migration is self-contained
--    proof that Chunk B's new columns are reachable by the production 'invekto' role
--    (lesson 2026-04-18: role audit — role is 'invekto', not 'invekto_app') and
--    (b) any operator re-running this script against a tenant with a partially
--    drifted permission state gets a clean recovery path.
GRANT ALL ON appointments       TO invekto;
GRANT ALL ON tenant_settings    TO invekto;
GRANT ALL ON outbound_templates TO invekto;

COMMIT;
