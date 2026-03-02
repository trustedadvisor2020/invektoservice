-- Migration 004: Tenant Usage Counters — Faz 1 Paket 2
-- Date: 2026-03-02
-- Description: Create tenant_usage table for monthly quota tracking
-- Safe: CREATE TABLE IF NOT EXISTS, CREATE INDEX IF NOT EXISTS
-- Precondition: tenant_registry table exists, update_updated_at_column() trigger function exists

-- ============================================================
-- Part A: Create tenant_usage table
-- ============================================================

CREATE TABLE IF NOT EXISTS tenant_usage (
    tenant_id       INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    period_month    DATE NOT NULL,              -- First day of month: '2026-03-01'
    messages_sent   INTEGER NOT NULL DEFAULT 0, -- Cumulative for month
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tenant_id, period_month)
);

-- ============================================================
-- Part B: Index for period-based queries
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_tenant_usage_period
    ON tenant_usage (period_month, tenant_id);

-- ============================================================
-- Part C: Updated_at trigger
-- ============================================================

CREATE OR REPLACE TRIGGER trigger_tenant_usage_updated_at
    BEFORE UPDATE ON tenant_usage
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- Part D: Grants
-- ============================================================

GRANT ALL ON tenant_usage TO invekto;

-- Verify: SELECT * FROM tenant_usage LIMIT 5;
