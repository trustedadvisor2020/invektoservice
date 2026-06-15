-- Migration 066 — FEAT-INMA-PIPELINE-V2 C3 HARDENING: customer_status_changed flow-trigger outbox
-- ----------------------------------------------------------------------------
-- Closes the C2->C3a durability hole. Before this, the flow-trigger Hangfire enqueue happened
-- AFTER the C2 persist tx committed (Backend Program.cs DispatchCustomerStatusFlowTriggers): a crash
-- or enqueue failure between commit and enqueue LOST the trigger permanently — INMA already got 200,
-- and C2's ON CONFLICT DO NOTHING means a re-delivery never re-dispatches.
--
-- Transactional outbox: the dispatch intent is now INSERTed INSIDE the C2 tx (one row per CHANGED
-- lead, only when the event-level suppression decision == Fire). A Backend timer worker
-- (CustomerStatusFlowOutboxDrainJob) drains it and enqueues TriggerCustomerStatusFlowJob. The drained
-- job claims the row (processing->done) before it runs so a re-enqueue (stale recovery) cannot
-- double-fire the flow (exactly-once-at-start — no duplicate WhatsApp send).
--
-- Idempotency is FOLDED into this table: UNIQUE(tenant_id, event_id, lead_id). event_id is the natural
-- idempotency key carried from C2's inma_webhook_events; lead_id is added because one event's phones[]
-- can fan out to MULTIPLE leads. The event carries a SINGLE feature_group_id/customer_id, so one event
-- can change a given lead at most once -> (tenant_id, event_id, lead_id) cannot collapse legitimate rows.
--
-- Mirrors the proven inma_optout_outbox (migration 017) drain pattern (status lifecycle + attempts +
-- FOR UPDATE SKIP LOCKED claim + startup stale-processing recovery). Backend C2 tables are migration-only
-- (inma_webhook_events / leads.customer_status, migration 065) — no canonical arch/db/*.sql mirror exists
-- for them, so this table is migration-only too (attested in /rev per CQ11).
--
-- Additive + idempotent (IF NOT EXISTS). No existing table/data is touched.
-- ----------------------------------------------------------------------------

BEGIN;

CREATE TABLE IF NOT EXISTS customer_status_flow_outbox (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id           INTEGER     NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    -- The INMA event id (inma_webhook_events.event_id) — the idempotency anchor carried from C2.
    event_id            TEXT        NOT NULL,
    -- The changed lead. CASCADE so a lead deletion cannot strand an undispatched outbox row.
    lead_id             INTEGER     NOT NULL REFERENCES leads(id) ON DELETE CASCADE,
    -- Scalar copies of the TriggerCustomerStatusFlowJob args (so the drain needs no re-derivation).
    new_status          TEXT,
    old_status          TEXT,
    feature_group_id    INTEGER,
    feature_group_name  TEXT,
    changed_by          TEXT,
    customer_id         INTEGER,
    phone               TEXT,
    -- 'processing' is the atomic claim state between FetchPendingBatch (drain UPDATE...RETURNING) and the
    -- drained JOB's terminal claim (processing->'done'). Multi-instance drains never pick the same row
    -- because the claim UPDATE is atomic at the DB (FOR UPDATE SKIP LOCKED). 'failed' = enqueue retries
    -- exhausted (INV-INM-009). The JOB, not the drain, writes 'done' so a re-enqueue cannot double-fire.
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    attempts            INTEGER     NOT NULL DEFAULT 0,
    last_error          TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    attempted_at        TIMESTAMPTZ,
    processed_at        TIMESTAMPTZ,

    CONSTRAINT uq_customer_status_flow_outbox UNIQUE (tenant_id, event_id, lead_id),
    CONSTRAINT chk_customer_status_flow_outbox_status
        CHECK (status IN ('pending', 'processing', 'done', 'failed'))
);

-- Hot path for the drain's FetchPendingBatch claim (status='pending', oldest first).
CREATE INDEX IF NOT EXISTS idx_customer_status_flow_outbox_pending
    ON customer_status_flow_outbox (created_at)
    WHERE status = 'pending';

-- Stale-recovery scan target (revert crashed-worker 'processing' rows to 'pending' at startup).
CREATE INDEX IF NOT EXISTS idx_customer_status_flow_outbox_processing
    ON customer_status_flow_outbox (attempted_at)
    WHERE status = 'processing';

-- Mandatory grant (new table) — the app role is 'invekto'.
GRANT ALL ON customer_status_flow_outbox TO invekto;

-- Fail loud if anything did not materialise.
DO $verify$
BEGIN
    IF to_regclass('public.customer_status_flow_outbox') IS NULL THEN
        RAISE EXCEPTION '[INV-SEED-066] customer_status_flow_outbox table missing after migration 066';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_customer_status_flow_outbox'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-066] uq_customer_status_flow_outbox UNIQUE(tenant_id,event_id,lead_id) missing after migration 066';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_customer_status_flow_outbox_status'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-066] chk_customer_status_flow_outbox_status CHECK missing after migration 066';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE indexname = 'idx_customer_status_flow_outbox_pending'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-066] idx_customer_status_flow_outbox_pending missing after migration 066';
    END IF;
END
$verify$;

COMMIT;
