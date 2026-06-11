-- =============================================================
-- Migration 063: tenant_instances — connection_type (WABA discriminator)
-- =============================================================
-- ADDITIVE, idempotent. The Projeler "WhatsApp Kanali" dropdown must show ONLY
-- real WABA (WhatsApp Cloud API) lines, but cxapi /api/Instances gives
-- instanceType=1 to BOTH WABA and QR-Code (WhatsApp Web) lines (live proof,
-- 2026-06-11: tenant 5050 -> 2131 + 8523 are instanceType=1 with
-- connectionType='QR Code'; medipol/100000001 -> 7041 'QR Code' + 8784 'WABA',
-- both type=1). The reliable discriminator is the separate connectionType
-- string the same API already returns.
--
-- New column:
--   connection_type  vendor connectionType, persisted verbatim (trimmed,
--                    empty->NULL upstream). Observed open set: 'WABA',
--                    'QR Code', 'SMS', 'Voip', 'Instagram', 'Telegram' —
--                    vendor-defined, so TEXT and NO CHECK constraint
--                    (a CHECK would break the fetch when the vendor ships a
--                    new value; plan-review applied: TEXT, not VARCHAR(30)).
--
-- NULL semantics (asymmetric, Q-approved 2026-06-11):
--   UI    -> NULL falls back to the old instanceType=1 filter (display only).
--   Server-> STRICT: Outbound IsCloudApiInstanceOwnedAsync requires
--            UPPER(TRIM(connection_type))='WABA' — NULL does NOT authorize.
--            Safe because the one-time data backfill (5050 + 100000001, the
--            ONLY tenants with tenant_instances rows) runs BEFORE the service
--            deploy, so no legitimate WABA row is ever NULL when the strict
--            guard goes live.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: ADD COLUMN IF NOT EXISTS. Safe to re-run.
-- tenant_instances already GRANTed — column adds inherit table grants.
-- Deploy order: THIS migration -> data backfill -> Outbound -> Backend+SPA.
-- =============================================================

ALTER TABLE tenant_instances
    ADD COLUMN IF NOT EXISTS connection_type TEXT;

-- -------------------------------------------------------------
-- Verifier: INV-SEED-063
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_col BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'tenant_instances' AND column_name = 'connection_type'
    ) INTO v_has_col;

    IF v_has_col IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-063: tenant_instances.connection_type column missing';
    END IF;

    RAISE NOTICE 'INV-SEED-063 OK: tenant_instances.connection_type (TEXT NULL) present — WABA discriminator persisted; strict server guard requires backfill before service deploy';
END
$verify$;
