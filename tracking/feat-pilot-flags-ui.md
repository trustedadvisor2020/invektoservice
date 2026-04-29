<!-- Status: BACKLOG | 2026-04-29 -->
# FEAT-PILOT-FLAGS-UI — Pilot Flags Dashboard Toggle UI

> **Tarih:** 2026-04-29
> **Durum:** BACKLOG (post-pilot)
> **Kategori:** DEV
> **Kanban:** D032 (board_key='inse', BACKLOG kolonu)
> **Öncelik:** P2 (UI gap audit 2026-04-29 — pilot flags manuel SQL UPDATE'le yönetiliyor, UI yok)

---

## Özet

UI Gap Audit (2026-04-29) — `tenant_settings` tablosunda 4 kolon **backend'de hazır ama Dashboard UI'sı YOK**. Şu an manuel SQL UPDATE ile yönetiliyor:

| Kolon | Tip | Şu anki yönetim |
|-------|-----|------------------|
| `enforce_message_category` | BOOL | Manuel SQL (FEAT-J2 marketing/transactional category guard) |
| `enable_dynamic_message` | BOOL | Manuel SQL (FEAT-DMP INMA dynamicMessage on/off) |
| `efs_test_mode` | BOOL | Manuel SQL (FEAT-EFS drip delay_days→minutes test mode) |
| `efs_no_reply_threshold_days` | INT | Manuel SQL (FEAT-EFS no-reply trigger threshold) |

Hedef: tek "Advanced Settings" tab altında 4 toggle/input + Backend GET/PUT endpoint.

---

## Scope

### Backend
- `GET /api/v1/tenant-settings/advanced` → `{enforce_message_category, enable_dynamic_message, efs_test_mode, efs_no_reply_threshold_days}`
- `PUT /api/v1/tenant-settings/advanced` → atomic UPDATE 4 kolon, validation:
  - `efs_no_reply_threshold_days` integer ≥1, ≤90
  - 3 BOOL strict bool.TryParse
- Cross-tenant 403 guard (TenantContext check)
- Cache invalidate push (FEAT-DMP enable_dynamic_message değişimi → DynamicFieldsCache invalidate)
- Error codes: INV-BE-122 (validation), INV-BE-123 (DB transient)

### Dashboard
- Yeni page: `src/Invekto.Backend/Dashboard/src/pages/settings/AdvancedSettingsPage.tsx`
- Route: `/settings/advanced`
- 4 input alanı:
  - 3 toggle switch (BOOL)
  - 1 number input (efs_no_reply_threshold_days, 1-90 range)
- Save button + validation feedback + 403 typed catch
- SettingsPage navigasyon link ekle

### Migration
- Migration N (validation): mevcut kolon nullable durumu kontrol + default değer set (BOOL → FALSE, INT → NULL ya da 7 default)

---

## Bağımlılıklar
- Backend tenant_settings tablosu ✅ (Migration 023, 027, 029'dan)
- Frontend Settings sayfa pattern ✅ (FieldMappingSettingsPage / CampaignConfigSettingsPage precedent)

---

## Aktivasyon Gate
- Pilot Stage 3 sonrası (P2 öncelik, blocker değil)
- 2. müşteri onboarding pre-req (her tenant kendi flag'ini set etmeli)

---

## Açık Sorular
1. `enforce_message_category` UI'da görünecek mi yoksa SuperAdmin-only ops setting mi (operatör değil)?
2. `efs_test_mode` üretim tenant'larında kapatılabilir mi yoksa SuperAdmin-only ops mu?
3. Default değerler: 4 alan tenant_registry create'inde otomatik populate edilsin mi?

---

## Referanslar
- UI Gap Audit raporu (session 2026-04-29 conversation log)
- FEAT-DMP: [arch/features/dynamic-message-placeholder.md](../arch/features/dynamic-message-placeholder.md)
- FEAT-EFS: [arch/features/event-followup-sequence.md](../arch/features/event-followup-sequence.md)
- FEAT-J2: opt-out INMA sync (enforce_message_category)
