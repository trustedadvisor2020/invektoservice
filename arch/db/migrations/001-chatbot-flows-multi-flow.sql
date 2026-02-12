-- ============================================================
-- Migration 001: chatbot_flows v1 -> multi-flow
-- Date: 2026-02-12
-- Description: PK change from tenant_id to flow_id SERIAL,
--              add flow_name, is_default columns,
--              add partial unique indexes
-- Precondition: chatbot_flows table exists with tenant_id as PK
-- ============================================================

-- Step 1: Drop old PK
ALTER TABLE chatbot_flows DROP CONSTRAINT chatbot_flows_pkey;

-- Step 2: Add new columns
ALTER TABLE chatbot_flows ADD COLUMN flow_id SERIAL;
ALTER TABLE chatbot_flows ADD COLUMN flow_name VARCHAR(200) NOT NULL DEFAULT 'Ana Flow';
ALTER TABLE chatbot_flows ADD COLUMN is_default BOOLEAN NOT NULL DEFAULT false;

-- Step 3: New PK
ALTER TABLE chatbot_flows ADD PRIMARY KEY (flow_id);

-- Step 4: Indexes
CREATE INDEX IF NOT EXISTS idx_chatbot_flows_tenant ON chatbot_flows(tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_active ON chatbot_flows(tenant_id) WHERE is_active = true;
CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_name ON chatbot_flows(tenant_id, flow_name);

-- Step 5: Mark existing rows as default
UPDATE chatbot_flows SET is_default = true;

-- Step 6: Permissions
GRANT ALL ON chatbot_flows TO invekto;
GRANT USAGE, SELECT ON SEQUENCE chatbot_flows_flow_id_seq TO invekto;

-- Verify
SELECT flow_id, tenant_id, flow_name, is_active, is_default FROM chatbot_flows;
