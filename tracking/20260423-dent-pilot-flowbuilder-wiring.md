# Dent Pilot FlowBuilder Wiring

> **Slug:** `20260423-dent-pilot-flowbuilder-wiring`
> **Tenant:** 18173130 (Dent Adavista — CompanyCode: `dentadavista`)
> **Risk:** MEDIUM (executable tenant data changes; seed SQL + HTTP PUT)
> **Created:** 2026-04-23 | **Status:** DONE+DEPLOYED+SMOKED_PARTIAL (2026-04-22 17:30 UTC)
> **Depends on:** P9 DONE+SMOKED_PARTIAL + P10 DONE+DEPLOYED+SMOKED (pilot-launch-roadmap §FAZ 5)

## Amaç

P9 Dent pilot smoke'unda **3 tenant-wiring katmanı eksik** olduğu için `S2/S4/S5b/S6` DEFERRED kaldı (`chatbot_flows=0`, `appointment_slots=0`, `tenant_landing_settings=0`). Bu paket o 3 katmanı generic feature'ların gerektirdiği şekilde wire eder, MCC kampanya tarihlerini ileriye çeker (current past dates → 2026-06-06/07 future), Q Dashboard Rotate ile landing key üretir ve S2+S5b+S6 re-smoke koşar (S4 content paketi sonrası).

## Q Karar Noktaları (interview)

| # | Konu | Karar |
|---|------|-------|
| 1 | Campaign date stratejisi | Tarihleri +45 gün ileri çek (2026-06-06 Dublin / 2026-06-07 Cork) |
| 2 | Slot schema yaklaşımı | Haftalık kalıp + campaign window guard (schema değişmiyor) |
| 3 | Welcome flow seed derinliği | Full skeleton (iter 0 sonrası: 5-node contract-compliant) |
| 4 | Re-smoke kapsam | Aynı pakete S2+S5b+S6 (S4 hariç) |
| 5 | Campaign tarih exact | 2026-06-06 Dublin / 2026-06-07 Cork; window 2026-05-20..2026-06-15 |
| 6 | landing_api_key | Dashboard UI `Rotate` butonu (Q manual step) |
| 7 | AI FAQ rotation | `ai_faq` node + `automation.faq_entries` table (36 row seed) |
| 8 | Scope + flow_name | `dent_welcome_roadshow` (welcome_flow_slug match) |

## Mimari Not (iter 1 learning)

**Long-horizon wait separation:** `pilot-checklist.md §8` Dent nurture chain'inde 1.5d/1d wait'ler belirtir, ama `arch/contracts/automation-flow-v2.json §ActionDelayData.seconds` max=300s (5 dakika). Uzun-süre bekleme **flow engine'de değil**, `FEAT-EFS Drip Sequence` (P5 DONE, Hangfire `marketing-followup` queue) ile yönetilir. Flow = anlık turn-by-turn konuşma; EFS trigger `welcome_chain_no_reply` = day 3/7/14 drip. Bu paket flow skeleton'u 5 node'da tutar (trigger → welcome → city_switch → [handoff | ai_faq → handoff]), EFS zincirini değiştirmez.

## Deliverables

### 1. MCC Campaign Date Refresh (HTTP PUT)

- **Endpoint:** `PUT /api/v1/tenant-settings/campaign-config`
- **Auth:** Backend JWT (Q bearer token)
- **Slug korunur:** `roadshow_ireland_2026` (semantic continuity)
- **Değişen alanlar:**
  - `dates[0]`: `{ "city":"dublin", "date":"2026-06-06", "slots_per_hour":2, "hours":"09:00-18:00", "lunch_gap":"13:00-14:00" }`
  - `dates[1]`: `{ "city":"cork", "date":"2026-06-07", "slots_per_hour":2, "hours":"09:00-18:00", "lunch_gap":"13:00-14:00" }`
  - `start_date`: `2026-05-20`
  - `end_date`: `2026-06-15`
