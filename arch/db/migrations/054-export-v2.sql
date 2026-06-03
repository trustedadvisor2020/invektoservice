-- =============================================================
-- Migration 054: FEAT-OBI Phase 1B — Export Manager v2 (filter-driven surface)
-- =============================================================
-- v2 turns the Export Manager into a single filter-driven surface over ALL of
-- the tenant's bulk-send recipients (Şablon · Kampanya · Data Listesi membership
-- · Teslim Durumu · Tarih). It needs NO new table — it reuses export_logs
-- (migration 053) and data_lists.source='export' (migration 052, already in the
-- source CHECK). This migration is purely ADDITIVE:
--
--   1. An index on bulk_send_recipients (tenant_id, normalized_phone) to make
--      the cross-job COUNT(DISTINCT normalized_phone), the list-membership
--      EXISTS sub-query, and the distinct-sendable-phones read (create-list)
--      fast on large tenants. IF NOT EXISTS — safe, non-data-changing.
--
--   2. Widen export_logs.chk_export_log_type to add the two new audit kinds the
--      v2 surface writes:
--        filtered_recipients = a filtered CSV/XLSX recipient export
--        list_from_export    = a "Liste Oluştur" (new data_list from the filter)
--      DROP + re-ADD the named CHECK so re-running is idempotent. Existing rows
--      ('contact_list','send_recipients','send_summary') stay valid — the set
--      only grows, so no row can violate the widened constraint.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS + DROP-then-ADD. Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- 1. Additive index: distinct-phone count + list-membership EXISTS + create-list read
-- -------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_bulk_send_recipients_tenant_phone
    ON bulk_send_recipients (tenant_id, normalized_phone);

-- -------------------------------------------------------------
-- 2. Widen export_logs export_type CHECK (additive value set)
-- -------------------------------------------------------------
ALTER TABLE export_logs DROP CONSTRAINT IF EXISTS chk_export_log_type;
ALTER TABLE export_logs ADD CONSTRAINT chk_export_log_type
    CHECK (export_type IN (
        'contact_list','send_recipients','send_summary',
        'filtered_recipients','list_from_export'));

-- "Liste Oluştur" is not a file egress, so logging it honestly needs a non-file
-- format ('list') and a non-streaming delivery mode ('internal') rather than
-- pretending bytes were produced/streamed. Widen both CHECKs (additive).
ALTER TABLE export_logs DROP CONSTRAINT IF EXISTS chk_export_log_format;
ALTER TABLE export_logs ADD CONSTRAINT chk_export_log_format
    CHECK (format IN ('csv','xlsx','pdf','list'));

ALTER TABLE export_logs DROP CONSTRAINT IF EXISTS chk_export_log_delivery;
ALTER TABLE export_logs ADD CONSTRAINT chk_export_log_delivery
    CHECK (delivery_mode IN ('server_stream','client_render','internal'));

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-054)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_recipient_index BOOLEAN;
    v_type_check_widened  BOOLEAN;
    v_format_widened      BOOLEAN;
    v_delivery_widened    BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_bulk_send_recipients_tenant_phone'
    ) INTO v_has_recipient_index;

    -- The widened CHECKs must admit the new values; probe the constraint text.
    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_type' AND contype = 'c'
          AND pg_get_constraintdef(oid) LIKE '%filtered_recipients%'
          AND pg_get_constraintdef(oid) LIKE '%list_from_export%'
    ) INTO v_type_check_widened;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_format' AND contype = 'c'
          AND pg_get_constraintdef(oid) LIKE '%list%'
    ) INTO v_format_widened;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_delivery' AND contype = 'c'
          AND pg_get_constraintdef(oid) LIKE '%internal%'
    ) INTO v_delivery_widened;

    IF v_has_recipient_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-054: bulk_send_recipients (tenant_id, normalized_phone) index missing';
    END IF;
    IF v_type_check_widened IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-054: export_logs export_type CHECK not widened with v2 values';
    END IF;
    IF v_format_widened IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-054: export_logs format CHECK not widened with list value';
    END IF;
    IF v_delivery_widened IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-054: export_logs delivery_mode CHECK not widened with internal value';
    END IF;

    RAISE NOTICE 'INV-SEED-054 OK: recipients phone index + widened type/format/delivery CHECKs verified';
END
$verify$;
