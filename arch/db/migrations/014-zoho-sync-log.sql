-- Migration 014: Zoho sync attempt log (Adim 3 Paket 1)
-- Purpose: Persist every Gunes -> Zoho sync attempt with status, attempt_count
--          and last_error. Enables manual retry (P3 UI) + cron retry worker (P2).
-- Reference: arch/plans/20260415-zoho-step3-p1-api.json

CREATE TABLE IF NOT EXISTS zoho_sync_log (
    id                    BIGSERIAL PRIMARY KEY,
    tenant_id             INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    gunes_event           VARCHAR(64) NOT NULL,
    gunes_lead_id         VARCHAR(128) NOT NULL,          -- Gunes-side lead identifier (opaque)
    zoho_lead_id          VARCHAR(64),                    -- Zoho CRM Lead id (populated after first sync)
    zoho_transition_id    VARCHAR(64),                    -- transition used for this attempt
    status                VARCHAR(16) NOT NULL
        CHECK (status IN ('pending','success','failed')),
    attempt_count         INTEGER NOT NULL DEFAULT 0,
    last_error_code       VARCHAR(32),                    -- INV-INT-xxx if failed
    last_error_message    TEXT,                           -- truncated Zoho API error / exception
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ                     -- populated on terminal status (success or permanent failed)
);

CREATE INDEX IF NOT EXISTS idx_zoho_sync_log_tenant_status ON zoho_sync_log(tenant_id, status);
CREATE INDEX IF NOT EXISTS idx_zoho_sync_log_retry
    ON zoho_sync_log(status, attempt_count)
    WHERE status = 'failed';
CREATE INDEX IF NOT EXISTS idx_zoho_sync_log_lead ON zoho_sync_log(tenant_id, gunes_lead_id);

-- Partial unique: at most one non-terminal attempt per (tenant, event, lead). Success rows are
-- outside the partial constraint so historical success entries do not block future resync attempts.
-- Gives BeginAttemptAsync a true atomic ON CONFLICT target (prevents concurrent duplicate 'pending' rows).
CREATE UNIQUE INDEX IF NOT EXISTS ux_zoho_sync_log_open_attempt
    ON zoho_sync_log(tenant_id, gunes_event, gunes_lead_id)
    WHERE status IN ('pending','failed');

GRANT ALL ON TABLE zoho_sync_log TO invekto;
GRANT USAGE, SELECT ON SEQUENCE zoho_sync_log_id_seq TO invekto;

COMMENT ON TABLE  zoho_sync_log IS 'Adim 3 P1: Every Gunes -> Zoho sync attempt. Retry source of truth.';
COMMENT ON COLUMN zoho_sync_log.status IS 'pending (queued) | success | failed (retriable until attempt_count >= 3)';
COMMENT ON COLUMN zoho_sync_log.last_error_code IS 'INV-INT-120..125 namespace (Adim 3 P1)';
