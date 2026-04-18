-- Migration 022: FEAT-LIW (Lead Intake Webhook) Chunk C
-- Date: 2026-04-19
-- Purpose: (1) Create liw_audit_log table for Dashboard-driven LIW setting changes
--          (API key rotate/revoke, field_map save, welcome_slug change) so ops can
--          reconstruct "who changed what, when" without parsing server logs.
--          (2) Relax tenant_landing_settings.landing_api_key NOT NULL -> NULL so
--          the Dashboard's Revoke action can truly disable the channel (a sentinel
--          value would pollute uq_tls_api_key index + require sentinel-aware
--          lookup code; NULL is the cleanest "key not set" signal).
-- Related: arch/features/lead-intake-webhook.md AC-1..AC-10 (Chunk C scope)
--          arch/plans/20260419-liw-chunk-c-dashboard-ui.json (AC1, AC5, AC6)
-- Canonical: arch/db/liw-audit-log.sql (new canonical for audit table)
--            arch/db/tenant-landing-settings.sql (nullable relaxation noted in header)
-- Idempotent: CREATE TABLE/INDEX IF NOT EXISTS + PL/pgSQL DO block gates the ALTER
--             on pg_attribute.attnotnull so reruns are safe.

-- ============================================================
-- 1. liw_audit_log (FEAT-LIW dashboard mutation history)
-- ============================================================

CREATE TABLE IF NOT EXISTS liw_audit_log (
    id           BIGSERIAL PRIMARY KEY,
    tenant_id    INT         NOT NULL,
    user_id      INT,
    -- Controlled vocabulary enforced at application layer (LiwSettingsService):
    -- 'apikey.rotate' | 'apikey.revoke' | 'fieldmap.save' | 'welcome_slug.change'
    -- DB-side check kept simple (length cap only) so rollback does not fight a CHECK clause.
    action       VARCHAR(50) NOT NULL,
    -- Snapshot of relevant settings BEFORE the change (nullable for 'create' actions
    -- where nothing existed prior, e.g. first-time apikey.rotate on a fresh tenant).
    before_json  JSONB,
    -- Snapshot of relevant settings AFTER the change (nullable for revoke audit where
    -- the "after" is conceptually "nothing"; UI renders that as Iptal edildi).
    after_json   JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_liw_audit_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

-- Timeline fetch is always "WHERE tenant_id = X ORDER BY created_at DESC LIMIT N";
-- composite index covers the full predicate + sort.
CREATE INDEX IF NOT EXISTS idx_liw_audit_log_tenant_created
    ON liw_audit_log(tenant_id, created_at DESC);

-- Prod role (per lesson 2026-04-18: canonical template says invekto_app but prod
-- service runs as invekto; grant to actual runtime role).
GRANT ALL ON liw_audit_log TO invekto;
GRANT USAGE, SELECT ON liw_audit_log_id_seq TO invekto;

-- ============================================================
-- 2. tenant_landing_settings: relax landing_api_key NOT NULL -> NULL
--    Enables Revoke semantics where the tenant-facing key is cleared but the row
--    is preserved (field_map, welcome_slug, dup_window remain). FindByApiKeyAsync
--    gains an IS NOT NULL guard so a NULL key never matches any inbound.
-- ============================================================

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relname = 'tenant_landing_settings'
          AND n.nspname = 'public'
          AND a.attname = 'landing_api_key'
          AND a.attnotnull = true
    ) THEN
        ALTER TABLE tenant_landing_settings
            ALTER COLUMN landing_api_key DROP NOT NULL;
    END IF;
END$$;
