-- PostgreSQL Setup for InvektoServis (Production)
-- Server: services.invekto.com
-- Run as postgres superuser: psql -U postgres -f setup-postgresql.sql

-- 1. Create database
CREATE DATABASE invekto;

-- 2. Create application user
CREATE USER invekto WITH PASSWORD 'BURAYA_GUCLU_SIFRE_YAZ';

-- 3. Grant privileges
GRANT CONNECT ON DATABASE invekto TO invekto;

-- Switch to invekto database
\c invekto

-- 4. Grant schema privileges
GRANT USAGE ON SCHEMA public TO invekto;
GRANT CREATE ON SCHEMA public TO invekto;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO invekto;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO invekto;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO invekto;

-- 5. Create tables (tenant-registry.sql icerigi)
CREATE TABLE IF NOT EXISTS tenant_registry (
    tenant_id           INTEGER PRIMARY KEY,
    tenant_name         VARCHAR(200) NOT NULL,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    callback_url        VARCHAR(500),
    sector              VARCHAR(50),
    plan_tier           VARCHAR(20) NOT NULL DEFAULT 'basic',
    features_json       JSONB,
    settings_json       JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tenant_registry_active ON tenant_registry (is_active) WHERE is_active = true;

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

-- 6. Verify
\dt
\du invekto

-- Done! Connection string for appsettings.Production.json:
-- Host=localhost;Port=5432;Database=invekto;Username=invekto;Password=BURAYA_GUCLU_SIFRE_YAZ
