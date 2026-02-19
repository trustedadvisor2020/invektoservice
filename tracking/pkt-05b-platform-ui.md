# PKT-5B: Platform UI+Adv

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 4, FORCE PASS
> **Commit:** 93d2392

## GR Listesi

- **GR-3.14 Ads Attribution:** UTM/Meta click webhook, lead_attributions + ad_costs CRUD, CPL queries
- **GR-3.18 Dashboard Genisletme:** CampaignPanel, AttributionPanel, PlaceholderPanel, AnalyticsPage
- **GR-3.19 Randevu Advanced:** Waitlist CRUD, no-show stats, service pricing, doctor slot, ICalendarSyncService

## GR Detail

### GR-3.14: Ads Attribution (Basit + Full)
- 3.14.1 UTM parameter capture (WhatsApp link'e UTM ekle)
- 3.14.2 Lead source → "Bu lead hangi kampanyadan geldi?"
- 3.14.3 Kampanya bazlı lead sayısı dashboard
- 3.14.4 Cost-per-lead hesaplama (manuel maliyet girişi)
- 3.14.5 Meta click id capture (campaign/adset/ad)
- 3.14.6 Pipeline auto-tagging (label + segment + UTM mapping)
- 3.14.7 Full attribution dashboard (kampanya → lead → conversion)
- DB: lead_attributions, ad_costs

### GR-3.18: Dashboard Genişletme
- 3.18.1 Outbound campaign dashboard (gönderim/okunma/dönüşüm)
- 3.18.2 İade çevirme oranı + kurtarılan gelir
- 3.18.3 Yorum kurtarma oranı + etki
- 3.18.4 Niche bazlı dashboard panelleri (e-ticaret / diş / estetik)

### GR-3.19: Randevu Motoru v2 (Advanced)
- 3.19.1 Google Calendar sync (2-way)
- 3.19.2 Doktor bazlı slot yönetimi (specialist vs genel)
- 3.19.3 Bekleme listesi (iptal → sıradaki hastaya sor)
- 3.19.4 No-show prediction (2+ kez → extra hatırlatma)
- 3.19.5 Fiyat aralığı editor (tedavi → min/max TL)
- DB: waitlist, service_pricing

## Deliverables

- Attribution engine (7 endpoint + 3 ops analytics)
- Dashboard: CampaignPanel, AttributionPanel
- Waitlist + service pricing + doctor slot filtering
- 22 dosya +2863/-55
- DB: lead_attributions, ad_costs, waitlist, service_pricing

## Plan

`arch/plans/20260217-pkt5b-platform-ui-adv.json`
