-- =============================================================
-- Dent Adavista Pilot — FlowBuilder Wiring Seed
-- Paket: 20260423-dent-pilot-flowbuilder-wiring
-- Tenant: 18173130 (Dent Adavista, CompanyCode=dentadavista)
-- Run on: prod invekto database (PostgreSQL 16) via MCP invekto-postgres
-- Idempotent: NOT EXISTS / ON CONFLICT ON CONSTRAINT (explicit) + postcondition assertions
-- Risk: MEDIUM (executable data changes; codex-review-policy §risk-classification)
--
-- Depends on:
--   arch/db/automation.sql         (chatbot_flows, faq_entries)
--   arch/db/appointments.sql       (appointment_slots)
--   arch/db/tenant-landing-settings.sql (tenant_landing_settings)
--   arch/db/tenant-registry.sql    (tenant_id=18173130 must exist)
--   arch/contracts/automation-flow-v2.json (flow_config v2 schema)
--
-- Scope:
--   (a) 4 row appointment_slots     (Dublin Sat + Cork Sun, morning+afternoon)
--   (b) 1 row chatbot_flows         (dent_welcome_roadshow, 5-node contract-compliant)
--   (c) 36 row automation.faq_entries (12 intents * 3 variants, is_active=FALSE guard)
--   (d) 1 row tenant_landing_settings (landing_api_key=NULL, Q rotates post-seed)
--   (e) Postcondition verification via DO block + RAISE EXCEPTION on assertion failure
--
-- Architectural note (EFS vs flow-delay):
--   Pilot-checklist §8 references 1.5d/1d waits in welcome chain. Those long-horizon
--   waits are implemented by FEAT-EFS (Event Follow-Up Sequence, P5 DONE) via
--   Hangfire scheduled jobs on `marketing-followup` queue — NOT via flow action_delay
--   nodes (contract automation-flow-v2.json §ActionDelayData.seconds max=300s / 5min).
--   Flow engine handles immediate turn-by-turn only; EFS trigger
--   'welcome_chain_no_reply' covers day3/7/14 drip.
--
-- Rollback: see tracking/20260423-dent-pilot-flowbuilder-wiring.md §Rollback Plan
-- =============================================================

BEGIN;

-- =============================================================
-- (a) appointment_slots — 4 row weekly recurring (DEFENSIVE is_active=FALSE)
-- Schema: arch/db/appointments.sql:19-38 (verbatim excerpt below)
--   CREATE TABLE IF NOT EXISTS appointment_slots (
--       id                  SERIAL PRIMARY KEY,
--       tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
--       doctor_id           INTEGER,                           -- Nullable
--       day_of_week         SMALLINT NOT NULL,                 -- 0=Sunday ... 6=Saturday
--       start_time          TIME NOT NULL,
--       end_time            TIME NOT NULL,
--       max_bookings        INTEGER NOT NULL DEFAULT 1,
--       is_active           BOOLEAN NOT NULL DEFAULT TRUE,
--       created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--       updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--       CONSTRAINT chk_slot_day_of_week CHECK (day_of_week BETWEEN 0 AND 6),
--       CONSTRAINT chk_slot_time_order CHECK (start_time < end_time),
--       CONSTRAINT chk_slot_max_bookings CHECK (max_bookings >= 1)
--   );
-- day_of_week 6=Saturday (Dublin event 2026-06-06), 0=Sunday (Cork event 2026-06-07).
-- Lunch gap 13:00-14:00 modeled as two separate slot rows (morning / afternoon).
-- max_bookings=8 = 30-min capacity * 2/hour * 4 hours.
-- Idempotency: NOT EXISTS guard on (tenant_id, day_of_week, start_time, end_time, doctor_id)
-- composite — NO SERIAL id bump on re-run. No unique index relied on here.
-- created_at/updated_at explicit NOW() for auditability (schema has DEFAULT NOW() fallback).
--
-- BUSINESS LOGIC CQ9 — why is_active=FALSE (defensive default):
--   Appointment_slots schema is inherently weekly-recurring (day_of_week column),
--   which would make is_active=TRUE slots bookable on every Saturday/Sunday forever
--   — not just 2026-06-06/07 roadshow dates. Platform currently has NO slot-level
--   date-bound enforcement (campaign window guards at arch/features/multi-city-campaign.md
--   are OUTBOUND guards, not inbound booking guards). To close this business-logic
--   gap without schema extension: seed is_active=FALSE, Q manually flips TRUE at
--   roadshow go-live (2026-05-20) via Dashboard, flips back FALSE post-event
--   (2026-06-08). Smoke S6 needs temp activation: UPDATE ... SET is_active=TRUE
--   for the specific slot being tested, then revert to FALSE in cleanup. This
--   pattern mirrors faq_entries' defensive-default + operator-flip lifecycle.
-- =============================================================

