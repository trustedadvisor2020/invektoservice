-- Migration 035: FEAT-PILOT-KANBAN — SuperAdmin pilot tracking board
--
-- Context:
-- Q'nun "tum gidisat" izleme istegi. SuperAdmin Dashboard'a 5 kolonlu
-- (BLOCKED/TODO/IN_PROGRESS/BACKLOG/DONE) read-only kanban sayfasi.
-- Kart click -> sagdan slide-in drawer (tum detay inline). Read-only:
-- Q manuel edit yapmaz, /wrap workflow Step 3.5 olarak otomatik kart
-- status update onerir, Q onaylar -> API PATCH gonderir.
--
-- Scope (Q kararlari 2026-04-28):
--   * 1a Platform-level: board_key='dent-pilot', tenant_id NULL — gelecek
--     pilotlar icin board_key='tenant-X-pilot' eklenebilir.
--   * 2a PostgreSQL tablo (FK YOK — board_key string-keyed).
--   * 3c /wrap hibrit (otomatik oner + Q onayla -> API PATCH).
--   * History/audit YOK — sadece updated_at + completed_at (Q HAYIR dedi).
--   * Drag-drop YOK (read-only Dashboard).
--
-- Bu migration:
--   * CREATE TABLE kanban_cards — board_key + card_slug composite UNIQUE
--     index. status/category/priority CHECK constraint'ler ile enum
--     enforce edilir (PostgreSQL ENUM type kullanmiyoruz — Migration 020+
--     pattern: VARCHAR + CHECK, Codex inline review icin daha kolay).
--   * 2 index: board_key+status+position (kanban render query'si),
--     board_key+updated_at DESC (recent activity sort).
--   * Seed ~50 kart Dent pilot icin (board_key='dent-pilot') —
--     ON CONFLICT (board_key, card_slug) DO NOTHING ile re-run idempotent.
--     Mevcut row'larin status/title'i kirilmaz; yalnizca yeni slug'lar
--     insert edilir. Re-seed istenirse manual UPDATE veya ayri seed script.
--   * GRANT ALL ON kanban_cards, kanban_cards_id_seq TO invekto.
--   * Postcondition DO $verify$ block — tablo var, en az 40 dent-pilot
--     kart var, tum CHECK constraint'ler aktif. RAISE EXCEPTION ile
--     fail-loud (031/033/034 pattern).
--
-- Status enum (5 deger):
--   BLOCKED      — Musteri/3rd-party/Karar bekliyor (kritik yol)
--   TODO         — Yapilacak (Invekto Ops + Dev)
--   IN_PROGRESS  — Aktif iste
--   BACKLOG      — Post-pilot, siraya alinmis
--   DONE         — Tamamlanmis
--
-- Category enum (6 deger):
--   CUSTOMER     — Musteri/3rd-party (Meta, Zoho, WABA)
--   OPS          — Invekto operasyon (config, smoke, monitoring)
--   DEV          — Yazilim gelistirme
--   DECISION     — Karar bekleyen (K1-K10, kapsam soruları)
--   UI           — UI ekrani eksik (#10-#18)
--   DOC          — Dokuman/tracking
--
-- Priority enum (4 deger):
--   P0 — Pilot blocker (acil)
--   P1 — Stage 2-3 yetistirilebilir
--   P2 — Standart (varsayilan)
--   P3 — Nice-to-have
--
-- Related:
--   * Plan       : arch/plans/20260428-feat-pilot-kanban-superadmin.json
--   * Tracking   : tracking/20260428-feat-pilot-kanban-superadmin.md
--   * Dent ref   : DentAdavista/plan/dent-golive.html (kart icerik kaynagi)
--   * Error code : INV-SEED-024..025 (postcondition guard)
--
-- Idempotency: ADD/CREATE IF NOT EXISTS, ON CONFLICT DO NOTHING.

-- §1. Tablo
-- tenant_id NULL = platform-level board (board_key='dent-pilot' SuperAdmin tek bakis,
-- Q karari 1a). Gelecek tenant-scoped board'lar (board_key='tenant-X-pilot') icin
-- tenant_id BIGINT dolu olur, runtime queries tenant_id WHERE clause ile scope'lar.
-- FK YOK — board_key string-keyed; tenant_id sadece izolasyon flag'i (cross-tenant
-- query guard /api/ops layer'inda + repository tarafinda uygulanir).
CREATE TABLE IF NOT EXISTS kanban_cards (
    id              BIGSERIAL PRIMARY KEY,
    board_key       VARCHAR(64)  NOT NULL,
    card_slug       VARCHAR(128) NOT NULL,
    tenant_id       BIGINT       NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'TODO',
    category        VARCHAR(20)  NOT NULL DEFAULT 'OPS',
    priority        VARCHAR(8)   NOT NULL DEFAULT 'P2',
    position        INTEGER      NOT NULL DEFAULT 100,
    title           VARCHAR(255) NOT NULL,
    summary         TEXT,
    body_markdown   TEXT,
    owner           VARCHAR(64),
    eta             VARCHAR(32),
    source_file     VARCHAR(255),
    source_anchor   VARCHAR(64),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ
);

-- Mevcut deploy'da tablo zaten var ise (idempotent re-run) tenant_id kolonu
-- eksikse ekle — DEFAULT NULL ile mevcut row'lar etkilenmez (platform-level
-- dent-pilot kart'lari NULL kalir, semantik dogru).
ALTER TABLE kanban_cards
    ADD COLUMN IF NOT EXISTS tenant_id BIGINT NULL;

-- §2. Composite UNIQUE (board_key + card_slug) — /wrap PATCH lookup anchor.
DO $unique$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'uq_kanban_cards_board_slug'
    ) THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT uq_kanban_cards_board_slug UNIQUE (board_key, card_slug);
    END IF;
END$unique$;

-- §3. CHECK constraint'ler (status, category, priority enum enforcement).
DO $checks$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'chk_kanban_cards_status'
    ) THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT chk_kanban_cards_status
            CHECK (status IN ('BLOCKED','TODO','IN_PROGRESS','BACKLOG','DONE'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'chk_kanban_cards_category'
    ) THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT chk_kanban_cards_category
            CHECK (category IN ('CUSTOMER','OPS','DEV','DECISION','UI','DOC'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'chk_kanban_cards_priority'
    ) THEN
        ALTER TABLE kanban_cards
            ADD CONSTRAINT chk_kanban_cards_priority
            CHECK (priority IN ('P0','P1','P2','P3'));
    END IF;
END$checks$;

-- §4. Index'ler.
CREATE INDEX IF NOT EXISTS idx_kanban_cards_board_status
    ON kanban_cards(board_key, status, position);

CREATE INDEX IF NOT EXISTS idx_kanban_cards_board_updated
    ON kanban_cards(board_key, updated_at DESC);

-- tenant_id partial index — tenant-scoped board'lar (board_key='tenant-X-...')
-- icin runtime query'ler WHERE tenant_id = $1 ile scope'lar; platform-level
-- dent-pilot kart'lari (tenant_id IS NULL) bu index'e girmez (partial WHERE).
CREATE INDEX IF NOT EXISTS idx_kanban_cards_tenant
    ON kanban_cards(tenant_id, board_key)
    WHERE tenant_id IS NOT NULL;

-- §5. GRANT (lesson 2026-04-18: invekto role, NOT invekto_app).
GRANT ALL ON kanban_cards TO invekto;
GRANT USAGE, SELECT ON SEQUENCE kanban_cards_id_seq TO invekto;

-- §6. Seed: Dent pilot kanban (board_key='dent-pilot').
-- ON CONFLICT (board_key, card_slug) DO NOTHING — re-run safe.
-- position degerleri 10/20/30... — yeni kart araya sokulurken 15/25 ile
-- yeniden numarlamadan eklenebilir.

-- §6.1 BLOCKED kolonu (Musteri / Karar / 3rd-party).
INSERT INTO kanban_cards (board_key, card_slug, status, category, priority, position, title, summary, body_markdown, owner, eta, source_file, source_anchor) VALUES
('dent-pilot','meta-hsm-template','BLOCKED','CUSTOMER','P0',10,
 'B.8 HSM Welcome Template Meta Onayi',
 'Meta`nin karsilama mesajini onaylamasi (24-48h). Pilot baslangicinin ANA ENGELI.',
 E'**Durum:** Meta`ya HSM template submit edildi (utility category), 24-48h onay sureci.\n\n**Kritik yol:** Onay gelmedikce Stage 1 baslayamaz. Reject olursa ayni gun ikinci versiyon submit.\n\n**Mitigation:** Utility category sec (marketing`den hizli onay), body`de CTA dogal dilde, sample value ver.\n\n**Action:** Onay geldiginde -> Stage 1 Gun 1 F.1 smoke baslatilir.',
 'Musteri + Meta','24-48h','DentAdavista/plan/dent-golive.html','#s1-b'),

('dent-pilot','meta-app-setup','BLOCKED','CUSTOMER','P0',20,
 'B.1-B.7 Meta App + Sayfa + Token + Webhook',
 'Meta App olusturma + sayfa baglantisi + Page Access Token + Page ID + Webhook subscription.',
 E'**Adimlar:**\n- B.1: Meta App olustur (developer.facebook.com)\n- B.2-B.3: Sayfa baglantisi + ortak yetkilendir\n- B.4-B.5: Page Access Token + Page ID al\n- B.6-B.7: Webhook subscription (leadgen callback URL)\n\n**Sure:** ~30 dk - 2 saat ekran paylasimiyla.\n\n**Bagimlilik:** B.8 HSM template onayi paralel.',
 'Musteri','30dk-2saat','DentAdavista/plan/dent-golive.html','#s1-b'),

('dent-pilot','zoho-blueprint','BLOCKED','CUSTOMER','P0',30,
 'C.1-C.5 Zoho CRM Blueprint Kurulumu (7 transition)',
 'Zoho`da 7 asamali pipeline + transition kurulumu (45 dk ekran paylasimi).',
 E'**Asamalar (7):**\n1. Yeni Aday\n2. Iletisim Kuruldu\n3. Konusma Basladi\n4. Nitelikli Aday\n5. Teklif Gonderildi\n6. Kapora Alindi\n7. Tedavi Tamamlandi (+ Iptal)\n\n**Sure:** ~45 dk birlikte ekran paylasimiyla.\n\n**Sahibi:** INMA — asama listesi system genelinde tek dogru kaynak. Zoho`ya manual kurulur, Invekto`ya INMA`dan akar.',
 'Musteri + Invekto Ops','45dk','DentAdavista/plan/dent-golive.html','#s1-c'),

('dent-pilot','zoho-oauth','BLOCKED','CUSTOMER','P0',40,
 'C.6-C.7 Zoho OAuth + Stage Mapping',
 'Zoho Invekto OAuth baglantisi + asama eslestirme dogrulamasi.',
 E'**Adimlar:**\n- C.6: Zoho OAuth baglanti (refresh token 30g)\n- C.7: Stage mapping dogrulama (LeadStatusEventMap -> Zoho gunes_event)\n\n**Bagimlilik:** C.1-C.5 sonrasi.',
 'Musteri','15dk','DentAdavista/plan/dent-golive.html','#s1-c'),

('dent-pilot','waba-number','BLOCKED','CUSTOMER','P0',50,
 'D.1-D.2 WABA Numarasi Saglama',
 'WhatsApp Business numarasi + phone_number_id INMA`ya tanimlanir.',
 E'**Adimlar:**\n- D.1: WABA numara onayi (Meta`dan)\n- D.2: phone_number_id elde et + INMA`ya kaydet\n\n**Bagimlilik:** B.1 (Meta App) sonrasi.',
 'Musteri','30dk','DentAdavista/plan/dent-golive.html','#s1-d'),

('dent-pilot','decision-k1-pilot-date','BLOCKED','DECISION','P0',60,
 'K1: Pilot Baslama Tarihi',
 'Pilot ne zaman baslasin? (a) 28 Nisan / (b) 29 Nisan / (c) HSM onayina gore kayar.',
 E'**Secenekler:**\n- (a) 28 Nisan — bugun (HSM onayi gelirse)\n- (b) 29 Nisan — yarin\n- (c) HSM onayina gore kayar (esnek)\n\n**Onerimiz:** (c) — pilot tarihi B.8 onayina bagli.',
 'Q + Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k2-gunes-persona','BLOCKED','DECISION','P1',70,
 'K2: "Gunes" Karakteri Kim?',
 'Gercek bir kisi mi, takim ismi mi, sembol mu? Hasta sorduzunda ne cevap?',
 E'**Secenekler:**\n- (a) Klinikte gercek bir karsilama gorevlisi\n- (b) Takim ismi (sembolik)\n- (c) Tamamen sembol\n\n**Etki:** Mesaj dilini, "yuz yuze gorusmek istiyorum" cevabini, transparency cumlesini sekillendirir.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k3-languages','BLOCKED','DECISION','P1',80,
 'K3: Acilis Dilleri',
 'Sitenizdeki 9 dilden hangisi pilot icinde olsun?',
 E'**Secenekler:**\n- (a) Sadece Ingilizce\n- (b) Ingilizce + Turkce\n- (c) +1 Almanca\n\n**Onerimiz:** (b) — Ingilizce + Turkce. Diger diller sirayla.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k4-pricing','BLOCKED','DECISION','P1',90,
 'K4: Fiyat Sorusuna Cevap',
 'Hasta "fiyat ne kadar" derse ne cevaplayalim?',
 E'**Secenekler:**\n- (a) Sabit aralik ver\n- (b) "Kisiye ozel, gorusmede konusalim"\n- (c) PDF fiyat listesi paylas\n\n**Etki:** AI agent FAQ cevabi.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k5-ad-management','BLOCKED','DECISION','P1',100,
 'K5: Reklam Yonetimi Kimde?',
 'Meta Ads kampanyalarini kim aciyor, butce kim kontrol ediyor?',
 E'**Secenekler:**\n- (a) Sizde — musteri kendi yonetir\n- (b) Bizde — Invekto ekibi\n- (c) Ortak\n\n**Etki:** Stage 3 sonrasi reklam buyumesi sorumlulugu.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k6-after-hours','BLOCKED','DECISION','P1',110,
 'K6: Mesai Disi Politika',
 'Calisma saatleri disinda gelen mesajlara ne yapsin?',
 E'**Secenekler:**\n- (a) Robot tam cevap (24/7)\n- (b) "Yarin 09:00`da donecegiz" mesaji\n- (c) Acil icin telefon ver\n\n**Onerimiz:** (a) AI agent 24/7 cevap, koordinator mesai basinda incele.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k7-coordinator','BLOCKED','DECISION','P0',120,
 'K7: Koordinator Kim?',
 'Sistem anlamadiginda, hasta sinirlendiginde devreye girecek kisi.',
 E'**Bilgi:** Isim + WhatsApp + e-posta\n\n**Pilot icin kritik:** Gun 1`den itibaren handoff mesajlari koordinatore gider.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k8-doctor-capacity','BLOCKED','DECISION','P0',130,
 'K8: Dr. Ozge Gorusme Kapasitesi',
 'Gunluk kac online gorusme? Hangi saat araligi?',
 E'**Bilgi:**\n- Gunluk gorusme sayisi (ornek: 5)\n- Saat araligi (ornek: 10:00-16:00)\n- Gorusme suresi (15dk / 30dk?)\n\n**Etki:** Slot generation, AppointmentsPage takvim grid.',
 'Dr. Ozge','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k9-cities','BLOCKED','DECISION','P1',140,
 'K9: Ilk Sehirler',
 'Pilot hangi sehirleri kapsiyor?',
 E'**Onerimiz:** Dublin + Cork + Roadshow tarihleri kesinlestir.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions'),

('dent-pilot','decision-k10-data-retention','BLOCKED','DECISION','P2',150,
 'K10: Veri Saklama Suresi',
 '30 gun / 6 ay / 1 yil?',
 E'**GDPR:** "Verimi sil" sonrasi 30 gun standart.\n\n**Etki:** lead retention policy + Zoho/INMA cleanup job.',
 'Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#decisions')
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §6.2 TODO kolonu (Invekto Ops + Dev).
INSERT INTO kanban_cards (board_key, card_slug, status, category, priority, position, title, summary, body_markdown, owner, eta, source_file, source_anchor) VALUES
('dent-pilot','dashboard-config-7-pages','TODO','OPS','P0',10,
 'E.1-E.8 Dashboard Konfigurasyon (7 sayfa)',
 'Tenant ayarlari + LIW + VCP + MCC + EFS + TFM + template settings.',
 E'**7 sayfa:**\n- E.1 Tenant ayarlari (locale, timezone, feature flags)\n- E.2 Lead Intake (LIW) — slug, API key, field map\n- E.3 VCP appointments\n- E.4 MCC multi-city\n- E.5 EFS drip\n- E.6 TFM field mapping\n- E.7 Templates\n- E.8 Meta Leadgen settings\n\n**Sure:** ~2-3 saat birlikte ayarlama.',
 'Invekto Ops','2-3 saat','DentAdavista/plan/dent-golive.html','#s1-e'),

('dent-pilot','inma-allowlist','TODO','OPS','P0',20,
 'D.3-D.5 INMA Allowlist + Bridge Test',
 'INMA tarafinda WABA numarasi allowlist + Dashboard bridge test.',
 E'**Adimlar:**\n- D.3: INMA tarafinda WABA numarasi allowlist`a ekle\n- D.4: Test mesaj gonderimi (5 numara)\n- D.5: Dashboard bridge test (INMA -> Invekto webhook)\n\n**Bagimlilik:** D.1-D.2 (WABA numarasi) sonrasi.',
 'Invekto Ops','30dk','DentAdavista/plan/dent-golive.html','#s1-d'),

('dent-pilot','stage1-day1-smoke','TODO','OPS','P0',30,
 'F.1 Stage 1 Gun 1 Happy Path Smoke',
 '5 numara × 6 senaryo happy path test (yarim gun).',
 E'**Test matrisi:** 5 telefon × 6 senaryo = 30 ko-execution.\n\n**Senaryolar:**\n1. Form -> karsilama mesaji\n2. "Dublin" yazdi -> saat listesi\n3. "Bedava mi?" -> AI cevap\n4. Saat secildi -> Meet linki\n5. Cevap vermedi -> hatirlatma akisi\n6. "STOP" -> iletisim kesilir\n\n**Bagimlilik:** B.8 HSM onay + E.1-E.8 config + D.3-D.5 INMA.',
 'Invekto Ops','Yarim gun','DentAdavista/plan/dent-golive.html','#s1-f1'),

('dent-pilot','stage1-day2-edge','TODO','OPS','P0',40,
 'F.2 Stage 1 Gun 2 Edge Case + Monitoring',
 'Edge case testleri + monitoring dogrulama.',
 E'**Edge cases:**\n- Sinir disinda mesaj (anlamadi)\n- Cift kayit (idempotency)\n- Saat dolmus slot\n- Foto formati gecersiz\n- Mesai disi mesaj\n\n**Monitoring:** F.1 sonrasi loglar + alert dogrula.',
 'Invekto Ops','Yarim gun','DentAdavista/plan/dent-golive.html','#s1-f2'),

('dent-pilot','stage1-go-no-go','TODO','OPS','P0',50,
 'F.3 Stage 1 Go/No-Go Kriterleri (Q sign-off)',
 'PASS kriterleri: hepsi `cikmali. FAIL durumunda Stage 2 bekler.',
 E'**PASS kriterleri:**\n- 30 senaryo `cikti\n- 0 critical bug\n- 0 cross-tenant leak\n- Logs temiz (alert yok)\n- Q sign-off\n\n**FAIL durumunda:** Stage 2 baslamiaz, hata sebebi tanimlanir, fix paketi acilir.',
 'Q + Invekto Ops','30dk review','DentAdavista/plan/dent-golive.html','#s1-f3'),

('dent-pilot','monitoring-waa-webhook-alert','TODO','DEV','P1',60,
 '9.1 WAA Webhook Error Alert',
 'Slack/email — rolling 5dk window >%1 fail rate.',
 E'**Alert:** WAA webhook error rate 5dk pencere >%1 -> Slack/email notification.\n\n**Threshold:** /1 fail = 0 alert; %1+ = warning; %5+ = critical.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#monitoring'),

('dent-pilot','monitoring-flow-stuck-alert','TODO','DEV','P1',70,
 '9.2 Flow Stuck Lead Alert',
 '7 gun boyunca ayni flow node`da kalmis lead.',
 E'**Alert:** Bir lead 7 gun boyunca ayni flow node`da kalmissa -> stuck flag + alert.\n\n**Action:** Koordinatore handoff oneri.',
 'Dev','~1 gun','DentAdavista/plan/dent-golive.html','#monitoring'),

('dent-pilot','monitoring-llm-cost-alert','TODO','DEV','P1',80,
 '9.3 LLM Cost Daily Limit',
 'Tenant basi esik + threshold >%80 warning + %100 hard stop.',
 E'**Esik:** Tenant basi gunluk LLM cost limit (config).\n\n**Threshold:**\n- %80: warning Slack\n- %100: hard stop (LLM call`lar reddedilir, fallback message)',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#monitoring'),

('dent-pilot','monitoring-intent-anomaly','TODO','DEV','P2',90,
 '9.4 Intent Confidence Anomaly',
 'Rolling 24h baseline >3σ dusus.',
 E'**Anomaly detection:** Intent confidence rolling 24h ortalamasi 3σ`dan fazla duserse alert.\n\n**Senaryo:** Yeni FAQ varyanti devreye girince confidence dustu vs gercek anomaly?',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#monitoring'),

('dent-pilot','training-coordinator-screencast','TODO','OPS','P1',100,
 '10.1 Koordinator Dashboard Screencast',
 '15 dk video — teklif hazirlama + offer send + manual override.',
 E'**Icerik:**\n- Lead listesi nasil okunur\n- Manuel mesaj gonderim\n- Teklif hazirlama akisi\n- Override (AI`yi devre disi birakmak)',
 'Invekto Ops','~3 saat hazirlik','DentAdavista/plan/dent-golive.html','#training'),

('dent-pilot','training-ai-supervise-screencast','TODO','OPS','P1',110,
 '10.2 AI Supervise Screencast',
 '10 dk video — handoff + intent log.',
 E'**Icerik:**\n- AI ne zaman handoff yapar (intent confidence)\n- Intent log nasil okunur\n- Yeni FAQ varyanti ekleme',
 'Invekto Ops','~2 saat hazirlik','DentAdavista/plan/dent-golive.html','#training'),

('dent-pilot','training-faq-cheatsheet','TODO','DOC','P2',120,
 '10.3 FAQ Cheatsheet',
 'Gunes anlayamadiginda ne yapacak — escalation karti.',
 E'**1 sayfalik cheatsheet:** Anlamayan AI -> 2 retry -> handoff. Olumsuz duygu -> direkt handoff. Koordinator override -> AI durdur.',
 'Invekto Ops','~1 saat','DentAdavista/plan/dent-golive.html','#training'),

('dent-pilot','training-kickoff-call','TODO','OPS','P1',130,
 '10.4 30dk Kickoff Call Q&A',
 'Pilot oncesi tum ekiple acilis toplantisi.',
 E'**Gundem:**\n- Sistem ozeti (5dk)\n- Senaryo demo (10dk)\n- Soru-cevap (15dk)\n\n**Katilimcilar:** Yonetim + koordinator + Dr. Ozge.',
 'Invekto + Musteri','30dk','DentAdavista/plan/dent-golive.html','#training'),

('dent-pilot','contract-quirks-pre-smoke','TODO','OPS','P1',140,
 '14 Contract Quirks Pre-Smoke Verify',
 '5 madde tik et — her biri gercek bir drift cycle sebep.',
 E'**5 quirk:**\n- Q.1 Header `X-Invekto-Api-Key` (NOT `X-Api-Key`)\n- Q.2 Body wrapper `{ fields: { ... } }`\n- Q.3 consent_marketing boolean (string `yes`/`1` rejected)\n- Q.4 Slug regex `^[a-z0-9][a-z0-9-]{0,49}$` (underscore rejected)\n- Q.5 Smoke URL: prod sunucu localhost:5000 (NOT app.invekto.com)',
 'Invekto Ops','5dk pre-smoke','DentAdavista/plan/dent-golive.html','#quirks'),

('dent-pilot','decision-photo-flow-pilot-usage','TODO','DECISION','P1',150,
 'Photo Flow Pilot Kullanim Karari',
 'FEAT-PHOTO-REQUEST yapildi (Migration 034). Pilotta hangi mod?',
 E'**Implementasyon hazir:** PhotoRequestService + InmaInboundMediaEndpoint + dashboard PhotoTab.\n\n**Karar:**\n- (a) Manuel — koordinator elle istek\n- (b) Yari otomatik — sistem ister, koordinator dogrulami\n- (c) Tam otomatik — pilotta acik (3 Kritik Soru B)\n- (d) Pilotta dahil degil — Stage 3 sonrasi\n\n**Onerimiz:** (b) yari otomatik.',
 'Q + Musteri','Toplanti','DentAdavista/plan/dent-golive.html','#real-gaps')
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §6.3 IN_PROGRESS kolonu (Pilot kapsamı yeni — 17.4).
INSERT INTO kanban_cards (board_key, card_slug, status, category, priority, position, title, summary, body_markdown, owner, eta, source_file, source_anchor) VALUES
('dent-pilot','lead-list-page','IN_PROGRESS','DEV','P0',10,
 '17.4.1 Lead Listesi Sayfasi',
 'Tablo + filtre (durum/sehir/foto) + sıralama + Lead detay acilisi.',
 E'**Endpoint`ler hazir:**\n- GET /api/v1/leads (zaten var)\n- PUT /api/v1/leads/{id}/status (zaten var)\n\n**Eksik:** Frontend tablo + filter UI.\n\n**Sure:** ~3 is gunu.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','lead-detail-timeline','IN_PROGRESS','DEV','P0',20,
 '17.4.2 Lead Detay Aktivite Timeline',
 'GET /api/v1/leads/{id}/activities + add note UI.',
 E'**Eksik:**\n- GET /api/v1/leads/{id}/activities endpoint\n- Lead detay sekmesi (timeline render)\n- Add note UI (koordinator manuel not)\n\n**Sure:** ~2 is gunu.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','appointments-page-zoho-sync','IN_PROGRESS','DEV','P0',30,
 '17.4.3 AppointmentsPage UI + Zoho Takvim Sync',
 'Slot tanimlama + takvim view + meeting link kopyalama. Cift yonlu Zoho.',
 E'**Iki kapsam:**\n- (a) Randevu UI: slot tanimlama + takvim view + link kopyalama (~4 gun)\n- (b) Zoho takvim senkron: Zoho`da olusan gorusme Invekto`ya yansir, tersi de (~2 gun)\n\n**Toplam:** ~6 gun.',
 'Dev','~6 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','doctors-management-page','IN_PROGRESS','DEV','P0',40,
 '17.4.4 Doktor Yonetimi Sayfasi',
 'Doktor ekle/duzenle + musaitlik haftalik takvim grid + slot generation.',
 E'**doctors tablosu Migration 031`de hazir.**\n\n**Eksik:**\n- Doktor CRUD UI\n- Musaitlik haftalik takvim grid\n- Slot generation (haftalik kuraldan slot uret)\n\n**Sure:** ~3 is gunu.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','conversion-funnel-chart','IN_PROGRESS','DEV','P0',50,
 '17.4.5 Donusum Hunisi Chart (Temel)',
 '/api/v1/leads/funnel endpoint`i var, gorsel yok.',
 E'**Endpoint var:** GET /api/v1/leads/funnel doner: { stage_counts, stage_dropoff }.\n\n**Eksik:** Frontend chart (huni gorsel: form -> karsilama -> cevap -> randevu -> gorusme -> tedavi).\n\n**Sure:** ~2 is gunu.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit')
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §6.4 BACKLOG kolonu (Post-pilot).
INSERT INTO kanban_cards (board_key, card_slug, status, category, priority, position, title, summary, body_markdown, owner, eta, source_file, source_anchor) VALUES
('dent-pilot','b-c2-welcome-overhaul','BACKLOG','DEV','P1',10,
 'B-C2 Welcome Templates Overhaul',
 '12 welcome + 36 FAQ persona-rich refresh + rotation.',
 E'**Kapsam:** Mevcut tek-varyant welcome -> 15 persona`li varyant + Stage 3 rotation.\n\n**Bagimlilik:** Pilot Stage 3 sonrasi.\n\n**Tracking:** tracking/feat-pilot-5-generic-roadmap.md',
 'Dev','Post-pilot','DentAdavista/plan/dent-golive.html','#b-c2'),

('dent-pilot','b0-google-meet-oauth','BACKLOG','DEV','P1',20,
 'B0 Google Meet Real OAuth',
 'Q Google Workspace credentials + real Meet link generation.',
 E'**Pilot durumu:** Mock Meet link.\n\n**Tam acilis icin:** Google Workspace Client ID/Secret -> ICS invite + real Meet odasi.\n\n**Bagimlilik:** Q Workspace provision.',
 'Q + Dev','Stage 3 oncesi','DentAdavista/plan/dent-golive.html','#b0'),

('dent-pilot','feat-clinic-metadata','BACKLOG','DEV','P1',30,
 'FEAT-CLINIC-METADATA — 3 Hardcoded Cleanup',
 'Klinik iletisim + persona isimleri + roadshow tarihleri tenant config`e tasi.',
 E'**3 hardcoded:**\n1. Klinik iletisim bilgileri (Migration 032 FAQ + dent-roadshow-content.json)\n2. Persona isimleri (Gunes/Dr. Ozge)\n3. Roadshow tarihleri\n\n**Cozum:** Tenant config alanlari + variable substitution.\n\n**Tracking:** tracking/feat-clinic-metadata.md',
 'Dev','2. musteri oncesi','DentAdavista/plan/dent-golive.html','#feat-clinic-metadata'),

('dent-pilot','tenant-default-seed','BACKLOG','DEV','P1',40,
 '17.4.6 Yeni Tenant Default Seed',
 'INMA lazy provisioning sonrasi default flow + template + feature flag hydration.',
 E'**Mevcut:** INMA`da firma acilir -> kullanici login olur -> InmaTokenIntrospector welcome -> ResolveOrCreateByInmaCodeAsync INSE tenant otomatik yaratir.\n\n**Eksik:** Yeni tenant icin default flow + template pack + feature flag hydration scripti.\n\n**Sure:** ~2 is gunu.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','tenant-suspend-button','BACKLOG','DEV','P2',50,
 '17.4.7 Tenant Suspend Butonu',
 'TenantsPage`e action; "Devre Disi" badge zaten var, action eksik.',
 E'**Mevcut:** TenantsPage tenant list + "Devre Disi" badge gosterimi.\n\n**Eksik:** Suspend action butonu (tenant_registry.is_active = false UPDATE).\n\n**Sure:** ~1 is gunu.',
 'Dev','~1 gun','DentAdavista/plan/dent-golive.html','#ui-full-audit'),

('dent-pilot','ui-coordinator-handoff-queue','BACKLOG','UI','P1',60,
 'UI #10 Koordinator Devir Kuyrugu (Handoff Queue)',
 'Robot anlamayinca devredilen hastalarin aksiyon ekrani.',
 E'**Icerik:** inbox + cevap input + oncelik sirasi + claim/release.\n\n**Sure:** ~3 gun. Pilot kritik (P0`a yukseltilebilir).',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-notification-hub','BACKLOG','UI','P1',70,
 'UI #11 Bildirim Merkezi (Notification Hub)',
 'Eskalasyon, hata, kapasite, anomali bildirimleri tek panelde.',
 E'**Icerik:** Tek panel + okundu/aksiyon isareti.\n\n**Gecici cozum:** Eskalasyonlar e-posta + WhatsApp ile koordinatore.\n\n**Sure:** ~3 gun.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-doctor-briefing','BACKLOG','UI','P2',80,
 'UI #12 Dr. Ozge Pre-Gorusme Brifing',
 'Gorusme oncesi 1 saatlik hazirlik: form + ozet + foto + onceki notlar.',
 E'**Icerik:** Form + sohbet ozeti + foto thumbnail + onceki notlar tek bakista.\n\n**Sure:** ~2 gun.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-quote-prep','BACKLOG','UI','P2',90,
 'UI #13 Teklif Hazirlama / Duzenleme UI',
 'Flowchart 3 ara durum: Hazirlanyor -> Hazirlandi -> Verildi.',
 E'**Icerik:** Fiyat kalemleri + paket notu + gonder butonu.\n\n**Sure:** ~4 gun.',
 'Dev','~4 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-daily-ops','BACKLOG','UI','P2',100,
 'UI #14 Operasyon Komuta Anasayfasi',
 '"Bugun kac hasta / kac randevu / kac eskalasyon / kac gorusme" tek bakis.',
 E'**Icerik:** Boslukk #8`in gunluk surumu.\n\n**Sure:** ~3 gun.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-gdpr-erasure','BACKLOG','UI','P3',110,
 'UI #15 GDPR Verimi Sil Akisi',
 'Hasta veri silme talebinde koordinator tek tikla tetikler.',
 E'**Mevcut:** SQL ile manuel.\n\n**Eksik:** UI buton + onay modali + 30g schedule.\n\n**Sure:** ~2 gun.',
 'Dev','~2 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-audit-log','BACKLOG','UI','P3',120,
 'UI #16 Audit Log / Aktivite Gecmisi',
 'Kim ne zaman ne degistirdi — Boslukk #9 icin kritik bagimlilik.',
 E'**Icerik:** Lead-bazli timeline.\n\n**Sure:** ~3 gun.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-bulk-message','BACKLOG','UI','P3',130,
 'UI #17 Toplu Mesaj / Kampanya Gonderim',
 'Stage 3`te 100 lead`e tek seferde mesaj.',
 E'**Mevcut:** CampaignsPage MCC config; bulk send farkli.\n\n**Sure:** ~4 gun.',
 'Dev','~4 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','ui-complaint-mgmt','BACKLOG','UI','P3',140,
 'UI #18 Sikayet / Problem Yonetimi',
 'Olumsuz mesaj tagging + escalation flow.',
 E'**Mevcut:** Sentiment analiz var (AiSentimentNode), dedicated tracker yok.\n\n**Sure:** ~3 gun.',
 'Dev','~3 gun','DentAdavista/plan/dent-golive.html','#ui-gaps'),

('dent-pilot','gap-multi-channel','BACKLOG','DEV','P2',150,
 'Boslukk #5 Multi-Channel (5 kanal)',
 'Instagram DM / Messenger / Twitter / TikTok / YouTube.',
 E'**Mevcut:** Pilotta 2 kanal (Meta form + web).\n\n**V2 paketinde:** Instagram DM + Telegram one cikiyor.\n\n**Sure:** Her kanal 1-2 hafta.',
 'Dev','Post-pilot V2','DentAdavista/plan/dent-golive.html','#scope-gaps'),

('dent-pilot','gap-quote-states','BACKLOG','DEV','P3',160,
 'Boslukk #6 Teklif 3 Ara Durum',
 'Hazirlanyor / Hazirlandi / Verildi otomatik gecisi.',
 E'**Pilot icin yeterli:** Manuel takip (koordinator panelinden).\n\n**Otomatik gecis icin:** ~1 gun. Zoho asama eklemek gerek.',
 'Dev','~1 gun','DentAdavista/plan/dent-golive.html','#scope-gaps'),

('dent-pilot','gap-24h-auto-cancel','BACKLOG','DEV','P3',170,
 'Boslukk #7 24h Teklif Otomatik Iptal',
 'Flowchart "24 SAAT SURE SINIRI KOY".',
 E'**Mevcut:** Hatirlatma var.\n\n**Eksik:** Otomatik iptal.\n\n**Tibbi sektorde hassas:** Genelde manuel onerilir. Karar: pilotta manuel mi kalsin?',
 'Dev','~0.5 gun','DentAdavista/plan/dent-golive.html','#scope-gaps'),

('dent-pilot','gap-kpi-dashboard','BACKLOG','DEV','P1',180,
 'Boslukk #8 Rapor / KPI Ekrani',
 'Donusum hunisi, A/B performansi, akış KPI tablosu, dropoff.',
 E'**Mevcut:** flow_executions + node_trace DB`de, agregat ekran yok.\n\n**Temel rapor:** ~5 is gunu (huni + 6 metrik kart + haftalik trend).\n\n**Detayli:** ~10 is gunu (A/B + intent + lead gecis sureleri).',
 'Dev','5-10 gun','DentAdavista/plan/dent-golive.html','#scope-gaps'),

('dent-pilot','gap-inma-pipeline-dropdown','BACKLOG','DEV','P2',190,
 'Boslukk #9 INMA Pipeline + Agent Dropdown V2',
 'INMA sohbet panelinde "Su anki asama" dropdown (cift yonlu).',
 E'**Pilot disinda — Stage 3 sonrasi.**\n\n**Is yuku:**\n- INMA ekibi (5-10 gun): yeni kolon + dropdown UI + API endpoint + INMA -> Invekto webhook\n- Bizim ekip (3-5 gun): webhook handler + reverse sync + idempotency + audit trail\n- Toplam paralel: 2 hafta\n\n**Onayli kararlar (28 Nisan):**\n- Yol A: INMA native alan\n- Cift yonlu sync\n- Asama listesi sahibi: INMA',
 'Dev (INMA paralel)','2 hafta paralel','DentAdavista/plan/dent-golive.html','#scope-gaps'),

('dent-pilot','lessons-learned-archive','BACKLOG','DOC','P3',200,
 'Lessons-Learned Archive (B4)',
 '422 entry -> arsive tasi (50 esik asti).',
 E'**Doc hygiene:** arch/lessons-learned.md 422 entry. Esik 50.\n\n**Action:** Eski entry`leri arch/lessons-learned-archive.md`ye tasi, son 3 ay aktif.\n\n**Sure:** ~30dk manual.',
 'Doc','30dk','DentAdavista/plan/dent-golive.html','#scope-gaps')
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §6.5 DONE kolonu (Tamamlanan).
INSERT INTO kanban_cards (board_key, card_slug, status, category, priority, position, title, summary, body_markdown, owner, eta, source_file, source_anchor, completed_at) VALUES
('dent-pilot','pilot-15-packets','DONE','DEV','P0',10,
 '15/15 Pilot-Critical Paket DONE+DEPLOYED',
 'Sistem yazilim tarafi tamamen hazir.',
 E'**15 paket:** B-META + B-VCP-DOCTORS + Paket A + Paket B + Paket C1 + FEAT-PHOTO + FEAT-MCC + FEAT-EFS + FEAT-TFM + LIW + ... (full liste tracking/README.md)',
 'Dev','-','DentAdavista/plan/dent-golive.html','#done','2026-04-24T00:00:00Z'),

('dent-pilot','b-meta-webhook','DONE','DEV','P0',20,
 'B-META Meta Leadgen Webhook Native',
 'Meta webhook -> Invekto leads pipeline.',
 E'**Deploy:** 2026-04-24 22:30. Codex 4-chunk PASS.\n\n**Tracking:** tracking/20260425-feat-meta-leadgen-webhook.md',
 'Dev','-','tracking/20260425-feat-meta-leadgen-webhook.md',NULL,'2026-04-24T22:30:00Z'),

('dent-pilot','p10-hangfire-queue','DONE','DEV','P1',30,
 'P10 Hangfire Marketing-Followup Queue Fix',
 'EFS drip sequence dead-on-arrival sorunu.',
 E'**Fix:** Backend scheduler marketing-followup queue`yu pickup eder.\n\n**Tracking:** tracking/20260423-feat-efs-hangfire-queue-fix.md',
 'Dev','-','tracking/20260423-feat-efs-hangfire-queue-fix.md',NULL,'2026-04-25T00:00:00Z'),

('dent-pilot','migration-031-doctors','DONE','DEV','P0',40,
 'Migration 031: Doctors Bootstrap',
 'doctors tablosu + 4 slot backfill.',
 E'**Migration:** 031-doctors-bootstrap.sql\n\n**Tracking:** tracking/20260424-dent-doctors-bootstrap.md',
 'Dev','-','arch/db/migrations/031-doctors-bootstrap.sql',NULL,'2026-04-25T00:00:00Z'),

('dent-pilot','migration-033-meta-leadgen','DONE','DEV','P0',50,
 'Migration 033: Meta Leadgen Webhook',
 'Tenant basina meta_leadgen_settings JSONB + webhook idempotency.',
 'Migration 033 prod`da, B-META endpoint`leri kullaniyor.',
 'Dev','-','arch/db/migrations/033-meta-leadgen-webhook.sql',NULL,'2026-04-24T22:30:00Z'),

('dent-pilot','migration-034-photo-flow','DONE','DEV','P1',60,
 'Migration 034: Photo Request Flow',
 'leads.photo_status + photo_inbound_idempotency + PhotoRequestService.',
 E'**Migration:** 034-photo-request-flow.sql\n\n**Service:** PhotoRequestService + InmaInboundMediaEndpoint + PhotoTab.tsx\n\n**Tracking:** tracking/20260427-feat-photo-request-flow.md',
 'Dev','-','arch/db/migrations/034-photo-request-flow.sql',NULL,'2026-04-27T00:00:00Z'),

('dent-pilot','paket-a-plan-tier','DONE','DEV','P0',70,
 'Paket A: plan_tier kurumsal Upgrade',
 'tenant_registry.plan_tier `baslangic` -> `kurumsal` (Appointments unlock).',
 E'**S6 smoke 201 verified.**\n\n**Tracking:** tracking/20260424-dent-plan-tier-upgrade.md',
 'Dev','-','tracking/20260424-dent-plan-tier-upgrade.md',NULL,'2026-04-24T00:00:00Z'),

('dent-pilot','lead-stages-inma-ownership','DONE','DECISION','P0',80,
 'Lead Asamalari INMA Sahipligi Karari',
 '8 durum + dropdown listesi INMA tarafinda yonetilir.',
 E'**28 Nisan kararı:** Asama isimleri, sirasi, gecis kurallari INMA tarafinda. Invekto + Zoho INMA`dan ceker.\n\n**Detay:** dent-golive.html §22 Lead Asamalari.',
 'Q + Musteri','-','DentAdavista/plan/dent-golive.html','#lead-stages','2026-04-28T00:00:00Z'),

('dent-pilot','section-19-24-consolidation','DONE','DOC','P2',90,
 'dent-toplanti.html -> dent-golive.html §19-24',
 'Toplanti dokumanindan 6 yeni section konsolide edildi.',
 E'**§19** Toplanti Soru Katalogu (27 soru)\n**§20** Karar Tablosu K1-K10\n**§21** Musteri SSS\n**§22** Lead Asamalari 8 Durum\n**§23** Musteri DocX Eslestirme + Boslukk Detay\n**§24** 6 Senaryo\n\ndent-toplanti.html silindi.',
 'Doc','-','DentAdavista/plan/dent-golive.html',NULL,'2026-04-28T11:00:00Z')
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §7. Postcondition guard (INV-SEED-024..025).
DO $verify$
DECLARE
    cards_count integer;
    constraint_count integer;
BEGIN
    SELECT COUNT(*) INTO cards_count FROM kanban_cards WHERE board_key = 'dent-pilot';
    IF cards_count < 40 THEN
        RAISE EXCEPTION '[INV-SEED-024] kanban_cards seed eksik: dent-pilot board_key icin % kart bulundu, en az 40 bekleniyordu.', cards_count;
    END IF;

    SELECT COUNT(*) INTO constraint_count
      FROM pg_constraint
     WHERE conname IN (
        'uq_kanban_cards_board_slug',
        'chk_kanban_cards_status',
        'chk_kanban_cards_category',
        'chk_kanban_cards_priority'
     );
    IF constraint_count < 4 THEN
        RAISE EXCEPTION '[INV-SEED-025] kanban_cards constraint eksik: % bulundu, 4 bekleniyordu.', constraint_count;
    END IF;

    RAISE NOTICE 'Migration 035 OK: % dent-pilot kart, % constraint aktif.', cards_count, constraint_count;
END$verify$;
