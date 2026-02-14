# Phase 3D — Face Analysis AI (Estetik Görsel Konsültasyon)

> **Hafta:** 29-32
> **MRR Hedefi:** 1M-1.2M TL
> **Müşteri Hedefi:** 120+ (mevcut) + FaceAI-only klinikler
> **Bağımlılık:** Phase 2 (Knowledge/RAG), Phase 3B (Multi-lang GR-3.25, Medikal Turizm GR-3.22)
> **Durum:** ⬜ Başlamadı
>
> **v4.5 (2026-02-14):** Estetik kliniklere selfie gönderen hastaları AI ile anında analiz etme.
> Kişiselleştirilmiş tedavi önerisi + fiyat + randevu. 7/24, çok dilli.
> Detaylı fikir dokümanı: [../face-analysis-ai.md](../face-analysis-ai.md)

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-3D.1 Face Analysis Core Engine | ⬜ Başlamadı | — | MediaPipe + Claude Vision hibrit |
| GR-3D.2 Treatment Matching | ⬜ Başlamadı | — | Tenant tedavi kataloğu eşleştirme |
| GR-3D.3 Multi-Language Response | ⬜ Başlamadı | — | TR/EN/AR cevap + kültürel uyum |
| GR-3D.4 WhatsApp + IG Entegrasyonu | ⬜ Başlamadı | — | Selfie → analiz → otomatik yanıt |
| GR-3D.5 Analytics & Ethics | ⬜ Başlamadı | — | Dashboard + KVKK + disclaimer |

---

## Özet

Hasta selfie gönderir → AI yüz analizi yapar → kişiselleştirilmiş tedavi önerileri + fiyat aralıkları + randevu linki döner. 7/24, çok dilli, otomatik.

**Satış dili:** "Hasta gece 2'de selfie attı, 5 saniyede kişisel analiz raporu + randevu linki aldı — rakipler uyuyor, siz satış yapıyorsunuz."

