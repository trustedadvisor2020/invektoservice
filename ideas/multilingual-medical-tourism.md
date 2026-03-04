<!-- Status: EVALUATED | PKT-13 | 2026-03-04 -->
# 7/24 Çok Dilli Medikal Turizm Asistanı

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm) + pazar araştırması
> **Durum:** FİKİR AŞAMASI
> **Sektör:** Estetik Klinikler (primer), Diş Klinikleri (sekonder)
> **Bağımsız SaaS Potansiyeli:** Evet — herhangi bir medikal turizm klinik

---

## Problem

Türkiye medikal turizm pazarı **$3.48 milyar** (2024) ve büyüyor. Ama klinikler bu pastadan yeterince pay alamıyor çünkü:

1. **Dil bariyeri:** Arapça, Rusça, Almanca, İngilizce bilen personel az ve pahalı
2. **Saat farkı:** Dubai'den gece 2'de (TR saati) gelen mesaja sabah 9'da cevap → hasta çoktan rakibe gitmiş
3. **İlk cevap veren kazanır:** Hasta 3-5 kliniğe aynı anda yazıyor, ilk anlamlı cevap veren %70+ kazanıyor
4. **Aracı komisyonu:** Klinikler medikal turizm ajanslarına %30-40 komisyon ödüyor çünkü kendi başlarına dil bariyerini aşamıyor

### Sayılarla Problem

| Metrik | Değer |
|--------|-------|
| Türkiye medikal turizm pazarı (2024) | $3.48 milyar |
| Orta klinik aylık uluslararası sorgu | 500-2,000 |
| Sorguların cevap alma süresi (şu an) | 4-24 saat |
| Cevapsız kalan sorgular | %30-50 |
| İlk cevap veren klinik dönüşüm avantajı | %70+ |
| Ajans komisyon oranı | %30-40 |
| Uluslararası hasta LTV (estetik) | $3,000-20,000 |
| Uluslararası hasta LTV (diş) | $2,000-10,000 |
| Arapça bilen personel maliyeti (TR) | ₺30,000-50,000/ay |
| Rusça bilen personel maliyeti (TR) | ₺25,000-40,000/ay |

### Hasta Yolculuğu (Bugün)

```
1. Hasta Instagram/Google'da klinik buluyor
2. WhatsApp'tan yazıyor (genellikle Arapça/İngilizce)
3. ⏳ 4-24 saat bekleme (personel mesai saatinde değil veya dil bilmiyor)
4. Hasta 3-5 kliniğe daha yazıyor
5. İlk anlamlı cevap veren klinik kazanıyor
6. Cevap veremeyen klinik → ajansa yönlendiriyor → %30-40 komisyon
7. Veya: Hasta daha hızlı cevap veren ülkeye (Dubai, Güney Kore) gidiyor
```

### Mevcut Çözümler

| Rakip | Fiyat | Eksik |
|-------|-------|-------|
| **Çok dilli personel** | ₺30-50K/ay/dil | Pahalı, mesai saati sınırlı, ölçeklenemiyor |
| **Medikal turizm ajansları** | %30-40 komisyon | Marjı eritiyor, hasta ilişkisi kliniğin değil |
| **Google Translate** | Ücretsiz | Tıbbi terminoloji hataları, kişiselleştirme yok |
| **Genel chatbot'lar** | $50-200/ay | Medikal bağlam yok, çok dilli zayıf |

**Boşluk:** 7/24, çok dilli (5+ dil), medikal bağlamlı, WhatsApp native, otomatik + akıllı bir asistan. Bu yok.

---

## Çözüm

**Multilingual Medical Tourism Assistant:** Hasta hangi dilden, hangi saatten yazarsa yazsın — anında, o dilde, medikal bağlamda doğru, kişiselleştirilmiş cevap veren AI asistan.

### Hasta Deneyimi

#### Senaryo 1: Arapça Sesli Mesaj (Gece 02:00 TR)