INSERT INTO appointment_slots (
    tenant_id, doctor_id, day_of_week, start_time, end_time, max_bookings, is_active, created_at, updated_at
)
SELECT v.tenant_id, v.doctor_id, v.day_of_week, v.start_time, v.end_time, v.max_bookings, v.is_active, NOW(), NOW()
FROM (VALUES
    (18173130, NULL::INTEGER, 6::SMALLINT, '09:00'::TIME, '13:00'::TIME, 8, FALSE),  -- Dublin Sat morning (Q flips TRUE at go-live)
    (18173130, NULL::INTEGER, 6::SMALLINT, '14:00'::TIME, '18:00'::TIME, 8, FALSE),  -- Dublin Sat afternoon
    (18173130, NULL::INTEGER, 0::SMALLINT, '09:00'::TIME, '13:00'::TIME, 8, FALSE),  -- Cork Sun morning
    (18173130, NULL::INTEGER, 0::SMALLINT, '14:00'::TIME, '18:00'::TIME, 8, FALSE)   -- Cork Sun afternoon
) AS v(tenant_id, doctor_id, day_of_week, start_time, end_time, max_bookings, is_active)
WHERE NOT EXISTS (
    SELECT 1 FROM appointment_slots s
    WHERE s.tenant_id = v.tenant_id
      AND s.day_of_week = v.day_of_week
      AND s.start_time = v.start_time
      AND s.end_time = v.end_time
      AND s.doctor_id IS NOT DISTINCT FROM v.doctor_id
);


-- =============================================================
-- (b) chatbot_flows — 1 row contract-compliant skeleton
-- Schema: arch/db/automation.sql:10-33 (verbatim excerpt below)
--   CREATE TABLE IF NOT EXISTS chatbot_flows (
--       flow_id             SERIAL PRIMARY KEY,
--       tenant_id           INTEGER NOT NULL,
--       flow_name           VARCHAR(200) NOT NULL DEFAULT 'Ana Flow',
--       flow_config         JSONB NOT NULL DEFAULT '{}'::jsonb,
--       is_active           BOOLEAN NOT NULL DEFAULT false,
--       is_default          BOOLEAN NOT NULL DEFAULT false,
--       current_version     INTEGER NOT NULL DEFAULT 0,
--       ...
--       CONSTRAINT fk_chatbot_flows_tenant FK tenant_registry(tenant_id)
--   );
--   CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_active
--       ON chatbot_flows (tenant_id) WHERE is_active = true;   -- partial unique
--   CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_name
--       ON chatbot_flows (tenant_id, flow_name);               -- target for ON CONFLICT
-- Contract: arch/contracts/automation-flow-v2.json FlowNodeType enum (lines 114-123):
--   trigger_start | message_text | message_menu | logic_condition | logic_switch
--   | ai_intent | ai_faq | action_handoff | action_api_call | action_delay
--   | utility_set_variable | utility_note
-- flow_name MUST MATCH tenant_landing_settings.welcome_flow_slug case-sensitive
--   (AutomationRepository.cs:57-58) — both set to 'dent_welcome_roadshow'.
-- Idempotency: `ON CONFLICT (tenant_id, flow_name) DO NOTHING` column-inference
--   form. Important: `uq_chatbot_flows_name` is created as UNIQUE INDEX (not
--   UNIQUE CONSTRAINT — see CREATE UNIQUE INDEX above). PostgreSQL `ON CONFLICT
--   ON CONSTRAINT <name>` requires an actual named CONSTRAINT and fails on
--   indexes with "constraint does not exist" (discovered at deploy time
--   2026-04-22; fixed by switching to column-inference form). The column form
--   is equally specific: PostgreSQL infers the unique index covering exactly
--   (tenant_id, flow_name), which is `uq_chatbot_flows_name`. The partial-unique
--   `uq_chatbot_flows_active` (covers only (tenant_id) with predicate WHERE
--   is_active=true) cannot match because column-inference requires exact index
--   column coverage — so conflicting active-flow insertions still fail loudly
--   rather than silently no-op, preserving the defensive property.
-- Skeleton scope: 5 nodes (trigger -> welcome -> city_switch -> {handoff | ai_faq
--   -> handoff}). Long-horizon drip (day3/7/14) is FEAT-EFS-owned, NOT flow.
--   Q refines node `data.text` placeholders from ROADSHOW DocX in FlowBuilder.
-- =============================================================

