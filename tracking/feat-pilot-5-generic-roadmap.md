# 5 Generic Feature Roadmap — Dent Adavista Pilot Unblocker

> **Slug:** feat-pilot-5-generic-roadmap | **Durum:** SUPERSEDED 2026-04-21 | **Faz:** Historical Planning Reference
> **Amac:** FEAT-MCC + FEAT-LIW + FEAT-VCP + FEAT-EFS + FEAT-TFM generic implementation + Dent pilot launch.
>
> ⚠️ **SUPERSEDED:** Bu dosya 2026-04-18 tarihli ilk planlama dokumanidir. Pilot Launch Mode (2026-04-21) ile 18-paketlik master queue `pilot-launch-roadmap.md`'ye tasindi. Execution queue olarak **ARTIK KULLANILMIYOR** — sadece planning phase interview checklist ve arch touchpoints referansi icin korundu.
> **Yeni otorite:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md)
>
> **Durum ozeti (2026-04-21 itibariyla):** LIW DONE+DEPLOYED, VCP A+B DONE+DEPLOYED (C kaldi), WTP DONE+DEPLOYED, TFM MVP DONE+DEPLOYED (UI/SYNC/FLOW/CACHE alt paketler roadmap'te), EFS + MCC DRAFT (roadmap FAZ 3).

## Executive Summary

FEAT-WTP DONE+DEPLOYED (2026-04-18 commit d5679cd) ile "insan hissi" uclugunun (HFM-1 + HFM-2 + WTP) uc kenari tamamlandi. Dent Adavista pilot launch icin geri kalan 5 generic feature:

| # | Spec | Risk | Effort | Pilot Gate | Paralel ile |
|---|------|------|--------|------------|-------------|
| 1 | [LIW — Lead Intake Webhook](../arch/features/lead-intake-webhook.md) | MEDIUM | 3-5 gun | **PILOT CRITICAL** (landing + WA direct) | VCP ile |
| 2 | [VCP — Video Consultation Provider](../arch/features/video-consultation-provider.md) | LOW | 2-3 gun | **PILOT CRITICAL** (Dublin/Cork appointment booking) | LIW ile |
| 3 | [EFS — Event Followup Sequence](../arch/features/event-followup-sequence.md) | LOW | 2-3 gun | **PILOT CRITICAL** (post-roadshow nurture) | MCC ile |
| 4 | [MCC — Multi-City Campaign](../arch/features/multi-city-campaign.md) | LOW | 1-2 gun | Nice-to-have (Dublin/Cork config) | EFS ile |
| 5 | [TFM — Tenant Field Mapping](../arch/features/tenant-field-mapping.md) | MEDIUM | 5-7 gun | Nice-to-have (custom_N semantic) | Standalone |

**Sequencing:**
- **Sprint A (1 hafta, paralel):** LIW + VCP (pilot kritik yollar)
- **Sprint B (3-4 gun, paralel):** EFS + MCC (nurture + config)
- **Sprint C (opsiyonel, pilot sonra):** TFM (10-field overlay, en karmasik; pilot MVP 5-field custom_1..5 hardcode kullanir, TFM sonra refactor)

Bu sira Dent Adavista pilot'u **1.5 haftada** go-live'a getirir (TFM hariç, sonra semantic overlay migration).

---

## 1. LIW — Lead Intake Webhook (MEDIUM, 3-5 gun)

**Plan session hedefi:** `POST /api/v1/leads/intake/{source_slug}` endpoint + per-tenant API key + field map + dup merge + welcome flow trigger + WA direct entry.

**Pre-implementation interview checklist:**
- [ ] API key rotation UX: tenant admin kac kez rotate edebilir, eski key invalidate TTL?
- [ ] Phone E.164 library: Backend'de NuGet `libphonenumber-csharp` mi, yoksa manuel TR/IE parse?
- [ ] Welcome flow auto-trigger: `tenant_settings.welcome_flow_slug` default deger ne? "welcome_default" yoksa no-trigger mi?
- [ ] Consent field source: checkbox mi, implied consent mi, GDPR-compliant explicit zorunlu mu?
- [ ] Rate limit: per-API-key req/s — 100/s yeterli mi?
- [ ] Duplicate window: 30g yeterli mi, tenant override olsun mu?

**Arch touchpoints:**
- `arch/contracts/lead-intake.json` (yeni)
- `arch/db/pkt6b1-niche-business.sql` ALTER (`intake_metadata JSONB`)
- `Invekto.Shared/Contracts/Leads/LeadIntakeRequest.cs`
- `arch/errors.md` INV-BE-092..095
- `src/Invekto.Backend/Controllers/LeadIntakeController.cs` (yeni)
- `src/Invekto.Backend/Services/LeadIntakeService.cs` (yeni)
- `src/Invekto.Backend/Services/ApiKeyAuthMiddleware.cs` (yeni, /api/v1/leads/intake/ prefix-scoped)
- Dashboard: Ayarlar > Landing Webhook page (API key manager + field map editor + dry-run preview)
- WAA INMA webhook: `InmaInboundController` extension — phone match leads yoksa auto-create

**Pilot Consumer (Dent):**
- Source slug: `ireland-roadshow-lp` (reklam CTA kendi landing page'inden)
- Source slug: `wa-direct` (`wa.me/+90...` reklam tiklanmasi)
- Field map: `{ ad_soyad: name, telefon: phone, sehir: custom_1 }` (custom_1 = roadshow_city, TFM sonra semantic)
- Welcome flow: `welcome_default` (HFM-1 chunks + WTP variant rotation)

**Risk watchpoints:**
- API key leak → client-side JS'de expose; mitigation rate limit + rotation UI
- Phone parse fail → fail-soft 400 with helpful error, log to metric
- Tenant field map hatali → dry-run endpoint + UI preview zorunlu

---

## 2. VCP — Video Consultation Provider (LOW, 2-3 gun)

**Plan session hedefi:** `IVideoConsultProvider` Shared interface + `GoogleMeetMockProvider` (OAuth-free, deterministic mock link) + appointment T-24h/T-1h reminder Hangfire job + ICS file + `{{meeting_link}}` template substitution.

**Pre-implementation interview checklist:**
- [ ] Mock provider link format: `https://meet.google.com/mock-{hash8}` mi yoksa `mock.meet.invekto.com/{id}` mi?
- [ ] Attendee email yoksa (WA-only lead) nasil: dentist+coordinator invite, lead sadece WA link mi?
- [ ] Reminder template separate mi yoksa mevcut `appointment_reminder` template mi kullanilacak?
- [ ] ICS DTSTART TZID: tenant TZ mi, attendee TZ mi (Dublin vs Istanbul)?
- [ ] Production GoogleMeetProvider pilot hafta 2'de mi yoksa pilot go-live ile birlikte mi?

**Arch touchpoints:**
- `Invekto.Shared/Contracts/Video/IVideoConsultProvider.cs`
- `Invekto.Shared/Contracts/Video/MeetingResult.cs`
- `Invekto.Shared/Services/IcsGenerator.cs` (helper)
- `arch/db/appointments.sql` ALTER (`meeting_link TEXT`, `meeting_provider VARCHAR(20)`, `calendar_event_id TEXT`)
- `arch/errors.md` INV-INT-140..142
- `src/Invekto.Integrations/Services/GoogleMeet/GoogleMeetMockProvider.cs` (yeni)
- `src/Invekto.Integrations/Services/GoogleMeet/GoogleMeetProvider.cs` (prod, OAuth — v2)
- `src/Invekto.Appointments/Services/MeetingScheduler.cs` (yeni; provider DI + Hangfire registration)
- Backend OAuth callback: `/api/v1/integrations/google/callback` (prod)
- Dashboard: Ayarlar > Video Provider section

**Pilot Consumer (Dent):**
- Mock provider, pilot ilk 1 hafta — Workspace OAuth setup yok
- Reminder: T-24h (Dublin gece vakti, dentist msj) + T-1h (randevu oncesi lead + dentist)
- ICS: DTSTART TZ=Europe/Istanbul (dentist reference), attendee Dublin TZ Calendar'in cevirisi

**Risk watchpoints:**
- Mock provider prod'a sizmasin — startup check: `env=prod + provider=mock → WARN` (tenant explicit opt-in required)
- OAuth token 6-ay expire → monitoring + re-consent UI (prod paket)
- Calendar API quota → per-tenant daily cap + backoff

---

## 3. EFS — Event Followup Sequence (LOW, 2-3 gun)

**Plan session hedefi:** `tenant_settings.followup_sequence_config JSONB` + Hangfire `BackgroundJob.Schedule` per-lead-per-stage + 4 warm pool trigger (no-reply/declined/timeout/on-hold N gun) + exit conditions + A/B control group + opt-out footer + per-stage metric.

**Pre-implementation interview checklist:**
- [ ] Mevcut Marketing servisi (port 7112) EFS orchestrator'u barindiracak mi, yoksa yeni servis mi?
- [ ] A/B group default %50/%50 mi, tenant override mi? Min sample size 100 mu?
- [ ] Opt-out footer tenant-level template mi, platform default mi?
- [ ] Multi-sequence per lead yasak (v1) — ikinci trigger geldiginde eski stages cancel mi yoksa skip mi?
- [ ] Max stage 5, max window 30 gun — Dent 3-stage 14-gun OK; diger tenant extension v2 mi?

**Arch touchpoints:**
- `arch/contracts/followup-sequence.json` (yeni)
- `arch/db/marketing.sql` ALTER (`leads.followup_state JSONB`, `leads.followup_ab_group VARCHAR(10)`)
- `arch/errors.md` INV-MK-050..052
- `src/Invekto.Marketing/Services/FollowupOrchestrator.cs` (yeni)
- `src/Invekto.Marketing/Services/FollowupJob.cs` (Hangfire handler)
- `src/Invekto.Automation/Services/Orchestrator.cs` extension (4 trigger hook → FollowupOrchestrator.EnqueueAsync)
- `Invekto.Shared/Contracts/Followup/SequenceConfig.cs`
- Dashboard: Ayarlar > Followup Sequence editor (stage list + delay days + template picker + A/B slider)

**Pilot Consumer (Dent):**
- 3-stage post-roadshow: Day 3 (gentle check-in) / Day 7 (offer reminder) / Day 14 (last chance)
- Trigger: no-reply welcome (3 gun cevapsiz) + offer declined + offer timeout + on-hold 7 gun
- A/B %50/%50 — control group "no drip" kontrolu

**Risk watchpoints:**
- Opt-out race: STOP/opt_out check **job execution basinda** (not enqueue basinda) — lead STOP sonrasi scheduled job'a guard
- Backlog 1000+ lead → `marketing-followup` dedicated Hangfire queue + queue size metric
- Tenant 10-stage yazarsa → validation max 5 stage, max 30g window

---

## 4. MCC — Multi-City Campaign (LOW, 1-2 gun)

**Plan session hedefi:** `tenant_settings.campaign_config JSONB` + `CampaignConfig` Shared DTO + Dashboard editor + `{{campaign.cities}}` / `{{campaign.event_date}}` template substitution + outbound window guard.

**Pre-implementation interview checklist:**
- [ ] Campaign slug uniqueness tenant-scope mi, global mi? Multiple active campaign ayni tenant'ta OK mi?
- [ ] Active window inclusive/exclusive? `start_date=T` + `start_at=09:00` mi yoksa sadece tarih mi?
- [ ] `{{campaign.cities}}` render: comma-separated string mi, JSON array mi, templated list mi?
- [ ] Cache invalidate: tenant_settings degisiminde Backend broadcasts Automation'a mi, Automation poll mu?
- [ ] Max 20 city, max 10 date yeterli mi?

**Arch touchpoints:**
- `arch/contracts/tenant-settings.json` additive (`campaign_config` field)
- `arch/db/tenant-registry.sql` ALTER (tenant_settings.campaign_config JSONB)
- `Invekto.Shared/Contracts/Campaigns/CampaignConfig.cs` + `CampaignCity.cs`
- `arch/errors.md` INV-BE-090..091
- `src/Invekto.Backend/Controllers/CampaignConfigController.cs`
- `src/Invekto.Automation/Services/TemplateSubstitutionService.cs` extension (`{{campaign.*}}` namespace)
- `src/Invekto.Automation/Services/OutboundWindowGuard.cs` (yeni; pre-send check)
- Dashboard: Ayarlar > Campaigns page (campaign list + city/date editor)

**Pilot Consumer (Dent):**
- Slug: `roadshow_ireland`
- Cities: `[{ slug: "dublin", name: "Dublin", date: "2026-03-14" }, { slug: "cork", name: "Cork", date: "2026-03-15" }]`
- Active window: 2026-02-28 → 2026-03-20 (pre-promo + post-event nurture dahil)
- Template substitution: "Will you be in {{campaign.cities_human}} on {{campaign.event_date}}?"

**Risk watchpoints:**
- Outbound guard INBOUND reply'i kesmesin — guard sadece scheduled/drip/reminder; inbound_reply_flow unconditional
- Multiple campaign overlapping dates — per-lead `active_campaign_slug` lead'in hangi campaign icinde oldugu takip edilir
- JSONB unbounded → validation max 20 city + max 10 date

---

## 5. TFM — Tenant Field Mapping (MEDIUM, 5-7 gun)

**Plan session hedefi:** INMA 10-field semantic overlay. `tenant_settings.field_mapping JSONB` + `ITenantFieldResolver` Shared + INMA sync worker + template substitution + flow builder UI extension + validation engine + reserved names guard.

**Pre-implementation interview checklist:**
- [ ] INMA sync: webhook-driven vs polling (daily vs per-lead)?
- [ ] Reserved names list: `[id, tenant_id, phone, full_name, email, created_at, pipeline_status]` yeterli mi?
- [ ] Enum validation: INMA'da degisirse INSE cache invalidate + tenant admin notification UI nasil?
- [ ] Resolver cache IMemoryCache 5dk TTL — tenant update anlik gorulmeli mi (cache bust endpoint)?
- [ ] INMA READONLY pattern — TFM sync SELECT-only (mevcut lisans pattern'i) mi, write gereksinimi var mi?
- [ ] 11. field ihtiyaci acigi — roadmap option: INMA ile 15-field gorusmesi vs `lead_extensions` tablosu v2?
- [ ] Flow builder UI condition node — mevcut `LeadFieldPicker` component refactor mi, yeni mi?

**Arch touchpoints:**
- `arch/contracts/tenant-field-mapping.json` (yeni)
- `arch/db/pkt6b1-niche-business.sql` ALTER (`leads.custom_1` ... `custom_10 TEXT`)
- `Invekto.Shared/Contracts/Inma/InmaCustomFieldsDto.cs`
- `Invekto.Shared/Services/ITenantFieldResolver.cs` + `TenantFieldResolver` impl (IMemoryCache)
- `arch/errors.md` INV-BE-096..099
- `src/Invekto.Backend/Controllers/FieldMappingController.cs`
- `src/Invekto.Backend/Services/FieldMappingValidator.cs`
- `src/Invekto.Integrations/Services/Inma/InmaCustomFieldSync.cs` (yeni; webhook + polling)
- `src/Invekto.Automation/Services/TemplateSubstitutionService.cs` extension (`{{lead.<semantic>}}`)
- Dashboard: Ayarlar > Field Mapping editor (semantic name + source dropdown + type + enum_values)
- Dashboard: Flow Builder extension — condition node semantic picker
- Backend startup validation: tenant's reserved name collision → WARN log

**Pilot Consumer (Dent):**
- 5 alan MVP: `roadshow_city` → custom_1, `appointment_slot` → custom_2, `offer_status` → custom_3, `deposit_status` → custom_4, `flight_booked` → custom_5
- 5 alan reserve (custom_6..10 gelecek)
- Types: enum (city, offer_status, deposit_status), date (appointment_slot), bool (flight_booked)

**Risk watchpoints:**
- INMA field rename → drift detection daily check + mapping snapshot version
- Reserved name collision → startup validation + UI warning
- 10 field yeter mi → pilot pocket ekleyip v2'de `lead_extensions` tablosu roadmap

---

## Dependency Graph

```
LIW (landing/WA entry)
  ├─ trigger → welcome flow (WTP variant rotation) ✓
  └─ trigger → EFS warm pool (if no-reply)

VCP (meeting link)
  └─ template substitution {{meeting_link}} (Automation) ✓

EFS (drip nurture)
  ├─ warm pool trigger (Automation orchestrator hook)
  └─ per-stage template (WTP pattern) ✓

MCC (campaign config)
  ├─ template substitution {{campaign.*}} (Automation)
  └─ outbound window guard (pre-send check)

TFM (semantic overlay)
  ├─ LIW field map can map to custom_N raw (no-TFM) OR semantic (TFM-integrated)
  ├─ flow builder condition node semantic picker
  └─ template substitution {{lead.<semantic>}}
```

**Critical path:** LIW → VCP → EFS (Dent pilot gating).
**Non-critical:** MCC (can hardcode Dublin/Cork pilot ilk hafta) + TFM (custom_1..5 raw pilot ilk sprint, semantic refactor sprint C'de).

---

## Parallel Execution Opportunities

| Sprint | Paralel Paketler | Gerekce |
|--------|------------------|---------|
| A (hafta 1) | **LIW + VCP** | Farkli servis scope'lari (Backend+Automation vs Integrations+Appointments); Shared DTO namespace ayri (Leads vs Video) |
| B (hafta 2 bas) | **EFS + MCC** | Farkli JSONB kolonlari (followup_sequence_config vs campaign_config); farkli servis (Marketing vs Automation); Dashboard page'leri ayri |
| C (hafta 2 orta) | **TFM standalone** | Karmasik + genis scope (Shared Resolver + Integrations sync worker + Automation substitution + Dashboard flow builder extension); paralel paket mantikli degil |

**Context discipline uyarisi:** Q'nun `CLAUDE.md`'indeki "Paket tamamlaninca /clear oner" kurali buradaki paralel paketler icin onemli — her Sprint sonrasi `/clear` + session-prompt zorunlu.

---

## Per-Feature Open Questions (Planning Phase Input)

Q'ya interview phase'de yoneltilecek kritik sorular (her feature'in "Pre-implementation interview checklist" bolumunden extract):

**LIW (6 soru):** API key rotation UX, phone parse lib, welcome flow default, consent source, rate limit, dup window.
**VCP (5 soru):** Mock link format, no-email WA-only attendee, reminder template, ICS TZ, prod OAuth timing.
**EFS (5 soru):** Marketing vs yeni servis, A/B default + sample size, opt-out footer source, multi-sequence policy, max stage/window.
**MCC (5 soru):** Slug uniqueness, active window granularity, render format, cache invalidation, limits.
**TFM (7 soru):** Sync mode, reserved names list, enum change workflow, cache TTL, READONLY pattern, 11. field roadmap, flow builder refactor.

**Toplam:** 28 interview sorusu; her feature ayri plan session'inda ele alinir.

---

## Pilot Gating Sequence (Dent Adavista Go-Live)

**T-14 gun:** Sprint A start (LIW + VCP paralel)
**T-10 gun:** LIW deploy + smoke (Dent landing test lead)
**T-9 gun:** VCP mock deploy + Dent dentist calendar smoke
**T-8 gun:** Sprint B start (EFS + MCC paralel)
**T-6 gun:** EFS deploy + 3-stage test (1 test lead, yapay delay shortcuts)
**T-5 gun:** MCC deploy + Dublin/Cork campaign config + outbound window aktif
**T-4 gun:** Dent pilot config: 48 template upload (DONE 2026-04-18 commit 5d0ba4c) + field map raw custom_1..5 + API key generate + welcome flow assignment
**T-2 gun:** End-to-end smoke (test lead landing → welcome → FAQ → offer → booking → meeting → reminders)
**T-0 gun:** Soft launch — reklam CTA aktif, ilk 50 gercek lead izleme
**T+7 gun:** Post-launch review + TFM Sprint C baslat (semantic overlay migration)

---

## Execution Queue Impact (session-memory update)

Bu plan session-memory.md Execution Queue'ya eklenecek entry'ler:

```
| NEW | FEAT-LIW implementation (Sprint A, paralel VCP ile) | PENDING | Pre-req: interview 6 soru |
| NEW | FEAT-VCP implementation (Sprint A, paralel LIW ile) | PENDING | Pre-req: interview 5 soru |
| NEW | FEAT-EFS implementation (Sprint B, paralel MCC ile) | PENDING | Pre-req: LIW entry triggers + interview 5 soru |
| NEW | FEAT-MCC implementation (Sprint B, paralel EFS ile) | PENDING | Pre-req: interview 5 soru |
| NEW | FEAT-TFM implementation (Sprint C, standalone) | PENDING | Pre-req: pilot go-live sonra; interview 7 soru |
| NEW | Dent pilot launch (tracking/feat-pilot-5-generic-roadmap.md T-0) | BLOCKED by Sprint A+B | 2026-04-XX go-live tarihi Q karari |
```

---

## References

- [FEAT-WTP (DONE)](../arch/features/welcome-template-pack.md) — tamamlandi 2026-04-18 commit d5679cd
- [FEAT-MCC](../arch/features/multi-city-campaign.md)
- [FEAT-LIW](../arch/features/lead-intake-webhook.md)
- [FEAT-VCP](../arch/features/video-consultation-provider.md)
- [FEAT-EFS](../arch/features/event-followup-sequence.md)
- [FEAT-TFM](../arch/features/tenant-field-mapping.md)
- [Dent Adavista plan README](../DentAdavista/plan/README.md)
- [Dent pilot checklist](../DentAdavista/plan/pilot-checklist.md)
- Tracking master: [README.md](README.md)

---

**Hazirlayan:** DevAgent 2026-04-18 | **Ilk sprint baslama:** Q karari (interview + /auto LIW + VCP paralel baslatma)
