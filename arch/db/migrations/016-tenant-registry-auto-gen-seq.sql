-- Migration 016: Auto-generated tenant_id sequence for INMA lazy auto-provision
-- Date: 2026-04-17
-- Purpose: Support InmaTokenIntrospector lazy provisioning (UP0.3 pattern planned
-- in migration 009 NOTE). CompanyCode claim is opaque string — unknown tenants
-- get a fresh tenant_id allocated from this sequence on first login.
--
-- Rationale:
--   - Existing tenant_ids: 1, 5050, 5051, 14120748, 15702882, 40695541 (max).
--   - Sequence starts at 100_000_000 to leave a large manual-assignment window
--     and avoid colliding with future INMA-side manual IDs below that range.
--   - CACHE 1 keeps sequence tightly synchronized across connections (tenant
--     creation is low-volume, so cache-amortization is not needed).
--   - OWNED BY tenant_registry.tenant_id ties sequence lifecycle to the table.

CREATE SEQUENCE IF NOT EXISTS tenant_registry_auto_id_seq
    START WITH 100000000
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
    OWNED BY tenant_registry.tenant_id;

GRANT USAGE, SELECT ON SEQUENCE tenant_registry_auto_id_seq TO invekto;

-- Backfill: tenant 5050 was treated as numeric-string CompanyCode by the old
-- int.TryParse path. After the string refactor, inma_code='5050' ensures the
-- lookup continues to match without forcing an auto-create on first re-login.
-- Other NULL inma_code rows are intentionally left alone (Q decision: lazy
-- auto-create will populate them on demand if ever reused from INMA).
UPDATE tenant_registry
SET inma_code = '5050',
    updated_at = NOW()
WHERE tenant_id = 5050
  AND inma_code IS NULL;
