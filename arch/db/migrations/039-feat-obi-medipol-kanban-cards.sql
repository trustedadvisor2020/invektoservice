-- Migration 039: FEAT-OBI + FEAT-MEDIPOL-WA kanban kart kayitlari
--                + board_key 'dent-pilot' -> 'inse' rename
--
-- Q karari 2026-04-29: TEK board kalacak, ismi pilot'a ait DEGIL, icinde
-- pilot kart'lari da olacak. Mevcut 'dent-pilot' board_key teknik isim
-- olarak yaniltiyor (UI'da zaten "Yol Haritasi" — Migration 036 rename).
-- Karar: board_key = 'inse' (INSE platform genel roadmap), tum mevcut
-- kart'lar yeni isim altinda kalir, yeni 2 feature kart'i da ayni board'a
-- dusER. Yeni board ACILMAYACAK — tek board, ismi degisecek.
--
-- D027 = FEAT-OBI Bulk WhatsApp External (INMA + Zoho) — BACKLOG (post-pilot)
-- D028 = FEAT-MEDIPOL-WA Faz 1 — TODO (assumption-driven, paralel musteri isi)
--
-- Frontend etkisi:
--   * src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx:20
--     DEFAULT_BOARD_KEY 'dent-pilot' -> 'inse' (ayni commit)
--   * src/Invekto.Backend/Dashboard/src/App.tsx:166
--     /pilot-kanban redirect /yol-haritasi/dent-pilot -> /yol-haritasi/inse
--   * Layout nav '/yol-haritasi' default route degismiyor (URL boardKey ops.)
--
-- Idempotency:
--   * §1 UPDATE WHERE board_key='dent-pilot' — re-run sonrasi 0 row match (no-op)
--   * §2 INSERT ON CONFLICT DO NOTHING
--   * §3 postcondition: dent-pilot=0, inse>=66 (64 mevcut + 2 yeni)
--
-- Migration history: 037/038 zaten WHERE board_key='dent-pilot' UPDATE'leri
-- iceriyor. Yeni env deploy sirasi 037->038->039 numerik, 037-038 dent-pilot
-- iken UPDATE basarir, 039 sonrasinda rename. Frozen migration history bozulmaz.

-- §1. Mevcut board rename: dent-pilot -> inse
UPDATE kanban_cards
   SET board_key  = 'inse',
       updated_at = NOW()
 WHERE board_key = 'dent-pilot';

-- §2. Yeni 2 kart INSERT (D027 + D028, INSE board genel feature backlog)
INSERT INTO kanban_cards (
    board_key, card_slug, status, category, priority, position,
    title, summary, body_markdown,
    owner, eta, source_file, source_anchor,
    ref_code, depends_on
) VALUES
(
    'inse', 'feat-obi-bulk-external', 'BACKLOG', 'DEV', 'P2', 220,
    'FEAT-OBI Bulk WhatsApp External (INMA + Zoho)',
    'Outbound /broadcast/send DEPLOYED. Eksik: external source ingestion (INMA contact pull + Zoho COQL query). Q istegi 2026-04-29.',
    E'**Mevcut altyapi:** Outbound (:7107) `/api/v1/broadcast/send` — template_id + recipients[] (max 1000) + opt-out filter + status tracking + trigger engine + rate limit.\n\n**Eksik:** Recipients listesini disaridan otomatik cekme. Q istegi: INMA + Zoho oncelikli.\n\n**Faz 1 (MVP):**\n- INMA `/api/contacts` adapter (filter: tag/segment/custom field)\n- Zoho COQL query adapter (Leads, Contacts modules)\n- Bulk Send Orchestrator (idempotency hash 24h)\n- Dashboard `/outbound/bulk-send` page (source picker + segment builder + preview + schedule)\n\n**Faz 2:** CSV upload + saved segments + recurring Hangfire.\n\n**Aktivasyon gate:** Pilot Stage 3 sonrasi, 2. musteri onboarding pre-req.\n\n**Tracking:** tracking/feat-obi-bulk-external.md',
    'Dev', '~10-15 gun (Faz 1)',
    'tracking/feat-obi-bulk-external.md', NULL,
    'D027', NULL
),
(
    'inse', 'feat-medipol-wa-faz1', 'TODO', 'DEV', 'P1', 230,
    'FEAT-MEDIPOL-WA Faz 1: WA Yonlendirme + KVKK',
    'Medipol Saglik Grubu (1.500 doktor). Doktorsitesi.com modeli: WA ikon -> KVKK modal -> consent log -> wa.me redirect. Q onayi 2026-04-29: assumption-driven baslangic.',
    E'**Musteri:** Medipol Saglik Grubu (B2C, hastane grubu, 1.500 doktor)\n**Referans:** Doktorsitesi.com akisi (https://www.doktorsitesi.com/...)\n\n**Faz 1 scope:**\n- Migration 040: branches + doctors ALTER (title/slug/specialty/branch_id/whatsapp_number) + whatsapp_consents + consent_text_versions\n- Backend MedipolEndpoints.cs: 4 endpoint (doctor detail, branch list, consent submit, consent text get)\n- Demo HTML page (Medipol entegrasyon referansi)\n- E.164 phone validation, 5dk/3 rate limit, IP+UA log\n\n**Assumption''lar (Q onayli, musteri override edebilir):**\n- Sube bazli ortak numara (1.500 numara degil) ✅\n- KVKK metni placeholder, hukuk birimi sonra ✅\n- Backend altinda multi-tenant feature ✅\n- Q manuel seed (1-2 branch + 5-10 doctor) ✅\n\n**Faz 2-3:** Slot goruntuleme (CRM read-only) + slot booking (CRM read+write) — musteri CRM API doc bekliyor.\n\n**Musteri yaniti:** mail tekrar atildi 2026-04-29, gelecek cevapla guncellenecek.\n\n**Tracking:** tracking/feat-medipol-wa.md\n**Plan:** arch/plans/20260429-feat-medipol-wa-faz1.json (pending)',
    'Dev + Q', '2-3 hafta (Faz 1)',
    'tracking/feat-medipol-wa.md', NULL,
    'D028', NULL
)
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §3. Postcondition guard.
DO $verify$
DECLARE
    old_board_count integer;
    new_board_count integer;
BEGIN
    SELECT COUNT(*) INTO old_board_count
      FROM kanban_cards WHERE board_key = 'dent-pilot';

    SELECT COUNT(*) INTO new_board_count
      FROM kanban_cards WHERE board_key = 'inse';

    IF old_board_count <> 0 THEN
        RAISE EXCEPTION '[INV-SEED-027] Migration 039 rename eksik: dent-pilot board hala % kart icerir, 0 bekleniyordu.', old_board_count;
    END IF;

    IF new_board_count < 66 THEN
        RAISE EXCEPTION '[INV-SEED-028] Migration 039 INSERT eksik: inse board % kart, en az 66 bekleniyordu (64 rename + 2 yeni).', new_board_count;
    END IF;

    RAISE NOTICE 'Migration 039 OK: dent-pilot=0, inse=% (rename + D027 + D028).', new_board_count;
END$verify$;