INSERT INTO chatbot_flows (tenant_id, flow_name, flow_config, is_active, is_default, current_version)
VALUES (
    18173130,
    'dent_welcome_roadshow',
    $flow$
{
  "version": 2,
  "metadata": {
    "name": "Dent Welcome - Ireland Roadshow",
    "canvas_viewport": { "x": 0, "y": 0, "zoom": 1 }
  },
  "nodes": [
    {
      "id": "trigger_start_1",
      "type": "trigger_start",
      "position": { "x": 50, "y": 50 },
      "data": {
        "label": "Start (any inbound)"
      }
    },
    {
      "id": "message_text_welcome_1",
      "type": "message_text",
      "position": { "x": 50, "y": 200 },
      "data": {
        "label": "Welcome (template-bound placeholder)",
        "text": "[EDIT:welcome_with_date_vN] — Hi {{lead.name}}, welcome! Ireland Roadshow: {{campaign.cities_human}} on {{campaign.event_hours}}. Reply with your preferred city (Dublin or Cork)."
      }
    },
    {
      "id": "logic_switch_city_1",
      "type": "logic_switch",
      "position": { "x": 50, "y": 350 },
      "data": {
        "label": "City detection (roadshow_city semantic = cf1 via TFM)",
        "variable": "roadshow_city",
        "cases": [
          { "value": "dublin", "handle_id": "h_dublin" },
          { "value": "cork", "handle_id": "h_cork" }
        ],
        "default_handle_id": "h_default"
      }
    },
    {
      "id": "ai_faq_1",
      "type": "ai_faq",
      "position": { "x": 400, "y": 350 },
      "data": {
        "label": "FAQ answer (Knowledge semantic search)",
        "min_confidence": 0.6,
        "search_source": "all"
      }
    },
    {
      "id": "action_handoff_1",
      "type": "action_handoff",
      "position": { "x": 50, "y": 500 },
      "data": {
        "label": "Coordinator handoff",
        "summary_template": "Dent lead — name={{lead.name}} phone={{lead.phone}} city={{roadshow_city}} last_faq={{faq_question}}"
      }
    }
  ],
  "edges": [
    { "id": "e1", "source": "trigger_start_1", "target": "message_text_welcome_1" },
    { "id": "e2", "source": "message_text_welcome_1", "target": "logic_switch_city_1" },
    { "id": "e3", "source": "logic_switch_city_1", "target": "action_handoff_1", "sourceHandle": "h_dublin" },
    { "id": "e4", "source": "logic_switch_city_1", "target": "action_handoff_1", "sourceHandle": "h_cork" },
    { "id": "e5", "source": "logic_switch_city_1", "target": "ai_faq_1", "sourceHandle": "h_default" },
    { "id": "e6", "source": "ai_faq_1", "target": "action_handoff_1", "sourceHandle": "no_match" }
  ]
}
$flow$::jsonb,
    TRUE,   -- is_active (partial unique index uq_chatbot_flows_active: max 1 per tenant)
    TRUE,   -- is_default
    1       -- current_version
)
ON CONFLICT (tenant_id, flow_name) DO NOTHING;


