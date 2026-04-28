# Yol Haritası Audit Batch A — Kanban Data Fix + Plan/Tracking Sync

> **Slug:** `20260429-feat-roadmap-audit-batch-a`
> **Status:** DONE+DEPLOYED+VERIFIED (Migration 038 prod execute + 7/7 verify PASS 2026-04-29 00:50 UTC)
> **Risk:** LOW (data-only migration + doc sync, build N/A)
> **Plan:** [`arch/plans/20260429-feat-roadmap-audit-batch-a.json`](../arch/plans/20260429-feat-roadmap-audit-batch-a.json)
> **Audit kaynak:** `c:/tmp/tmp-superadmin-roadmap-audit-20260428.md` + `c:/tmp/tmp-roadmap-audit-next-session-handoff.md`

## Özet

Audit handoff'taki 14 bulgu = 5 paket. Q kararı (2026-04-29 00:25 UTC): 4 commit batching. **Batch A** = Paket 1 (kanban data fix Migration 038) + Paket 5 (v1/v2/v3 plan+tracking sync) tek commit. LOW risk grupla, HIGH risk Batch D ayrı.

## Q Kararları (Pre-req)

| # | Soru | Karar |
|---|------|-------|
| G1 | Ops auth (Paket 3 önkoşulu) | **inse internal JWT** (TenantId int claim) — Q kendi credentials ile login; planlanan fix doğrudan çalışır; inma JWT fallback REJECTED |
| G2 | Migration retroaktif edit policy | **Retroaktif edit OK** — 036/037 doğrudan edit (Paket 2 Batch B) |
| G3 | Batching | **4 commit** — LOW risk grupla, HIGH risk ayrı |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | Migration 038 idempotent prod-ready: D005-D009 status TODO + D006/D009 summary reality + O001 title 8 ayar yuzeyi + D020 roll-up clarification | ✅ Prod (7/7 query verify PASS 2026-04-29 00:50 UTC) |
| AC2 | Migration 038 postcondition INV-SEED-028 fail-loud (3 assertion: inprogress=0, O001 title, D006 summary) | ✅ DO $verify$ block PASS (RAISE NOTICE info, no exception) |
| AC3 | v1 plan AC1-AC6 verified=true + verification_note | ✅ `arch/plans/20260428-feat-pilot-kanban-superadmin.json` 6 AC sync |
| AC4 | v1 tracking AC tablosu PENDING → ✅ + Status DONE+DEPLOYED | ✅ `tracking/20260428-feat-pilot-kanban-superadmin.md` 6 satır AC ✅ |
| AC5 | v2 plan status PLANNING→DONE + AC verified=true + files_changed + build PASS + verdict source codex | ✅ `arch/plans/20260428-feat-roadmap-v2-refcode.json` full sync |
| AC6 | v3 retroaktif plan + tracking yarat (Migration 037 paketi için) | ✅ `arch/plans/20260428-feat-roadmap-v3-depends.json` + `tracking/20260428-feat-roadmap-v3-depends.md` (NEW) |

## Files Changed

```
arch/db/migrations/038-roadmap-audit-data-fix.sql                  [NEW] Migration 038 (Paket 1)
arch/errors.md                                                     [MOD] INV-SEED-028 entry
arch/plans/20260428-feat-pilot-kanban-superadmin.json              [MOD] 6 AC verified=true (Paket 5 v1)
arch/plans/20260428-feat-roadmap-v2-refcode.json                   [MOD] status DONE + AC verified + files_changed + build PASS + verdict (Paket 5 v2)
arch/plans/20260428-feat-roadmap-v3-depends.json                   [NEW] Retroaktif plan (Paket 5 v3)
arch/plans/20260429-feat-roadmap-audit-batch-a.json                [NEW] Bu paket plan
tracking/20260428-feat-pilot-kanban-superadmin.md                  [MOD] AC ✅ + Status DONE+DEPLOYED (Paket 5 v1)
tracking/20260428-feat-roadmap-v3-depends.md                       [NEW] Retroaktif tracking (Paket 5 v3)
tracking/20260429-feat-roadmap-audit-batch-a.md                    [NEW] Bu dosya
```

## Migration 038 Prod Execute (2026-04-29 00:50 UTC)

```sql
-- §1 D005-D009 IN_PROGRESS → TODO (5 row)
-- §2 D006 summary 'Endpoint hazir (Program.cs:5712, 5750); LeadDetailPage Timeline tab eksik.'
-- §3 D009 summary 'Endpoint hazir (Program.cs:5771); frontend chart komponenti eksik.'
-- §4 O001 title 'E.1-E.8 Dashboard Konfigurasyon (8 ayar yuzeyi)' + summary 5/8 implement
-- §5 D020 (pilot-15-packets) summary '15 paket roll-up (D021-D026 alt-paket toplami...)'
-- §6 DO $verify$ INV-SEED-028 PASS
```