```
Hasta (Dubai, 03:00 yerel):
[🎤 Arapça sesli mesaj — 90 saniye]
"مرحباً، أريد عملية تجميل الأنف. كم التكلفة تقريباً؟
 وهل تشمل الفندق والنقل من المطار؟
 أنا متاح في شهر مارس..."

━━━ AI Pipeline (8 saniye) ━━━

1. Sesli Mesaj AI: 🎤 → 📝 Arapça transkript
2. Dil algılama: Arapça
3. Intent: tedavi_fiyat + paket_sorgusu + tarih_müsaitlik
4. Klinik tedavi kataloğu: rhinoplasty → €3,000-5,000
5. Paket bilgisi: 5 gece otel + transfer dahil
6. Takvim: Mart ayı müsait

━━━ Otomatik Arapça Cevap (5 saniye) ━━━

"مرحباً! شكراً لتواصلك مع عيادتنا 💫

 عملية تجميل الأنف:

 💰 السعر: ٣,٠٠٠ - ٥,٠٠٠ يورو
    (يعتمد على نوع العملية - مفتوحة أو مغلقة)

 🏨 الباكج يشمل:
    ✅ ٥ ليالي فندق ٤ نجوم
    ✅ نقل من وإلى المطار
    ✅ مترجم عربي مرافق
    ✅ فحوصات ما قبل العملية
    ✅ متابعة لمدة سنة

 📅 أقرب موعد متاح: ١٥ مارس ٢٠٢٦

 📹 نقدم استشارة فيديو مجانية مع الدكتور:
    [احجز الآن]

 📸 لتقييم أدق، أرسل لنا صورة لوجهك من الأمام والجانب

 هل لديك أسئلة أخرى؟ نحن هنا لمساعدتك 🤗"
```

#### Senaryo 2: İngilizce Metin (Sabah 06:00 TR)

```
Patient (UK): "Hi, I'm interested in dental veneers.
              How much for a full set? Do you offer financing?"

━━━ AI (5 saniye) ━━━

"Hello! Thank you for reaching out 😊

 Full Set Dental Veneers (20 teeth):

 💰 Price range: £2,800 - £4,500
    • Porcelain: £3,500 - £4,500
    • Zirconia: £2,800 - £3,800

 🏨 Package includes:
    ✅ 7 nights hotel (4-star, city center)
    ✅ Airport transfers
    ✅ Panoramic X-ray & consultation
    ✅ Temporary veneers during production
    ✅ 5-year warranty

 💳 Financing available:
    • 50% deposit, 50% on arrival
    • Or: 3 monthly instalments (0% interest)

 📅 Next available: March 10, 2026
    Treatment duration: 5-7 days (2 visits)

 📸 For an accurate quote, please send a photo
    of your smile (front view, teeth visible)

 Would you like to book a free video consultation?"
```

#### Senaryo 3: Rusça (Öğleden sonra 15:00 TR)

```
Пациент (Москва): "Здравствуйте, интересует пересадка волос.
                   Сколько стоит и какие методы используете?"

━━━ AI (5 saniye) ━━━

"Здравствуйте! Спасибо за ваш интерес 💫

 Пересадка волос:

 💰 Стоимость:
    • FUE: €1,500 - €2,500
    • DHI: €2,000 - €3,500
    (зависит от количества графтов)

 🏨 Пакет включает:
    ✅ 3 ночи отель 4*
    ✅ Трансфер аэропорт-отель-клиника
    ✅ Русскоговорящий координатор
    ✅ PRP терапия (1 сеанс)
    ✅ Набор для послеоперационного ухода

 📸 Для точной оценки пришлите фото головы
    (сверху, спереди, сзади)

 📹 Бесплатная видео-консультация: [Записаться]"
```

---

## Mimari

