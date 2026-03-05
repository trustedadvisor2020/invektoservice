-- Migration 007: Flow Versions
-- Feature: Flow Monitor & Versioning (SPEC-FM)
-- Author: Q | Date: 2026-03-05
-- Owner: Automation service

-- ============================================================
-- flow_versions: Her flow save'inde otomatik surum snapshot'i
-- ============================================================

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

-- Flow basina unique version numarasi
CREATE UNIQUE INDEX IF NOT EXISTS uq_flow_versions
    ON flow_versions (flow_id, version_number);

-- Son surumleri hizli cekme
CREATE INDEX IF NOT EXISTS idx_flow_versions_flow
    ON flow_versions (flow_id, version_number DESC);

-- Tenant bazli sorgular
CREATE INDEX IF NOT EXISTS idx_flow_versions_tenant
    ON flow_versions (tenant_id);

GRANT ALL ON flow_versions TO invekto;
GRANT USAGE, SELECT ON SEQUENCE flow_versions_id_seq TO invekto;

-- ============================================================
-- chatbot_flows: current_version kolonu ekle
-- ============================================================

ALTER TABLE chatbot_flows
    ADD COLUMN IF NOT EXISTS current_version INTEGER NOT NULL DEFAULT 0;

-- COMMENT: current_version = aktif version_number (0 = versioning oncesi)
