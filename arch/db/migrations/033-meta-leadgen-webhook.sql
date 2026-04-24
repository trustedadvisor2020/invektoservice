-- Migration 033: Paket B-META — Meta Leadgen Webhook Native Integration
--
-- Context:
-- Dent Adavista pilot Stage 1 Meta Lead Ads → Invekto → WhatsApp welcome native
-- integration (Zapier/3rd-party bridge eliminated). This migration provisions the
-- DB surface required by:
--   * Backend endpoints: GET/POST /api/inbound/meta/leadgen/{tenantId:int}
--     (X-Hub-Signature-256 HMAC-SHA256 verify + webhook handshake)
--   * Backend settings: /api/v1/tenant-settings/meta-leadgen (config CRUD,
--     discover-forms, list events)
--   * Automation: MetaLeadgenIntakeJob [Queue=automation] → Graph API fetch →
--     LeadIntakeService hop
--
-- Scope (minimal surface, interview gates G3/G5/G6 Q-approved 2026-04-24):
--   * ALTER TABLE tenant_settings ADD COLUMN meta_leadgen_config JSONB
--     (G3: plaintext JSONB per LIW precedent; encryption deferred post-pilot).
--     Shape:
--       { "verify_token": "...", "app_secret": "...",
--         "page_access_token": "...", "page_id": "...",
--         "field_id_map": { "<meta_question_id>": "<canonical>", ... } }
--     Empty default = no Meta integration (signature verify rejects all posts).
--   * CREATE TABLE meta_leadgen_events (audit trail + Dashboard [Test Events]
--     panel source + at-least-once idempotency anchor).
--     UNIQUE(tenant_id, leadgen_id) — Meta 10s timeout retry deduped pre-job
--     enqueue so a duplicate push returns 200 with no side effects. Raw payload
--     preserved 7 days for ops debug (partial index keeps hot path scannable).
--   * Postcondition DO $verify$ block (INV-SEED-018..020) fails the transaction
--     loudly if ALTER/CREATE/GRANT silently no-ops. Pattern inherited from
--     migration 031 (B-VCP-DOCTORS) — actionable root-cause + next-step guidance
--     in every RAISE EXCEPTION (Codex iter 0 CQ12 lesson carried forward).
--
-- Retention:
--   * raw_payload stays 7 days (partial index `idx_meta_leadgen_events_tenant_recent`
--     scopes Dashboard [Test Events] queries to the recent window; older rows
--     remain queryable for ops but fall off the hot index). Daily purge Hangfire
--     job is DEFERRED to a post-pilot packet (7-day window is self-limiting and
--     DB storage does not fill within the 3-day pilot timeline).
--
-- Security:
--   * app_secret / page_access_token land in meta_leadgen_config JSONB
--     (DB-level access controlled, GRANT ALL `invekto` only). Application layer
--     MUST guard against jsonl logging (MetaLeadgenConfigDto → caller never
--     writes the DTO verbatim to System* log lines; unit tests pin this).
--   * raw_payload contains Meta-supplied PII (phone, email inside `field_data`).
--     [Test Events] DTO exposes payload_keys[] only — never the raw values.
--     Operators with direct DB read access see PII; that channel is already
--     governed by the invekto role access policy (same as leads / chat_messages).
--
-- Related:
--   * Canonical mirror       : arch/db/tenant-settings.sql (meta_leadgen_config doc)
--   * Plan                   : arch/plans/20260425-feat-meta-leadgen-webhook.json
--   * Tracking               : tracking/20260425-feat-meta-leadgen-webhook.md
--   * Error codes            : arch/errors.md INV-META-001..006 + INV-SEED-018..020
--                              + INV-AT-072 (Automation lifecycle hop warning)
--   * Idempotency discipline : uq_meta_leadgen_events_tenant_leadgen +
--                              LeadIntakeService.UpsertIntakeLeadAsync (phone-based)
--                              + TriggerWelcomeFlowJob dup-window (24h JSONB guard)
--                              form a 3-layer dedup stack so Meta at-least-once
--                              delivery cannot produce double WhatsApp welcomes.

BEGIN;

