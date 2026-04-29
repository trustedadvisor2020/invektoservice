# 20260429-feat-roadmap-board-rename-frontend — Tracking

> **Slug:** `20260429-feat-roadmap-board-rename-frontend` | **Risk:** MEDIUM (iter 1 escalation per Codex CQ5 — runtime frontend behavior + client routing degisikligi)
> **Plan JSON:** [arch/plans/20260429-feat-roadmap-board-rename-frontend.json](../arch/plans/20260429-feat-roadmap-board-rename-frontend.json)
> **Status:** **REVIEW (iter 1)** — Codex /rev iter 0 LOW FAIL CQ5/Q4 → MEDIUM escalation
> **Build:** Dashboard SPA (vite tsc + build, 7.10s PASS), .NET dotnet build skip (TSX + asset only)

## Özet

Migration 039 frontend tamamlama paketi. Migration 039 SQL halihazirda prod'da execute edildi (board_key 'dent-pilot' -> 'inse' rename, 69 kart inse board'da), ancak frontend default route hala 'dent-pilot' arıyordu — sonuc: /yol-haritasi sayfasi bos goruluyor (0 kart). Bu paket frontend default constant + URL redirect target'leri **inse** olarak gunceller, SPA rebuild + Backend deploy ile prod'a tasir.

## Tetikleyici

Q ekrani gosterdi: "Yol Haritasi dent-pilot" title + 0 toplam/blocked/in progress/done — Migration 041 (D029/D030/D031 INSERT) execute sonrasi yeni kartlar gozulmeliydi. Tani: DB inse board 69 kart, ama frontend `DEFAULT_BOARD_KEY = 'dent-pilot'` arıyor.

## Scope

### In Scope
- `src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx` — DEFAULT_BOARD_KEY 'dent-pilot' -> 'inse' + comment update Migration 039 referansi
- `src/Invekto.Backend/Dashboard/src/App.tsx` — /pilot-kanban redirect target 'dent-pilot' -> 'inse' + comment update
- `src/Invekto.Backend/wwwroot/app/assets/**` — vite build sonucu yeni asset hash'leri (PilotKanbanPage-qWgtl3HU.js + index-B-oMET1V.js + 36 chunk rename)
- `src/Invekto.Backend/wwwroot/app/index.html` — script + css ref new hash'lere update
- `arch/plans/20260429-feat-roadmap-board-rename-frontend.json` — plan JSON
- `tracking/20260429-feat-roadmap-board-rename-frontend.md` — bu dosya

### Out of Scope (Explicit)
- `src/Invekto.Backend/Dashboard/src/lib/api.ts` (clinic metadata, working tree restored unstaged)
- `src/Invekto.Backend/Dashboard/src/pages/SettingsPage.tsx` (clinic metadata)
- `ClinicMetadataSettingsPage.tsx` + `clinicMetadata.ts` (clinic metadata, untracked Q WIP)
- Migration 039 SQL commit (FEAT-OBI/MEDIPOL paket scope)
- Migration 040 clinic metadata commit (FEAT-CLINIC-METADATA paket scope)
- Backend prod deploy timing (Codex /rev PASS + commit + push sonrasi ayri ops adim, intentional_exclusion)
- UI smoke (manuel Q ~1dk, deploy sonrasi)

## Files Changed

| Dosya | Tip | Lines | Sebep |
|-------|-----|-------|-------|
| `src/Invekto.Backend/Dashboard/src/App.tsx` | M | +4 / -2 | redirect target + comment update |
| `src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx` | M | +4 / -2 | DEFAULT_BOARD_KEY + comment update |
| `src/Invekto.Backend/wwwroot/app/assets/PilotKanbanPage-qWgtl3HU.js` | NEW | 15.31 kB | vite build, "inse" baked in |
| `src/Invekto.Backend/wwwroot/app/assets/index-B-oMET1V.js` | NEW | 939.50 kB | vite build, App.tsx + main bundle |
| `src/Invekto.Backend/wwwroot/app/assets/index-Cm_shVTa.css` | NEW | — | css bundle |
| `src/Invekto.Backend/wwwroot/app/assets/PilotKanbanPage-DOP3TgG8.js` | DELETE | — | eski chunk (vite emptyOutDir) |
| 30+ chunk rename | RENAME | — | vite hash regenerate (modulepreload graph) |
| `src/Invekto.Backend/wwwroot/app/index.html` | M | script+css ref update | new hash |
| `arch/plans/20260429-feat-roadmap-board-rename-frontend.json` | NEW | plan JSON | |
| `tracking/20260429-feat-roadmap-board-rename-frontend.md` | NEW | bu dosya | |

