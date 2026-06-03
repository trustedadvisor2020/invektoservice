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
    -- FEAT-VCP Chunk B (migration 024): +video_meeting_confirmed, video_reminder_24h, video_reminder_1h
    CONSTRAINT chk_trigger_event CHECK (trigger_event IN (
        'manual', 'new_lead', 'payment_received', 'appointment_reminder',
        'video_meeting_confirmed', 'video_reminder_24h', 'video_reminder_1h'))
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
    -- FEAT-DMP (migration 027): INMA placeholder keys recorded at broadcast-create time so
    -- the callback bridge can forward them to wapPayload.dynamicMessageFields without re-parsing
    -- MessageText on dequeue. NULL = INSE legacy substituted text (pre-DMP rows + legacy path).
    dynamic_fields          TEXT[],
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- status values: queued, sending, sent, delivered, read, failed, blocked
    -- broadcast_id is NULL for trigger-based single messages
    -- FEAT-J2 (migration 025): +'blocked' for INMA 906/907 marketing opt-out rejection
    CONSTRAINT chk_message_status CHECK (status IN ('queued', 'sending', 'sent', 'delivered', 'read', 'failed', 'blocked'))
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
-- inma_optout_outbox: FEAT-J2 outbox for INMA contact opt-out sync
-- =============================================================
-- Eventual-consistency push layer for INSE opt-out state -> INMA contact flags.
-- Enqueue at STOP detect (OptOutManager) + admin manuel opt-out (Backend forward).
-- Drain by InmaOptOutSyncJob (Hangfire recurring, 1-min cron). Source migration:
-- arch/db/migrations/017-inma-optout-outbox.sql.

CREATE TABLE IF NOT EXISTS inma_optout_outbox (
    id                 BIGSERIAL PRIMARY KEY,
    tenant_id          INTEGER     NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    phone              VARCHAR(32) NOT NULL,
    instance_id        INTEGER     NOT NULL,
    event_type         VARCHAR(16) NOT NULL,
    scope              VARCHAR(16) NOT NULL DEFAULT 'all',
    reason             VARCHAR(100),
    source             VARCHAR(32),
    status             VARCHAR(20) NOT NULL DEFAULT 'pending',
    attempts           INTEGER     NOT NULL DEFAULT 0,
    last_status_code   VARCHAR(16),
    last_error         TEXT,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    attempted_at       TIMESTAMPTZ,
    processed_at       TIMESTAMPTZ,

    CONSTRAINT chk_optout_outbox_event_type CHECK (event_type IN ('opt_out', 'opt_in')),
    CONSTRAINT chk_optout_outbox_scope      CHECK (scope IN ('all', 'channel')),
    CONSTRAINT chk_optout_outbox_status     CHECK (status IN ('pending', 'processing', 'processed', 'failed', 'skipped_noop'))
);

-- Idempotency: INMA 909 (AlreadyOptedOut) handles downstream dedup; DB-layer
-- uniqueness skipped because date_trunc / extract(epoch) on TIMESTAMPTZ both
-- violate Postgres' IMMUTABLE requirement for expression / generated-column
-- indexes. See migration 017 for detailed rationale.

CREATE INDEX IF NOT EXISTS idx_inma_optout_outbox_pending
    ON inma_optout_outbox (tenant_id, created_at)
    WHERE status = 'pending';

CREATE INDEX IF NOT EXISTS idx_inma_optout_outbox_skipped_noop
    ON inma_optout_outbox (tenant_id, created_at)
    WHERE status = 'skipped_noop';

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_templates TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_broadcasts TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_messages TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON outbound_optouts TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON inma_optout_outbox TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_templates_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_messages_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE outbound_optouts_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE inma_optout_outbox_id_seq TO invekto;

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

-- =============================================================
-- FEAT-OBI Phase 0 (Migration 051): Bulk Send (CSV source)
-- Parent-job layer over the broadcast engine. See migration 051 for full doc.
-- =============================================================

CREATE TABLE IF NOT EXISTS bulk_send_jobs (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    campaign_id             VARCHAR(80) NOT NULL,
    source                  VARCHAR(16) NOT NULL DEFAULT 'csv',
    template_id             INTEGER NOT NULL REFERENCES outbound_templates(id),
    lang                    VARCHAR(8),
    hard_cap                INTEGER NOT NULL,
    total_input             INTEGER NOT NULL DEFAULT 0,
    total_valid             INTEGER NOT NULL DEFAULT 0,
    total_duplicate         INTEGER NOT NULL DEFAULT 0,
    total_invalid           INTEGER NOT NULL DEFAULT 0,
    total_queued            INTEGER NOT NULL DEFAULT 0,
    total_skipped_optout    INTEGER NOT NULL DEFAULT 0,
    total_skipped_consent   INTEGER NOT NULL DEFAULT 0,
    broadcast_ids           UUID[] NOT NULL DEFAULT '{}',
    dispatch_error          BOOLEAN NOT NULL DEFAULT FALSE,
    status                  VARCHAR(24) NOT NULL DEFAULT 'preview_ready',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_at            TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,
    CONSTRAINT chk_bulk_job_status CHECK (status IN (
        'preview_ready','confirming','sending','completed',
        'completed_with_errors','failed','cancelled')),
    CONSTRAINT uq_bulk_job_tenant_campaign UNIQUE (tenant_id, campaign_id)
);

