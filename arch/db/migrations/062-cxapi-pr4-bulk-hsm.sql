-- =============================================================
-- Migration 062: FEAT-PROJELER / cxapi — PR-4 approved-template (HSM) run snapshot
-- =============================================================
-- ADDITIVE, idempotent. A project run (bulk_send_job) now snapshots the project's
-- cxapi send config AT PREVIEW TIME so the run is IMMUTABLE: editing the project
-- (or switching its template) after preview can never change what an in-flight or
-- paused run sends (Codex P0 #5 immutable-route doctrine; SS-D pause/resume reads
-- the snapshot, never the live project row).
--
-- New columns (mirror the proven projects-table columns, migration 057/059):
--   instance_id            cxapi Cloud API channel the run sends from (HSM: NOT NULL via CHECK)
--   template_kind          'plain_text' | 'wapcrm_template' (NULL = legacy/CSV/list rows)
--   wa_template_id         approved-template slug (cxapi templateId; language embedded in slug)
--   param_mapping          operator's placeholder->list-column mapping (opaque JSONB, <=16KB upstream)
--   template_language      DISPLAY-ONLY denormalization. NEVER on the wire: per INMA
--                          (2026-06-08) the send body has NO language field — each language
--                          is a separate template slug. Kept so run history shows the language
--                          even after the project is edited.
--   preview_skipped_params capped audit of recipients EXCLUDED at preview because a mapped
--                          required param resolved empty/NULL:
--                          {count, by_param:{key:n}, sample:[{masked_phone,list_id,record_id,missing:[..]}] (<=100)}
--                          (Codex plan-consult: a counter+log alone is not product audit.)
--
-- chk_bulk_message_source goes 2-way -> 3-way: EXACTLY ONE of
--   template_id (CSV/list + project gallery_template) |
--   inline_message_text (project free_text)           |
--   wa_template_id (project HSM run)
-- DATA-SAFETY: every existing row has exactly one of the first two set and
-- wa_template_id is brand-new (all NULL), so num_nonnulls(...)=1 holds for ALL
-- existing rows -> the replacement CHECK validates clean with ZERO backfill.
--
-- INERT / gate note: like 055-061 this ships safe. The HSM dispatch path is behind
-- the CxapiSend allowlist (preview AND confirm reject before any vendor call when the
-- tenant is not allowlisted); no tenant is allowlisted (P0-3), so no row can gain
-- wa_template_id in prod until the gate opens.
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: ADD COLUMN IF NOT EXISTS + named DROP/ADD CHECK. Safe to re-run.
-- bulk_send_jobs already GRANTed (migration 051) — column adds inherit table grants.
-- =============================================================

-- -------------------------------------------------------------
-- bulk_send_jobs: HSM snapshot columns
-- -------------------------------------------------------------
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS instance_id INTEGER;
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS template_kind VARCHAR(16);
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS wa_template_id VARCHAR(128);
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS param_mapping JSONB;
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS template_language VARCHAR(8);
ALTER TABLE bulk_send_jobs
    ADD COLUMN IF NOT EXISTS preview_skipped_params JSONB;

-- -------------------------------------------------------------
-- Exactly one content source per job (2-way -> 3-way)
-- -------------------------------------------------------------
ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS chk_bulk_message_source;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT chk_bulk_message_source
    CHECK (num_nonnulls(template_id, inline_message_text, wa_template_id) = 1);

-- template_kind value domain (mirrors chk_project_template_kind)
ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS chk_bulk_template_kind;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT chk_bulk_template_kind
    CHECK (template_kind IS NULL OR template_kind IN ('plain_text','wapcrm_template'));

-- HSM internal consistency: an HSM run MUST carry its slug + instance; a non-HSM
-- row must not carry HSM-only payload (wa_template_id is already excluded by the
-- XOR when another content source is set — this closes the kind/slug mismatch hole
-- and keeps param_mapping/template_language from leaking onto non-HSM rows).
ALTER TABLE bulk_send_jobs DROP CONSTRAINT IF EXISTS chk_bulk_hsm_consistency;
ALTER TABLE bulk_send_jobs ADD CONSTRAINT chk_bulk_hsm_consistency
    CHECK (
        CASE
            WHEN template_kind = 'wapcrm_template'
                THEN wa_template_id IS NOT NULL AND instance_id IS NOT NULL
            ELSE wa_template_id IS NULL AND param_mapping IS NULL AND template_language IS NULL
        END);

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-062)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_cols              INTEGER;
    v_source_def        TEXT;
    v_has_kind_check    BOOLEAN;
    v_has_hsm_check     BOOLEAN;
BEGIN
    SELECT COUNT(*) INTO v_cols
    FROM information_schema.columns
    WHERE table_name = 'bulk_send_jobs'
      AND column_name IN ('instance_id', 'template_kind', 'wa_template_id',
                          'param_mapping', 'template_language', 'preview_skipped_params');

    SELECT pg_get_constraintdef(oid) INTO v_source_def
    FROM pg_constraint
    WHERE conname = 'chk_bulk_message_source' AND contype = 'c';

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_bulk_template_kind' AND contype = 'c'
    ) INTO v_has_kind_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_bulk_hsm_consistency' AND contype = 'c'
    ) INTO v_has_hsm_check;

    IF v_cols <> 6 THEN
        RAISE EXCEPTION 'INV-SEED-062: bulk_send_jobs HSM snapshot columns missing (found %/6)', v_cols;
    END IF;
    IF v_source_def IS NULL OR v_source_def NOT LIKE '%wa_template_id%' THEN
        RAISE EXCEPTION 'INV-SEED-062: chk_bulk_message_source is not the 3-way XOR (def: %)', COALESCE(v_source_def, '<missing>');
    END IF;
    IF v_has_kind_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-062: chk_bulk_template_kind CHECK missing';
    END IF;
    IF v_has_hsm_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-062: chk_bulk_hsm_consistency CHECK missing';
    END IF;

    RAISE NOTICE 'INV-SEED-062 OK: bulk_send_jobs +6 HSM snapshot columns, 3-way exactly-one(template_id, inline_message_text, wa_template_id), kind domain + HSM consistency enforced (run config immutable at preview; prod-inert behind CxapiSend allowlist)';
END
$verify$;