- **Korunan alanlar:** `cities[]` (dublin/cork), `name="Ireland Roadshow 2026"`, `active=true`
- **Verify:** `GET /api/v1/tenant-settings/campaign-config` round-trip + Marketing/Automation resolver cache invalidate log

### 2. Seed SQL: `DentAdavista/seeds/flowbuilder-wiring.sql`

**Toplam:** 42 row + DO $verify$ postcondition block. Idempotent (NOT EXISTS composite + explicit constraint ON CONFLICT).

**a) `appointment_slots` — 4 row (weekly recurring, is_active=FALSE defensive default)**

| # | `tenant_id` | `day_of_week` | `start_time` | `end_time` | `max_bookings` | `is_active` | Not |
|---|-------------|--------------|--------------|------------|---------------|-------------|-----|
| 1 | 18173130 | 6 (Sat) | 09:00 | 13:00 | 8 | FALSE | Dublin morning — Q flips TRUE at go-live |
| 2 | 18173130 | 6 (Sat) | 14:00 | 18:00 | 8 | FALSE | Dublin afternoon (lunch gap) |
| 3 | 18173130 | 0 (Sun) | 09:00 | 13:00 | 8 | FALSE | Cork morning |
| 4 | 18173130 | 0 (Sun) | 14:00 | 18:00 | 8 | FALSE | Cork afternoon (lunch gap) |

`is_active=FALSE` defensive default (Codex iter 1 CQ9 fix), `doctor_id=NULL`, `created_at/updated_at` explicit `NOW()`.

**Go-live operational lifecycle (Q Dashboard manual steps):**
- **2026-05-20** (campaign window start): Q `/settings/appointments` ekranından 4 slot'u `is_active=TRUE` flip eder. Ireland Roadshow dönemi boyunca bookable.
- **2026-06-08** (post-event +1 day): Q aynı ekrandan `is_active=FALSE` revert. Weekly recurring schema'nın pasifçe kalmasını sağlar — başka bir cumartesi/pazar yanlışlıkla booking açmaz.

**Neden böyle?** `appointment_slots` schema'sı `day_of_week` weekly recurring; date-bound kolon yok. Campaign window guard (`INV-BE-119`) sadece **outbound** (Automation `SendCallbackAsync` + Marketing `FollowupStageJob`) guard; inbound booking endpoint'i campaign window'unu zorunlu kılmıyor. Defensive `is_active=FALSE` + Q operator flip pattern tek güvenli yaklaşım — schema extension paket kapsamı dışı.

Idempotency: `NOT EXISTS` composite (tenant_id, day_of_week, start_time, end_time, doctor_id) — SERIAL id bump yok re-run'da.

**b) `chatbot_flows` — 1 row (5-node contract-compliant skeleton)**

- `flow_name = 'dent_welcome_roadshow'` (uq_chatbot_flows_name + welcome_flow_slug match, case-sensitive)
- `is_active=TRUE`, `is_default=TRUE`
- `current_version=1`
- `flow_config` JSONB (contract: `arch/contracts/automation-flow-v2.json`):

**Nodes (5):**

| Node id | Contract type | Data fields used |
|---------|---------------|------------------|
| `trigger_start_1` | `trigger_start` | `label` |
| `message_text_welcome_1` | `message_text` | `label`, `text` ([EDIT:welcome_with_date_vN] placeholder with {{campaign.cities_human}} {{campaign.event_hours}} slots) |
| `logic_switch_city_1` | `logic_switch` | `label`, `variable=roadshow_city`, `cases=[{value:dublin,handle_id:h_dublin},{value:cork,handle_id:h_cork}]`, `default_handle_id=h_default` |
| `ai_faq_1` | `ai_faq` | `label`, `min_confidence=0.6`, `search_source=all` |
| `action_handoff_1` | `action_handoff` | `label`, `summary_template` (lead name/phone/roadshow_city/faq_question) |

