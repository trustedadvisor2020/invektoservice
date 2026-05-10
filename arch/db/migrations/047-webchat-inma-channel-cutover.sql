-- Migration 047: WebChat as INMA Channel — Phase 1 Cutover Schema
--
-- SPEC: arch/specs/webchat-inma-channel.md
-- CONTRACT: arch/contracts/webchat-inma-channel.json
-- Q karari 2026-05-10: WebChat artik INMA'nin yeni kanal tipi (WABA/IG gibi).
-- Invekto.WebChat.Gateway sadece browser-INMA koprusu olur.
-- INMA = single source of truth for messages.
--
-- Bu migration:
-- §1. webchat_widget_configs Phase 1 alanlari (display_name, primary_locale, allowed_origins)
-- §2. webchat_cutover_state — cutover/rollback control + bridge fix gate
-- §3. webchat_inma_idempotency — tenant-scoped, Hangfire TTL cleanup
-- §4. webchat_outbox — dual-write reconciliation (Outbox Pattern, Q karari)
-- §5. GRANT ALL on new tables (Postgres convention)
-- §6. Postcondition guard
--
-- Idempotency: ADD COLUMN IF NOT EXISTS + CREATE TABLE IF NOT EXISTS, re-run safe.

-- =============================================================
-- §1. webchat_widget_configs Phase 1 alanlari
-- =============================================================
ALTER TABLE webchat_widget_configs
    ADD COLUMN IF NOT EXISTS display_name      TEXT NOT NULL DEFAULT 'WebChat Widget',
    ADD COLUMN IF NOT EXISTS primary_locale    VARCHAR(10) NOT NULL DEFAULT 'tr-TR',
    ADD COLUMN IF NOT EXISTS allowed_origins   JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS inma_synced_at    TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS inma_sync_error   TEXT;

CREATE INDEX IF NOT EXISTS ix_webchat_widget_configs_tenant
    ON webchat_widget_configs(tenant_id);

CREATE INDEX IF NOT EXISTS ix_webchat_widget_configs_inma_sync_pending
    ON webchat_widget_configs(inma_synced_at) WHERE inma_synced_at IS NULL;

-- =============================================================
-- §2. webchat_cutover_state: feature flag + bridge fix gate
--
-- Boot-time check: feature_flag_enabled requires bridge_fix_verified=TRUE
-- (PKT-BRIDGE-906-907-FIX prerequisite enforcement)
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
    -- Gate constraint: cannot enable feature flag without bridge fix
    CONSTRAINT chk_bridge_gate
        CHECK (feature_flag_enabled = FALSE OR bridge_fix_verified = TRUE)
);

INSERT INTO webchat_cutover_state (id, feature_flag_enabled, bridge_fix_verified, notes)
VALUES (1, FALSE, FALSE, 'Phase 1 init — flag OFF until INMA endpoints ready and bridge fix verified')
ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- §3. webchat_inma_idempotency: dedupe with TENANT scope
--
-- Compound PK (tenant_id, message_id) for tenant isolation.
-- 24h TTL via Hangfire scheduled job (Q karari):
--   DELETE FROM webchat_inma_idempotency WHERE forwarded_at < NOW() - INTERVAL '24 hours'
-- =============================================================
CREATE TABLE IF NOT EXISTS webchat_inma_idempotency (
    tenant_id       INTEGER NOT NULL,
    message_id      UUID NOT NULL,
    widget_id       TEXT NOT NULL,
    forwarded_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    inma_response   JSONB,
    PRIMARY KEY (tenant_id, message_id)
);

-- Cleanup index for Hangfire job (24h TTL DELETE WHERE forwarded_at < NOW() - INTERVAL '24h')
CREATE INDEX IF NOT EXISTS ix_webchat_inma_idempotency_ttl
    ON webchat_inma_idempotency(forwarded_at);

-- Tenant-scoped lookup index
CREATE INDEX IF NOT EXISTS ix_webchat_inma_idempotency_tenant
    ON webchat_inma_idempotency(tenant_id, forwarded_at DESC);

