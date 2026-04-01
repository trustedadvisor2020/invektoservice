-- Migration 009: Add inma_code to tenant_registry for auto-provision from Inma
-- Date: 2026-04-01
-- Purpose: Maps Inma Company.Code (string like "voila") to İnse tenant_id

ALTER TABLE tenant_registry ADD COLUMN IF NOT EXISTS inma_code VARCHAR(100);

CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_registry_inma_code
    ON tenant_registry(inma_code) WHERE inma_code IS NOT NULL;

-- Backfill existing tenants if known
-- UPDATE tenant_registry SET inma_code = '5050' WHERE tenant_id = 5050;
