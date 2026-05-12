# FEAT-INMA-PIPELINE-V2 Chunk 5 — Dashboard Cleanup + Dent Pilot Doc Refresh

> **Slug:** `20260513-feat-inma-pipeline-v2-c5-dashboard-cleanup` | **Risk:** MEDIUM (Migration 049 + 1 yeni archive table) | **Status:** DONE+EXECUTED+VERIFIED 2026-05-12 22:00 UTC (Q FORCE PASS iter 4)
> **Created:** 2026-05-12 20:56 UTC | **Owner:** Q (trustedadvisor)
> **Parent feature:** [FEAT-INMA-PIPELINE-V2](README.md#feat-inma-pipeline-v2) (5 chunk)
> **Predecessor:** [C1 Zoho-out](feat-inma-pipeline-v2-c1.md) DONE+DEPLOYED+SMOKED 2026-05-12 17:50 UTC (commit `0c0733b`)
> **Memory:** [project_inma_pipeline_v2_decision](../../../Users/taner/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md)

## Bağımsız Chunk Notu

C5 bağımsız: INMA contract beklemiyor (C2/C3/C4 BLOCKED). Sadece doc + DB-content refresh paketi — **kod sıfır**, **deploy yok**, **service restart yok**.

Faz B Master Queue pozisyonu: C1 (DONE) → **C5 (bu paket, PENDING)** → C2/C3/C4 (BLOCKED INMA contract).

---

## Interview Gate Kararları (4/4 Q-onaylı, 2026-05-12 20:56)

| Gate | Soru | Karar |
|------|------|-------|
| G1 | Dent pilot dokümanları cleanup stratejisi | **DELETE + INMA-otorite yeni section** — Zoho bölümleri sil + 'Customer Status Akışı (V2)' INMA agent UI manuel dropdown + INMA→INSE webhook akışı section ekle |
| G2 | Kanban kart body_markdown (6 kart C003/C004/D007/D019/D027/K012) strateji | **Migration 049 atomik UPDATE** — tek transaction 6 kart body rewrite + DO $verify$ INV-SEED-039..045 fail-loud postcondition + archive snapshot |
| G3 | Lessons-learned + memory cleanup scope | **Sadece Zoho-bound olanlara DEPRECATED tag** — evrensel Codex CQ / migration / state pattern'leri DOKUNULMAZ, purely Zoho lesson'lar (Blueprint quirks, OAuth scope mismatch, ZohoConnectionPage helper dedup gibi) DEPRECATED 2026-05-12 inline tag. Memory zoho_blueprint_api_quirks.md frontmatter status='deprecated_historical' + V2 cross-link |
| G4 | Codex review strategy | **Single submission** — MEDIUM risk (Migration 049 + 1 yeni archive table; iter 0 FAIL CQ5 sonrası LOW→MEDIUM upgrade), kod sıfır; C1 chunked review MCP server bug ışığında daha güvenli; tek codex_review submission tüm değişiklikleri kapsar |

---

## Zoho Footprint Envanteri (Pre-C5)

Explore subagent scan 2026-05-12 20:56 (case-insensitive grep `zoho`):

| Kaynak | Mention | Status | Notlar |
|--------|---------|--------|--------|
| DentAdavista/plan/dent-golive.html | 52 | **C5 SCOPE** | Zoho CRM Blueprint 7-stage config (C.1-C.7), OAuth scope, region, transition map, Zoho Sync Log, smoke test spec |
| DentAdavista/plan/pilot-stage1-prep.md | 35 | **C5 SCOPE** | Zoho Blueprint sync, stage mapping, sync_log trigger, LeadStatusEventMap, ZohoLifecycleDispatcher integration, 45 min setup |
| DentAdavista/plan/decisions.md | 3 | **C5 SCOPE** | Blueprint Sync ownership (INMA'ya hizalanır), post-pilot async FEAT-PIPELINE 3-way |
| DentAdavista/plan/pilot-field-mapping.md | 1 | **C5 SCOPE** | pipeline_status → Zoho Blueprint sync (INSE native kolon) |
| arch/docs/whatisinvekto.md | 1 | **C5 SCOPE** | "Zoho entegrasyonu" tek mention (feature list) |
| arch/features/lead-intake-webhook.md | 2 | **C5 SCOPE** | Webhook → Zoho lead creation + COQL query snippet |
| arch/lessons-learned.md | 48 | **C5 PARTIAL** | Zoho-bound entry'ler (Blueprint quirks, OAuth scope mismatch, helper dedup) DEPRECATED tag; evrensel pattern'ler DOKUNULMAZ |
| arch/db/migrations/046-kanban-purpose-scenario.sql | 10 | **C5 via Migration 049** | 6 kart C003/C004/D007/D019/D027/K012 body_markdown |
| memory/zoho_blueprint_api_quirks.md | — | **C5 SCOPE** | Frontmatter status='deprecated_historical' + V2 cross-link |
| arch/db/migrations/048-zoho-out-drop-tables.sql | 71 | **AUDIT TRAIL (DOKUNULMAZ)** | C1 artifact, historical |
| arch/db/migrations/{012,013,014,015}.sql | 21 | **HISTORICAL (DOKUNULMAZ)** | Zoho table create/migration — historical record |
| arch/db/integrations.sql | — | **AUDIT TRAIL (DOKUNULMAZ)** | C1'de V2 audit trail block-comment eklendi, dokunulmaz |
| tracking/feat-inma-pipeline-v2-c1.md | 107 | **AUDIT TRAIL (DOKUNULMAZ)** | C1 final state, smoke results, AC1-5 verdict |
| tracking/README.md | 25 | **DOKUNULMAZ** | Master tracking, C1 row + FEAT-PIPELINE CANCELLED row historical |

**C5 active scope:** ~95 mention temizlenecek (Dent docs 91 + arch/docs 1 + arch/features 2 + 1 memory) + 6 kanban kart UPDATE + arch/lessons-learned.md Zoho-bound entries inline tag.

---

## Migration 049 — Kanban Card V2 Refresh

**Path:** `arch/db/migrations/049-kanban-zoho-out-content-refresh.sql`

**Strategy:** Atomic transaction snapshot-then-update:
1. CREATE TABLE `dent_paket_kanban_zoho_refresh_archive_20260513` AS SELECT * FROM `kanban_cards` WHERE `ref_code IN ('C003','C004','D007','D019','D027','K012') AND board_key='inse'` (6 row pre-state preservation)
2. UPDATE 6 kart body_markdown V2 context'e rewrite (idempotent guard `body_markdown NOT LIKE '%FEAT-INMA-PIPELINE-V2%'`)
3. DO $verify$ INV-SEED-039..045 7 postcondition fail-loud:
   - INV-SEED-039: archive table 6 row preserved
   - INV-SEED-040: C003 body_markdown V2 marker present
   - INV-SEED-041: C004 body_markdown V2 marker present
   - INV-SEED-042: D007 body_markdown V2 marker present
   - INV-SEED-043: D019 body_markdown V2 marker present
   - INV-SEED-044: D027 body_markdown V2 marker present
   - INV-SEED-045: K012 body_markdown V2 marker present

**6 Kart V2 Rewrite Tablosu:**

| ref_code | Eski (Zoho-bound, pre-C5) | Yeni (V2, post-C5) |
|----------|---------------------------|--------------------|
| C003 | Müşteri Zoho CRM'inde lead pipeline (7 aşama) Blueprint olarak tanımlanması | Müşteri INMA agent UI'da customer_status dropdown (V2 INMA-otorite) — Zoho-out 2026-05-12, INMA agent manuel set ederse INMA→INSE webhook ile INSE opaque TEXT olarak saklanır |
| C004 | Invekto'nun Zoho CRM'e OAuth ile bağlanması + stage eşleştirme | **DEPRECATED 2026-05-12 — FEAT-INMA-PIPELINE-V2 C1 Zoho-out, Zoho OAuth tamamen iptal. Kart kapatılabilir veya operasyonel referans için tutulabilir.** |
| D007 | Slot tanımlama + Zoho takvim çift yönlü sync | Slot tanımlama + takvim view + meeting link kopyalama (Zoho takvim sync iptal — Google Meet Mock provider FEAT-VCP Chunk A + Chunk C OAuth backlog) |
| D019 | INMA sohbet panelinde lead aşaması dropdown + çift yönlü sync (FEAT-PIPELINE) | INMA sohbet panelinde customer_status dropdown — INMA agent manuel set + INMA→INSE webhook → flow trigger `customer_status_changed` (FEAT-INMA-PIPELINE-V2 C2/C3/C4 BLOCKED INMA contract) |
| D027 | Outbound /broadcast/send'e INMA contact + Zoho COQL dış kaynak adapter | Outbound /broadcast/send'e INMA contact dış kaynak adapter (Zoho COQL adapter iptal — FEAT-OBI backlog B6 Zoho-out sonrası INMA-only) |
| K012 | Lead pipeline tanımı sahipliği INMA'da olacak; Invekto + Zoho INMA'dan çeker (FEAT-PIPELINE 3-way sync) | Lead pipeline tanımı sahipliği INMA'da; INSE INMA→INSE webhook ile customer_status'u opaque TEXT olarak saklar (FEAT-INMA-PIPELINE-V2 one-way INMA→INSE, Zoho-out 2026-05-12) |

---

## Dent Pilot Doc Refresh Stratejisi

**dent-golive.html (52 mention):**
- Zoho CRM Blueprint config bölümlerini (C.1-C.7) DELETE
- 'Customer Status Akışı (V2)' yeni section: INMA agent UI manuel dropdown + INMA→INSE webhook payload contract + INSE opaque TEXT storage + flow trigger `customer_status_changed` (C3 BLOCKED) + flow action `Set Customer Status` (C4 BLOCKED)
- Smoke test spec'inden Zoho stage sync adımları DELETE + INMA agent dropdown manuel test + webhook simulasyonu C2 ready olunca

**pilot-stage1-prep.md (35 mention):**
- Zoho Blueprint sync setup adımları (~45 min) DELETE
- Stage mapping table + LeadStatusEventMap + ZohoLifecycleDispatcher integration DELETE
- 'INMA Agent UI Setup (V2)' yeni section: customer_status field tenant config, agent UI dropdown training, webhook endpoint config (C2 BLOCKED)

**pilot-field-mapping.md (1 mention):**
- `pipeline_status → Zoho Blueprint sync` satırını DELETE veya 'INMA agent manuel dropdown (V2)' olarak güncelle

**decisions.md (3 mention):**
- Blueprint Sync ownership satırları DELETE
- 'V2 Mimari Karar (2026-05-12)' yeni section: Zoho INSE'den TAMAMEN çıkıyor, INMA otorite, one-way INMA→INSE webhook, FEAT-INMA-PIPELINE-V2 5 chunk

---

## Lessons-Learned + Memory Strateji

**arch/lessons-learned.md** Zoho-bound entry'ler (48 mention):
- Blueprint API quirks (undocumented /settings/blueprint, scope mismatch, record-in-process prereq) → inline `> **DEPRECATED 2026-05-12 — FEAT-INMA-PIPELINE-V2 C1 Zoho-out, V2 ile irrelevant.**`
- ZohoConnectionPage + ZohoStageMappingPage helper dedup → DEPRECATED tag
- Zoho branch context switch (work/zoho-p41-statemap-coverage) → DEPRECATED tag
- **Evrensel pattern'ler DOKUNULMAZ:** Codex CQ taksonomi, Migration idempotent DO $verify$ pattern, NSSM service deploy SSH-during-extract recovery, error code namespace audit, G7 SCHEDULER HOST EXCEPTION pattern

**Memory zoho_blueprint_api_quirks.md:**
```yaml
---
name: zoho-blueprint-api-quirks
description: "[DEPRECATED 2026-05-12] Historical Zoho v6/v8 Blueprint API quirks. Zoho INSE'den TAMAMEN çıkarıldı (FEAT-INMA-PIPELINE-V2 C1). Bkz [[project-inma-pipeline-v2-decision]]."
metadata:
  type: reference
  status: deprecated_historical
  deprecated_at: 2026-05-12
  superseded_by: project-inma-pipeline-v2-decision
---
> **⚠ DEPRECATED 2026-05-12** — FEAT-INMA-PIPELINE-V2 C1 (Zoho-out) ile irrelevant. Tarihi referans için tutuluyor. Aktif Zoho integration YOK.
```

---

## Acceptance Criteria

| ID | Criterion | Verified |
|----|-----------|----------|
| AC1 | Migration 049 atomic execute + 7 postcondition fail-loud PASS + idempotent re-run safe | ✅ DONE — Prod execute 2026-05-12, 6/6 V2 marker + 6 archive row, DO $verify$ silent PASS |
| AC2 | Dent docs 91 Zoho mention sıfırlanır + INMA-otorite section eklenir | ✅ DONE — dent-golive.html §V2 + §4.2-C rewrite + 8 durum tablosu V2; pilot-stage1-prep Bölüm A.3 + Bölüm C V2 rewrite; pilot-field-mapping + decisions.md V2 |
| AC3 | lessons-learned Zoho-bound DEPRECATED tag + evrensel DOKUNULMAZ | ✅ DONE — banner section eklendi (purely Zoho-bound vs evrensel pattern ayrımı) + 2 entry inline DEPRECATED tag |
| AC4 | arch/docs + arch/features 3 mention strip + INV-SEED-039..045 arch/errors.md | ✅ DONE — whatisinvekto + lead-intake-webhook strikethrough; INV-SEED-039..045 7 entry arch/errors.md |
| AC5 | Memory zoho_blueprint_api_quirks.md frontmatter deprecated_historical + cross-link | ✅ DONE — status='deprecated_historical' + banner + [[project-inma-pipeline-v2-decision]] cross-link |
| AC6 | Codex single submission PASS + grep audit clean | ✅ Q FORCE PASS iter 4 (11/12 CQ + 3/4 CoVe substantively PASS; tek kalan CQ11/Q1 pedantic CTAS schema drift recovery edge-case) |

---

## Build / Deploy / Smoke

| Aşama | Status | Notlar |
|-------|--------|--------|
| Build | **N/A** | Kod sıfır, doc + DB-content paketi |
| Migration 049 execute | **✅ DONE 2026-05-12** | MCP `invekto-postgres__execute` atomic tx + DO $verify$ silent PASS + 6 archive row + 6/6 V2 marker verify |
| Service deploy | **N/A** | Kod değişikliği yok |
| Smoke | **✅ DONE 2026-05-12** | Pre-flight verify (6 kart tenant_id=NULL confirmed) + post-execute query (6/6 V2 marker + 6 archive row) PASS; iter arc 0→4 fix list audited in commit message |
| Codex review | **Q FORCE PASS iter 4** | Iter 0 FAIL (4 blocker) → iter 1 FAIL (4 blocker NULL-safety + risk + V2 contract + atomic) → iter 2 FAIL (6 blocker LOW kalıntı + bidirectional kalıntı + DDL) → iter 3 FAIL (2 blocker CTAS schema drift discovered) → iter 4 FAIL (1 blocker CTAS recovery edge-case) → Q FORCE PASS: 11/12 CQ + 3/4 CoVe substantively PASS, tek kalan CQ11/Q1 pedantic CTAS schema drift recovery (archive snapshot table DEFAULTS not critical, prod data integrity intact) |

---

## Files Inventory

**Yeni dosyalar:**
- `arch/db/migrations/049-kanban-zoho-out-content-refresh.sql`
- `arch/plans/20260513-feat-inma-pipeline-v2-c5-dashboard-cleanup.json`
- `tracking/feat-inma-pipeline-v2-c5.md` (bu dosya)

**Edit edilecek dosyalar:**
- `DentAdavista/plan/dent-golive.html` (52 mention DELETE + V2 section)
- `DentAdavista/plan/pilot-stage1-prep.md` (35 mention DELETE + V2 section)
- `DentAdavista/plan/pilot-field-mapping.md` (1 mention update)
- `DentAdavista/plan/decisions.md` (3 mention DELETE + V2 section)
- `arch/lessons-learned.md` (Zoho-bound entries DEPRECATED tag)
- `arch/docs/whatisinvekto.md` (1 mention strip)
- `arch/features/lead-intake-webhook.md` (2 mention strip)
- `arch/errors.md` (INV-SEED-039..045 add)
- `arch/db/kanban-board.sql` (canonical mirror Migration 049 comment block)
- `tracking/README.md` (FEAT-INMA-PIPELINE-V2 row C5 status flip)
- `tracking/pilot-launch-roadmap.md` (Faz B C5 PENDING→DONE)
- `arch/session-memory.md` (Last Update + Execution Queue + Recently Completed)
- `C:/Users/taner/.claude/projects/c--CRMs-InvektoServices/memory/zoho_blueprint_api_quirks.md` (frontmatter + body banner)

**Toplam:** ~15 dosya, 0 kod LOC, 0 build, 0 service deploy.