**Verify query result:** 7/7 row doğru durumda
- `appointments-page-zoho-sync` → TODO ✓
- `conversion-funnel-chart` → TODO + summary "Endpoint hazir (Program.cs:5771)..." ✓
- `dashboard-config-7-pages` → title "(8 ayar yuzeyi)" + summary "5/8 implement" ✓
- `doctors-management-page` → TODO ✓
- `lead-detail-timeline` → TODO + summary "Endpoint hazir (Program.cs:5712, 5750)..." ✓
- `lead-list-page` → TODO ✓
- `pilot-15-packets` → DONE + summary "15 paket roll-up (D021-D026 alt-paket toplami...)" ✓

## Audit Bulgu Tracker (handoff §3)

### 🔴 P0 (2 — Batch A scope dışı)
- D027 Ops auth (Batch D)
- D028 Migration 036 partial INDEX guard (Batch B)

### 🟠 P1 (6)
| # | Bulgu | Batch |
|---|-------|-------|
| 3 | D005, D007, D008 IN_PROGRESS → TODO | **A** ✅ |
| 4 | D006 summary endpoint hazır | **A** ✅ |
| 5 | D009 summary endpoint hazır | **A** ✅ |
| 6 | D029 Migration 037 postcondition + docstring | B |
| 7 | D030 Drawer dependency metni yumuşatma | C |
| 8 | v1/v2/v3 plan AC + v3 retroaktif | **A** ✅ |

### 🟡 MEDIUM (3)
| # | Bulgu | Batch |
|---|-------|-------|
| 9 | O001 sayım hatası (7 sayfa → 8 ayar yüzeyi) | **A** ✅ |
| 10 | D020 roll-up duplikasyon clarification | **A** ✅ |
| 11 | K kodu kategori vs status karışıklığı | (skip — handoff non-goal note: 035 docstring fix Batch A scope dışı, ileride Q ihtiyaç olursa) |

### 🟢 LOW (3 — Batch C scope)
- D035 /pilot-kanban → Navigate redirect
- D036 COLUMNS.cls dead code
- D037 Kanban GET single-query

## Build / Codex / Deploy

- Build: N/A (sadece SQL + JSON + MD; .cs/.tsx kod değişikliği YOK; FEAT-PHOTO commit `50f8681` build PASS aynı session başında)
- Codex: Pending (`/rev` Batch A pure data+doc, expected single-iter PASS LOW risk)
- Migration 038 prod execute: ✅ 2026-04-29 00:50 UTC (postcondition INV-SEED-028 PASS)
- Service deploy: GEREK YOK (data-only migration; backend kod değişikliği yok; restart gerekmez)

## Smoke Plan

| # | Adım | Beklenen |
|---|------|----------|
| S1 | `mcp__invekto-postgres__query SELECT * FROM kanban_cards WHERE card_slug IN (...) ORDER BY card_slug` | 7/7 row doğru durum (yukarıda dökümante) |
| S2 | Dashboard `/yol-haritasi/dent-pilot` browser açılış | D005-D009 TODO kolonunda görünüyor; O001 title "(8 ayar yuzeyi)" ; D020 summary "15 paket roll-up..." |
| S3 | KanbanDrawer D006 click | Summary "Endpoint hazir (Program.cs:5712, 5750); LeadDetailPage Timeline tab eksik." |

## Rollback

Bu paket data-only; rollback gerekirse:
- Migration 038 §1 reverse UPDATE: `UPDATE kanban_cards SET status='IN_PROGRESS' WHERE card_slug IN (...)`
- §2-§5 summary/title eski değerlere SQL UPDATE
- INV-SEED-028 entry arch/errors.md'den çıkar
- Plan AC verified=false geri al

Pratik: rollback gereksiz — audit findings reality match, koordinator board'da daha doğru sinyal görür.

## Notes

- Migration 038 idempotent: re-run UPDATE matched 0 rows (data zaten doğru durumda); postcondition kontrol etmeyi unutmaz, sadece "no-op" davranır.
- v1 plan zaten `status: DONE` idi; AC verified=false stale flag'leri sync edildi
- v2 plan PLANNING→DONE büyük değişim; verdict source 'codex' iter 0 PASS retroaktif (commit log analiziyle)
- v3 retroaktif plan/tracking commit `2cc6d90` reality'sini reconstruct ediyor (commit message + git diff'den)
- Audit handoff "tek paketle sıkıştırma yanlış karar" dersi: Batch A LOW risk grupla, paralel staged dosyalar disjoint kalır

## Audit Handoff (handoff §11)

K prefix docstring fix (`035-kanban-cards.sql §40-48`) — Q non-goal kararı: 035 prod'da deployed retroaktif comment edit Batch A scope dışı (LOW kalmasi için); ileride ihtiyaç olursa Q ayrı paket önerir.
