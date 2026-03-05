# FM-1a: Flow Versioning Backend

> **Durum:** DONE | **Tarih:** 5 Mar 2026 | **Codex:** iter 1, PASS
> **Spec:** `arch/specs/flow-monitor.md` | **Risk:** MEDIUM

## Kapsam

- `flow_versions` tablosu olusturma (migration 007)
- `chatbot_flows.current_version` kolonu
- Mevcut PUT save endpoint'ine versioning entegrasyonu (her save = yeni version)
- Versioning API endpoint'leri:
  - GET `/api/v1/flows/{tenantId}/{flowId}/versions` — surum listesi
  - GET `/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}` — surum detayi
  - POST `/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}/rollback` — geri al
- Flow builder UI'da aktif surum gosterimi: "v3 - 5 Mar 2026"
- Backend proxy route'lari (FlowBuilderClient)

## Acceptance Criteria

| # | Kriter | Durum |
|---|--------|-------|
| AC-9 | Her flow save'inde otomatik version artar | DONE |
| AC-10 | Flow builder'da aktif surum numarasi ve tarihi goruntulenir | DONE |
| AC-11 | Rollback: eski surume geri donebilme | DONE |
| AC-12 | Surum formati: "v1 - 5 Mar 2026" | DONE |

## DB Degisiklikleri

- `arch/db/flow-versions.sql` — Yeni tablo (source of truth)
- `arch/db/migrations/007-flow-versions.sql` — Migration (production'da calistirildi)
- `arch/db/automation.sql` — `current_version` kolonu eklendi

## Deliverables

- [x] Migration calistir (production)
- [x] AutomationRepository: version CRUD metotlari (CreateFlowVersionAsync, GetFlowVersionsAsync, GetFlowVersionAsync, RollbackFlowVersionAsync)
- [x] Save endpoint'ine version hook (non-fatal, NpgsqlException catch)
- [x] 3 yeni API endpoint (versions list, detail, rollback)
- [x] FlowBuilderClient proxy route'lari (3 proxy route)
- [x] Flow builder UI: surum badge + surum gecmisi dropdown

## Error Codes

- INV-AT-046: Version not found
- INV-AT-047: Version create failed
- INV-AT-048: Rollback failed

## Files Changed

- `src/Invekto.Shared/Constants/ErrorCodes.cs`
- `src/Invekto.Automation/Data/AutomationRepository.cs`
- `src/Invekto.Automation/Program.cs`
- `src/Invekto.Backend/Program.cs`
- `src/Invekto.Backend/Dashboard/src/lib/api.ts`
- `src/Invekto.Backend/Dashboard/src/pages/flow-builder/FlowEditorPage.tsx`
- `src/Invekto.Backend/Dashboard/src/pages/flow-builder/components/Toolbar.tsx`
- `arch/errors.md`
- `arch/db/automation.sql`
- `arch/db/flow-versions.sql`
- `arch/db/migrations/007-flow-versions.sql`
