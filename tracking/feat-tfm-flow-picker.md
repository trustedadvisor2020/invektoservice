# P4 — FEAT-TFM-FLOW Picker

> **Slug:** `20260424-feat-tfm-flow-picker` | **Faz:** 2 | **Risk:** MEDIUM
> **Roadmap:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md) P4
> **Plan JSON:** [`arch/plans/20260424-feat-tfm-flow-picker.json`](../arch/plans/20260424-feat-tfm-flow-picker.json)
> **Durum:** DONE — Codex iter 1 PASS (12/12 CQ + 3/3 CoVe, 0 blocking), iter 0 FAIL 1 blocker CQ12 resolved. Deploy pending.

## Scope

PlaceholderPicker'a `tfmAware?: boolean` prop ekle — 2-grup render (Semantic + INMA Ham). useFieldMapping hook (useDynamicFields ikizi). TemplateCreatePage + flow-builder NodePropertyPanel explicit `tfmAware={true}`. renderDynamicPreview opt-in mapping parametresi + semantic substitute.

## Interview (8/8 Q-approved)

- Approach: mevcut PlaceholderPicker'a opt-in prop
- Display: 2-grup (Semantic Alanlar / INMA Ham Alanlar)
- Token: `{{semantic}}` literal (backend TFM resolver cf1'e cevirir)
- Empty: INMA-only (semantic grup gizli)
- Hook: useDynamicFields ikizi (module-cache + single-flight + errorKind)
- Default: false, 2 consumer'da explicit true
- Preview: opt-in mapping param, semantic substitute

## AC (8)

AC1 tfmAware prop + 2-grup render | AC2 useFieldMapping hook | AC3 semantic `{{<semantic>}}` literal | AC4 INMA ham mevcut | AC5 empty/fail = INMA-only | AC6 TemplateCreate + NodePropertyPanel explicit | AC7 renderDynamicPreview opt-in mapping | AC8 build + Codex PASS

## Scope Discipline

**Touchable (Dashboard only):**
- `hooks/useFieldMapping.ts` (YENI)
- `components/PlaceholderPicker.tsx`
- `pages/TemplateCreatePage.tsx`
- `pages/flow-builder/panels/NodePropertyPanel.tsx`

**Forbidden:**
- Backend kod (endpoint DEPLOYED)
- Shared kod (Validator/Resolver dokunulmaz)
- api.ts (P3'de eklendi)
- InmaDynamicFieldsCache (P1 fix)

## Deploy

- **Scope:** Backend SPA rebuild
- **Post-deploy smoke:** SPA bundle PlaceholderPicker-new-chunk + manuel Dashboard'da /templates/new acip dropdown 2-grup gorme + semantic click'te `{{roadshow_city}}` insert + preview tile render

## Codex Verdict

- **Iter 0: FAIL** — 1 blocker CQ12 (uncoded `field_mapping_load_failed` / `field_mapping_refresh_failed` Errors violated INV-XX-NNN policy).
- **Iter 1: PASS** (12/12 CQ + 3/3 CoVe, 0 blocking) — wrapError helper ApiClientError.errorCode verbatim preserve + fallback INV-OB-037 + Object.assign .code/.requestId telemetry. Turkish user-facing messages bracket INV code for ops log grep. model gpt-5.4-2026-03-05, tokens 16796.
