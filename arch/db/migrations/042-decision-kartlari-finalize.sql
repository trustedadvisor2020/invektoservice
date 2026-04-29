-- Migration 042: K1-K9 decision kartlari finalize
--
-- Q kararlari 2026-04-29 (post-wrap follow-up):
--   K1 (pilot baslama tarihi)        -> SIL (onemsiz)
--   K2 (Gunes karakteri)             -> SIL (Gunes onemsiz)
--   K3 (acilis dilleri)              -> DONE: EN + TR (b)
--   K4 (fiyat sorusuna cevap)        -> DONE: Gorusmede konusalim (b - kisiye ozel)
--   K5 (reklam yonetimi)             -> DONE: Ortak yonetim (c - Musteri + Invekto)
--   K6 (mesai disi politika)         -> DONE: 7/24 robot tam cevap (a)
--   K7 (koordinator kim)             -> DOKUNULMUYOR (Q konusmadi, BLOCKED kalir)
--   K8 (Dr. Ozge gorusme kapasitesi) -> DONE: Gunde 4 gorusme
--   K9 (ilk sehirler)                -> SIL (sehir sorulmayacak — decision moot)
--
-- Idempotency:
--   * §1 DELETE WHERE ref_code IN (...) — re-run sonrasi 0 row match (no-op)
--   * §2 UPDATE WHERE status<>'DONE' guard — re-run sonrasi WHERE match etmez (no-op)
--   * §3 postcondition: deleted_remaining=0, pending_done=0 (PASS sonsuza)
--
-- Body update strategy: mevcut body_markdown'in basina KARAR satiri prepend.
-- WHERE status<>'DONE' guard double-prepend engellenir.
--
-- Error codes:
--   INV-SEED-035 = K001/K002/K009 silme postcondition fail (3 kart > 0 demektir)
--   INV-SEED-036 = K003-K006/K008 DONE update postcondition fail (5 kart hala BLOCKED)
--
-- Note: errors.md tanimi ayri follow-up commit'te (Q clinic-metadata branch
-- uncommitted oldugu icin selective wrap saygisi — INV-SEED-035/036 tanimi
-- Q'nun clinic-metadata wrap sonrasi eklenir).

-- §1. K001 + K002 + K009 SIL (irrelevant decisions)
DELETE FROM kanban_cards
 WHERE board_key = 'inse'
   AND ref_code IN ('K001', 'K002', 'K009');

-- §2. K003 + K004 + K005 + K006 + K008 -> DONE + KARAR markaj
UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** EN + TR (secenek b)\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse' AND ref_code = 'K003' AND status <> 'DONE';

UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** Gorusmede konusalim (secenek b — kisiye ozel)\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse' AND ref_code = 'K004' AND status <> 'DONE';

UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** Ortak yonetim (secenek c — Musteri + Invekto)\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse' AND ref_code = 'K005' AND status <> 'DONE';

UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** 7/24 robot tam cevap (secenek a)\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse' AND ref_code = 'K006' AND status <> 'DONE';

UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** Gunde 4 gorusme\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse' AND ref_code = 'K008' AND status <> 'DONE';

-- §3. Postcondition guard
DO $verify$
DECLARE
    deleted_remaining integer;
    pending_done integer;
BEGIN
    SELECT COUNT(*) INTO deleted_remaining
      FROM kanban_cards
     WHERE board_key='inse'
       AND ref_code IN ('K001','K002','K009');

    SELECT COUNT(*) INTO pending_done
      FROM kanban_cards
     WHERE board_key='inse'
       AND ref_code IN ('K003','K004','K005','K006','K008')
       AND status <> 'DONE';

    IF deleted_remaining > 0 THEN
        RAISE EXCEPTION '[INV-SEED-035] Migration 042: K001/K002/K009 silme eksik, % kart hala mevcut.', deleted_remaining;
    END IF;

    IF pending_done > 0 THEN
        RAISE EXCEPTION '[INV-SEED-036] Migration 042: K003-K006/K008 DONE update eksik, % kart hala BLOCKED.', pending_done;
    END IF;

    RAISE NOTICE 'Migration 042 OK: 3 kart silindi (K001/K002/K009), 5 kart DONE (K003-K006 + K008).';
END$verify$;
