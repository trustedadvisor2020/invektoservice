-- ============================================================
-- Migration 040 — FEAT-CLINIC-METADATA: Multi-Tenant Hardcoded Cleanup
-- ============================================================
-- Purpose: tenant_settings tablosuna iki yeni JSONB sutun (clinic_contact + team_members)
--          ekler, Dent (tenant_id=18173130) icin seed yazar, faqs tablosunda Dent'e ait
--          36 row icindeki hardcoded klinik verilerini placeholder'a cevirir, ve
--          chatbot_flows flow_id=29 welcome metnini placeholder-driven hale getirir.
--          Boylece 2. musteri Dashboard config formu doldurarak sifir SQL kopyalama
--          ile sisteme katilir.
--
-- Scope (explore subagent kanitladi 2026-04-29 14:55 UTC):
--   §1 Pre-state snapshot   -> dent_paket_clinic_metadata_archive_20260429
--   §2 ALTER TABLE          -> tenant_settings 2 yeni JSONB kolon (additive)
--   §3 Dent seed UPDATE     -> clinic_contact 7 alan + team_members 2 uye
--   §4 faqs targeted UPDATE -> 6 REPLACE statement (literal -> placeholder)
--   §5 chatbot_flows UPDATE -> flow_id=29 nodes[1].data.text jsonb_set
--   §6 DO $verify$          -> INV-SEED-032..034 fail-loud postcondition
--
-- Risk: LOW (ALTER additive + targeted UPDATE + atomic transaction). Mevcut
--       tenant_settings rows '{}' ve '[]' default ile guvenli, FEAT-DMP / FEAT-MCC
--       / FEAT-TFM altyapisi UNTOUCHED.
--
-- Rollback: dent_paket_clinic_metadata_archive_20260429 table preserves pre-state
--           row_to_json snapshots; manuel restore icin ROLLBACK section bottom.
--
-- INV codes: INV-SEED-032 (kolonlar), INV-SEED-033 (Dent seed), INV-SEED-034 (faqs/flow ROW_COUNT)
-- ============================================================

BEGIN;

-- ============================================================
-- §0. PRECONDITION: fail-loud guards (pre-INSERT/UPDATE state assertions)
-- ============================================================
-- Idempotency strategy: ALTER TABLE IF NOT EXISTS guards re-run; UPDATE statements
-- WHERE clause includes literal-presence check so re-run after success becomes no-op
-- (ROW_COUNT=0 → expected on re-run, not fail-loud). Pre-execute precondition checks
-- ensure the migration is running on a state we recognize (Dent tenant present + faqs
-- 36 row + chatbot_flows flow_id=29 wiring intact).

DO $precondition$
DECLARE
    v_tenant_exists       INTEGER;
    v_faqs_count          INTEGER;
    v_flow_text           TEXT;
