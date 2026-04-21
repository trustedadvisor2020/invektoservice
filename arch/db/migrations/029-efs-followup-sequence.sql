-- Migration 029: FEAT-EFS Drip Sequence — Event Follow-Up Sequence orchestrator schema.
--
-- Scope (per arch/plans/20260425-feat-efs-drip-sequence.json + arch/features/event-followup-sequence.md):
--
-- 1) tenant_settings additions
--    - efs_test_mode BOOLEAN DEFAULT FALSE — when TRUE, FollowupOrchestrator interprets
--      stage delay_days as TimeSpan.FromMinutes (3 → 3 minutes) for fast smoke. Pilot
--      Dent toggles TRUE before P9 smoke, restored to FALSE post-smoke.
--    - efs_no_reply_threshold_days INT NOT NULL DEFAULT 3 — Automation
--      NoReplyCheckJob uses this delay before checking lead.last_inbound_at vs
--      welcome enqueue timestamp. Operator-tunable per tenant.
--
-- 2) leads additions (forward-compat columns, NULL keeps existing rows untouched)
--    - followup_state JSONB NOT NULL DEFAULT '{}'::jsonb — tracks active sequence id,
--      last stage index, opted_out_at timestamp, ab assignment timestamp.
--    - followup_ab_group VARCHAR(10) NULL — 'drip' | 'control' | NULL (pre-trigger).
--      Set deterministically on first FollowupOrchestrator.EnqueueAsync via
--      sha256(tenant_id|lead_id|sequence_id) hash; persists for audit.
--
-- 3) event_followup_sequences (tenant config table — replaces spec's JSONB-in-tenant_settings
--    per Q1 interview decision; roadmap v2.1 P5 row authoritative)
--    Columns:
--      id            BIGSERIAL PRIMARY KEY
--      tenant_id     INT NOT NULL — FK to tenants.id (no on-delete cascade — keep audit)
--      slug          VARCHAR(64) NOT NULL — tenant-scoped unique name (e.g. 'post-roadshow')
--      stages        JSONB NOT NULL — array of { delay_days, template_slug, template_group? }
--      ab_split_percent SMALLINT NOT NULL DEFAULT 50 — 0..100, % of leads getting drip
--      enabled       BOOLEAN NOT NULL DEFAULT FALSE — disabled sequences reject EnqueueAsync
--      created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
--      updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
--    Constraints:
--      uq(tenant_id, slug) — slug unique per tenant
--      ck ab_split_percent BETWEEN 0 AND 100
--
-- 4) event_followup_runs (per-lead-per-stage schedule history — replaces spec's
--    "Hangfire dashboard" reliance; gives DB-native A/B reporting + idempotency)
--    Columns:
--      id            BIGSERIAL PRIMARY KEY
--      tenant_id     INT NOT NULL
--      sequence_id   BIGINT NOT NULL — FK to event_followup_sequences.id ON DELETE CASCADE
--      lead_id       BIGINT NOT NULL — FK to leads.id (no cascade — keep audit if lead deleted)
--      stage_index   SMALLINT NOT NULL — 0-based stage offset within sequence.stages array
--      ab_group      VARCHAR(10) NOT NULL — duplicates leads.followup_ab_group for direct row query
--      scheduled_at  TIMESTAMPTZ NOT NULL — when the Hangfire job is set to fire
--      executed_at   TIMESTAMPTZ NULL — when FollowupStageJob.Execute actually ran
--      status        VARCHAR(32) NOT NULL DEFAULT 'scheduled'
--                    — 'scheduled' | 'sent' | 'skipped_optout' | 'skipped_disabled' | 'failed'
--      hangfire_job_id VARCHAR(64) NULL — Hangfire's BackgroundJob.Schedule return value
--      error_code    VARCHAR(32) NULL — INV-MK-* on failure
--      created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
--    Indexes:
--      idx (tenant_id, lead_id, status) — orchestrator collision check
--      idx (scheduled_at) WHERE status = 'scheduled' — ops dashboard hot-path
--      idx (sequence_id, stage_index) — sequence-scoped reporting
--    Constraints:
--      ck status IN ('scheduled','sent','skipped_optout','skipped_disabled','failed')
--      ck ab_group IN ('drip','control')
--      ck stage_index >= 0
--
-- Idempotency: all CREATE / ALTER use IF NOT EXISTS. Re-running this migration is safe.
-- GRANT ALL re-asserted at end (lessons 2026-04-18 — prod role is `invekto`, not `invekto_app`).

