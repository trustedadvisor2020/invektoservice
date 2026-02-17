-- ============================================================
-- Outbound v2 DB Schema Extensions
-- PKT-5A: GR-3.15 + GR-3.26 + GR-3.29
-- ============================================================

-- Campaigns: scheduled/recurring/event-based message campaigns
CREATE TABLE outbound_campaigns (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    name                VARCHAR(200) NOT NULL,
    trigger_type        VARCHAR(50) NOT NULL,        -- 'manual', 'scheduled', 'event_based', 'recurring'
    target_criteria_json JSONB DEFAULT '{}',          -- audience targeting rules
    template_id         INTEGER REFERENCES outbound_templates(id),
    ab_template_id      INTEGER REFERENCES outbound_templates(id),  -- A/B variant B
    ab_split_pct        INTEGER DEFAULT 50 CHECK (ab_split_pct BETWEEN 0 AND 100),
    schedule_json       JSONB,                       -- {"start_at":"...", "recurring_cron":"...", "end_at":"..."}
    status              VARCHAR(20) NOT NULL DEFAULT 'draft',  -- draft, scheduled, active, paused, completed, archived
    stats_json          JSONB DEFAULT '{}',          -- {"sent":0, "delivered":0, "read":0, "failed":0, "converted":0}
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_campaigns_tenant_status ON outbound_campaigns(tenant_id, status);

-- Conversions: message -> action tracking
CREATE TABLE outbound_conversions (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    message_id          BIGINT REFERENCES outbound_messages(id),
    campaign_id         INTEGER REFERENCES outbound_campaigns(id),
    conversion_type     VARCHAR(50) NOT NULL,        -- 'reply', 'purchase', 'appointment', 'click', 'custom'
    value_amount        DECIMAL(12,2),
    metadata_json       JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_conversions_tenant_campaign ON outbound_conversions(tenant_id, campaign_id);
CREATE INDEX idx_conversions_tenant_type ON outbound_conversions(tenant_id, conversion_type);

-- Consent records: opt-in / opt-out tracking (GR-3.26)
CREATE TABLE consent_records (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    customer_phone      VARCHAR(20) NOT NULL,
    consent_type        VARCHAR(50) NOT NULL,        -- 'marketing', 'utility', 'all'
    channel             VARCHAR(50) NOT NULL,        -- 'whatsapp', 'web_form', 'order_confirmation', 'appointment'
    source              VARCHAR(200),                -- description of where consent was collected
    opted_in            BOOLEAN NOT NULL DEFAULT TRUE,
    opted_in_at         TIMESTAMPTZ,
    opted_out_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_consent_tenant_phone ON consent_records(tenant_id, customer_phone);
CREATE UNIQUE INDEX idx_consent_tenant_phone_type ON consent_records(tenant_id, customer_phone, consent_type);

-- Template audit trail: every sent message logged for compliance (GR-3.29)
CREATE TABLE template_audit_trail (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    template_id         INTEGER REFERENCES outbound_templates(id),
    campaign_id         INTEGER REFERENCES outbound_campaigns(id),
    recipient_phone     VARCHAR(20) NOT NULL,
    template_content    TEXT NOT NULL,
    sent_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_trail_tenant_phone ON template_audit_trail(tenant_id, recipient_phone);
CREATE INDEX idx_audit_trail_tenant_date ON template_audit_trail(tenant_id, sent_at);

-- Data deletion requests: KVKK/GDPR veri silme (GR-3.29)
CREATE TABLE data_deletion_requests (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    customer_phone      VARCHAR(20) NOT NULL,
    requested_by        VARCHAR(100),
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending, processing, completed, failed
    services_cleaned    JSONB DEFAULT '[]',          -- ["consent_records", "template_audit_trail", "outbound_messages"]
    error_message       TEXT,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_deletion_tenant_status ON data_deletion_requests(tenant_id, status);

-- Grants
GRANT ALL ON outbound_campaigns TO invekto;
GRANT ALL ON outbound_conversions TO invekto;
GRANT ALL ON consent_records TO invekto;
GRANT ALL ON template_audit_trail TO invekto;
GRANT ALL ON data_deletion_requests TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
