-- Migration 015: Rename gunes_event/gunes_lead_id -> zoho_event/source_lead_id (Adim 3 Paket 3A)
-- Purpose: Drop customer-specific vocabulary from Zoho integration. Zoho is a generic platform
--          integration usable by all tenants; 'gunes' references must not leak into column names.
-- Reference: arch/plans/20260415-zoho-step3-p3a-rename.json
-- Preconditions: migrations 013 + 014 already applied. This migration MUST ship in the same deploy
--                window as 013 + 014 (P1+P2 not yet in production).

BEGIN;

-- zoho_stage_mappings: rename column. The UNIQUE(tenant_id, gunes_event) constraint and the
-- CHECK on gunes_event are automatically carried over by Postgres to the new column name
-- (ALTER COLUMN RENAME updates index expressions and constraint definitions in place).
ALTER TABLE zoho_stage_mappings RENAME COLUMN gunes_event TO zoho_event;
COMMENT ON COLUMN zoho_stage_mappings.zoho_event IS 'Lifecycle event identifier (welcome_sent / engaged / qualified / offer_sent / closed_won / deposit_paid / closed_lost).';

-- zoho_sync_log: rename both columns. The partial unique index ux_zoho_sync_log_open_attempt
-- (expression-based on tenant_id + gunes_event + gunes_lead_id) is automatically rewritten
-- to reference the new column names by ALTER COLUMN RENAME — its index NAME is intentionally
-- preserved because its semantic ("one open attempt per row") does not depend on the renamed
-- columns. Only idx_zoho_sync_log_lead's name references the column ("_lead") and is renamed
-- to match the new source_lead_id column.
ALTER TABLE zoho_sync_log RENAME COLUMN gunes_event    TO zoho_event;
ALTER TABLE zoho_sync_log RENAME COLUMN gunes_lead_id  TO source_lead_id;

ALTER INDEX IF EXISTS idx_zoho_sync_log_lead RENAME TO idx_zoho_sync_log_source_lead;

COMMENT ON COLUMN zoho_sync_log.zoho_event     IS 'Lifecycle event identifier synced to Zoho (generic, not customer-specific).';
COMMENT ON COLUMN zoho_sync_log.source_lead_id IS 'Source-side (Invekto) lead identifier used as idempotency key with (tenant_id, zoho_event).';
COMMENT ON TABLE  zoho_sync_log IS 'Adim 3 P1 + P3A: Every source -> Zoho sync attempt. Retry source of truth.';

COMMIT;