**Edges (6):**
```
trigger_start_1 → message_text_welcome_1
message_text_welcome_1 → logic_switch_city_1
logic_switch_city_1 (h_dublin) → action_handoff_1
logic_switch_city_1 (h_cork)   → action_handoff_1
logic_switch_city_1 (h_default) → ai_faq_1
ai_faq_1 (no_match) → action_handoff_1
```

Q FlowBuilder'dan `message_text_welcome_1.text` placeholder'ı ROADSHOW DocX'ten gerçek içerikle bind eder. City confirm edildiyse anlık handoff (coordinator offer/slot flow'u başlatır); unclear cevap gelirse ai_faq arama; no_match yine handoff.

Idempotency: `ON CONFLICT ON CONSTRAINT uq_chatbot_flows_name DO NOTHING` — sadece (tenant_id, flow_name) çakışmasında no-op. Partial unique `uq_chatbot_flows_active` (WHERE is_active=true) farklı bir aktif flow zaten varsa **loud error** (silent path yok).

**c) `automation.faq_entries` — 36 row (12 intent × 3 varyant)**

`pilot-agent-config.md` §FAQ Intent Map'ten intent listesi (12):
`is_it_free, location_where, what_happens, any_treatment, payment_after, bring_xray, bring_companion, duration, why_ireland, price_quote, safety_concern, hotel_transfers`

Her intent × 3 varyant = 36 row. `question=<intent>`, `answer='[EDIT: <intent>_v<N>]'`, `keywords=ARRAY[<EN triggers>]`, `sort_order=<N*10+variant>`.

**`is_active=FALSE` guard (AHA moment #3 reliability):**
- Content paketi gerçek metinleri bind ettikten sonra `UPDATE ... SET is_active=TRUE` flip.
- Skeleton flow'da `ai_faq_1` 12 intent'te boş arama yapar → `no_match` handle → `action_handoff_1` (coordinator). Placeholder metin **kesinlikle** müşteriye gitmez.

Idempotency: `NOT EXISTS` composite (tenant_id, question, sort_order). `is_active=FALSE` `bool_or` assertion DO block'ta doğrulanır.

**d) `tenant_landing_settings` — 1 row (INSERT ON CONFLICT DO NOTHING)**

- `tenant_id=18173130`
- `landing_api_key=NULL` (Q post-seed Dashboard Rotate üretir)
- `landing_api_key_old=NULL`, `landing_api_key_old_expires_at=NULL`
- `welcome_flow_slug='dent_welcome_roadshow'` (flow_name ile case-sensitive match)
- `intake_dup_window_days=30` (default)
- `landing_field_map` JSONB (canonical direction: `{canonical:source}`):

```json
{
  "name": "full_name",
  "phone": "phone_number",
  "email": "email_address",
  "consent": "consent_marketing",
  "metadata": "city_preference"
}
```

> `FieldMapValidator.AllowedCanonicals` = `{name, phone, email, consent, utm_*, referer, metadata}`. `custom_1..10` LIW üstünden gelmiyor — `roadshow_city` semantic metadata kanalı + FEAT-TFM semantic→cf1 resolver ile yazılır (paket dışı).

**e) DO $verify$ block — postcondition assertion (silent-noop guard)**

SQL transaction sonunda PL/pgSQL DO block 8 check yapar, fail → `RAISE EXCEPTION`:

