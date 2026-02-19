# PKT-5A: Platform Infra

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 2, FORCE PASS
> **Commit:** d1e28bc

## GR Listesi

- **GR-3.4 HB API:** Integrations Service - HepsiburadaClient, OrderSyncService
- **GR-3.6 Kargo Mock:** ShipmentTrackingService (HB, Trendyol, N11 provider)
- **GR-3.15 Outbound v2:** CampaignOrchestrator, CampaignSenderService
- **GR-3.26 Opt-in Framework:** ConsentManager, marketing consent check
- **GR-3.29 Compliance Delta:** ComplianceHelper, data deletion, batch audit trail

## GR Detail

### GR-3.4: Hepsiburada API Entegrasyonu
- 3.4.1 Integrations servis iskeleti (port 7106)
- 3.4.2 HB API entegrasyonu (Trendyol pattern kopyası)
- 3.4.3 Sipariş sync + tracking
- 3.4.4 Müşteri platform tespiti
- DB: integration_accounts, orders_cache

### GR-3.6: Kargo Entegrasyonu
- 3.6.1 Aras Kargo tracking API
- 3.6.2 Yurtiçi Kargo tracking API
- 3.6.3 Kargo durumu değişince proaktif mesaj

### GR-3.15: Outbound Engine v2
- 3.15.1 Campaign yönetimi (oluştur, hedef kitle, zamanlama)
- 3.15.2 AI-generated personalization (Knowledge ile)
- 3.15.3 Conversion tracking (mesaj → aksiyon)
- 3.15.4 A/B testing (2 şablon karşılaştırma)
- 3.15.5 Time-based trigger'lar (delay, recurring)
- 3.15.6 ROI dashboard
- DB: outbound_campaigns, outbound_conversions

### GR-3.26: Opt-in Toplama Framework
- 3.26.1 Opt-in kanalları: WA, web form, sipariş onay, randevu
- 3.26.2 Profilde wa_opt_in, opt_in_date, opt_in_source
- 3.26.3 STOP → otomatik unsubscribe
- 3.26.4 Kategori bazlı onam (utility vs marketing)
- 3.26.5 Compliance log
- DB: consent_records

### GR-3.29: Compliance Temel (KVKK/GDPR)
- 3.29.1 Explicit consent flow (GR-3.26 ile entegre)
- 3.29.2 Template audit trail
- 3.29.3 Veri silme hakkı iş akışı
- 3.29.4 Saklama süresi (sektör bazlı config)
- 3.29.5 Temel maskeleme (TC kimlik, telefon)

## Deliverables

- Yeni **Invekto.Integrations** servisi (port 7106)
- Outbound v2: campaign engine
- Opt-in + Compliance altyapisi
- 33 dosya +3445/-14
- Split review: Part1 137KB + Part2 209KB

## Plan

`arch/plans/20260217-pkt5a-platform-infra.json`
