# FEAT-ROADMAP-V2-REFCODE — Yol Haritası v2 + Kart Referans Kodu

**Slug:** `20260428-feat-roadmap-v2-refcode`
**Risk:** MEDIUM
**Status:** REVIEW (build PASS, ready for /rev)
**Owner:** Claude + Q
**Started:** 2026-04-28
**Plan:** [`arch/plans/20260428-feat-roadmap-v2-refcode.json`](../arch/plans/20260428-feat-roadmap-v2-refcode.json)

## Q İstekleri

| # | İstek | Çözüm |
|---|-------|-------|
| 1 | "Pilot Kanban" değil → **"Yol Haritası"** | Layout.tsx nav label rename |
| 2 | Tek pilot için değil — multi-board | URL param `/yol-haritasi/:boardKey?` + fallback `dent-pilot` |
| 3 | Her işe referans numarası (G638 gibi 1 harf + 3 rakam) | Migration 036 ref_code kolonu + kategori prefix (C/K/O/D/U/X) |

## Audit'ten Real Bug Fix

| Bug | Çözüm |
|-----|-------|
| Drawer source link 404 (DentAdavista wwwroot deploy edilmiyor) | `isSourceAccessibleInProd()` blocklist + graceful fallback gri text |
| Frontend BOARD_KEY hardcoded | URL param boardKey + DEFAULT 'dent-pilot' |

## Ref Code Dağılımı (64 kart)

| Prefix | Kategori | Aralık | Sayı |
|--------|----------|--------|------|
| **C** | CUSTOMER (Müşteri / 3rd-party) | C001-C005 | 5 |
| **K** | DECISION (Karar) | K001-K012 | 12 |
| **O** | OPS (Invekto operasyon) | O001-O009 | 9 |
| **D** | DEV (Yazılım) | D001-D026 | 26 |
| **U** | UI (UI ekran) | U001-U009 | 9 |
| **X** | DOC (Doküman) | X001-X003 | 3 |
| **TOPLAM** | | | **64** |

Önemli kart örnekleri:
- **C001** = B.8 HSM Welcome Template Meta Onayı (P0 BLOCKER)
- **K005** = K5: Reklam Yönetimi Kimde?
- **D021** = B-META Webhook (DONE)
- **D025** = Migration 034 Photo Flow (DONE)
- **U001** = UI #10 Koordinator Devir Kuyruğu (BACKLOG P1)

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | Migration 036 idempotent + 64 ref_code UPDATE + INV-SEED-026 postcondition | ✅ Prod'da |
| AC2 | Backend Repository SELECT/UPDATE ref_code + Endpoints PATCH slug VEYA ref_code | ✅ Build PASS |
| AC3 | Frontend BOARD_KEY → URL param + nav "Yol Haritası" + KanbanCard.ref_code | ✅ TS PASS |
| AC4 | Card UI ref_code badge prominent + Drawer header + source 404 fix | ✅ Implement |
| AC5 | /wrap Step 3.5 ref_code matching desteği | ✅ wrap.md update |
| AC6 | Tracking + session-memory + arch/errors.md INV-SEED-026 | ✅ Bu dosya |

## Mimari (Eklenenler)

### DB Schema (Migration 036)
```sql
ALTER TABLE kanban_cards ADD COLUMN IF NOT EXISTS ref_code VARCHAR(4) NOT NULL DEFAULT '----';
ALTER TABLE kanban_cards ADD CONSTRAINT chk_kanban_cards_ref_code
  CHECK (ref_code = '----' OR ref_code ~ '^[A-Z][0-9]{3}$');
CREATE UNIQUE INDEX uq_kanban_cards_board_ref ON kanban_cards(board_key, ref_code)
  WHERE ref_code <> '----';
-- 64 UPDATE WHERE ref_code = '----' (idempotent re-run)
```

### Repository Lookup (slug VEYA ref_code)
```sql
WHERE board_key = @board
  AND (card_slug = @slug OR ref_code = @slug)
  AND tenant_id IS NOT DISTINCT FROM @tenant
```

### Frontend Multi-board
- Route: `/yol-haritasi/:boardKey?` + backward compat `/pilot-kanban`
- `useParams<{boardKey?: string}>()` + `DEFAULT_BOARD_KEY = 'dent-pilot'`
- Nav label: "Yol Haritası"
- Page header: `Yol Haritası <code>{boardKey}</code>`
- Card UI: ref_code sol başta `bg-slate-900 text-white` mono badge

### Drawer Source Link Fallback
```typescript
function isSourceAccessibleInProd(file: string): boolean {
  const blocked = ['DentAdavista/', 'arch/', 'tracking/', 'src/'];
  return !blocked.some(prefix => file.startsWith(prefix));
}
// Erişilebilir → <a href> link; değilse → mono text + "prod'da arşivlenmemiş" italic uyarı
```

## Dosya Listesi

**Yeni (3):**
- `arch/db/migrations/036-kanban-ref-code.sql`
- `arch/plans/20260428-feat-roadmap-v2-refcode.json`
- `tracking/20260428-feat-roadmap-v2-refcode.md`

**Modified (10):**
- `arch/db/kanban-board.sql` — canonical mirror update (ref_code kolonu + constraint + partial UNIQUE)
- `arch/errors.md` — INV-SEED-026
- `src/Invekto.Shared/Contracts/Kanban/KanbanCardDto.cs` — RefCode field
- `src/Invekto.Backend/Data/KanbanRepository.cs` — SELECT/UPDATE ref_code + slugOrRef OR
- `src/Invekto.Backend/Endpoints/KanbanEndpoints.cs` — comment + 404 message slug/ref
- `src/Invekto.Backend/Dashboard/src/lib/api.ts` — KanbanCard.ref_code
- `src/Invekto.Backend/Dashboard/src/components/Layout.tsx` — nav "Yol Haritası" + path /yol-haritasi
- `src/Invekto.Backend/Dashboard/src/App.tsx` — 3 route (yol-haritasi + :boardKey + pilot-kanban)
- `src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx` — useParams + page title + ref_code badge
- `src/Invekto.Backend/Dashboard/src/components/KanbanDrawer.tsx` — ref_code header + source fallback
- `C:/Users/taner/.claude/commands/wrap.md` — Step 3.5 ref_code matching

## Riskler / Mitigation

| Risk | Mitigation |
|------|-----------|
| Migration 036 var olan ref_code override eder | WHERE `ref_code = '----'` guard — re-run sadece placeholder'ları doldurur |
| Endpoint slug/ref_code çakışma | Slug regex lowercase kebab, ref_code regex `^[A-Z][0-9]{3}$` — disjoint |
| Yeni kart eklendiğinde ref_code manuel | Helper SQL function (gelecek paket) `next_ref_code(board, category)` |
| Multi-board için 2. board nav listesi | Şu an URL manual nav yeterli; gelecek board picker dropdown |

## Notes

- Component file rename **YAPILMADI** — `PilotKanbanPage.tsx` adı stabil internal (3 yer import etse hepsini yeniden adlandırmak risk). Sadece UI label ve route adı değiştirildi.
- `/pilot-kanban` URL backward-compat olarak destekli — eski bookmark çalışır.
- `ref_code = '----'` placeholder yeni eklenen kartlar için — partial UNIQUE index ile birden fazla kart placeholder olabilir.
