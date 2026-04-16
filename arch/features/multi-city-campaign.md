# SPEC: Multi-City Campaign Config

> **Spec ID:** FEAT-MCC | **Paket:** TBD | **Risk:** LOW
> **Yazar:** Q | **Son Guncelleme:** 2026-04-16 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Tenant'in sehirli (veya lokasyonlu) kampanya verilerini flow'a expose etmek. Ornekler:
- Dental klinik yurt disi "roadshow" (Dublin 14 Mart, Cork 15 Mart)
- E-ticaret pop-up store turu (5 sehir × 2 hafta)
- Egitim kurumu seminer serisi (10 sehir × 1 ay)

Amac: flow nodes'larinda (`{{city_options}}`, `{{event_date}}`, `{{location_address}}`) tenant-spesifik city listesi erisilebilsin; hardcode olmasin; tenant admin UI'dan guncelleyebilsin.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | Tenant `campaign_config` JSONB alani + UI editor (name, cities[], dates[], locations[]) | `tenant_settings` tablosu row |
| AC-2 | Flow variable `{{campaign.cities}}` template substitution'da `[Dublin, Cork]` donuyor | `TemplateSubstitution.cs` unit test |
| AC-3 | Flow condition node `city_choice` intent → tenant cities ile match eder | Intent detector'a dynamic keyword inject |
| AC-4 | Campaign active window dis gonderim engeli (start_date oncesi / end_date sonrasi outbound SKIP) | Scheduled job guard |
| AC-5 | 1+ campaign support (tenant ayni anda 2 event calistirabilsin) | `campaign_id` slug-based, default "primary" |
| AC-6 | Campaign config degisince flow cache invalidate | Automation cache layer |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| JSONB (array of objects) — ayri tablo yerine | Nested city/date/location pattern, query degil read-often | EXPECTED: CQ7 schema simplicity |
| Backend'de okur, Automation'a Shared DTO ile gecer | Single source of truth, servisler arasi dogrudan ref yasak | CQ5/CQ9: ok |
| Campaign slug free-form (tenant tanimlar) | Generic — "roadshow_ireland", "summer_tour", "back_to_school" | — |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Tenant Settings API | `arch/contracts/tenant-settings.json` (mevcut + additive `campaign_config`) |
| DB Schema | `arch/db/tenant-registry.sql` + ALTER |
| Shared DTO | `Invekto.Shared/Contracts/Campaigns/CampaignConfig.cs` (yeni) |
| Error Codes | INV-BE-090 (campaign_not_found), INV-BE-091 (campaign_window_closed) |

## 5. Scope Boundaries

### In Scope
- `tenant_settings.campaign_config JSONB` kolonu
- `CampaignConfig` Shared DTO (Name, Slug, Cities[], Dates[], Locations[], StartAt, EndAt, Active)
- Dashboard campaign editor sayfasi
- Template substitution genisletmesi (`{{campaign.*}}` namespace)
- Flow pre-send guard: active window kontrolu

### Out of Scope (Explicit)
- Campaign analytics/reporting (separate PKT backlog)
- Dynamic slot booking per-city (ayri feature: see `tenant-field-mapping.md` for slot_slots field)
- Multi-language campaign content (per-city translation — v2)

### Degismeyen Alanlar (Pre-existing)
- `tenant_settings` mevcut kolonlari
- Flow engine execution
- Template substitution mevcut namespace'leri (`{{lead.*}}`, `{{tenant.*}}`)

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Backend | CRUD API + tenant cache layer | Yeni endpoint set |
| Automation | Template substitution + pre-send window guard | Minor |
| Dashboard | Campaign editor UI | Yeni page |
| Shared | CampaignConfig DTO | Yeni |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|-----------|
| JSONB unbounded (tenant 100+ city yazabilir) | LOW | Validation: max 20 city, max 10 date per campaign |
| Window guard flow'u yanlis yerde keser (lead dort gun onceden mesaj istedi) | MEDIUM | Guard sadece OUTBOUND scheduled'a; INBOUND reply her zaman isler |
| Tenant multiple campaign overlapping dates | MEDIUM | `campaign_id` slug uniqueness + per-lead `active_campaign_slug` |

## 8. Pilot Consumer

Dent Adavista — "roadshow_ireland" slug, 2 city (Dublin, Cork), tarih aralik konfigurasyonu. Detay: `DentAdavista/plan/pilot-checklist.md`.
