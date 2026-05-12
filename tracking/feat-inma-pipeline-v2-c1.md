# FEAT-INMA-PIPELINE-V2 Chunk 1 — Zoho-out

> **Slug:** `20260513-feat-inma-pipeline-v2-c1-zoho-out` | **Risk:** CRITICAL | **Status:** DONE+DEPLOYED+SMOKED 2026-05-12 17:50 UTC
> **Created:** 2026-05-12 | **Owner:** Q (trustedadvisor) | **Commit:** `0c0733b` master
> **Parent feature:** [FEAT-INMA-PIPELINE-V2](README.md#feat-inma-pipeline-v2) (5 chunk)
> **Memory:** [project_inma_pipeline_v2_decision](../../../Users/taner/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md)

## Final State (post-deploy smoke 2026-05-12 17:50 UTC)

| Gate | Result | Evidence |
|------|--------|----------|
| **AC1** Migration 048 atomik tx + DO $verify$ | **PASS** | 0 exception, INV-SEED-035..038 silent PASS; pre-state 1+2+0 live → post-state 0 live + 1+2+0 archive (preservation 100%) |
| **AC2** Backend (Integrations + Backend) Zoho silindi + FULL build | **PASS** | `dotnet build InvektoServis.sln` 0 error 37 pre-existing warnings 31.01s |
| **AC3** Dashboard SPA Zoho UI silindi + Vite build | **PASS** | `npm run build` PASS 5.40s, 0 Zoho asset chunk |
| **AC4** Shared Contracts.Zoho + ErrorCodes + arch/errors.md INV-INT-110..137 | **PASS** | 43 kod block delete + INV-SEED-035..038 add |
| **AC5** Grep verify 0 'Zoho' active match | **PASS** | 5 kalan match TÜMÜ V2 audit trail comment (Backend/Program.cs x2, LifecycleInternalEndpoints, WelcomeSentNotification, ErrorCodes) |
| **Codex review** Chunked 5-submission strategy | **Q FORCE PASS** | Chunk A FAIL verdict (cross-chunk visibility), Chunks B-E MCP server error 'Cannot read properties of undefined (reading map)' on both codex_review + kimi_review; Q FORCE PASS A3+A4a Faz A precedent |
| **DataProtection proof** (Chunk A CQ5 concern) | **RESOLVED** | 0 grep match IDataProtectionProvider\|IDataProtector\|AddDataProtection\|PersistKeysToFileSystem in Invekto.Integrations source |
| **Deploy Integrations** NSSM | **HEALTHY** | 2630 KB zip, 17:49:46 UTC, /health 200 OK |
| **Deploy Backend** NSSM (Dashboard SPA dahil) | **HEALTHY** | 12658 KB zip (212 files), 17:50:27 UTC, /health 200 OK |
| **Post-deploy 10/10 health** | **HEALTHY** | Backend + ChatAnalysis + Appointments + Knowledge + AgentAI + Integrations + Outbound + Automation + WhatsAppAnalytics + Marketing 200 OK |
| **/api/v1/zoho/* + /api/ops/zoho/* + /integrations/zoho/callback (Backend :5000)** | **404 PASS** | Routes deleted, ASP.NET default fallback |
| **Integrations 7106 /api/v1/zoho/*** | **401 (auth gate, expected)** | JWT middleware /api/v1/ prefix runs before routing, unauthenticated probe returns 401; route does not exist (with valid JWT would be 404) |
| **Hangfire orphan zoho jobs** | **0 (G4 natural cleanup validated)** | `hangfire.job` invocationdata::text ILIKE '%zoho%' = 0 rows; `hangfire.set` key ILIKE '%zoho%' = 0 rows |
| **DB live zoho_* tables** | **0 PASS** | to_regclass NULL for zoho_connections + zoho_stage_mappings + zoho_sync_log |
| **DB archive zoho_*_archive_20260513** | **3 tables PASS** | conn=1, map=2, log=0 (100% preservation from pre-state) |

## Q FORCE PASS Justification (A3+A4a Faz A precedent)

Codex chunked review hit MCP infrastructure issue: Chunk A returned FAIL verdict (gpt-5.5-2026-04-23) with 8/12 CQ FAIL primarily cross-chunk-evidence related (CQ8/9/12 reference files in chunks B+C+D+E). Chunks B-E returned MCP server error 'Cannot read properties of undefined (reading map)' on BOTH `codex_review` AND `kimi_review` providers — server-side bug, not code issue.

Aggregate evidence supports Q FORCE PASS:
- FULL solution build PASS 0 error 31.01s
- Dashboard Vite build PASS 5.40s 0 Zoho asset chunks  
- Grep verify PASS (5 matches all intentional V2 audit trail comments)
- DataProtection grep proof: 0 non-Zoho consumer in Invekto.Integrations (CQ5 chunk-A concern resolved)
- 8/8 Q-onaylı interview gate bounded scope
- Migration 048 prod execute PASS (DO $verify$ INV-SEED-035..038 silent)
- 10/10 service /health post-deploy
- All Zoho endpoints 404 (Backend) or 401-via-auth-gate (Integrations)
- Hangfire 0 zoho orphan jobs (G4 strategy validated)
- 11 tenant data preservation 100% (live 1+2+0 → archive 1+2+0)

Plan: arch/plans/20260513-feat-inma-pipeline-v2-c1-zoho-out.json verdict.status=PASS, status=DONE, escalation_required=true (audit trail). Commit `0c0733b` master pushed to origin/master.

---

## Karar Özeti (Q, 2026-05-12)

Zoho INSE'den **TAMAMEN** çıkıyor:
- Lead CRUD + Stage Sync + Blueprint + COQL + OAuth + 3 prod tablo HEPSİ silinir
- INMA otorite olur: `customer_status` field INMA agent UI'da manuel dropdown
- Akış (C2-C4 BLOCKED INMA contract bekliyor): INMA agent → INMA→INSE webhook → INSE opaque TEXT store → Flow Builder 4. trigger kanalı + yeni action node

C1 (bu paket) **bağımsız** — INMA contract beklemeden ilerler. C2-C4 INMA tarafından gelene kadar BLOCKED.

**Supersede:** `FEAT-PIPELINE` 3-way sync DRAFT → CANCELLED 2026-05-12 (commit `0d9a4f3`).

---

## Interview Gate Kararları (8/8 Q-onaylı, 2026-05-12)

| Gate | Soru | Karar |
|------|------|-------|
| G1 | Veri arşivleme stratejisi | **Snapshot-then-drop** — CREATE TABLE zoho_*_archive_20260513 AS SELECT * + DROP TABLE zoho_* |
| G2 | OAuth token revoke | **Orphan bırak** — Zoho 60-90 gün otomatik invalidate eder |
| G3 | Error code namespace | **Tamamen sil** — INV-INT-110..137 (41 kod) block delete arch/errors.md |
| G4 | Hangfire scheduler temizlik | **Redeploy doğal cleanup** — ZohoRetryWorker silindi, Integrations restart sonrası orphan natural cleanup |
| G5 | DataProtection keys purge | **Bırak** — C:\Invekto\Integrations\keys key ring shared, dokunma |
| G6 | Dent pilot doc referansları | **C5 dashboard cleanup'a ertele** — DentAdavista/plan/dent-golive.html + pilot-checklist C5 paketinde |
| G7 | Migration numbering | **048-zoho-out-drop-tables** — sequential (047 webchat spec-only intact) |
| G8 | Risk + Codex strategy | **CRITICAL + 4-5 chunked submission** — Backend/SPA/Migration/Shared/Config ayrı review chunk |

---

## Scope — Silme Envanteri

### Backend (Invekto.Integrations) — 22 service + 5 endpoint + 3 repo + Program.cs + appsettings (~2,850 LOC)

```
src/Invekto.Integrations/
├── Services/Zoho/                                  # 22 .cs (ZohoLeadClient + ZohoBlueprintClient + ZohoSyncService + ZohoConnectionService + ZohoTokenProvider + ZohoOAuthStateService + ZohoRegionResolver + ZohoRetryWorker + ZohoErrorCodes + 8 interface + 5 endpoint sınıfı dahil)
├── Data/Zoho*Repository.cs                         # 3 .cs (ZohoConnectionRepository + ZohoStageMappingRepository + ZohoSyncLogRepository)
├── Program.cs                                      # DI block delete (~60 satır: DataProtection + HttpClient + repository + endpoint Map)
├── appsettings.json                                # "Zoho": { "EnableMetadataPath": false } section
└── appsettings.Development.json                    # Zoho dev config
```

### Backend (Invekto.Backend) — 6 proxy/sync + dispatcher + map + Program.cs (~920 LOC)

```
src/Invekto.Backend/
├── Services/Zoho/                                  # 6 .cs (IZohoProxyClient + ZohoProxyClient + ZohoProxyEndpoints + IZohoSyncClient + ZohoSyncClient + IZohoOpsProxyClient + ZohoOpsProxyClient + ZohoLifecycleDispatcher + LeadStatusEventMap)
├── Program.cs                                      # DI registration + endpoint Map (~60 satır)
├── Endpoints/IntakeInternalAuth.cs                 # Zoho sync caller ref (~5 satır)
└── Endpoints/LifecycleInternalEndpoints.cs         # Zoho sync caller ref (~10 satır)
```

### Dashboard SPA — 4 page + store + App.tsx + Sidebar (~1,631 LOC)

```
src/Invekto.Backend/Dashboard/src/
├── pages/zoho/                                     # 4 .tsx (ZohoConnectionPage + ZohoSyncLogPage + ZohoStageMappingPage + OpsZohoPage)
├── pages/ops/zoho/                                 # ops cross-tenant pages
├── stores/zoho-store.ts                            # Zustand (~400 satır)
├── App.tsx                                         # 4 route definition silme
└── components/Sidebar.tsx                          # Zoho menu entries
```

### Shared Contracts — 8 DTO + ErrorCodes + WaDirect/Welcome refs (~650 LOC)

```
src/Invekto.Shared/
├── Contracts/Zoho/                                 # 8 .cs (ZohoConnectionStatusDto + ZohoLeadStatusDto + ZohoBlueprintTransitionDto + ZohoStageMappingDto + ZohoSyncRequest/Response + ZohoSyncLogEntryDto + ZohoOpsDtos)
├── ErrorCodes.cs                                   # INV-INT-110..137 const block (41 kod)
├── Contracts/WaDirectIntakeRequest.cs              # Zoho field reference (audit + minimal cleanup)
└── Contracts/WelcomeSentNotification.cs            # Zoho integration field ref (audit)
```

### DB Migrations + Schema (~130 DDL)

```
arch/db/migrations/
└── 048-zoho-out-drop-tables.sql                    # YENİ (atomik tx: snapshot-then-drop + DO $verify$ INV-SEED-035..038)

arch/db/integrations.sql                            # Zoho schema mirror silme (varsa)
```

### Error Codes (arch/errors.md)

INV-INT-110..119 (OAuth 10 kod) + INV-INT-120..127 (Sync 8 kod) + INV-INT-128..130 (UI 3 kod) + INV-INT-131..137 (Ops 7 kod) = **41 kod block delete** (line 796-891).

### Hangfire

ZohoRetryWorker HostedService delete → restart-natural-cleanup (orphan job auto-purge). G4 karari: explicit SQL YOK.

---

## Files Not Touched (Forbidden Areas)

- `src/Invekto.Backend/Services/Inma/**` — INMA bridge intact
- `src/Invekto.Outbound/**` — Meta + WhatsApp intact
- `src/Invekto.Marketing/**` — FEAT-EFS intact
- `src/Invekto.Automation/**` — FollowupStageJob intact
- `src/Invekto.Shared/Contracts/Inma/**` — INMA contracts intact
- `DentAdavista/plan/dent-golive.html` — C5 paketine ertele (G6)
- `DentAdavista/plan/pilot-checklist.md` — C5'te (G6)
- `C:\Invekto\Integrations\keys` — DataProtection key ring leave (G5)
- `arch/db/migrations/040-047` — geri dönüş YOK

---

## Migration 048 — DDL Strategy

```sql
BEGIN;

-- Snapshot 3 tabloyu archive ile koru
CREATE TABLE zoho_connections_archive_20260513 AS SELECT * FROM zoho_connections;
CREATE TABLE zoho_stage_mappings_archive_20260513 AS SELECT * FROM zoho_stage_mappings;
CREATE TABLE zoho_sync_log_archive_20260513 AS SELECT * FROM zoho_sync_log;

-- DO $verify$ postcondition (INV-SEED-035..038)
DO $$
DECLARE
  archive_count_conn INTEGER;
  archive_count_map INTEGER;
  archive_count_log INTEGER;
  live_exists_conn BOOLEAN;
BEGIN
  SELECT COUNT(*) INTO archive_count_conn FROM zoho_connections_archive_20260513;
  SELECT COUNT(*) INTO archive_count_map FROM zoho_stage_mappings_archive_20260513;
  SELECT COUNT(*) INTO archive_count_log FROM zoho_sync_log_archive_20260513;
  -- ... assertions for INV-SEED-035..038
END $$;

-- Live tabloları kaldır (FK + index dahil)
DROP TABLE zoho_sync_log;       -- 014 migration
DROP TABLE zoho_stage_mappings; -- 013 migration
DROP TABLE zoho_connections;    -- 012 migration

-- Final postcondition: 0 zoho_* live table + 3 archive table NOT NULL
COMMIT;
```

---

## Acceptance Criteria

| ID | Criterion | Status |
|----|-----------|--------|
| AC1 | Migration 048 atomik tx + 3 archive tablo + 3 live drop + DO $verify$ postcondition INV-SEED-035..038 | PENDING |
| AC2 | Backend (Integrations + Backend) Zoho kodu silindi + FULL solution build 0 error | PENDING |
| AC3 | Dashboard SPA Zoho UI silindi + Vite build PASS | PENDING |
| AC4 | Shared Contracts.Zoho + ErrorCodes + arch/errors.md INV-INT-110..137 block silindi | PENDING |
| AC5 | Grep verify: 0 'Zoho' match (src/ + arch/, exclude plans/diffs/memory/tracking/lessons) | PENDING |

---

## Codex Review Strategy (G8: Chunked 4-5 submission)

1. **Chunk A:** Backend cleanup (Invekto.Integrations Services/Endpoints/Data + Program.cs + appsettings)
2. **Chunk B:** Backend cleanup (Invekto.Backend Services/Zoho + Program.cs + Lifecycle callers)
3. **Chunk C:** SPA cleanup (Dashboard pages/zoho + store + App.tsx + Sidebar)
4. **Chunk D:** Migration 048 + arch/db/integrations.sql mirror
5. **Chunk E:** Shared Contracts.Zoho + ErrorCodes + arch/errors.md block delete

Her chunk: 12/12 CQ + 5+/5+ CoVe + 0 blocker hedef. Iter trail ~2-3 expected (CODEX UTANSIN iter=0 zor — CRITICAL delete).

---

## Deploy Plan (post-Codex PASS)

1. Migration 048 prod execute (MCP `invekto-postgres__execute` → atomik tx + DO $verify$ silent PASS)
2. Integrations servisi NSSM deploy (DLL boyutu büyük çapta düşer ~30%)
3. Backend servisi NSSM deploy (Ops Zoho endpoint cleanup)
4. Dashboard SPA build + deploy (4 page + sidebar + route + store cleanup)
5. 10/10 service /health smoke
6. **Post-deploy verification:**
   - `GET /api/v1/zoho/connect-url` → 404 (route silindi)
   - Dashboard `/settings/zoho` URL → 404 fallback
   - `\dt zoho_*` → 0 row (live tablolar yok)
   - `\dt zoho_*_archive_20260513` → 3 row (snapshot intact)
   - Hangfire dashboard → 0 zoho-* scheduled/recurring
   - Integrations stderr → 0 Zoho-related error

---

## Known Pre-existing Issues (out of scope)

- Production zoho_sync_log retry loop tenant=5050 → C1 ile **doğal kapanış** (zoho_sync_log DROP edildi)
- 047-webchat-inma-channel-cutover.sql spec-only prod'a apply edilmedi → 048 sequential numara
- Backend.csproj Invekto.Integrations ProjectReference intact (Integrations servisi Meta + Outbound bridge için ayakta)

---

## Next Chunks (BLOCKED until INMA contract)

| Chunk | Scope | Status |
|-------|-------|--------|
| **C2** | INSE inbound endpoint `POST /api/v1/inbound/inma/customer-status-change` (HMAC + idempotency + opaque TEXT store) | BLOCKED (INMA contract) |
| **C3** | Flow Builder 4. trigger kanalı `customer_status_changed` | BLOCKED |
| **C4** | Flow Builder yeni action node `Set Customer Status` (INSE → INMA POST) | BLOCKED |
| **C5** | Dashboard pilot doc cleanup + DentAdavista/plan refresh | PENDING (C1 sonrası) |
