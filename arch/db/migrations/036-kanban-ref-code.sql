-- Migration 036: FEAT-ROADMAP-V2-REFCODE — kart referans kodu (1 harf + 3 rakam)
--
-- Q karari 2026-04-28: Pilot Kanban → Yol Haritasi rename + multi-board destegi
-- + her karta unique ref_code (kategori prefix + 3 rakam, ornek: K005, D021, C001).
--
-- Format:
--   C001-C005 = CUSTOMER (Musteri / 3rd-party)        : 5 kart
--   K001-K012 = DECISION (Karar bekleyenler)           : 12 kart
--   O001-O009 = OPS (Invekto operasyon)                : 9 kart
--   D001-D026 = DEV (Yazilim gelistirme)               : 26 kart
--   U001-U009 = UI (UI ekrani eksik)                   : 9 kart
--   X001-X003 = DOC (Dokuman / tracking)               : 3 kart
--   ----     = placeholder default (CHECK izinli)
--
-- Kategori prefix sabit (kart kategori degisimi nadir, migration seed ile baslar);
-- ref_code mnemonic kart kimligi olarak Q + musteri iletisiminde 4-karakter kisalik:
-- "K005 yarin halledilir", "/wrap D021=DONE" — slug yerine ref_code kullanilabilir.
--
-- Idempotency: ADD COLUMN IF NOT EXISTS, ADD CONSTRAINT IF NOT EXISTS pattern,
-- 64 UPDATE statement WHERE ref_code = '----' (default) — re-run mevcut atanan
-- ref_code'u override etmez. Postcondition INV-SEED-026 RAISE EXCEPTION fail-loud.
--
-- Related:
--   * Plan         : arch/plans/20260428-feat-roadmap-v2-refcode.json
--   * Tracking     : tracking/20260428-feat-roadmap-v2-refcode.md
--   * Mirror       : arch/db/kanban-board.sql (ref_code kolonu eklendi)
--   * Error code   : INV-SEED-026 (postcondition guard)

-- §1. ref_code kolonu (idempotent ADD).
ALTER TABLE kanban_cards
    ADD COLUMN IF NOT EXISTS ref_code VARCHAR(4) NOT NULL DEFAULT '----';

-- §2. CHECK constraint: '^[A-Z][0-9]{3}$' VEYA '----' (placeholder).
DO $check_ref$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_kanban_cards_ref_code') THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT chk_kanban_cards_ref_code
            CHECK (ref_code = '----' OR ref_code ~ '^[A-Z][0-9]{3}$');
    END IF;
END$check_ref$;

-- §3. UNIQUE (board_key, ref_code) — placeholder '----' multi-row icin partial.
-- Birden fazla kart '----' olabilir (yeni eklenen, henuz atanmamis); ref_code
-- atanan kart'lar (regex match) per-board UNIQUE.
DO $unique_ref$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_kanban_cards_board_ref') THEN
        -- Partial UNIQUE: yalnizca atanan ref_code'lar UNIQUE; '----' placeholder'lar excluded.
        CREATE UNIQUE INDEX uq_kanban_cards_board_ref
            ON kanban_cards (board_key, ref_code)
            WHERE ref_code <> '----';
    END IF;
END$unique_ref$;

-- §4. Mevcut 64 dent-pilot kart icin ref_code UPDATE (kategori-bazli).
-- WHERE ref_code = '----' — mevcut atanmis ref_code override edilmez (idempotent re-run).

-- §4.1 CUSTOMER: C001-C005 (BLOCKED kolonu)
UPDATE kanban_cards SET ref_code = 'C001' WHERE board_key = 'dent-pilot' AND card_slug = 'meta-hsm-template'      AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'C002' WHERE board_key = 'dent-pilot' AND card_slug = 'meta-app-setup'         AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'C003' WHERE board_key = 'dent-pilot' AND card_slug = 'zoho-blueprint'         AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'C004' WHERE board_key = 'dent-pilot' AND card_slug = 'zoho-oauth'             AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'C005' WHERE board_key = 'dent-pilot' AND card_slug = 'waba-number'            AND ref_code = '----';

-- §4.2 DECISION: K001-K012 (K1-K10 BLOCKED + photo karari TODO + lead-stages-inma DONE)
UPDATE kanban_cards SET ref_code = 'K001' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k1-pilot-date'        AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K002' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k2-gunes-persona'     AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K003' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k3-languages'         AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K004' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k4-pricing'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K005' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k5-ad-management'     AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K006' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k6-after-hours'       AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K007' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k7-coordinator'       AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K008' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k8-doctor-capacity'   AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K009' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k9-cities'            AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K010' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-k10-data-retention'   AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K011' WHERE board_key = 'dent-pilot' AND card_slug = 'decision-photo-flow-pilot-usage' AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'K012' WHERE board_key = 'dent-pilot' AND card_slug = 'lead-stages-inma-ownership'    AND ref_code = '----';

-- §4.3 OPS: O001-O009 (TODO ops + training)
UPDATE kanban_cards SET ref_code = 'O001' WHERE board_key = 'dent-pilot' AND card_slug = 'dashboard-config-7-pages'        AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O002' WHERE board_key = 'dent-pilot' AND card_slug = 'inma-allowlist'                  AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O003' WHERE board_key = 'dent-pilot' AND card_slug = 'stage1-day1-smoke'               AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O004' WHERE board_key = 'dent-pilot' AND card_slug = 'stage1-day2-edge'                AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O005' WHERE board_key = 'dent-pilot' AND card_slug = 'stage1-go-no-go'                 AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O006' WHERE board_key = 'dent-pilot' AND card_slug = 'training-coordinator-screencast' AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O007' WHERE board_key = 'dent-pilot' AND card_slug = 'training-ai-supervise-screencast' AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O008' WHERE board_key = 'dent-pilot' AND card_slug = 'training-kickoff-call'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'O009' WHERE board_key = 'dent-pilot' AND card_slug = 'contract-quirks-pre-smoke'       AND ref_code = '----';

