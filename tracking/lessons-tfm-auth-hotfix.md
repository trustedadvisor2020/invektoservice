# P2 — Lessons +4 TFM AUTH-HOTFIX + 1 inline update

> **Slug:** `20260422-lessons-tfm-auth-hotfix` | **Faz:** 1 | **Risk:** LOW (doc-only)
> **Roadmap:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md) P2
> **Plan JSON:** [`arch/plans/20260422-lessons-tfm-auth-hotfix.json`](../arch/plans/20260422-lessons-tfm-auth-hotfix.json)
> **Durum:** DONE — Codex iter 0 PASS (12/12 CQ + 3/3 CoVe, 0 blocking) 2026-04-21 23:42 UTC

## Scope

`arch/lessons-learned.md`'ye 4 yeni entry + 1 inline update. Doc-only. Build N/A. Deploy yok.

### Yeni Entries (2026-04-21 TFM AUTH-HOTFIX + P1 retro-fix kaynagi)

| # | Mistake | Kaynak |
|---|---------|--------|
| A | `ctx.Items["TenantId"]` latent bug — 6 handler wrong key okuyordu (middleware `Items["TenantContext"]` object setliyor); grep write match = 0 instant detection | TFM AUTH-HOTFIX 2026-04-21 |
| B | `jwtRequiredPrefixes` registration audit — tenant-scoped handler eklerken prefix registration zorunlu; aksi middleware path-skip sessiz fail | TFM AUTH-HOTFIX 2026-04-21 |
| C | Deploy smoke 401 expected test 3-tier probe — NoAuth middleware-401 vs handler-401 "Tenant context not available" differentiate edilmeli | TFM AUTH-HOTFIX 2026-04-21 |
| D | `InmaDynamicFieldsCacheTests.Invalidate_DuringInflight` race test non-deterministic → kaldirildi; defensive XML doc + prod HttpClient async-boundary guarantee yeterli; race-test-infrastructure lesson | P1 FEAT-DMP Cache Poison Fix 2026-04-21 |

### Inline Update

Satir 17 (cancellation poison lesson, 2026-04-21): "FEAT-DMP InmaDynamicFieldsCache ayni bug — retro-fix paket onerisi" → "retro-fix uygulandi commit `ca2d2d5` 2026-04-21 (P1 pilot-launch-roadmap)".

## AC

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC1 | 4 yeni entry eklendi, mevcut prose format korundu | git diff lessons-learned.md |
| AC2 | Satir 17 inline update uygulandi | git diff lessons-learned.md |
| AC3 | Kronoloji dogru (2026-04-21 uste, eski entries etkilenmedi) | diff scan |
| AC4 | Codex review LOW PASS (hedef iter 0) | `/rev` verdict |

## Scope Discipline

**Touchable:**
- `arch/lessons-learned.md`
- `arch/plans/20260422-lessons-tfm-auth-hotfix.json`
- `arch/session-memory.md`
- `tracking/pilot-launch-roadmap.md` (P2 Status=DONE guncelleme)
- `tracking/lessons-tfm-auth-hotfix.md` (bu dosya)

**Forbidden:**
- Kod dosyalari (.cs, .ts, .sql)
- `arch/errors.md` / `ErrorCodes.cs`
- Mevcut lessons entries satir 19+ (sadece satir 17 inline update)
- Archive file (B4 BACKLOG)

## Deploy

Yok. Doc-only. Commit + push master yeterli.

## Codex Verdict

- **Iter 0: PASS** (12/12 CQ + 3/3 CoVe, 0 blocking) — model gpt-5.4-2026-03-05, tokens 15879.
- Summary: "Doc-only diff is within declared scope, preserves the established lessons-learned formatting, and the inline update is minimally scoped."
