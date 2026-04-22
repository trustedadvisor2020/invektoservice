# P6 FEAT-MCC Multi-City Campaign — Tracking

> **Slug:** `20260425-feat-mcc-multi-city` | **Roadmap:** P6 FAZ 3 | **Risk:** MEDIUM
> **Plan JSON:** `arch/plans/20260425-feat-mcc-multi-city.json` (verdict=PASS, 4-chunk review all green)
> **Status:** **DONE** — code complete + build PASS + Codex 4-chunk PASS (48/48 CQ + 13/13 CoVe). Awaiting deploy + smoke (P9 prep).

## Scope

Tenant-scoped multi-city campaign config (`tenant_settings.campaign_config JSONB`) with locale-aware `{{campaign.cities_human|cities_csv|cities_json|name|slug|start_date|end_date|event_date|event_hours}}` substitution in Automation outbound, active-window guard firing in BOTH Automation `SendCallbackAsync` AND Marketing `FollowupStageJob.ExecuteAsync`. Push-cache pattern (Backend PUT → resolver Invalidate). Dent pilot seed (Dublin + Cork roadshow 2026-03-14/15) loaded idempotently via migration 030.

## Interview Answers (2026-04-22 11:04 UTC)

- **Q1 Slug uniqueness scope:** Tenant-scope unique (per tenant; same slug allowed across tenants).
- **Q2 Active window edges:** Inclusive `[start_date, end_date]` in `tenant_settings.timezone`.
- **Q3 Cities render format:** Human-readable list (`{{campaign.cities_human}}` → "Dublin and Cork" / "Dublin ve Cork" locale-aware) + sub-namespace `cities_csv` ("Dublin, Cork") + `cities_json` (`["Dublin","Cork"]`).
- **Q4 Cache invalidate pattern:** Push — Backend PUT calls `ITenantCampaignResolver.Invalidate(tenantId)` on the receiving instance; peer Automation/Marketing on 5dk TTL (eventual consistency).
- **Q5 Config shape:** Array of campaigns 1..N (max 8); `campaign_config = { "campaigns": [...] }` wrapper for forward-compat.
- **Q6 Window guard scope:** Automation message dispatch + Marketing EFS scheduler (both layers; tenants without campaigns bypass).
- **Q7 Substitution priority:** `lead.custom_1` (city) > tenant default; resolver supports both, Automation hot path passes `leadCity=null` for MVP (resolver falls back to first dates[]). Marketing EFS guard doesn't substitute — it only checks window.
- **Q8 Pilot seed:** Migration with idempotent INSERT for tenant_id=18173130 (Dent). `jsonb_path_exists` guard prevents re-run drift; tenants other than Dent untouched.

## Acceptance Criteria

| # | Criterion | Status |
|---|-----------|--------|
| AC1 | tenant_settings.campaign_config JSONB additive + GIN index + idempotent Dent seed (migration 030 + canonical mirror) | **MET (code)** |
| AC2 | Backend GET/PUT `/api/v1/tenant-settings/campaign-config` envelope + validator + cross-tenant 403 + cache invalidate + INV-BE-* class breakdown | **MET (code)** |
| AC3 | Automation SendCallbackAsync hook between KVKK and DMP: window guard + locale-aware substitution; campaign-agnostic outbound bypasses | **MET (code)** |
| AC4 | Marketing FollowupStageJob window guard after opt-out, before SEND-INTENT; tenants with empty campaigns[] bypass | **MET (code)** |
| AC5 | DbTenantCampaignResolver: 5dk MemoryCache + CT-safe single-flight + push invalidate; Npgsql/JsonException → empty config + WARN logs | **MET (code)** |
| AC6 | Build PASS (full sln 0 errors, no NEW warnings) + SPA build PASS (CampaignConfigSettingsPage chunk emitted) | **PASS ✅** |
| AC7 | Dashboard SPA editor at `/settings/campaigns` follows useFollowupSequence pattern (module cache + wrapError + bracket INV-* surfacing); SettingsPage entry card | **MET (code)** |

## Deliverables

### Migration
- `arch/db/migrations/030-tenant-campaign-config.sql` — campaign_config JSONB additive + GIN jsonb_path_ops index + idempotent Dent pilot seed (DO $$ block) + GRANT re-assertion
- `arch/db/tenant-settings.sql` — canonical schema mirror updated

