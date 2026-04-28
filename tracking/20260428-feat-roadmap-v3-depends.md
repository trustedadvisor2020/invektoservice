# FEAT-ROADMAP-V3-DEPENDS — Kart Bağımlılık + P0 Text Badge UX

> **Slug:** `20260428-feat-roadmap-v3-depends`
> **Status:** DONE+DEPLOYED (retroaktif tracking; commit 2cc6d90 prod'da 2026-04-28 22:03 UTC)
> **Risk:** LOW (additive migration + read-only field + UI polish)
> **Plan:** [`arch/plans/20260428-feat-roadmap-v3-depends.json`](../arch/plans/20260428-feat-roadmap-v3-depends.json) (retroaktif)
> **Audit handoff Batch A — Paket 5 of 5:** Bu tracking artefakt commit 2cc6d90 sırasında yaratılmamıştı; audit raporundan tespit edilip 2026-04-29 retroaktif eklendi.

## Özet

Q'nun board'da kart-arası ilişki görme isteği + P0 visual cue accessibility iyileştirmesi. 13 kart için Stage 1 launch zinciri (C002→O003→O004 vb.) görsel olarak işaretlendi. Backend'de depends_on read-only field, frontend'de kart altı `↳ C001, C003` indicator + drawer "Bağımlılıklar" bölümü.

## Q Kararları

| Soru | Karar | Etki |
|------|-------|------|
| Dependency semantik | Görsel-only (runtime engel YOK) | Drawer text "planning ipucu" yumuşatma D030 (Batch C ileride) |
| P0 visual cue | 2px nokta → text badge "P0" | WCAG 2.1 AA + tooltip aciklama |
| 13 kart bağımlılık | Stage 1 chain + Zoho/WABA/training | Q manuel mapping migration seed |
| Backend update | Read-only (sadece migration) | UPDATE endpoint depends_on kabul etmiyor |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | Migration 037 idempotent + 13 kart bağımlılık + INV-SEED-027 postcondition | ✅ Prod (commit 2cc6d90 + canonical mirror commit c8d150b) |
| AC2 | Backend KanbanCardDto.DependsOn nullable + KanbanRepository SELECT depends_on column shift | ✅ Prod canli |
| AC3 | Frontend KanbanCard.depends_on interface + CardItem indicator + Drawer "Bağımlılıklar" section | ✅ Prod SPA |
| AC4 | P0 visual cue 2px nokta → "P0" text badge + tooltip | ✅ Prod SPA |

## Files Changed

```
arch/db/migrations/037-kanban-depends-on.sql              [NEW]
arch/db/kanban-board.sql                                  [MOD] (canonical mirror, c8d150b)
arch/errors.md                                            [MOD] (INV-SEED-027)
src/Invekto.Shared/Contracts/Kanban/KanbanCardDto.cs      [MOD] (DependsOn nullable)
src/Invekto.Backend/Data/KanbanRepository.cs              [MOD] (column index shift)
src/Invekto.Backend/Dashboard/src/lib/api.ts              [MOD] (interface)
src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx [MOD] (CardItem deps + P0 text badge)
src/Invekto.Backend/Dashboard/src/components/KanbanDrawer.tsx [MOD] ("Bağımlılıklar" section)
```

## Audit handoff Paket 4 (Batch C ileride)

D030 drawer dependency metnini "İlgili Kartlar" + planning ipucu olarak yumuşatma — bu paket V3'in extension'ı, Batch C scope'unda işlenecek.

## Build / Codex / Deploy

- Build PASS (commit 2cc6d90 message confirms 0 errors)
- Codex review iter 0 PASS (single-iter, commit message tek-iter implication)
- Prod deploy 2026-04-28 22:03 UTC

## Notes (retroaktif)

- Plan/tracking commit 2cc6d90 sırasında yapılmadı — audit raporundan tespit edildi (`c:/tmp/tmp-superadmin-roadmap-audit-20260428.md` v2 kombine v3 plan/tracking artefakt eksikliği)
- Audit Batch A 2026-04-29 (Paket 5 of 5) retroaktif yarattığı bu artefakt commit 2cc6d90 git diff'inden + commit message'dan rebuild edildi
- Asıl deploy verifikasyonu için `git show 2cc6d90` referans
