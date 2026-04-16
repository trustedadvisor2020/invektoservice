# Dent Adavista — AI Agent "Gunes" Configuration

> **Generic feature:** `arch/features/welcome-template-pack.md`
> Bu dokuman **Dent'e ozel persona + icerik** barindirir. Feature spec'te gecmez (tenant-specific).

## Persona

| Alan | Deger |
|------|-------|
| Agent adi | **Gunes** |
| Tone | warm, professional, low-pressure, emoji-friendly |
| Signature | `Gunes - Dent Adavista Dental Clinic - Kusadasi` |
| Handoff threshold | intent confidence < 0.6 → coordinator |
| Primary locale | en-US |
| Fallback locale | tr-TR |

## Template Paketi (46 adet)

### Welcome (10 varyant, 2 grup)

**Grup A — `welcome_with_date` (7 varyant):** ilk mesaj, Dublin/Cork tarih iceriyor
**Grup B — `welcome_no_date` (5 varyant):** registration sonrasi follow-up

Variable slotlari: `{{lead.name}}`, `{{campaign.cities}}`, `{{campaign.dates}}`
Deterministik secim: `hash(lead.phone) % N` (feature spec AC-2)

Kaynak: `../ROADSHOW AI AGENT KARSILAMA MESAJI.docx` (10 varyant)

### FAQ Intent Map (12 intent x 3 cevap = 36 template)

| Intent | Keyword trigger (EN) | Template group |
|--------|---------------------|----------------|
| `is_it_free` | free, cost, pay, charge | `faq_pricing` |
| `location_where` | where, address, hotel | `faq_location` |
| `what_happens` | what happens, agenda | `faq_agenda` |
| `any_treatment` | treatment there, procedure | `faq_treatment_scope` |
| `payment_after` | obligation, commit, after | `faq_commitment` |
| `bring_xray` | xray, x-ray, scan | `faq_xray` |
| `bring_companion` | friend, family, bring | `faq_companion` |
| `duration` | how long, time, minutes | `faq_duration` |
| `why_ireland` | why ireland, why here | `faq_why_here` |
| `price_quote` | price, how much, cost of | `faq_price_quote` + fallback link |
| `safety_concern` | safe, trust, legit | `faq_safety` + social links inject |
| `hotel_transfers` | hotel, transfer, flight | `faq_logistics` |

Rotation: per-lead round-robin (feature spec AC-3, `leads.faq_rotation_state` JSONB).

### Ozel Intent Davranislari

- `price_quote` → 3. tetikleme sonrasi fiyat listesi linki + "Dentist call-back?" teklif
- `safety_concern` → otomatik inject: website + Instagram (son 3 before/after post) + FB + telefon
- `location_where` → sehir secilmeden once "Dublin or Cork?" sor; secildikten sonra spesifik hotel
- Bilinmeyen intent → "Let me connect you with our coordinator" → human handoff

## Dil Detection

- Inbound TR mesaj → TR template fallback (var ise; yoksa EN + "I'll respond in English, hope that's okay")
- Diger diller → EN fallback
- Translation layer (mevcut Gemma/Claude) — outgoing EN template TR'ye otomatik cevrilebilir (tenant flag)

## Intent Training Data

- docx kaynaktan cikarilan soru ornekleri + 5-10 paraphrase per intent
- Mock detector -> LLM detector (prod)
- Sim test: 20 senaryo, target accuracy >=85%

## Handoff

- Intent confidence < 0.6 -> "Let me connect you with our coordinator"
- Keyword: "agent", "human", "staff" -> direkt handoff
- Coordinator Dashboard uyarisi (sidebar notification)

## Meta HSM Template Onayi

- Welcome 2 template ornegi submit (date-variant + no-date-variant)
- FAQ template'ler 24h window icinde freeform (Meta gerektirmez)
- `post_event_day3_en`, `post_event_day7_en`, `post_event_day14_en` (3 template) — utility category
- `offer_sent_en`, `offer_followup_24h_en`, `appointment_confirmed_en` — marketing category
