-- =============================================================
-- Invekto.Outbound v2 Migration (PKT-1: GR-2.3 Multi-lang)
-- Run AFTER outbound.sql base schema
-- =============================================================

-- GR-2.3: Add language column to templates
ALTER TABLE outbound_templates ADD COLUMN IF NOT EXISTS lang VARCHAR(10) NOT NULL DEFAULT 'tr';

-- GR-2.3: Add language to broadcasts (requested language for the broadcast)
ALTER TABLE outbound_broadcasts ADD COLUMN IF NOT EXISTS lang VARCHAR(10);

-- GR-2.3: Add language to individual messages (actual language sent)
ALTER TABLE outbound_messages ADD COLUMN IF NOT EXISTS lang VARCHAR(10);

-- Update trigger_event CHECK to include new events (add if not exists pattern)
-- Note: existing CHECK constraint allows: manual, new_lead, payment_received, appointment_reminder
-- No new events needed for multi-lang, lang is orthogonal to event type

-- Index for language-filtered template lookups
CREATE INDEX IF NOT EXISTS idx_outbound_templates_tenant_lang
    ON outbound_templates (tenant_id, lang, is_active) WHERE is_active = TRUE;

-- Grant permissions (idempotent)
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_templates TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_broadcasts TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_messages TO invekto;
