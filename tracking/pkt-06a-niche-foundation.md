# PKT-6A: Niche Foundation

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 1, PASS

## GR Listesi

- **GR-3.1 Intent Genisletme + Oto. Etiketleme:** DB-driven intent, Knowledge bridge, ApplyTag
- **GR-3.2 B2B / VIP Lead Tespiti:** Siparis hacmi, mesaj frekansi, anahtar kelime analizi
- **GR-3.5 Onboarding Otomasyonu:** Tenant sektor bazli seed data
- **GR-3.9 Dis Intent + Fiyat Pipeline:** Dis sektoru intent tanimlari
- **GR-3.10 Dis Onboarding Otomasyonu:** Dis sektoru seed data
- **GR-3.12 Estetik Intent + Lead Pipeline:** Estetik sektoru intent tanimlari
- **GR-3.23 Voice Message AI:** Sesli mesaj AI altyapisi (evrensel)

## GR Detail

### GR-3.1: Intent Genişletme + Otomatik Etiketleme
- 3.1.0 AI bazlı konu tespiti → etiket ata
- 3.1.1 Feedback analizi: en çok ne soruluyor?
- 3.1.2 Yeni intent'ler: iade, değişim, fatura, iptal, stok, B2B, yorum sinyali
- 3.1.3 Confidence threshold ayarı
- 3.1.4 Multi-turn conversation

### GR-3.2: B2B / VIP Lead Tespiti
- 3.2.1 B2B sinyal algılama ("toptan", "100 adet", "kurumsal fatura")
- 3.2.2 VIP flag + otomatik etiketleme
- 3.2.3 Sales team alert (email/webhook)
- 3.2.4 Müşteri geçmişi tarama
- 3.2.5 Özel teklif akışı başlatma
- DB: vip_flags

### GR-3.5: Onboarding Otomasyonu
- 3.5.1 Self-service API key girişi
- 3.5.2 Basit tenant setup wizard
- 3.5.3 Default intent ayarları (başlangıç seti)
- 3.5.4 Tenant veri izolasyonu güçlendirme

### GR-3.9: Diş Intent + Fiyat Pipeline
- 3.9.1 Hasta feedback analizi
- 3.9.2 Yeni intent'ler: randevu değişiklik, tedavi bilgisi, acil, sigorta, adres, çalışma saatleri
- 3.9.3 Confidence threshold

### GR-3.10: Diş Onboarding Otomasyonu
- 3.10.1 Template özelleştirme (klinik adı, doktor adı)
- 3.10.2 Tenant veri izolasyonu

### GR-3.12: Estetik Intent + Lead Pipeline
- 3.12.1 Yeni intent'ler: before/after, işlem detayı, kontrendikasyon, iyileşme, paket, yabancı hasta, referans, taksit
- 3.12.2 Confidence threshold

### GR-3.23: Voice Message AI
- 3.23.1 Whisper API entegrasyonu (OGG/MP3 → WAV, çoklu dil)
- 3.23.2 AgentAI pipeline'ına transkript aktar
- 3.23.3 Automation trigger: sesli mesaj → flow tetikle
- 3.23.4 Agent UI: transkript + intent gösterimi
- 3.23.5 Sentiment: ses tonundan duygu (kızgın/memnun/acil)
- 3.23.6 Düşük güven → "Yazılı gönderir misiniz?"
- DB: voice_transcripts

## Deliverables

- KnowledgeIntentClient, AiIntentHandler (DB-driven)
- VipDetectionService, OnboardingService
- 22 sektor intent seed (eticaret/dis/estetik)
- 16 dosya +918/-28

## Plan

`arch/plans/20260217-pkt6a-niche-foundation.json`
