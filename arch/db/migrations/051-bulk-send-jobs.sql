-- =============================================================
-- Migration 051: FEAT-OBI Phase 0 — Bulk Send (CSV source)
-- =============================================================
-- Adds a parent-job layer over the existing broadcast engine so an
-- operator can upload a recipient list (CSV/paste), get a deduped
-- preview, then confirm a chunked send (<=1000/broadcast).
--
-- Codex review safety rails (2026-06-03):
--   * immutable recipient SNAPSHOT (preview != send drift protection)
--   * client-supplied campaign_id idempotency (NOT query_hash)
--   * dedup on normalized_phone, dropped tracked as counts
--   * rich status states (not just fetching->sending->completed)
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS guards, safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- bulk_send_jobs: one row per campaign (parent over N broadcasts)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bulk_send_jobs (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    -- Client-supplied idempotency key. Re-confirming the same campaign_id
    -- returns the existing job instead of re-sending (Codex §5).
    campaign_id             VARCHAR(80) NOT NULL,
    source                  VARCHAR(16) NOT NULL DEFAULT 'csv',
    template_id             INTEGER NOT NULL REFERENCES outbound_templates(id),
    lang                    VARCHAR(8),
    hard_cap                INTEGER NOT NULL,

    -- Parse/dedup counters (set at preview)
    total_input             INTEGER NOT NULL DEFAULT 0,
    total_valid             INTEGER NOT NULL DEFAULT 0,
    total_duplicate         INTEGER NOT NULL DEFAULT 0,
    total_invalid           INTEGER NOT NULL DEFAULT 0,

    -- Send counters (set at confirm; aggregated across child broadcasts)
    total_queued            INTEGER NOT NULL DEFAULT 0,
    total_skipped_optout    INTEGER NOT NULL DEFAULT 0,
    total_skipped_consent   INTEGER NOT NULL DEFAULT 0,

    -- Child broadcast ids (one per <=1000 chunk)
    broadcast_ids           UUID[] NOT NULL DEFAULT '{}',
    -- TRUE if any chunk failed to dispatch (no child broadcast created).
    -- Persisted so the terminal roll-up can report completed_with_errors even
    -- when the failed chunk left no broadcast row to aggregate.
    dispatch_error          BOOLEAN NOT NULL DEFAULT FALSE,

    status                  VARCHAR(24) NOT NULL DEFAULT 'preview_ready',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_at            TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,

    -- status lifecycle:
    --   preview_ready        -> snapshot built, awaiting operator confirm
    --   confirming           -> chunking + dispatching broadcasts
    --   sending              -> all chunks dispatched, messages in flight
    --   completed            -> all child broadcasts completed, no failures
    --   completed_with_errors-> all completed, some messages failed/blocked
    --   failed               -> dispatch failed (no broadcast created)
    --   cancelled            -> operator/admin cancelled before/while sending
    CONSTRAINT chk_bulk_job_status CHECK (status IN (
        'preview_ready','confirming','sending','completed',
        'completed_with_errors','failed','cancelled')),

    -- Idempotency: one campaign_id per tenant
    CONSTRAINT uq_bulk_job_tenant_campaign UNIQUE (tenant_id, campaign_id)
);

CREATE INDEX IF NOT EXISTS idx_bulk_send_jobs_tenant_created
    ON bulk_send_jobs (tenant_id, created_at DESC);

-- -------------------------------------------------------------
-- bulk_send_recipients: immutable snapshot of the deduped list
-- -------------------------------------------------------------
-- Only VALID, deduped recipients are stored. Dropped rows
-- (invalid/duplicate) are tracked as counts on bulk_send_jobs —
-- we don't persist PII for rows we will never message.
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bulk_send_recipients (
    id                      BIGSERIAL PRIMARY KEY,
    job_id                  BIGINT NOT NULL REFERENCES bulk_send_jobs(id) ON DELETE CASCADE,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    normalized_phone        VARCHAR(20) NOT NULL,
    -- Per-recipient template variables (legacy INSE substitution path).
    -- NULL when the template uses INMA dynamic placeholders (resolved server-side).
    variables_json          JSONB,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Snapshot dedup guarantee: one phone per job (Codex §4B / §5)
    CONSTRAINT uq_bulk_recipient_job_phone UNIQUE (job_id, normalized_phone)
);

CREATE INDEX IF NOT EXISTS idx_bulk_send_recipients_job
    ON bulk_send_recipients (job_id);

-- Grants (production runs as role 'invekto')
GRANT ALL ON bulk_send_jobs TO invekto;
GRANT ALL ON bulk_send_recipients TO invekto;
GRANT ALL ON SEQUENCE bulk_send_jobs_id_seq TO invekto;
GRANT ALL ON SEQUENCE bulk_send_recipients_id_seq TO invekto;
