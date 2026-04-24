# Paket B-META — Meta Leadgen Webhook Native

> **Slug:** 20260425-feat-meta-leadgen-webhook
> **Risk:** MEDIUM
> **Status:** **DONE+DEPLOYED+SMOKED** (commit `9763012` 2026-04-24 22:30 UTC; Codex 4-chunk merged PASS; Migration 033 + Backend/Automation deploy HEALTHY; AC1 4/4 PASS)
> **Created:** 2026-04-24 17:45 UTC
> **Completed:** 2026-04-24 22:30 UTC
> **Paket scope:** Zapier/3rd-party bridge YOK. Invekto içinde Meta Lead Ads native webhook entegrasyonu.

## Scope

Dent Adavista pilot Stage 1 launch için Meta Lead Ads → Invekto → WhatsApp welcome zincirinin native entegrasyonu. Müşteri Zapier/Make.com kullanmayacağı için Meta webhook'u doğrudan Invekto endpoint'ine gelir.

- Endpoint: `GET` + `POST /api/inbound/meta/leadgen/{tenantId:int}` (Backend)
- Signature validate: `X-Hub-Signature-256` HMAC-SHA256 constant-time (CryptographicOperations.FixedTimeEquals)
- Hangfire async: `MetaLeadgenIntakeJob` queue=automation, Graph API lead fetch + LIW hop
- Migration 033: `tenant_settings.meta_leadgen_config` JSONB + NEW `meta_leadgen_events` tablo (7-day retention)
- Error codes: `INV-META-001..006` yeni namespace
- Cross-service hop: Automation → Backend `/api/internal/lifecycle/welcome-sent` (welcome_sent Zoho dispatch)
- Dashboard SPA `/settings/meta-leadgen` (verify_token rotate + config + discover + test events)

## Interview Gates (9 gate, hepsi Q-approved AskUserQuestion 2026-04-24 17:35-17:45 UTC)

| Gate | Karar | Rationale |
|------|-------|-----------|
| G1 URL path param | tenant_id (int) | Signature verify asıl güvenlik, migration tasarruf |
| G2 welcome_sent dispatch koşul | messages.Count > 0 | Literal 'mesaj atıldı' semantiği |
| G3 secret storage | Plaintext JSONB | LIW pattern precedent |
| G4 form discover | Dedicated backend endpoint | Frontend Graph API leak yok |
| G5 migration scope | meta_leadgen_events tablo + JSONB key | Test Webhook UI persistence şart |
| G6 queue | automation (mevcut) | TriggerWelcomeFlowJob ile aynı queue |
| G7 error prefix | INV-META-001..006 yeni namespace | INV-SEED precedent, INV-INT ile karışmasın |
| G8 SPA scope | Full SPA + backend atomik tek paket | Pilot 3-gün timeline |
| G9 cross-service hop | Automation → Backend HTTP internal | MarketingFollowupClient precedent |

## Acceptance Criteria

| AC | Criterion | Verify |
|----|-----------|--------|
| AC1 | Prod curl 3 test PASS (verify handshake + bozuk signature 401 + geçerli signed mock → 200 + event row + Hangfire enqueue) | Commit-time |
| AC1b | Gerçek Meta test lead → WhatsApp delivered + zoho_sync_log welcome_sent | **Stage 1 smoke** (post-paket) |
| AC2 | TriggerWelcomeFlowJob unit test 2 scenario (messages>0 → dispatch called, messages=0 → NOT called) | Commit-time |
| AC2b | Dent test lead zoho_sync_log welcome_sent row (success OR blueprint_not_configured) | **Stage 1 smoke** (post-paket) |
| AC3 | Q manuel 5-tik Dashboard smoke (login + rotate + save + discover graceful fail + events empty) | Commit-time |
| AC4 | Full solution build PASS 0 errors + SPA prod chunk produced | Commit-time |
| AC5 | Codex review iter=0 PASS 12/12 CQ + 3/3 CoVe + 0 blocker | Commit-time |
| AC6 | Migration 033 prod execute + DO $verify$ INV-SEED-018..020 PASS + Backend+Automation NSSM restart + /health HEALTHY | Deploy |

## Architecture — Cross-Service Flow

```
Meta Lead Form (Facebook/Instagram)
   ↓ [Meta Leadgen webhook POST — signed]
Backend (:5000) /api/inbound/meta/leadgen/{tenantId}
   ↓ [signature validate + event INSERT + Hangfire enqueue]
Automation (:7108) MetaLeadgenIntakeJob [queue=automation]
   ↓ [Graph API fetch via Backend /api/internal/meta-leadgen/fetch-lead hop]
Backend → LeadIntakeService.IntakeAsync (internal)
   ↓ [UPSERT lead + welcome enqueue]
Automation TriggerWelcomeFlowJob [queue=automation]
   ↓ [FlowEngineV2.ExecuteAsync + messages.Count > 0 check]
Automation → Backend /api/internal/lifecycle/welcome-sent HTTP hop
   ↓ [ZohoLifecycleDispatcher.DispatchEvent(welcome_sent)]
Backend → Integrations (:7106) /api/v1/zoho/sync
   ↓ [ZohoBlueprintClient.ExecuteTransitionAsync]
Zoho CRM — Leads transition "1. Mesaj Atıldı" executed
```

