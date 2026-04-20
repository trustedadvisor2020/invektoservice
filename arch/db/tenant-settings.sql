-- arch/db/tenant-settings.sql — CANONICAL schema for tenant_settings table.
-- FEAT-VCP Chunk A introduced this table for video_provider; future per-tenant toggles
-- land in this same table (single-row-per-tenant PK discipline).
--
-- Source migration: arch/db/migrations/023-tenant-settings-video-provider.sql
-- Any column additions land here first, then as an additive ALTER migration.

CREATE TABLE tenant_settings (
    tenant_id                 INTEGER     PRIMARY KEY
                                          REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    video_provider            VARCHAR(20) NULL
                                          CHECK (video_provider IS NULL
                                                 OR video_provider IN ('mock', 'googlemeet')),
    video_provider_config     JSONB       NULL,
    -- FEAT-VCP Chunk B (migration 024): per-tenant IANA timezone for ICS DTSTART TZID
    -- and WA meeting_start_local substitution. Default Europe/Istanbul for TR pilot.
    timezone                  VARCHAR(40) NOT NULL DEFAULT 'Europe/Istanbul',
    -- FEAT-J2 (migration 026): MessageCategory enforcement gate. FALSE keeps backward-
    -- compat (legacy flows with null event_name still send, INMA opt-out check skipped).
    -- TRUE rejects send_message without event_name (INV-OB-031). Pilot tenants only.
    enforce_message_category  BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Production role (see 2026-04-18 lesson: invekto, not invekto_app).
GRANT ALL ON tenant_settings TO invekto;
