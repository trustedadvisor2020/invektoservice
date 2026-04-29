# SPEC: Meta Ads Insights — Read-Only Performance Dashboard

> **Spec ID:** FEAT-META-ADS-INSIGHTS | **Paket:** TBD | **Risk:** LOW (read-only, no PII writes)
> **Yazar:** Q + Claude planlama 2026-04-29 | **Son Guncelleme:** 2026-04-29 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

**Sorun:** Reklam veren Invekto musterileri ($10k/ay toplam spend) reklam performansini **Ads Manager'da ayri ekranda** takip ediyor. Invekto Dashboard'da CRM verisi var, FB Ads metrik yok. Lead-to-revenue donusum oranı + Ads spend yan-yana yok → musteri yatirim getirisini hesaplayamiyor, "Invekto'da raporum eksik" hissi.

**Cozum:** Invekto Dashboard'a **FB Ads performans widget'i**: spend, impressions, clicks, CTR, CPM, lead count (Lead Ads ile match) per campaign. Read-only, Meta Marketing API `ads_read` permission (App Review hafif).

**Beklenen ROI:** Musteri Invekto'yu **Ads Manager alternatifi** gormeye baslar → tek pencerede iki dunya (CRM + Ads). Churn azaltici, "Invekto'siz olmaz" momentumu.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `tenant_settings.meta_ads_config JSONB` (ad_account_id + access_token_encrypted + enabled) | DB row + UNIQUE per-tenant |
| AC-2 | `IMetaAdsInsightsClient.GetCampaignInsightsAsync(tenantId, dateRange)` async report API ile (Meta `POST /act_{id}/insights` → run_id → poll) | Mock + integration test |
| AC-3 | Cache layer: 1 saat TTL per tenant per date_range; cache miss durumunda Meta API hit | Cache hit/miss log |
| AC-4 | Dashboard widget `/reports/ads-insights`: 7-gun varsayilan, custom date picker, campaign breakdown table | Manuel UI smoke |
| AC-5 | Metrikler: spend, impressions, clicks, ctr, cpm, cpc, frequency, reach, **lead_count** (Lead Ads campaigns: `actions[action_type=lead]`) | Mock data render verify |
| AC-6 | Lead-to-CRM matching: Meta `campaign_id` ile Invekto leads'teki `intake_metadata.campaign_id` join → "23 leads from FB Ads, 5 converted" gosterimi | Cross-table query |
| AC-7 | Hangfire daily refresh job (gece 03:00) — onceki gun spend snapshot, aylik aggregate hizlandirma | Hangfire dashboard |
| AC-8 | Read-only — config disinda hicbir write Meta'ya. Audit log: every API call + tenant_id + endpoint + duration | Audit trace |
| AC-9 | App Review submission: `ads_read` permission, Meta'ya use-case "agency reporting tool" video | App Review APPROVED status |
| AC-10 | Rate limit handling: Meta async insights queue (BUC), 429 backoff, kullaniciya "Verileriniz yenileniyor..." UX | Rate limit smoke |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Async report API (sync degil) | Meta `/insights` sync endpoint kucuk hesaplarda calisir, $10k+ spend hesaplarinda timeout. Async run_id pattern industry-standard. | EXPECTED: CQ12 reliability |
| 1 saat cache TTL | Reklam metrikleri saatlik anlamli degisir; daha sik cagri = rate limit risk; daha az = stale UI | — |
| Dashboard → Backend proxy → Marketing servisi (microservice isolation) | Microservice izolasyonu zorunlu (CLAUDE.md kural): Dashboard yalnizca Backend ile konusur; Backend `/api/v1/reports/ads-insights` proxy edip Marketing internal `/api/internal/meta-ads-insights/{tenantId}` (X-Internal-Service-Token) cagirir. EFS+MCC+CAPI pattern ayni iki-hop yapidir. | CQ9: ok |
| Per-tenant token (CAPI ile ayni; ad account + pixel ayni Business Manager) | Single token simplification | — |
| Date range max 90 gun (Meta limiti default) | API constraint, UI'da disable | — |
| Lead matching `intake_metadata.campaign_id` | FEAT-META-FULL-INTAKE Meta Leadgen webhook payload'da `campaign_id` zaten yakalandi → query JOIN | EXPECTED: CQ7 reuse |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Tenant Settings API | `arch/contracts/tenant-settings.json` (additive `meta_ads_config`) |
| DB Schema | `arch/db/marketing.sql` (`meta_ads_insights_cache` table + index) |
| Shared DTO | `Invekto.Shared/Contracts/MetaAdsInsights/AdsInsightsReport.cs` (yeni) + `CampaignInsightRow.cs` |
| Marketing Internal API | `GET /api/internal/meta-ads-insights/{tenantId}?from=&to=` (X-Internal-Service-Token) |
| Backend Proxy | `GET /api/v1/reports/ads-insights?from=&to=` (jwt) + `GET/PUT /api/v1/tenant-settings/meta-ads-config` |
| Error Codes | INV-META-ADS-001..005 (config missing, token expired, async report timeout, rate limited, ad account access denied) — pre-flight |