**Neden ayrı phase?**
- Tamamen yeni mikro servis + görsel AI pipeline (MediaPipe + Claude Vision)
- Bağımsız SaaS potansiyeli — Invekto dışı estetik klinikler de kullanabilir
- Etik/yasal katman (tıbbi tavsiye disclaimeri, KVKK biyometrik veri)
- Phase 3B'deki medikal turizm ve multi-lang altyapısını kullanır

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.FaceAnalysis` | 7110 | Yüz analizi, tedavi eşleştirme, çok dilli rapor |

---

## Gereksinimler

### GR-3D.1: Face Analysis Core Engine

> **Servis:** `Invekto.FaceAnalysis` (port 7110) — YENİ
> **Bağımlılık:** Phase 2 Knowledge (RAG — tedavi bilgisi)

- [ ] **3D.1.1** FaceAnalysis servis iskeletini oluştur (port 7110, health check, tenant izolasyon)
- [ ] **3D.1.2** Image Input Handler:
  - Yüz fotoğrafı mı kontrol (face detection)
  - Birden fazla yüz kontrolü → "Tek kişilik fotoğraf gönderin"
  - Kalite kontrolü (aydınlatma, açı, netlik)
- [ ] **3D.1.3** Face Detection + Landmark (MediaPipe):
  - 468 landmark noktası
  - Bölge segmentasyonu (alın, göz, burun, dudak, çene, boyun)
  - Simetri analizi
- [ ] **3D.1.4** Region Analysis:
  - Alın: kırışıklık seviyesi (0-10)
  - Göz: torba, halka, kaz ayağı
  - Burun: dorsal profil, uç açısı, simetri
  - Dudak: hacim, simetri, komissür
  - Çene: kontür, çift çene, asimetri
  - Cilt: kırışıklık, leke, gözenek, nem
- [ ] **3D.1.5** Claude Vision Estetik Değerlendirme:
  - MediaPipe geometri + Claude Vision estetik yorum
  - Doğal dil çıktı → yapılandırılmış tedavi önerisi
- [ ] **3D.1.6** DB — Core:
  ```sql
  face_analyses (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    patient_phone VARCHAR(20),
    channel VARCHAR(20),
    image_hash VARCHAR(64),
    face_quality_score FLOAT,
    regions_analysis JSONB,
    recommendations JSONB,
    response_lang VARCHAR(5),
    response_time_ms INT,
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

### GR-3D.2: Treatment Matching

> **Servis:** `Invekto.FaceAnalysis`
> **Bağımlılık:** GR-3D.1 (face analysis), Tenant tedavi kataloğu

- [ ] **3D.2.1** Tenant tedavi kataloğu yönetimi:
  - Tedavi adı, açıklama, fiyat aralığı, süre, recovery süresi
  - Kontrendikasyonlar
- [ ] **3D.2.2** Treatment Matching Algorithm:
  - Bölge analizi + tenant tedavi kataloğu eşleştir
  - Hasta yaşı/cinsiyeti → uygun tedaviler filtrele
  - Agresiflik limiti: max 3 öneri (etik — "her şeyi yaptır" demek değil)
  - Kombinasyon önerileri (botox + dolgu paketi)
- [ ] **3D.2.3** Fiyat aralığı gösterimi (tenant'ın fiyatlarından)
- [ ] **3D.2.4** Randevu/video konsültasyon linki ekleme
- [ ] **3D.2.5** DB — Catalog:
  ```sql
  treatment_catalog (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    treatment_name VARCHAR(200) NOT NULL,
    category VARCHAR(100),
    description_tr TEXT,
    description_en TEXT,
    description_ar TEXT,
    price_min DECIMAL(10,2),
    price_max DECIMAL(10,2),
    currency VARCHAR(3) DEFAULT 'TRY',
    duration_minutes INT,
    recovery_days INT,
    contraindications TEXT,
    target_regions JSONB,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

### GR-3D.3: Multi-Language Response

> **Servis:** `Invekto.FaceAnalysis` + Phase 3B Multilingual altyapısı (GR-3.25)
> **Bağımlılık:** GR-3D.1 + GR-3.25 (Multilingual Medical Tourism)

- [ ] **3D.3.1** Analiz raporunu hastanın dilinde oluştur (TR/EN/AR)
- [ ] **3D.3.2** Kültürel uyum:
  - Arapça: resmi + EUR/USD fiyat + paket bilgisi
  - İngilizce: rahat + GBP/USD + before/after referans
  - Rusça: detaylı + teknik + EUR
- [ ] **3D.3.3** Etik disclaimer (her raporda, her dilde)
- [ ] **3D.3.4** Klinik personeline TR gösterim (orijinal + çeviri + AI rapor)

---

### GR-3D.4: WhatsApp + Instagram Entegrasyonu

> **Servis:** `Invekto.FaceAnalysis` + Invekto Ana Uygulama
> **Bağımlılık:** GR-3D.1 (analysis engine)

- [ ] **3D.4.1** WhatsApp selfie algılama → FaceAnalysis'e gönder → rapor döndür
- [ ] **3D.4.2** Instagram DM selfie handler
- [ ] **3D.4.3** "Lütfen net bir selfie gönderin" kalite uyarısı
- [ ] **3D.4.4** Flow Builder'da "Selfie Analizi" trigger node'u
- [ ] **3D.4.5** Analiz sonrası: randevu linki + "Video konsültasyon ister misiniz?"

---

### GR-3D.5: Analytics & Ethics

> **Servis:** `Invekto.FaceAnalysis` + Dashboard
> **Bağımlılık:** GR-3D.1-4

- [ ] **3D.5.1** Analiz dashboard:
  - Günlük analiz sayısı
  - Kanal bazlı dağılım
  - En çok önerilen tedaviler
  - Analiz → randevu dönüşüm oranı
- [ ] **3D.5.2** Etik kontroller:
  - 18 yaş altı analiz engeli
  - Agresif satış engeli (max 3 öneri)
  - Her zaman "ön değerlendirme" disclaimer
- [ ] **3D.5.3** KVKK uyum:
  - Açık rıza (analiz öncesi)
  - Analiz sonrası fotoğraf silme opsiyonu
  - Biyometrik veri özel kategori uyumluluğu
- [ ] **3D.5.4** Gizlilik: fotoğraf şifreleme + silme politikası

---

## Genişleme Potansiyeli (Gelecek)

| Genişleme | Sektör | Açıklama |
|-----------|--------|----------|
| **Gülüş Analizi** | Diş | Hasta gülümseme fotoğrafı → beyazlatma/kaplama önerisi |
| **Saç Analizi** | Saç Ekimi | Hasta saç fotoğrafı → Norwood skalası → tedavi önerisi |
| **Before/After Simülasyon** | Estetik | AI ile tedavi sonrası görünüm simülasyonu |
| **Vücut Analizi** | Estetik Cerahi | Vücut fotoğrafı → liposuction/karın germe önerisi |

---

## Fiyatlandırma Modeli

| Plan | Fiyat | Analiz/ay | Diller |
|------|-------|-----------|--------|
| **Starter** | $79/ay | 200 | TR |
| **Growth** | $199/ay | 1,000 | TR + EN |
| **Pro** | $399/ay | 5,000 | Tüm diller |
| **Enterprise** | Custom | Sınırsız | Tüm diller + özel branding |

---

## Çıkış Kriterleri (Phase 4'e Geçiş Şartı)

- [ ] FaceAnalysis servis (:7110) production'da çalışıyor
- [ ] En az 3 estetik klinik aktif kullanıyor
- [ ] Analiz doğruluğu: klinik doktorlardan %80+ onay
- [ ] Analiz → randevu dönüşüm oranı %30+
- [ ] Multi-language çalışıyor (TR + EN + AR)
- [ ] KVKK/etik disclaimer tüm cevaplarda mevcut
- [ ] Analytics dashboard aktif

---

## Risk & Mitigasyon

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| AI yanlış tedavi önerirse → yasal risk | 🔴 Kritik | Her zaman disclaimer, "ön değerlendirme", doktor onayı zorunlu |
| Fotoğraf kalitesi düşük (aydınlatma, açı) | 🟡 Orta | Kalite kontrolü + "iyi aydınlatılmış ortamda tekrar çekin" |
| Hasta beklentisi yanlış oluşur | 🟠 Yüksek | "Kesin sonuç değil, yol gösterici" vurgusu + doktor konsültasyon zorunlu |
| Etik: AI baskıcı satış aracı olur | 🟠 Yüksek | Agresiflik limiti: max 3 öneri, "gereksiz" tedavi önerme |
| KVKK: Yüz fotoğrafı = biyometrik veri | 🟠 Yüksek | Açık rıza, analiz sonrası silme opsiyonu, şifreleme |

---

## Notlar

- VPS (Phase 3C) ile teknik sinerji: görsel AI altyapısı, pgvector, tenant kataloğu
- Phase 3B Multilingual (GR-3.25) altyapısını kullanır
- Phase 3B Voice AI (GR-3.23) ile birlikte: sesli mesaj → transkript → face analiz tetikle
- Etik/yasal katman kritik — AI tıbbi tavsiye vermez, sadece ön değerlendirme
- Bağımsız SaaS ürünü potansiyeli (Invekto dışı klinikler de kullanabilir)
- Detaylı fikir dokümanı: [../face-analysis-ai.md](../face-analysis-ai.md)