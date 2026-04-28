-- Migration 037: FEAT-ROADMAP-V3-DEPENDS — kart bagimlilik referansi
--
-- Q istegi 2026-04-28 21:52: "kutularda dependency de olsun"
-- Her karta diger kart(lar)in ref_code listesi (CSV) ile bagimlilik tanitilir.
-- UI: kart altinda kucuk "↳ C001, C003" indicator + drawer'da tiklanabilir.
--
-- Format:
--   depends_on TEXT NULL — null (bagimsiz) VEYA "C001" VEYA "C001,C003" (CSV)
--   Regex: '^([A-Z][0-9]{3})(,[A-Z][0-9]{3})*$'
--
-- Bagimlilik scope (en kritik 22 kart, kalan 42 kart bagimsiz NULL):
--   Stage 1 zinciri:    O001 deps C002,C003,C005 / O003 deps C001,O001,O002 / O004 deps O003 / O005 deps O004
--   Zoho/WABA zinciri:  C004 deps C003 / C005 deps C002 / O002 deps C005
--   Training zinciri:   O006-O008 deps O005 / O008 deps O006,O007,X001
--   Dev bagliliklar:    D006 deps D005 / D007 deps C003,C004
--   Backlog:            D010 deps O005 (Stage 3 onaylani sonrasi welcome overhaul)
--
-- Idempotency: ADD COLUMN IF NOT EXISTS, UPDATE WHERE depends_on IS NULL guard.
-- Postcondition: regex CHECK constraint enforce.

-- §1. depends_on kolonu (idempotent ADD).
ALTER TABLE kanban_cards
    ADD COLUMN IF NOT EXISTS depends_on TEXT NULL;

-- §2. CHECK constraint: NULL VEYA CSV ref_code listesi.
DO $check_deps$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_kanban_cards_depends_on') THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT chk_kanban_cards_depends_on
            CHECK (depends_on IS NULL OR depends_on ~ '^[A-Z][0-9]{3}(,[A-Z][0-9]{3})*$');
    END IF;
END$check_deps$;

-- §3. UPDATE bagimliliklar (WHERE depends_on IS NULL — re-run idempotent).

-- Stage 1 zinciri (Customer → Ops → Smoke)
UPDATE kanban_cards SET depends_on = 'C003'                  WHERE board_key = 'dent-pilot' AND ref_code = 'C004' AND depends_on IS NULL; -- zoho-oauth → zoho-blueprint
UPDATE kanban_cards SET depends_on = 'C002'                  WHERE board_key = 'dent-pilot' AND ref_code = 'C005' AND depends_on IS NULL; -- waba-number → meta-app
UPDATE kanban_cards SET depends_on = 'C002,C003,C005'        WHERE board_key = 'dent-pilot' AND ref_code = 'O001' AND depends_on IS NULL; -- dashboard-config → meta+zoho+waba
UPDATE kanban_cards SET depends_on = 'C005'                  WHERE board_key = 'dent-pilot' AND ref_code = 'O002' AND depends_on IS NULL; -- inma-allowlist → waba
UPDATE kanban_cards SET depends_on = 'C001,O001,O002'        WHERE board_key = 'dent-pilot' AND ref_code = 'O003' AND depends_on IS NULL; -- stage1-smoke → HSM+config+INMA
UPDATE kanban_cards SET depends_on = 'O003'                  WHERE board_key = 'dent-pilot' AND ref_code = 'O004' AND depends_on IS NULL; -- day2-edge → day1
UPDATE kanban_cards SET depends_on = 'O004'                  WHERE board_key = 'dent-pilot' AND ref_code = 'O005' AND depends_on IS NULL; -- go-no-go → day2

-- Training zinciri (post Go/No-Go)
UPDATE kanban_cards SET depends_on = 'O005'                  WHERE board_key = 'dent-pilot' AND ref_code = 'O006' AND depends_on IS NULL; -- coord screencast
UPDATE kanban_cards SET depends_on = 'O005'                  WHERE board_key = 'dent-pilot' AND ref_code = 'O007' AND depends_on IS NULL; -- ai supervise screencast
UPDATE kanban_cards SET depends_on = 'O006,O007,X001'        WHERE board_key = 'dent-pilot' AND ref_code = 'O008' AND depends_on IS NULL; -- kickoff call → screencasts + cheatsheet

-- Dev bagliliklari
UPDATE kanban_cards SET depends_on = 'D005'                  WHERE board_key = 'dent-pilot' AND ref_code = 'D006' AND depends_on IS NULL; -- lead-detail-timeline → lead-list
UPDATE kanban_cards SET depends_on = 'C003,C004'             WHERE board_key = 'dent-pilot' AND ref_code = 'D007' AND depends_on IS NULL; -- appointments-zoho-sync → zoho

-- Post-pilot
UPDATE kanban_cards SET depends_on = 'O005'                  WHERE board_key = 'dent-pilot' AND ref_code = 'D010' AND depends_on IS NULL; -- b-c2-welcome-overhaul → Stage 1 PASS

-- §4. Postcondition (INV-SEED-027).
DO $verify_deps$
DECLARE
    deps_count integer;
    invalid_count integer;
BEGIN
    SELECT COUNT(*) INTO deps_count
      FROM kanban_cards
     WHERE board_key = 'dent-pilot'
       AND depends_on IS NOT NULL;

    SELECT COUNT(*) INTO invalid_count
      FROM kanban_cards
     WHERE board_key = 'dent-pilot'
       AND depends_on IS NOT NULL
       AND depends_on !~ '^[A-Z][0-9]{3}(,[A-Z][0-9]{3})*$';

    IF invalid_count > 0 THEN
        RAISE EXCEPTION '[INV-SEED-027] kanban_cards depends_on regex bozuk: % satir gecersiz format.', invalid_count;
    END IF;

    RAISE NOTICE 'Migration 037 OK: % dent-pilot kart depends_on dolu (regex valid).', deps_count;
END$verify_deps$;
