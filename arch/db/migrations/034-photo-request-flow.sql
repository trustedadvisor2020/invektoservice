-- Migration 034: FEAT-PHOTO — Foto Isteme Akisi (Dent Adavista pilot)
--
-- Context:
-- Dent Adavista pilot Stage 1: hasta randevu onaylandiktan ~30 saniye sonra
-- WhatsApp uzerinden foto isteme mesaji + sablon gorseli (dental angle guide)
-- gonderilir; ilk inbound resim mesaji geldiginde leads.photo_status='received'
-- otomatik mark olur. 24h sessizlik = nazik hatirlatma, 48h = koordinator
-- eskalasyonu. Pilot scope = A+B alt akislari (sablon + yazili acilar);
-- C (cross-platform pull) ve D (WhatsApp numara isteme) post-pilot.
--
-- Bu migration `leads` tablosuna 3 yeni kolon ekler ve INMA at-least-once
-- redelivery'sine karsi PhotoInboundHandler icin idempotency anchor olarak
-- `photo_inbound_idempotency` tablosu olusturur. Migration 033 (B-META) /
-- 031 (B-VCP-DOCTORS) yorum + postcondition pattern'lerini birebir takip eder.
--
-- Scope (minimal surface, interview gates Q1..Q4 Q-approved 2026-04-27):
--   * ALTER TABLE leads ADD 3 COLUMN IF NOT EXISTS:
--       - photo_status      VARCHAR(20) NOT NULL DEFAULT 'none'
--                           CHECK (photo_status IN
--                             ('none','requested','received','escalated','rejected'))
--       - photo_received_at TIMESTAMPTZ NULL
--                           (ilk inbound resim mesaji geldiginde now())
--       - photo_count       INTEGER NOT NULL DEFAULT 0
--                           (atomic UPDATE ... SET photo_count = photo_count + 1)
--     Mevcut tum lead row'lar 'none' / 0 / NULL ile dolar (DEFAULT clause). Existing
--     lead semantics zero behavior change — eski lead'ler 'none' status'unda kalir,
--     dispatch job tetiklenmedikce status degismez.
--   * CREATE TABLE photo_inbound_idempotency
--       (lead_id, media_url_hash) UNIQUE -> PhotoInboundHandler her INMA inbound
--       media event icin INSERT ON CONFLICT DO NOTHING ile duplicate'leri reddeder.
--       lead_id ON DELETE CASCADE — GDPR right-to-erasure leads silinince audit row
--       da temizlenir (foto retention policy ileri paket).
--   * Postcondition DO $verify$ block (INV-SEED-021..023) RAISE EXCEPTION ile
--     fail-loud (031/033 pattern). ALTER/CREATE/GRANT silently no-op olursa
--     migration transaction abort eder; partial-apply riskine karsi savunma.
--   * Idempotent: ADD COLUMN IF NOT EXISTS, CREATE TABLE IF NOT EXISTS,
--     CREATE INDEX IF NOT EXISTS, GRANT ALL ON ... TO invekto.
--   * CHECK constraint adlandirilarak (chk_leads_photo_status) eklenir; idempotent
--     re-run icin precondition guard (information_schema.constraint_column_usage'a
--     bakip varsa atla) DO block icindeki ALTER TABLE ... ADD CONSTRAINT'i
--     IF NOT EXISTS ile sarar.
--
-- Retention:
--   * photo_inbound_idempotency satirlari `received_at`'a gore biriktirilir (ne
--     zaman dedup edildigi). Pilotta lead basina ~5 foto ortalama beklenir;
--     5 lead/gun x 5 foto x 30 gun = 750 satir/ay (ihmal edilebilir).
--     Daily purge job DEFERRED (post-pilot paket, INMA inbound media volume
--     uzerinden gercek metrikten sonra retention belirlenir).
--   * leads.photo_count integer counter; reset/cleanup yok (lead lifecycle ile
--     biter). Foto thumbnail'lar INMA tarafinda host edilir, biz URL referansi
--     tutmuyoruz (storage drift onleme — INMA URL otoritesidir).
--
-- Security:
--   * media_url_hash SHA-256 (handler tarafinda compute edilir), raw URL
--     saklanmaz — tenant izolasyon side-channel'i + GDPR-by-design.
--   * Cross-tenant contamination guard: leads.tenant_id FK + photo_inbound_
--     idempotency.lead_id -> leads(id) zinciri tenant scope'u korur. Tenant
--     A'nin lead'i icin INSERT eden handler tenant B'nin row'una dokunamaz
--     (path: lead_id PK lookup'tan once tenant guard'i Backend layer'da
--     enforce edilir; bkz. PhotoInboundHandler.HandleInboundMediaAsync).
--   * `invekto` role grant (lesson 2026-04-18 — `invekto_app` degil!).
--
-- Related:
--   * Canonical mirror       : arch/db/pkt6b1-niche-business.sql (leads ALTER
--                              mirrored inline; plan'da yanlislikla
--                              "arch/db/leads.sql" yazildi — gercek canonical
--                              pkt6b1-niche-business.sql, migration 020/021
--                              precedent ile)
--   * Plan                   : arch/plans/20260427-feat-photo-request-flow.json
--   * Tracking               : tracking/20260427-feat-photo-request-flow.md
--   * Error codes            : arch/errors.md INV-AT-073..080 +
--                              INV-SEED-021..023
--   * Idempotency discipline : photo_inbound_idempotency UNIQUE
--                              (lead_id, media_url_hash) anchor, mirror'i LIW
--                              precedent'i ile (lead_id + payload hash UNIQUE).
--                              Atomic UPDATE leads SET photo_status='received',
--                              photo_count=photo_count+1
--                              WHERE id=@id AND photo_status<>'received'
--                              + INSERT ON CONFLICT DO NOTHING birlestirilince
--                              redelivery yan etkisi yok (Q1 verification).
--   * Bridge limitation      : INMA chatoperation /api/v1/callback/wapcrm
--                              messageType=1 (text-only) destekliyor; gercek
--                              media attachment (image upload) bridge expansion
--                              gerektiriyor. Pilot dispatch'i text-only +
--                              public seed image URL referansi pattern'iyle
--                              cozuldu (DentAdavista/seeds/photo-request-
--                              templates.json varyant A icindeki URL).

