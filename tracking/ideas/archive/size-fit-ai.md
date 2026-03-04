<!-- ARCHIVED: 2026-03-04 | Reason: incorporated into PKT-7 as GR-3C.8 | See: tracking/pkt-07-visual-ai.md -->
# Beden/Ölçü AI — Akıllı Beden Önerisi Servisi

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm) + pazar araştırması
> **Durum:** FİKİR AŞAMASI
> **Sektör:** E-ticaret (giyim, ayakkabı, aksesuar)
> **Bağımsız SaaS Potansiyeli:** Evet — herhangi bir e-ticaret sitesi

---

## Problem

E-ticaret'in en büyük kanayan yarası: **iade oranı %35-40 ve #1 sebebi beden uyumsuzluğu.**

Her gün tekrarlanan senaryo:

```
Müşteri: "Boy 170, kilo 65. M mi S mi alayım?"
Çalışan: "M alın" (tahmin)
→ Ürün geldi, büyük
→ İade talebi
→ Kargo maliyeti (gidiş + dönüş): ₺80-150
→ Operasyon maliyeti (iade işleme): ₺30-50
→ Ürün deforme riski: ₺50-200
→ Müşteri memnuniyetsizliği → bir daha almaz
```

### Sayılarla Problem

| Metrik | Değer |
|--------|-------|
| E-ticaret iade oranı (Türkiye, giyim) | %35-40 |
| İade sebebi #1 | Beden uyumsuzluğu (%60-70) |
| İade başına maliyet (satıcıya) | ₺80-200 |
| Günlük "beden sorusu" (orta satıcı) | 30-100 |
| Çalışanın beden sorusuna cevap süresi | 2-5 dk |
| Tahminle verilen cevapların doğruluğu | %50-60 |

### Yıllık Maliyet (Orta E-ticaret)

```
Aylık sipariş:     5,000
İade oranı:        %35 = 1,750 iade
Beden kaynaklı:    %65 = 1,137 iade
İade başı maliyet: ₺120
Aylık kayıp:       ₺136,440
Yıllık kayıp:      ₺1,637,280 (sadece beden iadesi)
```

### Mevcut Çözümler

| Rakip | Fiyat | Eksik |
|-------|-------|-------|
| **True Fit** | Enterprise ($5K+/ay) | Pahalı, Türk marketplace'lere entegre değil |
| **Fit Analytics (Snap)** | Enterprise | Sadece büyük markalar, API karmaşık |
| **Sizefox** | $99+/ay | Sınırlı Türkçe, WhatsApp entegrasyonu yok |
| **Beden tablosu** | Ücretsiz | Statik, kişiye özel değil, müşteri anlamıyor |

**Boşluk:** WhatsApp'tan beden sorusu → AI kişiselleştirilmiş önerisi → "Bu üründe L alın, iade oranı %5". Bu yok.

---

## Çözüm

**Size/Fit AI:** Müşterinin beden bilgileri + ürünün ölçü verisi + geçmiş iade verisi = kişiselleştirilmiş beden önerisi. WhatsApp üzerinden konuşarak çalışır.

### Müşteri Deneyimi

```
Müşteri: "Boy 170, kilo 65, normalde M giyiyorum.
          Bu elbise nasıl kalıyor?"

━━━ AI Pipeline (3 saniye) ━━━

Girdi:
  • Müşteri: boy=170, kilo=65, normal beden=M
  • Ürün: Midi Elbise #12345 (ölçü tablosu var)
  • İade verisi: M alanların %38'i iade etti ("büyük geldi")
                 L alanların %5'i iade etti

Analiz:
  • Müşteri tahmini vücut ölçüleri (boy/kilo → göğüs/bel/kalça)
  • Ürün ölçüleriyle karşılaştır
  • İade verisini dahil et

━━━ Otomatik Cevap ━━━

Bot: "Bu elbise için size önerimiz:

     📏 Beden Analizi:
     • Göğüs: M uygun ✅
     • Bel: M 2cm dar kalabilir ⚠️
     • Boy: 170cm için etek boyu uygun ✅

     👉 Önerimiz: S/M arası kaldıysanız → M alın
        Ama bu ürün dar kalıp, rahat istiyorsanız → L alın

     📊 Veri: L alanların memnuniyet oranı %95
              M alanların memnuniyet oranı %62

     🔄 Yine de olmadıysa ücretsiz değişim!"
```

### İleri Seviye: Fotoğraf ile Beden Tahmini

```
Müşteri: [📸 boy fotoğrafı gönderir]
         "Bunun için beden ne alayım?"

AI: • Fotoğraftan vücut oranları tahmin (boy, omuz genişliği, bel çevresi)
    • Ürün ölçü tablosuyla karşılaştır
    • Kişiselleştirilmiş öneri ver
```

