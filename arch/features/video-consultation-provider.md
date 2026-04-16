# SPEC: Video Consultation Provider

> **Spec ID:** FEAT-VCP | **Paket:** TBD | **Risk:** LOW
> **Yazar:** Q | **Son Guncelleme:** 2026-04-16 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Tenant'in appointment-confirmed lead'i icin otomatik video consultation link + calendar invite ureten generic katman. Provider interchangeable: Google Meet (ilk), Zoom (v2), MS Teams (v2), Jitsi (self-host), Daily.co (API).

v1 icin `GoogleMeetMockProvider` (OAuth consent-free mock link generator) + production `GoogleMeetProvider` (Workspace OAuth). Amac: pilot tenant icin ilk gun pilot calisabilsin, production tenant Workspace setup'ini sonradan yapsin.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `IVideoConsultProvider` Shared interface — `CreateMeetingAsync(title, startAt, duration, attendees[]) → MeetingResult` | Shared project compile |
| AC-2 | `GoogleMeetMockProvider` deterministic fake link (format: `https://meet.google.com/mock-{hash}`) — OAuth yok | Unit test |
| AC-3 | `GoogleMeetProvider` (prod) Workspace OAuth refresh token + Calendar API events.create | Integration test stub |
| AC-4 | Provider selection per-tenant (`tenant_settings.video_provider` enum) | DI resolution by tenant |
| AC-5 | ICS file attachment + WA outbound template (placeholder `{{meeting_link}}`, `{{meeting_start_iso}}`) | Outbound message contains link |
| AC-6 | T-24h + T-1h reminder hook (Hangfire job per-meeting) | Hangfire job list shows 2 scheduled |
| AC-7 | Post-meeting satisfaction prompt T+30m (optional, tenant config flag) | Automation followup flow |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Interface Shared'da, impl Integrations servisinde | Microservice isolation; Backend cagirmak icin Shared yeterli | CQ5/CQ9: ok |
| Mock first, production ikinci | Pilot tenant Workspace OAuth setup beklemeden pilot baslatabilsin | — |
| Google Meet baslangic secimi (Zoom/Teams degil) | Link generation complexity: Meet en dusuk (Calendar event yaratinca otomatik Meet link) | — |
| Reminder'lar Hangfire'da (G7 mevcut) | `RecurringJob` degil `BackgroundJob.Schedule` (bir defalik) | CQ11: Hangfire schema evidence |
| Attendee email required — phone-only lead icin lead.email bos ise dentist + coordinator invite, lead'e sadece WA link | Calendar invite "join without account" Meet'te mumkun | — |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Provider interface | `Invekto.Shared/Contracts/Video/IVideoConsultProvider.cs` (yeni) |
| DTO | `Invekto.Shared/Contracts/Video/MeetingResult.cs`, `AttendeeDto.cs` |
| DB Schema | `arch/db/appointments.sql` (mevcut + `meeting_link TEXT`, `meeting_provider VARCHAR(20)`, `calendar_event_id TEXT` ALTER) |
| Error Codes | INV-INT-140 (oauth_token_invalid), INV-INT-141 (meeting_create_failed), INV-INT-142 (provider_not_configured) |

## 5. Scope Boundaries

### In Scope
- `IVideoConsultProvider` interface + 2 impl (Mock + GoogleMeet)
- `tenant_settings.video_provider` + `video_provider_config JSONB` (OAuth refresh token encrypted)
- Appointments servisinde Provider DI resolution
- T-24h + T-1h reminder scheduling (Hangfire)
- ICS file generation helper (Shared)
- Dashboard: video provider setting UI (OAuth connect flow for GoogleMeet)

### Out of Scope (Explicit)
- Zoom / Teams / Jitsi providers (v2)
- In-app video (WebRTC ourselves) — never, 3rd party
- Meeting recording / transcription (v2)
- Tenant-supplied custom ICS template (standard template yeterli)
- Post-meeting AI summary (v2)

### Degismeyen Alanlar (Pre-existing)
- Mevcut `appointments` tablosu
- Hangfire infrastructure (G7)
- Appointment state machine

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Integrations | GoogleMeet OAuth + Calendar API client | Yeni namespace |
| Appointments | Provider DI + meeting link persistence + reminder job registration | Yeni method |
| Backend | OAuth callback endpoint (consent redirect) + Dashboard proxy | Yeni endpoint |
| Shared | Interface + DTO + ICS helper | Yeni |
| Dashboard | Provider settings UI | Yeni section |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|-----------|
| Google OAuth refresh token 6-month expire | MEDIUM | Monitoring alert + re-consent UI trigger |
| Calendar API quota exhaustion | LOW | Per-tenant rate limit + daily cap |
| Mock provider accidentally deployed to production | LOW | Startup check: production + mock = WARN log; tenant config override required |
| Timezone mismatch (tenant TZ vs attendee TZ) | MEDIUM | ICS DTSTART with explicit TZID; Google Calendar handles conversion |

## 8. Pilot Consumer

Dent Adavista — GoogleMeet provider (mock ilk 1 hafta, prod OAuth sonra). Dentist Istanbul TZ, lead Dublin TZ. Detay: `DentAdavista/plan/pilot-checklist.md`.
