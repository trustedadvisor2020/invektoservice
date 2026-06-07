-- =============================================================
-- Migration 056: FEAT-PROJELER / cxapi Send Engine — PR-3a (plain-text cutover)
-- =============================================================
-- ADDITIVE, idempotent. Opens the message-status domain for the cxapi send state
-- machine and adds a broadcast-level 'ambiguous' counter so totals reconcile.
-- Runtime behaviour stays a NO-OP until a tenant is allowlisted for cxapi
-- (CxapiSendOptions, default OFF) — these are schema-only changes.
--
-- PR-3a state machine (minimal-blast, Q interview 2026-06-06):
--   * NEW message statuses: 'posting'  (in-flight; external POST may have happened —
--     never auto-requeued) and 'ambiguous' (timeout/transport/stranded — unknown
--     delivery, manual/ops, never auto-retried). 'submitted' reuses 'sent' and
--     'provider_failed'/rate-exhausted reuse 'failed'; cxapi detail lives in the
--     provider_* columns (migration 055).
--   * Terminal = sent|delivered|read|failed|ambiguous; non-terminal = queued|sending|posting.
--
-- Codex plan-review (2026-06-06) P1-3: the minimal-blast model would otherwise drop
-- 'ambiguous' messages out of every broadcast counter (sent+failed < total). Adding
-- outbound_broadcasts.ambiguous keeps the invariant:
--   sent + failed + ambiguous + delivered + read + queued = total_recipients.
--
-- DEFERRED (NOT in this migration):
--   * ext_id composite UNIQUE (tenant_id, external_message_id) + tenant-scoped lookup
--     -> PR-3b, gated on INMA G12 (requestID == delivery-status webhook id?).
--   * bulk_send_jobs cxapi columns + approved-template config -> PR-4.
--
-- snake_case, single shared Postgres (rollout skill). Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- (A) outbound_messages: widen the status domain for the cxapi state machine
-- -------------------------------------------------------------
-- Existing rows only ever hold the legacy values, so widening validates cleanly.
ALTER TABLE outbound_messages DROP CONSTRAINT IF EXISTS chk_message_status;
ALTER TABLE outbound_messages ADD CONSTRAINT chk_message_status
    CHECK (status IN ('queued', 'sending', 'sent', 'delivered', 'read', 'failed', 'blocked', 'posting', 'ambiguous'));

-- -------------------------------------------------------------
-- (B) outbound_broadcasts: 'ambiguous' counter (totals reconciliation)
-- -------------------------------------------------------------
ALTER TABLE outbound_broadcasts
    ADD COLUMN IF NOT EXISTS ambiguous INTEGER NOT NULL DEFAULT 0;

ALTER TABLE outbound_broadcasts DROP CONSTRAINT IF EXISTS chk_outbound_broadcasts_ambiguous_nonneg;
ALTER TABLE outbound_broadcasts ADD CONSTRAINT chk_outbound_broadcasts_ambiguous_nonneg
    CHECK (ambiguous >= 0);

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-056)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_status_def  TEXT;
    v_amb_cols    INTEGER;
BEGIN
    SELECT pg_get_constraintdef(oid) INTO v_status_def
    FROM pg_constraint
    WHERE conname = 'chk_message_status'
      AND conrelid = 'outbound_messages'::regclass;

    IF v_status_def IS NULL THEN
        RAISE EXCEPTION 'INV-SEED-056: chk_message_status missing on outbound_messages';
    END IF;
    IF position('posting' IN v_status_def) = 0 OR position('ambiguous' IN v_status_def) = 0 THEN
        RAISE EXCEPTION 'INV-SEED-056: chk_message_status does not allow posting/ambiguous (def=%)', v_status_def;
    END IF;

    SELECT COUNT(*) INTO v_amb_cols
    FROM information_schema.columns
    WHERE table_name = 'outbound_broadcasts' AND column_name = 'ambiguous';

    IF v_amb_cols <> 1 THEN
        RAISE EXCEPTION 'INV-SEED-056: outbound_broadcasts.ambiguous column missing (found %/1)', v_amb_cols;
    END IF;

    RAISE NOTICE 'INV-SEED-056 OK: chk_message_status +posting/+ambiguous, outbound_broadcasts.ambiguous counter (ext_id -> PR-3b, bulk_send_jobs -> PR-4)';
END
$verify$;
