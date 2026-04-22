# Dent Adavista Pilot — Deployment Checklist

> **Pilot kod adi:** "Gunes" (AI agent persona — sadece Dent'e ozel, feature spec'lerinde gecmez)
> **Generic feature spec'leri:** `arch/features/*.md` (Dent-bagimsiz, her tenant kullanir)

Bu dokuman **sadece Dent Adavista tenant'inin ozel configuration + go-live checklist**'idir. Platform feature'lari generic; Dent ilk tuketici.

## 1. Tenant Provisioning

**Bagimlilik:** Unified Platform P0 (SSO + tenant auto-provision — `arch/platform/inma-inse-unification/`)

### INMA tarafi (source of truth)
- [ ] INMA'da Dent Adavista firma kaydi var mi dogrula
- [ ] `CompanyCode = dentadavista` (INSE tenant_id esleme)
- [ ] `X-CIB-SecretKey` al
- [ ] INMA webhook URL kaydet: `https://app.invekto.com/api/inbound/inma/dentadavista`

### INSE tarafi (enrichment)
- [ ] `tenants` row'unu event-driven auto-provision dogrula (INMA firma create event tetikler)
- [ ] `locale_default = en-US`, `locale_fallback = tr-TR`
- [ ] `timezone_primary = Europe/Dublin` (operasyon), `timezone_clinic = Europe/Istanbul` (dentist)
- [ ] Feature flags: `ai_agent_enabled=true`, `flow_builder=true`, `multi_city_campaign=true`, `followup_sequence=true`, `appointments=true`

### Branding
- [ ] Logo upload (tenant assets)
- [ ] Primary/secondary renk (kurumsal)
- [ ] Agent display name: **Gunes**
- [ ] Agent signature: `Gunes - Dent Adavista Dental Clinic - Kusadasi`

### Kullanicilar (INMA'da, INSE SSO ile)
- [ ] 3 kullanici: `gunes` (agent), `coordinator`, `admin`
- [ ] Rol map: INMA admin->INSE tenant_admin, INMA manager->INSE manager, INMA agent->INSE agent

## 2. Multi-City Campaign Config

**Feature:** `arch/features/multi-city-campaign.md`

```json
{
  "campaigns": [{
    "slug": "roadshow_ireland_2026",
    "name": "Ireland Roadshow 2026",
    "cities": [
      { "slug": "dublin", "name": "Dublin", "country": "IE", "timezone": "Europe/Dublin" },
      { "slug": "cork", "name": "Cork", "country": "IE", "timezone": "Europe/Dublin" }
    ],
    "dates": [
      { "city": "dublin", "date": "2026-03-14", "slots_per_hour": 2, "hours": "09:00-18:00", "lunch_gap": "13:00-14:00" },
      { "city": "cork",   "date": "2026-03-15", "slots_per_hour": 2, "hours": "09:00-18:00", "lunch_gap": "13:00-14:00" }
    ],
    "start_date": "2026-03-01",
    "end_date":   "2026-03-20",
    "active": true
  }]
}
```

- [ ] Campaign config Dashboard'dan kaydet
- [ ] Flow'da `{{campaign.cities}}` substitution dogrula
- [ ] Active window guard test: 2026-03-21 sonrasi outbound SKIP

## 3. Lead Intake Webhook

**Feature:** `arch/features/lead-intake-webhook.md`

