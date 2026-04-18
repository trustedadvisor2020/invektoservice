-- Migration 021: FEAT-LIW (Lead Intake Webhook) Chunk A
-- Date: 2026-04-18
-- Purpose: Tenant-scoped landing webhook config (per-tenant API key with 24h grace
--          rotation, field map JSONB, welcome flow slug, dup window override),
--          and extend leads with intake_metadata JSONB + source_slug for per-
--          submission attribution.
-- Related: arch/features/lead-intake-webhook.md AC-1..AC-10
-- Canonical: arch/db/tenant-landing-settings.sql (new canonical for tls table)
--            arch/db/pkt6b1-niche-business.sql (leads ALTER mirrored inline)

-- ============================================================
-- 1. tenant_landing_settings (per-tenant LIW config + API key rotation)
-- ============================================================

CREATE TABLE IF NOT EXISTS tenant_landing_settings (
    tenant_id                      INT PRIMARY KEY,
    -- Active API key (clients send via X-Invekto-Api-Key header).
    landing_api_key                VARCHAR(64) NOT NULL,
    -- Previous key during 24h grace window (rotation support).
    landing_api_key_old            VARCHAR(64),
    -- Old-key expiry wall-clock. If NULL or <= NOW(), old key is inactive.
    landing_api_key_old_expires_at TIMESTAMPTZ,
    -- Maps tenant's source field names to Invekto canonical fields.
    -- Shape: { "<canonical>": "<source_field>", "phone.country_hint": "IE" }
    landing_field_map              JSONB NOT NULL DEFAULT '{}'::jsonb,
    -- Welcome flow slug to enqueue on fresh lead (falls back to 'welcome_default').
    welcome_flow_slug              VARCHAR(100),
    -- Duplicate merge window; lead re-submission within N days merges metadata,
    -- outside the window a fresh welcome trigger fires.
    intake_dup_window_days         INT NOT NULL DEFAULT 30,
    created_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_tls_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_tls_dup_window
        CHECK (intake_dup_window_days BETWEEN 1 AND 365)
);

-- Active key lookup: O(1) by landing_api_key (globally unique across tenants).
CREATE UNIQUE INDEX IF NOT EXISTS uq_tls_api_key
    ON tenant_landing_settings(landing_api_key);

-- Old key lookup: partial unique so multiple NULLs coexist; non-null must be unique.
CREATE UNIQUE INDEX IF NOT EXISTS uq_tls_api_key_old
    ON tenant_landing_settings(landing_api_key_old)
    WHERE landing_api_key_old IS NOT NULL;

GRANT ALL ON tenant_landing_settings TO invekto_app;

-- ============================================================
-- 2. leads: per-submission attribution metadata + latest source_slug
--    intake_metadata accumulates submissions via JSONB `||` concat; each new
--    top-level key (submission_<iso8601>) records { source_slug, submitted_at,
--    referer, utm, resolved } for that submission. Raw source_fields and
--    ip_hash are intentionally NOT stored (privacy + scope — TFM Sprint C will
--    promote mapped customs to dedicated columns).
--    source_slug is overwritten on each UPSERT (latest intake wins).
-- ============================================================

ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS intake_metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS source_slug VARCHAR(50);
