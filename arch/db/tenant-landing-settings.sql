-- Canonical: tenant_landing_settings (FEAT-LIW Chunk A)
-- Run on: invekto database (PostgreSQL 16)
-- Depends on: tenant_registry
-- Source migration: arch/db/migrations/021-leads-intake-tenant-landing-settings.sql

-- Per-tenant Lead Intake Webhook (LIW) configuration:
--   - API key with 24h grace-period rotation (active + old + old_expires_at)
--   - landing_field_map: source field name -> Invekto canonical field map
--   - welcome_flow_slug: auto-trigger target (falls back to 'welcome_default')
--   - intake_dup_window_days: duplicate merge window (default 30, tenant override 1..365)

CREATE TABLE IF NOT EXISTS tenant_landing_settings (
    tenant_id                      INT PRIMARY KEY,
    landing_api_key                VARCHAR(64) NOT NULL,
    landing_api_key_old            VARCHAR(64),
    landing_api_key_old_expires_at TIMESTAMPTZ,
    landing_field_map              JSONB NOT NULL DEFAULT '{}'::jsonb,
    welcome_flow_slug              VARCHAR(100),
    intake_dup_window_days         INT NOT NULL DEFAULT 30,
    created_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_tls_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_tls_dup_window
        CHECK (intake_dup_window_days BETWEEN 1 AND 365)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_tls_api_key
    ON tenant_landing_settings(landing_api_key);

CREATE UNIQUE INDEX IF NOT EXISTS uq_tls_api_key_old
    ON tenant_landing_settings(landing_api_key_old)
    WHERE landing_api_key_old IS NOT NULL;

GRANT ALL ON tenant_landing_settings TO invekto_app;
