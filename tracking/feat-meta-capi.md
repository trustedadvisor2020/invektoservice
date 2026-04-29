# FEAT-META-CAPI — Tracking

> **Slug:** `20260429-feat-meta-capi` (planlanan) | **Risk:** MEDIUM (PII + per-tenant token)
> **Spec:** [arch/features/meta-conversions-api.md](../arch/features/meta-conversions-api.md)
> **Status:** **DRAFT** — Q kararlari alindi 2026-04-29, chunk A baslangic icin Pixel/Token provision (Q manuel adim) + soru 2 teyit bekliyor

## Q Kararlari (2026-04-29)

| # | Soru | Q Cevabi | Etki |
|---|------|----------|------|
| 1 | Pilot tenant | **Dent Adavista** ilk rollout | Chunk E pilot smoke Dent ile. **NOT:** Kod tum sistem icin generic (multi-tenant `tenant_settings.meta_capi_config`); Dent sadece ilk aktif tenant. |
| 2 | Test pixel ayri mi, prod+test_event_code mi? | **(Q "bilmiyorum" — Claude oneri)** Prod pixel + `test_event_code` | Tek BM/Pixel maintenance, Meta'nin Test Events paneli zaten bu use-case icin var, real ad delivery etkilenmez. Q teyit ederse final. |
| 3 | Token expiry warning kanali | **Dashboard alert** | Hangfire daily check job → expiry < 7 gun ise tenant Dashboard'inda banner; `notifications` table veya `tenant_alerts` mekanizmasi (mevcut altyapi audit gerek) |
| 4 | Schedule event hook noktasi | **Ikisi de** — Appointments service hook **+** Lead status `appointment_booked` hook | Cift kanal: (a) Appointments service icinden direkt (gercek randevu), (b) Lead pipeline'da `appointment_booked` state'e gecince (CRM-level scheduling). Race-safe: `event_id` her iki yolda da deterministic SHA256(tenant+lead+appointment_id) → dedup window 7 gun |
| 5 | consent=false → CAPI gonderim | **Hard reject** | Marketing dispatcher'da gate: `consent_marketing != true` → CAPI dispatch ATLA + audit log "skipped_consent" + Dashboard'da counter (KVKK/GDPR uyumu). FEAT-META-FULL-INTAKE'in `consent_marketing` field'i otoritedir. |

## Özet

Meta Conversions API (CAPI) server-side event tracking. Invekto'da olusan donusumler (Lead, Schedule, Purchase, CompleteRegistration) hashed PII + FBC/FBP cookie + IP/UA ile Meta'ya server-side gonderilir. Browser Pixel ile event_id deduplication. EMQ optimizasyonu reklam ROAS'ini artirir.

## Strateji

- **INMA bypass:** Donusum Invekto'da olusur (deal close, appointment book), INMA inbox'ta degil. Direkt Marketing servisinden Meta Graph API.
- **Per-tenant token:** Verified Tech Provider System User token (60-gun once warning).
- **Hangfire queue:** `meta-capi-dispatch` (Marketing servisi). Idempotent, retry=2, dead-letter.
- **Browser+Server dedup:** event_id GUID Dashboard SPA'da emit edilir, server CAPI dispatch'te ayni id.

## ROI Tahmini ($10k/ay spend)

- %10-15 efficiency artisi → **$1-1.5k/ay tasarruf**
- Musteri Invekto raporunda "FB attributed sales" gorur → somut argument

## Bagimliliklar

| # | Bagimlilik | Status |
|---|-----------|--------|
| 1 | Q Pixel/Dataset provision (Business Manager → Events Manager) | PENDING — Q manuel adim |
| 2 | System User Token uretimi (`ads_management` + `business_management`) | PENDING — Q BM'den uretir |
| 3 | App Review (`ads_management_standard_access` HAFIF; CAPI use-case net) | PENDING — Pixel verify yeterli + light review |
| 4 | FEAT-META-FULL-INTAKE (consent_marketing capture) | DONE 2026-04-29 — gate Marketing dispatcher'da consent=true |

## Chunk Breakdown

| Chunk | Scope | Risk | Tahmin |
|-------|-------|------|--------|
| A | Shared DTO (MetaCapiEvent + UserData + CustomData + EventType enum) + Marketing IMetaCapiClient + MockClient | LOW | 1 session |
| B | Marketing ProdMetaCapiClient + Hangfire queue + dead-letter + retry | MED | 1-2 session |
| C | Migration + Backend tenant-settings endpoint + 3 hook point (Lead/Schedule/Purchase) + audit table | MED | 1 session |
| D | Dashboard SPA `/settings/meta-capi` editor + test event + Pixel event_id emit util | LOW | 1 session |
| E | Pilot smoke (test pixel) + production rollout (Dent Adavista oneri) | MED | 0.5 session |

**Toplam:** 4-6 session

## Acceptance Criteria

Spec'in §2 bolumune bakiniz: AC-1..AC-10.

## Açik Sorular (Q karari)

| # | Soru | Etki |
|---|------|------|
| 1 | Hangi tenant pilot olarak baslar — Dent Adavista mi yoksa baska? | Migration seed + smoke planı |
| 2 | Test pixel ayri olusturulsun mu yoksa prod pixel + `test_event_code` ile mi? | Dev/test guvenligi |
| 3 | Meta token expiry warning kanali (Q'ya nasil bildirilsin: log, email, Dashboard alert?) | Operasyonel |
| 4 | Event mapping `Schedule` Appointments service hook mi yoksa Lead status `appointment_booked` mı? | Hook noktasi sayisi |
| 5 | Consent=false durumda CAPI **hard reject** mi yoksa "anonymized event" (PII'siz) mi gonderilsin? | KVKK/GDPR + EMQ |

## Pre-Flight Checks (paket plan JSON oncesi)

- [ ] Error code namespace audit (`INV-META-007+` veya yeni `INV-MARK-CAPI-*` — Codex audit oncesi)
- [ ] Migration numarasi (FEAT-PIPELINE Migration N + paralel coordination)
- [ ] Marketing.csproj G7 SCHEDULER HOST EXCEPTION pattern (PrivateAssets="all" Backend tarafinda)
- [ ] Q Pixel/Dataset provision dogrulamasi
- [ ] `Q Pending Operational Tasks` listesine eklenecek: token gen + pixel create

## Notlar

- FEAT-META-FULL-INTAKE deploy bekliyor (FEAT-PIPELINE bundle ile). CAPI bu deploy'dan sonra basla.
- Browser Pixel kurulumu **musteri sitesi sorumlulugu** — Invekto sadece event_id paylasimi sozlesmesi sunar.
- KVKK/GDPR: consent_marketing zaten yakalandi → CAPI sadece consent=true gonderir.

## Sonraki Adim

Q karari: chunk A baslamadan once acik sorulari coz + pilot tenant netlestir + Pixel/Token provision (paralel hazirlik).