-- =============================================================
-- §4. webchat_outbox: dual-write reconciliation (Outbox Pattern)
--
-- Q karari: 7-day dual-write window + Outbox + periodic reconciliation cron.
-- Hangfire job (every 5min): SELECT * WHERE pending AND retry_count <= 5 AND next_retry_at <= NOW()
--   → retry failed sends, alert if pending count > 100 (INV-WC-025)
-- Terminal: 6th attempt fail (retry_count was 5) → atomic UPDATE retry_count=6, status='failed'.
-- chk_outbox_no_silent_stuck constraint forbids pending+retry_count>5 (no silent stuck row).
-- =============================================================
CREATE TABLE IF NOT EXISTS webchat_outbox (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    widget_id       TEXT NOT NULL,
    message_id      UUID NOT NULL,
    visitor_key     TEXT NOT NULL,
    payload         JSONB NOT NULL,
    -- Dual-write status: each path tracked separately
    inma_status     VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending | sent | failed | skipped
    legacy_status   VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending | sent | failed | skipped
    inma_sent_at    TIMESTAMPTZ,
    legacy_sent_at  TIMESTAMPTZ,
    last_error      TEXT,
    retry_count     INTEGER NOT NULL DEFAULT 0,
    next_retry_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),       -- Exponential backoff scheduling
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Reconciliation cron index honors next_retry_at for exponential backoff
-- Backoff schedule: retry_count 0→1→2→3→4→5 with offsets 0,1,2,5,15,60 min (cumulative 83 min)
-- Hangfire 5dk cron: SELECT * WHERE pending AND retry_count <= 5 AND next_retry_at <= NOW()
-- Terminal state: retry_count=6 → status MUST be 'failed' atomically, INV-WC-025 alert.
-- Invariant: status='pending' AND retry_count>5 is FORBIDDEN (silent stuck state).
CREATE INDEX IF NOT EXISTS ix_webchat_outbox_pending
    ON webchat_outbox(next_retry_at, retry_count)
    WHERE inma_status = 'pending' OR legacy_status = 'pending';

-- Invariant guard: pending rows MUST have retry_count <= 5
-- (Hangfire job MUST set status='failed' atomically when retry_count crosses to 6)
-- Idempotent: existence check via pg_constraint (re-run safe)
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

-- Tenant-scoped lookup
CREATE INDEX IF NOT EXISTS ix_webchat_outbox_tenant
    ON webchat_outbox(tenant_id, created_at DESC);

-- Per-message dedupe (prevent double-enqueue)
CREATE UNIQUE INDEX IF NOT EXISTS ux_webchat_outbox_msg
    ON webchat_outbox(tenant_id, message_id);

-- =============================================================
-- §5. GRANT ALL (Postgres convention for shared invekto DB)
-- =============================================================
GRANT ALL ON webchat_cutover_state TO PUBLIC;
GRANT ALL ON webchat_inma_idempotency TO PUBLIC;
GRANT ALL ON webchat_outbox TO PUBLIC;
GRANT ALL ON SEQUENCE webchat_outbox_id_seq TO PUBLIC;

-- =============================================================
-- §6. Postcondition guard
-- =============================================================
DO $verify$
DECLARE
    has_display_name      boolean;
    has_locale            boolean;
    has_origins           boolean;
    has_cutover_state     boolean;
    has_idempotency       boolean;
    has_outbox            boolean;
    has_bridge_constraint boolean;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name='webchat_widget_configs' AND column_name='display_name'
    ) INTO has_display_name;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name='webchat_widget_configs' AND column_name='primary_locale'
    ) INTO has_locale;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name='webchat_widget_configs' AND column_name='allowed_origins'
    ) INTO has_origins;

    SELECT EXISTS (
        SELECT 1 FROM webchat_cutover_state WHERE id = 1
    ) INTO has_cutover_state;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name='webchat_inma_idempotency'
    ) INTO has_idempotency;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name='webchat_outbox'
    ) INTO has_outbox;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE table_name='webchat_cutover_state' AND constraint_name='chk_bridge_gate'
    ) INTO has_bridge_constraint;

    IF NOT (has_display_name AND has_locale AND has_origins
            AND has_cutover_state AND has_idempotency AND has_outbox
            AND has_bridge_constraint) THEN
        RAISE EXCEPTION 'Migration 047 verification failed: missing tables/columns/constraints';
    END IF;
END $verify$;

-- =============================================================
-- §7. Drop schedule (informational, NOT executed here)
-- T+7  : SET legacy_readonly_at = NOW(); legacy webchat_messages INSERT trigger raises error
-- T+8  : drop dashboard route /webchat (code change, not SQL)
-- T+90 : DROP TABLE webchat_messages, webchat_conversations, webchat_visitors,
--                    webchat_push_tokens (separate migration)
-- =============================================================
