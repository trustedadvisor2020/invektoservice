-- =============================================================
-- Migration 059: FEAT-PROJELER — project plain_text content config (PKT-14, send-exec SS-B)
-- =============================================================
-- ADDITIVE, idempotent. Lets a project carry, IN ITS SETTINGS, the CONTENT that a
-- plain_text run will send. Q decision (2026-06-09 interview): the operator picks
-- the content in project settings (not at send time), choosing EITHER:
--   * a Şablon Galerisi (outbound_templates) row  -> content_mode='gallery_template'
--   * free text typed into the project            -> content_mode='free_text'
--
-- This is the persistence half of the send-execution slice. The RUN dispatch that
-- consumes these columns (inline-text bulk path + project_id run) lands in SS-A/SS-C.
-- No runtime behaviour changes here; existing CRUD keeps working (all new columns
-- nullable, existing rows validate clean against the consistency CHECK with NULLs).
--
-- Design (Q interview 2026-06-09):
--   * content_mode applies ONLY to template_kind='plain_text'. For
--     template_kind='wapcrm_template' (HSM, PR-4) content_mode stays NULL — the
--     SERVICE enforces that coupling; the DB enforces only content-internal
--     consistency (mode <-> which content column is set).
--   * Free text is stored ON THE PROJECT (plain_text_body) and dispatched via an
--     INLINE-TEXT bulk path (SS-A), NOT as a backing outbound_templates row — so
--     outbound_templates is intentionally NOT touched here (Q chose inline send).
--   * gallery_template stores outbound_template_id (single-col FK to
--     outbound_templates(id), tenant ownership enforced in app — same pattern as
--     bulk_send_jobs.template_id; the (tenant_id,id) composite is not unique on
--     outbound_templates so a composite FK is not available).
--
-- INERT / gate note: independent of the cxapi live-enablement gate (P0-3). Like
-- 055/056/057 it ships safe; no tenant is allowlisted and no send path is touched.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: ADD COLUMN IF NOT EXISTS + named DROP/ADD CHECK. Safe to re-run.
-- GRANT ALL is the project canon; projects already granted in 057 (no change).
-- =============================================================

-- -------------------------------------------------------------
-- projects: plain_text content config (mode + the two content carriers)
-- -------------------------------------------------------------
ALTER TABLE projects
    ADD COLUMN IF NOT EXISTS content_mode          VARCHAR(16),
    ADD COLUMN IF NOT EXISTS outbound_template_id  INTEGER REFERENCES outbound_templates(id),
    ADD COLUMN IF NOT EXISTS plain_text_body       TEXT;

-- content_mode value domain.
ALTER TABLE projects DROP CONSTRAINT IF EXISTS chk_project_content_mode;
ALTER TABLE projects ADD CONSTRAINT chk_project_content_mode
    CHECK (content_mode IS NULL OR content_mode IN ('gallery_template', 'free_text'));

-- Content-internal consistency: the mode decides EXACTLY ONE content column.
--   gallery_template -> outbound_template_id set, plain_text_body NULL
--   free_text        -> plain_text_body set,     outbound_template_id NULL
--   NULL (no plain_text content) -> both NULL
-- Existing rows (content_mode NULL, both content cols NULL) validate clean.
ALTER TABLE projects DROP CONSTRAINT IF EXISTS chk_project_content_consistency;
ALTER TABLE projects ADD CONSTRAINT chk_project_content_consistency
    CHECK (
        CASE
            WHEN content_mode = 'gallery_template'
                THEN outbound_template_id IS NOT NULL AND plain_text_body IS NULL
            WHEN content_mode = 'free_text'
                THEN plain_text_body IS NOT NULL AND outbound_template_id IS NULL
            ELSE outbound_template_id IS NULL AND plain_text_body IS NULL
        END
    );

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-059)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_cols              INTEGER;
    v_has_mode_check    BOOLEAN;
    v_has_consist_check BOOLEAN;
    v_has_tmpl_fk       BOOLEAN;
BEGIN
    SELECT COUNT(*) INTO v_cols
    FROM information_schema.columns
    WHERE table_name = 'projects'
      AND column_name IN ('content_mode', 'outbound_template_id', 'plain_text_body');

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_project_content_mode' AND contype = 'c'
    ) INTO v_has_mode_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_project_content_consistency' AND contype = 'c'
    ) INTO v_has_consist_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'projects_outbound_template_id_fkey' AND contype = 'f'
    ) INTO v_has_tmpl_fk;

    IF v_cols <> 3 THEN
        RAISE EXCEPTION 'INV-SEED-059: projects content columns missing (found %/3)', v_cols;
    END IF;
    IF v_has_mode_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-059: chk_project_content_mode missing';
    END IF;
    IF v_has_consist_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-059: chk_project_content_consistency missing';
    END IF;
    IF v_has_tmpl_fk IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-059: projects.outbound_template_id FK to outbound_templates missing';
    END IF;

    RAISE NOTICE 'INV-SEED-059 OK: projects +content_mode/+outbound_template_id/+plain_text_body (inert; run dispatch -> SS-A/SS-C)';
END
$verify$;
