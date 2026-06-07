-- =============================================================
-- Migration 057: FEAT-PROJELER — Projeler management layer (PKT-14, slice S1)
-- =============================================================
-- ADDITIVE, idempotent, INERT (schema-only, NO runtime behaviour change).
-- Introduces the "Projeler" parent layer above the existing bulk send engine:
--
--     projects (parent)
--        |- project_targets (junction: 1 project -> N data_lists)
--        \- Run = bulk_send_jobs (gains a nullable project_id FK)
--
-- The bulk engine itself is UNCHANGED. A "Run" stays exactly today's
-- bulk_send_job; this migration only lets many runs hang off one project and
-- lets a project pre-select the cxapi instance + approved-template config that
-- a run inherits. None of these columns are read/written by shipped code yet
-- (CRUD lands in slice S2, the cxapi template picker in S3, the UI in S4).
--
-- Structure reference: TONIVA mode4 `ivr_campaigns` (parent -> run model,
-- multi-source junction, Draft->Running->Paused->Completed/Cancelled state
-- machine, soft-delete-as-archive). STRUCTURE ONLY — no TONIVA code, no
-- telephony fields (audio/trunk/dtmf/calls_per_second dropped). The "action"
-- is a WhatsApp template ref instead of an audio file.
--
-- Design decisions (Q interview 2026-06-07):
--   * Composite FK (tenant_id, *_id) -> projects(tenant_id, id) on every child
--     makes a cross-tenant child row structurally impossible even with an app
--     bug (same guard data_lists/list_records use, migration 052).
--   * Soft-delete-as-archive (archived_at) — never hard-delete a project that
--     has runs (TONIVA PiaBella lesson: FK-cascade hard delete orphans
--     downstream billable records). bulk_send_jobs.project_id FK is RESTRICT so
--     a hard DELETE on a project with runs is refused; archive instead.
--   * project_id is NULLABLE so every existing bulk_send_jobs row + today's
--     project-less inserts stay valid (MATCH SIMPLE: FK unchecked while NULL).
--   * Denormalized per-project roll-up counters (run/sent/delivered/... ),
--     recomputed authoritatively after each run (never per-row) — INERT 0 here,
--     populated by S2+. Mirrors outbound_broadcasts counter design.
--
-- INERT / gate note: this migration is independent of the cxapi live-enablement
-- gate (P0-3). Like 055/056 it ships safe with CxapiSend default-OFF; no tenant
-- is allowlisted and no send path is touched.
--
-- DEFERRED (NOT in this migration):
--   * projects CRUD repo/service/endpoints + INV-OB-066+ error codes -> S2.
--   * cxapi template-list endpoint + WapCrmTemplateClient                -> S3.
--   * Projeler React page + wizard                                        -> S4.
--   * bulk_send_jobs cxapi send columns (instance/template/param)         -> PR-4.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS guards + named DROP/ADD constraints, safe to re-run.
-- GRANT ALL is the project canon (codex-context.md L20/L69); do NOT downgrade.
-- =============================================================

-- -------------------------------------------------------------
-- projects: one row per Projeler project (parent over N runs)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS projects (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    name                    VARCHAR(255) NOT NULL,
    description             TEXT,

    -- lifecycle state machine (TONIVA mode4 structure ref, no telephony):
    --   draft     -> created, not yet launched
    --   running   -> at least one run dispatched / in flight
    --   paused    -> operator paused; runs may resume
    --   completed -> all runs finished
    --   cancelled -> operator/admin stopped before completion
    --   archived  -> soft-deleted (preserved for reconciliation; name freed)
    status                  VARCHAR(16) NOT NULL DEFAULT 'draft',

    -- cxapi send config carried at PROJECT level (roadmap: instance_id +
    -- template chosen on the project, inherited by each run). INERT until PR-4
    -- wires approved-template send + a tenant is allowlisted (P0-3 gate).
    instance_id             INTEGER,
    template_kind           VARCHAR(16),       -- 'plain_text' | 'wapcrm_template'
    wa_template_id          VARCHAR(128),
    template_language       VARCHAR(8),
    param_mapping           JSONB,

    -- denormalized roll-up counters across this project's runs (bulk_send_jobs);
    -- recomputed authoritatively after each run (single UPDATE = aggregate),
    -- never incrementally. INERT 0 here; populated by S2+.
    run_count               INTEGER NOT NULL DEFAULT 0,
    total_targets           INTEGER NOT NULL DEFAULT 0,
    sent_count              INTEGER NOT NULL DEFAULT 0,
    delivered_count         INTEGER NOT NULL DEFAULT 0,
    read_count              INTEGER NOT NULL DEFAULT 0,
    failed_count            INTEGER NOT NULL DEFAULT 0,
    ambiguous_count         INTEGER NOT NULL DEFAULT 0,

    -- tenant user id (TenantContext.UserId); no local users table -> no FK.
    created_by              INTEGER,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at              TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,
    -- soft-delete-as-archive: set when status -> archived; frees the name,
    -- preserves rows + counters for reporting/reconciliation.
    archived_at             TIMESTAMPTZ,

    CONSTRAINT chk_project_status CHECK (status IN (
        'draft','running','paused','completed','cancelled','archived')),
    CONSTRAINT chk_project_template_kind CHECK (
        template_kind IS NULL OR template_kind IN ('plain_text','wapcrm_template')),
    CONSTRAINT chk_project_counters_nonneg CHECK (
        run_count >= 0 AND total_targets >= 0 AND sent_count >= 0
        AND delivered_count >= 0 AND read_count >= 0 AND failed_count >= 0
        AND ambiguous_count >= 0),
    -- Composite unique so child rows (project_targets, bulk_send_jobs) can FK on
    -- (tenant_id, id) and a cross-tenant child becomes structurally impossible.
    CONSTRAINT uq_projects_tenant_id UNIQUE (tenant_id, id)
);

