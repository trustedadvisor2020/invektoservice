# Yol Haritası Audit Batch B — Migration 036/037 Retroaktif Idempotency Fix

> **Slug:** `20260429-feat-roadmap-audit-batch-b`
> **Status:** DONE (retroaktif source file edit; prod DB dokunulmaz)
> **Risk:** LOW (SQL only, build N/A, service restart gerek yok)
> **Plan:** [`arch/plans/20260429-feat-roadmap-audit-batch-b.json`](../arch/plans/20260429-feat-roadmap-audit-batch-b.json)
> **Audit kaynak:** `c:/tmp/tmp-roadmap-audit-next-session-handoff.md` Paket 2 (D028 + D029)

## Özet

Audit Paket 2 (handoff §): Migration 036/037 retroaktif idempotency + postcondition guard fixleri. Mevcut prod schema dokunulmaz; sadece source file content edit yapılır — re-run/DR senaryosunda davranış düzelir.

## Q Kararları (Batch A pre-req'lerinden)

| # | Soru | Karar |
|---|------|-------|
| G2 | Migration retroaktif edit policy | **Retroaktif edit OK** — fix-forward only alternatif reddedildi (2x kod + schema history kalabalıklık) |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | Migration 036 §3 partial UNIQUE INDEX guard `pg_constraint` → `pg_class WHERE relname AND relkind='i'` | ✅ Source edit (036:46-58) |
| AC2 | Migration 037 §4 postcondition `deps_count < 13` RAISE EXCEPTION sessiz başarısızlık guard | ✅ Source edit (037:75-90) |
| AC3 | Migration 037 docstring "22 kart" → "13 kart" sayım düzeltme | ✅ Source edit (037:11) |
| AC4 | Mevcut prod schema dokunulmaz (sadece SQL source file edit) | ✅ No DB execute; commit 2b7da89/2cc6d90 prod state korunuyor |

## Düzeltilen Buglar

### D028 — Migration 036 §3 Partial UNIQUE INDEX Guard

**Bug:** `pg_constraint WHERE conname='uq_kanban_cards_board_ref'` lookup yanlış.

Postgres docs: partial UNIQUE INDEX (`WHERE` clause'lu) **CONSTRAINT değil**, sadece **INDEX**. `pg_constraint` NEVER has this entry → `IF NOT EXISTS` HER ZAMAN TRUE → `CREATE UNIQUE INDEX` her zaman çalışmaya çalışır → re-run/DR durumunda **"relation already exists"** patlatır.

**Fix:** `pg_class WHERE relname='uq_kanban_cards_board_ref' AND relkind='i'` doğru lookup.

```sql
-- ÖNCESİ
IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_kanban_cards_board_ref') THEN
    CREATE UNIQUE INDEX ...
END IF;

-- SONRASI
IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = 'uq_kanban_cards_board_ref' AND relkind = 'i') THEN
    CREATE UNIQUE INDEX ...
END IF;
```

### D029 — Migration 037 §4 Postcondition Sessiz Başarısızlık Guard

**Bug:** Postcondition sadece `invalid_count > 0` (regex bozuk) kontrol ediyordu. UPDATE statement'lar matched 0 row scenario'da `deps_count = 0` ama `invalid_count = 0` → silent PASS. DR senaryosunda kart bağımlılıkları kayar, kimse fark etmez.

**Fix:** `IF deps_count < 13 THEN RAISE EXCEPTION` guard ekle.

```sql
-- §4 expanded with deps_count guard
IF deps_count < 13 THEN
    RAISE EXCEPTION '[INV-SEED-027] kanban_cards depends_on yetersiz: % satir dolu, en az 13 bekleniyordu', deps_count;
END IF;
```

**Bonus:** Docstring "22 kart" → "13 kart" (line 11 sayım düzeltme; gerçek UPDATE sayısı 7+3+2+1=13).

## Files Changed

```
arch/db/migrations/036-kanban-ref-code.sql       [MOD] D028 partial UNIQUE INDEX guard
arch/db/migrations/037-kanban-depends-on.sql     [MOD] D029 postcondition deps_count guard + docstring
arch/plans/20260429-feat-roadmap-audit-batch-b.json [NEW]
tracking/20260429-feat-roadmap-audit-batch-b.md  [NEW] Bu dosya
```

## Build / Deploy

- Build: **N/A** (SQL only retroaktif edit, .NET pipeline etkilenmiyor)
- Service deploy: **GEREK YOK** (mevcut prod schema dokunulmaz; backend kod değişikliği yok)
- DB execute: **GEREK YOK** (mevcut prod state zaten doğru: Migration 036 commit 2b7da89 + 037 commit 2cc6d90 prod'da DEPLOYED)

## Doğrulama

Re-run/DR senaryosunda:
- Migration 036 yeniden çalıştırılırsa: `pg_class` lookup mevcut INDEX'i bulur → IF NOT EXISTS FALSE → skip silently (eski kod patlatırdı)
- Migration 037 partial DB seed senaryosu: `deps_count < 13` RAISE EXCEPTION ile fail-loud

## Notes

- Mevcut prod (commit 2cc6d90) deps_count = 13 → yeni guard PASS, davranış değişmez
- D028/D029 audit handoff'tan; D027 (ops auth) Batch D'de
- 035 K prefix docstring (handoff #11) Batch A non-goal Q kararı; bu paket scope dışı
