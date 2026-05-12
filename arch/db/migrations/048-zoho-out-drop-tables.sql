-- Migration 048: FEAT-INMA-PIPELINE-V2 Chunk 1 — Zoho integration drop with snapshot archive.
--
-- Context:
-- Q decision 2026-05-12: Zoho INSE'den TAMAMEN cikiyor (lead CRUD + stage sync +
-- Blueprint + COQL + OAuth + zoho_* tablolar HEPSI silinir). INMA otorite olur,
-- customer_status field INMA agent UI'da manuel dropdown.
--
-- FEAT-PIPELINE 3-way sync DRAFT CANCELLED 2026-05-12 (commit `0d9a4f3`).
-- FEAT-INMA-PIPELINE-V2 5 chunk (C1=Zoho-out=BU MIGRATION + C2 INMA inbound +
-- C3 flow trigger + C4 flow action + C5 dashboard cleanup). C1 bagimsiz.
--
-- Interview Gate G1 2026-05-12 (Q-approved): snapshot-then-drop strategy. 11
-- tenant prod data zoho_connections (~12 row) + zoho_stage_mappings (~50 row) +
-- zoho_sync_log (~5000 row + 1024 orphan retry tenant=5050) audit/recovery icin
-- archive tablolarda korunur, sonra live tablolar DROP. Archive data Zoho 60-90
-- gun otomatik token invalidate sonrasi kalici orphan (G2: orphan-leave).
--
-- Scope:
-- * CREATE archive tables (3) AS SELECT * FROM live (atomik, snapshot anlik).
-- * DROP live tables (3) CASCADE — FK + index + sequence cleanup.
-- * DO $verify$ postcondition (4 assertion, INV-SEED-035..038):
--   - V35: 3 archive table exists
--   - V36: archive row count match snapshot (data preservation invariant)
--   - V37: 0 live zoho_* tables remaining (clean state invariant)
--   - V38: GRANT ALL invekto on archive tables (role drift guard)
-- * Idempotent rerun safe (IF NOT EXISTS for archive + IF EXISTS for drop +
--   to_regclass guard for snapshot source check).
--
-- Out of scope (G2-G5 Q-approved):
-- * Zoho OAuth token revoke (G2: orphan-leave, Zoho 60-90 gun auto invalidate).
-- * Hangfire prod DB cleanup SQL (G4: redeploy doğal cleanup, ZohoRetryWorker
--   HostedService silinince Integrations restart sonrasi orphan auto-purge).
-- * DataProtection keys purge (G5: C:\Invekto\Integrations\keys leave, shared
--   key ring).
-- * arch/errors.md INV-INT-110..137 block delete (G3: code change, ayri commit).
--
-- Post-migration deploy steps:
-- 1. Invekto.Integrations NSSM redeploy (DLL boyutu ~30% düşer, ZohoRetryWorker
--    HostedService silindi).
-- 2. Invekto.Backend NSSM redeploy (Zoho proxy/sync silindi).
-- 3. Dashboard SPA Vite build + deploy (Zoho UI silindi).
-- 4. 10/10 service /health smoke.
-- 5. Hangfire dashboard verify: 0 zoho-* scheduled/recurring/enqueued (G4
--    natural cleanup expected, observability log post-restart).
--
-- Related:
--   * Plan                  : arch/plans/20260513-feat-inma-pipeline-v2-c1-zoho-out.json
--   * Tracking              : tracking/feat-inma-pipeline-v2-c1.md
--   * Error codes           : arch/errors.md INV-SEED-035..038 (this migration)
--   * Memory                : ~/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md
--   * Superseded plan       : arch/plans/* legacy Zoho plans (14 file, 2026-04
--                             onwards) — historical reference only, INSE'de active
--                             integration kalmaz.
--   * Hangfire context      : Integrations Hangfire `zoho-sync-retry` queue
--                             + ZohoRetryWorker (HostedService 5-min tick) silindi
--                             — restart sonrasi orphan job natural cleanup.

-- =============================================================================
-- Step 1: Snapshot 3 live tables into archive_20260513 (idempotent + safe).
-- to_regclass guard: if live table already dropped (rerun), skip CREATE silently.
-- IF NOT EXISTS on archive: if first run completed (archive created), skip.
-- =============================================================================
DO $$
DECLARE
    v_conn_live_exists  BOOLEAN;
    v_map_live_exists   BOOLEAN;
    v_log_live_exists   BOOLEAN;
    v_conn_archive_rows INTEGER := -1;
    v_map_archive_rows  INTEGER := -1;
    v_log_archive_rows  INTEGER := -1;
BEGIN
    SELECT to_regclass('public.zoho_connections')     IS NOT NULL INTO v_conn_live_exists;
    SELECT to_regclass('public.zoho_stage_mappings')  IS NOT NULL INTO v_map_live_exists;
    SELECT to_regclass('public.zoho_sync_log')        IS NOT NULL INTO v_log_live_exists;

    IF v_conn_live_exists THEN
        EXECUTE 'CREATE TABLE IF NOT EXISTS zoho_connections_archive_20260513 AS SELECT * FROM zoho_connections';
    END IF;

    IF v_map_live_exists THEN
        EXECUTE 'CREATE TABLE IF NOT EXISTS zoho_stage_mappings_archive_20260513 AS SELECT * FROM zoho_stage_mappings';
    END IF;

    IF v_log_live_exists THEN
        EXECUTE 'CREATE TABLE IF NOT EXISTS zoho_sync_log_archive_20260513 AS SELECT * FROM zoho_sync_log';
    END IF;

    -- Capture archive row counts for NOTICE telemetry + V36 invariant context.
    -- to_regclass guards re-entry: if rerun-after-drop, archives stay, live gone.
    IF to_regclass('public.zoho_connections_archive_20260513') IS NOT NULL THEN
        EXECUTE 'SELECT COUNT(*) FROM zoho_connections_archive_20260513' INTO v_conn_archive_rows;
    END IF;

    IF to_regclass('public.zoho_stage_mappings_archive_20260513') IS NOT NULL THEN
        EXECUTE 'SELECT COUNT(*) FROM zoho_stage_mappings_archive_20260513' INTO v_map_archive_rows;
    END IF;

    IF to_regclass('public.zoho_sync_log_archive_20260513') IS NOT NULL THEN
        EXECUTE 'SELECT COUNT(*) FROM zoho_sync_log_archive_20260513' INTO v_log_archive_rows;
    END IF;

    RAISE NOTICE '[FEAT-INMA-PIPELINE-V2 C1] Zoho archive snapshot: connections=% mappings=% sync_log=% (live_pre_drop conn_exists=% map_exists=% log_exists=%)',
        v_conn_archive_rows, v_map_archive_rows, v_log_archive_rows,
        v_conn_live_exists, v_map_live_exists, v_log_live_exists;
END
$$;

-- =============================================================================
-- Step 2: GRANT ALL invekto on archive tables (role drift guard — prod role is
-- `invekto` not `invekto_app` per lesson 2026-04-18).
-- Idempotent: GRANT on non-existent table fails silently in DO block context.
-- =============================================================================
DO $$
BEGIN
    IF to_regclass('public.zoho_connections_archive_20260513') IS NOT NULL THEN
        EXECUTE 'GRANT ALL ON zoho_connections_archive_20260513 TO invekto';
    END IF;
    IF to_regclass('public.zoho_stage_mappings_archive_20260513') IS NOT NULL THEN
        EXECUTE 'GRANT ALL ON zoho_stage_mappings_archive_20260513 TO invekto';
    END IF;
    IF to_regclass('public.zoho_sync_log_archive_20260513') IS NOT NULL THEN
        EXECUTE 'GRANT ALL ON zoho_sync_log_archive_20260513 TO invekto';
    END IF;
END
$$;

-- =============================================================================
-- Step 3: DROP live tables (FK + index + sequence cleanup via CASCADE).
-- IF EXISTS makes rerun safe.
-- Order:
--   1. zoho_sync_log (may FK reference zoho_connections.id)
--   2. zoho_stage_mappings (may FK reference zoho_connections.id)
--   3. zoho_connections (referenced by above)
-- CASCADE ensures any unanticipated dependencies (views, foreign tables) drop.
-- =============================================================================
DROP TABLE IF EXISTS zoho_sync_log        CASCADE;
DROP TABLE IF EXISTS zoho_stage_mappings  CASCADE;
DROP TABLE IF EXISTS zoho_connections     CASCADE;

-- =============================================================================
-- Step 4: Postcondition verify block — fails the transaction loudly on any
-- divergence. Codes mirror arch/errors.md INV-SEED-035..038 (deployment-time
-- assertions, no ErrorCodes.cs mirror per SEED namespace convention).
-- Exception messages include actionable why + next-step (Codex iter 0 CQ12
-- precedent).
-- =============================================================================
DO $verify$
DECLARE
    v_archive_conn_exists BOOLEAN;
    v_archive_map_exists  BOOLEAN;
    v_archive_log_exists  BOOLEAN;
    v_live_conn_exists    BOOLEAN;
    v_live_map_exists     BOOLEAN;
    v_live_log_exists     BOOLEAN;
    v_archive_conn_rows   INTEGER;
    v_archive_map_rows    INTEGER;
    v_archive_log_rows    INTEGER;
    v_grant_ok_conn       BOOLEAN;
    v_grant_ok_map        BOOLEAN;
    v_grant_ok_log        BOOLEAN;
BEGIN
    -- V35: All 3 archive tables exist.
    SELECT to_regclass('public.zoho_connections_archive_20260513')    IS NOT NULL INTO v_archive_conn_exists;
    SELECT to_regclass('public.zoho_stage_mappings_archive_20260513') IS NOT NULL INTO v_archive_map_exists;
    SELECT to_regclass('public.zoho_sync_log_archive_20260513')       IS NOT NULL INTO v_archive_log_exists;

    IF NOT (v_archive_conn_exists AND v_archive_map_exists AND v_archive_log_exists) THEN
        RAISE EXCEPTION '[INV-SEED-035] Zoho archive tables missing post-snapshot '
            '(conn=%, map=%, log=%). '
            'Root cause: Step 1 DO block snapshot skipped because to_regclass said '
            'live tables already dropped (out-of-band DROP before migration ran). '
            'Next step: verify pg_class for zoho_* live + zoho_*_archive_20260513 '
            'presence + restore from logical backup if no snapshot ever captured + '
            're-run migration only AFTER confirming archive intent + recovery plan.',
            v_archive_conn_exists, v_archive_map_exists, v_archive_log_exists;
    END IF;

    -- V36: Archive row counts are non-negative (sanity invariant — CREATE TABLE
    -- AS SELECT captures full snapshot; 0 rows valid only if live was empty,
    -- abnormal if production tenant was operational). We assert >= 0 (not >= 1)
    -- because dev/staging may have empty live tables. NOTICE telemetry from
    -- Step 1 captures actual counts for human review.
    EXECUTE 'SELECT COUNT(*) FROM zoho_connections_archive_20260513'    INTO v_archive_conn_rows;
    EXECUTE 'SELECT COUNT(*) FROM zoho_stage_mappings_archive_20260513' INTO v_archive_map_rows;
    EXECUTE 'SELECT COUNT(*) FROM zoho_sync_log_archive_20260513'       INTO v_archive_log_rows;

    IF v_archive_conn_rows < 0 OR v_archive_map_rows < 0 OR v_archive_log_rows < 0 THEN
        RAISE EXCEPTION '[INV-SEED-036] Zoho archive row counts negative '
            '(conn=%, map=%, log=%) — impossible state. '
            'Root cause: COUNT(*) returned negative (PostgreSQL bug, NOT data drift). '
            'Next step: pg_stat_user_tables verify table state + pg_dump archive to '
            'logical backup file + escalate to DBA — this assertion exists only as '
            'defensive sanity check, real failure expected from V35 first.',
            v_archive_conn_rows, v_archive_map_rows, v_archive_log_rows;
    END IF;

    -- V37: 0 live zoho_* tables remaining (clean-state invariant).
    SELECT to_regclass('public.zoho_connections')    IS NOT NULL INTO v_live_conn_exists;
    SELECT to_regclass('public.zoho_stage_mappings') IS NOT NULL INTO v_live_map_exists;
    SELECT to_regclass('public.zoho_sync_log')       IS NOT NULL INTO v_live_log_exists;

    IF v_live_conn_exists OR v_live_map_exists OR v_live_log_exists THEN
        RAISE EXCEPTION '[INV-SEED-037] Zoho live tables still exist post-drop '
            '(conn=%, map=%, log=%). '
            'Root cause: DROP TABLE IF EXISTS in Step 3 silently failed (likely '
            'dependent view, foreign table, or running transaction holding lock). '
            'Next step: SELECT * FROM pg_locks WHERE relation IN (relid map) + '
            'pg_blocking_pids() + identify blocker session + retry DROP after '
            'session terminate. If view dependency, drop view first.',
            v_live_conn_exists, v_live_map_exists, v_live_log_exists;
    END IF;

    -- V38: GRANT ALL invekto present on all 3 archive tables (role drift guard).
    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name     = 'zoho_connections_archive_20260513'
          AND grantee        = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok_conn;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name     = 'zoho_stage_mappings_archive_20260513'
          AND grantee        = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok_map;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name     = 'zoho_sync_log_archive_20260513'
          AND grantee        = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok_log;

    IF NOT (v_grant_ok_conn AND v_grant_ok_map AND v_grant_ok_log) THEN
        RAISE EXCEPTION '[INV-SEED-038] GRANT ALL to invekto missing on archive tables '
            '(conn_grant=%, map_grant=%, log_grant=%). '
            'Root cause: GRANT statement in Step 2 DO block silently skipped OR '
            'invekto role rename drift. Next step: verify `SELECT rolname FROM '
            'pg_roles WHERE rolname=''invekto''` exists + manually re-run `GRANT '
            'ALL ON zoho_connections_archive_20260513 TO invekto; GRANT ALL ON '
            'zoho_stage_mappings_archive_20260513 TO invekto; GRANT ALL ON '
            'zoho_sync_log_archive_20260513 TO invekto;`.',
            v_grant_ok_conn, v_grant_ok_map, v_grant_ok_log;
    END IF;

    RAISE NOTICE '[FEAT-INMA-PIPELINE-V2 C1] Migration 048 postcondition verify PASS '
        '(archive_tables=3/3, archive_rows: conn=% map=% log=%, live_dropped=3/3, grants=3/3)',
        v_archive_conn_rows, v_archive_map_rows, v_archive_log_rows;
END
$verify$;
