-- =============================================================
-- Migration 067: FEAT-OBI Phase 2 — Telefon Numarası Ara (single-number history)
-- =============================================================
-- A new "Telefon Numarası Ara" section in Export Manager lets an operator pin a
-- single phone number and see the tenant's FULL outbound history to it (bulk +
-- project + legacy broadcast), newest-first, then export it (CSV/PDF). It needs
-- NO new table — it reads outbound_messages and reuses export_logs (migration
-- 053). Purely ADDITIVE:
--
--   1. An index on outbound_messages (tenant_id, recipient_phone, created_at DESC,
--      id DESC) so the phone lookup AND the newest-first ORDER BY are both served
--      by one index (the existing idx_outbound_messages_tenant_broadcast_phone has
--      broadcast_id in the middle column, so it cannot serve a phone-only lookup).
--      IF NOT EXISTS — safe, non-data-changing.
--
--      NOTE on CONCURRENTLY: a plain CREATE INDEX is used (not CONCURRENTLY)
--      because (a) this migration file runs as one script alongside the DO $verify$
--      block — CREATE INDEX CONCURRENTLY cannot run inside a transaction block, and
--      (b) outbound_messages is small today so the brief ACCESS SHARE-blocking build
--      is sub-second. If the table grows large enough that the build lock matters,
--      run the CONCURRENTLY form manually, outside this migration, before deploy.
--
--   2. Widen export_logs.chk_export_log_type to add the new audit kind:
--        phone_history = a single-number history export (CSV server-stream OR the
--                        client-rendered PDF, both audited for KVKK).
--      DROP + re-ADD the named CHECK so re-running is idempotent. Existing rows stay
--      valid — the value set only grows, so no row can violate the widened CHECK.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS + DROP-then-ADD. Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- 1. Additive index: (tenant_id, recipient_phone) lookup + newest-first ordering
-- -------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_outbound_messages_tenant_phone_created
    ON outbound_messages (tenant_id, recipient_phone, created_at DESC, id DESC);

-- -------------------------------------------------------------
-- 2. Widen export_logs export_type CHECK (additive value set)
-- -------------------------------------------------------------
ALTER TABLE export_logs DROP CONSTRAINT IF EXISTS chk_export_log_type;
ALTER TABLE export_logs ADD CONSTRAINT chk_export_log_type
    CHECK (export_type IN (
        'contact_list','send_recipients','send_summary',
        'filtered_recipients','list_from_export',
        'phone_history'));

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-067)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_phone_index    BOOLEAN;
    v_type_check_widened BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_outbound_messages_tenant_phone_created'
    ) INTO v_has_phone_index;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_type' AND contype = 'c'
          AND pg_get_constraintdef(oid) LIKE '%phone_history%'
    ) INTO v_type_check_widened;

    IF v_has_phone_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-067: outbound_messages (tenant_id, recipient_phone, created_at, id) index missing';
    END IF;
    IF v_type_check_widened IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-067: export_logs export_type CHECK not widened with phone_history value';
    END IF;

    RAISE NOTICE 'INV-SEED-067 OK: outbound_messages phone index + widened export_type CHECK verified';
END
$verify$;
