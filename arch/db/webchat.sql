-- =============================================================
-- Invekto.WebChat Database Schema
-- Service: Invekto.WebChat.Gateway (port 7113, renamed 2026-05-10)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
--
-- SPEC: arch/specs/webchat-inma-channel.md (2026-05-10)
-- Q karari: WebChat artik INMA'nin kanal tipi. INMA = SSoT for messages.
--
-- ACTIVE TABLES (Phase 1+):
--   - webchat_widget_configs (extended via migration 047)
--   - webchat_cutover_state (migration 047)
--   - webchat_inma_idempotency (migration 047)
--
-- DEPRECATED TABLES (read-only after T+7, drop at T+90):
--   - webchat_visitors      → INMA contact (channel=WEBCHAT)
--   - webchat_conversations → INMA conversation
--   - webchat_messages      → INMA message
--   - webchat_push_tokens   → INMA push (drop at T+0)
-- =============================================================

-- =============================================================
-- DEPRECATED: webchat_visitors (T+7 read-only, T+90 drop)
-- Replacement: INMA contact tablosu (channel_type=WEBCHAT, external_id=wc-{w}-{v})
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_visitors (
    id              TEXT PRIMARY KEY,                    -- UUID stored in browser localStorage
    name            TEXT,                                -- Optional, asked at chat start
    email           TEXT,                                -- Optional, asked at chat start
    first_seen      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    page_url        TEXT,                                -- Last visited page URL
    user_agent      TEXT                                 -- Browser user agent
);

-- =============================================================
-- DEPRECATED: webchat_conversations (T+7 read-only, T+90 drop)
-- Replacement: INMA conversation tablosu
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_conversations (
    id              BIGSERIAL PRIMARY KEY,
    visitor_id      TEXT NOT NULL REFERENCES webchat_visitors(id),
    widget_id       TEXT,                                     -- Links to webchat_widget_configs
    status          VARCHAR(20) NOT NULL DEFAULT 'active',    -- active | ai | closed
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at       TIMESTAMPTZ,
    last_message_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ai_active       BOOLEAN NOT NULL DEFAULT TRUE             -- AI auto-reply enabled
);

CREATE INDEX ix_webchat_conversations_status ON webchat_conversations(status);
CREATE INDEX ix_webchat_conversations_visitor ON webchat_conversations(visitor_id);