BEGIN;

-- =============================================================================
-- §1. leads ALTER — 3 yeni kolon (photo_status / photo_received_at / photo_count)
-- =============================================================================
-- Empty default = 'none' / NULL / 0; mevcut lead row'lari migration sonrasi
-- "foto akisi devre disi" durumunda kalir, dispatch job tetiklenmedikce
-- status degismez. Bu, P11 slot booking sonrasi otomatik trigger'in ilk
-- defa A varyantini yollamasiyla 'requested'a gecisi tetikleyecek.

ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS photo_status      VARCHAR(20)  NOT NULL DEFAULT 'none',
    ADD COLUMN IF NOT EXISTS photo_received_at TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS photo_count       INTEGER      NOT NULL DEFAULT 0;

-- CHECK constraint idempotent guard. ADD CONSTRAINT IF NOT EXISTS PostgreSQL
-- 9.6+ destekliyor degil (sadece UNIQUE/PRIMARY KEY icin), CHECK constraint
-- icin DO block ile precondition wrap edilir. Constraint adi
-- chk_leads_photo_status — DROP gerekirse acik isim ile.
DO $$
BEGIN
    -- Codex iter 2 CQ2 fix: scope pre-add check to public.leads relation;
    -- pg_constraint.conname can collide with a same-named constraint on
    -- another table, so verify ownership before deciding to skip the ADD.
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class rel ON rel.oid = c.conrelid
          JOIN pg_namespace ns ON ns.oid = rel.relnamespace
         WHERE c.conname = 'chk_leads_photo_status'
           AND rel.relname = 'leads'
           AND ns.nspname = 'public'
    ) THEN
        ALTER TABLE leads
            ADD CONSTRAINT chk_leads_photo_status
            CHECK (photo_status IN ('none','requested','received','escalated','rejected'));
    END IF;
END
$$;

