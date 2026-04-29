-- Migration 045: D005 Lead Listesi Sayfasi -> INMA scope karari
--
-- Q karari 2026-04-29: Lead listesi + filtreler INMA tarafinda yonetilecek.
-- Invekto-side bu sayfa YAPILMAYACAK. Lead detay sayfasi ayri kart D006
-- (lead-detail-timeline) — Invekto-side detay + timeline kalir.
--
-- Status: TODO -> DONE (Invekto-side scope kapandi)
-- Body: KARAR satiri prepend (mevcut body korunur)
--
-- Idempotency: UPDATE WHERE status<>'DONE' guard re-run no-op.

UPDATE kanban_cards
   SET status = 'DONE',
       completed_at = NOW(),
       updated_at = NOW(),
       body_markdown = E'**KARAR (Q 2026-04-29):** Lead listesi + filtreler INMA tarafinda yonetilecek. Invekto-side bu sayfa YAPILMAYACAK. Lead detay sayfasi (timeline + add note) ayri kart D006 (lead-detail-timeline) — Invekto-side detay scope kalir.\n\n---\n\n' || COALESCE(body_markdown, '')
 WHERE board_key = 'inse'
   AND ref_code = 'D005'
   AND status <> 'DONE';

-- §2. Postcondition guard
DO $verify$
DECLARE
    pending_done integer;
BEGIN
    SELECT COUNT(*) INTO pending_done
      FROM kanban_cards
     WHERE board_key='inse' AND ref_code='D005' AND status <> 'DONE';

    IF pending_done > 0 THEN
        RAISE EXCEPTION '[INV-SEED-039] Migration 045: D005 DONE update eksik, hala % kart non-DONE.', pending_done;
    END IF;

    RAISE NOTICE 'Migration 045 OK: D005 -> DONE (INMA scope karari).';
END$verify$;
