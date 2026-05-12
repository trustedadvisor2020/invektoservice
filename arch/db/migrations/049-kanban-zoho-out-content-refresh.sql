-- Migration 049: FEAT-INMA-PIPELINE-V2 Chunk 5 — Kanban kart Zoho-out content refresh.
--
-- Context:
-- C1 (Zoho-out kod silme) 2026-05-12 17:50 UTC DONE+DEPLOYED+SMOKED (commit `0c0733b`).
-- C5 (bu migration) bağımsız doc + DB-content refresh paketi:
--  * Kod sıfır, service deploy yok, sadece dashboard /yol-haritasi/inse 6 kartının
--    body_markdown'ı V2 context'e çekilir (C003/C004/D007/D019/D027/K012).
--  * Migration 046 (kanban purpose+scenario) Zoho referansları içeriyordu — bu
--    migration prepend pattern ile V2 marker + V2 context açıklaması ekler.
--  * Eski "Ne ise yarar / Senaryo" content korunur (audit trail).
--
-- Interview Gate G2 2026-05-12 (Q-approved): Migration 049 atomik UPDATE — tek tx
-- içinde 6 kart body_markdown rewrite + DO $verify$ INV-SEED-039..045 fail-loud
-- postcondition + dent_paket_kanban_zoho_refresh_archive_20260513 pre-state snapshot.
--
-- Idempotency: WHERE body_markdown NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%' — re-run
-- sonrası 0 row match (no-op). Archive table CREATE IF NOT EXISTS.
--
-- Scope:
--   * Step 1: CREATE TABLE dent_paket_kanban_zoho_refresh_archive_20260513 AS
--             SELECT * FROM kanban_cards WHERE ref_code IN (6 list) AND
--             board_key='inse' — 6 row pre-state preservation.
--   * Step 2: 6 UPDATE statement prepend V2 marker + V2 context.
--   * Step 3: DO $verify$ INV-SEED-039..045 7 postcondition.
--
-- Out of scope:
--   * Kod değişikliği (C1'de tamamlandı).
--   * Migration 046 üzerine ALTER (historical kalır, audit trail).
--   * tracking/feat-inma-pipeline-v2-c1.md edit (audit trail dokunulmaz).
--
-- Related:
--   * Plan                  : arch/plans/20260513-feat-inma-pipeline-v2-c5-dashboard-cleanup.json
--   * Tracking              : tracking/feat-inma-pipeline-v2-c5.md
--   * Predecessor migration : arch/db/migrations/048-zoho-out-drop-tables.sql (C1)
--   * Kanban source         : arch/db/migrations/046-kanban-purpose-scenario.sql
--   * Canonical mirror      : arch/db/kanban-board.sql (comment block)
--   * Error codes           : arch/errors.md INV-SEED-039..045 (this migration)
--   * Memory                : ~/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md

-- =============================================================================
-- Explicit transaction wrapper: tüm migration atomic execute.
-- Tek bir RAISE EXCEPTION (DO $verify$) full ROLLBACK fırlatır → snapshot + 6 UPDATE
-- + postcondition aynı transaction'da. Runner autocommit'i (MCP, psql, vs.) tek
-- statement-stream gönderse bile explicit BEGIN/COMMIT artifact-level atomicity
-- garantisi sağlar (Codex iter 1 CQ11 fix).
-- =============================================================================
BEGIN;

-- =============================================================================
-- Step 1: Snapshot 6 kanban kart pre-state into archive table.
-- IF NOT EXISTS guard: re-run safe (archive zaten varsa skip).
-- =============================================================================
-- Schema DDL + data INSERT iki adıma ayrıldı (Codex iter 3 CQ11 fix):
-- (a) CREATE TABLE IF NOT EXISTS (LIKE kanban_cards INCLUDING DEFAULTS) — parent
--     schema'dan defaults+constraint'ler türetilir; arch/db/kanban-board.sql
--     canonical mirror DDL ile birebir aynı (drift yok).
-- (b) INSERT INTO ... SELECT — yalnız ilk run'da 6 row populate; idempotent guard
--     ile re-run skip (zaten yüklü).
-- Eski CTAS pattern (CREATE TABLE AS SELECT) yerine bu refactor; CTAS defaults
-- preserve ETMEZ, LIKE preserve EDER → kanban-board.sql DDL ile tutarlılık.

CREATE TABLE IF NOT EXISTS dent_paket_kanban_zoho_refresh_archive_20260513
    (LIKE kanban_cards INCLUDING DEFAULTS);

GRANT ALL ON dent_paket_kanban_zoho_refresh_archive_20260513 TO invekto;

DO $$
DECLARE
    v_existing_count INTEGER;
BEGIN
    -- Idempotent data populate guard: archive zaten doluysa skip (re-run safe).
    SELECT COUNT(*) INTO v_existing_count
      FROM dent_paket_kanban_zoho_refresh_archive_20260513;

    IF v_existing_count = 0 THEN
        -- tenant_id IS NULL filter: inse board = ops-scope (cross-tenant kartlar yok).
        -- Pre-flight query verify: 6 kart hepsi tenant_id=NULL (2026-05-12 prod).
        INSERT INTO dent_paket_kanban_zoho_refresh_archive_20260513
        SELECT *
          FROM kanban_cards
         WHERE board_key = 'inse'
           AND tenant_id IS NULL
           AND ref_code IN ('C003', 'C004', 'D007', 'D019', 'D027', 'K012');

        RAISE NOTICE 'Archive table populated: 6 rows snapshot.';
    ELSE
        RAISE NOTICE 'Archive table already populated (% rows), skipping snapshot (idempotent re-run).', v_existing_count;
    END IF;
END
$$;

-- =============================================================================
-- Step 2: 6 kart body_markdown V2 context prepend.
-- Idempotent guard: body_markdown NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%' → re-run
-- sonrası 0 row match.
-- =============================================================================

-- C003: Müşteri lead pipeline tanımı → INMA agent UI customer_status dropdown
-- NULL-safe guard: COALESCE(body_markdown,'') NOT LIKE ... yerine string concat sonrası substring kontrol;
-- direct LIKE NULL'da NULL döner ve UPDATE skip eder. COALESCE ile defansif değerlendirme.
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12:** Müşteri lead pipeline tanımı artık INMA agent UI''da `customer_status` field manuel dropdown olarak yönetilir (V2 INMA-otorite). Zoho CRM Blueprint integration TAMAMEN iptal (commit `0c0733b`). INMA agent customer_status değiştirdiğinde INMA→INSE webhook (`POST /api/v1/inbound/inma/customer-status-change`) ile INSE opaque TEXT olarak saklar (C2 BLOCKED INMA contract).\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'C003'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- C004: Zoho OAuth bağlanma → tamamen deprecated
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12 (DEPRECATED kart):** Zoho OAuth integration TAMAMEN silindi (commit `0c0733b`). zoho_connections + zoho_stage_mappings + zoho_sync_log live tablolar Migration 048 ile archive''lendi + DROP edildi. Bu kart historical referans için açık tutulur — operasyonel etki YOK, scope tamamen kapandı.\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'C004'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- D007: Slot + Zoho takvim sync → sadece Google Meet Mock + slot management
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12:** Zoho takvim çift yönlü sync iptal — Slot tanımlama + takvim view + meeting link kopyalama scope''ta kalır. Google Meet entegrasyonu FEAT-VCP Chunk A Mock provider (DEPLOYED 2026-04-19) + Chunk C prod OAuth (BACKLOG B0 Q Google Workspace provision bekliyor) ile devam eder. Zoho takvim push/pull YOK.\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'D007'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- D019: INMA sohbet paneli pipeline status dropdown → one-way INMA→INSE
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12:** Çift yönlü sync (eski FEAT-PIPELINE 3-way) iptal — yerine one-way INMA→INSE webhook. INMA agent panelinde `customer_status` dropdown manuel set, INMA→INSE webhook (`POST /api/v1/inbound/inma/customer-status-change`) ile INSE opaque TEXT saklar. Zoho forward YOK. Flow Builder 4. trigger kanalı `customer_status_changed` + action node `Set Customer Status` (FEAT-INMA-PIPELINE-V2 C3/C4 BLOCKED INMA contract).\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'D019'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- D027: Outbound bulk INMA contact + Zoho COQL → sadece INMA contact
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12:** Zoho COQL dış kaynak adapter iptal — FEAT-OBI Faz 1 sadece INMA contact adapter + opsiyonel CSV upload (Zoho COQL kalktı). Outbound `/broadcast/send` mevcut (max 1000 recipient + opt-out filter), recipient list source: INMA contact API (kontrat verify pending) veya CSV upload.\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'D027'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- K012: Lead pipeline sahipliği INMA'da + 3-way sync → INMA-otorite one-way
UPDATE kanban_cards
   SET body_markdown = E'**⚠ FEAT-INMA-PIPELINE-V2 C1 — Zoho-out 2026-05-12:** Eski 3-way sync mimari (Invekto + Zoho INMA''dan çeker) iptal. V2: Lead pipeline (`customer_status`) sahipliği TAMAMEN INMA''da. INSE INMA→INSE webhook ile customer_status''u opaque TEXT olarak saklar, validate ETMEZ, Zoho''ya forward ETMEZ. One-way INMA→INSE, FEAT-PIPELINE 3-way DRAFT CANCELLED.\n\n---\n\n'
                       || COALESCE(body_markdown, ''),
       updated_at = NOW()
 WHERE board_key = 'inse'
   AND tenant_id IS NULL
   AND ref_code = 'K012'
   AND COALESCE(body_markdown, '') NOT LIKE '%FEAT-INMA-PIPELINE-V2 C1%';

-- =============================================================================
-- Step 3: DO $verify$ INV-SEED-039..045 fail-loud postcondition.
-- 7 assertion: archive row count + 6 kart V2 marker present.
-- =============================================================================
-- DO $verify$ NULL-safe pattern: IS DISTINCT FROM TRUE yakalar (NULL veya FALSE
-- her ikisini de exception fırlatır). LIKE NULL döndürürse v_* NULL kalır;
-- IS DISTINCT FROM TRUE = TRUE (NULL durumda) → RAISE EXCEPTION fail-loud.
-- Ayrıca SELECT'in row dönmemesi durumunda (kart silinmiş = NOT FOUND) v_* NULL
-- kalır; IS DISTINCT FROM TRUE yine RAISE EXCEPTION fire eder (silent pass YOK).
DO $verify$
DECLARE
    v_archive_count INTEGER;
    v_c003_v2       BOOLEAN;
    v_c004_v2       BOOLEAN;
    v_d007_v2       BOOLEAN;
    v_d019_v2       BOOLEAN;
    v_d027_v2       BOOLEAN;
    v_k012_v2       BOOLEAN;
BEGIN
    -- INV-SEED-039: archive table 6 row preserved
    SELECT COUNT(*) INTO v_archive_count FROM dent_paket_kanban_zoho_refresh_archive_20260513;
    IF v_archive_count IS DISTINCT FROM 6 THEN
        RAISE EXCEPTION 'INV-SEED-039: archive table row count mismatch — expected 6, got %', COALESCE(v_archive_count::text, 'NULL');
    END IF;

    -- INV-SEED-040: C003 V2 marker present (NULL-safe: IS DISTINCT FROM TRUE)
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_c003_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='C003';
    IF v_c003_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-040: C003 body_markdown V2 marker missing (or card not found)';
    END IF;

    -- INV-SEED-041: C004 V2 marker present
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_c004_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='C004';
    IF v_c004_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-041: C004 body_markdown V2 marker missing (or card not found)';
    END IF;

    -- INV-SEED-042: D007 V2 marker present
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_d007_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='D007';
    IF v_d007_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-042: D007 body_markdown V2 marker missing (or card not found)';
    END IF;

    -- INV-SEED-043: D019 V2 marker present
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_d019_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='D019';
    IF v_d019_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-043: D019 body_markdown V2 marker missing (or card not found)';
    END IF;

    -- INV-SEED-044: D027 V2 marker present
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_d027_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='D027';
    IF v_d027_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-044: D027 body_markdown V2 marker missing (or card not found)';
    END IF;

    -- INV-SEED-045: K012 V2 marker present
    SELECT (COALESCE(body_markdown, '') LIKE '%FEAT-INMA-PIPELINE-V2 C1%') INTO v_k012_v2
      FROM kanban_cards WHERE board_key='inse' AND tenant_id IS NULL AND ref_code='K012';
    IF v_k012_v2 IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-045: K012 body_markdown V2 marker missing (or card not found)';
    END IF;

    RAISE NOTICE 'Migration 049 verify PASS: 7/7 postcondition (INV-SEED-039..045) silent (NULL-safe).';
END
$verify$;

-- =============================================================================
-- Commit: tüm değişiklikler atomic commit. RAISE EXCEPTION yukarıda olursa
-- otomatik ROLLBACK; buraya gelirse 6 UPDATE + archive table + GRANT atomic
-- olarak persiste edilir.
-- =============================================================================
COMMIT;