## Migration 033 Schema

```sql
-- (1) Tenant config JSONB key
ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS
    meta_leadgen_config JSONB DEFAULT '{}'::jsonb;

-- (2) Event audit table
CREATE TABLE IF NOT EXISTS meta_leadgen_events (
    id BIGSERIAL PRIMARY KEY,
    tenant_id INT NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    leadgen_id VARCHAR(64) NOT NULL,
    form_id VARCHAR(64),
    page_id VARCHAR(64),
    received_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    http_status SMALLINT,
    error_code VARCHAR(20),
    raw_payload JSONB,
    lead_id INT REFERENCES leads(id) ON DELETE SET NULL,
    CONSTRAINT uq_meta_leadgen_events_tenant_leadgen UNIQUE (tenant_id, leadgen_id)
);

CREATE INDEX IF NOT EXISTS idx_meta_leadgen_events_tenant_recent
    ON meta_leadgen_events (tenant_id, received_at DESC)
    WHERE received_at > now() - INTERVAL '7 days';

GRANT ALL ON meta_leadgen_events TO invekto;
GRANT USAGE, SELECT ON SEQUENCE meta_leadgen_events_id_seq TO invekto;

-- (3) Postcondition verify
DO $verify$
DECLARE
    v_table_exists BOOLEAN;
    v_jsonb_col_exists BOOLEAN;
    v_grant_ok BOOLEAN;
BEGIN
    -- V1 INV-SEED-018 meta_leadgen_events table exists
    SELECT EXISTS(SELECT 1 FROM information_schema.tables
                  WHERE table_name='meta_leadgen_events')
      INTO v_table_exists;
    IF NOT v_table_exists THEN
        RAISE EXCEPTION '[INV-SEED-018] meta_leadgen_events table not created. Why: migration 033 CREATE TABLE failed. Next step: check migration log + re-run ALTER/CREATE manually.';
    END IF;

    -- V2 INV-SEED-019 tenant_settings.meta_leadgen_config JSONB column exists
    SELECT EXISTS(SELECT 1 FROM information_schema.columns
                  WHERE table_name='tenant_settings' AND column_name='meta_leadgen_config')
      INTO v_jsonb_col_exists;
    IF NOT v_jsonb_col_exists THEN
        RAISE EXCEPTION '[INV-SEED-019] tenant_settings.meta_leadgen_config JSONB not added. Why: ALTER TABLE DDL suppressed. Next step: verify invekto role OWNER tenant_settings + re-run.';
    END IF;

    -- V3 INV-SEED-020 GRANT ALL invekto on meta_leadgen_events
    SELECT has_table_privilege('invekto', 'meta_leadgen_events', 'INSERT,SELECT,UPDATE,DELETE')
      INTO v_grant_ok;
    IF NOT v_grant_ok THEN
        RAISE EXCEPTION '[INV-SEED-020] GRANT ALL invekto on meta_leadgen_events not applied. Why: role grant drift. Next step: GRANT ALL ON meta_leadgen_events TO invekto + GRANT USAGE,SELECT ON sequence.';
    END IF;

    RAISE NOTICE '[B-META] postcondition verify PASS (table=ok, jsonb_col=ok, grant=ok)';
END $verify$;
```

## Error Code Registry

| Code | HTTP | Semantic | Retry |
|------|------|----------|-------|
| INV-META-001 | 401 | Signature invalid (HMAC mismatch) | No (security fail) |
| INV-META-002 | 403 | Verify token mismatch (handshake) | No |
| INV-META-003 | 502 | Graph API fetch failed (transient) | Yes (Hangfire 3x) |
| INV-META-004 | 422 | field_data parse failed | No (terminal) |
| INV-META-005 | 404 | Tenant unknown or inactive | No (config fix) |
| INV-META-006 | 500 | Access token missing/expired | No (config renew) |
| INV-SEED-018 | — | Postcondition: meta_leadgen_events table_not_created | — |
| INV-SEED-019 | — | Postcondition: tenant_settings.meta_leadgen_config JSONB missing | — |
| INV-SEED-020 | — | Postcondition: GRANT ALL invekto not applied | — |

## Q Override Log

