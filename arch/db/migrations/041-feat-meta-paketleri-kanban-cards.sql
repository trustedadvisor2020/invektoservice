-- Migration 041: FEAT-META-CAPI + FEAT-META-ADS-INSIGHTS + FEAT-META-MARKETING-API
--                kanban kart kayitlari (3 yeni DEV kart, INSE board)
--
-- Q karari 2026-04-29 (cevap=B): 3 Meta paketi DB-backed kanban'a (kanban_cards
-- tablosu) ekle, audit pattern (Migration 038/039) izle: idempotent INSERT
-- + DO $verify$ postcondition + Codex review.
--
-- D029 = FEAT-META-CAPI Conversions API (server-side event tracking)
--        Status: TODO. Q Pixel/Token provision (BM Events Manager + System User
--        Token `ads_management`+`business_management`) sonrasi chunk A baslar.
--        Spec: arch/features/meta-conversions-api.md (Q kararlari §0 yansidi)
--        ROI: $10k/ay reklam spend uzerinde %10-15 efficiency = $1-1.5k/ay tasarruf.
--
-- D030 = FEAT-META-ADS-INSIGHTS Reporting Widget (read-only)
--        Status: BACKLOG. CAPI bundle bagimliligi (ayni BM/Pixel/Token, ayni
--        App Review submission `ads_management`+`ads_read` cift permission).
--        D029 chunk E pilot smoke sonrasi BACKLOG -> TODO flip.
--        Spec: arch/features/meta-ads-insights.md
--
-- D031 = FEAT-META-MARKETING-API Campaign Mgmt
--        Status: BACKLOG. $10k/ay toplam spend gate ile App Review barı gecmez
--        (`ads_management_standard_access` "anlamli volume" + tutarli use-case
--        ister). Activation gate: spend tabani $50k+/ay veya tier-degistirici
--        musteri talebi. D029 + D030 production track record on-kosul.
--
-- Idempotency:
--   * §1 INSERT ON CONFLICT (board_key, card_slug) DO NOTHING
--     (Migration 035 unique constraint pattern)
--   * §2 postcondition: 3 yeni ref_code (D029/D030/D031) DB'de varlik check
--
-- Migration history: 039 (FEAT-OBI + FEAT-MEDIPOL D027/D028) sonrasi yeni
-- D-kategori kartlari. Kategori prefix: DEV (Yazilim). Position 240-260
-- (Migration 039 220/230 sonrasi 10'luk artis pattern).
--
-- depends_on regex (INV-SEED-027 invariant): '^[A-Z][0-9]{3}(,[A-Z][0-9]{3})*$'
--   * D029 -> NULL (top of chain)
--   * D030 -> 'D029' (CAPI bundle dep)
--   * D031 -> 'D029,D030' (track record on-kosul)
--
-- Frontend etkisi: YOK. Mevcut PilotKanbanPage 'inse' board zaten render ediyor;
-- yeni 3 kart otomatik gozukur (DEV kategori filter aktifse).

-- §1. 3 yeni kart INSERT (D029 + D030 + D031, INSE board DEV kategori).
INSERT INTO kanban_cards (
    board_key, card_slug, status, category, priority, position,
    title, summary, body_markdown,
    owner, eta, source_file, source_anchor,
    ref_code, depends_on
) VALUES
(
    'inse', 'feat-meta-capi', 'TODO', 'DEV', 'P1', 240,
    'FEAT-META-CAPI Conversions API (server-side)',
    'Reklam veren musteriler ($10k/ay spend) icin server-side donusum bildirimi. Lead/Schedule/Purchase/CompleteRegistration eventleri Marketing servisi (:7112) Hangfire `meta-capi-dispatch` queue ile Meta''ya. EMQ icin hashed PII + FBC/FBP cookie + IP/UA. Browser Pixel ile event_id deduplication. INMA bypass.',
    E'**ROI:** $10k/ay reklam spend uzerinde %10-15 efficiency = $1-1.5k/ay tasarruf.\n\n**5 chunk (~4-6 session):**\n- A: Shared DTO + IMetaCapiClient + MockClient\n- B: ProdClient + Hangfire queue + dead-letter + retry\n- C: Migration + Backend tenant-settings + 3 hook (Lead/Schedule/Purchase) + audit table\n- D: Dashboard `/settings/meta-capi` editor + test event button + Pixel event_id emit util\n- E: Pilot smoke (Dent Adavista) + production rollout\n\n**Q kararlari (2026-04-29):**\n- Multi-tenant generic kod, **Dent ilk pilot** (Chunk E)\n- Test pixel: prod + `test_event_code` (Q teyit bekliyor, Claude oneri)\n- Token expiry warning kanali = **Dashboard alert** (Hangfire daily check)\n- Schedule hook = **ikisi de** (Appointments + Lead pipeline `appointment_booked`, deterministic event_id ile dedup)\n- consent=false -> **hard reject** (Marketing dispatcher gate, KVKK/GDPR)\n\n**Bagimliliklar (ON-KOSUL):**\n1. Q manuel: BM Pixel/Dataset olustur (Events Manager)\n2. Q manuel: System User Token gen (`ads_management`+`business_management`+`ads_read` cift permission, D030 ile bundle)\n3. App Review submission (Pixel verify hafif yol)\n4. FEAT-META-FULL-INTAKE consent_marketing capture ✅ DONE 2026-04-29\n\n**Spec:** arch/features/meta-conversions-api.md\n**Tracking:** tracking/feat-meta-capi.md\n**Plan:** TBD (chunk A baslayinca)',
    'Dev', '~4-6 session (chunk A start = Q provision sonrasi)',
    'tracking/feat-meta-capi.md', NULL,
    'D029', NULL
),
(
    'inse', 'feat-meta-ads-insights', 'BACKLOG', 'DEV', 'P2', 250,
    'FEAT-META-ADS-INSIGHTS Reporting Widget (read-only)',
    'Marketing API `ads_read` permission ile reklam performans Dashboard widget (spend/impressions/clicks/CTR/CPM/CPC + lead matching). Async report API + 1 saat cache + Hangfire daily refresh. Dashboard `/reports/ads-insights` widget + date picker + campaign breakdown.',
    E'**Bundle onerisi:** D029 FEAT-META-CAPI ile **tek App Review submission** (`ads_management`+`ads_read` cift permission, tek video, hizli onay). Ayni BM/Pixel/Token kullanir.\n\n**5 chunk (~3-4 session):**\n- A: Shared DTO + IMetaAdsInsightsClient + MockClient\n- B: ProdClient (async report run_id+poll) + Hangfire daily refresh\n- C: Migration + Backend tenant-settings + reports proxy + lead matching SQL JOIN\n- D: Dashboard `/reports/ads-insights` page + widget + date picker\n- E: App Review submission + smoke + first tenant rollout\n\n**Lead matching:** FEAT-META-FULL-INTAKE `intake_metadata.campaign_id` ile JOIN -> "23 leads from FB Ads, 5 converted, $342 spend".\n\n**Status BACKLOG sebebi:** D029 chunk E pilot smoke sonrasi BACKLOG -> TODO flip; bundle integrity (ayni token surface) icin sequential ship.\n\n**Spec:** arch/features/meta-ads-insights.md\n**Tracking:** tracking/feat-meta-ads-insights.md',
    'Dev', '~3-4 session (D029 sonrasi)',
    'tracking/feat-meta-ads-insights.md', NULL,
    'D030', 'D029'
),
(
    'inse', 'feat-meta-marketing-api', 'BACKLOG', 'DEV', 'P3', 260,
    'FEAT-META-MARKETING-API Campaign Create/Manage',
    'Reklam kampanya olusturma/butce yonetimi/raporlama Invekto Dashboard icinden. **GATE:** `ads_management_standard_access` Meta App Review barı $10k/ay toplam spend ile gecmez (anlamli volume + tutarli use-case ister).',
    E'**Activation gate:** Spend tabani $50k+/ay veya tier-degistirici musteri talebi.\n\n**On-kosul:** D029 + D030 production track record gosterilebilir hale gelmeli (Meta App Review submission''da history evidence).\n\n**Spec yazimi:** Backlog. Activation gate triggered olunca:\n- arch/features/meta-marketing-api.md spec yaz\n- tracking/feat-meta-marketing-api.md tracking ac\n- Chunk breakdown (campaign create + ad set + creative + insights write + lifecycle)\n- App Review video + use-case dokumantasyon hazirlik\n\n**Su andaki konum:** roadmap.md Backlog Idea (IDEA status), README.md master tablosu BACKLOG, kanban''da bu kart placeholder.',
    'Dev', 'Aktive sonrasi 8-12 session (uzak)',
    NULL, NULL,
    'D031', 'D029,D030'
)
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §2. Postcondition guard — count + per-row content drift detection.
--
-- Codex iter 0 CQ2/Q1 feedback: ON CONFLICT DO NOTHING ile pre-existing
-- D029/D030/D031 ref_code rows (yanlis status/position/depends_on/card_slug
-- ile) sessiz skip + count=3 PASS yaratir. Iter 1 fix: per-row drift detect.
DO $verify$
DECLARE
    expected_refs       text[] := ARRAY['D029','D030','D031'];
    found_refs          integer;
    new_card_count      integer;
    drift_count         integer;
BEGIN
    -- §2.1. Yeni 3 ref_code DB'de mi (INSERT ya da pre-existing on conflict)?
    SELECT COUNT(*) INTO found_refs
      FROM kanban_cards
     WHERE board_key = 'inse'
       AND ref_code = ANY(expected_refs);

    IF found_refs <> 3 THEN
        RAISE EXCEPTION '[INV-SEED-029] Migration 041 INSERT eksik: inse board D029/D030/D031 icin % kart bulundu, 3 bekleniyordu. ON CONFLICT DO NOTHING ile sessiz skip riski (mevcut card_slug catismasi) — manuel inceleme: SELECT ref_code, card_slug FROM kanban_cards WHERE ref_code IN (''D029'',''D030'',''D031'').', found_refs;
    END IF;

    -- §2.2. Per-row content drift check (CQ2 fix): D029/D030/D031 her biri
    -- beklenen card_slug + status + position + category + priority + depends_on
    -- kombinasyonuyla mi DB'de? Pre-existing stale row varsa fail-loud.
    SELECT COUNT(*) INTO drift_count
      FROM kanban_cards
     WHERE board_key = 'inse'
       AND ref_code  = ANY(expected_refs)
       AND NOT (
              (ref_code = 'D029'
                AND card_slug   = 'feat-meta-capi'
                AND status      = 'TODO'
                AND category    = 'DEV'
                AND priority    = 'P1'
                AND position    = 240
                AND depends_on IS NULL)
           OR (ref_code = 'D030'
                AND card_slug   = 'feat-meta-ads-insights'
                AND status      = 'BACKLOG'
                AND category    = 'DEV'
                AND priority    = 'P2'
                AND position    = 250
                AND depends_on  = 'D029')
           OR (ref_code = 'D031'
                AND card_slug   = 'feat-meta-marketing-api'
                AND status      = 'BACKLOG'
                AND category    = 'DEV'
                AND priority    = 'P3'
                AND position    = 260
                AND depends_on  = 'D029,D030')
       );

    IF drift_count > 0 THEN
        RAISE EXCEPTION '[INV-SEED-029] Migration 041 content drift: D029/D030/D031 satirlarindan % tanesi beklenen card_slug/status/position/category/priority/depends_on kombinasyonuyla uyumlu degil. Pre-existing stale row tespit edildi (ON CONFLICT DO NOTHING update yapmadi). Tani: SELECT ref_code, card_slug, status, category, priority, position, depends_on FROM kanban_cards WHERE board_key=''inse'' AND ref_code IN (''D029'',''D030'',''D031''). Fix-forward: stale row DELETE + migration re-run, ya da manuel UPDATE ile alignement sagla.', drift_count;
    END IF;

    -- §2.3. Toplam kart sayisi NOTICE log (info).
    SELECT COUNT(*) INTO new_card_count
      FROM kanban_cards WHERE board_key = 'inse';

    RAISE NOTICE 'Migration 041 OK: inse board D029=ok, D030=ok, D031=ok (count + content drift checks PASS); toplam % kart.', new_card_count;
END$verify$;