---

## Mimari

```
┌─────────────────────────────────────────────────────────────┐
│                    INPUT LAYER                               │
│                                                             │
│  Kaynak 1: WhatsApp konuşması                               │
│    "Boy 170, kilo 65, M giyiyorum"                          │
│    → NLP ile boy/kilo/beden çıkarma                         │
│                                                             │
│  Kaynak 2: Müşteri profili (CRM'den)                        │
│    Daha önce girdiği bilgiler + satın alma geçmişi          │
│                                                             │
│  Kaynak 3: Fotoğraf (opsiyonel)                             │
│    Body measurement estimation from photo                    │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    BODY ESTIMATION ENGINE                     │
│                                                             │
│  ┌──────────────────────────────────┐                       │
│  │ Measurement Predictor            │                       │
│  │ ─────────────────────────────── │                       │
│  │ Boy + Kilo + Cinsiyet + Yaş     │                       │
│  │ → Tahmini ölçüler:              │                       │
│  │   Göğüs: 94cm                   │                       │
│  │   Bel: 78cm                     │                       │
│  │   Kalça: 98cm                   │                       │
│  │   Omuz: 44cm                    │                       │
│  │   Kol boyu: 60cm               │                       │
│  └────────────────┬─────────────────┘                       │
│                   ▼                                          │
│  ┌──────────────────────────────────┐                       │
│  │ Size Matching Algorithm           │                       │
│  │ ─────────────────────────────── │                       │
│  │ Müşteri ölçüleri vs ürün ölçü   │                       │
│  │ tablosu → her beden için fit    │                       │
│  │ skoru hesapla                   │                       │
│  │                                  │                       │
│  │ S: sıkı (göğüs -2, bel +1)     │                       │
│  │ M: ideal (göğüs ✓, bel -2)     │                       │
│  │ L: rahat (göğüs +4, bel +3)    │                       │
│  └────────────────┬─────────────────┘                       │
│                   ▼                                          │
│  ┌──────────────────────────────────┐                       │
│  │ Return Data Enhancement          │                       │
│  │ ─────────────────────────────── │                       │
│  │ Bu ürünün iade verisi:           │                       │
│  │ S: %12 iade ("küçük geldi")     │                       │
│  │ M: %38 iade ("büyük geldi")     │                       │
│  │ L: %5 iade (memnun)             │                       │
│  │                                  │                       │
│  │ → İade verisini öneriyle birleş  │                       │
│  └────────────────┬─────────────────┘                       │
└───────────────────┼─────────────────────────────────────────┘
                    ▼
┌─────────────────────────────────────────────────────────────┐
│                    RECOMMENDATION ENGINE                      │
│                                                             │
│  • En iyi beden + güven skoru                                │
│  • "Dar/normal/rahat" kalıp tercihi sorusu                  │
│  • Fit detayı: hangi bölge iyi, hangi bölge riskli          │
│  • İade oranı verisi: sosyal kanıt                           │
│  • Alternatif: "Bu ürün yerine X daha rahat kalıyor"        │
└─────────────────────────────────────────────────────────────┘
```

---

## Veri Kaynakları

### Ürün Tarafı (Tenant'tan Gelir)

| Veri | Kaynak | Zorunluluk |
|------|--------|-----------|
| Ürün ölçü tablosu (S/M/L/XL → cm) | Tenant kataloğu | ✅ Zorunlu |
| Ürün kalıp tipi (dar/normal/bol) | Tenant girişi | 🟡 Opsiyonel |
| Kumaş esnekliği | Tenant girişi | 🟡 Opsiyonel |
| İade verisi (beden + sebep) | Tenant ERP/marketplace | 🟢 Varsa süper |

### Müşteri Tarafı (Konuşmadan Çıkar)

| Veri | Kaynak |
|------|--------|
| Boy, kilo | Müşteri söyler |
| Cinsiyet | Ürün kategorisinden veya müşteri söyler |
| Normal giydiği beden | Müşteri söyler |
| Kalıp tercihi (dar/rahat) | AI sorar |
| Geçmiş satın alma | CRM'den (varsa) |

### Topluluk Verisi (Zamanla Oluşur)

| Veri | Açıklama |
|------|----------|
| Beden bazlı memnuniyet | "M alanların %95'i memnun" |
| Beden bazlı iade oranı | "S alanların %30'u iade etti" |
| İade sebepleri | "büyük geldi", "küçük geldi", "kalıp beğenmedim" |

---

## Maliyet Analizi

### Birim Maliyet (Öneri Başına)

| Bileşen | Maliyet |
|---------|---------|
| NLP ile beden bilgisi çıkarma | ~$0.002 |
| Size matching algoritması | ~$0.001 |
| İade verisi sorgusu | ~$0.001 |
| **Toplam** | **~$0.004/öneri** |

