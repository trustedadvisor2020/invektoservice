# FEAT-META-ADS-INSIGHTS — Tracking

> **Slug:** `20260429-feat-meta-ads-insights` (planlanan) | **Risk:** LOW (read-only)
> **Spec:** [arch/features/meta-ads-insights.md](../arch/features/meta-ads-insights.md)
> **Status:** **DRAFT** — Q onayi + chunk breakdown bekliyor

## Özet

Meta Ads performans verisini Invekto Dashboard'a tasiyan read-only widget. Marketing API `ads_read` permission. Async report API + 1 saat cache + lead matching (`intake_metadata.campaign_id` JOIN). Musteri "23 leads from FB Ads, 5 converted, $342 spend" raporunu Invekto'da gorur — Ads Manager'a girmeden.

## Strateji

- **Read-only:** Hicbir write Meta'ya. App Review hafif (`ads_read`).
- **Async report API:** Buyuk hesaplar icin sync timeout riski; Meta'nin run_id + poll pattern'i.
- **Cache 1 saat:** Reklam metrikleri saatlik anlamli degisir; rate limit + UI hizi dengesi.
- **Lead matching:** FEAT-META-FULL-INTAKE'in capture ettigi `intake_metadata.campaign_id` ile JOIN → conversion oranı.

## ROI Tahmini

- Musteri **Ads Manager alternatifi** olarak Invekto'yu gormeye baslar
- Tek pencerede CRM + Ads → "Invekto'siz olmaz" momentumu
- Churn azaltici (subjective; metric: support ticketlarinda "raporum eksik" sikayetinde dusus)

## Bagimliliklar

| # | Bagimlilik | Status |
|---|-----------|--------|
| 1 | FEAT-META-CAPI Pixel/Token provision (ayni Business Manager + ayni System User Token) | PENDING — paralel paket |
| 2 | App Review `ads_read` (CAPI ile bundle submission tek video) | PENDING — Codex review oncesi |
| 3 | FEAT-META-FULL-INTAKE `campaign_id` capture | DONE 2026-04-29 |

## Chunk Breakdown

| Chunk | Scope | Risk | Tahmin |
|-------|-------|------|--------|
| A | Shared DTO (AdsInsightsReport + CampaignInsightRow) + Marketing IMetaAdsInsightsClient + MockClient | LOW | 0.5 session |
| B | Marketing ProdMetaAdsInsightsClient (async report + poll + cache) + Hangfire daily refresh | MED | 1-1.5 session |
| C | Migration + Backend tenant-settings endpoint + Backend reports proxy + lead matching SQL | MED | 1 session |
| D | Dashboard SPA `/reports/ads-insights` page + widget + date picker + campaign table | LOW | 1 session |
| E | App Review submission + smoke test + first tenant rollout | LOW | 0.5 session |

**Toplam:** 3-4 session (CAPI'den daha kucuk)

## Acceptance Criteria

Spec'in §2 bolumune bakiniz: AC-1..AC-10.

## Açik Sorular (Q karari)

| # | Soru | Etki |
|---|------|------|
| 1 | Default date range 7 gun mu, 30 gun mu? | UI varsayilan |
| 2 | Lead matching strict `campaign_id` mi, fuzzy ad name match opsiyonu da mi? | Implementation karmasiklik |
| 3 | Daily refresh hangi saatte (TR 03:00 mi, UTC 03:00 mi)? | Tenant timezone politika |
| 4 | "Yenile" butonu rate limit guard threshold (N saatte bir manuel refresh OK)? | UX |
| 5 | Multi-account aggregation gerekiyor mu Dent disinda baska tenant icin? | Scope |

## Pre-Flight Checks

- [ ] App Review CAPI ile bundle (`ads_management` + `ads_read` tek submission, tek video)
- [ ] Error code namespace audit (`INV-META-ADS-*` 001..005)
- [ ] Migration numarasi (CAPI ile sirali — 035 + 036 veya bundle 035)
- [ ] Marketing.csproj G7 pattern dogrulamasi
- [ ] Lead matching `intake_metadata.campaign_id` JSONB index gerekiyor mu (perf)

## Sonraki Adim

Q karari: CAPI ile bundle olarak mi yoksa ayri paket mi? Bundle avantaj: tek App Review, tek pixel/token provision. Dezavantaj: paket buyur (8-10 chunk total). Onerim **bundle** — CAPI Chunk E pilot smoke'unda Ads Insights da test edilir.
