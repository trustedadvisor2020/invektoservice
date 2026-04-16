# Dent Adavista — Field Mapping

> **Generic feature:** `arch/features/tenant-field-mapping.md`
> INMA'nin 10 custom field'ina Dent-spesifik semantic overlay.

## Aktif Mapping (5 / 10)

| Semantic Name | INMA Source | Type | Enum / Format | Required | Kullanim |
|---------------|-------------|------|---------------|----------|----------|
| `roadshow_city` | `custom_1` | enum | `dublin \| cork` | yes | Flow condition node, slot resolution |
| `appointment_slot` | `custom_2` | date | ISO 8601 | no | Slot booking state, reminders |
| `offer_status` | `custom_3` | enum | `none \| preparing \| sent \| accepted \| declined \| on_hold` | no (default `none`) | Offer state machine |
| `deposit_status` | `custom_4` | enum | `not_requested \| requested \| paid` | no | Flight booking trigger |
| `flight_booked` | `custom_5` | bool | — | no | Post-offer handoff to travel coordinator |

## Reserve Fields (5 / 10 — ileride kullanim)

`custom_6` ... `custom_10` bos; platform evriminde ihtiyac dogarsa tahsis edilir.

Potansiyel adaylar:
- `xray_file_id` (Faz 6 bonus — X-ray upload attachment)
- `meeting_type` (in_person | online)
- `consent_marketing_v2` (ayri legal surum)
- `referral_source_detail` (hangi reklam/kanal)
- `lead_segment` (A/B group persistent)

## Validation Rules

- `roadshow_city` enum disi deger: 400 INV-BE-098
- `offer_status` state machine — transition validation Automation orchestrator'da
- `appointment_slot` past date: 400
- `deposit_status = paid` ise `flight_booked = true` entegrasyonu Integrations'ta (v2)

## INMA Sync

- Webhook event: `custom_field_updated` -> INSE `leads.custom_N` update
- Polling fallback: 5dk interval, since-last-sync query
- Write direction: INSE -> INMA (mesaj gonderim/offer state degisimlerinde)

## Pipeline Status vs Field Mapping

**Onemli ayrim:** Lead pipeline_status (new/contacted/qualified/offer_sent/closed_won/closed_lost) INSE-native `leads.pipeline_status` kolonunda tutulur, custom field DEGILDIR. Zoho Blueprint sync bu kolondan beslenir. Field mapping 5 alani `pipeline_status`'tan bagimsiz.

Karar: pipeline_status = CRM-standart lifecycle; field mapping = tenant domain vocabulary.