-- =============================================================================
-- §2. photo_inbound_idempotency (PhotoInboundHandler duplicate guard)
-- =============================================================================
-- Idempotency anchor: PhotoInboundHandler her INMA inbound media event icin
-- (lead_id, sha256(media_url)) tuple'i INSERT ON CONFLICT DO NOTHING; UNIQUE
-- index conflict = duplicate redelivery, handler tarafinda swallow + INV-AT-073
-- log + photo_count UPDATE skip. Returning bool firstReceive (rowsAffected=1)
-- atomic UPDATE'in basari/skip semantici icin tek source.
--
-- received_at (audit trail): handler ilk gordugu anda now(); duplicate satir
-- gelmeyecek. Q1 verification: race condition'da iki concurrent INSERT ayni
-- (lead_id, hash) icin biri UNIQUE violation alir, diger PASS — atomic UPDATE
-- WHERE photo_status<>'received' guard'i sayesinde count yine 1'de kalir.

CREATE TABLE IF NOT EXISTS photo_inbound_idempotency (
    id              BIGSERIAL    PRIMARY KEY,
    lead_id         INTEGER      NOT NULL REFERENCES leads(id) ON DELETE CASCADE,
    media_url_hash  CHAR(64)     NOT NULL,
        -- SHA-256 hex (lowercase, 64 char). Handler INMA media URL'i UTF-8
        -- bytes -> sha256 -> hex.ToLower() ile compute eder.
    received_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT uq_photo_inbound_idempotency UNIQUE (lead_id, media_url_hash)
);

-- Tenant + recency index (post-pilot Dashboard "foto inbound timeline" hot path
-- icin forward-compat). lead_id zaten PK lookup; ek index lead_id'ye gore degil
-- received_at DESC partition'i icin (recent N inbound photos).
CREATE INDEX IF NOT EXISTS idx_photo_inbound_idempotency_recent
    ON photo_inbound_idempotency (lead_id, received_at DESC);

-- Prod role is `invekto` (lesson 2026-04-18, not `invekto_app`).
GRANT ALL ON photo_inbound_idempotency TO invekto;
GRANT USAGE, SELECT ON SEQUENCE photo_inbound_idempotency_id_seq TO invekto;

-- =============================================================================
-- §3. Postcondition verify — fail-loud assertions (INV-SEED-021..023)
-- =============================================================================
-- Exception messages include actionable why + next-step guidance per Codex iter
-- 0 CQ12 feedback carried forward from migrations 031 / 032 / 033.

DO $verify$
DECLARE
    v_status_col       BOOLEAN;
    v_received_col     BOOLEAN;
    v_count_col        BOOLEAN;
    v_status_check     BOOLEAN;
    v_idem_table       BOOLEAN;
    v_idem_unique      BOOLEAN;
    v_idem_recent_idx  BOOLEAN;
    v_grant_ok         BOOLEAN;