BEGIN
    -- P1: Dent tenant exists in tenant_registry (refactor scope is Dent-specific)
    SELECT COUNT(*) INTO v_tenant_exists
    FROM tenant_registry WHERE tenant_id = 18173130 AND is_active = TRUE;
    IF v_tenant_exists <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-033] Pre-UPDATE precondition fail: Dent tenant_id=18173130 not found or inactive (expected 1 active row, got %). '
            'Root cause: tenant_registry seed missing or tenant deactivated post-pilot. '
            'Next step: SELECT tenant_id, tenant_name, is_active FROM tenant_registry WHERE tenant_id=18173130; verify seed before re-running.', v_tenant_exists;
    END IF;

    -- P2: faqs has 36 rows for Dent (Migration 032 INSERT'leri)
    SELECT COUNT(*) INTO v_faqs_count FROM faqs WHERE tenant_id = 18173130;
    IF v_faqs_count <> 36 THEN
        RAISE EXCEPTION '[INV-SEED-034] Pre-UPDATE precondition fail: faqs row count mismatch for tenant_id=18173130 (expected 36 from Migration 032, got %). '
            'Root cause: Migration 032 not run, partial run, or external row INSERT/DELETE. '
            'Next step: SELECT COUNT(*), MIN(id), MAX(id) FROM faqs WHERE tenant_id=18173130; verify Migration 032 baseline before re-running.', v_faqs_count;
    END IF;

    -- P3: chatbot_flows flow_id=29 exists and welcome text accessible
    SELECT flow_config->'nodes'->1->'data'->>'text' INTO v_flow_text
    FROM chatbot_flows WHERE tenant_id = 18173130 AND flow_id = 29;
    IF v_flow_text IS NULL THEN
        RAISE EXCEPTION '[INV-SEED-034] Pre-UPDATE precondition fail: chatbot_flows flow_id=29 nodes[1].data.text not found for tenant_id=18173130. '
            'Root cause: Migration 032 §3 welcome update not applied or flow row deleted. '
            'Next step: SELECT flow_id, flow_name, jsonb_typeof(flow_config) FROM chatbot_flows WHERE tenant_id=18173130; verify flow seed.';
    END IF;
END $precondition$;

-- ============================================================
-- §1. Pre-state archive snapshot (rollback evidence)
-- ============================================================
-- Migration 032 precedent: dent_paket_c1_archive_20260424. Same pattern: dedicated
-- archive table, tenant-scoped row_to_json snapshots, GRANT ALL invekto, drop after
-- pilot go-live confirmed (post-pilot ops cleanup paket).

CREATE TABLE IF NOT EXISTS dent_paket_clinic_metadata_archive_20260429 (
    id            SERIAL PRIMARY KEY,
    archive_type  TEXT NOT NULL,
    payload       JSONB NOT NULL,
    archived_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
COMMENT ON TABLE dent_paket_clinic_metadata_archive_20260429 IS
    'FEAT-CLINIC-METADATA pre-state snapshots for rollback. Drop after multi-tenant validation confirmed (post-2nd-tenant ops cleanup).';

GRANT ALL ON dent_paket_clinic_metadata_archive_20260429 TO invekto;
GRANT USAGE, SELECT ON SEQUENCE dent_paket_clinic_metadata_archive_20260429_id_seq TO invekto;

-- Snapshot tenant_settings before column add + seed
INSERT INTO dent_paket_clinic_metadata_archive_20260429 (archive_type, payload)
SELECT 'tenant_settings_pre_alter', row_to_json(ts.*)::jsonb
FROM tenant_settings ts
WHERE ts.tenant_id = 18173130;

-- Snapshot faqs before targeted UPDATE
INSERT INTO dent_paket_clinic_metadata_archive_20260429 (archive_type, payload)
SELECT 'faqs_pre_update', row_to_json(f.*)::jsonb
FROM faqs f
WHERE f.tenant_id = 18173130;

-- Snapshot chatbot_flows before welcome jsonb_set
INSERT INTO dent_paket_clinic_metadata_archive_20260429 (archive_type, payload)
SELECT 'chatbot_flows_pre_update', row_to_json(cf.*)::jsonb
FROM chatbot_flows cf
WHERE cf.tenant_id = 18173130 AND cf.flow_id = 29;

-- ============================================================
-- §2. ALTER TABLE tenant_settings: 2 yeni JSONB sutun (additive)
-- ============================================================
-- Idempotent (ALTER COLUMN IF NOT EXISTS): re-run safe. NOT NULL DEFAULT JSONB literal
-- pattern matches FEAT-MCC campaign_config + FEAT-TFM field_mapping precedent.

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS clinic_contact JSONB NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS team_members   JSONB NOT NULL DEFAULT '[]'::jsonb;

COMMENT ON COLUMN tenant_settings.clinic_contact IS
    'FEAT-CLINIC-METADATA (migration 040): per-tenant klinik iletisim bilgileri JSONB. '
    'Shape: { "name": "...", "phone": "+CC ...", "website": "https://...", '
    '"instagram": "https://...", "facebook": "https://...", "address": "...", "city": "..." }. '
    'Empty default ({}) = klinik bilgisi tanimlanmamis; ClinicTemplateApplier {{clinic.X}} '
    'placeholder render bos string doner (graceful no-op). Validation app-side: phone E.164 '
    '(INV-BE-123), URL https?:// (INV-BE-124), JSON shape (INV-BE-122). 5dk MemoryCache TTL '
    '(DbTenantClinicMetadataResolver) — Backend PUT cache.Invalidate, peer Automation/Marketing '
    'eventual consistency 5dk (FEAT-MCC pattern).';

COMMENT ON COLUMN tenant_settings.team_members IS
    'FEAT-CLINIC-METADATA (migration 040): per-tenant ekip uyeleri JSONB array. '
    'Shape: [{ "role": "dentist|receptionist|coordinator|...", "name": "...", "title": "...", '
    '"language": "en|tr|ar|de|fr|...", "pronouns": "she/her|he/him|they/them" }]. '
    'Empty default ([]) = ekip tanimlanmamis; ClinicTemplateApplier {{team.role.field}} '
    'placeholder render bos string doner. Multi-role klinikler icin first-match rule '
    '(LINQ FirstOrDefault on role match — array order preserved); index syntax '
    '{{team.dentist[0].name}} post-pilot Phase 2 backlog. Dashboard /settings/clinic-metadata '
    'CRUD ile yonetilir.';

-- ============================================================
-- §3. Dent seed UPDATE: clinic_contact + team_members
-- ============================================================
-- Idempotent (jsonb assignment overwrites previous content). Pre-state snapshot §1 ile
-- alindi, post-state §6 verification ile dogrulanir. Other tenants UNTOUCHED.

UPDATE tenant_settings
SET clinic_contact = '{
        "name":      "Dent Adavista Dental Clinic",
        "phone":     "+90 545 343 09 09",
        "website":   "https://dentadavista.com",
        "instagram": "https://www.instagram.com/dentadavistaclinic",
        "facebook":  "https://www.facebook.com/profile.php?id=61554804412823",
        "address":   "Kuşadası, Aydın, Türkiye",
        "city":      "Kuşadası"
    }'::jsonb,
    team_members = '[
        {
            "role":     "dentist",
            "name":     "Dr. Özge Yılmazoğlu",
            "title":    "Founder Dentist",
            "language": "en",
            "pronouns": "she/her"
        },
        {
            "role":     "receptionist",
            "name":     "Güneş",
            "title":    "Patient Coordinator",
            "language": "en",
            "pronouns": "she/her"
        }
    ]'::jsonb,
    updated_at = NOW()
