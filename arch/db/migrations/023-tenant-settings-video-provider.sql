-- Migration 023: tenant_settings generic per-tenant configuration table.
-- FEAT-VCP Chunk A: starts with video_provider (mock / googlemeet); designed as the
-- long-term home for other per-tenant toggles (notification_preferences,
-- feature_flags overrides, etc.). Single-row-per-tenant enforced by PK.
--
-- Idempotent: IF NOT EXISTS on the table, inline CHECK constraint (PG14+ safe rerun),
-- re-applicable GRANT. Canonical DDL mirror: arch/db/tenant-settings.sql.
--
-- Related: arch/features/video-consultation-provider.md (AC-4), lesson 2026-04-18
-- (GRANT role audit: role is 'invekto' in prod, not 'invekto_app').

CREATE TABLE IF NOT EXISTS tenant_settings (
    tenant_id              INTEGER     PRIMARY KEY
                                       REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    video_provider         VARCHAR(20) NULL
                                       CHECK (video_provider IS NULL
                                              OR video_provider IN ('mock', 'googlemeet')),
    video_provider_config  JSONB       NULL,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

GRANT ALL ON tenant_settings TO invekto;