(Fotoğraf bazlı body estimation eklenirse: +$0.01-0.02)

### Fiyatlandırma Önerisi

| Plan | Fiyat | Öneri/ay | İçerik |
|------|-------|----------|--------|
| **Starter** | $29/ay | 1,000 | Temel beden önerisi |
| **Growth** | $79/ay | 5,000 | + İade verisi entegrasyonu |
| **Pro** | $199/ay | 20,000 | + Fotoğraf analizi + kalıp detayı |
| **Enterprise** | Custom | Sınırsız | + Özel model eğitimi |

### ROI Hesabı

```
Önce:   5,000 sipariş/ay × %35 iade × %65 beden = 1,137 iade × ₺120 = ₺136,440/ay kayıp
Sonra:  5,000 sipariş/ay × %15 iade × %65 beden = 487 iade × ₺120 = ₺58,500/ay kayıp
Fark:   ₺77,940/ay tasarruf
AI:     ₺5,000/ay (Growth plan)
ROI:    15.6x
```

---

## MVP Scope

| Bileşen | MVP'de Var | Sonrası |
|---------|------------|---------|
| Boy/kilo → beden önerisi | ✅ | |
| Ürün ölçü tablosu eşleştirme | ✅ | |
| WhatsApp konuşma entegrasyonu | ✅ | |
| "Dar/rahat" tercih sorusu | ✅ | |
| Tenant ürün kataloğu API | ✅ | |
| İade verisi entegrasyonu | | ✅ |
| Fotoğraftan ölçü tahmini | | ✅ |
| Müşteri profili (geçmiş alışveriş) | | ✅ |
| "Bu ürün yerine X al" önerisi | | ✅ |
| Beden bazlı memnuniyet yüzdesi | | ✅ |

---

## VPS ile Sinerji

```
VPS + Size AI birlikte çalışırsa:

Müşteri: [📸 Instagram screenshot]
         "Bu var mı? 170 boy 65 kilo, beden ne olur?"

VPS:  → Ürünü bul (kırmızı midi elbise #12345)
Size: → Beden öner (L, güven %92)

Birleşik cevap:
"Bu ürünü bulduk! Kırmızı Midi Elbise - ₺899
 📏 Size önerimiz: L beden
 Stok: L ✅ mevcut
 👉 Satın al: magaza.com/urun/12345?size=L"
```

---

## AHA Moments

| Kategori | AHA |
|----------|-----|
| **UX** | "Boy/kilom + ürün = bana özel beden önerisi, beden tablosuna bakmama bile gerek kalmadı" |
| **SPEED** | 3 saniyede kişiselleştirilmiş beden önerisi — mağaza çalışanı 3 dakikada bile veremezdi |
| **RELIABILITY** | "L alanların %95'i memnun" — topluluk verisi güven veriyor |
| **SALES** | İade oranı %35 → %15 = aylık ₺80K+ tasarruf — satıcının ilk göreceği sayı |
| **SUPPORT** | "En çok iade edilen ürünler" raporu → ölçü tablosu yanlış olan ürünleri bul → düzelt |

---

## Riskler

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| Boy/kilo → ölçü tahmini hatalı olabilir | 🟡 Orta | Geniş tolerans aralığı + "emin değilseniz 1 beden büyük alın" |
| Ürün ölçü tablosu yanlış/eksik | 🟠 Yüksek | Tenant onboarding'de ölçü tablosu zorunlu + doğrulama |
| Farklı markalar farklı kalıp | 🟡 Orta | Marka/ürün bazlı kalıp profili + iade verisinden öğren |
| Fotoğraftan ölçü tahmini güvenilirliği | 🟡 Orta | MVP'de fotoğraf yok, sadece beyan + iade verisi |

---

## Roadmap Referansı

> **Phase:** 3C (Visual Product Search + Size/Fit) — [phases/phase-3c.md](phases/phase-3c.md)
> **GR:** GR-3C.8 Size/Fit AI (Akıllı Beden Önerisi)
> **Sinerji:** VPS (GR-3C.1-7) ile birleşik "ürün bul + beden öner" deneyimi
> **Entegre:** 2026-02-14

---

## Sonraki Adımlar

- [x] Q karar: Phase 3C — VPS ile birlikte entegre edildi (GR-3C.8)
- [ ] Veri analizi: Türk e-ticaret iade verisi — beden iadesi gerçek oranı
- [ ] PoC: 100 ürünün ölçü tablosu + 50 müşteri profili ile test
- [ ] Tenant onboarding: Ölçü tablosu nasıl toparlayacağız? (CSV, API, scrape?)
- [ ] VPS entegrasyon: Görsel arama + beden önerisi birleşik deneyim
