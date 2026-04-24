-- ============================================================
-- Migration 032 — Dent Paket C1: faqs Content Bind + Flow Welcome + Legacy Cleanup
-- ============================================================
-- Purpose: AI FAQ runtime bind for Dent (tenant_id=18173130) — Knowledge `faqs` table is
--          authoritative source for ai_faq node semantic search (pgvector). Currently EMPTY
--          for Dent → S4 pilot smoke would FAIL. This migration:
--          1. Snapshots pre-state (archive table for rollback evidence)
--          2. Inserts 36 FAQ rows (12 intent x 3 A/B/C variant) from ROADSHOW DocX source-of-truth
--          3. Updates chatbot_flows flow_id=29 message_text_welcome_1 placeholder -> DocX welcome-wdate-1
--          4. Deletes legacy faq_entries 36 [EDIT:*] is_inactive rows (Q-approved cleanup)
--          5. DO $verify$ 5 postcondition assertions (INV-SEED-013..017) actionable exception messages
--
-- POST-RUN: Operator must call POST /api/v1/knowledge/18173130/generate-embeddings (Bearer JWT)
--           to populate `embedding` column (vector(3072)) for 36 inserted rows. AI FAQ semantic
--           search hybrid (pgvector + ts_vector) requires embedding. Plan AC4 verifies this hop.
--
-- Risk: MEDIUM (prod DB content INSERT 36 row + flow_config jsonb update + cross-table DELETE
--       + post-migration HTTP embedding hop). Atomik transaction (BEGIN..COMMIT) ensures
--       all-or-nothing — DO $verify$ RAISE EXCEPTION aborts on assertion fail.
--
-- Rollback: dent_paket_c1_archive_20260424 table preserves pre-state row_to_json snapshots
--           for restore: see ROLLBACK section at bottom (commented, manual run).
-- ============================================================

BEGIN;

-- ============================================================
-- §0. PRECONDITION: fail-loud guards (pre-INSERT/UPDATE/DELETE state assertions)
-- ============================================================
-- Codex iter 0 CQ2/Q2 feedback: ON CONFLICT DO NOTHING is silent suppression. Replaced with
-- raw INSERT below (§2) — UNIQUE(tenant_id, question) violation will RAISE EXCEPTION + abort.
-- Plus this precondition block adds explicit pre-state guard so race conditions or stale
-- pre-existing rows are rejected at write time, not via aggregate post-checks.

DO $precondition$
DECLARE
    v_pre_faqs_count INTEGER;
    v_pre_flow_text  TEXT;
