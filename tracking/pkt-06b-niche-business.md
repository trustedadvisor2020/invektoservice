# PKT-6B: Niche Business Logic

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 2, FORCE PASS

## GR Listesi

- **GR-3.7 Outbound E-ticaret Senaryolari:** 4 trigger (order_delivered, return_follow_up, b2b_alert, review_prep)
- **GR-3.8 Iade Cevirme v1:** ReturnDeflectionService, intent algilama, neden siniflandirma
- **GR-3.11 Klinik Outbound v1:** 2 trigger (checkup_reminder, birthday_message)
- **GR-3.13 Lead Management v2:** LeadRepository CRUD + scoring + pipeline + follow-up
- **GR-3.3 Agent Assist E-ticaret:** OrderCardService, EscalationNoteService
- **GR-3.16 Negatif Yorum Kurtarma:** Review alert webhook, auto-message flow, recovery tracking
- **GR-3.17 Iade Cevirme v2:** Kupon + stok kontrol + conversion tracking

## GR Detail

### GR-3.7: Outbound E-ticaret Senaryoları
- 3.7.1 Sipariş teslim → "Memnun musunuz?" trigger
- 3.7.2 İade talebi follow-up (T+24h)
- 3.7.3 B2B lead → sales alert
- 3.7.4 Yorum → otomatik mesaj prep
- 3.7.5 Tenant-bazlı trigger config

### GR-3.8: İade Çevirme v1
- 3.8.1 "İade etmek istiyorum" intent algıla
- 3.8.2 Neden sor (kalite/beden/renk/hasarlı/fikrini değiştirdi)
- 3.8.3 Nedene göre aksiyon: değişim / kupon / iade başlat
- 3.8.4 Basit conversion tracking
- DB: return_deflections

### GR-3.11: Klinik Outbound v1
- 3.11.1 Randevu hatırlatma (Outbound Engine'e taşı)
- 3.11.2 Kontrol randevusu (T+30 gün)
- 3.11.3 Doğum günü mesajı
- 3.11.4 Opt-out yönetimi

### GR-3.13: Lead Management v2
- 3.13.1 Lead source tracking (Instagram, Google, referans, organik)
- 3.13.2 Lead scoring (ilgi + bütçe + zaman)
- 3.13.3 Pipeline view (yeni → iletişim → konsültasyon → randevu → hasta)
- 3.13.4 Follow-up otomasyonu (T+24h, T+72h, T+7gün)
- 3.13.5 "Sıcak lead" alert
- 3.13.6 Funnel dashboard
- DB: leads, lead_activities, service_catalog

### GR-3.3: Agent Assist E-ticaret
- 3.3.1 Sipariş kartı (son sipariş — Trendyol/HB)
- 3.3.2 Escalation notu (AI özet)
- 3.3.3 E-ticaret intent'lerine özel cevap kalitesi
- DB: suggested_replies

### GR-3.16: Negatif Yorum Kurtarma
- 3.16.1 Trendyol Review API (1-2 yıldız tespiti)
- 3.16.2 Otomatik tetikleme: yorum → AI mesaj
- 3.16.3 Mesaj akışı: özür → çözüm → yorum güncelleme ricası (T+48h retry)
- 3.16.4 Recovery tracking
- DB: review_alerts

### GR-3.17: İade Çevirme v2
- 3.17.1 Otomatik kupon oluşturma
- 3.17.2 Değişim stok kontrolü (Integrations)
- 3.17.3 İade çevirme başarı oranı
- 3.17.4 Kurtarılan gelir dashboard
- 3.17.5 Follow-up (T+24h)

## Deliverables

- 6 outbound trigger (e-ticaret + klinik)
- ReturnDeflectionService (v1+v2)
- LeadRepository (hot leads, funnel, activity log)
- Review alert + recovery tracking
- 21 dosya. 5-chunk review, 3 fix round
- DB: leads, lead_activities, return_deflections, review_alerts

## Plan

`arch/plans/20260217-pkt6b1-niche-business.json`
