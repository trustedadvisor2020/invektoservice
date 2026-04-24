# Paket C1 — Dent FAQ Surface Content Bind

> **Slug:** `20260424-dent-paket-c1-faqs-content-bind` | **Risk:** MEDIUM | **Status:** IN_PROGRESS
> **Plan:** [arch/plans/20260424-dent-paket-c1-faqs-content-bind.json](../arch/plans/20260424-dent-paket-c1-faqs-content-bind.json)
> **Migration:** [arch/db/migrations/032-dent-paket-c1-faqs-content-bind.sql](../arch/db/migrations/032-dent-paket-c1-faqs-content-bind.sql)
> **DocX Source:** `DentAdavista/ROADSHOW Aİ AGENT KARŞILAMA MESAJI.docx` (24KB, 13 Nis)
> **JSON Intermediate:** [DentAdavista/seeds/dent-roadshow-content.json](../DentAdavista/seeds/dent-roadshow-content.json)

---

## Scope (Q-Approved Tek-Shot)

C1 = **AI FAQ runtime content bind** (Knowledge `faqs` source-of-truth):
- `faqs` 36 row INSERT (12 intent × 3 A/B/C variant from DocX)
- `chatbot_flows` flow_id=29 message_text_welcome_1 placeholder → DocX welcome-wdate-1
- `faq_entries` 36 [EDIT:*] is_inactive DELETE (legacy cleanup, Q-approved G12 MID)
- POST embeddings hop (`/api/v1/knowledge/18173130/generate-embeddings`)
- S4 smoke 3 FAQ × 3 locale (translation hop verify)

C2 (post-pilot, BACKLOG): welcome templates 12→15 overhaul + persona-rich content + flow rotation_group_tag + template_catalog faq_* refresh.

---

## 5-Surprise Discovery Trail

| # | Iddia (session-memory) | Gerçek prod state | Karar |
|---|------------------------|-------------------|-------|
| s1 | "DocX yok, Claude generate" (G1 first answer) | DocX MEVCUT (24KB Turkish casing path) | G1-revize: DocX parse python-docx 1.2.0 |
| s2 | "46 welcome `[EDIT:*]` placeholder" | template_catalog 48 row ZATEN content + is_active=TRUE | Templates scope dışı (G5+G11 MIN) |
| s3 | "chatbot_flows.flow_config.nodes[].data.text placeholder" | flow_id=29 nodes[1].data.text gerçekten `[EDIT:welcome_with_date_vN]` | Flow node fix dahil (1 node) |
| s4 | "FAQ schema: intent_slug + variant_index + lang" | faq_entries schema: question + answer + keywords[] + sort_order (NO intent_slug); question stores SLUG verbatim | G6: `faqs` UNIQUE(tenant_id, question) suffix '(Variant A/B/C)' disambiguation |
| s5 | "AI FAQ runtime reads faq_entries" | **3 ayrı FAQ surface:** `faqs` (Knowledge pgvector runtime AUTHORITY), `faq_entries` (legacy migration source), `template_catalog faq_*` (FEAT-WTP rotation pool, inactive) | G8: `faqs` 36 INSERT + embeddings (primary target); G12: faq_entries DELETE (cleanup) |

---

## Interview Gates Q-Approved (3 Round, 11 Gate)

### Initial Round (G1-G4)
| Gate | Question | Answer | Revised? |
|------|----------|--------|----------|
| G1 | DocX content source | "Claude generate, Q review" | ✅ REVIZE → DocX parse (after s1) |
| G2 | S4 smoke scope | "Mid 3 FAQ × 3 locale (9 call)" | — |
| G3 | Idempotency anchor | "template_id + faq_id" | ✅ REVIZE → faqs UNIQUE(tenant_id, question) (after s4) |
| G4 | Rollback strategy | "Snapshot + atomic flip" | — |

### Revize Round (G1-revize, G5-G7)
| Gate | Question | Answer |
|------|----------|--------|
| G1-revize | DocX exists, source strategy | "DocX parse, source-of-truth (Recommended)" |
| G5 | Templates scope | "DocX original ile compare, sapma varsa update" → REVIZE in G11 |
| G6 | FAQ question field fix | "Real soru cümlesi yaz (Recommended)" |
| G7 | Flow welcome node | "Generic neutral welcome inline (Recommended)" |