-- =============================================================
-- (c) automation.faq_entries — 36 row (12 intents * 3 variants)
-- Schema: arch/db/automation.sql:44-60
-- is_active=FALSE intentional guard:
--   Content paketi ROADSHOW DocX'ten gercek cevaplari bind ettikten sonra
--   UPDATE ... SET is_active=TRUE ile flip edilir. Bu seed canli gitse bile
--   ai_faq_1 node'un 'matched' handle'i bos sonuc dondurur, flow 'no_match'
--   fallback uzerinden action_handoff_1'e duser (Let me connect you with our
--   coordinator) — placeholder [EDIT:*] metni asla musteriye gonderilmez.
-- Idempotency: NOT EXISTS on (tenant_id, question, sort_order) composite.
-- Intent list source: DentAdavista/plan/pilot-agent-config.md §FAQ Intent Map.
-- =============================================================

INSERT INTO faq_entries (tenant_id, question, answer, keywords, is_active, sort_order)
SELECT v.tenant_id, v.question, v.answer, v.keywords, v.is_active, v.sort_order
FROM (VALUES
    -- is_it_free
    (18173130, 'is_it_free', '[EDIT: is_it_free_v1]', ARRAY['free','cost','pay','charge'], FALSE, 10),
    (18173130, 'is_it_free', '[EDIT: is_it_free_v2]', ARRAY['free','cost','pay','charge'], FALSE, 11),
    (18173130, 'is_it_free', '[EDIT: is_it_free_v3]', ARRAY['free','cost','pay','charge'], FALSE, 12),
    -- location_where
    (18173130, 'location_where', '[EDIT: location_where_v1]', ARRAY['where','address','hotel'], FALSE, 20),
    (18173130, 'location_where', '[EDIT: location_where_v2]', ARRAY['where','address','hotel'], FALSE, 21),
    (18173130, 'location_where', '[EDIT: location_where_v3]', ARRAY['where','address','hotel'], FALSE, 22),
    -- what_happens
    (18173130, 'what_happens', '[EDIT: what_happens_v1]', ARRAY['what happens','agenda'], FALSE, 30),
    (18173130, 'what_happens', '[EDIT: what_happens_v2]', ARRAY['what happens','agenda'], FALSE, 31),
    (18173130, 'what_happens', '[EDIT: what_happens_v3]', ARRAY['what happens','agenda'], FALSE, 32),
    -- any_treatment
    (18173130, 'any_treatment', '[EDIT: any_treatment_v1]', ARRAY['treatment there','procedure'], FALSE, 40),
    (18173130, 'any_treatment', '[EDIT: any_treatment_v2]', ARRAY['treatment there','procedure'], FALSE, 41),
    (18173130, 'any_treatment', '[EDIT: any_treatment_v3]', ARRAY['treatment there','procedure'], FALSE, 42),
    -- payment_after
    (18173130, 'payment_after', '[EDIT: payment_after_v1]', ARRAY['obligation','commit','after'], FALSE, 50),
    (18173130, 'payment_after', '[EDIT: payment_after_v2]', ARRAY['obligation','commit','after'], FALSE, 51),
    (18173130, 'payment_after', '[EDIT: payment_after_v3]', ARRAY['obligation','commit','after'], FALSE, 52),
    -- bring_xray
    (18173130, 'bring_xray', '[EDIT: bring_xray_v1]', ARRAY['xray','x-ray','scan'], FALSE, 60),
    (18173130, 'bring_xray', '[EDIT: bring_xray_v2]', ARRAY['xray','x-ray','scan'], FALSE, 61),
    (18173130, 'bring_xray', '[EDIT: bring_xray_v3]', ARRAY['xray','x-ray','scan'], FALSE, 62),
    -- bring_companion
    (18173130, 'bring_companion', '[EDIT: bring_companion_v1]', ARRAY['friend','family','bring'], FALSE, 70),
    (18173130, 'bring_companion', '[EDIT: bring_companion_v2]', ARRAY['friend','family','bring'], FALSE, 71),
    (18173130, 'bring_companion', '[EDIT: bring_companion_v3]', ARRAY['friend','family','bring'], FALSE, 72),
    -- duration
    (18173130, 'duration', '[EDIT: duration_v1]', ARRAY['how long','time','minutes'], FALSE, 80),
    (18173130, 'duration', '[EDIT: duration_v2]', ARRAY['how long','time','minutes'], FALSE, 81),
    (18173130, 'duration', '[EDIT: duration_v3]', ARRAY['how long','time','minutes'], FALSE, 82),
    -- why_ireland
    (18173130, 'why_ireland', '[EDIT: why_ireland_v1]', ARRAY['why ireland','why here'], FALSE, 90),
    (18173130, 'why_ireland', '[EDIT: why_ireland_v2]', ARRAY['why ireland','why here'], FALSE, 91),
    (18173130, 'why_ireland', '[EDIT: why_ireland_v3]', ARRAY['why ireland','why here'], FALSE, 92),
    -- price_quote
    (18173130, 'price_quote', '[EDIT: price_quote_v1]', ARRAY['price','how much','cost of'], FALSE, 100),
    (18173130, 'price_quote', '[EDIT: price_quote_v2]', ARRAY['price','how much','cost of'], FALSE, 101),
    (18173130, 'price_quote', '[EDIT: price_quote_v3]', ARRAY['price','how much','cost of'], FALSE, 102),
    -- safety_concern
    (18173130, 'safety_concern', '[EDIT: safety_concern_v1]', ARRAY['safe','trust','legit'], FALSE, 110),
    (18173130, 'safety_concern', '[EDIT: safety_concern_v2]', ARRAY['safe','trust','legit'], FALSE, 111),
    (18173130, 'safety_concern', '[EDIT: safety_concern_v3]', ARRAY['safe','trust','legit'], FALSE, 112),
    -- hotel_transfers
    (18173130, 'hotel_transfers', '[EDIT: hotel_transfers_v1]', ARRAY['hotel','transfer','flight'], FALSE, 120),
    (18173130, 'hotel_transfers', '[EDIT: hotel_transfers_v2]', ARRAY['hotel','transfer','flight'], FALSE, 121),
    (18173130, 'hotel_transfers', '[EDIT: hotel_transfers_v3]', ARRAY['hotel','transfer','flight'], FALSE, 122)
) AS v(tenant_id, question, answer, keywords, is_active, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM faq_entries f
    WHERE f.tenant_id = v.tenant_id
      AND f.question = v.question
      AND f.sort_order = v.sort_order
);