WHERE tenant_id = 18173130;

-- ============================================================
-- §4. faqs UPDATE: targeted REPLACE (literal -> placeholder)
-- ============================================================
-- Each statement is a standalone REPLACE on faqs.answer for rows that contain the
-- literal. WHERE LIKE pre-filter ensures ROW_COUNT reflects the number of rows
-- actually changed (not the full 36 row count). Re-run safe: literal absent on second
-- run -> WHERE LIKE matches 0 rows -> no-op. Verification block §6 reports total
-- post-state literal count = 0 (all 6 patterns cleaned).
--
-- Replacement map (Migration 032 INSERT'lerden tespit edildi):
--   "Dr. Özge Yılmazoğlu"                                        -> {{team.dentist.name}}        (2 row)
--   "+90 545 343 09 09"                                          -> {{clinic.phone}}             (2 row)
--   "https://dentadavista.com/"                                  -> {{clinic.website}}           (3 row)
--   "https://www.instagram.com/dentadavistaclinic/"              -> {{clinic.instagram}}         (3 row)
--   "https://www.facebook.com/profile.php?id=61554804412823"     -> {{clinic.facebook}}          (3 row)
--   "Dent Adavista Dental Clinic"                                -> {{clinic.name}}              (1 row)

UPDATE faqs
SET answer = REPLACE(answer, 'Dr. Özge Yılmazoğlu', '{{team.dentist.name}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%Dr. Özge Yılmazoğlu%';

UPDATE faqs
SET answer = REPLACE(answer, '+90 545 343 09 09', '{{clinic.phone}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%+90 545 343 09 09%';

UPDATE faqs
SET answer = REPLACE(answer, 'https://dentadavista.com/', '{{clinic.website}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%https://dentadavista.com/%';

UPDATE faqs
SET answer = REPLACE(answer, 'https://www.instagram.com/dentadavistaclinic/', '{{clinic.instagram}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%https://www.instagram.com/dentadavistaclinic/%';

UPDATE faqs
SET answer = REPLACE(answer, 'https://www.facebook.com/profile.php?id=61554804412823', '{{clinic.facebook}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%https://www.facebook.com/profile.php?id=61554804412823%';

UPDATE faqs
SET answer = REPLACE(answer, 'Dent Adavista Dental Clinic', '{{clinic.name}}'),
    updated_at = NOW()
WHERE tenant_id = 18173130 AND answer LIKE '%Dent Adavista Dental Clinic%';

-- ============================================================
-- §5. chatbot_flows UPDATE: flow_id=29 welcome text -> placeholder-driven
-- ============================================================
-- Original (Migration 032:171-173):
--   "Hi {{lead.name}} 😊 How are you?
--    I'm Güneş from Dent Adavista Dental Clinic (Kuşadası). We're hosting a free 1-to-1
--    dental Roadshow with our founder dentist Dr. Özge Yılmazoğlu — Dublin (14 March)
--    and Cork (15 March).
--    Would you like me to save you a free spot? Dublin or Cork?"
--
-- Refactored (placeholders for tenant-agnostic):
--   "Hi {{lead.name}} 😊 How are you?
--    I'm {{team.receptionist.name}} from {{clinic.name}} ({{clinic.city}}). We're hosting
--    a free 1-to-1 dental Roadshow with our founder dentist {{team.dentist.name}} —
--    {{campaign.cities_human}}.
--    Would you like me to save you a free spot? Reply with your preferred city."
--
-- Format note (AC4 + tracking md S2): welcome metni format degisiyor — orijinal
-- "Dublin (14 March) and Cork (15 March)" + "Dublin or Cork?" hardcoded date+city
-- referanslari yerine {{campaign.cities_human}} (multi-tenant). Tarih+sehir spesifikligi
-- subsequent flow nodes'larda (slot booking) {{campaign.event_date}} + leadCity context
-- ile cozulur. Bu intentional; Smoke S1 strict bytewise regression FAQ rows icin gecerli,
-- welcome icin format-aware functional regression.
--
-- Defensive WHERE: idempotent re-run icin literal-presence check. Mevcut welcome text
-- `[EDIT:` placeholder (Migration 032 PRE-update durumu) veya hardcoded literal
-- ("Güneş from Dent Adavista") icermiyorsa UPDATE no-op (ROW_COUNT=0).

DO $update_welcome$
DECLARE
    v_updated_rows INTEGER;
    v_current_text TEXT;
BEGIN
    SELECT flow_config->'nodes'->1->'data'->>'text' INTO v_current_text
    FROM chatbot_flows WHERE tenant_id = 18173130 AND flow_id = 29;

    -- Re-run safe: if welcome already contains placeholder, skip silently.
    IF v_current_text LIKE '%{{team.receptionist.name}}%' THEN
        RAISE NOTICE '[migration 040 §5] chatbot_flows flow_id=29 already refactored to placeholder format — UPDATE skipped (idempotent re-run).';
        RETURN;
    END IF;

    UPDATE chatbot_flows
    SET flow_config = jsonb_set(
            flow_config,
            '{nodes,1,data,text}',
            to_jsonb('Hi {{lead.name}} 😊 How are you?
I''m {{team.receptionist.name}} from {{clinic.name}} ({{clinic.city}}). We''re hosting a free 1-to-1 dental Roadshow with our founder dentist {{team.dentist.name}} — {{campaign.cities_human}}.
Would you like me to save you a free spot? Reply with your preferred city.'::text)
        ),
        updated_at = NOW()
    WHERE tenant_id = 18173130
      AND flow_id = 29
      AND flow_config->'nodes'->1->'data'->>'text' LIKE '%Güneş from Dent Adavista%';

    GET DIAGNOSTICS v_updated_rows = ROW_COUNT;

    IF v_updated_rows <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-034] Welcome flow UPDATE silent no-op: expected 1 row updated, got %. '
            'Root cause: §0 P3 PASSed but UPDATE WHERE clause literal "Güneş from Dent Adavista" not present (text changed externally between precondition + UPDATE — race condition or manual edit). '
            'Next step: SELECT flow_config->''nodes''->1->''data''->>''text'' FROM chatbot_flows WHERE tenant_id=18173130 AND flow_id=29; '
            'verify current state, decide preserve external change vs reset to refactored format.', v_updated_rows;
    END IF;
END $update_welcome$;

-- ============================================================
-- §6. DO $verify$: postcondition fail-loud (INV-SEED-032..034)
-- ============================================================
-- Three-bucket verification:
--   INV-SEED-032: ALTER TABLE columns present + correct type + default
--   INV-SEED-033: Dent seed populated (clinic_contact 7 keys + team_members 2 entries)
--   INV-SEED-034: faqs hardcoded literals removed (count=0) + chatbot_flows welcome refactored

DO $verify$
DECLARE
    v_clinic_col_ok       BOOLEAN;
    v_team_col_ok         BOOLEAN;
    v_dent_clinic_keys    INTEGER;
    v_dent_team_count     INTEGER;
    v_dent_team_dentist   INTEGER;
    v_dent_team_receptionist INTEGER;
    v_remaining_dr_ozge   INTEGER;
    v_remaining_phone     INTEGER;
    v_remaining_website   INTEGER;
    v_remaining_instagram INTEGER;
    v_remaining_facebook  INTEGER;
    v_remaining_clinic_name INTEGER;
    v_welcome_refactored  INTEGER;
BEGIN
    -- INV-SEED-032: tenant_settings columns
    SELECT COUNT(*) = 1 INTO v_clinic_col_ok
    FROM information_schema.columns
    WHERE table_name = 'tenant_settings' AND column_name = 'clinic_contact' AND data_type = 'jsonb';
    IF NOT v_clinic_col_ok THEN
        RAISE EXCEPTION '[INV-SEED-032] tenant_settings.clinic_contact column verification FAIL: column missing or wrong type. '
            'Root cause: ALTER TABLE skipped (role privilege, type clash) or column dropped externally. '
            'Next step: SELECT column_name, data_type, is_nullable, column_default FROM information_schema.columns WHERE table_name=''tenant_settings''; verify schema before re-running.';
    END IF;

    SELECT COUNT(*) = 1 INTO v_team_col_ok
    FROM information_schema.columns
    WHERE table_name = 'tenant_settings' AND column_name = 'team_members' AND data_type = 'jsonb';
    IF NOT v_team_col_ok THEN
        RAISE EXCEPTION '[INV-SEED-032] tenant_settings.team_members column verification FAIL: column missing or wrong type. '
            'Root cause: ALTER TABLE skipped or column dropped. '
            'Next step: same as above.';
    END IF;

    -- INV-SEED-033: Dent seed (clinic_contact 7 keys, team_members 2 entries with role split)
    -- Count JSONB object keys via lateral subquery — jsonb_object_keys is SETOF text.
    SELECT (
        SELECT COUNT(*)::INTEGER
        FROM jsonb_object_keys(ts.clinic_contact)
    ) INTO v_dent_clinic_keys
    FROM tenant_settings ts
    WHERE ts.tenant_id = 18173130;
    -- If clinic_contact is empty {} -> 0 keys. Expected 7 (name, phone, website, instagram, facebook, address, city).
    IF v_dent_clinic_keys IS NULL OR v_dent_clinic_keys < 7 THEN
        RAISE EXCEPTION '[INV-SEED-033] Dent clinic_contact seed verification FAIL: expected >= 7 keys, got % (NULL = row missing). '
            'Root cause: §3 UPDATE matched 0 rows (Dent tenant_id mismatch) or jsonb assignment partially applied. '
            'Next step: SELECT clinic_contact FROM tenant_settings WHERE tenant_id=18173130; verify seed shape.', COALESCE(v_dent_clinic_keys, -1);
    END IF;

    SELECT jsonb_array_length(team_members) INTO v_dent_team_count
    FROM tenant_settings WHERE tenant_id = 18173130;
    IF v_dent_team_count IS NULL OR v_dent_team_count <> 2 THEN
        RAISE EXCEPTION '[INV-SEED-033] Dent team_members seed verification FAIL: expected 2 entries, got %. '
            'Root cause: §3 UPDATE matched 0 rows or partial assignment. '
            'Next step: SELECT team_members FROM tenant_settings WHERE tenant_id=18173130; verify array length.', COALESCE(v_dent_team_count, -1);
    END IF;

    -- INV-SEED-033: role split (1 dentist + 1 receptionist)
    SELECT COUNT(*) INTO v_dent_team_dentist
    FROM tenant_settings ts,
         jsonb_array_elements(ts.team_members) AS m
    WHERE ts.tenant_id = 18173130 AND m->>'role' = 'dentist';
    IF v_dent_team_dentist <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-033] Dent team_members role=dentist count verification FAIL: expected 1, got %. '
            'Root cause: §3 seed JSON mistyped role field. '
            'Next step: SELECT team_members FROM tenant_settings WHERE tenant_id=18173130; verify role field per entry.', v_dent_team_dentist;
    END IF;

    SELECT COUNT(*) INTO v_dent_team_receptionist
    FROM tenant_settings ts,
         jsonb_array_elements(ts.team_members) AS m
    WHERE ts.tenant_id = 18173130 AND m->>'role' = 'receptionist';
    IF v_dent_team_receptionist <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-033] Dent team_members role=receptionist count verification FAIL: expected 1, got %. '
            'Root cause: §3 seed JSON mistyped role field. '
            'Next step: same as above.', v_dent_team_receptionist;
    END IF;

    -- INV-SEED-034: faqs hardcoded literals removed (post-state count=0 each)
    SELECT COUNT(*) INTO v_remaining_dr_ozge   FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%Dr. Özge Yılmazoğlu%';
    SELECT COUNT(*) INTO v_remaining_phone     FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%+90 545 343 09 09%';
    SELECT COUNT(*) INTO v_remaining_website   FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%https://dentadavista.com/%';
    SELECT COUNT(*) INTO v_remaining_instagram FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%https://www.instagram.com/dentadavistaclinic/%';
    SELECT COUNT(*) INTO v_remaining_facebook  FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%https://www.facebook.com/profile.php?id=61554804412823%';
    SELECT COUNT(*) INTO v_remaining_clinic_name FROM faqs WHERE tenant_id = 18173130 AND answer LIKE '%Dent Adavista Dental Clinic%';

    IF v_remaining_dr_ozge <> 0 OR v_remaining_phone <> 0 OR v_remaining_website <> 0
       OR v_remaining_instagram <> 0 OR v_remaining_facebook <> 0 OR v_remaining_clinic_name <> 0 THEN
        RAISE EXCEPTION '[INV-SEED-034] faqs hardcoded literal verification FAIL: post-state remaining counts dr_ozge=% phone=% website=% instagram=% facebook=% clinic_name=% (expected 0 each). '
            'Root cause: §4 REPLACE statements partially applied (UTF-8 normalization mismatch, embedded variant, or external row INSERT after migration started). '
            'Next step: SELECT id, intent_slug, answer FROM faqs WHERE tenant_id=18173130 AND answer LIKE ANY (ARRAY[''%Dr. Özge%'', ''%+90 545%'', ''%dentadavista.com%'', ''%dentadavistaclinic%'', ''%Dent Adavista Dental%'']); inspect remaining rows manually.',
            v_remaining_dr_ozge, v_remaining_phone, v_remaining_website, v_remaining_instagram, v_remaining_facebook, v_remaining_clinic_name;
    END IF;

    -- INV-SEED-034: chatbot_flows welcome refactored
    SELECT COUNT(*) INTO v_welcome_refactored
    FROM chatbot_flows
    WHERE tenant_id = 18173130 AND flow_id = 29
      AND flow_config->'nodes'->1->'data'->>'text' LIKE '%{{team.receptionist.name}}%'
      AND flow_config->'nodes'->1->'data'->>'text' LIKE '%{{clinic.name}}%'
      AND flow_config->'nodes'->1->'data'->>'text' LIKE '%{{team.dentist.name}}%';

    IF v_welcome_refactored <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-034] chatbot_flows welcome refactor verification FAIL: expected 1 row containing all 3 placeholders, got %. '
            'Root cause: §5 jsonb_set silent no-op or partial placeholder set. '
            'Next step: SELECT flow_config->''nodes''->1->''data''->>''text'' FROM chatbot_flows WHERE tenant_id=18173130 AND flow_id=29; verify rendered text contains team.receptionist/clinic.name/team.dentist tokens.', v_welcome_refactored;
    END IF;

    RAISE NOTICE '[migration 040] FEAT-CLINIC-METADATA postcondition PASS: clinic_contact + team_members columns added; Dent seed populated (% clinic keys, % team entries); faqs hardcoded literals cleaned; chatbot_flows welcome refactored.',
        v_dent_clinic_keys, v_dent_team_count;
END $verify$;

COMMIT;

-- ============================================================
-- ROLLBACK (manual, post-failure restore — DO NOT auto-run)
-- ============================================================
-- BEGIN;
--   -- Restore tenant_settings (only Dent row)
--   UPDATE tenant_settings ts
--   SET clinic_contact = COALESCE((archive.payload->>'clinic_contact')::jsonb, '{}'::jsonb),
--       team_members   = COALESCE((archive.payload->>'team_members')::jsonb, '[]'::jsonb),
--       updated_at     = (archive.payload->>'updated_at')::timestamptz
--   FROM dent_paket_clinic_metadata_archive_20260429 archive
--   WHERE archive.archive_type = 'tenant_settings_pre_alter'
--     AND ts.tenant_id = 18173130;
--
--   -- Restore faqs (full row replacement from snapshot — rare, manual decision)
--   -- DELETE FROM faqs WHERE tenant_id = 18173130;
--   -- INSERT INTO faqs (...) SELECT ... FROM dent_paket_clinic_metadata_archive_20260429 WHERE archive_type='faqs_pre_update';
--
--   -- Restore chatbot_flows welcome
--   UPDATE chatbot_flows cf
--   SET flow_config = (archive.payload->>'flow_config')::jsonb,
--       updated_at  = (archive.payload->>'updated_at')::timestamptz
--   FROM dent_paket_clinic_metadata_archive_20260429 archive
--   WHERE archive.archive_type = 'chatbot_flows_pre_update'
--     AND cf.tenant_id = 18173130 AND cf.flow_id = 29;
--
--   -- Drop columns ONLY if rollback intent is full revert (rare)
--   -- ALTER TABLE tenant_settings DROP COLUMN IF EXISTS clinic_contact;
--   -- ALTER TABLE tenant_settings DROP COLUMN IF EXISTS team_members;
-- COMMIT;