### Error Codes (4 new)
- `arch/errors.md` + `Invekto.Shared/Constants/ErrorCodes.cs`
  - `INV-BE-118` — campaign config validation invalid (slug regex / cap / structure / date order / orphan city ref / Automation render miss)
  - `INV-BE-119` — campaign window closed (Automation dispatch + Marketing EFS scheduler suppression)
  - `INV-BE-120` — campaign slug uses reserved token (primary/system/default/all)
  - `INV-BE-121` — tenant_settings.campaign_config DB transient (NpgsqlException)

### Shared Contracts (8 new files)
- `Invekto.Shared/Contracts/Campaigns/Dtos/CampaignCity.cs`
- `Invekto.Shared/Contracts/Campaigns/Dtos/CampaignDate.cs`
- `Invekto.Shared/Contracts/Campaigns/Dtos/CampaignEntry.cs`
- `Invekto.Shared/Contracts/Campaigns/Dtos/CampaignConfig.cs` (top-level wrapper)
- `Invekto.Shared/Contracts/Campaigns/ITenantCampaignResolver.cs` (GetAsync + Invalidate + RenderPlaceholderAsync + IsWithinWindowAsync)
- `Invekto.Shared/Contracts/Campaigns/DbTenantCampaignResolver.cs` (Db-backed, 5dk MemoryCache + CT-safe single-flight + locale-aware cities_human + lead-aware date selection)
- `Invekto.Shared/Contracts/Campaigns/TenantCampaignConfigValidationException.cs`
- `Invekto.Shared/Services/TenantCampaignConfigValidator.cs` (slug regex / reserved set / max 8 campaigns / max 20 cities&dates / start≤end / dates.city referential)

### Backend (:5000)
- `Invekto.Backend/Data/TenantSettingsRepository.cs` — `GetCampaignConfigAsync` + `UpsertCampaignConfigAsync`
- `Invekto.Backend/Endpoints/TenantCampaignConfigEndpoints.cs` (NEW) — GET/PUT with TenantContext guard, body parse + cross-tenant 403, validator integration, resolver Invalidate
- `Invekto.Backend/Program.cs` — DI (DbTenantCampaignResolver + ITenantCampaignResolver alias) + endpoint mapping + API metadata listing

### Automation (:7108)
- `Invekto.Automation/Services/CampaignTemplateApplier.cs` (NEW) — regex `\{\{campaign\.([a-z_]+)\}\}` detect + window guard + render-and-replace; CampaignApplyResult record (NoOp/Substituted/Skip)
- `Invekto.Automation/Services/AutomationOrchestrator.cs` — `_campaignApplier` ctor param + invocation in `SendCallbackAsync` between KVKK and DMP layers; Skip → log INV-BE-119 + return false
- `Invekto.Automation/Program.cs` — DI (resolver + applier)

### Marketing (:7112)
- `Invekto.Marketing/Services/Jobs/FollowupStageJob.cs` — `_campaignResolver` ctor param + window guard between opt-out check and SEND-INTENT log; suppression marks `status='skipped_disabled'` with INV-BE-119
- `Invekto.Marketing/Program.cs` — `AddMemoryCache()` (first IMemoryCache reg in Marketing) + ITenantCampaignResolver registration

### Dashboard SPA (Backend wwwroot)
- `Dashboard/src/types/campaignConfig.ts` (NEW) — wire DTOs + UI draft types
- `Dashboard/src/lib/api.ts` — `getCampaignConfig` + `putCampaignConfig` + type re-exports
- `Dashboard/src/hooks/useCampaignConfig.ts` (NEW) — module-cached opt-in hook with wrapError + save invalidate
- `Dashboard/src/pages/settings/CampaignConfigSettingsPage.tsx` (NEW) — multi-card editor (campaigns + cities + dates sub-rows), slug+cap+date-order client validation, save error surfacing
- `Dashboard/src/App.tsx` — lazy import + Route `/settings/campaigns`
- `Dashboard/src/pages/SettingsPage.tsx` — entry card

### Build Results
- Full solution: **PASS** (0 errors, 17 pre-existing warnings only)
- Vite SPA: **PASS** (`CampaignConfigSettingsPage-VsvlFArG.js` 13.24 kB chunk emitted under `wwwroot/app/assets/`)

## Architectural Decisions

See `arch/plans/20260425-feat-mcc-multi-city.json` `spec_architectural_decisions[]` for the full set (9 entries). Highlights:

1. Top-level wrapper `{ campaigns: [...] }` over direct array (forward-compat for global flags)
2. Substitution via dedicated `CampaignTemplateApplier` (Automation), NOT via `ExpressionEvaluator` extension (keeps flow-variable scope intact)
3. Window guard in BOTH Automation dispatch AND Marketing EFS scheduler (interview Q6)
4. Window guard scoped to `{{campaign.X}}`-bearing messages — campaign-agnostic outbound unaffected
5. Dent pilot seed inside migration 030 (interview Q8 — Q-authorized exception to the schema/data separation)
6. Resolver single-flight CT-safe pattern reused verbatim from FEAT-TFM (lessons 2026-04-21 iter 3)
7. INV-BE-118..121 (4 codes) over roadmap's INV-BE-118..120 (3 codes); INV-BE-121 added for transient DB classification (FEAT-TFM precedent)

## Pilot Smoke Hooks (P9 prep)

`tracking/pilot-launch-roadmap.md` Pilot Smoke Step S7:

> **S7 — FEAT-MCC city substitution + window guard:** Template `{{campaign.cities_human}}` → "Dublin & Cork" (or locale-aware "Dublin and Cork"); out-of-window send → rejected INV-BE-118 ⟶ **NOTE:** rejection code is **INV-BE-119** (window closed), not INV-BE-118 (validation). Roadmap line should be updated to read "rejected INV-BE-119" as part of `/wrap`. Substitution + guard both active.

Pre-Pilot Tenant Prep (P9 step 3): "FEAT-MCC campaign config set — Dashboard Campaigns page" — covered by migration 030 idempotent Dent seed; operator can verify via `/settings/campaigns` that the row is present.

## Rollback Plan

- **Schema:** `tenant_settings.campaign_config` is additive with `DEFAULT '{"campaigns":[]}'`; no DROP needed if rolled back. To disable behaviour without a deploy, operator can `UPDATE tenant_settings SET campaign_config = '{"campaigns":[]}'::jsonb WHERE tenant_id = X` (campaigns[] empty → all guards/substitution become no-op).
- **Code:** Rollback Backend + Automation + Marketing to prior commit reverts the feature; the DB column stays (additive). Empty campaigns[] keeps everything behaving as pre-paket.
- **Dent pilot seed:** `UPDATE tenant_settings SET campaign_config = jsonb_set(campaign_config, '{campaigns}', '[]'::jsonb) WHERE tenant_id = 18173130;`

## Codex Review Result (2026-04-22 12:30 UTC)

4-chunk review — all PASS. Aggregate: **48/48 CQ gates + 13/13 CoVe questions**.

| Chunk | Scope | Iter | Result | Notes |
|-------|-------|------|--------|-------|
| 1 of 4 | Schema + Errors + Shared layer (DTOs/validator/exception/IResolver) | 2 | PASS 12/12 + 3/3 | Iter 1 FAIL → CQ5 null-forgiving operator (`city!.Name`, `entry!.Date`) + CQ12 silent null-config return; both fixed via explicit `is null` guards throwing INV-BE-118 |
| 2 of 4 | DbResolver + Backend endpoints + repo + Program.cs | 0 | PASS 12/12 + 4/4 | Clean — no iterations |
| 3 of 4 | Automation CampaignTemplateApplier + AutomationOrchestrator hook + Marketing FollowupStageJob window guard + DI | 1 | PASS 12/12 + 3/3 | Iter 0 FAIL → CQ12 chunk-isolation false positive (Codex didn't see INV-BE-119 registration in chunk 1); re-submitted with errors.md + ErrorCodes.cs evidence → PASS |
| 4 of 4 | Dashboard SPA (types + api + hook + page + App.tsx route + SettingsPage card) | 0 | PASS 12/12 + 3/3 | Clean — no iterations |

## Outstanding Items

- **Pilot Smoke S7 roadmap line** mentions INV-BE-118 for window-closed rejection; actual code is INV-BE-119. Needs roadmap correction during `/wrap`.
- **Lead.custom_1 plumbing** through Automation chat hot path is documented as a deferred follow-up paket — resolver supports it, only the call-site passes null.
- **Cross-instance cache invalidation** still 5dk eventual consistency (matches FEAT-TFM-CACHE backlog scope).
- **Deploy + Pilot Smoke S7** — paket is DONE but not yet DEPLOYED+SMOKED. Migration 030 needs production execution + Backend/Automation/Marketing redeploy + Dashboard SPA reload + manual S7 test (Dent tenant via Dashboard `/settings/campaigns`).