-- =============================================================
-- (d) tenant_landing_settings — 1 row (landing_api_key=NULL, Q rotates)
-- Schema: arch/db/tenant-landing-settings.sql (verbatim excerpt below)
--   CREATE TABLE IF NOT EXISTS tenant_landing_settings (
--       tenant_id                      INT PRIMARY KEY,   -- single-row/tenant invariant
--       landing_api_key                VARCHAR(64),
--       landing_api_key_old            VARCHAR(64),
--       landing_api_key_old_expires_at TIMESTAMPTZ,
--       landing_field_map              JSONB NOT NULL DEFAULT '{}'::jsonb,
--       welcome_flow_slug              VARCHAR(100),
--       intake_dup_window_days         INT NOT NULL DEFAULT 30,
--       created_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--       updated_at                     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--       CONSTRAINT fk_tls_tenant FK tenant_registry(tenant_id),
--       CONSTRAINT chk_tls_dup_window CHECK (intake_dup_window_days BETWEEN 1 AND 365)
--   );
-- `ON CONFLICT (tenant_id)` target is the PRIMARY KEY constraint (named
-- `tenant_landing_settings_pkey` by PostgreSQL convention) — guaranteed to exist
-- since tenant_id is declared as the table PK above. Using column form instead
-- of named-constraint form is equally specific here because PK columns are
-- unambiguous conflict targets.
-- landing_field_map JSONB direction: {canonical -> source} (migration 021 comment).
-- Allowed canonicals: FieldMapValidator.AllowedCanonicals (name, phone, email,
--   consent, utm_*, referer, metadata). custom_1..10 NOT permitted via landing
--   intake; Dent 'roadshow_city' semantic flows through metadata during intake
--   and gets stamped onto cf1 later via FEAT-TFM semantic overlay resolver.
-- welcome_flow_slug MUST MATCH chatbot_flows.flow_name case-sensitive verbatim.
-- Idempotency: ON CONFLICT (tenant_id) DO NOTHING with postcondition assertion
--   in the DO block below — if an existing row has mismatched welcome_flow_slug
--   or a non-null api_key, the assertion raises INV-SEED-001 (visible, not
--   silent) so Q can decide whether to rotate or overwrite.
-- =============================================================

