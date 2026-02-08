-- GR-1.9: Tenant Registry DDL for PostgreSQL
-- Database: invekto (per-environment: invekto_dev, invekto_staging, invekto_prod)
-- This table maps Main App tenant_id (SQL Server int) to InvektoServis config.

-- Create database (run once, manually)
-- CREATE DATABASE invekto_dev;

-- Tenant registry: stores per-tenant configuration for InvektoServis
CREATE TABLE IF NOT EXISTS tenant_registry (
    tenant_id           INTEGER PRIMARY KEY,         -- Main App SQL Server tenant_id (NOT auto-generated here)
    tenant_name         VARCHAR(200) NOT NULL,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    callback_url        VARCHAR(500),                -- Override callback URL (null = use default)
    sector              VARCHAR(50),                 -- e.g., 'eticaret', 'dis_klinik', 'estetik', 'genel'
    plan_tier           VARCHAR(20) NOT NULL DEFAULT 'basic', -- 'basic', 'pro', 'enterprise'
    features_json       JSONB,                       -- Feature flags per tenant, e.g., {"chatbot": true, "broadcast": false}
    settings_json       JSONB,                       -- Tenant-specific settings, e.g., {"working_hours": {...}}
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for active tenants lookup
CREATE INDEX IF NOT EXISTS idx_tenant_registry_active ON tenant_registry (is_active) WHERE is_active = true;

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
