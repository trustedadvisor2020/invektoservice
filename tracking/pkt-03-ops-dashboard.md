# PKT-3: Ops Dashboard

> **Durum:** DONE | **Tarih:** 2026-02-16 | **Codex:** iter 1, FORCE PASS
> **Commit:** 63543d4

## GR Listesi

- **GR-2.5 Automation Analytics:** Deflection/handoff rate, trend grafikleri, intent performance, FRT, conversation metadata
- **WA-4 BI Dashboard:** Agent performans, conversion, trend raporlari

## GR Detail

### GR-2.5: Otomasyon Dashboard + Log İyileştirme
- 2.5.1 Deflection rate: otomatik cevaplanan / toplam
- 2.5.2 Handoff rate: temsilciye devredilen
- 2.5.3 Günlük/haftalık trend grafikleri
- 2.5.4 Akıllı Özet Kartları
- 2.5.5 Log entry summary field
- 2.5.6 Intent performance (hangi intent ne kadar çözüyor)
- 2.5.7 Top unanswered questions
- 2.5.8 Müşteri bazlı deflection rate
- 2.5.9 Basit FRT (First Response Time) — Phase 6 SLA hazırlığı
- 2.5.10 Conversation metadata log (Phase 6 Mining için)
- DB: daily_metrics, daily_intent_metrics

## Deliverables

- MetricsAggregationService (IHostedService, 5min timer)
- 4 automation endpoint + 4 WA endpoint
- React: MetricCards, DeflectionChart, IntentTable, WaTrendsChart, WaAgentTable
- AnalyticsPage: tenant dropdown, date range filters
- 17 dosya +2052/-1
- DB: daily_metrics, daily_intent_metrics

## Plan

`arch/plans/20260216-pkt3-ops-dashboard.json`
