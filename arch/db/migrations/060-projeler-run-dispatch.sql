-- =============================================================
-- Migration 060: FEAT-PROJELER — project run dispatch (PKT-14, send-exec SS-C)
-- =============================================================
-- ADDITIVE, idempotent. Lets a bulk_send_job carry EITHER a template (template_id — the
-- existing path: CSV/list runs AND a project's gallery_template content) OR inline free text
-- (inline_message_text — a project's free_text content, dispatched via the SS-A inline broadcast
-- path with NO backing outbound_templates row). EXACTLY ONE content source per job.
--
-- This is the schema half of the project run-dispatch slice. The consumer code
-- (BulkSendRepository.CreatePreviewJobFromProjectAsync + BulkSendOrchestrator.ConfirmAsync inline/
-- instance branch + project send endpoints) lands in the same SS-C change; the migration alone
-- changes no runtime behaviour.
--
-- DATA-SAFETY: template_id was NOT NULL and every existing row has it set with
-- inline_message_text absent, so num_nonnulls(template_id, inline_message_text)=1 holds for ALL
-- existing rows -> the new CHECK validates clean with ZERO backfill. Dropping NOT NULL is a
-- metadata-only, instant ALTER.
--
-- source: bulk_send_jobs.source has NO CHECK constraint (only data_lists.source does — see
-- outbound.sql:253 vs :325), so the new 'project' value needs no constraint change; existing
-- code already writes 'csv'/'list' as free strings.
--
-- INERT / gate note: independent of the cxapi live-enablement gate (P0-3). Like 055-059 it ships
-- safe; no tenant is allowlisted and no send path fires until a tenant is added to the BulkSend
-- allowlist (Outbound:BulkSend).
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: ADD COLUMN IF NOT EXISTS + ALTER COLUMN DROP NOT NULL (no-op if already nullable)
-- + named DROP/ADD CHECK. Safe to re-run. bulk_send_jobs already GRANTed (migration 051).
-- =============================================================

-- -------------------------------------------------------------
-- bulk_send_jobs: inline content carrier + template_id made optional
-- -------------------------------------------------------------
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS inline_message_text TEXT;

ALTER TABLE bulk_send_jobs
    ALTER COLUMN template_id DROP NOT NULL;

-- Exactly one content source per job: a template_id (CSV/list runs + a project's gallery_template)
-- XOR an inline_message_text (a project's free_text). num_nonnulls(...)=1 rejects BOTH-set and
-- BOTH-NULL — so a job can never dispatch with no content nor with an ambiguous double source.
ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS chk_bulk_message_source;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT chk_bulk_message_source
    CHECK (num_nonnulls(template_id, inline_message_text) = 1);

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-060)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_cols                  INTEGER;
    v_has_source_check      BOOLEAN;
    v_template_id_nullable  BOOLEAN;
BEGIN
    SELECT COUNT(*) INTO v_cols
    FROM information_schema.columns
    WHERE table_name = 'bulk_send_jobs'
      AND column_name IN ('template_id', 'inline_message_text');

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_bulk_message_source' AND contype = 'c'
    ) INTO v_has_source_check;

    SELECT is_nullable = 'YES' INTO v_template_id_nullable
    FROM information_schema.columns
    WHERE table_name = 'bulk_send_jobs' AND column_name = 'template_id';

    IF v_cols <> 2 THEN
        RAISE EXCEPTION 'INV-SEED-060: bulk_send_jobs content columns missing (found %/2)', v_cols;
    END IF;
    IF v_has_source_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-060: chk_bulk_message_source CHECK missing';
    END IF;
    IF v_template_id_nullable IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-060: bulk_send_jobs.template_id must be nullable';
    END IF;

    RAISE NOTICE 'INV-SEED-060 OK: bulk_send_jobs +inline_message_text, template_id nullable, exactly-one(template_id, inline_message_text) enforced (project free_text run -> inline path; gallery_template/CSV/list -> template path)';
END
$verify$;