### Source: musteri landing page
- [ ] API key uret + musteriye teslim (secure channel)
- [ ] Endpoint: `POST /api/v1/leads/intake/roadshow-landing`
- [ ] Musteri backend'ine webhook entegrasyonu
- [ ] Field mapping tenant config (Dashboard `/settings/lead-intake` editor'un kaydettigi shape — GET `/api/v1/tenant/landing/settings` response ile 1:1):
  ```json
  {
    "field_map": {
      "full_name": "name",
      "phone_number": "phone",
      "email_address": "email",
      "city_preference": "custom_1",
      "country_code": "custom_2",
      "consent_marketing": "consent_marketing"
    },
    "phone_country_hint": "IE"
  }
  ```
  > **Direction:** `{ source_field → canonical }` (UI-natural — tenant kendi form field adi once yazar). Storage JSONB shape ters yonde (`canonical → source_field`); Backend otomatik cevirir (`LiwSettingsService.SerializeFieldMapForStorage`). Doc'ta slug-keyed wrapper (`"roadshow-landing": {...}`) **YOK** — tek `tenant_landing_settings.landing_field_map` JSONB kolonu var (per-tenant, slug-agnostic).

### Contract Quirks (5 nokta — pre-smoke verify gate)

Smoke/debug curl hazirlarken bu 5 maddeyi ONCE tick et. Her biri gercek bir drift cycle'in kaynagi (lessons 2026-04-22 P9 wiring):

- [ ] **Header adi:** `X-Invekto-Api-Key` (NOT `X-Api-Key`). Missing/bozuk key → 401 `INV-BE-100`. Ref: `src/Invekto.Backend/Program.cs:5097`.
- [ ] **Body wrapper:** Payload `{ "fields": { ... } }` icinde olmak zorunda (flat `{ "name": "...", ... }` 400 `INV-BE-109` reddedilir). UTM + referer + submitted_at ayri top-level field. Ref: `src/Invekto.Shared/Contracts/Leads/LeadIntakeRequest.cs:15`.
- [ ] **Consent tipi:** `consent_marketing` alani **boolean** `true`/`false` (JSON `true` veya string `"true"`/`"false"` case-insensitive kabul edilir; string `"yes"`, `"1"`, `"on"` REDDEDILIR → `INV-BE-105`). Ref: `src/Invekto.Backend/Services/FieldMapResolver.cs:128-144` (`bool.TryParse`).
- [ ] **Slug regex:** `{source_slug}` path parametresi `^[a-z0-9][a-z0-9-]{0,49}$` (kucuk harf + digit + **sadece tire** — underscore REDDEDILIR → 400 `INV-BE-101`). `roadshow_landing` ❌ `roadshow-landing` ✅. Ref: `src/Invekto.Backend/Services/LeadIntakeService.cs:21`.
- [ ] **Smoke/debug URL:** Prod sunucuda MCP `invekto-ops server-exec` icinden Backend'e direkt `http://localhost:5000/...` kullan. `https://app.invekto.com` INMA legacy login IIS surface'i (Backend reverse proxy DEGIL) — external URL ile test etmek 404/kimlik kontrolu hatasina takilir.

### Source: WhatsApp direct (reklam CTA)
- [ ] WA numarasi: `+44 7547 762090`
- [ ] wa.me link: `wa.me/447547762090?text=Hi+I%27m+interested+in+the+Roadshow`
- [ ] Inbound mesaj -> lead auto-create dogrula

### Consent
- [ ] Landing form'da opt-in checkbox zorunlu
- [ ] Opt-out footer her drip mesajinda: "Reply STOP to opt out"

## 4. Welcome Template Pack

**Feature:** `arch/features/welcome-template-pack.md` | **Detay:** `pilot-agent-config.md`

- [ ] 10 welcome varyanti (7 tarihli "welcome_with_date" + 5 tarihsiz "welcome_no_date")
- [ ] 12 FAQ intent x 3 cevap = 36 FAQ template
- [ ] TOPLAM 46 template yuklendi + approved
- [ ] Intent detector EN training data (5-10 paraphrase/intent) yuklendi

## 5. Field Mapping (INMA 10-field)

**Feature:** `arch/features/tenant-field-mapping.md` | **Detay:** `pilot-field-mapping.md`

5 field aktif, 5 reserve.

## 6. Video Consultation

**Feature:** `arch/features/video-consultation-provider.md`

- [ ] `video_provider = googlemeet_mock` (ilk hafta)
- [ ] Musterinin Workspace hesabi al: `ops@dentadavista.com` (tahmin)
- [ ] OAuth consent flow tamamla -> `video_provider = googlemeet`
- [ ] Dentist Dr. Ozge attendee email
- [ ] Coordinator attendee email

## 7. Event Follow-Up Sequence

**Feature:** `arch/features/event-followup-sequence.md`

```json
{
  "followup_sequence_config": {
    "stages": [
      { "delay_days": 3,  "template_slug": "post_event_day3_en",  "template_group": "followup_soft" },
      { "delay_days": 7,  "template_slug": "post_event_day7_en",  "template_group": "followup_nudge" },
      { "delay_days": 14, "template_slug": "post_event_day14_en", "template_group": "followup_final" }
    ],
    "triggers": ["welcome_chain_no_reply", "offer_declined", "offer_timeout", "offer_on_hold_7d"],
    "ab_control_percent": 50
  }
}
```

- [ ] 3 template upload + Meta HSM approval (utility category)
- [ ] A/B control group %50
- [ ] Analytics metric key kayit

## 8. Flow Builder (nurture flow)

- [ ] Welcome flow: `welcome_send -> wait 1.5d -> msj2_reminder -> wait 1d -> msj3_final -> warm_pool`
- [ ] City detection node: `dublin | cork | unclear`
- [ ] Unclear branch: 2 retry sonra human handoff
- [ ] Reply interrupt handler (wait iptal, intent route)
- [ ] Timezone window: 08:00-21:00 local time
- [ ] Flow validation gate passed

## 9. Offer & Appointment State Machine

- [ ] `offer_status` enum values: `none | preparing | sent | accepted | declined | on_hold`
- [ ] Slot booking: 30dk/slot, capacity=1, concurrent lock
- [ ] Slot picker UI: WA interactive list message
- [ ] 24h timer: Hangfire scheduled job per offer
- [ ] Follow-up template `offer_followup_24h_en` yuklendi

## 10. UAT Test Senaryolari (20)

Detay: `pilot-golive.md`

## 11. Musteri Egitimi

- [ ] 15dk screencast: coordinator dashboard
- [ ] 10dk screencast: AI agent supervise
- [ ] FAQ cheatsheet: escalation
- [ ] 30dk kickoff call Q&A

## 12. Monitoring & Alerting

- [ ] WAA webhook error alert
- [ ] Flow stuck lead alert (1 hafta)
- [ ] LLM cost gunluk limit
- [ ] Intent confidence anomaly detection

## 13. Asamali Go-Live

- [ ] Stage 1 (2 gun): Test numaralari only
- [ ] Stage 2 (3 gun): Ilk 20 gercek lead manuel inceleme
- [ ] Stage 3: Full trafik

## 14. Success Metrics (ilk 30 gun)

- Welcome -> reply rate: hedef >40%
- Reply -> slot booked: hedef >50%
- Slot -> offer accept: hedef >60%
- Offer -> Meet attended: hedef >80%
- Warm pool -> recovery (day 3/7/14): hedef >10%
- Agent auto-resolve: hedef >75%

## Musteri Input Bekliyor

- [ ] WhatsApp Business numarasi / Meta Business Manager access
- [ ] Landing page URL + form field listesi
- [ ] Logo + kurumsal renkler
- [ ] Gunes kisi mi ortak klinik numarasi mi (persona decision)
- [ ] Google Workspace hesabi (Meet)
- [ ] Fiyat listesi (PDF veya link)
- [ ] Instagram / FB URL'leri (sosyal proof)

## Kaynak Dosyalar

- `../Flowchart.pdf` — musteri akis semasi
- `../ROADSHOW AI AGENT KARSILAMA MESAJI.docx` — 10 welcome + 12 FAQ icerigi