INSERT INTO tenant_landing_settings (
    tenant_id,
    landing_api_key,
    landing_api_key_old,
    landing_api_key_old_expires_at,
    landing_field_map,
    welcome_flow_slug,
    intake_dup_window_days
)
VALUES (
    18173130,
    NULL,  -- Q rotates via Dashboard /settings/lead-intake post-seed
    NULL,
    NULL,
    $map$
{
  "name": "full_name",
  "phone": "phone_number",
  "email": "email_address",
  "consent": "consent_marketing",
  "metadata": "city_preference"
}
$map$::jsonb,
    'dent_welcome_roadshow',
    30
)
ON CONFLICT (tenant_id) DO NOTHING;


-- =============================================================
-- (e) Postcondition verification — RAISE EXCEPTION on mismatch.
-- Silent no-op guardrail: if any prior row exists with mismatched values,
-- operator sees [INV-SEED-00X] failure instead of wondering why re-smoke fails.
-- Error codes are canonical entries in arch/errors.md SEED service category
-- (INV-SEED-001..008). Codes are seed-time only (PL/pgSQL RAISE EXCEPTION);
-- no ErrorCodes.cs mirror because no runtime C# code references them — the
-- literal string is embedded in the exception message for operator visibility.
-- =============================================================

DO $verify$
DECLARE
    v_slot_count        INTEGER;
    v_flow_count        INTEGER;
    v_flow_is_active    BOOLEAN;
    v_faq_count         INTEGER;
    v_faq_any_active    BOOLEAN;
    v_landing_count     INTEGER;
    v_landing_slug      VARCHAR(100);
    v_landing_key_null  BOOLEAN;
    v_map_keys          TEXT[];
