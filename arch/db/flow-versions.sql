-- flow_versions: Flow surum snapshot'lari
-- Owner: Automation service
-- Created: 2026-03-05
-- Spec: SPEC-FM (Flow Monitor & Versioning)

CREATE TABLE IF NOT EXISTS flow_versions (
    id              SERIAL PRIMARY KEY,
    flow_id         INTEGER NOT NULL,
    tenant_id       INTEGER NOT NULL,
    version_number  INTEGER NOT NULL,                   -- Auto-increment per flow (1, 2, 3...)
    flow_config     JSONB NOT NULL,                     -- Full flow_config snapshot
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(100),                       -- 'user' | 'ai' | 'rollback' | 'migration'
    CONSTRAINT fk_fv_flow FOREIGN KEY (flow_id) REFERENCES chatbot_flows(flow_id) ON DELETE CASCADE,
    CONSTRAINT fk_fv_tenant FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_flow_versions
    ON flow_versions (flow_id, version_number);

CREATE INDEX IF NOT EXISTS idx_flow_versions_flow
    ON flow_versions (flow_id, version_number DESC);

CREATE INDEX IF NOT EXISTS idx_flow_versions_tenant
    ON flow_versions (tenant_id);

GRANT ALL ON flow_versions TO invekto;
GRANT USAGE, SELECT ON SEQUENCE flow_versions_id_seq TO invekto;