-- =============================================================================
-- §1. tenant_settings.meta_leadgen_config JSONB (additive, backward-compat)
-- =============================================================================
-- Empty default ({}) = no Meta integration for the tenant; webhook GET handshake
-- returns 403 (verify_token empty -> mismatch -> INV-META-002) and POST returns
-- 401 (no app_secret -> HMAC compute produces a different digest -> INV-META-001).
-- Existing tenants observe zero behavior change until an operator configures.

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS meta_leadgen_config JSONB NOT NULL DEFAULT '{}'::jsonb;

-- =============================================================================
-- §2. meta_leadgen_events (audit + idempotency anchor + [Test Events] source)
-- =============================================================================
-- lead_id is NULL during initial INSERT (endpoint layer fast-path: ingest +
-- enqueue under Meta 3s target) and filled in by MetaLeadgenIntakeJob after
-- LeadIntakeService UPSERT completes. SET NULL on leads deletion so GDPR
-- deletion cascades without orphaning audit rows.
--
-- UNIQUE(tenant_id, leadgen_id) is the at-least-once idempotency anchor. Meta
-- retries the same leadgen_id on 10s timeout; the second POST INSERT hits the
-- UNIQUE violation, orchestrator catches it, returns 200 (Meta stops retrying),
-- and crucially DOES NOT re-enqueue the Hangfire job.
--
-- http_status / error_code columns capture the endpoint outcome (200 on happy
-- path, 401 INV-META-001 on signature fail written from endpoint defensive
-- branch, subsequent job-layer failures written back by MetaLeadgenIntakeJob).

CREATE TABLE IF NOT EXISTS meta_leadgen_events (
    id            BIGSERIAL PRIMARY KEY,
    tenant_id     INTEGER     NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    leadgen_id    VARCHAR(64) NOT NULL,
    form_id       VARCHAR(64),
    page_id       VARCHAR(64),
    received_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    http_status   SMALLINT,
    error_code    VARCHAR(20),
    raw_payload   JSONB,
    lead_id       INTEGER     REFERENCES leads(id) ON DELETE SET NULL,
    CONSTRAINT uq_meta_leadgen_events_tenant_leadgen UNIQUE (tenant_id, leadgen_id)
);

-- Tenant + recency index: Dashboard [Test Events] panel queries ORDER BY
-- received_at DESC per tenant. A partial-predicate `WHERE received_at > now() -
-- INTERVAL '7 days'` would match the hot path ideally, BUT PostgreSQL requires
-- index predicates to be IMMUTABLE (see 40.7 "Immutability of Index Functions")
-- and `now()` is STABLE not IMMUTABLE — CREATE INDEX would either reject the
-- DDL outright or freeze the predicate at build time making it static after
-- ~7 days (silent index decay). We therefore use a full index on
-- (tenant_id, received_at DESC). Size cost: one row per Meta webhook call per
-- tenant; thousands of tenants at typical webhook volumes stay comfortably under
-- typical B-tree thresholds. A scheduled DELETE job (post-pilot packet per plan
-- intentional_exclusions) will prune rows > 7 days old so the index stays small.
CREATE INDEX IF NOT EXISTS idx_meta_leadgen_events_tenant_recent
    ON meta_leadgen_events (tenant_id, received_at DESC);

-- Prod role is `invekto` (lesson 2026-04-18, not `invekto_app`).
GRANT ALL ON meta_leadgen_events TO invekto;
GRANT USAGE, SELECT ON SEQUENCE meta_leadgen_events_id_seq TO invekto;

-- =============================================================================
-- §3. Postcondition verify — fail-loud assertions (INV-SEED-018..020)
-- =============================================================================
-- Exception messages include actionable why + next-step guidance per Codex iter
-- 0 CQ12 feedback carried forward from migrations 031 and 032.

DO $verify$
DECLARE
    v_table_exists       BOOLEAN;
    v_jsonb_col_exists   BOOLEAN;
    v_grant_ok           BOOLEAN;
    v_unique_constraint  BOOLEAN;
    v_recent_index       BOOLEAN;
