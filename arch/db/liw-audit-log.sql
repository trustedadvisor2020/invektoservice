-- Canonical: liw_audit_log (FEAT-LIW Chunk C)
-- Run on: invekto database (PostgreSQL 16)
-- Depends on: tenant_registry
-- Source migration: arch/db/migrations/022-liw-audit-log.sql

-- Append-only mutation history for the Dashboard-driven LIW settings page.
-- Every rotate/revoke/fieldmap.save/welcome_slug.change emits one row inside
-- the SAME transaction as the underlying tenant_landing_settings UPDATE so
-- audit can never silently drift from the actual state (same-tx means an
-- audit-insert failure rolls back the main change; user retries; consistent).
-- Action vocabulary is enforced at application layer (LiwSettingsService),
-- not via DB CHECK, so rollback/schema evolution does not fight a CHECK clause.

CREATE TABLE IF NOT EXISTS liw_audit_log (
    id           BIGSERIAL PRIMARY KEY,
    tenant_id    INT         NOT NULL,
    user_id      INT,
    action       VARCHAR(50) NOT NULL,   -- apikey.rotate | apikey.revoke | fieldmap.save | welcome_slug.change
    before_json  JSONB,
    after_json   JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_liw_audit_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_liw_audit_log_tenant_created
    ON liw_audit_log(tenant_id, created_at DESC);

GRANT ALL ON liw_audit_log TO invekto;
GRANT USAGE, SELECT ON liw_audit_log_id_seq TO invekto;
