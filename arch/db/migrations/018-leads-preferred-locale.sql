-- Migration 018: leads.preferred_locale (HFM-2)
-- Date: 2026-04-17
-- Purpose: Per-lead language preference for FAQ multi-language fallback
--          (lead -> 'en' -> raw chain). Detected on first inbound message,
--          sticky after first successful upsert (COALESCE preserves existing).
-- Scope:   Q plan 20260417-human-feel-multilang-pilot.json AC-4
-- Safety:  Additive (nullable), idempotent. Existing rows stay NULL →
--          fallback chain kicks in with default 'en', translation service
--          gracefully degrades to raw answer if reachable.

ALTER TABLE leads ADD COLUMN IF NOT EXISTS preferred_locale VARCHAR(5);

-- Locale format: 'xx' or 'xx-YY' (ISO 639-1 + optional ISO 3166-1).
-- NULL allowed → not yet detected or opt-out of i18n.
ALTER TABLE leads
    DROP CONSTRAINT IF EXISTS chk_leads_preferred_locale;
ALTER TABLE leads
    ADD CONSTRAINT chk_leads_preferred_locale
    CHECK (preferred_locale IS NULL OR preferred_locale ~ '^[a-z]{2}(-[A-Z]{2})?$');

-- Partial index: only leads with a detected locale are in the hot path
-- for locale-aware FAQ lookup. Rows with NULL fall through fallback chain.
CREATE INDEX IF NOT EXISTS idx_leads_preferred_locale
    ON leads(tenant_id, preferred_locale)
    WHERE preferred_locale IS NOT NULL;
