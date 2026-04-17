-- GR-1.9: Tenant Registry DDL for PostgreSQL
-- Database: invekto (per-environment: invekto_dev, invekto_staging, invekto_prod)
-- This table maps Main App tenant_id (SQL Server int) to InvektoServis config.

-- Create database (run once, manually)
-- CREATE DATABASE invekto_dev;

-- Tenant registry: stores per-tenant configuration for InvektoServis
CREATE TABLE IF NOT EXISTS tenant_registry (
    tenant_id           INTEGER PRIMARY KEY,         -- INSE internal id. Manual assign (legacy) OR nextval('tenant_registry_auto_id_seq') via lazy auto-provision (migration 016)
    tenant_name         VARCHAR(200) NOT NULL,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    callback_url        VARCHAR(500),                -- Override callback URL (null = use default)
    sector              VARCHAR(50),                 -- e.g., 'eticaret', 'dis_klinik', 'estetik', 'genel'
    plan_tier           VARCHAR(20) NOT NULL DEFAULT 'baslangic', -- 'baslangic', 'profesyonel', 'kurumsal' (Faz 1: migration 003)
    features_json       JSONB,                       -- Per-tenant feature overrides. NULL = plan defaults. Format: {"Feature": "mode"}
    settings_json       JSONB,                       -- Tenant-specific settings, e.g., {"working_hours": {...}}
    inma_code           VARCHAR(100),                -- Inma Company.Code (opaque string, migration 009). UNIQUE partial index (uq_tenant_registry_inma_code) enforces one INSE tenant per INMA company.
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for active tenants lookup
CREATE INDEX IF NOT EXISTS idx_tenant_registry_active ON tenant_registry (is_active) WHERE is_active = true;

-- Partial unique on inma_code (migration 009): supports ON CONFLICT DO NOTHING for lazy auto-provision.
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_registry_inma_code
    ON tenant_registry(inma_code) WHERE inma_code IS NOT NULL;

-- Auto-gen sequence for INMA lazy auto-provision (migration 016). START WITH 100_000_000
-- keeps ample head-room above the existing manual range (max ~40M elodi).
CREATE SEQUENCE IF NOT EXISTS tenant_registry_auto_id_seq
    START WITH 100000000
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
    OWNED BY tenant_registry.tenant_id;

-- Sequence nextval() grant for the invekto application role (migration 016).
GRANT USAGE, SELECT ON SEQUENCE tenant_registry_auto_id_seq TO invekto;

-- Example: Insert a test tenant (Q runs this manually for dev)
-- INSERT INTO tenant_registry (tenant_id, tenant_name, sector, plan_tier)
-- VALUES (1, 'Test Tenant', 'genel', 'basic');

-- Updated_at trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE TRIGGER trigger_tenant_registry_updated_at
    BEFORE UPDATE ON tenant_registry
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