```
┌─────────────────────────────────────────────────────────────┐
│                    INPUT LAYER                               │
│                                                             │
│  WhatsApp / Instagram DM / Web Chat                         │
│  ↓                                                          │
│  Mesaj tipi algıla:                                         │
│  • Yazılı → Dil algılama (fasttext/langdetect)              │
│  • Sesli → Voice AI transkript → Dil algılama               │
│  • Görsel → Face Analysis AI / VPS (varsa)                  │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    LANGUAGE ROUTER                            │
│                                                             │
│  Algılanan dil → uygun pipeline:                            │
│                                                             │
│  🇹🇷 Türkçe  → Mevcut Invekto pipeline (AgentAI/Automation) │
│  🇬🇧 English → Medical Tourism Pipeline                     │
│  🇸🇦 العربية → Medical Tourism Pipeline (AR)                 │
│  🇷🇺 Русский → Medical Tourism Pipeline (RU)                 │
│  🇩🇪 Deutsch → Medical Tourism Pipeline (DE)                 │
│  🇫🇷 Français→ Medical Tourism Pipeline (FR) — opsiyonel    │
│                                                             │
│  Ayrıca: Internal çeviri (klinik personeline TR göster)     │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    MEDICAL TOURISM ENGINE                     │
│                                                             │
│  ┌──────────────────────────────────┐                       │
│  │ Intent + Entity Extraction       │                       │
│  │ ─────────────────────────────── │                       │
│  │ Intent: treatment_inquiry,       │                       │
│  │         price_query,             │                       │
│  │         package_query,           │                       │
│  │         availability_check,      │                       │
│  │         photo_consultation       │                       │
│  │                                  │                       │
│  │ Entity: treatment_type,          │                       │
│  │         budget, travel_date,     │                       │
│  │         patient_country,         │                       │
│  │         accommodation_pref       │                       │
│  └────────────────┬─────────────────┘                       │
│                   ▼                                          │
│  ┌──────────────────────────────────┐                       │
│  │ Knowledge Retrieval (RAG)        │                       │
│  │ ─────────────────────────────── │                       │
│  │ Klinik bilgi tabanı:             │                       │
│  │ • Tedavi kataloğu (fiyat, süre)  │                       │
│  │ • Paket detayları (otel, transfer)│                       │
│  │ • Doktor profilleri               │                       │
│  │ • Sık sorulan sorular             │                       │
│  │ • Contraindications               │                       │
│  │ • İyileşme süreleri               │                       │
│  └────────────────┬─────────────────┘                       │
│                   ▼                                          │
│  ┌──────────────────────────────────┐                       │
│  │ Response Generator (çok dilli)   │                       │
│  │ ─────────────────────────────── │                       │
│  │ • Hastanın dilinde cevap oluştur │                       │
│  │ • Kültürel uyum (Arap → resmi,  │                       │
│  │   İngiliz → rahat, Rus → detaylı)│                       │
│  │ • Fiyatları hastanın para        │                       │
│  │   biriminde göster (EUR/USD/GBP) │                       │
│  │ • Randevu/video konsültasyon link│                       │
│  └──────────────────────────────────┘                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    CLINIC STAFF VIEW                          │
│                                                             │
│  Personel ekranında:                                         │
│  • Orijinal mesaj (yabancı dilde)                           │
│  • Türkçe çeviri                                             │
│  • AI'ın verdiği cevap (yabancı dilde)                      │
│  • Hastanın ülkesi, dili, ilgilendiği tedavi                │
│  • Lead skoru (sıcak/soğuk)                                 │
│  • "Doktora yönlendir" butonu                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Desteklenen Diller (Öncelik Sırası)

| Dil | Pazar | Hasta LTV | Hacim | Öncelik |
|-----|-------|-----------|-------|---------|
| 🇬🇧 İngilizce | UK, ABD, Avustralya | €3,000-15,000 | Yüksek | MVP |
| 🇸🇦 Arapça | BAE, S. Arabistan, Kuveyt | €5,000-20,000 | Yüksek | MVP |
| 🇷🇺 Rusça | Rusya, Kazakistan | €2,000-10,000 | Orta | Phase 2 |
| 🇩🇪 Almanca | Almanya, Avusturya, İsviçre | €3,000-12,000 | Orta | Phase 2 |
| 🇫🇷 Fransızca | Fransa, Belçika, K.Afrika | €2,000-8,000 | Düşük | Phase 3 |

---

## Kültürel Uyum Katmanı

| Dil/Kültür | İletişim Stili | Fiyat Gösterimi | Önemli Notlar |
|------------|---------------|-----------------|---------------|
| **Arapça** | Resmi, saygılı, uzun selamlama | EUR veya USD | Helal yemek, namaz saatleri, kadın doktor tercihi |
| **İngilizce** | Rahat, profesyonel, kısa | GBP (UK), USD (ABD), AUD | NHS karşılaştırma, sigorta, garanti |
| **Rusça** | Detaylı, teknik, güven odaklı | EUR veya USD | Before/after çok önemli, Rusça koordinatör |
| **Almanca** | Formal, kesin bilgi, sertifika | EUR | JCI akreditasyonu, kalite sertifikaları, hassas fiyat |

---

## Maliyet Analizi

### Birim Maliyet (Konuşma Başına)

| Bileşen | Maliyet |
|---------|---------|
| Dil algılama | ~$0.001 |
| Sesli mesaj transkript (varsa) | ~$0.006 |
| Intent + entity extraction | ~$0.005 |
| Knowledge retrieval (RAG) | ~$0.003 |
| Response generation (çok dilli) | ~$0.01 |
| WhatsApp mesaj | ~$0.05-0.15 |
| **Toplam** | **~$0.07-0.18/konuşma** |

### ROI Hesabı (Estetik Klinik)

```
Şu an (ajans ile):
  100 uluslararası hasta/ay × €5,000 ort. tedavi = €500,000 gelir
  Ajans komisyonu: %35 = €175,000 kayıp
  Net: €325,000

AI ile (direkt):
  100 mevcut + 50 yeni (7/24 hızlı cevap) = 150 hasta/ay
  150 × €5,000 = €750,000 gelir
  AI maliyeti: ~€500/ay
  Net: €749,500