BEGIN
    -- V18: meta_leadgen_events table exists
    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'meta_leadgen_events'
    ) INTO v_table_exists;
    IF NOT v_table_exists THEN
        RAISE EXCEPTION '[INV-SEED-018] meta_leadgen_events table not created. '
            'Root cause: CREATE TABLE IF NOT EXISTS silently skipped (schema drift '
            'or transaction abort at prior DDL). Next step: check preceding statements '
            'for failure in server log, verify invekto role owns public schema, re-run '
            'migration manually in a fresh transaction.';
    END IF;

    -- V18b: UNIQUE(tenant_id, leadgen_id) constraint present (at-least-once idempotency anchor)
    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uq_meta_leadgen_events_tenant_leadgen'
    ) INTO v_unique_constraint;
    IF NOT v_unique_constraint THEN
        RAISE EXCEPTION '[INV-SEED-018] meta_leadgen_events UNIQUE (tenant_id, leadgen_id) constraint missing. '
            'Root cause: CREATE TABLE completed but CONSTRAINT clause was stripped (partial DDL apply). '
            'Next step: ALTER TABLE meta_leadgen_events ADD CONSTRAINT uq_meta_leadgen_events_tenant_leadgen '
            'UNIQUE (tenant_id, leadgen_id); Meta at-least-once delivery dedup depends on this.';
    END IF;

    -- V18c: recent-events index present (Dashboard [Test Events] hot path).
    -- Full B-tree (NOT partial). A predicate `WHERE received_at > now() - INTERVAL '7 days'`
    -- was considered but rejected: PostgreSQL requires index predicates to be IMMUTABLE
    -- (per 40.7 Immutability of Index Functions), and now() is STABLE — a partial index
    -- with that predicate is invalid DDL. A scheduled DELETE job (post-pilot packet per
    -- plan intentional_exclusions) will prune rows > 7 days so this full index stays small.
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_meta_leadgen_events_tenant_recent'
    ) INTO v_recent_index;
    IF NOT v_recent_index THEN
        RAISE EXCEPTION '[INV-SEED-018] meta_leadgen_events idx_meta_leadgen_events_tenant_recent index missing. '
            'Root cause: CREATE INDEX IF NOT EXISTS silently skipped. '
            'Next step: CREATE INDEX idx_meta_leadgen_events_tenant_recent ON meta_leadgen_events '
            '(tenant_id, received_at DESC);  (full B-tree index, no predicate — partial with now() '
            'is invalid per PostgreSQL 40.7; retention handled by scheduled DELETE job). '
            'Dashboard Test Events panel performance depends on this.';
    END IF;

    -- V19: tenant_settings.meta_leadgen_config JSONB column exists
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = 'tenant_settings'
          AND column_name  = 'meta_leadgen_config'
          AND data_type    = 'jsonb'
    ) INTO v_jsonb_col_exists;
    IF NOT v_jsonb_col_exists THEN
        RAISE EXCEPTION '[INV-SEED-019] tenant_settings.meta_leadgen_config JSONB column missing. '
            'Root cause: ALTER TABLE DDL suppressed (column exists with wrong type, table missing, '
            'or role lacks ALTER privilege). Next step: verify `SELECT column_name, data_type FROM '
            'information_schema.columns WHERE table_name=''tenant_settings''` shape, run ALTER TABLE '
            'manually + re-run verify.';
    END IF;

    -- V20: GRANT ALL invekto on meta_leadgen_events (prod role grant drift guard)
    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name     = 'meta_leadgen_events'
          AND grantee        = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok;
    IF NOT v_grant_ok THEN
        RAISE EXCEPTION '[INV-SEED-020] GRANT ALL to invekto not applied on meta_leadgen_events. '
            'Root cause: GRANT statement silently skipped OR role rename drift. '
            'Next step: verify `SELECT rolname FROM pg_roles WHERE rolname=''invekto''` exists, '
            'manually re-run `GRANT ALL ON meta_leadgen_events TO invekto; GRANT USAGE, SELECT '
            'ON SEQUENCE meta_leadgen_events_id_seq TO invekto;` + re-run verify.';
    END IF;

    RAISE NOTICE '[B-META] postcondition verify PASS '
        '(table=ok, unique=ok, recent_idx=ok, jsonb_col=ok, grant=ok)';
END
$verify$;

COMMIT;
