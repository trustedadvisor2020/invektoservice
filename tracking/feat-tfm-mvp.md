# FEAT-TFM MVP — Tenant Field Mapping (Resolver + Config CRUD + DI Swap)

> **Paket:** FEAT-TFM-MVP | **Risk:** MEDIUM | **Plan:** [arch/plans/20260421-feat-tfm-resolver-mvp.json](../arch/plans/20260421-feat-tfm-resolver-mvp.json)
> **Spec:** [arch/features/tenant-field-mapping.md](../arch/features/tenant-field-mapping.md)
> **Açılış:** 2026-04-21 | **Durum:** IN_PROGRESS (code complete, pending Codex /rev)

## Scope

MVP scope (Q interview 2026-04-21):
- ✅ `tenant_settings.field_mapping JSONB` + `leads.custom_1..custom_10 TEXT` migration (028)
- ✅ Shared: `DbTenantFieldMappingResolver` (IMemoryCache 5dk TTL + single-flight) + `TenantFieldMappingValidator` + `TenantFieldMappingValidationException` + `TenantFieldMappingEntry` DTO
- ✅ ErrorCodes INV-BE-096..099
- ✅ Backend: `TenantSettingsRepository` + `TenantFieldMappingEndpoints` (GET/PUT /api/v1/tenant-settings/field-mapping, tenant-scoped JWT)
- ✅ DI swap Backend/Outbound/Automation (Null→Db)
- ✅ DMP backward-compat intact (mapping yoksa null → raw INMA key allowlist fallback)

Out of scope (sonraki paketler):
- ❌ Dashboard UI editor → FEAT-TFM-UI
- ❌ INMA → INSE custom field sync worker (webhook/polling) → FEAT-TFM-SYNC
- ❌ FlowBuilder/TemplateCreate semantic picker UI → FEAT-TFM-FLOW
- ❌ Multi-instance cache cross-invalidation (Redis pub/sub) → FEAT-TFM-CACHE

## Acceptance Criteria Checklist (13 AC)

- [ ] AC1: Migration 028 idempotent + canonical mirrors
- [ ] AC2: TenantFieldMappingEntry DTO (snake_case JSONB)
- [ ] AC3: Validator reserved guard (InmaDynamicFieldKeys.Allowlist ∪ leads core cols)
- [ ] AC4: ErrorCodes INV-BE-096..099 + arch/errors.md
- [ ] AC5: DbTenantFieldMappingResolver + IMemoryCache 5dk + single-flight + typed catch fail-safe null
- [ ] AC6: Backend/Data/TenantSettingsRepository (GET + UPSERT)
- [ ] AC7: GET endpoint (TenantContext.TenantId, body-tenant ALMA)
- [ ] AC8: PUT endpoint (validate + UPSERT + local cache invalidate + multi-instance eventual consistency note)
- [ ] AC9: DI swap 3 servis (Null dosyası korunur, intentional)
- [ ] AC10: FEAT-DMP backward-compat (mapping yoksa null → raw INMA key)
- [ ] AC11: arch/features + tracking + codex-context.md updates
- [ ] AC12: Build PASS, yeni warning YOK
- [ ] AC13: Microservice isolation korundu (3 servis kendi DB, cross-service reference yok)

(AC'ler Codex /rev sırasında doğrulanacak)

## Deliverables

| Tip | Dosya | Durum |
|-----|-------|-------|
| Migration | arch/db/migrations/028-tenant-field-mapping.sql | ✅ |
| Mirror | arch/db/tenant-settings.sql | ✅ (field_mapping col) |
| Mirror | arch/db/pkt6b1-niche-business.sql | ✅ (custom_1..10 cols) |
| Shared DTO | Invekto.Shared/Contracts/TenantFieldMapping/Dtos/TenantFieldMappingEntry.cs | ✅ |
| Shared Exception | Invekto.Shared/Contracts/TenantFieldMapping/TenantFieldMappingValidationException.cs | ✅ |
| Shared Validator | Invekto.Shared/Services/TenantFieldMappingValidator.cs | ✅ |
| Shared Resolver | Invekto.Shared/Contracts/TenantFieldMapping/DbTenantFieldMappingResolver.cs | ✅ |
| Shared Const | Invekto.Shared/Constants/ErrorCodes.cs (INV-BE-096..099) | ✅ |
| Backend Repo | Invekto.Backend/Data/TenantSettingsRepository.cs | ✅ |
| Backend Endpoints | Invekto.Backend/Endpoints/TenantFieldMappingEndpoints.cs | ✅ |
| Backend DI | Invekto.Backend/Program.cs (DI swap + endpoint map + discovery list) | ✅ |
| Outbound DI | Invekto.Outbound/Program.cs (Null→Db + AddMemoryCache) | ✅ |
| Automation DI | Invekto.Automation/Program.cs (Null→Db) | ✅ |
| Docs | arch/errors.md (INV-BE-096..099) | ✅ |
| Docs | arch/features/tenant-field-mapping.md (status update) | ✅ |
| Docs | arch/codex-context.md (intent guidance) | ✅ |
| Tracking | tracking/README.md master satır | ✅ |
| Tracking | tracking/feat-tfm-mvp.md (bu dosya) | ✅ |

## Build Status

- **Build:** PASS (0 error, 17 warning — hepsi pre-existing, FEAT-TFM yeni warning eklemedi)
- **Solution:** `powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"`

## Next Steps

1. Self-review CQ1-CQ8 + AQ1-AQ6 (CODEX UTANSIN hedefi: iteration=0)
2. `/rev` (MCP Codex review) MEDIUM risk mandatory
3. Verdict PASS → commit + deploy planning (Q decision)
4. Deploy: migration 028 + Backend/Outbound/Automation publish + NSSM restart (aynı DMP deploy pattern)
