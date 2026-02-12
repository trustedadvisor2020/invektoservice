-- =============================================================
-- Invekto.Outbound Database Schema
-- Service: Invekto.Outbound (port 7107)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- =============================================================

-- Depends on: tenant-registry.sql (tenant_registry table)

-- =============================================================
-- outbound_templates: Message templates for broadcasts & triggers
-- =============================================================

CREATE TABLE IF NOT EXISTS outbound_templates (
    id                      SERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    name                    VARCHAR(200) NOT NULL,
    trigger_event           VARCHAR(50) NOT NULL DEFAULT 'manual',
    message_template        TEXT NOT NULL,
    variables_json          JSONB,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- trigger_event values: manual, new_lead, payment_received, appointment_reminder
    CONSTRAINT chk_trigger_event CHECK (trigger_event IN ('manual', 'new_lead', 'payment_received', 'appointment_reminder'))
);

CREATE INDEX IF NOT EXISTS idx_outbound_templates_tenant_active
    ON outbound_templates (tenant_id, is_active) WHERE is_active = TRUE;

CREATE INDEX IF NOT EXISTS idx_outbound_templates_tenant_trigger
    ON outbound_templates (tenant_id, trigger_event) WHERE is_active = TRUE;

-- =============================================================
-- outbound_broadcasts: Broadcast job records
-- =============================================================

CREATE TABLE IF NOT EXISTS outbound_broadcasts (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    template_id             INTEGER REFERENCES outbound_templates(id),
    total_recipients        INTEGER NOT NULL DEFAULT 0,
    queued                  INTEGER NOT NULL DEFAULT 0,
    sent                    INTEGER NOT NULL DEFAULT 0,
    delivered               INTEGER NOT NULL DEFAULT 0,
    read                    INTEGER NOT NULL DEFAULT 0,
    failed                  INTEGER NOT NULL DEFAULT 0,
    status                  VARCHAR(20) NOT NULL DEFAULT 'queued',
    scheduled_at            TIMESTAMPTZ,
    started_at              TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- status values: queued, processing, completed, failed
    CONSTRAINT chk_broadcast_status CHECK (status IN ('queued', 'processing', 'completed', 'failed'))
);

CREATE INDEX IF NOT EXISTS idx_outbound_broadcasts_tenant_created
    ON outbound_broadcasts (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_outbound_broadcasts_status
    ON outbound_broadcasts (status) WHERE status IN ('queued', 'processing');

-- =============================================================
-- outbound_messages: Individual message records in a broadcast
-- =============================================================

CREATE TABLE IF NOT EXISTS outbound_messages (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    broadcast_id            UUID REFERENCES outbound_broadcasts(id),
    template_id             INTEGER REFERENCES outbound_templates(id),
    recipient_phone         VARCHAR(20) NOT NULL,
    message_text            TEXT NOT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'queued',
    external_message_id     VARCHAR(100),
    sent_at                 TIMESTAMPTZ,
    delivered_at            TIMESTAMPTZ,
    read_at                 TIMESTAMPTZ,
    failed_reason           VARCHAR(500),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- status values: queued, sending, sent, delivered, read, failed
    -- broadcast_id is NULL for trigger-based single messages
    CONSTRAINT chk_message_status CHECK (status IN ('queued', 'sending', 'sent', 'delivered', 'read', 'failed'))
);

CREATE INDEX IF NOT EXISTS idx_outbound_messages_tenant_created
    ON outbound_messages (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_outbound_messages_broadcast_status
    ON outbound_messages (broadcast_id, status);

CREATE INDEX IF NOT EXISTS idx_outbound_messages_queued
    ON outbound_messages (status, created_at) WHERE status = 'queued';

CREATE INDEX IF NOT EXISTS idx_outbound_messages_external_id
    ON outbound_messages (external_message_id) WHERE external_message_id IS NOT NULL;

-- =============================================================
-- outbound_optouts: Opt-out registry per tenant+phone
-- =============================================================

CREATE TABLE IF NOT EXISTS outbound_optouts (
    id                      SERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    phone                   VARCHAR(20) NOT NULL,
    reason                  VARCHAR(200),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- One opt-out record per phone per tenant
    CONSTRAINT uq_optout_tenant_phone UNIQUE (tenant_id, phone)
);

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_templates TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_broadcasts TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_messages TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_optouts TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_templates_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_messages_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_optouts_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. outbound_templates: trigger_event='manual' for broadcast-only templates
-- 2. outbound_broadcasts: counter columns (queued/sent/delivered/read/failed)
--    are updated atomically via UPDATE ... SET sent = sent + 1
-- 3. outbound_messages: broadcast_id is NULL for trigger-based single messages
-- 4. outbound_optouts: checked before every message send, UNIQUE prevents duplicates
-- 5. Rate limiting is handled in-memory (per tenant msg/minute), not in DB
-- 6. external_message_id links to WapCRM/WhatsApp message ID for delivery tracking
-- =============================================================