## 5. Scope Boundaries

### In Scope
- `tenant_settings.meta_ads_config JSONB` migration
- Marketing servisinde `IMetaAdsInsightsClient` + ProdMetaAdsInsightsClient (async report + poll + cache)
- 1 saat TTL cache table (`meta_ads_insights_cache`)
- Daily Hangfire refresh (`meta-ads-insights-refresh` queue)
- Dashboard widget `/reports/ads-insights` (campaign table + date picker + lead matching)
- Backend proxy + tenant-settings editor

### Out of Scope (Explicit)
- Campaign create/update/delete (Marketing API write — backlog FEAT-META-MARKETING-API)
- Ad creative download/preview (separate paket)
- Multi-account aggregation per tenant (tek ad account MVP)
- Real-time websocket updates (1 saat cache yeterli)
- Custom metric formulas (Meta'nin hazir metricleri — custom CPA hesabi v2)
- Funnel analysis Meta tarafindan (Pixel/CAPI events + Meta Pixel Helper, ayri concern)

### Degismeyen Alanlar (Pre-existing)
- FEAT-META-FULL-INTAKE Lead Ads intake (campaign_id capture zaten var)
- FEAT-META-CAPI server-side events (paralel, baginsiz)
- INMA + chatoperation (Ads Insights INMA bypass eder)

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Backend (5000) | JWT proxy `/api/v1/reports/ads-insights` + `/api/v1/tenant-settings/meta-ads-config` | Yeni endpoint |
| Marketing (7112) | Async report runner + cache + lead matching JOIN + Hangfire daily refresh | Yeni modul (FEAT-META-CAPI ile ayni servis) |
| Dashboard SPA | `/reports/ads-insights` page + sparkline widget reusable | Yeni page |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| App Review red (ads_read) | LOW | "agency reporting tool" use-case net + Verified Tech Provider track record |
| Token paylasimi CAPI ile (ayni tenant farkli scope) | LOW | Tek token cift scope (`ads_read` + `ads_management` CAPI icin); BM System User token uretirken cift permission iste |
| Async report timeout (10dk+) | MED | Polling max 10 retry (10dk), fail durumunda UI "Tekrar dene" + 1 saat sonra Hangfire refresh |
| Lead matching false positive | LOW | `campaign_id` strict equality, fuzzy match yok |
| Cache invalidation gecikmesi | LOW | 1 saat TTL kabul edilebilir; manuel "Yenile" butonu (rate-limit guard ile) |

## 8. Pre-Flight Checks

- [ ] **Error code namespace:** `INV-META-ADS-*` vs `INV-MARK-ADS-*` — son INV-META-006 (Leadgen Graph fail), 007+'dan devam mi yoksa yeni paket 001-005 mi (canonical decision Codex review oncesi)
- [ ] **Migration numarasi:** FEAT-META-CAPI ile sirali (035 + 036 veya bundle 035)
- [ ] **App Review reuse:** FEAT-META-CAPI `ads_management` + `ads_read` ayni submission'da gonderilebilir mi (tek video, hizli onay)
- [ ] **Pixel/Dataset cleanup:** CAPI Pixel ile Ads Insights Ad Account ayni Business Manager altinda olmali — config Q dogrulayacak
- [ ] **Marketing.csproj reference path:** Backend G7 SCHEDULER HOST EXCEPTION pattern + EFS/CAPI pattern hizalama

## 9. Stage Plan (Chunk Breakdown)

| Chunk | Scope | Risk |
|-------|-------|------|
| A | Shared DTO + Marketing IMetaAdsInsightsClient interface + MockClient | LOW |
| B | Marketing ProdMetaAdsInsightsClient (async report + poll + cache) + Hangfire daily refresh | MED |
| C | Migration + Backend tenant-settings endpoint + Backend reports proxy + lead matching SQL | MED |
| D | Dashboard SPA `/reports/ads-insights` page + widget + date picker + campaign table | LOW |
| E | App Review submission + smoke test + first tenant rollout | LOW (ops) |

**Toplam tahmin:** 3-4 session (CAPI'den daha kucuk, read-only)
