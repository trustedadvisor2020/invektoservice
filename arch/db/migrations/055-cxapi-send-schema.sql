-- =============================================================
-- Migration 055: FEAT-PROJELER / cxapi Send Engine — PR-1 (schema groundwork)
-- =============================================================
-- ADDITIVE, NO-OP migration. Lays the schema foundation for moving bulk
-- WhatsApp sends from the INMA Main App bridge to WapCRM cxapi /chatoperation
-- (+ approved-template/HSM). PR-1 ONLY adds columns — runtime behaviour is
-- UNCHANGED. The new columns are written with safe defaults / left NULL and are
-- NOT yet consumed by any routing decision; the send client (PR-2), plain-text
-- cutover + state machine (PR-3) and approved-template param resolve (PR-4) land
-- later.
--
-- Codex critique (2026-06-06) safety rails encoded here:
--   * All new columns nullable OR NOT NULL with a constant DEFAULT, so adding
--     them is a metadata-only change (Postgres 11+) and existing INSERT/SELECT
--     paths keep working without referencing them.
--   * Immutable route column: outbound_messages.send_route + message_kind
--     (CHECK-constrained) replace the fragile broadcast_id discriminator later.
--   * Structured provider result columns (provider_status_code/status/
--     request_id/error_message, last_attempt_at, attempt_count) so a send's
--     route + provider response become persistently auditable in PR-3.
--   * Reserved nullable columns (template_header_media, template_language) are
--     opened now but stay unwritten until PR-4.
--
-- DEFERRED to PR-3 (Codex review CQ9/Q2 + roadmap G12, decided 2026-06-06):
--   The ext_id uniqueness change (composite UNIQUE (tenant_id,
--   external_message_id)) is intentionally NOT in PR-1. Tightening uniqueness to
--   per-tenant while the delivery-status lookup (FindMessageByExternalIdAsync)
--   is still tenant-blind would formalize a cross-tenant ambiguity. The unique
--   index ships ATOMICALLY with the tenant-scoped lookup in PR-3, after INMA
--   confirms G12 (requestID == delivery-status webhook id). PR-1 leaves the
--   existing idx_outbound_messages_external_id and lookup path untouched.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: ADD COLUMN IF NOT EXISTS + DROP-then-ADD CHECK. Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- (A) outbound_messages: per-message route + template snapshot + provider result
-- -------------------------------------------------------------
ALTER TABLE outbound_messages
    ADD COLUMN IF NOT EXISTS send_route             VARCHAR(20)  NOT NULL DEFAULT 'mainapp_bridge',
    ADD COLUMN IF NOT EXISTS message_kind           VARCHAR(20)  NOT NULL DEFAULT 'plain_text',
    ADD COLUMN IF NOT EXISTS instance_id            INTEGER,
    ADD COLUMN IF NOT EXISTS template_ref           VARCHAR(128),
    ADD COLUMN IF NOT EXISTS template_params        JSONB,
    ADD COLUMN IF NOT EXISTS template_language      VARCHAR(8),
    ADD COLUMN IF NOT EXISTS template_header_media  JSONB,
    ADD COLUMN IF NOT EXISTS provider_status_code   VARCHAR(8),
    ADD COLUMN IF NOT EXISTS provider_status        BOOLEAN,
    ADD COLUMN IF NOT EXISTS provider_request_id    VARCHAR(128),
    ADD COLUMN IF NOT EXISTS provider_error_message TEXT,
    ADD COLUMN IF NOT EXISTS last_attempt_at        TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS attempt_count          INTEGER      NOT NULL DEFAULT 0;

-- Route/kind value domains are locked by Codex P0 #5; CHECK them now (existing
-- rows already carry the defaults, so the constraint validates cleanly).
ALTER TABLE outbound_messages DROP CONSTRAINT IF EXISTS chk_outbound_messages_send_route;
ALTER TABLE outbound_messages ADD CONSTRAINT chk_outbound_messages_send_route
    CHECK (send_route IN ('mainapp_bridge', 'wapcrm_cxapi'));

ALTER TABLE outbound_messages DROP CONSTRAINT IF EXISTS chk_outbound_messages_message_kind;
ALTER TABLE outbound_messages ADD CONSTRAINT chk_outbound_messages_message_kind
    CHECK (message_kind IN ('plain_text', 'wapcrm_template'));

-- -------------------------------------------------------------
-- (B) DEFERRED to PR-4: bulk_send_jobs per-job template config
-- -------------------------------------------------------------
-- bulk_send_jobs has NO canonical arch/db/*.sql (migration-only table). Per Codex
-- CQ11 (DB-code source-of-truth must hold every column) + Q decision 2026-06-06,
-- its cxapi columns (instance_id / template_kind / wa_template_id / param_mapping /
-- template_language) land in PR-4 — together with their writer (BulkSendRepository)
-- AND their canonical schema sync — since they are approved-template (PR-4) config
-- and are unused before then. PR-1 adds NOTHING to bulk_send_jobs.

-- -------------------------------------------------------------
-- (C) outbound_broadcasts: homogeneous route + template config per broadcast
-- -------------------------------------------------------------
-- A broadcast is homogeneous (one route/template/lang/instance for all its
-- messages — Codex P0 #5). send_route defaults to the bridge so existing
-- broadcast creation (OutboundRepository.CreateBroadcastAsync) is unchanged.
ALTER TABLE outbound_broadcasts
    ADD COLUMN IF NOT EXISTS send_route         VARCHAR(20) NOT NULL DEFAULT 'mainapp_bridge',
    ADD COLUMN IF NOT EXISTS instance_id        INTEGER,
    ADD COLUMN IF NOT EXISTS template_kind      VARCHAR(16),
    ADD COLUMN IF NOT EXISTS wa_template_id     VARCHAR(128),
    ADD COLUMN IF NOT EXISTS param_mapping      JSONB,
    ADD COLUMN IF NOT EXISTS template_language  VARCHAR(8);

ALTER TABLE outbound_broadcasts DROP CONSTRAINT IF EXISTS chk_outbound_broadcasts_send_route;
ALTER TABLE outbound_broadcasts ADD CONSTRAINT chk_outbound_broadcasts_send_route
    CHECK (send_route IN ('mainapp_bridge', 'wapcrm_cxapi'));

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-055)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_msg_cols    INTEGER;
    v_bcast_cols  INTEGER;
BEGIN
    -- All 13 new outbound_messages columns present
    SELECT COUNT(*) INTO v_msg_cols
    FROM information_schema.columns
    WHERE table_name = 'outbound_messages'
      AND column_name IN (
        'send_route','message_kind','instance_id','template_ref','template_params',
        'template_language','template_header_media','provider_status_code',
        'provider_status','provider_request_id','provider_error_message',
        'last_attempt_at','attempt_count');

    SELECT COUNT(*) INTO v_bcast_cols
    FROM information_schema.columns
    WHERE table_name = 'outbound_broadcasts'
      AND column_name IN ('send_route','instance_id','template_kind','wa_template_id','param_mapping','template_language');

    IF v_msg_cols <> 13 THEN
        RAISE EXCEPTION 'INV-SEED-055: outbound_messages missing new columns (found %/13)', v_msg_cols;
    END IF;
    IF v_bcast_cols <> 6 THEN
        RAISE EXCEPTION 'INV-SEED-055: outbound_broadcasts missing new columns (found %/6)', v_bcast_cols;
    END IF;

    RAISE NOTICE 'INV-SEED-055 OK: +13 message / +6 broadcast cols (bulk_send_jobs -> PR-4, ext_id unique -> PR-3)';
END
$verify$;