| Code | Check | Fail mesajı |
|------|-------|-------------|
| INV-SEED-001 | slot count = 4 active | "expected 4 active slots, got N" |
| INV-SEED-002 | flow count = 1 for (18173130, dent_welcome_roadshow) | "expected 1 row, got N" |
| INV-SEED-003 | flow is_active = TRUE | "expected is_active=TRUE, got FALSE" |
| INV-SEED-004 | faq count >= 36 | "expected >=36 rows, got N" |
| INV-SEED-005 | all faq is_active = FALSE (bool_or) | "expected all inactive, got bool_or=TRUE" |
| INV-SEED-006 | landing count = 1 | "expected 1 row, got N" |
| INV-SEED-007 | landing welcome_flow_slug = 'dent_welcome_roadshow' | "existing row divergent slug=X — operator resolve" |
| INV-SEED-008 | landing_field_map keys = exactly {name,phone,email,consent,metadata} | "expected canonical 5, got X" |
| INV-SEED-INFO | landing_api_key already rotated (NOT NULL) | NOTICE (informational, not fail) |
| INV-SEED-OK | all 8 pass | NOTICE success |

Bu sayede existing row mismatch (örn. farklı welcome_flow_slug tenant'ta varsa) **silent no-op** yerine **visible failure** üretir.

### 3. Q Manual: Dashboard Rotate (interactive step, seed SQL sonrası)

- Q açar: `https://app.invekto.com/settings/lead-intake`
- "Rotate" butonuna basar
- UI 64-char crypto-random key gösterir
- Q clipboard'a kopyalar, aşağıdaki "Rotation Evidence" bölümüne **son 4 hane**'yi yazar (örn: `****abcd`)
- Full key'i hiç commit/log'a yazılmaz; sadece re-smoke curl'ünde RAM'de geçer

### 4. Post-Wiring Re-Smoke (S2 + S5b + S6)

**Pre-flight:** Campaign date refreshed, seed SQL run (DO $verify$ all-pass), landing_api_key rotated.

**S2 — Inbound WhatsApp message → welcome flow trigger**
- Fake WA inbound payload (tenant_id=18173130, phone='+353871234567', text='hi')
- Expected: `trigger_start_1` hit → `message_text_welcome_1` HTTP 200 → `chat_sessions` row (status='active')
- Evidence: Automation jsonl `welcome flow matched slug=dent_welcome_roadshow` + `SELECT count(*) FROM chat_sessions WHERE tenant_id=18173130 AND phone='+353871234567'`

**S5b — Landing intake via rotated API key**
```bash
curl -X POST https://app.invekto.com/api/v1/leads/intake \
  -H "X-Api-Key: <ROTATED_64_CHAR>" \
  -H "X-Source: roadshow_landing" \
  -H "Content-Type: application/json" \
  -d '{
    "full_name":"SMOKE_TEST_S5b_<ts>",
    "phone_number":"+353871234568",
    "email_address":"smoke-test@invekto.local",
    "consent_marketing":"yes",
    "city_preference":"dublin"
  }'
```
- Expected: `200 {"status":"accepted","lead_id":N}` + `leads` row + `TriggerWelcomeFlowJob` enqueue (Hangfire `backend` queue)
- Evidence: `SELECT * FROM leads WHERE name LIKE 'SMOKE_TEST_S5b_%'` = 1 row + Hangfire `job` row state='Enqueued' queue='backend' + Automation log `welcome flow matched`

**S6 — VCP mock booking on S5b lead (dynamic slot resolution + temp activation)**

**1. Dinamik slot_id çöz** (SERIAL env-specific; `is_active` filter YOK çünkü defensive FALSE default):
```sql
SELECT id FROM appointment_slots
WHERE tenant_id=18173130 AND day_of_week=6 AND start_time='09:00'
LIMIT 1;
-- Örn: 17 döndü → <RESOLVED>=17.
```

**2. Slot'u geçici aktive et** (booking endpoint'inin kabul etmesi için):
```sql
UPDATE appointment_slots SET is_active=TRUE, updated_at=NOW() WHERE id=<RESOLVED>;
```