-- =============================================================
-- DEPRECATED: webchat_messages (T+7 read-only, T+90 drop)
-- Replacement: INMA message tablosu (POST /api/v1/inbound/webchat)
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_messages (
    id              BIGSERIAL PRIMARY KEY,
    conversation_id BIGINT NOT NULL REFERENCES webchat_conversations(id),
    sender_type     VARCHAR(20) NOT NULL,                     -- visitor | operator | ai
    content         TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_webchat_messages_conv ON webchat_messages(conversation_id, created_at);

-- =============================================================
-- webchat_push_tokens: Expo push notification tokens for operator app
-- =============================================================

-- =============================================================
-- ACTIVE: webchat_widget_configs (Phase 1)
--
-- Each widget instance is one webchat surface (one website embed).
-- Tenant can create multiple widgets, each with own language/flow/origins.
-- flow_id = 0 means that Invekto-side Automation event is disabled
-- (Phase 2: Automation flows migrate to INMA).
--
-- Phase 1 columns (display_name, primary_locale, allowed_origins,
-- inma_synced_at, inma_sync_error) added via migration 047.
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_widget_configs (
    widget_id                   TEXT PRIMARY KEY,
    tenant_id                   INTEGER NOT NULL,
    flow_conversation_created   INTEGER NOT NULL DEFAULT 0,
    flow_visitor_message        INTEGER NOT NULL DEFAULT 0,
    flow_conversation_closed    INTEGER NOT NULL DEFAULT 0,
    is_active                   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    -- Phase 1 (SPEC-WC-INMA, migration 047)
    display_name                TEXT NOT NULL DEFAULT 'WebChat Widget',
    primary_locale              VARCHAR(10) NOT NULL DEFAULT 'tr-TR',
    allowed_origins             JSONB NOT NULL DEFAULT '[]'::jsonb,
    inma_synced_at              TIMESTAMPTZ,
    inma_sync_error             TEXT
);

CREATE INDEX IF NOT EXISTS ix_webchat_widget_configs_tenant
    ON webchat_widget_configs(tenant_id);

CREATE INDEX IF NOT EXISTS ix_webchat_widget_configs_inma_sync_pending
    ON webchat_widget_configs(inma_synced_at) WHERE inma_synced_at IS NULL;

-- =============================================================
-- DEPRECATED: webchat_push_tokens (drop at T+0 cutover)
-- Replacement: INMA push notification system (operator app integration)
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_push_tokens (
    id              BIGSERIAL PRIMARY KEY,
    token           TEXT NOT NULL UNIQUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =============================================================
-- ACTIVE: webchat_cutover_state (Phase 1 — added via migration 047)
-- Single-row control table for cutover/rollback orchestration.
-- Bridge fix gate: feature_flag_enabled requires bridge_fix_verified=TRUE.
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_cutover_state (
    id                          INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    feature_flag_enabled        BOOLEAN NOT NULL DEFAULT FALSE,
    bridge_fix_verified         BOOLEAN NOT NULL DEFAULT FALSE,
    bridge_fix_verified_at      TIMESTAMPTZ,
    dual_write_started_at       TIMESTAMPTZ,
    legacy_readonly_at          TIMESTAMPTZ,
    legacy_dropped_at           TIMESTAMPTZ,
    notes                       TEXT,
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_bridge_gate
        CHECK (feature_flag_enabled = FALSE OR bridge_fix_verified = TRUE)
);

-- =============================================================
-- ACTIVE: webchat_inma_idempotency (Phase 1 — added via migration 047)
-- Tenant-scoped messageId dedupe. 24h TTL via Hangfire scheduled job.
-- Compound PK (tenant_id, message_id) for tenant isolation.
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_inma_idempotency (
    tenant_id       INTEGER NOT NULL,
    message_id      UUID NOT NULL,
    widget_id       TEXT NOT NULL,
    forwarded_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    inma_response   JSONB,
    PRIMARY KEY (tenant_id, message_id)
);

CREATE INDEX IF NOT EXISTS ix_webchat_inma_idempotency_ttl
    ON webchat_inma_idempotency(forwarded_at);

CREATE INDEX IF NOT EXISTS ix_webchat_inma_idempotency_tenant
    ON webchat_inma_idempotency(tenant_id, forwarded_at DESC);

-- =============================================================
-- ACTIVE: webchat_outbox (Phase 1 — added via migration 047)
-- Outbox Pattern for 7-day dual-write reconciliation.
-- Hangfire reconciliation cron every 5min retries pending entries.
-- Alert threshold: pending count > 100 → INV-WC-025
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_outbox (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    widget_id       TEXT NOT NULL,
    message_id      UUID NOT NULL,
    visitor_key     TEXT NOT NULL,
    payload         JSONB NOT NULL,
    inma_status     VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending | sent | failed | skipped
    legacy_status   VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending | sent | failed | skipped
    inma_sent_at    TIMESTAMPTZ,
    legacy_sent_at  TIMESTAMPTZ,
    last_error      TEXT,
    retry_count     INTEGER NOT NULL DEFAULT 0,
    next_retry_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_webchat_outbox_pending
    ON webchat_outbox(next_retry_at, retry_count)
    WHERE inma_status = 'pending' OR legacy_status = 'pending';

-- Invariant: pending rows MUST have retry_count <= 5 (no silent stuck state)
-- Idempotent (existence check via pg_constraint)
DO $constraint_outbox$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_outbox_no_silent_stuck'
          AND conrelid = 'webchat_outbox'::regclass
    ) THEN
        ALTER TABLE webchat_outbox
            ADD CONSTRAINT chk_outbox_no_silent_stuck
                CHECK (
                    NOT (
                        (inma_status = 'pending' AND retry_count > 5)
                        OR (legacy_status = 'pending' AND retry_count > 5)
                    )
                );
    END IF;
END $constraint_outbox$;

CREATE INDEX IF NOT EXISTS ix_webchat_outbox_tenant
    ON webchat_outbox(tenant_id, created_at DESC);

CREATE UNIQUE INDEX IF NOT EXISTS ux_webchat_outbox_msg
    ON webchat_outbox(tenant_id, message_id);

-- =============================================================
-- GRANTS (Postgres convention for shared invekto DB)
-- =============================================================
GRANT ALL ON webchat_cutover_state TO PUBLIC;
GRANT ALL ON webchat_inma_idempotency TO PUBLIC;
GRANT ALL ON webchat_outbox TO PUBLIC;
GRANT ALL ON SEQUENCE webchat_outbox_id_seq TO PUBLIC;
