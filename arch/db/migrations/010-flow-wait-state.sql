-- Migration 010: Flow wait state persistence (G6)
-- Purpose: Enable restart-safe long waits (up to 30 days) for action_wait_until nodes.
-- Reference: arch/plans/20260413-g6-flow-wait-persistence.json

CREATE TABLE IF NOT EXISTS flow_execution_state (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER     NOT NULL,
    flow_id         INTEGER     NOT NULL,
    chat_id         TEXT        NOT NULL,
    phone           TEXT        NULL,
    instance_id     TEXT        NULL,
    node_id         TEXT        NOT NULL,
    resume_at       TIMESTAMPTZ NOT NULL,
    max_wait_at     TIMESTAMPTZ NOT NULL,
    session_state   JSONB       NOT NULL,
    callback_url    TEXT        NULL,
    status          TEXT        NOT NULL DEFAULT 'pending', -- pending | resumed | cancelled | failed
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resumed_at      TIMESTAMPTZ NULL,
    last_error      TEXT        NULL,
    CONSTRAINT ck_flow_wait_status CHECK (status IN ('pending','resumed','cancelled','failed'))
);

-- Resumer scan: find due rows
CREATE INDEX IF NOT EXISTS ix_flow_wait_due
    ON flow_execution_state (status, resume_at)
    WHERE status = 'pending';

-- Cancel-on-reply + single-active-per-chat invariant
CREATE INDEX IF NOT EXISTS ix_flow_wait_chat
    ON flow_execution_state (tenant_id, chat_id, status);

-- Ops overdue query
CREATE INDEX IF NOT EXISTS ix_flow_wait_created
    ON flow_execution_state (created_at);

COMMENT ON TABLE flow_execution_state IS 'G6: Persistent pause points for action_wait_until nodes. Restart-safe long waits.';
COMMENT ON COLUMN flow_execution_state.session_state IS 'Full SessionStateV2 snapshot at wait boundary (JSON).';
COMMENT ON COLUMN flow_execution_state.max_wait_at IS 'Upper bound (≤30 days from created_at). Rows past this are flagged by ops.';

GRANT ALL ON flow_execution_state TO invekto;
GRANT USAGE, SELECT ON SEQUENCE flow_execution_state_id_seq TO invekto;
