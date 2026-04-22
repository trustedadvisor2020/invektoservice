# P8 — Prod Bak Dosyalari Cleanup (INMA Bypass + VCP/P-checkpoint)

> **Slug:** `20260427-prod-bypass-bak-remove` | **Faz:** 4 (Cleanup) | **Risk:** LOW (ops-only)
> **Roadmap:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md) P8
> **Plan JSON:** [`arch/plans/20260427-prod-bypass-bak-remove.json`](../arch/plans/20260427-prod-bypass-bak-remove.json)
> **Durum:** DONE+DEPLOYED 2026-04-22 13:35 UTC — Codex iter 0 PASS (12/12 CQ + 3/3 CoVe, 0 blocking) 2026-04-22 13:42 UTC

## Scope

Prod sunucudan 6 stale `appsettings.Production.json.bak-*` dosyasini sil. Kod degisikligi yok, build yok, deploy yok, service restart yok. Repo tarafinda 5 dosya guncellenir.

### Silinecek Dosyalar (6)

| # | Path | Boyut | Timestamp | Baglam |
|---|------|-------|-----------|--------|
| 1 | `C:\Invekto\Backend\appsettings.Production.json.bak-inma-companycode` | 3001 B | 2026-04-16 20:25 | INMA companycode investigation — **P8 roadmap hedef dosyasi** (roadmap'te `bak-20260416-inma-bypass` varyanti olarak yazilmis, exact isim `bak-inma-companycode`) |
| 2 | `C:\Invekto\Backend\appsettings.Production.json.bak-20260419-precheck` | 3001 B | 2026-04-18 23:17 | Pre-deploy pre-check snapshot |
| 3 | `C:\Invekto\Appointments\current\appsettings.Production.json.bak-20260420-170856-vcp-chunk-b` | 1001 B | 2026-04-13 19:47 | VCP Chunk B pre-deploy snapshot |
| 4 | `C:\Invekto\Appointments\current\appsettings.Production.json.bak-20260420-171005-add-shared-secret` | 1001 B | 2026-04-20 14:09 | Add SharedSecret config snapshot |
| 5 | `C:\Invekto\Integrations\appsettings.Production.json.bak-20260419-vcp-chunk-a` | 2849 B | 2026-04-19 00:17 | VCP Chunk A pre-deploy snapshot |
| 6 | `C:\Invekto\Integrations\current\appsettings.Production.json.bak-20260420-170923-vcp-chunk-b` | 2849 B | 2026-04-19 00:17 | VCP Chunk B pre-deploy snapshot |

### Scope Disindaki Stale Bak'lar (Intentional Exclusion)

Ayri cleanup paketi icin bekliyor:
- `C:\Invekto\scripts\tmp\Automation-appsettings-bak.json`
- `C:\Invekto\scripts\tmp\Backend-appsettings-bak.json`
- `C:\Invekto\scripts\tmp\Outbound-appsettings-bak.json`
- `C:\Invekto\staging\*.bak` (5 dosya, 2026-04-13 eski baseline)
- `C:\Invekto\_staging\*.bak-20260418-142252` (3 dosya, pre-VCP baseline)
- `C:\Invekto\WebChat\appsettings.Production.json.bak` (2026-03-02, eski WebChat context)

## AC

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC1 | 6 hedef bak dosyasi MCP `invekto-ops server-exec Remove-Item` ile silindi | MCP exec output |
| AC2 | Silme sonrasi `Test-Path` her 6 dosya icin False, aktif `appsettings.Production.json` dosyalari degismedi | MCP `Test-Path` batch |
| AC3 | 10 servis HEALTHY (NSSM bak dosyalarini okumaz, ama safety probe) | MCP `server-health all` |
| AC4 | Repo 5 dosya guncellendi: plan JSON + tracking (bu dosya) + roadmap (P8 Status=DONE) + session-memory + lessons-learned | git diff |
| AC5 | Codex review LOW iter 0 PASS (CODEX UTANSIN) + commit + push master | `/rev` verdict + git log |

## Scope Discipline

**Touchable (prod):**
- 6 listelenen bak dosyasi (silme)

**Touchable (repo):**
- `arch/plans/20260427-prod-bypass-bak-remove.json`
- `tracking/20260427-prod-bypass-bak-remove.md` (bu dosya)
- `tracking/pilot-launch-roadmap.md` (P8 Status=DONE)
- `arch/session-memory.md`
- `arch/lessons-learned.md` (1 yeni entry: prod bak stale cleanup)

**Forbidden:**
- Kod dosyalari (.cs, .ts, .sql, .tsx)
- Aktif `appsettings.Production.json` icerikleri
- `scripts/tmp/` + `staging/` + `_staging/` + `WebChat/` bak dosyalari (ayri paket)
- NSSM service restart / binary redeploy
- `arch/errors.md` / `ErrorCodes.cs`

## Rollback Plan

Silme sonrasi regression tespit edilirse:
1. Git history: `appsettings.Production.json` Config Preserve Sandwich ile git'e senk (aktif dosya otoritatif)
2. INMA bypass specifics: commit `2b078b2` (2026-04-18) ve oncesi
3. VCP Chunk A/B: migrations 023/024 + commit history
4. Redeploy (gerekirse): `/deploy` skill ile full publish + config sandwich

**Bak dosyalari rollback icin gerekmedi** (P7 INMA Debug Log Cleanup 2026-04-18'den beri hic rollback yapilmadi, FEAT-MCC P6'ya kadar tum paketler forward-only).

## Deploy

Yok (prod file op only). NSSM service restart yok. Binary redeploy yok. Health probe AC3 icin calisiyor ama zorunlu degil (bak dosyalari runtime'a etkisiz).

## Execution Log

### Prod Remove-Item (2026-04-22 13:33 UTC)

MCP `invekto-ops server-exec` tek batch ile her 6 dosya icin `Remove-Item -Force -ErrorAction Stop` + Test-Path verify:

```
DELETED | C:\Invekto\Backend\appsettings.Production.json.bak-inma-companycode
DELETED | C:\Invekto\Backend\appsettings.Production.json.bak-20260419-precheck
DELETED | C:\Invekto\Appointments\current\appsettings.Production.json.bak-20260420-170856-vcp-chunk-b
DELETED | C:\Invekto\Appointments\current\appsettings.Production.json.bak-20260420-171005-add-shared-secret
DELETED | C:\Invekto\Integrations\appsettings.Production.json.bak-20260419-vcp-chunk-a
DELETED | C:\Invekto\Integrations\current\appsettings.Production.json.bak-20260420-170923-vcp-chunk-b
```

### Post-Delete Verify (2026-04-22 13:34 UTC)

**Deleted files (6/6 GONE):**
- All 6 target bak files: `Test-Path = False`

**Active files intact (3/3 OK):**
- `C:\Invekto\Backend\current\appsettings.Production.json` 3001B 2026-04-18T23:18 (pre-delete timestamp, unchanged)
- `C:\Invekto\Appointments\current\appsettings.Production.json` 1172B 2026-04-20T14:10 (pre-delete, unchanged)
- `C:\Invekto\Integrations\current\appsettings.Production.json` 2849B 2026-04-20T14:09 (pre-delete, unchanged)

### Health Probe (2026-04-22 10:26 UTC server clock)

MCP `server-health all` → 10/10 OK:
- Backend :5000 OK
- ChatAnalysis :7101 OK
- Appointments :7102 OK
- Knowledge :7104 OK
- AgentAI :7105 OK
- Integrations :7106 OK
- Outbound :7107 OK
- Automation :7108 OK
- WhatsAppAnalytics :7109 OK
- Marketing :7112 OK

## Codex Verdict

- **Iter 0: PASS** (12/12 CQ + 3/3 CoVe, 0 blocking) — 2026-04-22 13:42 UTC
- **Model:** gpt-5.4-2026-03-05
- **Tokens:** 18906 (17210 prompt + 1696 completion)
- **Summary:** "Diff is documentation-only and stays within declared plan scope; no code, schema, auth, tenant, or microservice behavior changed. Provided plan/tracking evidence is internally consistent for the 6 backup-file deletions, active config preservation, and documented rollback path."
- **CODEX UTANSIN iter=0 hedef tutuldu.**
