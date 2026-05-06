<!-- Status: DONE+DEPLOYED+SMOKED | 2026-05-06 -->
# FEAT-META-FULL-INTAKE — Meta Leadgen Full Field Intake

> **Slug:** `20260429-feat-meta-full-intake`
> **Risk:** HIGH (intake topology değişikliği)
> **Code commit:** `a4509a9` (2026-04-29 01:32 UTC)
> **Deploy:** `6670b3b` chain → Backend redeploy 2026-05-06 09:43 UTC (commit `6670b3b` Medipol cancel + master HEAD birikmiş 4 commit + META code)
> **Codex:** iter 0 PASS 12/12 CQ + 4/4 CoVe + 0 blocker (gpt-5.5-2026-04-23, 23583 token, CODEX UTANSIN ✅)
> **Status:** DONE+DEPLOYED+SMOKED 2026-05-06 09:45 UTC

---

## Scope

MetaLeadgen process-lead handler canonical map sonrası sadece phone+name'i `WaDirectIntakeRequest`'e koyup intake'e yolluyordu. Email/custom_1..5/consent canonical alanları kayboluyordu (Codex audit raporu, dent-golive.html drift bulgu 2026-04-29).

**Q kararı (b):** Kod genişlet — full canonical persist olsun, plan E.2 source slug `meta-leadgen` field map devreye girsin, tüm alan persist.

## Acceptance Criteria

| AC | Kriter | Verified |
|----|--------|----------|
| AC1 | Process-lead email/custom_1..5/consent persist eder; smoke: leads.email non-null + intake_metadata.resolved.custom_1='dublin' + consent=true | ✅ Codex iter 0 |
| AC2 | consent yok/false → 400 INV-BE-105 + audit row error_code=INV-BE-105; lead INSERT YOK | ✅ Codex iter 0 |
| AC3 | WaDirect path UNTOUCHED — IntakeWaDirectAsync + EnsureLeadForWaDirectAsync regresyon yok | ✅ Codex iter 0 |
| AC4 | Build PASS 0 error/warning regression + Dashboard tsc 0 error + /rev iter 0 (CODEX UTANSIN) | ✅ |
| AC5 | Dashboard `/settings/meta-leadgen` field_id_map editor'unda 'consent (pazarlama onayı)' option seçilebilir + PUT round-trip persist | ✅ |

## Architectural Decisions (Codex pre-empt)

1. **custom_1..5 leads kolonlarına YAZILMAZ** — sadece `intake_metadata.resolved.<canonical>` JSONB'de. LIW Chunk A pattern (FEAT-TFM-SYNC paketi semantic projection üretecek)
2. **Yeni internal HTTP endpoint AÇILMAZ** — in-process DI ile `LeadIntakeService.IntakeMetaLeadgenAsync` direct invoke (same-service, cross-service değil)
3. **MetaLeadgenConfigDto canonical key 'consent_marketing' → 'consent'** drift fix (LIW canonical class). Validator/whitelist yok (field_id_map JSONB serbest)

## Files Changed (8)

| Path | Change |
|------|--------|
| `src/Invekto.Shared/Contracts/Leads/MetaLeadgenIntakeRequest.cs` | NEW — in-process payload (TenantId/Phone/Name/Email/Consent/Custom1..5/Referer/ReceivedAt) |
| `src/Invekto.Shared/Contracts/Leads/MetaLeadgenIntakeResponse.cs` | NEW — response envelope (LeadId/IsNew/WelcomeFlowEnqueued/Warnings) |
| `src/Invekto.Backend/Data/LeadRepository.cs` | ADD — `EnsureLeadForMetaLeadgenAsync` (WaDirect superset CTE + email INSERT/UPDATE-COALESCE + source='facebook') |
| `src/Invekto.Backend/Services/LeadIntakeService.cs` | ADD — `IntakeMetaLeadgenAsync` (~210 satır, consent gate INV-BE-105 hard reject + intake_metadata snapshot + welcome enqueue fail-soft) |
| `src/Invekto.Backend/Services/MetaLeadgen/MetaLeadgenEndpoints.cs` | EDIT — process-lead step 4 swap + ParseConsentBool helper (strict bool.TryParse) |
| `src/Invekto.Shared/Contracts/MetaLeadgen/MetaLeadgenConfigDto.cs` | DOC — canonical 'consent_marketing' → 'consent' |
| `src/Invekto.Backend/Dashboard/src/pages/settings/MetaLeadgenSettingsPage.tsx` | UI — CANONICAL_OPTIONS line 33 'consent (pazarlama onayı)' |
| `arch/plans/20260429-feat-meta-full-intake.json` | NEW — plan v5.1 HIGH risk |

