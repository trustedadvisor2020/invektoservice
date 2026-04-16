# SPEC: Lead Intake Webhook

> **Spec ID:** FEAT-LIW | **Paket:** TBD | **Risk:** MEDIUM
> **Yazar:** Q | **Son Guncelleme:** 2026-04-16 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Tenant'in mevcut landing page'i/form'u/3rd party lead source'u (Meta Lead Ads, Google Lead Form, Typeform, kendi HTML form'u) Invekto'ya webhook ile lead duseklebilsin. Amac:
- Tenant kendi landing page'ini tutsun (biz rebranding/redirect zorlamayalim)
- Generic kontrat: her tenant kendi field isimlerini Invekto canonical field'larina map edebilsin
- Duplicate merge, consent, source attribution standart olsun
- WhatsApp direct entry (wa.me link) paralel yol — lead otomatik create

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `POST /api/v1/leads/intake/{source_slug}` endpoint + pre-shared API key (per-tenant) | Auth middleware 401 invalid key |
| AC-2 | Field mapping config JSONB (`landing_field_map`) → source field → canonical field | Unit test: `{ "ad_soyad": "name" }` merges into `leads.full_name` |
| AC-3 | Phone E.164 normalize + duplicate check (son 30g window, tenant-scoped) | 2 POST same phone → 1 lead, 2. merged |
| AC-4 | Source tag auto-assign (`source_slug` path param) + UTM passthrough (JSONB `intake_metadata`) | DB row has source+utm |
| AC-5 | Consent flag required (source form'da opt-in checkbox, field mapping'te zorunlu alan) | Validation 400 if missing |
| AC-6 | Welcome flow auto-trigger (tenant config'ten welcome_flow_slug) | Automation flow_execution row |
| AC-7 | WA direct (`wa.me`) inbound → eger lead yoksa otomatik create (profile name + phone) | INMA webhook handler |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Webhook signature YOK (pre-shared API key yeterli) | Tenant landing dev capacity dusuk, HMAC over-engineering | EXPECTED: CQ12 skip security banner with justification |
| Field map JSONB (`tenant_settings.landing_field_map`) | Kod-degisikligi-gerektirmez per-tenant | — |
| Endpoint `/api/v1/leads/intake/{source_slug}` — slug path'te, tenant JWT degil API key header | Tenant landing'in JWT sign etmesi beklentisi gercekci degil | CQ5/CQ9: per-tenant API key tenant_id resolve eder |
| Duplicate merge: phone-based + 30g window | SMS-era standardi | — |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Intake Request/Response | `arch/contracts/lead-intake.json` (yeni) |
| DB Schema | `arch/db/pkt6b1-niche-business.sql` (mevcut leads tablosu; `intake_metadata JSONB` ALTER) |
| Shared DTO | `Invekto.Shared/Contracts/Leads/LeadIntakeRequest.cs` |
| Error Codes | INV-BE-092 (invalid_api_key), INV-BE-093 (field_mapping_missing), INV-BE-094 (consent_required), INV-BE-095 (phone_invalid_e164) |

## 5. Scope Boundaries

### In Scope
- Generic `/leads/intake/{source_slug}` endpoint (API key auth)
- `landing_field_map` JSONB + UI editor (Dashboard Ayarlar)
- Phone normalize (libphonenumber-equivalent)
- Duplicate detection (phone-based, 30g window, tenant-scoped)
- Source tag + UTM metadata capture
- Welcome flow auto-trigger hook
- WA direct entry handler (mevcut WAA inbound'a additive)

### Out of Scope (Explicit)
- Meta Lead Ads native integration (v2, ayri feature)
- Reverse-ETL to tenant's CRM (Zoho sync ayri feature — `ZohoLifecycleDispatcher` pattern kullanilir)
- Email-only leads (v2 — phone zorunlu v1)
- Captcha/anti-bot (tenant landing sorumluluu)

### Degismeyen Alanlar (Pre-existing)
- Mevcut `leads` tablosu kolonlari
- WAA INMA webhook handler akisi (minor extension)
- Flow engine auto-trigger mekanizmasi

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Backend | Intake endpoint + field map resolve + duplicate check + flow trigger | Yeni endpoint |
| Automation | Welcome flow entry hook | Minor (trigger listener) |
| WAA / ChatAnalysis | INMA inbound — lead auto-create if WA direct | Minor extension |
| Dashboard | Field mapping editor + API key management | Yeni page |
| Shared | DTO | Yeni |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|-----------|
| API key leak (landing JS'de expose olursa) | HIGH | Key rotation endpoint + invalidate UI + rate limit per-key |
| Phone E.164 parse failure (exotic formats) | MEDIUM | Try-parse + source country hint from API key config; fail-soft 400 |
| Tenant wrong field map → silent data loss | MEDIUM | Dry-run validation endpoint; UI preview before save |
| Landing form submit 100/sec spike | LOW-MED | Rate limit per-API-key + 429 with backoff header |

## 8. Pilot Consumer

Dent Adavista — kendi landing page'i (Ireland roadshow lead form) + WA direct (reklam CTA `wa.me/{number}`). Detay: `DentAdavista/plan/pilot-checklist.md`.
