# Yol Haritası Audit Batch C — Kanban UI/Optimization Hijyen

> **Slug:** `20260429-feat-roadmap-audit-batch-c`
> **Status:** DONE (build + SPA rebuild PASS, deploy pending)
> **Risk:** LOW (UI text + dead code + DB call optimize)
> **Plan:** [`arch/plans/20260429-feat-roadmap-audit-batch-c.json`](../arch/plans/20260429-feat-roadmap-audit-batch-c.json)
> **Audit kaynak:** `c:/tmp/tmp-roadmap-audit-next-session-handoff.md` Paket 4 (D030 + D035 + D036 + D037)

## Özet

Audit Paket 4: Kanban UI hijyen. 5 file 4 fix:

| Fix | Dosya | Değişiklik |
|-----|-------|-----------|
| **D030** | `KanbanDrawer.tsx` | "Bağımlılıklar" → "İlgili Kartlar" + "planning ipucu, runtime engel değil" yumuşatma |
| **D035** | `App.tsx` | `/pilot-kanban` Suspense PilotKanbanPage render → `<Navigate to="/yol-haritasi/dent-pilot" replace />` |
| **D036** | `PilotKanbanPage.tsx` | `COLUMNS.cls` dead code temizlik (interface + 5 row literal) |
| **D037** | `KanbanEndpoints.cs` + `KanbanRepository.cs` | GET 2 DB call → 1 DB call + `cards.Max(c => c.UpdatedAt)` in-memory |

## Q Kararları

| # | Soru | Karar |
|---|------|-------|
| G3 | Batching | **4 commit** — LOW risk grupla |
| audit interview | Dependency semantik | **Görsel-only** — drawer text yumuşat, backend enforce YOK |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | D030 KanbanDrawer "İlgili Kartlar" + planning ipucu | ✅ Edit |
| AC2 | D035 App.tsx /pilot-kanban → Navigate redirect | ✅ Edit |
| AC3 | D036 PilotKanbanPage COLUMNS.cls dead code temizlik | ✅ Edit |
| AC4 | D037 KanbanEndpoints single-query + Repository.GetBoardUpdatedAt sil | ✅ Edit |

## Files Changed

```
src/Invekto.Backend/Dashboard/src/components/KanbanDrawer.tsx       [MOD] D030
src/Invekto.Backend/Dashboard/src/App.tsx                           [MOD] D035
src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx         [MOD] D036
src/Invekto.Backend/Endpoints/KanbanEndpoints.cs                    [MOD] D037
src/Invekto.Backend/Data/KanbanRepository.cs                        [MOD] D037
src/Invekto.Backend/wwwroot/app/**                                  [MOD] SPA Vite rebuild
arch/plans/20260429-feat-roadmap-audit-batch-c.json                 [NEW]
tracking/20260429-feat-roadmap-audit-batch-c.md                     [NEW] Bu dosya
```

## Build / Deploy

- Backend build: **PASS** (0 errors / 17 warnings pre-existing, 23.39s)
- SPA Vite rebuild: **PASS** (6.37s; LeadDetailPage chunk + tüm sayfa chunk'ları yeni hash)
- Service deploy: **GEREKİYOR** (Backend kod değişikliği var: KanbanEndpoints + KanbanRepository)

## Smoke Plan (deploy sonrası)

| # | Adım | Beklenen |
|---|------|----------|
| S1 | Browser `/pilot-kanban` → URL bar `/yol-haritasi/dent-pilot` (D035) |
| S2 | KanbanDrawer açık + bağımlı kart → header "İlgili Kartlar" + italic "Planning ipucu — runtime engel değil" (D030) |
| S3 | `GET /api/ops/kanban/dent-pilot` response shape aynı (KanbanBoardDto unchanged); UpdatedAt field aynı değer (cards.Max) |
| S4 | Hangi server log'lara bakılmasın — single-query optimize log değişikliği yok |

## Notes

- D030 drawer text yumuşatma davranış değişikliği yapmıyor — sadece kullanıcı algısı düzelir
- D035 Navigate `replace` kullanıldı (back button geri gitmez)
- D036 dead code remove TypeScript compile-clean (col.cls okuyan kod yok)
- D037 cards.Max in-memory eski SQL MAX(updated_at) sonucu birebir aynı (kart listesi already loaded; küçük LINQ pass)
- Bu paket Batch C; sıradaki Batch D ops auth HIGH risk D027