**Diff total:** 718 ins / 15 del

## Deploy Evidence (2026-05-06 09:43 UTC)

- **Vite build:** Dashboard SPA 24 chunks built in 10.36s. `MetaLeadgenSettingsPage-D9a1DPNF.js` 11.84 KB / 3.71 KB gzip
- **dotnet publish:** Release config, no errors. 116 files. NU1603 + CS1998 + CA1416 warnings pre-existing (not regressions)
- **Server-deploy:** stop → backup config → upload (12326 KB zip) → extract → restore config → start
  - `Backend.dll` LastWrite=2026-05-06 12:42:38 UTC+3, 1441 KB
  - `Shared.dll` LastWrite=2026-05-06 12:42:10 UTC+3, 464.5 KB
  - `wwwroot/app/assets/MetaLeadgenSettingsPage-D9a1DPNF.js` LastWrite=2026-05-06 12:41:54 UTC+3
- **Health post-deploy:** Backend HEALTHY @ 09:43:26Z + 9 peer services HEALTHY (regression check)

## Smoke Evidence (3/3 PASS, 09:45 UTC)

| # | Endpoint | Expected | Actual | Status |
|---|----------|----------|--------|--------|
| S1 | `GET /api/inbound/meta/leadgen/18173130?hub.verify_token=wrong` | 403 INV-META-002 | 403 | ✅ |
| S2 | `POST /api/inbound/meta/leadgen/18173130` (X-Hub-Signature-256: sha256=bad) | 401 INV-META-001 | 401 | ✅ |
| S3 | `GET /api/v1/tenant-settings/meta-leadgen` (no JWT) | 401 | 401 | ✅ |

**Auth gates intact** — B-META 2026-04-24 baseline preserved. INMA + Meta + JWT middleware regresyon yok.

## Persistence Smoke — DEFERRED to Pilot Stage 1

Real Meta webhook → process-lead → `leads.email` + `intake_metadata.resolved.consent=true` doğrulaması bekleniyor:
- **Bağımlılık:** Meta App + HSM template Meta onayı (24-48h customer-side) + WABA `phone_number_id` + Invekto ops Dashboard config (Dent tenant Meta config)
- **Stage 1 launch smoke:** AC1b/AC2b customer-side aktive olunca 5 numara × 6 senaryo
- **Şu anki kanıt:** Codex iter 0 PASS code-path verification + AC3 regression guard (WaDirect UNTOUCHED) + auth gate prod smoke

## FEAT-PIPELINE Bağımlılığı YOK

Önceki erteleme sebebi (commit message 2026-04-29): "FEAT-PIPELINE blocker var (KARAR-INMA-PIPELINE-CONTRACT pending) → tek deploy bundle". Q kararı 2026-05-06: solo deploy, FEAT-PIPELINE INMA contract beklemesinin süresizliği kabul edilmedi. **Teknik dependency yok** — META full intake `LeadStatusEventMap` veya `LeadStatusOrchestrator` kullanmıyor (FEAT-PIPELINE Faz 1 kapsamı).

## Lessons

1. **HIGH risk paket Codex iter 0 PASS recipe** — 3 architectural decision pre-declare + existing pattern superset (WaDirect → MetaLeadgen) + AC3 regression guard explicit + scope_discipline forbidden_areas listesi
2. **Solo deploy vs bundle erteleme trade-off** — bundle "hijyen" gerekçesi süresizlik riskine sahip (INMA contract bekleme), bağımsız paket için solo deploy default. Code 1 hafta beklemiş olsa da Codex PASS + master HEAD'de stable + smoke green.
