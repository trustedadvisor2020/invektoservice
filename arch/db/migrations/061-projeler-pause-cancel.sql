-- =============================================================
-- Migration 061: FEAT-PROJELER — project run pause / resume / cancel (PKT-14, send-exec SS-D)
-- =============================================================
-- ADDITIVE, idempotent. Opens the run lifecycle so an operator can pause, resume
-- and cancel a project's in-flight send. Runtime behaviour stays a NO-OP until a
-- tenant is allowlisted for sending (Outbound:BulkSend + CxapiSend default-OFF,
-- P0-3 gate) — these are schema-only changes.
--
-- SS-D state machine (Q interview 2026-06-10):
--   * NEW message statuses:
--       'paused'    — PRE-TERMINAL. Pause flips a project's 'queued' rows to
--                     'paused'; the dequeue worker only claims 'queued' so they
--                     halt in place. Resume flips them back to 'queued'. Because
--                     'paused' is pre-terminal it stays inside the broadcast.queued
--                     counter (no counter math on pause/resume).
--       'cancelled' — TERMINAL. Cancel terminalizes remaining 'queued'+'paused'
--                     rows to 'cancelled' with a per-broadcast cancelled++ / queued--
--                     so totals reconcile (see (C)). In-flight 'sending'/'posting'
--                     rows are NOT recalled (already handed to the provider).
--   * Terminal = sent|delivered|read|failed|blocked|ambiguous|cancelled;
--     non-terminal = queued|sending|posting|paused.
--
-- Codex-056 reconciliation invariant extended (a cancelled message must land in a
-- counter bucket, not vanish):
--   sent + failed + ambiguous + delivered + read + queued + cancelled = total_recipients.
--
-- Run + project lifecycle:
--   * bulk_send_jobs.status gains 'paused' (already had 'cancelled') so the run row
--     reflects pause/resume/cancel alongside the message + project status.
--   * projects gains a denormalized cancelled_count rollup (mirrors ambiguous_count),
--     populated by ProjectsRepository.RecomputeRollupAsync from broadcast.cancelled.
--
-- DEFERRED (NOT in this migration):
--   * bulk_send_jobs cxapi columns + approved-template (HSM) send -> PR-4.
--
-- snake_case, single shared Postgres (rollout skill). Idempotent: named DROP/ADD
-- constraints + ADD COLUMN IF NOT EXISTS. Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- (A) outbound_messages: widen the status domain for pause/cancel
-- -------------------------------------------------------------
-- Existing rows only ever hold the pre-SS-D values, so widening validates cleanly.
ALTER TABLE outbound_messages DROP CONSTRAINT IF EXISTS chk_message_status;
ALTER TABLE outbound_messages ADD CONSTRAINT chk_message_status
    CHECK (status IN ('queued', 'sending', 'sent', 'delivered', 'read', 'failed', 'blocked', 'posting', 'ambiguous', 'paused', 'cancelled'));

-- -------------------------------------------------------------
-- (B) bulk_send_jobs: the run row can reflect a paused send
-- -------------------------------------------------------------
-- 'cancelled' already present (migration 051); add 'paused' for SS-D.
ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS chk_bulk_job_status;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT chk_bulk_job_status
    CHECK (status IN ('preview_ready', 'confirming', 'sending', 'completed', 'completed_with_errors', 'failed', 'cancelled', 'paused'));

-- -------------------------------------------------------------
-- (C) outbound_broadcasts: 'cancelled' counter (totals reconciliation)
-- -------------------------------------------------------------
-- Mirrors the migration-056 'ambiguous' counter. Cancel does queued-- / cancelled++
-- atomically so a cancelled message never drops out of the broadcast totals.
ALTER TABLE outbound_broadcasts
    ADD COLUMN IF NOT EXISTS cancelled INTEGER NOT NULL DEFAULT 0;

ALTER TABLE outbound_broadcasts DROP CONSTRAINT IF EXISTS chk_outbound_broadcasts_cancelled_nonneg;
ALTER TABLE outbound_broadcasts ADD CONSTRAINT chk_outbound_broadcasts_cancelled_nonneg
    CHECK (cancelled >= 0);

-- -------------------------------------------------------------
-- (D) projects: denormalized cancelled_count rollup
-- -------------------------------------------------------------
-- Mirrors ambiguous_count; RecomputeRollupAsync sums broadcast.cancelled into it.
ALTER TABLE projects
    ADD COLUMN IF NOT EXISTS cancelled_count INTEGER NOT NULL DEFAULT 0;

-- Widen the non-negative counter guard to include the new column (idempotent rebuild).
ALTER TABLE projects DROP CONSTRAINT IF EXISTS chk_project_counters_nonneg;
ALTER TABLE projects ADD CONSTRAINT chk_project_counters_nonneg CHECK (
    run_count >= 0 AND total_targets >= 0 AND sent_count >= 0
    AND delivered_count >= 0 AND read_count >= 0 AND failed_count >= 0
    AND ambiguous_count >= 0 AND cancelled_count >= 0);

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-061)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_msg_status_def TEXT;
    v_job_status_def TEXT;
    v_bcast_cancelled_cols  INTEGER;
    v_proj_cancelled_cols   INTEGER;
BEGIN
    SELECT pg_get_constraintdef(oid) INTO v_msg_status_def
    FROM pg_constraint
    WHERE conname = 'chk_message_status'
      AND conrelid = 'outbound_messages'::regclass;

    IF v_msg_status_def IS NULL THEN
        RAISE EXCEPTION 'INV-SEED-061: chk_message_status missing on outbound_messages';
    END IF;
    IF position('paused' IN v_msg_status_def) = 0 OR position('cancelled' IN v_msg_status_def) = 0 THEN
        RAISE EXCEPTION 'INV-SEED-061: chk_message_status does not allow paused/cancelled (def=%)', v_msg_status_def;
    END IF;

    SELECT pg_get_constraintdef(oid) INTO v_job_status_def
    FROM pg_constraint
    WHERE conname = 'chk_bulk_job_status'
      AND conrelid = 'bulk_send_jobs'::regclass;

    IF v_job_status_def IS NULL THEN
        RAISE EXCEPTION 'INV-SEED-061: chk_bulk_job_status missing on bulk_send_jobs';
    END IF;
    IF position('paused' IN v_job_status_def) = 0 THEN
        RAISE EXCEPTION 'INV-SEED-061: chk_bulk_job_status does not allow paused (def=%)', v_job_status_def;
    END IF;

    SELECT COUNT(*) INTO v_bcast_cancelled_cols
    FROM information_schema.columns
    WHERE table_name = 'outbound_broadcasts' AND column_name = 'cancelled';

    IF v_bcast_cancelled_cols <> 1 THEN
        RAISE EXCEPTION 'INV-SEED-061: outbound_broadcasts.cancelled column missing (found %/1)', v_bcast_cancelled_cols;
    END IF;

    SELECT COUNT(*) INTO v_proj_cancelled_cols
    FROM information_schema.columns
    WHERE table_name = 'projects' AND column_name = 'cancelled_count';

    IF v_proj_cancelled_cols <> 1 THEN
        RAISE EXCEPTION 'INV-SEED-061: projects.cancelled_count column missing (found %/1)', v_proj_cancelled_cols;
    END IF;

    RAISE NOTICE 'INV-SEED-061 OK: chk_message_status +paused/+cancelled, chk_bulk_job_status +paused, outbound_broadcasts.cancelled counter, projects.cancelled_count rollup (SS-D pause/resume/cancel; inert until BulkSend allowlist)';
END
$verify$;