### Extended Round (G8, G11-G13)
| Gate | Question | Answer |
|------|----------|--------|
| G8 | FAQ insert target (5. surprise post) | "`faqs` 36 row INSERT (12 × 3 A/B/C) + embeddings (Recommended)" |
| G11 | Templates revised scope | "MIN: dokunma, sadece flow node welcome inline DocX-1 kopya (Recommended)" |
| G12 | Cleanup scope | "MID: faq_entries 36 row DELETE (legacy noise temizle)" |
| G13 | Paket scope split | "Iki ayri paket C1 + C2" |

---

## Files Diff (Pre-Commit)

| Path | Type | Change |
|------|------|--------|
| `arch/plans/20260424-dent-paket-c1-faqs-content-bind.json` | NEW | Plan JSON, AC1-AC8 |
| `arch/db/migrations/032-dent-paket-c1-faqs-content-bind.sql` | NEW | 220 lines, atomik tx + 5 DO $verify$ |
| `DentAdavista/seeds/dent-roadshow-content.json` | NEW | DocX → JSON intermediate (192 lines) |
| `arch/errors.md` | EDIT | +5 entries INV-SEED-013..017 |
| `tracking/20260424-dent-paket-c1-faqs-content-bind.md` | NEW | Bu dosya |
| `tracking/pilot-launch-roadmap.md` | EDIT | Master Queue Paket C1 row + B-C2 backlog |
| `arch/session-memory.md` | EDIT | Last Update + Recently Completed |
| `arch/lessons-learned.md` | EDIT | +1 multi-surface FAQ drift lesson |

---

## Acceptance Criteria

| # | Criterion | Status | Verification |
|---|-----------|--------|--------------|
| AC1 | DocX parse + JSON intermediate (10+5 welcome + 12×3 FAQ) | ⏳ Done (pre-Codex) | Script stdout: 10/10 + 5/5 + 12 intents |
| AC2 | Migration 032 SQL 5 sections + DO $verify$ 5 postcondition | ⏳ Done (pre-execute) | wc -l 220, header docstring |
| AC3 | errors.md +5 INV-SEED-013..017 | ⏳ Done (pre-Codex) | YAML block syntax preserved |
| AC4 | Migration 032 prod execute + DO $verify$ PASS + embeddings POST 200 + 36 NOT NULL | ⏳ Pending | MCP invekto-postgres + invekto-ops |
| AC5 | S4 smoke 3 FAQ × 3 locale = 9 call PASS | ⏳ Pending | Knowledge `/search` + translation hop |
| AC6 | Pilot config preservation post-execute+smoke | ⏳ Pending | Baseline query batch |
| AC7 | Tracking + roadmap + session-memory + lessons updates | ⏳ In progress | Post-execute finalize |
| AC8 | Codex review iter=0 PASS hedef (CODEX UTANSIN) | ⏳ Pending | /rev MCP Codex |

---

## Codex Iter Arc

(populated after /rev)

---

## Ops Log

(populated during prod execute)

---

## Pilot Config Preserve Baseline (Post-Execute Expected)

| Tablo/State | Pre-C1 | Post-C1 Expected |
|-------------|--------|------------------|
| tenant_registry.plan_tier | kurumsal (Paket A) | kurumsal (preserved) |
| tenant_settings TFM 5 entries | 1 row | 1 row (preserved) |
| tenant_settings campaign_config (MCC seed) | 1 row | 1 row (preserved) |
| video_provider | mock | mock (preserved) |
| template_catalog (Dent) | 48 row, all active | 48 row (preserved, C2 scope) |
| doctors (Dent) | 1 row 'Dr. Dent Adavista' (Paket B) | 1 row (preserved) |
| appointment_slots (Dent) | 4 row, doctor_id=1, is_active=FALSE | 4 row (preserved) |
| **faqs (Dent)** | **0 row** | **36 row, embedding NOT NULL post-hop** |
| **faq_entries (Dent)** | **36 row [EDIT] is_active=FALSE** | **0 row (DELETE cleanup)** |
| **chatbot_flows flow_id=29 welcome text** | **`[EDIT:welcome_with_date_vN]`** | **DocX welcome-wdate-1 (Güneş + Dr. Özge + Dublin/Cork)** |
| tenant_landing_settings | 1 row | 1 row (preserved) |
| **dent_paket_c1_archive_20260424** | **(table absent)** | **table created + 37 row snapshot** |

---

## Roadmap Counter Updates (Post-Execute)

| Metric | Pre-C1 | Post-C1 |
|--------|--------|---------|
| Total Pilot-Critical Packages | 13 | 14 |
| DONE | 13 | 14 |
| IN_PROGRESS | 0 | 0 |
| Backlog Packages | 6 | 7 (B-C2 added) |
| Progress | 100% (13/13) | 100% (14/14) |
