-- flow_execution_log: Tracks each flow execution with full node trace
-- Owner: Automation service
-- Created: 2026-02-23

CREATE TABLE IF NOT EXISTS flow_execution_log (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    flow_id         INTEGER NOT NULL,
    chat_id         VARCHAR(50),
    phone           VARCHAR(20),
    instance_id     VARCHAR(100),
    trigger_message TEXT,
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,
    status          VARCHAR(20) NOT NULL DEFAULT 'running',  -- running | completed | error | handed_off | waiting
    node_trace      JSONB NOT NULL DEFAULT '[]'::jsonb,
    variables_final JSONB,
    error_detail    TEXT,
    CONSTRAINT fk_fel_tenant FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT fk_fel_flow FOREIGN KEY (flow_id) REFERENCES chatbot_flows(flow_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_fel_flow ON flow_execution_log(flow_id, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_fel_tenant ON flow_execution_log(tenant_id, started_at DESC);

GRANT ALL ON flow_execution_log TO invekto;
GRANT USAGE, SELECT ON SEQUENCE flow_execution_log_id_seq TO invekto;