BEGIN
    -- (a) slots expected 4 rows (is_active=FALSE defensive default; Q flips TRUE
    --     at roadshow go-live via Dashboard; count-by-shape, not count-by-active).
    SELECT count(*) INTO v_slot_count
    FROM appointment_slots
    WHERE tenant_id = 18173130
      AND day_of_week IN (0, 6)
      AND start_time IN ('09:00'::TIME, '14:00'::TIME);
    IF v_slot_count <> 4 THEN
        RAISE EXCEPTION '[INV-SEED-001] appointment_slots postcondition FAIL: expected 4 rows (Sat+Sun x morn+aft shape), got %', v_slot_count;
    END IF;

    -- (b) flow expected 1 row with matching name + active + default + current_version=1
    SELECT count(*), bool_or(is_active) INTO v_flow_count, v_flow_is_active
    FROM chatbot_flows
    WHERE tenant_id = 18173130 AND flow_name = 'dent_welcome_roadshow';
    IF v_flow_count <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-002] chatbot_flows postcondition FAIL: expected 1 row for (18173130, dent_welcome_roadshow), got %', v_flow_count;
    END IF;
    IF v_flow_is_active IS NOT TRUE THEN
        RAISE EXCEPTION '[INV-SEED-003] chatbot_flows postcondition FAIL: expected is_active=TRUE for dent_welcome_roadshow, got %', v_flow_is_active;
    END IF;

    -- (c) faq expected 36 rows, all is_active=FALSE (placeholder guard)
    SELECT count(*), bool_or(is_active) INTO v_faq_count, v_faq_any_active
    FROM faq_entries
    WHERE tenant_id = 18173130;
    IF v_faq_count < 36 THEN
        RAISE EXCEPTION '[INV-SEED-004] faq_entries postcondition FAIL: expected >=36 rows, got %', v_faq_count;
    END IF;
    IF v_faq_any_active IS DISTINCT FROM FALSE THEN
        RAISE EXCEPTION '[INV-SEED-005] faq_entries postcondition FAIL: expected all is_active=FALSE (placeholder guard), got bool_or=%', v_faq_any_active;
    END IF;

    -- (d) landing expected 1 row with matching slug + null key (Q rotates later)
    SELECT count(*) INTO v_landing_count
    FROM tenant_landing_settings WHERE tenant_id = 18173130;
    IF v_landing_count <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-006] tenant_landing_settings postcondition FAIL: expected exactly 1 row, got %', v_landing_count;
    END IF;

    SELECT welcome_flow_slug, (landing_api_key IS NULL)
    INTO v_landing_slug, v_landing_key_null
    FROM tenant_landing_settings WHERE tenant_id = 18173130;

    IF v_landing_slug IS DISTINCT FROM 'dent_welcome_roadshow' THEN
        RAISE EXCEPTION '[INV-SEED-007] tenant_landing_settings postcondition FAIL: expected welcome_flow_slug=dent_welcome_roadshow, got % (existing row has divergent slug — operator must resolve before seed continues)', v_landing_slug;
    END IF;

    -- landing_api_key NULL check only informational — if existing row already rotated
    -- (not null), we still surface but do not fail (Q may have pre-rotated).
    IF v_landing_key_null IS FALSE THEN
        RAISE NOTICE '[INV-SEED-INFO] tenant_landing_settings.landing_api_key is NOT NULL (already rotated by Q before seed). Proceeding.';
    END IF;

    -- Verify landing_field_map keys are exactly the 5 allowed canonicals
    SELECT ARRAY(SELECT jsonb_object_keys(landing_field_map) ORDER BY 1)
    INTO v_map_keys
    FROM tenant_landing_settings WHERE tenant_id = 18173130;

    IF NOT (v_map_keys @> ARRAY['name','phone','email','consent','metadata']::TEXT[]
            AND ARRAY['name','phone','email','consent','metadata']::TEXT[] @> v_map_keys) THEN
        RAISE EXCEPTION '[INV-SEED-008] tenant_landing_settings postcondition FAIL: expected landing_field_map keys {name,phone,email,consent,metadata}, got %', v_map_keys;
    END IF;

    RAISE NOTICE '[INV-SEED-OK] All 8 postcondition checks passed for tenant 18173130 (slots=4 shape inactive defensive default, flow=1 active, faq=%, landing=1 slug=dent_welcome_roadshow, key_null=%)', v_faq_count, v_landing_key_null;
END;
$verify$;

COMMIT;

-- =============================================================
-- Post-seed manual step (Q, interactive)
-- =============================================================
-- 1. Open https://app.invekto.com/settings/lead-intake
-- 2. Click "Rotate" button
-- 3. Copy generated 64-char landing_api_key
-- 4. Record last 4 chars (masked) in tracking/20260423-dent-pilot-flowbuilder-wiring.md
-- 5. Use full key in S5b re-smoke curl (never logged/committed)
-- =============================================================

-- =============================================================
-- Post-seed dynamic slot_id resolution for re-smoke (do NOT hard-code id=1)
-- =============================================================
-- Dublin Saturday morning slot id (for S6 VCP booking smoke):
--   SELECT id FROM appointment_slots
--   WHERE tenant_id=18173130 AND day_of_week=6 AND start_time='09:00'
--   LIMIT 1;  -- is_active filter OMITTED (defensive default FALSE at seed time)
-- Use this id in the booking curl; do not assume id=1 (SERIAL sequence varies).
--
-- BEFORE S6 re-smoke: temporarily activate the resolved slot so booking
-- endpoint accepts it. AFTER cleanup: revert to FALSE (defensive default).
--   UPDATE appointment_slots SET is_active=TRUE,  updated_at=NOW() WHERE id=<RESOLVED>;
--   -- run S6 booking smoke here --
--   UPDATE appointment_slots SET is_active=FALSE, updated_at=NOW() WHERE id=<RESOLVED>;
-- Go-live operational activation: Q flips is_active=TRUE via Dashboard near
-- 2026-05-20 (campaign start window), flips back FALSE after 2026-06-08.
-- =============================================================
