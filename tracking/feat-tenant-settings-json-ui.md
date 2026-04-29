<!-- Status: BACKLOG | 2026-04-29 -->
# FEAT-TENANT-SETTINGS-JSON-UI — Generic Tenant Settings JSONB Editor

> **Tarih:** 2026-04-29
> **Durum:** BACKLOG (post-pilot)
> **Kategori:** DEV
> **Kanban:** D034 (board_key='inse', BACKLOG kolonu)
> **Öncelik:** P2 (UI gap audit 2026-04-29 — `tenant_registry.settings_json` 20+ servis aktif okuyor, UI yok)

---

## Özet

UI Gap Audit (2026-04-29) — `tenant_registry.settings_json` JSONB **aktif kullanımda** (2/7 tenant dolu, 20+ servis okuyor) ama Dashboard UI'sı YOK. Şu an manuel SQL UPDATE.

### Mevcut Kullanım
| Key | Tip | Anlam |
|-----|-----|-------|
| `flow_builder_api_key` | string (secret) | Flow Builder publish API key |
| `wapcrm` | object | WapCRM integration ayarları |
| `default_tone` | string | AI yanıt tonu |
| `confidence_threshold` | number | AI intent confidence eşiği |

Kullanıcı: 7 tenant'tan 2 tanesi (tenant_id 1 + 5050).

---

## Scope

### Backend
- `GET /api/ops/tenants/{tenantId}/settings-json` → tenant_registry.settings_json (secret key'ler masked: `flow_builder_api_key` → `***last4`)
- `PUT /api/ops/tenants/{tenantId}/settings-json` → atomic JSONB UPDATE
  - Validation:
    - JSON parse + max 10KB size guard
    - Reserved key whitelist (sadece bilinen 4 key + future extensible — schema-less değil)
    - Secret key (flow_builder_api_key) plain text yazma yasak (rotate endpoint ayrı)
  - Cross-tenant guard (Ops auth — TenantsPage pattern, /api/ops scope)
- `POST /api/ops/tenants/{tenantId}/settings-json/rotate-secret/{key}` → flow_builder_api_key rotation (LIW pattern)
- Error codes: INV-BE-125 (validation), INV-BE-126 (size cap), INV-BE-127 (reserved key violation)

### Dashboard
- Yeni page: `src/Invekto.Backend/Dashboard/src/pages/settings/TenantSettingsJsonPage.tsx`
  - SuperAdmin only (Ops mode, `/ops/*` route)
- Layout: LicensesPage `features_json` editor pattern reuse
  - Read-only display: 4 key + son 4 karakter masked secret
  - Edit mode: textarea raw JSON OR structured key/value list
  - Save button + 403 typed catch + size cap warning
  - Rotate Secret button (flow_builder_api_key için LIW pattern)
- Route: `/ops/tenants/{tenantId}/settings-json` (TenantsPage'den link)

### Migration
- Migration N: validation guard (whitelist key list, size cap CHECK constraint?)
- Yeni JSONB key tanımı: typed schema dosyası (`arch/contracts/tenant-settings-json-schema.json`)

---

## Bağımlılıklar
- `tenant_registry.settings_json` JSONB ✅ mevcut
- 20+ servis aktif okuyor (TenantRegistryRepository, OutboundRepository, AutomationOrchestrator, vs.)
- LicensesPage features_json editor precedent ✅
- Ops auth pattern (Migration 038 D027 audit fix sonrası TenantId=0 SuperAdmin canonical) ✅

---

## Aktivasyon Gate
- Pilot Stage 3 sonrası (P2 öncelik, blocker değil)
- 2. müşteri onboarding pre-req (her tenant kendi flow_builder_api_key + tone ayarını set etmeli)

---

## Açık Sorular (paket aktivasyonu öncesi)

1. **Schema-strict vs schema-less:** 4 bilinen key whitelist mi yoksa generic JSON editor (her key kabul) mi?
2. **Secret key masking:** `flow_builder_api_key` görüntülenir mi (last4 mask) yoksa hiç UI'da render edilmez mi (rotate-only)?
3. **Tenant-scope edit yetkisi:** Sadece SuperAdmin mi (ops mode) yoksa tenant operatörü kendi tenant'ı için edit edebilir mi (sector/timezone gibi)?
4. **`wapcrm` key shape validation:** WapCRM integration object şeması arch/contracts'a eklensin mi?
5. **Audit log:** Her PUT için audit log row mı (LIW pattern), yoksa updated_at yeterli mi?

---

## İlgili UI Gap Audit Bulguları

- `tenant_registry.callback_url` → DEAD COLUMN (0/7 tenant dolu, code'da appsettings global config kullanılıyor) → DROP candidate, UI gereksiz, opsiyonel future Migration cleanup
- `tenant_settings.*` 12 kolon ✅ tam UI kapsamı (D027-D033 paketleriyle)

---

## Referanslar

- UI Gap Audit raporu (session 2026-04-29 conversation log)
- LicensesPage features_json editor: [src/Invekto.Backend/Dashboard/src/pages/LicensesPage.tsx](../src/Invekto.Backend/Dashboard/src/pages/LicensesPage.tsx)
- TenantRegistryRepository.cs (settings_json read pattern)
- LIW Rotate Secret pattern: [tracking/20260419-liw-tenant-exists-precheck.md](20260419-liw-tenant-exists-precheck.md)
