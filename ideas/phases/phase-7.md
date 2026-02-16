# Phase 7 — Genişleme: Yeni Kanallar + Global + Mobil

> **Hafta:** 49+
> **MRR Hedefi:** 2M++ TL
> **Müşteri Hedefi:** 200++
> **Bağımlılık:** Phase 6 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v6 (2026-02-16):** 2 yeni GR eklendi: GR-7.6 QR Kod (M6),
> GR-7.7 Çevrimdışı Mod (M7). Toplam: 7 GR.

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-7.1 Mobil Uygulama (iOS + Android) | ⬜ Başlamadı | — | — |
| GR-7.2 Yeni Kanal Entegrasyonları | ⬜ Başlamadı | — | — |
| GR-7.3 Voice & Video | ⬜ Başlamadı | — | — |
| GR-7.4 Predictive Analytics | ⬜ Başlamadı | — | — |
| GR-7.5 Global Pazar Hazırlığı | ⬜ Başlamadı | — | — |
| GR-7.6 QR Kod ile Hızlı Erişim | ⬜ Başlamadı | — | v6: M6, fiziksel→dijital köprü |
| GR-7.7 Çevrimdışı Mod | ⬜ Başlamadı | — | v6: M7, offline mesaj kuyruğu |

---

## Özet

WhatsApp + 7 kanal zaten var. Bu phase: mobil erişim, yeni kanallar, global pazar ve tahminsel analitik.

**Satış dili:** "Her yerden, her kanaldan, her dilde — cebinizden yönetin"

---

## Gereksinimler

### GR-7.1: Mobil Uygulama (iOS + Android)

> **Servis:** Yeni uygulama (React Native veya Flutter — Q kararı)
> **Sektör:** Tümü

**Temel özellikler:**
- [ ] **7.1.1** Teknoloji seçimi (React Native vs Flutter)
- [ ] **7.1.2** Konuşma listesi + mesaj okuma/yazma
- [ ] **7.1.3** Push notification
  - Yeni mesaj
  - SLA breach
  - VIP lead
- [ ] **7.1.4** Agent Assist (AI cevap önerisi) — mobilde de çalışır
- [ ] **7.1.5** Konuşma transferi + etiketleme
- [ ] **7.1.6** Basit dashboard (günlük metrikler)

**İleri özellikler:**
- [ ] **7.1.7** Offline queue (internet kesilince mesaj kuyruğa alınır)
- [ ] **7.1.8** Fotoğraf/dosya gönderme
- [ ] **7.1.9** Bildirim tercihleri (sessiz saatler, VIP only)

**Niche-özel faydalar:**
- Diş: Doktor gece mesai dışı acil mesajları mobilde görebilir
- Estetik: Operasyon sorumlusu sahada lead takibi yapabilir
- E-ticaret: Satıcı hareket halinde siparişleri yönetebilir

