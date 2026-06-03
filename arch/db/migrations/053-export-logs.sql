-- =============================================================
-- Migration 053: FEAT-OBI Phase 1A Plan B — Export Manager (audit layer)
-- =============================================================
-- KVKK audit trail for the Export Manager: every export request (contact
-- list OR bulk-send campaign results) writes exactly one row here — who /
-- when / what / how many rows / which format / which delivery mode.
--
-- Design (Codex plan review 2026-06-03):
--   * Append-only at the APPLICATION layer: ExportRepository issues INSERT
--     only; no UPDATE/DELETE statement ever targets export_logs. GRANT ALL
--     is kept (project canon, codex-context.md L20/L69; B1 downgrade FAILED
--     review). There is no separate audit-writer DB role in this single-role
--     'invekto' architecture — least-privilege is enforced in code by design.
--   * delivery_mode makes audit honesty explicit:
--       server_stream = bytes were streamed to the client (csv/xlsx)
--       client_render = only report-data JSON was served; the PDF is rendered
--                        in the browser (jsPDF), so 'completed' means
--                        "export data served", NOT "PDF physically rendered".
--   * source_id is polymorphic BIGINT (data_lists.id | bulk_send_jobs.id —
--     both BIGSERIAL); nullable. source_name_snapshot captures the list/
--     campaign name at export time (forensic, survives later rename/delete).
--   * requested_by = JWT subject/operator id (NOT a display name).
--
-- Plus an ADDITIVE index on the existing hot outbound_messages table to make
-- the per-recipient send-results join (tenant_id, broadcast_id, recipient_phone)
-- fast. IF NOT EXISTS — safe, non-locking-blocking on an existing table.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS guards, safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- export_logs: one row per export request (KVKK audit trail)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS export_logs (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),

    -- what kind of data left the system
    export_type             VARCHAR(24) NOT NULL,
    -- data_lists.id | bulk_send_jobs.id (both BIGINT); nullable for forensic-only rows
    source_id               BIGINT,
    -- list/campaign name as it was at export time (survives later rename/delete)
    source_name_snapshot    VARCHAR(255),

    format                  VARCHAR(8) NOT NULL,
    -- server_stream = bytes streamed (csv/xlsx); client_render = data served, PDF in browser
    delivery_mode           VARCHAR(16) NOT NULL,

    row_count               INTEGER NOT NULL DEFAULT 0,
    status                  VARCHAR(16) NOT NULL DEFAULT 'completed',

    -- JWT subject / operator user id (NOT display name)
    requested_by            VARCHAR(120),
    -- populated on failure: INV-OB-0xx
    error_code              VARCHAR(16),

    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_export_log_type
        CHECK (export_type IN ('contact_list','send_recipients','send_summary')),
    CONSTRAINT chk_export_log_format
        CHECK (format IN ('csv','xlsx','pdf')),
    CONSTRAINT chk_export_log_delivery
        CHECK (delivery_mode IN ('server_stream','client_render')),
    CONSTRAINT chk_export_log_status
        CHECK (status IN ('completed','failed'))
);

-- Audit lookup: "who exported for this tenant, newest first".
CREATE INDEX IF NOT EXISTS idx_export_logs_tenant_created
    ON export_logs (tenant_id, created_at DESC);

-- -------------------------------------------------------------
-- Additive index on the existing hot outbound_messages table:
-- speeds the per-recipient send-results join keyed by
-- (tenant_id, broadcast_id, recipient_phone). Additive + IF NOT EXISTS.
-- -------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_outbound_messages_tenant_broadcast_phone
    ON outbound_messages (tenant_id, broadcast_id, recipient_phone);

-- -------------------------------------------------------------
-- Grants (production runs as role 'invekto'). GRANT ALL = project canon;
-- append-only is enforced in ExportRepository (INSERT only), not via grants.
-- -------------------------------------------------------------
GRANT ALL ON export_logs TO invekto;
GRANT ALL ON SEQUENCE export_logs_id_seq TO invekto;

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-053)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_export_logs     BOOLEAN;
    v_has_type_check      BOOLEAN;
    v_has_delivery_check  BOOLEAN;
    v_has_tenant_index    BOOLEAN;
    v_has_join_index      BOOLEAN;
BEGIN
    v_has_export_logs := to_regclass('public.export_logs') IS NOT NULL;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_type' AND contype = 'c'
    ) INTO v_has_type_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_export_log_delivery' AND contype = 'c'
    ) INTO v_has_delivery_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_export_logs_tenant_created'
    ) INTO v_has_tenant_index;

    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_outbound_messages_tenant_broadcast_phone'
    ) INTO v_has_join_index;

    IF v_has_export_logs IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-053: export_logs table missing after migration';
    END IF;
    IF v_has_type_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-053: export_logs export_type CHECK missing';
    END IF;
    IF v_has_delivery_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-053: export_logs delivery_mode CHECK missing';
    END IF;
    IF v_has_tenant_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-053: export_logs tenant/created index missing';
    END IF;
    IF v_has_join_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-053: outbound_messages send-results join index missing';
    END IF;

    RAISE NOTICE 'INV-SEED-053 OK: export_logs audit table + join index verified';
END
$verify$;
