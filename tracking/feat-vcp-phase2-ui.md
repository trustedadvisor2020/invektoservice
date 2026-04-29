<!-- Status: BACKLOG | 2026-04-29 -->
# FEAT-VCP-PHASE2-UI — Video Provider Settings Dashboard UI

> **Tarih:** 2026-04-29
> **Durum:** BACKLOG (B0 OAuth provision sonrası)
> **Kategori:** DEV
> **Kanban:** D033 (board_key='inse', BACKLOG kolonu)
> **Öncelik:** P2 (UI gap audit 2026-04-29 — video provider seçim manuel SQL'le yapılıyor, UI yok)

---

## Özet

UI Gap Audit (2026-04-29) — `tenant_settings.video_provider` (VARCHAR) + `video_provider_config` (JSONB) **backend'de hazır, FEAT-VCP Chunk A/B prod'da DEPLOYED ama Dashboard UI'sı YOK**. Şu an manuel SQL UPDATE.

Provider seçenekleri:
- `mock` (default — GoogleMeetMockProvider deterministic SHA256 link, ICS RFC 5545)
- `googlemeet` (Chunk C OAuth provision sonrası)

`video_provider_config` JSONB:
```json
{
  "google_oauth_client_id": "...",   // googlemeet only
  "google_oauth_client_secret_ref": "...", // KMS ref, plain TEXT YASAK
  "google_workspace_user_email": "...",
  "default_meeting_duration_minutes": 30,
  "calendar_event_organizer": "Dr. Adı"
}
```

Hedef: Settings → "Video Provider" tab altında provider picker + config form.

---

## Scope

### Backend
- `GET /api/v1/tenant-settings/video-provider` → `{video_provider, video_provider_config}` (secret ref masked)
- `PUT /api/v1/tenant-settings/video-provider` → atomic UPDATE, validation:
  - `video_provider` whitelist: `mock` | `googlemeet`
  - `video_provider_config` JSONB schema validation per provider
  - GoogleMeet için OAuth client_id + client_secret_ref + workspace_user_email zorunlu
  - secret_ref KMS path validation (örn `kms://google-oauth/<tenant>/secret`)
- Cross-tenant 403 + TenantContext guard
- VideoProviderFactory cache invalidate push
- Error codes: INV-BE-124 (validation), INV-INT-148 (provider config malformed), INV-INT-149 (KMS ref invalid)

### Dashboard
- Yeni page: `src/Invekto.Backend/Dashboard/src/pages/settings/VideoProviderSettingsPage.tsx`
- Route: `/settings/video-provider`
- Provider seçici: radio (Mock / Google Meet)
- Provider seçimine göre dinamik config form:
  - Mock: empty (no config gerekli)
  - Google Meet: 4 input (Client ID + Secret Ref + Workspace Email + Default Duration)
- Test connection butonu (GoogleMeet için): ICS round-trip mock smoke
- Save + 403 typed catch + 4 INV error code typed display

### Migration
- Migration N: validation guard (mevcut FEAT-VCP-A Migration 023 zaten kolon ekledi, sadece tenant default `mock` set)

---

## Bağımlılıklar
- FEAT-VCP Chunk A ✅ DEPLOYED (Migration 023)
- FEAT-VCP Chunk B ✅ DEPLOYED (Appointments + Hangfire reminders + Migration 024)
- **FEAT-VCP Chunk C (B0 backlog)** — GoogleMeet prod OAuth Q-provision blocker
  - GoogleMeet seçimi UI'da görünecek ama "OAuth provision pending" disabled state
  - Veya UI Mock-only kalacak ta ki Chunk C tamamlanana dek

---

## Aktivasyon Gate
- Pilot smoke S6 Mock provider ile yeterli ✅
- Production GoogleMeet için Chunk C OAuth provision tamamlandığında UI ile birlikte deploy

---

## Açık Sorular
1. Mock provider üretim tenant'ı için aktif kalsın mı (gerçek Meet yerine fake link)? Yoksa pilot/test only?
2. KMS ref pattern: hangi KMS kullanılacak (Azure KeyVault, AWS KMS, native Postgres pgcrypto)?
3. Secret rotation flow: Backend'den Q manuel rotate mı, yoksa Q UI'da "Rotate Secret" butonu mu?
4. `default_meeting_duration_minutes` tenant-default mi, lead-bazlı override mı?

---

## Referanslar
- FEAT-VCP Chunk A: [arch/features/video-consultation-provider.md](../arch/features/video-consultation-provider.md)
- B0 backlog: [tracking/pilot-launch-roadmap.md](pilot-launch-roadmap.md) Backlog B0
- Migration 023: [arch/db/migrations/023-tenant-settings-video-provider.sql](../arch/db/migrations/023-tenant-settings-video-provider.sql)
- UI Gap Audit raporu (session 2026-04-29 conversation log)