BEGIN
    -- V21: leads.photo_status / photo_received_at / photo_count columns +
    --      CHECK constraint hepsi present.
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = 'leads'
          AND column_name  = 'photo_status'
          AND data_type IN ('character varying', 'varchar')
    ) INTO v_status_col;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = 'leads'
          AND column_name  = 'photo_received_at'
    ) INTO v_received_col;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = 'leads'
          AND column_name  = 'photo_count'
          AND data_type    = 'integer'
    ) INTO v_count_col;

    -- Codex iter 2 CQ2 fix: scope pg_constraint check to the leads relation
    -- via conrelid join. pg_constraint.conname is unique within a relation but
    -- a same-named constraint on another table could otherwise produce a
    -- silent false-positive on this verification. Joining to pg_class on
    -- conrelid + pg_namespace on relnamespace pins the check to public.leads.
    SELECT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class rel ON rel.oid = c.conrelid
          JOIN pg_namespace ns ON ns.oid = rel.relnamespace
         WHERE c.conname = 'chk_leads_photo_status'
           AND rel.relname = 'leads'
           AND ns.nspname = 'public'
    ) INTO v_status_check;

    IF NOT (v_status_col AND v_received_col AND v_count_col AND v_status_check) THEN
        RAISE EXCEPTION '[INV-SEED-021] leads photo_* columns / CHECK constraint missing '
            '(status_col=%, received_col=%, count_col=%, status_check=%). '
            'Root cause: ALTER TABLE silently skipped (column type clash, table missing, '
            'or role lacks ALTER privilege) OR DO block did not run ADD CONSTRAINT (pg_constraint '
            'missing chk_leads_photo_status). Next step: '
            'verify `\d leads` shape, manually re-run ALTER TABLE leads ADD COLUMN IF NOT EXISTS '
            'photo_status VARCHAR(20) NOT NULL DEFAULT ''none'', photo_received_at TIMESTAMPTZ NULL, '
            'photo_count INTEGER NOT NULL DEFAULT 0; + ALTER TABLE leads ADD CONSTRAINT '
            'chk_leads_photo_status CHECK (photo_status IN '
            '(''none'',''requested'',''received'',''escalated'',''rejected'')); + re-run verify.',
            v_status_col, v_received_col, v_count_col, v_status_check;
    END IF;

    -- V22: photo_inbound_idempotency table + UNIQUE constraint + recent-events index.
    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'photo_inbound_idempotency'
    ) INTO v_idem_table;

    -- Codex iter 2 CQ2 fix: same name-scoping discipline as v_status_check
    -- — pin UNIQUE constraint check to public.photo_inbound_idempotency.
    SELECT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class rel ON rel.oid = c.conrelid
          JOIN pg_namespace ns ON ns.oid = rel.relnamespace
         WHERE c.conname = 'uq_photo_inbound_idempotency'
           AND rel.relname = 'photo_inbound_idempotency'
           AND ns.nspname = 'public'
    ) INTO v_idem_unique;

    -- pg_indexes already exposes schemaname + tablename — pin to both so a
    -- same-named index on another table cannot produce a false-positive.
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_photo_inbound_idempotency_recent'
          AND tablename = 'photo_inbound_idempotency'
          AND schemaname = 'public'
    ) INTO v_idem_recent_idx;

    IF NOT (v_idem_table AND v_idem_unique AND v_idem_recent_idx) THEN
        RAISE EXCEPTION '[INV-SEED-022] photo_inbound_idempotency table / UNIQUE / recent-index missing '
            '(table=%, unique=%, recent_idx=%). '
            'Root cause: CREATE TABLE IF NOT EXISTS silently skipped (schema drift or transaction '
            'abort at prior DDL) OR UNIQUE clause stripped (partial DDL apply) OR CREATE INDEX '
            'IF NOT EXISTS skipped. Next step: verify `\d photo_inbound_idempotency`, manually '
            're-run §2 block (CREATE TABLE + ALTER TABLE ADD CONSTRAINT '
            'uq_photo_inbound_idempotency UNIQUE (lead_id, media_url_hash) + CREATE INDEX '
            'idx_photo_inbound_idempotency_recent ON photo_inbound_idempotency '
            '(lead_id, received_at DESC)). PhotoInboundHandler at-least-once dedup depends '
            'on UNIQUE constraint; without it duplicate INMA webhooks would inflate photo_count.',
            v_idem_table, v_idem_unique, v_idem_recent_idx;
    END IF;

    -- V23: GRANT ALL invekto on photo_inbound_idempotency (prod role grant drift guard).
    SELECT EXISTS (
        SELECT 1 FROM information_schema.role_table_grants
        WHERE table_name     = 'photo_inbound_idempotency'
          AND grantee        = 'invekto'
          AND privilege_type = 'INSERT'
    ) INTO v_grant_ok;
    IF NOT v_grant_ok THEN
        RAISE EXCEPTION '[INV-SEED-023] GRANT ALL to invekto not applied on '
            'photo_inbound_idempotency. Root cause: GRANT statement silently skipped OR role '
            'rename drift. Next step: verify `SELECT rolname FROM pg_roles WHERE '
            'rolname=''invekto''` exists, manually re-run `GRANT ALL ON photo_inbound_idempotency '
            'TO invekto; GRANT USAGE, SELECT ON SEQUENCE photo_inbound_idempotency_id_seq TO '
            'invekto;` + re-run verify.';
    END IF;

    RAISE NOTICE '[FEAT-PHOTO] postcondition verify PASS '
        '(leads_cols=ok, leads_check=ok, idem_table=ok, idem_unique=ok, '
        'idem_recent_idx=ok, grant=ok)';
END
$verify$;

COMMIT;
