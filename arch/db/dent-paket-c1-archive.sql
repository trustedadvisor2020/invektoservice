-- ============================================================
-- dent_paket_c1_archive_20260424 — Ephemeral pre-state snapshot
-- ============================================================
-- Purpose: Rollback evidence for Paket C1 Migration 032 (Dent FAQ Surface Content Bind).
-- Captures pre-state row_to_json snapshots of faq_entries (36 rows pre-DELETE) and
-- chatbot_flows flow_id=29 (1 row pre-jsonb_set UPDATE).
--
-- Lifecycle:
--   - CREATED in arch/db/migrations/032-dent-paket-c1-faqs-content-bind.sql §1
--     (CREATE TABLE IF NOT EXISTS, idempotent re-run safe)
--   - WRITTEN once during Migration 032 §1 (37 rows: 36 faq_entries + 1 chatbot_flows)
--   - READ-ONLY post-COMMIT (rollback procedure documented at Migration 032 bottom)
--   - DROPPED in Paket B-C2 (Welcome Templates Overhaul, post-pilot go-live ops cleanup)
--
-- Microservice scope: Paket C1-specific, tenant-agnostic table name (no tenant_id column;
-- single-tenant scope by Migration filter). NOT a runtime application table — operator/ops
-- audit only. Schema mirrored here per arch/db/*.sql source-of-truth + GRANT ALL invekto
-- requirement (Codex iter 1 CQ8/CQ11 fix; ephemeral lifecycle does not exempt schema policy).
-- ============================================================

CREATE TABLE IF NOT EXISTS dent_paket_c1_archive_20260424 (
    id            SERIAL PRIMARY KEY,
    archive_type  TEXT NOT NULL,
    payload       JSONB NOT NULL,
    archived_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE dent_paket_c1_archive_20260424 IS
    'Paket C1 (Dent FAQ Surface Content Bind, Migration 032, 2026-04-24) pre-state snapshots. archive_type values: faq_entries_pre_delete (36 rows), chatbot_flows_pre_update (1 row). DROP after pilot go-live confirmed (Stage 3 full traffic, post-pilot ops; Paket B-C2 scope).';

COMMENT ON COLUMN dent_paket_c1_archive_20260424.archive_type IS
    'Snapshot category: faq_entries_pre_delete | chatbot_flows_pre_update. Rollback path filters by this column.';

COMMENT ON COLUMN dent_paket_c1_archive_20260424.payload IS
    'Full row_to_json(.*) snapshot of source row. Restore via Migration 032 ROLLBACK section (commented at SQL bottom): JSONB extraction back into source columns.';

GRANT ALL ON dent_paket_c1_archive_20260424 TO invekto;
GRANT USAGE, SELECT ON SEQUENCE dent_paket_c1_archive_20260424_id_seq TO invekto;