-- Normalized active-name uniqueness per tenant (case/space-insensitive),
-- scoped to non-archived rows so an archived project's name can be reused.
CREATE UNIQUE INDEX IF NOT EXISTS uq_projects_tenant_name_active
    ON projects (tenant_id, lower(btrim(name)))
    WHERE archived_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_projects_tenant_created
    ON projects (tenant_id, created_at DESC)
    WHERE archived_at IS NULL;

-- -------------------------------------------------------------
-- project_targets: junction — a project targets one-or-many data_lists
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS project_targets (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL,
    project_id              BIGINT NOT NULL,
    data_list_id            BIGINT NOT NULL,
    -- operator-defined ordering of lists within the project
    sort_order              INTEGER NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Cross-tenant integrity: child tenant_id MUST match its parent project's
    -- tenant. Cascade because a target is a pure link with no history value.
    CONSTRAINT fk_project_targets_project
        FOREIGN KEY (tenant_id, project_id)
        REFERENCES projects (tenant_id, id) ON DELETE CASCADE,
    -- The targeted list must belong to the same tenant (composite FK to the
    -- data_lists (tenant_id, id) unique, migration 052).
    CONSTRAINT fk_project_targets_data_list
        FOREIGN KEY (tenant_id, data_list_id)
        REFERENCES data_lists (tenant_id, id) ON DELETE CASCADE,
    -- A list appears at most once per project.
    CONSTRAINT uq_project_target_project_list UNIQUE (project_id, data_list_id)
);

CREATE INDEX IF NOT EXISTS idx_project_targets_project
    ON project_targets (project_id);

-- Reverse lookup ("which projects target this list") + supports the
-- data_lists ON DELETE CASCADE child scan.
CREATE INDEX IF NOT EXISTS idx_project_targets_data_list
    ON project_targets (tenant_id, data_list_id);

-- -------------------------------------------------------------
-- bulk_send_jobs.project_id: link a Run to its parent project (nullable)
-- -------------------------------------------------------------
-- Nullable so existing/project-less runs stay valid. Composite FK
-- (tenant_id, project_id) -> projects(tenant_id, id); MATCH SIMPLE leaves the
-- FK unchecked while project_id IS NULL. ON DELETE RESTRICT: a project with
-- runs cannot be hard-deleted (archive it) — protects run history.
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS project_id BIGINT;

ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS fk_bulk_send_jobs_project;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT fk_bulk_send_jobs_project
    FOREIGN KEY (tenant_id, project_id)
    REFERENCES projects (tenant_id, id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS idx_bulk_send_jobs_project
    ON bulk_send_jobs (tenant_id, project_id)
    WHERE project_id IS NOT NULL;

-- -------------------------------------------------------------
-- Grants (production runs as role 'invekto')
-- -------------------------------------------------------------
GRANT ALL ON projects TO invekto;
GRANT ALL ON project_targets TO invekto;
GRANT ALL ON SEQUENCE projects_id_seq TO invekto;
GRANT ALL ON SEQUENCE project_targets_id_seq TO invekto;

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-057)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_projects        BOOLEAN;
    v_has_targets         BOOLEAN;
    v_has_projects_uq     BOOLEAN;
    v_has_targets_fk      BOOLEAN;
    v_has_status_check    BOOLEAN;
    v_has_name_index      BOOLEAN;
    v_has_job_project_col INTEGER;
    v_has_job_project_fk  BOOLEAN;
BEGIN
    v_has_projects := to_regclass('public.projects')        IS NOT NULL;
    v_has_targets  := to_regclass('public.project_targets') IS NOT NULL;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_projects_tenant_id'
    ) INTO v_has_projects_uq;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_project_targets_project' AND contype = 'f'
    ) INTO v_has_targets_fk;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_project_status' AND contype = 'c'
    ) INTO v_has_status_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_indexes WHERE indexname = 'uq_projects_tenant_name_active'
    ) INTO v_has_name_index;

    SELECT COUNT(*) INTO v_has_job_project_col
    FROM information_schema.columns
    WHERE table_name = 'bulk_send_jobs' AND column_name = 'project_id';

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_bulk_send_jobs_project' AND contype = 'f'
    ) INTO v_has_job_project_fk;

    IF v_has_projects IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: projects table missing after migration';
    END IF;
    IF v_has_targets IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: project_targets table missing after migration';
    END IF;
    IF v_has_projects_uq IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: projects composite unique (tenant_id,id) missing';
    END IF;
    IF v_has_targets_fk IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: project_targets composite tenant FK missing';
    END IF;
    IF v_has_status_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: chk_project_status missing';
    END IF;
    IF v_has_name_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: projects normalized active-name unique index missing';
    END IF;
    IF v_has_job_project_col <> 1 THEN
        RAISE EXCEPTION 'INV-SEED-057: bulk_send_jobs.project_id column missing (found %/1)', v_has_job_project_col;
    END IF;
    IF v_has_job_project_fk IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-057: bulk_send_jobs.project_id composite FK missing';
    END IF;

    RAISE NOTICE 'INV-SEED-057 OK: projects + project_targets + bulk_send_jobs.project_id verified (inert; CRUD -> S2, cxapi -> PR-4)';
END
$verify$;