**Toplam:** ~40 file, ~50 insertion / 46 deletion (cogu rename).

## Acceptance Criteria

| # | Kriter | Status |
|---|--------|--------|
| AC1 | PilotKanbanPage.tsx DEFAULT_BOARD_KEY rename + comment | PENDING |
| AC2 | App.tsx /pilot-kanban redirect target rename + comment | PENDING |
| AC3 | Dashboard SPA build PASS (vite 7.10s, 0 hata, asset hash'ler) | PASS ✅ |
| AC4 | PilotKanbanPage chunk grep "inse" MATCH + "dent-pilot" YOK | PASS ✅ |
| AC5 | Build N/A (.cs degismiyor — TSX + asset only, dotnet build skip) | PASS ✅ |
| AC6 | Backend prod deploy + UI smoke (default /yol-haritasi 'inse' 69 kart + /pilot-kanban redirect) | PENDING (post-Codex+commit) |

## Deploy Plan

1. **/rev Codex review** (MEDIUM risk per iter 1 escalation — CQ5 runtime frontend behavior + client routing change LOW guardrail violation)
2. Codex PASS sonrasi commit master (HEREDOC + Co-Authored-By)
3. **MCP invekto-ops server-deploy** Backend (publish + zip + upload + extract + NSSM restart)
4. /health HEALTHY verify
5. **UI smoke (~1dk):** Tarayicidan acilis `/yol-haritasi` -> "Yol Haritasi inse" title + 69 kart goruluyor + D029/D030/D031 DEV kategori filter altinda var. /pilot-kanban legacy URL deneme: /yol-haritasi/inse'a redirect olur.

## Rollback Plan

```bash
git revert HEAD
git push origin master
mcp__invekto-ops__server-deploy Backend  # eski PilotKanbanPage chunk geri gelir
```

Q manuel URL workaround `/yol-haritasi/inse` yine calisir (Migration 039 SQL board rename intact, sadece default route degisir).

## Cross-References

- **Migration 039 (FEAT-OBI/MEDIPOL):** SQL execute olmus prod'da (D027/D028 + board_key rename), frontend kismi bu paket
- **Migration 041 (FEAT-META D029/D030/D031):** kanban_cards INSERT + DO $verify$ (commit `967594f` + `ee6631d`), kart'lar inse board'da DB'de mevcut (Migration 041 wrap dogrulamasi 13:05 UTC)
- **Hunk isolation pattern:** Migration 041 wrap dance ile birebir uyumlu (backup + reset + re-apply + build + stage + restore)
- **Chunk: PilotKanbanPage-qWgtl3HU.js:** "inse" baked in (verify: `grep -oE '"(inse|dent-pilot)"'` MATCH "inse" only)

## Risk Analizi

- **Source change:** 4-line frontend constant + comment. Kod logic intact.
- **SPA rebuild:** vite emptyOutDir + new chunk hash'ler — cache busting otomatik
- **Backend deploy:** Standard NSSM restart + zip extract pattern
- **Rollback:** Git revert + redeploy ile eski chunk geri gelir, Q URL workaround intact
- **Risk: MEDIUM** (iter 1 Codex CQ5 escalation per LOW guardrail policy — runtime frontend behavior change + client routing target change. ADDITIVE only, kod logic intact, audit fix D035 line precedent)
