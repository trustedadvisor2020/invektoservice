# P3 — FEAT-TFM-UI Dashboard Editor

> **Slug:** `20260423-feat-tfm-ui-editor` | **Faz:** 2 | **Risk:** MEDIUM
> **Roadmap:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md) P3
> **Plan JSON:** [`arch/plans/20260423-feat-tfm-ui-editor.json`](../arch/plans/20260423-feat-tfm-ui-editor.json)
> **Durum:** DONE+DEPLOYED+SMOKED 2026-04-22 12:12 UTC — Codex iter 1 PASS, Backend redeploy 10/10 HEALTHY, SPA bundle `FieldMappingSettingsPage-D2v_4qCv.js` 13,269 bytes prod'da, auth gate 401/401 (middleware reachable).

## Scope

Dashboard standalone `/settings/field-mapping` sayfa. 10-row tablo grid (cf1..cf10), INMA Label entegrasyonu, inline editable row, form-level save, live duplicate+reserved validation. Backend endpoints (GET/PUT `/api/v1/tenant-settings/field-mapping`) DEPLOYED.

## Interview (8/8 Q-approved)

- Rota: Standalone page + SettingsPage hub card (LeadIntake pattern)
- Layout: Tablo grid (INMA Key | Label | Semantic | Type | Enum | Required | Action)
- Label: Inline kolon (read-only)
- Duplicate: Hard block + 400 INV-BE-096
- Edit: Inline editable row
- Save: Form-level 'Kaydet' button (disabled if no changes)
- Validation: Hybrid (live duplicate + live reserved, diger on-save)
- Empty label: '(INMA config edilmemis)' italic disabled row

## AC (8)

AC1 /settings/field-mapping + SettingsPage hub card | AC2 10-row grid | AC3 inline editable + form save | AC4 live duplicate + reserved | AC5 on-save backend 400 | AC6 useDynamicFields label | AC7 backend GET/PUT envelope | AC8 build+Codex PASS

## Scope Discipline

**Touchable (Dashboard only):**
- `pages/settings/FieldMappingSettingsPage.tsx` (YENI)
- `components/FieldMapping/FieldMappingTableRow.tsx` (YENI)
- `components/FieldMapping/EmptyLabelNotice.tsx` (YENI)
- `constants/tenantFieldMappingReserved.ts` (YENI — 23-key reserved set FE mirror)
- `types/tenantFieldMapping.ts` (YENI)
- `lib/api.ts` (getFieldMapping / putFieldMapping method eklenir)
- `App.tsx` (route eklenir)
- `pages/SettingsPage.tsx` (hub card eklenir)

**Forbidden:**
- Backend kod (`*.cs`) — endpoint zaten DEPLOYED
- Shared kod (validator, resolver, constants)
- Migration (yok)
- useDynamicFields hook modifikasyonu
- FlowBuilder / TemplateCreate (P4 scope)

## Deploy

- **Scope:** Backend SPA rebuild (tsc + vite + dotnet publish + server-deploy)
- **Post-deploy smoke:** `/settings/field-mapping` 401 probe (auth gate) + tenant 5050 GET + PUT 2-entry test mapping + GET verify + DELETE cleanup

## Codex Verdict

- **Iter 0: FAIL** — 8 blocker (CQ1 errorKind branches missing, CQ2 silent handleSave no-op, CQ5/CQ10 invalid HTML td, CQ6 unmanaged toast timer, CQ9 disabled-row UX mismatch, CQ12 fabricated INV-TFM-FE-001 code, Q2 cf11+ not rejected).
- **Iter 1: PASS** (12/12 CQ + 5/5 CoVe, 0 blocking) — model gpt-5.4-2026-03-05, tokens 25554.
- Summary: "The iter-1 diff addresses the previously identified UI/lifecycle issues without scope creep or contract breakage. Error surfacing, single-GET lifecycle control, reserved-name validation, and table structure are all implemented consistently."