BEGIN
    -- P1: faqs MUST be empty for Dent (otherwise INSERT will hit UNIQUE conflict; we want loud failure with context here)
    SELECT COUNT(*) INTO v_pre_faqs_count FROM faqs WHERE tenant_id = 18173130;
    IF v_pre_faqs_count <> 0 THEN
        RAISE EXCEPTION '[INV-SEED-013] Pre-INSERT precondition fail: faqs has % pre-existing rows for tenant_id=18173130 (expected 0). '
            'Root cause: duplicate migration run, parallel insert race, or unexpected manual seed. '
            'Next step: SELECT id, question, embedding IS NULL AS emb_null FROM faqs WHERE tenant_id=18173130 ORDER BY id; '
            'verify state, decide manual cleanup vs preserve before re-running this migration.', v_pre_faqs_count;
    END IF;

    -- P2: chatbot_flows flow_id=29 nodes[1].data.text must still be the [EDIT placeholder
    SELECT flow_config->'nodes'->1->'data'->>'text' INTO v_pre_flow_text
    FROM chatbot_flows WHERE tenant_id = 18173130 AND flow_id = 29;
    IF v_pre_flow_text IS NULL THEN
        RAISE EXCEPTION '[INV-SEED-016] Pre-UPDATE precondition fail: chatbot_flows flow_id=29 not found for tenant_id=18173130 (expected 1 row). '
            'Root cause: post-P9 wiring seed missing or flow row deleted externally. '
            'Next step: SELECT flow_id, flow_name, is_active FROM chatbot_flows WHERE tenant_id=18173130; verify wiring intact before re-running.';
    END IF;
    IF v_pre_flow_text NOT LIKE '%[EDIT:%' THEN
        RAISE EXCEPTION '[INV-SEED-016] Pre-UPDATE precondition fail: chatbot_flows flow_id=29 nodes[1].data.text already updated (does not contain [EDIT placeholder; current value preview: %). '
            'Root cause: duplicate migration run or external manual update. '
            'Next step: SELECT jsonb_pretty(flow_config) FROM chatbot_flows WHERE tenant_id=18173130 AND flow_id=29; verify intended state vs current, decide preserve vs reset.', LEFT(v_pre_flow_text, 60);
    END IF;
END $precondition$;

-- ============================================================
-- §1. Archive pre-state snapshot (rollback evidence)
-- ============================================================
CREATE TABLE IF NOT EXISTS dent_paket_c1_archive_20260424 (
    id            SERIAL PRIMARY KEY,
    archive_type  TEXT NOT NULL,
    payload       JSONB NOT NULL,
    archived_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
COMMENT ON TABLE dent_paket_c1_archive_20260424 IS
    'Paket C1 pre-state snapshots for rollback. Drop after pilot go-live confirmed (post-pilot ops).';

-- Codex iter 1 CQ8/CQ11 fix: GRANT ALL to invekto role per arch/db schema policy
-- (every new table requires GRANT ALL + canonical mirror in arch/db/*.sql).
-- Canonical mirror: arch/db/dent-paket-c1-archive.sql (NEW this paket).
GRANT ALL ON dent_paket_c1_archive_20260424 TO invekto;
GRANT USAGE, SELECT ON SEQUENCE dent_paket_c1_archive_20260424_id_seq TO invekto;

-- Snapshot faq_entries before DELETE
INSERT INTO dent_paket_c1_archive_20260424 (archive_type, payload)
SELECT 'faq_entries_pre_delete', row_to_json(fe.*)::jsonb
FROM faq_entries fe
WHERE fe.tenant_id = 18173130;

-- Snapshot chatbot_flows before UPDATE
INSERT INTO dent_paket_c1_archive_20260424 (archive_type, payload)
SELECT 'chatbot_flows_pre_update', row_to_json(cf.*)::jsonb
FROM chatbot_flows cf
WHERE cf.tenant_id = 18173130 AND cf.flow_id = 29;

-- ============================================================
-- §2. Insert 36 FAQs (12 intent x 3 A/B/C variant) from DocX
-- ============================================================
-- UNIQUE(tenant_id, question) constraint via uq_faqs_tenant_question (arch/db/knowledge.sql:101)
-- -> question text disambiguated with " (Variant A/B/C)" suffix per FEAT-WTP rotation pool
-- intent (top-K hybrid search returns variant diversity).
-- **Codex iter 0 CQ2/Q2 fix:** ON CONFLICT clause REMOVED — raw INSERT now fail-loud on
-- duplicate (UNIQUE violation = SQLSTATE 23505 = transaction abort + rollback). §0
-- precondition guards pre-state explicitly so silent suppression no longer possible.
-- embedding=NULL, populated post-migration via POST /api/v1/knowledge/18173130/generate-embeddings.

INSERT INTO faqs (
    tenant_id, question, answer,
    category, lang, source, source_metadata,
    keywords, embedding, is_active, created_at, updated_at
) VALUES
  (18173130, 'Is it really free? (Variant A)', 'Yes, it really is free. We genuinely believe that anyone considering dental treatment abroad should have the chance to understand the process clearly before making a decision. That’s exactly why we organise these Roadshows — so you can meet our dentist, ask everything openly, and leave with clarity. There’s no fee and no obligation.', 'roadshow', 'en', 'manual', '{"intent_slug": "is_it_free", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['free', 'cost', 'pay', 'charge']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Is it really free? (Variant B)', 'Yes, there’s absolutely no charge to attend. The consultation is free, and you’re not committing to anything. It’s simply an opportunity to get honest, professional information face to face.', 'roadshow', 'en', 'manual', '{"intent_slug": "is_it_free", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['free', 'cost', 'pay', 'charge']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Is it really free? (Variant C)', 'Yes, it’s completely free to attend. You can ask all your questions, discuss your situation, and receive guidance without paying anything.', 'roadshow', 'en', 'manual', '{"intent_slug": "is_it_free", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['free', 'cost', 'pay', 'charge']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Where exactly is it? (Variant A)', 'It will take place in a hotel meeting room in the city centre. Once I confirm your attendance, I’ll send you the full address, time, and location link.', 'roadshow', 'en', 'manual', '{"intent_slug": "location_where", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['where', 'address', 'hotel']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Where exactly is it? (Variant B)', 'We’re hosting it in a central hotel venue for private one-to-one meetings. As soon as your slot is confirmed, I’ll share the exact location details with you.', 'roadshow', 'en', 'manual', '{"intent_slug": "location_where", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['where', 'address', 'hotel']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Where exactly is it? (Variant C)', 'It’s being held in a hotel conference room so we can meet comfortably and privately. I’ll send you the full details right after we schedule your time.', 'roadshow', 'en', 'manual', '{"intent_slug": "location_where", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['where', 'address', 'hotel']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'What happens there? (Variant A)', 'You’ll meet Dr. Özge Yılmazoğlu face to face. If you have an X-ray, you can bring it and review it together. You’ll talk through possible treatment options, timelines, travel arrangements, aftercare — everything from A to Z. It’s really about making sure you fully understand the journey before deciding anything.', 'roadshow', 'en', 'manual', '{"intent_slug": "what_happens", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['what happens', 'agenda']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'What happens there? (Variant B)', 'Think of it as a clarity meeting. We’ll look at your current situation, discuss what might be suitable for you, and explain the full treatment process in Turkey — including how many days you’d need, how it works step by step, and what happens afterwards.', 'roadshow', 'en', 'manual', '{"intent_slug": "what_happens", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['what happens', 'agenda']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'What happens there? (Variant C)', 'It’s a private one-to-one conversation with the dentist. If you have reports or X-rays, we’ll review them. Then you’ll receive a clear explanation of the treatment approach, the process, and what to expect — so there are no surprises later.', 'roadshow', 'en', 'manual', '{"intent_slug": "what_happens", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['what happens', 'agenda']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Will any treatment be done there? (Variant A)', 'No treatment will be carried out during the Roadshow 🙂 It’s purely a consultation and planning session, so you can make an informed decision before travelling.', 'roadshow', 'en', 'manual', '{"intent_slug": "any_treatment", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['treatment there', 'procedure']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Will any treatment be done there? (Variant B)', 'No procedures take place there. It’s just an in-person discussion and evaluation to understand your options clearly.', 'roadshow', 'en', 'manual', '{"intent_slug": "any_treatment", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['treatment there', 'procedure']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Will any treatment be done there? (Variant C)', 'Nothing clinical will be done at the event. It’s simply a chance to meet the dentist and plan properly. Any treatment would take place later at our clinic in Turkey.', 'roadshow', 'en', 'manual', '{"intent_slug": "any_treatment", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['treatment there', 'procedure']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do I need to pay anything after? (Variant A)', 'No, there’s no obligation at all. After the Roadshow, you’ll have all the information you need — and then the decision is entirely yours. We only move forward if you feel comfortable.', 'roadshow', 'en', 'manual', '{"intent_slug": "payment_after", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['obligation', 'commit', 'after']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do I need to pay anything after? (Variant B)', 'You’re not required to commit to anything. The purpose is to give you clarity, not pressure.', 'roadshow', 'en', 'manual', '{"intent_slug": "payment_after", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['obligation', 'commit', 'after']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do I need to pay anything after? (Variant C)', 'No payment is required unless you decide to proceed with treatment later on. And that choice is completely up to you.', 'roadshow', 'en', 'manual', '{"intent_slug": "payment_after", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['obligation', 'commit', 'after']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Should I bring my X-ray? (Variant A)', 'If you have a recent X-ray, that’s very helpful — it allows the dentist to give more detailed guidance. But if you don’t have one, that’s absolutely fine too.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_xray", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['xray', 'x-ray', 'scan']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Should I bring my X-ray? (Variant B)', 'Yes, please bring it if you have one. If not, don’t worry — we can still have a very useful discussion.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_xray", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['xray', 'x-ray', 'scan']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Should I bring my X-ray? (Variant C)', 'An X-ray helps us be more precise, but it’s not mandatory. You’re welcome either way.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_xray", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['xray', 'x-ray', 'scan']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Can I bring someone with me? (Variant A)', 'Of course — you’re very welcome to bring a family member or a friend. Many people do, and it’s completely free for them as well.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_companion", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['friend', 'family', 'bring']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Can I bring someone with me? (Variant B)', 'Yes, absolutely. If it makes you feel more comfortable, please bring someone along.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_companion", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['friend', 'family', 'bring']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Can I bring someone with me? (Variant C)', 'You can definitely bring someone with you. We’re happy to answer their questions too.', 'roadshow', 'en', 'manual', '{"intent_slug": "bring_companion", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['friend', 'family', 'bring']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How long does it take? (Variant A)', 'Appointments usually take around 20–30 minutes. If you have more questions, we can take a little longer — we won’t rush you.', 'roadshow', 'en', 'manual', '{"intent_slug": "duration", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['how long', 'time', 'minutes']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How long does it take? (Variant B)', 'Around half an hour on average, depending on how detailed the discussion is.', 'roadshow', 'en', 'manual', '{"intent_slug": "duration", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['how long', 'time', 'minutes']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How long does it take? (Variant C)', 'It’s typically 20–30 minutes, but we adjust based on your needs.', 'roadshow', 'en', 'manual', '{"intent_slug": "duration", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['how long', 'time', 'minutes']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Why are you doing this in Ireland? (Variant A)', 'That’s a very fair question. We’ve treated many patients from Ireland over the years, and we’ve learned that people really value meeting the dentist before travelling. We believe important health decisions should start with trust and clarity — that’s why we come to meet you in person.', 'roadshow', 'en', 'manual', '{"intent_slug": "why_ireland", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['why ireland', 'why here']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Why are you doing this in Ireland? (Variant B)', 'We know travelling abroad for treatment comes with questions. Instead of asking you to decide from a distance, we prefer to meet face to face and answer everything properly.', 'roadshow', 'en', 'manual', '{"intent_slug": "why_ireland", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['why ireland', 'why here']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Why are you doing this in Ireland? (Variant C)', 'Ireland has been one of the countries where many of our patients come from. Hosting a Roadshow allows us to connect directly, answer concerns honestly, and build confidence before any travel happens.', 'roadshow', 'en', 'manual', '{"intent_slug": "why_ireland", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['why ireland', 'why here']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How much is this treatment? (Variant A)', 'The cost depends on your specific case — the number of teeth involved, the type of treatment, materials used, and your overall oral condition. That’s why we prefer to evaluate properly first, so we can give you accurate information instead of guessing.', 'roadshow', 'en', 'manual', '{"intent_slug": "price_quote", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['price', 'how much', 'cost of']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How much is this treatment? (Variant B)', 'I completely understand wanting to know the price. The exact cost becomes clear once the dentist reviews your situation and outlines the treatment plan. That way, everything is transparent and tailored to you.', 'roadshow', 'en', 'manual', '{"intent_slug": "price_quote", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['price', 'how much', 'cost of']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'How much is this treatment? (Variant C)', 'Because every case is different, we avoid giving random estimates. During the Roadshow, once we understand your needs, we can explain the pricing structure clearly.', 'roadshow', 'en', 'manual', '{"intent_slug": "price_quote", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['price', 'how much', 'cost of']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Is treatment in Turkey safe? (Variant A)', 'That’s a very reasonable question. Healthcare decisions should never feel rushed or uncertain. Dent Adavista Dental Clinic is fully licensed, experienced in treating international patients, and led by Dr. Özge Yılmazoğlu. I’m happy to share our official pages so you can review our work and patient feedback: website https://dentadavista.com/ and Instagram https://www.instagram.com/dentadavistaclinic/ (Facebook: https://www.facebook.com/profile.php?id=61554804412823). And please feel free to ask anything openly.', 'roadshow', 'en', 'manual', '{"intent_slug": "safety_concern", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['safe', 'trust', 'legit']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Is treatment in Turkey safe? (Variant B)', 'Your concern makes sense. Travelling for treatment is a big decision. We operate under strict medical standards and focus strongly on patient safety and long-term results. If you’d like, you can check our official pages: https://dentadavista.com/ and https://www.instagram.com/dentadavistaclinic/ (Facebook: https://www.facebook.com/profile.php?id=61554804412823).', 'roadshow', 'en', 'manual', '{"intent_slug": "safety_concern", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['safe', 'trust', 'legit']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Is treatment in Turkey safe? (Variant C)', 'It’s good that you’re asking this. We always encourage patients to research properly. I can share our clinic details and official channels so you can feel confident: https://dentadavista.com/ and https://www.instagram.com/dentadavistaclinic/ (Facebook: https://www.facebook.com/profile.php?id=61554804412823). If you have any questions, you can also reach us on +90 545 343 09 09.', 'roadshow', 'en', 'manual', '{"intent_slug": "safety_concern", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['safe', 'trust', 'legit']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do you organise hotel and transfers? (Variant A)', 'If you’d like, we can guide you through the whole process — from airport transfers to accommodation options. Some patients prefer to organise everything themselves, others prefer support. We’re flexible. If you want, you can also reach us on +90 545 343 09 09.', 'roadshow', 'en', 'manual', '{"intent_slug": "hotel_transfers", "variant": "A", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['hotel', 'transfer', 'flight']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do you organise hotel and transfers? (Variant B)', 'Yes, we assist with planning if needed. During the Roadshow, we explain how the travel and clinic schedule works step by step.', 'roadshow', 'en', 'manual', '{"intent_slug": "hotel_transfers", "variant": "B", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['hotel', 'transfer', 'flight']::text[], NULL, TRUE, NOW(), NOW()),
  (18173130, 'Do you organise hotel and transfers? (Variant C)', 'We’re happy to help coordinate the process so it feels smooth and stress-free. You can choose the level of support you prefer.', 'roadshow', 'en', 'manual', '{"intent_slug": "hotel_transfers", "variant": "C", "source": "dent_roadshow_docx_2026_04_24", "paket": "C1"}'::jsonb, ARRAY['hotel', 'transfer', 'flight']::text[], NULL, TRUE, NOW(), NOW())
; -- Raw INSERT: UNIQUE(tenant_id, question) violation aborts transaction (Codex iter 0 CQ2/Q2 fix)

-- ============================================================
-- §3. Update chatbot_flows flow_id=29 message_text_welcome_1 placeholder -> DocX welcome-wdate-1
-- ============================================================
-- Q decision G7: generic neutral inline (DocX welcome-wdate-1 verbatim + canonical token rename
-- {Name} -> {lead.name}). FEAT-WTP rotation_group_tag NOT attached (post-C2 paket scope).
-- jsonb_set in-place at $.nodes[1].data.text (5-node flow, index 1 = message_text_welcome_1).
-- Index 1 verified pre-edit via prod query (e1: trigger_start_1 -> message_text_welcome_1 in
-- nodes[1] position; not nodes[0] which is trigger_start_1).

-- Codex iter 2 CQ2 fix: UPDATE wrapped in DO block with GET DIAGNOSTICS ROW_COUNT assertion.
-- Previously bare UPDATE with defensive `LIKE '%[EDIT:%'` WHERE could silently no-op if text
-- was concurrently changed to another non-placeholder value (T0 precondition PASS, T1 external
-- mutation, T2 UPDATE matches 0 rows, V16 placeholder-absence still PASSes — silent success path).
-- Now: UPDATE inside DO block, immediate ROW_COUNT check; 0 rows → fail-loud INV-SEED-016 dual semantic.

DO $update_flow$
DECLARE
    v_updated_rows INTEGER;
BEGIN
    UPDATE chatbot_flows
    SET flow_config = jsonb_set(
            flow_config,
            '{nodes,1,data,text}',
            to_jsonb('Hi {{lead.name}} 😊 How are you?
I’m Güneş from Dent Adavista Dental Clinic (Kuşadası). We’re hosting a free 1-to-1 dental Roadshow with our founder dentist Dr. Özge Yılmazoğlu — Dublin (14 March) and Cork (15 March).
Would you like me to save you a free spot? Dublin or Cork?'::text)
        ),
        updated_at = NOW()
    WHERE tenant_id = 18173130
      AND flow_id = 29
      AND flow_config->'nodes'->1->'data'->>'text' LIKE '%[EDIT:%'; -- defensive: only update if still placeholder

    GET DIAGNOSTICS v_updated_rows = ROW_COUNT;

    IF v_updated_rows <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-016] Flow welcome UPDATE silent no-op: expected 1 row updated, got %. '
            'Root cause: §0 precondition P2 PASSed but UPDATE WHERE clause matched 0 rows post-precondition '
            '(text changed to non-placeholder by external process between precondition check and UPDATE — race condition or external manual update). '
            'Next step: SELECT flow_config->''nodes''->1->''data''->>''text'' FROM chatbot_flows WHERE tenant_id=18173130 AND flow_id=29; '
            'verify current state, decide preserve external change vs reset to DocX welcome-wdate-1.', v_updated_rows;
    END IF;
END $update_flow$;

-- ============================================================
-- §4. Delete legacy faq_entries (36 row [EDIT:*] is_inactive cleanup, Q-approved G12 MID)
-- ============================================================
-- Defensive guards: tenant + is_active=FALSE + answer LIKE '%[EDIT%' triple-filter prevents
-- accidental delete of any future hand-edited Dent FAQ. Migration script
-- arch/db/faq-entries-to-knowledge-migration.sql becomes no-op for Dent post-cleanup
-- (faqs is now authoritative — see §2 above).

DELETE FROM faq_entries
WHERE tenant_id = 18173130
  AND is_active = FALSE
  AND answer LIKE '%[EDIT%';

-- ============================================================
-- §5. DO $verify$ postcondition (INV-SEED-013..017)
-- ============================================================
-- All assertions fail-loud with RAISE EXCEPTION (transaction abort + rollback). Each message
-- carries Root cause + Next step guidance per Codex iter 0 CQ12 lesson (Paket B precedent).

DO $verify$
DECLARE
    v_faqs_count          INTEGER;
    v_faqs_null_embedding INTEGER;
    v_faq_entries_count   INTEGER;
    v_flow_text           TEXT;
    v_archive_count       INTEGER;
BEGIN
    -- V13: Dent faqs row count = 36
    SELECT COUNT(*) INTO v_faqs_count FROM faqs WHERE tenant_id = 18173130;
    IF v_faqs_count <> 36 THEN
        RAISE EXCEPTION '[INV-SEED-013] Dent faqs count mismatch: expected 36 rows, got %. '
            'Root cause: INSERT step §2 hit UNIQUE(tenant_id, question) conflict (duplicate run?) '
            'or transaction abort masked partial insert. '
            'Next step: SELECT id, question FROM faqs WHERE tenant_id=18173130 ORDER BY id; check duplicates + re-run after manual reconciliation.', v_faqs_count;
    END IF;

    -- V14: Dent faqs all embedding IS NULL (will be populated post-migration via HTTP endpoint)
    SELECT COUNT(*) INTO v_faqs_null_embedding FROM faqs
        WHERE tenant_id = 18173130 AND embedding IS NULL;
    IF v_faqs_null_embedding <> 36 THEN
        RAISE EXCEPTION '[INV-SEED-014] Dent faqs embedding state mismatch: expected 36 NULL embeddings, got %. '
            'Root cause: pre-existing rows with embeddings (UNIQUE conflict skipped INSERT) or unexpected ALTER default. '
            'Next step: SELECT id, question, embedding IS NULL AS emb_null FROM faqs WHERE tenant_id=18173130; if pre-existing rows have embeddings, decide overwrite vs preserve.', v_faqs_null_embedding;
    END IF;

    -- V15: Dent faq_entries cleanup target set fully drained (symmetric with §4 defensive DELETE filter)
    -- Codex iter 0 CQ8 fix: assertion now checks ONLY the rows that §4 DELETE targeted
    -- (is_active=FALSE + [EDIT placeholder), not all faq_entries. Active/manual rows that survived
    -- the defensive guard are intentionally preserved by design (Q-approved G12 MID cleanup scope).
    SELECT COUNT(*) INTO v_faq_entries_count FROM faq_entries
        WHERE tenant_id = 18173130
          AND is_active = FALSE
          AND answer LIKE '%[EDIT%';
    IF v_faq_entries_count <> 0 THEN
        RAISE EXCEPTION '[INV-SEED-015] Dent faq_entries placeholder cleanup target set incomplete: expected 0 [EDIT placeholder is_inactive rows, got %. '
            'Root cause: §4 DELETE statement failed silently (transaction state unexpected, lock contention, or post-snapshot insert race re-introduced placeholder rows). '
            'Next step: SELECT id, is_active, LEFT(answer, 50) FROM faq_entries WHERE tenant_id=18173130 AND is_active=FALSE AND answer LIKE %%[EDIT%%; verify what survived + manual cleanup decision.', v_faq_entries_count;
    END IF;

    -- V16: chatbot_flows welcome node text no longer contains [EDIT placeholder
    SELECT flow_config->'nodes'->1->'data'->>'text' INTO v_flow_text
    FROM chatbot_flows WHERE tenant_id = 18173130 AND flow_id = 29;
    IF v_flow_text IS NULL OR v_flow_text LIKE '%[EDIT:%' THEN
        RAISE EXCEPTION '[INV-SEED-016] Dent chatbot_flows welcome node placeholder still present (text starts with: %). '
            'Root cause: §3 UPDATE WHERE clause excluded the row (text already changed?) or jsonb_set path mismatch (nodes[1] is not message_text_welcome_1?). '
            'Next step: SELECT jsonb_pretty(flow_config) FROM chatbot_flows WHERE tenant_id=18173130 AND flow_id=29; verify node order + manually patch jsonb_set path.', LEFT(COALESCE(v_flow_text, '(NULL)'), 60);
    END IF;

    -- V17: archive table populated (rollback evidence preserved)
    SELECT COUNT(*) INTO v_archive_count FROM dent_paket_c1_archive_20260424;
    IF v_archive_count < 37 THEN -- 36 faq_entries + 1 chatbot_flows
        RAISE EXCEPTION '[INV-SEED-017] Archive snapshot incomplete: expected >=37 rows (36 faq_entries + 1 chatbot_flows), got %. '
            'Root cause: §1 INSERT INTO ... SELECT failed (no source rows? table missing?). '
            'Next step: SELECT archive_type, COUNT(*) FROM dent_paket_c1_archive_20260424 GROUP BY archive_type; verify both snapshot types present + rollback evidence sufficient before COMMIT.', v_archive_count;
    END IF;

    RAISE NOTICE '[DENT-PAKET-C1] postcondition verify PASS: faqs=% (all embedding NULL=%), faq_entries_placeholder_target_drained=0 (was % pre-DELETE; active/manual rows preserved by defensive filter), flow_welcome_no_placeholder=true, archive_rows=% (rollback evidence preserved). Next ops: POST /api/v1/knowledge/18173130/generate-embeddings to populate pgvector embeddings.',
        v_faqs_count, v_faqs_null_embedding, (SELECT COUNT(*) FROM dent_paket_c1_archive_20260424 WHERE archive_type='faq_entries_pre_delete'), v_archive_count;
END $verify$;

COMMIT;

-- ============================================================
-- ROLLBACK PROCEDURE (manual, post-COMMIT) — uncomment + run via MCP if needed
-- ============================================================
-- BEGIN;
-- -- Restore faq_entries from archive
-- INSERT INTO faq_entries (tenant_id, question, answer, keywords, is_active, sort_order, created_at, updated_at)
-- SELECT (payload->>'tenant_id')::INTEGER, payload->>'question', payload->>'answer',
--        ARRAY(SELECT jsonb_array_elements_text(payload->'keywords'))::text[],
--        (payload->>'is_active')::BOOLEAN, (payload->>'sort_order')::INTEGER,
--        (payload->>'created_at')::TIMESTAMPTZ, (payload->>'updated_at')::TIMESTAMPTZ
-- FROM dent_paket_c1_archive_20260424 WHERE archive_type = 'faq_entries_pre_delete';
-- -- Restore chatbot_flows (overwrite with pre-state flow_config)
-- UPDATE chatbot_flows SET flow_config = (SELECT payload->'flow_config' FROM dent_paket_c1_archive_20260424 WHERE archive_type = 'chatbot_flows_pre_update' LIMIT 1)::jsonb
-- WHERE tenant_id = 18173130 AND flow_id = 29;
-- -- Drop new faqs
-- DELETE FROM faqs WHERE tenant_id = 18173130 AND source_metadata->>'paket' = 'C1';
-- COMMIT;
-- ============================================================
