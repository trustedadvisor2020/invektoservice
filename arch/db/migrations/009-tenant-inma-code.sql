-- Migration 009: Add inma_code to tenant_registry for auto-provision from Inma
-- Date: 2026-04-01
-- Purpose: Maps Inma Company.Code (string like "voila") to İnse tenant_id

ALTER TABLE tenant_registry ADD COLUMN IF NOT EXISTS inma_code VARCHAR(100);

CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_registry_inma_code
    ON tenant_registry(inma_code) WHERE inma_code IS NOT NULL;

-- NOTE (2026-04-17): UP0.3 Backfill Strategy — lazy provisioning TEK PATH (Q kararı).
-- Mevcut INMA tenant'ları için manuel SQL backfill YAPILMAYACAK.
-- Pattern: InmaTokenIntrospector welcome 200 + tenant_registry miss → o istekte INSERT ON CONFLICT DO NOTHING.
-- Detay: arch/platform/inma-inse-unification/roadmap.md UP0.3 section.
