# PKT-8: Face AI

> **Durum:** PLANNED | **Phase:** 3D

## Ozet

MediaPipe + Claude Vision ile yüz analizi. Estetik klinikler için tedavi eşleştirme, before/after takip. Çok dilli destek.

**Yeni Servis:** `Invekto.FaceAnalysis` (port 7110)
**Bagimlilik:** PKT-6A (Intent), PKT-6C (Marketing multilingual)

## GR Listesi

- **GR-3D.1** MediaPipe + Claude Vision: yüz landmark tespiti + estetik analiz
- **GR-3D.2** Tedavi Eşleştirme: yüz analiz sonuçları → tedavi önerisi (Knowledge ile)
- **GR-3D.3** Multi-lang: analiz sonuçlarını çok dilde sunma (TR/EN/AR)
- **GR-3D.4** WA/IG Entegrasyonu: fotoğraf gönder → analiz → öneri döngüsü
- **GR-3D.5** Analytics + Ethics: kullanım metrikleri + etik guardrail'ler

## Notlar

- 5 GR, ~20 alt madde
- Estetik klinik upsell aracı: "Fotoğrafınızı çekin, size uygun tedavileri görelim"
- KVKK hassas: yüz verisi özel nitelikli kişisel veri — onam zorunlu