-- §4.4 DEV: D001-D026 (monitoring + IN_PROGRESS + BACKLOG dev + DONE)
UPDATE kanban_cards SET ref_code = 'D001' WHERE board_key = 'dent-pilot' AND card_slug = 'monitoring-waa-webhook-alert'   AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D002' WHERE board_key = 'dent-pilot' AND card_slug = 'monitoring-flow-stuck-alert'    AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D003' WHERE board_key = 'dent-pilot' AND card_slug = 'monitoring-llm-cost-alert'      AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D004' WHERE board_key = 'dent-pilot' AND card_slug = 'monitoring-intent-anomaly'      AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D005' WHERE board_key = 'dent-pilot' AND card_slug = 'lead-list-page'                 AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D006' WHERE board_key = 'dent-pilot' AND card_slug = 'lead-detail-timeline'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D007' WHERE board_key = 'dent-pilot' AND card_slug = 'appointments-page-zoho-sync'    AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D008' WHERE board_key = 'dent-pilot' AND card_slug = 'doctors-management-page'        AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D009' WHERE board_key = 'dent-pilot' AND card_slug = 'conversion-funnel-chart'        AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D010' WHERE board_key = 'dent-pilot' AND card_slug = 'b-c2-welcome-overhaul'          AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D011' WHERE board_key = 'dent-pilot' AND card_slug = 'b0-google-meet-oauth'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D012' WHERE board_key = 'dent-pilot' AND card_slug = 'feat-clinic-metadata'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D013' WHERE board_key = 'dent-pilot' AND card_slug = 'tenant-default-seed'            AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D014' WHERE board_key = 'dent-pilot' AND card_slug = 'tenant-suspend-button'          AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D015' WHERE board_key = 'dent-pilot' AND card_slug = 'gap-multi-channel'              AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D016' WHERE board_key = 'dent-pilot' AND card_slug = 'gap-quote-states'               AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D017' WHERE board_key = 'dent-pilot' AND card_slug = 'gap-24h-auto-cancel'            AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D018' WHERE board_key = 'dent-pilot' AND card_slug = 'gap-kpi-dashboard'              AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D019' WHERE board_key = 'dent-pilot' AND card_slug = 'gap-inma-pipeline-dropdown'     AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D020' WHERE board_key = 'dent-pilot' AND card_slug = 'pilot-15-packets'               AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D021' WHERE board_key = 'dent-pilot' AND card_slug = 'b-meta-webhook'                 AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D022' WHERE board_key = 'dent-pilot' AND card_slug = 'p10-hangfire-queue'             AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D023' WHERE board_key = 'dent-pilot' AND card_slug = 'migration-031-doctors'          AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D024' WHERE board_key = 'dent-pilot' AND card_slug = 'migration-033-meta-leadgen'     AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D025' WHERE board_key = 'dent-pilot' AND card_slug = 'migration-034-photo-flow'       AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'D026' WHERE board_key = 'dent-pilot' AND card_slug = 'paket-a-plan-tier'              AND ref_code = '----';

-- §4.5 UI: U001-U009 (BACKLOG UI #10-#18)
UPDATE kanban_cards SET ref_code = 'U001' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-coordinator-handoff-queue' AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U002' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-notification-hub'          AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U003' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-doctor-briefing'           AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U004' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-quote-prep'                AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U005' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-daily-ops'                 AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U006' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-gdpr-erasure'              AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U007' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-audit-log'                 AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U008' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-bulk-message'              AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'U009' WHERE board_key = 'dent-pilot' AND card_slug = 'ui-complaint-mgmt'            AND ref_code = '----';

-- §4.6 DOC: X001-X003 (TODO faq + BACKLOG archive + DONE consolidation)
UPDATE kanban_cards SET ref_code = 'X001' WHERE board_key = 'dent-pilot' AND card_slug = 'training-faq-cheatsheet'      AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'X002' WHERE board_key = 'dent-pilot' AND card_slug = 'lessons-learned-archive'     AND ref_code = '----';
UPDATE kanban_cards SET ref_code = 'X003' WHERE board_key = 'dent-pilot' AND card_slug = 'section-19-24-consolidation' AND ref_code = '----';

-- §5. Postcondition guard (INV-SEED-026).
DO $verify_ref$
DECLARE
    assigned_count integer;
BEGIN
    SELECT COUNT(*) INTO assigned_count
      FROM kanban_cards
     WHERE board_key = 'dent-pilot'
       AND ref_code <> '----'
       AND ref_code ~ '^[A-Z][0-9]{3}$';

    IF assigned_count < 60 THEN
        RAISE EXCEPTION '[INV-SEED-026] kanban_cards ref_code seed eksik: dent-pilot board icin % kart atanmis ref_code, en az 60 bekleniyordu.', assigned_count;
    END IF;

    RAISE NOTICE 'Migration 036 OK: % dent-pilot kart ref_code atandi.', assigned_count;
END$verify_ref$;