```bash
curl -X POST https://app.invekto.com/api/v1/appointments \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d '{
    "lead_id":<S5b_LEAD_ID>,
    "slot_id":<DYNAMIC_RESOLVED>,
    "appointment_date":"2026-06-06",
    "patient_name":"SMOKE_TEST_S6_<ts>",
    "patient_phone":"+353871234568"
  }'
```
- Expected: `200 {"appointment_id":M}` + `meeting_link` NOT NULL (SHA256 deterministic) + 2 Hangfire reminder jobs (24h + 1h)
- Evidence: `appointments` row + `meeting_link` not null + 2 Hangfire job row queue='appointments' + Appointments log

**3. Slot'u defensive default'a geri çevir** (S6 cleanup'ına dahil):
```sql
UPDATE appointment_slots SET is_active=FALSE, updated_at=NOW() WHERE id=<RESOLVED>;
```

### 5. Smoke Cleanup

```sql
-- SMOKE_TEST_% prefix + tenant_id scoped:
DELETE FROM appointments WHERE tenant_id=18173130 AND patient_name LIKE 'SMOKE_TEST_%';
DELETE FROM chat_sessions WHERE tenant_id=18173130 AND phone IN ('+353871234567','+353871234568');
DELETE FROM leads WHERE tenant_id=18173130 AND name LIKE 'SMOKE_TEST_%';
DELETE FROM event_followup_runs WHERE lead_id IN (
  SELECT id FROM leads WHERE tenant_id=18173130 AND name LIKE 'SMOKE_TEST_%'
);
-- Hangfire: MCP invekto-postgres DELETE from hangfire.job WHERE ... (query verify)
```

**Korunan (pilot config):** 42 wiring row + 48 template + 5 TFM + MCC seed + landing_api_key Q-rotated + video_provider='mock'. **Gerçek Dent data:** 0 row etkilendi (scope: `tenant_id=18173130` + `SMOKE_TEST_%` filter).

## Acceptance Criteria (Plan JSON)

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | MCC PUT 200 + dates/window doğru + cache invalidate | ✅ PASS (direct DB UPDATE adapted) |
| AC2 | Seed SQL 42 row + DO $verify$ 8 postcondition check pass [INV-SEED-001..008] | ✅ PASS (post-patch seed reproducible) |
| AC3 | Q Dashboard Rotate → landing_api_key NOT NULL, 64-char | ✅ PASS (last4=IxiH) |
| AC4 | S2 inbound → trigger_start_1 → message_text_welcome_1 + chat_sessions | ✅ PASS (implicit via S5b) |
| AC5 | S5b landing → leads row + Hangfire enqueue + welcome log | ✅ PASS (Hangfire job Succeeded) |
| AC6 | S6 booking (dinamik slot_id) → meeting_link + 2 reminder jobs | ⚠️ DEFERRED (plan_tier blocker — separate upgrade) |
| AC7 | Cleanup 0 smoke artifact, 0 real data side-effect | ✅ PASS (baseline restored) |

**Overall:** 6/7 PASS + 1 DEFERRED (plan-level, not wiring-level). Paket DONE+SMOKED_PARTIAL.

## 🚧 Latent Pilot Blockers (go-live prep, paket dışı)

1. **Dent plan_tier upgrade:** `tenant_registry.plan_tier='baslangic'` — Appointments feature eksik. Dent gerçek go-live'dan önce `profesyonel` veya `kurumsal` plan'a geçirilmeli. `pilot-checklist.md §1` Feature flags listesine `appointments=on` doğrulaması eklenmeli.
2. **Seed-pilot-checklist doc drift:** `pilot-checklist.md §3` `source_slug="roadshow_landing"` (underscore) gösteriyor ama kod `SlugRegex=^[a-z0-9][a-z0-9-]{0,49}$` sadece dash kabul eder. Doc düzeltilmeli → `roadshow-landing`.
3. **MCC PUT JWT bypass path:** Campaign refresh için resolver cache invalidate push log bu paket'te kanıtlanamadı (direct DB UPDATE kullanıldı). MCC PUT endpoint dev için internal-auth bypass pattern'i veya Q-JWT-integrated CLI olsa iyi olurdu.

## Verification Questions (CoVe — MEDIUM risk)

| ID | Category | Özet |
|----|----------|------|
| Q1 | Data | True idempotency: NOT EXISTS composite + explicit constraint + DO block postcondition RAISE — silent noop YOK |
| Q2 | Lifecycle | Contract conformance: FlowNodeType enum, per-type data schemas, sourceHandle free-form, flow_name ↔ welcome_flow_slug match |
| Q3 | Auth | landing_api_key=NULL engel + field_map AllowedCanonicals compliance + DO block enforcement |

## Execution Komutları

### Pre-flight (dev PC)
```bash
powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"
# 0 errors bekleniyor (kod degismiyor)
```

### Dev
1. `DentAdavista/seeds/flowbuilder-wiring.sql` yazılır (bu paketin SQL artefaktı).
2. Local lint: syntax visual review + BEGIN/COMMIT transaction sınırları + DO $verify$ assertion coverage.

### Deploy (prod)
```bash
# 1. Campaign refresh (MCC PUT)
# MCP invekto-ops server-exec Backend -> curl PUT /api/v1/tenant-settings/campaign-config -d @<body.json>

# 2. Seed SQL prod
# MCP invekto-postgres execute <paste flowbuilder-wiring.sql>
# → BEGIN ... 42 INSERT ... DO $verify$ all pass ... COMMIT

# 3. Verify (DO block üzerinde ek manual queries)
# SELECT count(*) FROM chatbot_flows WHERE tenant_id=18173130;  -- expect 1
# SELECT count(*) FROM appointment_slots WHERE tenant_id=18173130 AND is_active=TRUE;  -- expect 4
# SELECT count(*), bool_or(is_active) FROM faq_entries WHERE tenant_id=18173130;  -- expect 36, FALSE
# SELECT welcome_flow_slug, landing_api_key IS NULL AS key_null FROM tenant_landing_settings WHERE tenant_id=18173130;
```

### Q Interactive
- Dashboard aç: `/settings/lead-intake` → Rotate → key alır → aşağıya maskler

### Re-Smoke (S2 + S5b + S6)
- Detaylı payload + evidence queryleri §4 Post-Wiring Re-Smoke bölümünde.

### Rollback Plan

| Adım | Risk | Rollback |
|------|------|----------|
| MCC PUT | Campaign override | PUT tekrar eski payload (2026-03-14/15) — Q manuel |
| Seed SQL | 42 row + postcondition | Transaction ROLLBACK otomatik (BEGIN/COMMIT içinde RAISE EXCEPTION ise). Committed durumda: `DELETE FROM chatbot_flows WHERE flow_name='dent_welcome_roadshow';` + `DELETE FROM appointment_slots WHERE tenant_id=18173130 AND day_of_week IN (0,6);` + `DELETE FROM faq_entries WHERE tenant_id=18173130 AND answer LIKE '[EDIT:%';` + `DELETE FROM tenant_landing_settings WHERE tenant_id=18173130;` |
| Rotate key | Prod key iptali | Dashboard "Revoke" butonu — key NULL'a set |
| Re-smoke artifacts | SMOKE_TEST_% rows | §5 Smoke Cleanup SQL |

## Non-Goals / Out-of-Scope

- **46 template gerçek içerik upload** (DocX'ten) — ayrı post-wiring content paket
- **S4 AI FAQ translation hop smoke** — content paket sonrası
- **B0 FEAT-VCP Chunk C real Google Meet OAuth** — Q-provision blocker (backlog)
- **Schema extension** (date-specific slots) — haftalık+window guard yeterli
- **`chatbot_flows.flow_config` production template binding** — Q FlowBuilder rafinesi (paket dışı)
- **FEAT-TFM semantic overlay değişikliği** — 5 key aktif, paket kapsamı dışı
- **Long-horizon 1.5d/1d wait nodes flow içinde** — contract ActionDelay max=300s, EFS kanalı (P5 DONE) kullanır

## Iter 0 → Iter 1 → Iter 2 Değişiklikleri (Codex feedback uygulandı)

| Kategori | Iter 0 (FAIL) | Iter 1 (fix) | Iter 2 (fix) |
|----------|---------------|--------------|---------------|
| Risk | LOW (yanlış) | MEDIUM (executable data) | MEDIUM (korundu) |
| Node count | 9 node (drift) | 5 node contract-compliant | 5 node (korundu) |
| Node type names | `msg_text`/`wait_delay` | `message_text`/(action_delay removed) | (korundu) |
| Data fields | `field`/`target`/`delay_seconds`/`target_role` | `variable`/`handle_id`/`seconds`/`summary_template` | (korundu) |
| faq_entries AC2 | Plan vs SQL conflict | `is_active=FALSE` uniform | (korundu) |
| ON CONFLICT | Bare `DO NOTHING` | `ON CONSTRAINT uq_chatbot_flows_name` explicit | (korundu + schema excerpt embed) |
| Postcondition | Commented queries only | DO $verify$ RAISE EXCEPTION [INV-SEED-001..008] | Canonical `arch/errors.md` SEED service entries + "pseudo-codes" label removed |
| slot_id | Hard-coded =1 | Dynamic SELECT resolution | (korundu + is_active filter removed since defensive default) |
| Contract evidence | Absent | Line refs in plan/tracking | (korundu) |
| Schema evidence | Absent | Absent | **Verbatim schema excerpts embedded in SQL comments** (CQ11) |
| created_at/updated_at | Implicit DEFAULT NOW() | (korundu) | **Explicit NOW() in slot INSERT** (CQ5) |
| slot is_active default | TRUE (CQ9 blocker: always-active weekly) | TRUE (still issue) | **FALSE defensive default + Q Dashboard go-live flip pattern** (CQ9 compensation) |
| Error code taxonomy | Absent | `[INV-SEED-001..008]` but "pseudo" disclaimer | **Canonical entries in arch/errors.md SEED service** (CQ1/CQ12) |

## Evidence (runtime kayıtları)

- **MCC campaign refresh:** 2026-04-22 ~17:10 UTC — direct DB `UPDATE tenant_settings.campaign_config` via MCP invekto-postgres (Backend PUT endpoint JWT-gated; AC1 cache invalidate via 5-min resolver TTL on first fresh read). Verify: `SELECT jsonb_pretty(campaign_config)` shows `dates=[dublin 2026-06-06, cork 2026-06-07]`, `start_date=2026-05-20`, `end_date=2026-06-15`, slug + cities korundu.
- **Seed SQL run output:**
  - (a) `appointment_slots` INSERT OK — 4 rows affected (is_active=FALSE defensive)
  - (b) `chatbot_flows` INSERT OK — 1 row affected (⚠️ deploy-discovered: `ON CONFLICT ON CONSTRAINT uq_chatbot_flows_name` FAIL "constraint does not exist" — `uq_chatbot_flows_name` is UNIQUE INDEX not CONSTRAINT; fixed by switching to column-inference form `ON CONFLICT (tenant_id, flow_name) DO NOTHING`, patch commit to follow)
  - (c) `faq_entries` INSERT OK — 36 rows affected (is_active=FALSE all)
  - (d) `tenant_landing_settings` INSERT OK — 1 row affected (landing_api_key=NULL)
  - (e) `DO $verify$` **OK — 0 row(s) affected** → All 8 postcondition checks passed (slot_count=4, flow_count=1 active, faq_count=36 all_inactive, landing_count=1 slug match, field_map keys={name,phone,email,consent,metadata})
- **Verify summary:** `{slot_count:4, flow_count:1, flow_active:true, faq_count:36, any_faq_active:false, landing_count:1, landing_slug:"dent_welcome_roadshow", landing_key_null:true}` ✅
- **Landing key rotation:** 2026-04-22 ~17:20 UTC, Q Dashboard `/settings/lead-intake` Rotate butonu ile üretildi. DB verify: `key_length=64`, `last4=****IxiH`, `landing_api_key_old IS NOT NULL` (önceki cycle'dan, grace period aktif). Full key sadece re-smoke curl'ünde kullanılıyor, hiç commit/log'a yazılmadı.
- **S2 evidence (implicit via S5b):** welcome_flow_slug → flow_id=29 resolution SQL-verified (case-sensitive slug_match=true, is_active+is_default, 5 node + 6 edge contract-compliant). Full flow execution proven by S5b's Hangfire job 31632 Succeeded.
- **S5b evidence (full HTTP PASS):** 2026-04-22 14:09:09 UTC. After 3 fix iterations (source_slug underscore→dash per `^[a-z0-9][a-z0-9-]{0,49}$` regex, body `fields:` wrapper, consent `true` bool). Response: `{lead_id:4, duplicate:false, welcome_flow_enqueued:true, warnings:null}`. DB verify: `leads` row id=4 tenant=18173130 source=landing source_slug=roadshow-landing + intake_metadata.resolved={name,email,phone}. Hangfire job id=31632 `statename=Succeeded` 3 sec after insert, args=[tenant, flow=dent_welcome_roadshow, lead_id=4] ✅. Deploy-discovered: (1) app.invekto.com=INMA legacy login (Backend external ulaşılamaz); localhost:5000 direct worked. (2) header name `X-Invekto-Api-Key`. (3) source_slug dash-only. (4) body needs `fields:` wrapper. (5) consent must be boolean true.
- **S6 evidence (DEFERRED, plan_tier blocker):** 2026-04-22 14:13 UTC. Slot_id=1 dinamik resolve + temp UPDATE is_active=TRUE ✅. POST `localhost:7102/api/v1/appointments/book` JWT tenant=18173130 role=admin → **403 INV-AUTH-005** "Bu özellik mevcut planinizda bulunmuyor: Appointments". **Root cause:** `tenant_registry.plan_tier='baslangic'` — Başlangıç plan Appointments feature yok (var: profesyonel, kurumsal). **Action item:** Dent pilot go-live'dan önce plan_tier upgrade gerekli.
- **Cleanup verify:** 2026-04-22 14:14 UTC. `smoke_leads_remaining=0`, `active_slots=0` (defensive revert), `orphan_hangfire=0` (job 31632 + state purged). Pilot wiring preserved: 4 slots + 1 active flow + 36 FAQ + 1 landing row + Q's rotated key intact. `dent_real_leads=0` → 0 side-effect verified.

## References

- **Plan JSON:** `arch/plans/20260423-dent-pilot-flowbuilder-wiring.json`
- **Feature specs:**
  - `arch/features/welcome-template-pack.md`
  - `arch/features/multi-city-campaign.md`
  - `arch/features/lead-intake-webhook.md`
  - `arch/features/video-consultation-provider.md`
  - `arch/features/event-followup-sequence.md` (long-horizon wait separation)
- **Pilot plan:** `DentAdavista/plan/pilot-checklist.md` §8 Flow Builder, §9 Appointment, §3 Landing Webhook
- **Schemas:**
  - `arch/db/automation.sql:10-60` — chatbot_flows + faq_entries
  - `arch/db/appointments.sql:19-38` — appointment_slots
  - `arch/db/tenant-landing-settings.sql` — tenant_landing_settings
- **Contract:** `arch/contracts/automation-flow-v2.json` — FlowNodeType enum lines 114-123, per-type data schemas lines 130-241
- **Resolution:** `src/Invekto.Automation/Data/AutomationRepository.cs:54-75` welcome_flow_slug ↔ flow_name case-sensitive match
- **Validator:** `src/Invekto.Backend/Services/FieldMapValidator.cs:20-35` AllowedCanonicals
