# Pilot Checklist Doc Drift Fixes — Tracking

> **Slug:** `20260423-pilot-checklist-doc-drift`
> **Plan:** [`arch/plans/20260423-pilot-checklist-doc-drift.json`](../arch/plans/20260423-pilot-checklist-doc-drift.json)
> **Risk:** LOW | **Scope:** Doc-only | **Build:** N/A
> **Status:** DONE (Codex iter 0 PASS 12/12 CQ + 0 blocker, model=gpt-5.4)
> **Kickoff:** 2026-04-23 09:00 UTC
> **Motivation:** Post-P9 Dent Pilot FlowBuilder Wiring smoke'da 5 doc↔code drift ortaya çıktı. Pilot go-live öncesi Q + operator mental model'i ile kod contract'ını hizala.

## Q Intent

"pilot-checklist.md doc drift fixes — (a) §1 feature flags listesine appointments=on ekle, (b) §3 source_slug roadshow_landing→roadshow-landing (dash), (c) §3 5 LIW contract quirks notu (header, fields wrapper, consent bool, regex dash-only, backend localhost:5000). Q tercihi: doc-only paket B."

## Interview Gates (3/3 onaylı)

1. **§1 scope:** Sadece `appointments=true` eklenir (plan_tier verify adımı paket A scope'u).
2. **§3 Contract Quirks format:** Ayrı callout block (inline liste değil — görsel prominence için).
3. **§3 JSON field_map shape:** Tam shape fix (outer+inner wrapper fictional, `FieldMapResolver.ParseMap` flat object bekliyor — minimum dash fix yetmez).

## Code Verify (pre-edit source of truth)

| Drift | Code Path | Evidence |
|-------|-----------|----------|
| Slug regex dash-only | `src/Invekto.Backend/Services/LeadIntakeService.cs:21` | `SlugRegex = new("^[a-z0-9][a-z0-9-]{0,49}$", ...)` |
| Header name | `src/Invekto.Backend/Program.cs:5097` | `ctx.Request.Headers["X-Invekto-Api-Key"].FirstOrDefault()` |
| Endpoint path | `src/Invekto.Backend/Program.cs:5090` | `app.MapPost("/api/v1/leads/intake/{source_slug}", ...)` |
| Fields wrapper | `src/Invekto.Shared/Contracts/Leads/LeadIntakeRequest.cs:15` | `[JsonPropertyName("fields")] public Dictionary<string, object?> Fields` |
| Consent bool parser | `src/Invekto.Backend/Services/FieldMapResolver.cs:128-144` | `ValueToBool` accepts bool + `JsonValueKind.True/False` + `bool.TryParse(string)` — "yes"/"1"/"on" reject |
| field_map flat shape | `src/Invekto.Backend/Services/FieldMapResolver.cs:15-36` | `ParseMap` iterates root properties directly; no slug-keyed wrapper |
| field_map direction | `src/Invekto.Shared/Contracts/Leads/TenantLandingSettingsDto.cs:33-43` | DTO: source→canonical (UI-natural); storage inverse (canonical→source) via `LiwSettingsService.SerializeFieldMapForStorage` |
| INV-BE-101 slug-invalid | `src/Invekto.Shared/Constants/ErrorCodes.cs` + `arch/errors.md` | Source slug validation error code |
| INV-BE-109 fields missing | `src/Invekto.Shared/Constants/ErrorCodes.cs` + `arch/errors.md` | Fields wrapper missing error code |

## Deliverables (Doc-only)

- [x] Plan JSON (`arch/plans/20260423-pilot-checklist-doc-drift.json`) — AC 5, aha 5, LOW risk, DONE
- [x] Tracking doc (this file)
- [x] `DentAdavista/plan/pilot-checklist.md` edit — 3 drift fix (AC1-AC4) + integrity (AC5), 23 ins / 13 del
- [x] `/rev` Codex iter 0 PASS 12/12 CQ + 0 blocker (model=gpt-5.4-2026-03-05, 7972 tokens)
- [ ] Commit + push master

## Post-Paket Follow-ups (scope dışı)

- **Paket A:** `tenant_registry.plan_tier='baslangic' → 'profesyonel'|'kurumsal'` (Dashboard Ops page veya direct SQL UPDATE). Appointments feature unlock → S6 smoke re-open.
- **Paket C:** 46 welcome + 36 FAQ `[EDIT:*]` placeholder → ROADSHOW DocX gerçek içerik bind + faq_entries is_active=TRUE + chatbot_flows.flow_config.nodes[].data.text güncelle. Post-bind S4 AI FAQ translation hop smoke açılır.
- **Future generic doc:** Contract Quirks bloğu `arch/features/lead-intake-webhook.md`'ye mirror edilirse 2. tenant onboarding template haline gelir (bu paket scope'unda değil).

## Codex Review Queue

- **Protocol:** v5.1 LOW risk (verification_questions optional; aha_moments 5 required)
- **MCP tool:** `mcp__codex-review__codex_review` (single reviewer, LOW risk)
- **Expected CQ focus:** CQ7 (documentation clarity), CQ8 (plan/code drift) — doc contract vs code contract alignment evidence grep'leri plan'daki interview_notes içinde listelendi.

## Changelog

| Date (UTC) | Action | Commit |
|------------|--------|--------|
| 2026-04-23 09:00 | Paket kickoff — interview 3 gate | - |
| 2026-04-23 09:01 | Plan JSON + tracking doc yazıldı | - |
| 2026-04-23 09:10 | pilot-checklist.md edit (3 drift fix, 23 ins / 13 del) | - |
| 2026-04-23 09:14 | Codex iter 0 PASS 12/12 CQ + 0 blocker (gpt-5.4) | - |
| 2026-04-23 09:15 | AC1-AC5 verified=true, plan status=DONE | - |
| TBD | Commit master + update session-memory + roadmap | - |