Fark: +€424,500/ay (ajans komisyonu tasarrufu + yeni hasta geliri)
```

### Fiyatlandırma Önerisi

| Plan | Fiyat | Konuşma/ay | Diller | İçerik |
|------|-------|-----------|--------|--------|
| **Starter** | $99/ay | 500 | EN + 1 dil | Temel tedavi bilgisi + fiyat |
| **Growth** | $249/ay | 2,000 | EN + AR + 1 dil | + Paket bilgisi + randevu |
| **Pro** | $499/ay | 10,000 | 5 dil | + Sesli mesaj + video konsültasyon |
| **Enterprise** | Custom | Sınırsız | Tüm diller | + Özel branding + SLA |

---

## MVP Scope

| Bileşen | MVP'de Var | Sonrası |
|---------|------------|---------|
| İngilizce metin cevap | ✅ | |
| Arapça metin cevap | ✅ | |
| Tedavi kataloğu + fiyat (tenant bazlı) | ✅ | |
| Paket bilgisi (otel, transfer) | ✅ | |
| Randevu/video konsültasyon linki | ✅ | |
| Klinik personeline Türkçe çeviri | ✅ | |
| Lead skoru (sıcak/soğuk) | ✅ | |
| Döviz çevirisi (EUR/USD/GBP) | ✅ | |
| Sesli mesaj desteği (EN/AR) | | ✅ (Voice AI ile) |
| Rusça + Almanca | | ✅ |
| Kültürel uyum (helal, namaz, vs.) | | ✅ |
| Video konsültasyon entegrasyonu | | ✅ |
| Before/after galeri gönderimi | | ✅ |
| Ajans bypass analitik (kurtarılan komisyon) | | ✅ |

---

## Diğer Fikirlerle Sinerji

```
Multilingual + Voice AI + Face Analysis birlikte:

Hasta (Dubai, gece 02:00):
  1. 🎤 Arapça sesli mesaj: "Burun estetiği istiyorum"
     → Voice AI: transkript + çeviri

  2. 📸 Selfie gönderir
     → Face Analysis AI: kişiselleştirilmiş analiz raporu (Arapça)

  3. 🤖 Otomatik Arapça cevap:
     "Analiz raporunuz hazır! Sizin için rhinoplasty (kapalı teknik) öneriyoruz.
      €3,500 paket dahil otel+transfer. Mart 15'te müsaitiz."

  4. 📅 Randevu alır (gece 02:00'de, kimse uyandırılmadı)

= 3 AI servis birlikte → tam otomatik medikal turizm pipeline
```

---

## AHA Moments

| Kategori | AHA |
|----------|-----|
| **UX** | Dubai'den gece 2'de Arapça sesli mesaj → 10 saniyede Arapça cevap + fiyat + paket + randevu linki |
| **SPEED** | "3 kliniğe yazdım, sizden 10 saniyede cevap geldi, diğerleri hâlâ susuyor" — ilk cevap = %70+ dönüşüm |
| **SALES** | Ajans komisyonu %35 → AI maliyeti %0.1 — yıllık yüz binlerce euro tasarruf |
| **RELIABILITY** | 7/24, 365 gün, hiç hasta cevapsız kalmıyor — tatil günleri, gece vardiyası problemi yok |
| **SUPPORT** | Klinik personeli ekranda: "Orijinal (Arapça) + Türkçe çeviri + AI cevabı" — dil bilmeden takip edebiliyor |

---

## Riskler

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| Tıbbi terminoloji çeviri hataları | 🟠 Yüksek | Medikal turizm-özel bilgi tabanı, doktor review |
| AI yanlış fiyat/paket bilgisi verirse | 🟠 Yüksek | Tenant kataloğundan çek, "tahmini fiyat" disclaimer |
| Kültürel hassasiyet (dini/cinsiyet) | 🟡 Orta | Kültürel uyum katmanı, tenant özelleştirmesi |
| Hasta güveni — "bot mu benimle konuşan?" | 🟡 Orta | Doğal dil, klinik adına konuşma, gerekince insana devret |
| KVKK/GDPR — uluslararası veri | 🟠 Yüksek | Hasta ülkesine göre uyum, şifreli iletişim |

---

## Roadmap Referansı

> **Phase:** 3B (Niche Derinleştirme) — [phases/phase-3b.md](phases/phase-3b.md)
> **GR:** GR-3.25 Multilingual Medical Tourism Assistant
> **İlişki:** GR-3.22 Medikal Turizm Lead genişletme + GR-2.3 Multi-lang altyapısı
> **Entegre:** 2026-02-14

---

## Sonraki Adımlar

- [x] Q karar: Phase 3B — GR-3.22 genişletme olarak entegre edildi (GR-3.25)
- [ ] Pazar araştırması: 3-5 estetik klinikle "uluslararası hasta yönetimi nasıl?" görüşmesi
- [ ] PoC: Claude ile İngilizce + Arapça medikal turizm konuşma testi (50 simüle)
- [ ] Mevcut müşterilerden kaçı medikal turizm yapıyor? → erken adopter bulma
- [ ] Ajans komisyon karşılaştırması: gerçek rakamlarla ROI doğrulama
