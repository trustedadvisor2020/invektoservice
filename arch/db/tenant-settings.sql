-- arch/db/tenant-settings.sql — CANONICAL schema for tenant_settings table.
-- FEAT-VCP Chunk A introduced this table for video_provider; future per-tenant toggles
-- land in this same table (single-row-per-tenant PK discipline).
--
-- Source migration: arch/db/migrations/023-tenant-settings-video-provider.sql
-- Any column additions land here first, then as an additive ALTER migration.

CREATE TABLE tenant_settings (
    tenant_id              INTEGER     PRIMARY KEY
                                       REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    video_provider         VARCHAR(20) NULL
                                       CHECK (video_provider IS NULL
                                              OR video_provider IN ('mock', 'googlemeet')),
    video_provider_config  JSONB       NULL,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Production role (see 2026-04-18 lesson: invekto, not invekto_app).
GRANT ALL ON tenant_settings TO invekto;