CREATE INDEX IF NOT EXISTS idx_bulk_send_jobs_tenant_created
    ON bulk_send_jobs (tenant_id, created_at DESC);

CREATE TABLE IF NOT EXISTS bulk_send_recipients (
    id                      BIGSERIAL PRIMARY KEY,
    job_id                  BIGINT NOT NULL REFERENCES bulk_send_jobs(id) ON DELETE CASCADE,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    normalized_phone        VARCHAR(20) NOT NULL,
    variables_json          JSONB,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_bulk_recipient_job_phone UNIQUE (job_id, normalized_phone)
);

CREATE INDEX IF NOT EXISTS idx_bulk_send_recipients_job
    ON bulk_send_recipients (job_id);

GRANT ALL ON bulk_send_jobs TO invekto;
GRANT ALL ON bulk_send_recipients TO invekto;
GRANT ALL ON SEQUENCE bulk_send_jobs_id_seq TO invekto;
GRANT ALL ON SEQUENCE bulk_send_recipients_id_seq TO invekto;

-- =============================================================
-- FEAT-OBI Phase 1A (Migration 052): Contact Lists (data layer)
-- Reusable tenant-scoped contact lists feeding the bulk send engine.
-- NO auto-partition (single per-list cap). Composite FK guarantees
-- cross-tenant integrity. See migration 052 for full doc + verifier.
-- =============================================================

CREATE TABLE IF NOT EXISTS data_lists (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    name                    VARCHAR(255) NOT NULL,
    source                  VARCHAR(16) NOT NULL DEFAULT 'upload',
    active                  BOOLEAN NOT NULL DEFAULT TRUE,
    status                  VARCHAR(16) NOT NULL DEFAULT 'ready',
    total_records           INTEGER NOT NULL DEFAULT 0,
    sendable_count          INTEGER NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT chk_data_list_status CHECK (status IN ('importing','ready','failed')),
    CONSTRAINT chk_data_list_source CHECK (source IN ('upload','export')),
    CONSTRAINT uq_data_lists_tenant_id UNIQUE (tenant_id, id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_data_lists_tenant_name_active
    ON data_lists (tenant_id, lower(btrim(name)))
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_data_lists_tenant_active
    ON data_lists (tenant_id, active, created_at DESC)
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS list_records (
    id                      BIGSERIAL PRIMARY KEY,
    list_id                 BIGINT NOT NULL,
    tenant_id               INTEGER NOT NULL,
    normalized_phone        VARCHAR(20),
    invalid_reason          VARCHAR(64),
    name                    VARCHAR(255),
    surname                 VARCHAR(255),
    email                   VARCHAR(320),
    tags                    TEXT,
    note                    TEXT,
    field1                  VARCHAR(255),
    field2                  VARCHAR(255),
    field3                  VARCHAR(255),
    field4                  VARCHAR(255),
    field5                  VARCHAR(255),
    custom_fields           JSONB,
    sendable                BOOLEAN NOT NULL DEFAULT FALSE,
    first_contact_at        TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_list_records_list
        FOREIGN KEY (tenant_id, list_id)
        REFERENCES data_lists (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT chk_list_record_sendable_phone
        CHECK (sendable = FALSE OR normalized_phone IS NOT NULL),
    CONSTRAINT uq_list_record_list_phone UNIQUE (list_id, normalized_phone)
);

CREATE INDEX IF NOT EXISTS idx_list_records_list
    ON list_records (list_id);
CREATE INDEX IF NOT EXISTS idx_list_records_list_sendable
    ON list_records (list_id) WHERE sendable = TRUE;
CREATE INDEX IF NOT EXISTS idx_list_records_tenant_phone
    ON list_records (tenant_id, normalized_phone);

GRANT ALL ON data_lists TO invekto;
GRANT ALL ON list_records TO invekto;
GRANT ALL ON SEQUENCE data_lists_id_seq TO invekto;
GRANT ALL ON SEQUENCE list_records_id_seq TO invekto;