-- ============================================================
-- 1. tenant_settings additions
-- ============================================================
ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS efs_test_mode BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS efs_no_reply_threshold_days INT NOT NULL DEFAULT 3;

-- ============================================================
-- 2. leads additions
-- ============================================================
ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS followup_state JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS followup_ab_group VARCHAR(10) NULL;

-- ============================================================
-- 3. event_followup_sequences (tenant config)
-- ============================================================
-- Tenant FK references tenant_registry (authoritative tenant catalog, see arch/db/pkt6b1-niche-business.sql).
-- ON DELETE CASCADE because a deleted tenant has no meaningful sequence to keep.
CREATE TABLE IF NOT EXISTS event_followup_sequences (
    id                BIGSERIAL PRIMARY KEY,
    tenant_id         INT NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    slug              VARCHAR(64) NOT NULL,
    stages            JSONB NOT NULL,
    ab_split_percent  SMALLINT NOT NULL DEFAULT 50,
    enabled           BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT efs_sequences_slug_unique_per_tenant UNIQUE (tenant_id, slug),
    CONSTRAINT efs_sequences_ab_split_range CHECK (ab_split_percent BETWEEN 0 AND 100)
);

-- ============================================================
-- 4. event_followup_runs (per-lead-per-stage schedule)
-- ============================================================
-- FK policy:
--   tenant_id   → tenant_registry ON DELETE CASCADE (aligns with sequences; a deleted
--                 tenant cannot have dangling run rows).
--   sequence_id → event_followup_sequences ON DELETE CASCADE (deleting a sequence
--                 invalidates all its scheduled/historical runs; caller already drains
--                 the queue via Hangfire before deletion).
--   lead_id     → leads ON DELETE NO ACTION (default). Runs carry audit value even if
--                 the lead row is later deleted; a hard FK block prevents accidental
--                 lead deletion without operator first purging followup history.
CREATE TABLE IF NOT EXISTS event_followup_runs (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    sequence_id     BIGINT NOT NULL REFERENCES event_followup_sequences(id) ON DELETE CASCADE,
    lead_id         BIGINT NOT NULL REFERENCES leads(id),
    stage_index     SMALLINT NOT NULL,
    ab_group        VARCHAR(10) NOT NULL,
    scheduled_at    TIMESTAMPTZ NOT NULL,
    executed_at     TIMESTAMPTZ NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'scheduled',
    hangfire_job_id VARCHAR(64) NULL,
    error_code      VARCHAR(32) NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT efs_runs_status_enum CHECK (status IN ('scheduled','sent','skipped_optout','skipped_disabled','failed')),
    CONSTRAINT efs_runs_ab_group_enum CHECK (ab_group IN ('drip','control')),
    CONSTRAINT efs_runs_stage_index_nonneg CHECK (stage_index >= 0)
);

-- Indexes (orchestrator collision lookup + ops dashboard hot-path + sequence-scoped reporting)
CREATE INDEX IF NOT EXISTS idx_efs_runs_tenant_lead_status
    ON event_followup_runs (tenant_id, lead_id, status);

CREATE INDEX IF NOT EXISTS idx_efs_runs_scheduled_at_pending
    ON event_followup_runs (scheduled_at)
    WHERE status = 'scheduled';

CREATE INDEX IF NOT EXISTS idx_efs_runs_sequence_stage
    ON event_followup_runs (sequence_id, stage_index);

-- Concurrent-trigger race guard: at most one scheduled stage row per (tenant, lead, stage_index).
-- Closes the EnqueueAsync race window between CountScheduledRunsForLeadAsync and
-- InsertScheduledRunAsync — two parallel trigger calls for the same lead would both
-- see zero and both attempt to schedule; the second INSERT now fails with a
-- unique-constraint violation (Npgsql 23505), surfaced as INV-MK-055 by the
-- orchestrator's typed catch.
CREATE UNIQUE INDEX IF NOT EXISTS uq_efs_runs_lead_stage_scheduled
    ON event_followup_runs (tenant_id, lead_id, stage_index)
    WHERE status = 'scheduled';

-- ============================================================
-- 5. GRANT ALL re-assertion
-- ============================================================
-- Idempotent re-grant — required so the prod `invekto` role can read/write the new
-- tables (lessons 2026-04-18: PostgreSQL role is `invekto`, not `invekto_app`).
GRANT ALL ON tenant_settings TO invekto;
GRANT ALL ON leads TO invekto;
GRANT ALL ON event_followup_sequences TO invekto;
GRANT ALL ON event_followup_runs TO invekto;
GRANT USAGE, SELECT ON SEQUENCE event_followup_sequences_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE event_followup_runs_id_seq TO invekto;
