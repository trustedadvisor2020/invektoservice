# PKT-6C1: Health Automation

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 7, FORCE PASS

## GR Listesi

- **GR-3.20 Tedavi Sonrasi Takip:** T+24h, T+168h, T+720h
- **GR-3.41 Tedavi Plani Onay Akisi:** T+24h, T+72h, T+168h
- **GR-3.43 Tedavi Oncesi Hazirlik:** T-72h, T-24h, T-3h

## GR Detail

### GR-3.20: Tedavi Sonrası Takip Otomasyonu
- 3.20.1 T+1 gün "Nasıl hissediyorsunuz?"
- 3.20.2 T+7 gün kontrol soruları (ağrı, şişlik)
- 3.20.3 T+30 gün "Kontrol randevusu alalım mı?"
- 3.20.4 Şikayet → doktora alert (acil/normal)
- 3.20.5 Takip compliance tracking
- DB: treatment_followups

### GR-3.41: Tedavi Planı Onay Akışı
- 3.41.1 Tedavi planı gönderildi → follow-up zinciri
- 3.41.2 T+1 gün: "Planınızı incelediniz mi?"
- 3.41.3 T+3 gün: "Sorularınız varsa yardımcı olabiliriz"
- 3.41.4 T+7 gün: son hatırlatma + özel teklif
- 3.41.5 Onay gelmezse → supervisor alert

### GR-3.43: Tedavi Öncesi Hazırlık Talimatları
- 3.43.1 Tedavi tipine göre talimat template (ameliyat, implant, estetik)
- 3.43.2 Outbound trigger: T-3gün, T-1gün, T-sabah zinciri
- 3.43.3 Hazırlık onay tracking

## Deliverables

- TreatmentLifecycleService (IHostedService, 3 lifecycle tipi)
- LifecycleStepDefinitions: Static config (pozitif/negatif offset)
- Outbound trigger entegrasyonu
- Escalation: complaint -> doctor alert
- 5 API endpoint + Backend proxy
- 9 dosya +1450
- DB: treatment_followups, treatment_followup_steps

## Codex Notlari

- iter 4: Real bug - HandleLastStepAsync escalation sent regardless of response
- Chunk 2 persistent false positive: cross-tenant scheduler (8 iter)
- FORCE PASS: all CQ1-CQ8 PASS

## Plan

`arch/plans/20260217-pkt6c1-health-automation.json`