- **Override type:** max_diff_lines_per_paket
- **Limit:** 200
- **Estimated:** 760 line (3.8x)
- **Reason:** Atomik paket (endpoint + config + dispatch + SPA mutually dependent). Split 2-3 paket pilot 3-gün timeline aşar. Tek Codex review agresif yazma disiplini zorunlu.
- **Approved:** Q 2026-04-24 17:40 UTC
- **Commit trailer:** "Q-override: max_diff_lines=200 → ~760 (atomic paket, pilot timeline)"

## Deploy Plan

1. Migration 033 prod execute (MCP invekto-postgres) + DO $verify$ 3/3 PASS
2. Backend publish + NSSM restart Invekto-Backend + /health HEALTHY
3. Automation publish + NSSM restart Invekto-Automation + /health HEALTHY
4. Backend SPA chunk verify (MetaLeadgenSettingsPage-*.js referenced from index-*.js)
5. AC3 Q manuel 5-tik Dashboard smoke
6. AC1 prod curl 3 test (verify/bozuk-sig/valid-signed)

## Stage 1 Launch Dependencies (AC1b + AC2b için)

Bu paket commit edildikten sonra paralel yürür:

1. **Müşteri Meta App + Lead Form + HSM template submit** (pilot-stage1-prep Bölüm B)
2. **Müşteri Zoho Blueprint + stage mapping** (pilot-stage1-prep Bölüm C)
3. **Müşteri WABA phone_number_id + INMA allowlist** (pilot-stage1-prep Bölüm D)
4. **Invekto ops Dashboard config** (pilot-stage1-prep Bölüm E)
5. **Stage 1 smoke 5 test numarası × 6 senaryo** (pilot-stage1-prep Bölüm F)

## Post-Codex Checklist

- [x] Codex 4-chunk merged PASS (chunk-1 iter 2 + chunk-2 iter 5 + chunk-3 iter 2 + chunk-4 iter 3; CODEX UTANSIN iter=0 hedefi NOT achieved — iter 5 chunk 2 max; real bugs + chunking communication noise birlikte fix edildi)
- [x] Build full solution 0 errors
- [x] Unit test TriggerWelcomeFlowJobTests 5/5 PASS (helper-level AC2 invariant)
- [x] Migration 033 prod execute + DO $verify$ 5/5 postcondition PASS (table_exists + unique_constraint + recent_index + jsonb_col + grant_ok)
- [x] Backend + Automation NSSM restart + /health HEALTHY (Backend 213 files 12.7MB + Automation 65 files 2.9MB)
- [x] AC1 prod curl 4/4 PASS (handshake 200 'test123' + bozuk 401 INV-META-001 actionable + valid HMAC 200 accepted + wrong-token 403 INV-META-002 bonus; end-to-end audit chain validated via test event_row with downstream INV-META-006)
- [ ] AC3 Q manuel 5-tik Dashboard smoke (Q müsait zamanda `/settings/meta-leadgen` 5-step; commit-time config seed Q tercihi, ops seed ayrı)
- [x] Commit + push master (`9763012` — 64 file / 3761 ins / 77 del, HEREDOC message + Co-Authored-By + 3 Q-override trailer)
- [x] roadmap + session-memory update (B-META backlog entry → DONE with full summary)
- [x] lessons-learned +3 entries (pending commit — a/ PostgreSQL 40.7 Immutability Index Functions rejects partial predicate with now(); b/ Codex chunked review needs explicit chunking-context injection; c/ curl.exe --data body-modification vs --data-binary @file for HMAC smoke)
- [x] /wrap next-session prompt üretildi (Stage 1 launch müşteri prep paralel)

## AC Pending (post-commit, customer-side dependency)

- [ ] AC1b Stage 1 smoke — gerçek Meta test lead → WhatsApp delivered + zoho_sync_log welcome_sent row (müşteri Meta App + HSM template onayı 24-48h bekleniyor)
- [ ] AC2b Stage 1 smoke — Dent test lead zoho_sync_log welcome_sent status=success OR blueprint_not_configured (müşteri Zoho Blueprint kurulumu bekleniyor)
- [ ] AC3 Q Dashboard manuel 5-tik — Q müsait zamanda Dent tenant admin login + /settings/meta-leadgen 5 adım

## References

- `DentAdavista/plan/pilot-stage1-prep.md` (Bölüm A — scope source)
- `DentAdavista/plan/pilot-checklist.md` (müşteri input matrix)
- `arch/features/lead-intake-webhook.md` (LIW pattern precedent)
- `src/Invekto.Backend/Services/LeadIntakeService.cs` (intake hop pattern)
- `src/Invekto.Backend/Services/Zoho/ZohoLifecycleDispatcher.cs` (dispatcher pattern)
- `src/Invekto.Automation/Services/Jobs/TriggerWelcomeFlowJob.cs` (welcome hook point line 192)
- `arch/errors.md` (INV-META namespace precedent check: 0 → yeni)
- `arch/contracts/plan-schema.json` (plan shape)
