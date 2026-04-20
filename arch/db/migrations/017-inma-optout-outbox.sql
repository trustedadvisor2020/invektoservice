-- Migration 017: FEAT-J2 — INMA opt-out outbox table.
-- Outbox pattern for eventual-consistency push of INSE opt-out state to INMA
-- contact flags. Enqueue happens at STOP detection + admin manuel opt-out;
-- drain handled by InmaOptOutSyncJob (Hangfire recurring, 1 min cron).
--
-- Canonical mirror: arch/db/outbound.sql (additive).
--
-- Idempotent: IF NOT EXISTS table, partial index, constraint name for re-runs.
-- Related: arch/plans/20260417-j2-opt-out-inse-sync.json AC2, AC3.

CREATE TABLE IF NOT EXISTS inma_optout_outbox (
    id                 BIGSERIAL PRIMARY KEY,
    tenant_id          INTEGER     NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    phone              VARCHAR(32) NOT NULL,
    instance_id        INTEGER     NOT NULL,
    event_type         VARCHAR(16) NOT NULL,
    scope              VARCHAR(16) NOT NULL DEFAULT 'all',
    reason             VARCHAR(100),
    source             VARCHAR(32),
    status             VARCHAR(20) NOT NULL DEFAULT 'pending',
    attempts           INTEGER     NOT NULL DEFAULT 0,
    last_status_code   VARCHAR(16),
    last_error         TEXT,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    attempted_at       TIMESTAMPTZ,
    processed_at       TIMESTAMPTZ,

    CONSTRAINT chk_optout_outbox_event_type CHECK (event_type IN ('opt_out', 'opt_in')),
    CONSTRAINT chk_optout_outbox_scope      CHECK (scope IN ('all', 'channel')),
    -- 'processing' is the atomic claim state between FetchPendingOutboxBatch
    -- (UPDATE ... RETURNING) and MarkOutboxProcessed/Failed. Multi-instance
    -- workers never pick the same row because the UPDATE is atomic at the DB.
    CONSTRAINT chk_optout_outbox_status     CHECK (status IN ('pending', 'processing', 'processed', 'failed', 'skipped_noop'))
);

-- Idempotency at enqueue: same tenant+phone+event_type within the same second
-- collapses into a single row (ON CONFLICT DO NOTHING in repository).
CREATE UNIQUE INDEX IF NOT EXISTS uq_inma_optout_outbox_dedup
    ON inma_optout_outbox (tenant_id, phone, event_type, date_trunc('second', created_at));

-- Hot path for FetchPendingOutboxBatchAsync drain job.
CREATE INDEX IF NOT EXISTS idx_inma_optout_outbox_pending
    ON inma_optout_outbox (tenant_id, created_at)
    WHERE status = 'pending';

-- Admin ops retry-skipped drain target.
CREATE INDEX IF NOT EXISTS idx_inma_optout_outbox_skipped_noop
    ON inma_optout_outbox (tenant_id, created_at)
    WHERE status = 'skipped_noop';

GRANT SELECT, INSERT, UPDATE, DELETE ON inma_optout_outbox TO invekto;
GRANT USAGE, SELECT ON SEQUENCE inma_optout_outbox_id_seq TO invekto;