**Yapılmayacak:**
- ❌ Tam dashboard (web'de yeterli)
- ❌ Admin/config işlemleri (web'de)
- ❌ Knowledge base yönetimi (web'de)

---

### GR-7.2: Yeni Kanal Entegrasyonları

> **Servis:** Ana Uygulama + `Integrations`
> **Sektör:** Tümü (müşteri talebine göre önceliklendir)

- [ ] **7.2.1** Shopify / WooCommerce entegrasyonu (global müşteri talebi varsa)
- [ ] **7.2.2** Amazon Türkiye / n11 marketplace entegrasyonu
- [ ] **7.2.3** Google Business Messages (sağlık: Google Maps'ten direkt mesaj)
- [ ] **7.2.4** Apple Business Chat (iOS ağırlıklı pazarlar)
- [ ] **7.2.5** Web chat widget (kendi sitesinden mesaj başlatma)

**Yapılmayacak:**
- ❌ Her kanalı aynı anda ekleme (talep bazlı)
- ❌ Özel protokol kanalları (Signal vb.)

---

### GR-7.3: Voice & Video

> **Servis:** Yeni veya mevcut VOIP genişleme
> **Sektör:** Tümü

- [ ] **7.3.1** Voice message transcription (Whisper API — sesli mesajı yazıya çevir)
- [ ] **7.3.2** Video call entegrasyonu (sağlık konsültasyon — medikal turizm)
- [ ] **7.3.3** Voice analytics (ses tonundan sentiment)

**Yapılmayacak:**
- ❌ Tam çağrı merkezi (VOIP zaten var)
- ❌ Video kayıt/arşiv (KVKK riski çok yüksek)

---

### GR-7.4: Predictive Analytics

> **Servis:** `AgentAI` + Backend
> **Sektör:** Tümü
> **Ön koşul:** Minimum 6 aylık data biriktikten sonra

- [ ] **7.4.1** Churn prediction (hangi müşteri ayrılmak üzere?)
- [ ] **7.4.2** Predictive lead scoring (hangi lead kapanacak?)
- [ ] **7.4.3** Best send-time prediction (outbound mesaj ne zaman gönderilsin?)
- [ ] **7.4.4** Demand forecasting
  - Sağlık: randevu talebi tahmini
  - E-ticaret: sezonluk talep

**Yapılmayacak:**
- ❌ Veri yetersizse force etme — minimum 6 aylık data gerekli

---

### GR-7.5: Global Pazar Hazırlığı

> **Servis:** Backend + `Outbound` genişleme
> **Sektör:** Tümü

- [ ] **7.5.1** Multi-currency desteği (USD/EUR/GBP — medikal turizm)
- [ ] **7.5.2** Timezone-aware scheduling (outbound + randevu)
- [ ] **7.5.3** Yeni dil desteği (Rusça, Almanca — medikal turizm talebi varsa)
- [ ] **7.5.4** Compliance scanner (KVKK/GDPR otomatik tarama)
- [ ] **7.5.5** Regional template library (ülke bazlı WhatsApp template onay)

**Yapılmayacak:**
- ❌ Her ülkeye ayrı instance (multi-tenant yeterli)
- ❌ Local payment gateway'ler (iyzico/PayTR global desteği yeterli)

---

### GR-7.6: QR Kod ile Hızlı Erişim

> **Servis:** Backend + Mobil Uygulama
> **Kaynak:** M6 (B5.1 — QR Kod)
> **Sektör:** Tümü
> **Etki:** ORTA — fiziksel→dijital köprü
>
> **v6 (2026-02-16):** Otel odası, restoran masası, klinik bekleme salonunda QR → direkt WA.

- [ ] **7.6.1** QR kod jeneratörü: tenant + konum + kanal bilgisi gömülü
- [ ] **7.6.2** QR tarama → WA konuşma başlatma (pre-filled mesaj)
- [ ] **7.6.3** QR tarama anında opt-in toplama fırsatı (GR-3.26 ile entegre)
- [ ] **7.6.4** Konum bazlı routing: QR'daki konum bilgisine göre doğru şubeye yönlendirme
- [ ] **7.6.5** QR analytics: tarama sayısı, dönüşüm, lokasyon bazlı performans

---

### GR-7.7: Çevrimdışı Mod

> **Servis:** Mobil Uygulama (GR-7.1)
> **Kaynak:** M7 (B5.2 — Çevrimdışı Mod)
> **Sektör:** Tümü
> **Etki:** DÜŞÜK-ORTA — saha çalışanları için
>
> **v6 (2026-02-16):** İnternet olmadan önceki mesajları görme, taslak kaydetme.

- [ ] **7.7.1** Offline mesaj kuyruğu: internet yokken yazdığın mesajlar local'de saklanır
- [ ] **7.7.2** Son mesajları cache'leme: son N konuşmayı offline erişilebilir yap
- [ ] **7.7.3** Taslak kaydetme: offline hazırlanan cevapları kuyrukta tut
- [ ] **7.7.4** Sync mekanizması: internet gelince otomatik gönder + çakışma çöz

---

## Phase 7 Sonunda

- 200++ aktif müşteri
- MRR 2M++ TL
- Mobil uygulama (iOS + Android) yayında
- Yeni kanal(lar) aktif (talep bazlı)
- Voice transcription çalışıyor
- Predictive analytics pilot başlamış
- Global müşteri altyapısı hazır (multi-currency, timezone, yeni diller)
