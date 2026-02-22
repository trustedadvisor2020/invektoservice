-- Instance Filtering & Multi-Flow Routing
-- Database: invekto (same as tenant_registry, chatbot_flows)
-- Table: tenant_instances — WapCRM instance cache + flow assignment per tenant

-- ============================================================
-- 1. tenant_instances: Tenant bazli WapCRM hat yonetimi
--    Her tenant icin WapCRM'den cekilen instance'lar cache'lenir.
--    is_enabled: Tenant admin hat bazli mesaj filtresi yapar.
--    flow_id: Instance hangi flow'a atanmis (NULL = atanmamis).
-- ============================================================
CREATE TABLE IF NOT EXISTS tenant_instances (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    instance_id     VARCHAR(100) NOT NULL,          -- WapCRM instanceID (string form)
    instance_name   VARCHAR(200) NOT NULL,          -- WapCRM instanceName (display label)
    account         VARCHAR(50),                    -- WapCRM account (telefon numarasi)
    instance_type   INTEGER NOT NULL DEFAULT 1,     -- WapCRM instanceType (1=Cloud API, 2=Web, 5=Kanal, 6=SMS)
    is_enabled      BOOLEAN NOT NULL DEFAULT true,  -- Tenant-controlled on/off
    flow_id         INTEGER,                        -- Assigned flow (NULL = unassigned)
    fetched_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_ti_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT fk_ti_flow
        FOREIGN KEY (flow_id) REFERENCES chatbot_flows(flow_id) ON DELETE SET NULL,
    CONSTRAINT uq_ti_instance
        UNIQUE (tenant_id, instance_id)
);

-- Routing index: tenant + instance + enabled hizli lookup
CREATE INDEX IF NOT EXISTS idx_ti_routing
    ON tenant_instances (tenant_id, instance_id, is_enabled)
    WHERE is_enabled = true;

-- Tenant bazli listeleme
CREATE INDEX IF NOT EXISTS idx_ti_tenant
    ON tenant_instances (tenant_id);

GRANT ALL ON tenant_instances TO invekto;
GRANT USAGE, SELECT ON SEQUENCE tenant_instances_id_seq TO invekto;

-- ============================================================
-- 2. MIGRATION: Multi-active flow (uq_chatbot_flows_active DROP)
--    Partial unique index tenant basina 1 aktif flow zorluyor.
--    Instance bazli routing icin birden fazla flow aktif olabilmeli.
--    PRODUCTION'DA ONCE CALISTIRILMALI!
-- ============================================================
DROP INDEX IF EXISTS uq_chatbot_flows_active;
