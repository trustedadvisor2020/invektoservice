<!-- ARCHIVED: 2026-03-04 | Reason: incorporated into PKT-8 | See: tracking/pkt-08-face-ai.md -->
# Yüz Analizi AI — Estetik Klinikler İçin Görsel Konsültasyon

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm) + pazar araştırması
> **Durum:** FİKİR AŞAMASI
> **Sektör:** Estetik Klinikler (primer), Diş Klinikleri (sekonder — gülüş tasarımı)
> **Bağımsız SaaS Potansiyeli:** Evet — herhangi bir estetik/güzellik klinik

---

## Problem

Estetik klinikler günde 50-300 WhatsApp sorgusu alıyor. Bunların büyük kısmı:

1. Hasta selfie gönderiyor: *"Ne önerirsiniz?"*
2. Sekreter ne diyeceğini bilmiyor (tıbbi bilgi veremez)
3. Doktora yönlendiriyor → doktor meşgul → hasta 2-3 gün bekliyor
4. Hasta beklerken rakip kliniğe yazıyor → **kaybedilen hasta**

### Sayılarla Problem

| Metrik | Değer |
|--------|-------|
| Günlük WhatsApp sorgusu (orta klinik) | 50-300 |
| "Ne önerirsiniz?" tarzı sorgular | %40-60 |
| Doktora ulaşma süresi | 1-3 gün |
| Bu sürede rakibe giden hasta | %60-70 |
| Ortalama hasta değeri (estetik) | ₺15,000-50,000 |
| Kayıp hasta başına gelir kaybı | ₺15,000-50,000 |

### Mevcut Çözümler

| Rakip | Fiyat | Eksik |
|-------|-------|-------|
| **Afters.me** | $299/ay | Sadece before/after simülasyon, WhatsApp entegrasyonu yok |
| **Perfect Corp (YouCam)** | Enterprise | Çok pahalı, sadece büyük zincirler |
| **EntityMed** | Custom | Yeni, sınırlı dil desteği |
| **Manuel Photoshop** | Personel maliyeti | 1 simülasyon = 30-60 dk, ölçeklenemiyor |

**Boşluk:** WhatsApp'tan selfie → anında AI analiz + tedavi önerisi + fiyat + randevu. Bu pipeline yok.

---

## Çözüm

**Face Analysis AI:** Hasta selfie gönderir → AI yüz analizi yapar → kişiselleştirilmiş tedavi önerileri + fiyat aralıkları + randevu linki döner. 7/24, çok dilli, otomatik.

### Hasta Deneyimi

```
Hasta: [📸 selfie gönderir]
       "Yüzüme ne yapılabilir?"

━━━ AI Pipeline (5-8 saniye) ━━━

Yüz Analizi:
  • Yüz bölgeleri tespiti (alın, göz çevresi, burun, dudak, çene, boyun)
  • Her bölge için yaşlanma/asimetri/hacim skoru
  • Cilt kalitesi analizi (kırışıklık, leke, gözenek)

Kişiselleştirilmiş Öneri:
  • Hastanın yaşı, cilt tipi, yüz yapısına göre
  • Kliniğin sunduğu tedavilerle eşleştir
  • Agresif olmayan yaklaşım (etik — "her şeyi yaptır" demek yerine)

━━━ Otomatik Yanıt (3 saniye) ━━━

Bot:
┌─────────────────────────────────────────────┐
│ 📋 Kişisel Yüz Analiz Raporunuz             │
│                                             │
│ Fotoğrafınızı inceledik. İşte kişisel       │
│ değerlendirmemiz:                            │
│                                             │
│ 🔍 Gözlemlerimiz:                           │
│  1. Göz altı: Hafif hacim kaybı            │
│  2. Dudak: Üst dudak ince                  │
│  3. Alın: Hafif ifade çizgileri             │
│                                             │
│ 💡 Size Özel Öneriler:                      │
│                                             │
│  ① Göz Altı Dolgusu                        │
│     Dinlenmiş bir görünüm sağlar            │
│     💰 ₺8,000 - ₺12,000                    │
│     ⏱️ 15-20 dakika, anestezisiz            │
│                                             │
│  ② Dudak Dolgusu                            │
│     Doğal dolgun dudak görünümü             │
│     💰 ₺5,000 - ₺8,000                     │
│     ⏱️ 15 dakika, minimal şişlik            │
│                                             │
│  ③ Botox (Alın)                             │
│     İfade çizgilerini yumuşatır             │
│     💰 ₺3,000 - ₺5,000                     │
│     ⏱️ 10 dakika, hemen günlük hayata dönüş │
│                                             │
│ ⚕️ Bu değerlendirme yol göstericidir.       │
│    Kesin tedavi planı yüz yüze muayenede    │
│    belirlenir.                               │
│                                             │
│ 📅 Ücretsiz Konsültasyon:                   │
│  [Pazartesi 14:00] [Çarşamba 10:00]         │
│                                             │
│ 📹 Online Video Konsültasyon:               │
│  [Randevu Al - Ücretsiz]                    │
└─────────────────────────────────────────────┘
```

### Medikal Turizm Senaryosu (İngilizce)

```
Patient (UK): [📸 selfie]
              "I'm interested in rhinoplasty, what do you think?"

Bot (EN):
┌─────────────────────────────────────────────┐
│ 📋 Your Personal Face Analysis Report       │
│                                             │
│ 🔍 Assessment:                              │
│  • Nasal bridge: Slight dorsal hump         │
│  • Tip: Mildly droopy                       │
│  • Overall: Good candidate for rhinoplasty  │
│                                             │
│ 💡 Recommended:                              │
│  Closed Rhinoplasty                         │
│  💰 €3,000 - €5,000                         │
│  🏨 Package includes:                       │
│     • 5 nights hotel                        │
│     • Airport transfer                      │
│     • 2 follow-up visits                    │
│                                             │
│ 📅 Next available: March 15                 │
│ 📹 Free video consultation: [Book Now]      │
└─────────────────────────────────────────────┘
```

---

## Mimari

```
┌─────────────────────────────────────────────────────────────┐
│                    IMAGE INPUT                               │
│                                                             │
│  WhatsApp / Instagram DM / Web Upload                       │
│  → Yüz fotoğrafı mı kontrol (face detection)               │
│  → Yüz yoksa: "Lütfen net bir selfie gönderin"              │
│  → Birden fazla yüz: "Tek kişilik fotoğraf gönderin"        │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    FACE ANALYSIS ENGINE                       │
│                                                             │
│  ┌─────────────────────┐  ┌──────────────────────────┐      │
│  │ Face Detection       │  │ Landmark Detection        │      │
│  │ ──────────────────  │  │ ────────────────────────  │      │
│  │ • MediaPipe Face    │  │ • 468 landmark noktası    │      │
│  │ • Yüz sınırlayıcı  │  │ • Bölge segmentasyonu     │      │
│  │ • Kalite kontrolü   │──>│   (alın, göz, burun,     │      │
│  │   (aydınlatma, açı, │  │    dudak, çene, boyun)    │      │
│  │    netlik)          │  │ • Simetri analizi         │      │
│  └─────────────────────┘  └──────────┬───────────────┘      │
│                                      ▼                       │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Region Analysis (bölge bazlı)                         │   │
│  │ ──────────────────────────────────────────────────── │   │
│  │ Alın: kırışıklık seviyesi (0-10)                      │   │
│  │ Göz: torba, halka, kaz ayağı seviyesi                 │   │
│  │ Burun: dorsal profil, uç açısı, simetri               │   │
│  │ Dudak: hacim, simetri, komissür                       │   │
│  │ Çene: kontür, çift çene, asimetri                     │   │
│  │ Boyun: bantlama, cilt kalitesi                        │   │
│  │ Cilt: kırışıklık, leke, gözenek, nem                  │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                          ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Treatment Matching (tedavi eşleştirme)                │   │
│  │ ──────────────────────────────────────────────────── │   │
│  │ • Bölge analizi + tenant'ın tedavi kataloğu           │   │
│  │ • Hasta yaşı/cinsiyeti → uygun tedaviler filtrele     │   │
│  │ • Agresiflik seviyesi (koruyucu → invaziv)            │   │
│  │ • Kombinasyon önerileri (botox + dolgu paketi)        │   │
│  │ • Tenant'ın fiyat aralıkları                          │   │
│  └──────────────────────┬───────────────────────────────┘   │
└──────────────────────────┼──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    RESPONSE BUILDER                           │
│                                                             │
│  • Hasta diline uygun mesaj (TR/EN/AR/RU/DE)                │
│  • Etik disclaimer (yol gösterici, kesin değil)             │
│  • Tedavi kartları (ad, açıklama, fiyat, süre)              │
│  • Randevu/video konsültasyon linki                          │
│  • Kanal formatı (WhatsApp card, web UI, IG DM)             │
└─────────────────────────────────────────────────────────────┘
```

---

## Teknik Seçenekler

### Yüz Analizi

| Seçenek | Artı | Eksi | Maliyet |
|---------|------|------|---------|
| **MediaPipe Face Mesh** | Ücretsiz, 468 landmark, hızlı | Sadece geometri, cilt analizi yok | Ücretsiz |
| **Claude Vision** | Estetik değerlendirme çok iyi, doğal dil çıktı | Yavaş, pahalı | ~$0.02-0.05/analiz |
| **Custom CV Model** | Tam kontrol | Eğitim verisi gerekli, geliştirme süresi | GPU maliyeti |
| **Hibrit (Önerilen)** | MediaPipe geometri + Claude Vision estetik değerlendirme | Daha karmaşık | Orta |

**Önerilen: Hibrit**
1. **MediaPipe** → Yüz tespiti, landmark'lar, simetri ölçümü, bölge segmentasyonu (ücretsiz, hızlı)
2. **Claude Vision** → Estetik değerlendirme, cilt kalitesi, tedavi önerisi mantığı (doğal dil, akıllı)
3. **Tenant tedavi kataloğu** → Önerilen tedavileri kliniğin sunduğu hizmetlerle eşleştir

### Etik & Yasal

| Konu | Yaklaşım |
|------|----------|
| **Tıbbi tavsiye değildir** | Her raporda disclaimer: "Bu AI destekli ön değerlendirmedir, kesin tedavi planı doktor muayenesinde belirlenir" |
| **Veri gizliliği** | Hasta fotoğrafı analiz sonrası silinir (opsiyonel: saklama izni ile) |
| **KVKK** | Sağlık verisi — açık rıza gerekli, özel kategori veri |
| **Agresif satış engeli** | AI "her şeyi yaptır" demez, gerçekten fayda görecek tedavileri önerir |
| **Yaş sınırı** | 18 yaş altı analiz yapmaz |

---

## Maliyet Analizi

### Birim Maliyet (Analiz Başına)

| Bileşen | Maliyet/analiz |
|---------|---------------|
| MediaPipe (self-host) | ~$0.001 |
| Claude Vision API | ~$0.02-0.05 |
| Mesaj gönderimi | ~$0.05-0.15 (WhatsApp) |
| **Toplam** | **~$0.07-0.20/analiz** |

### Fiyatlandırma Önerisi

| Plan | Fiyat | Analiz/ay | Dil |
|------|-------|-----------|-----|
| **Starter** | $79/ay | 200 | TR |
| **Growth** | $199/ay | 1,000 | TR + EN |
| **Pro** | $399/ay | 5,000 | Tüm diller |
| **Enterprise** | Custom | Sınırsız | Tüm diller + özel branding |

### ROI Hesabı (Orta Klinik)

```
Mevcut:  200 sorgu/ay → %15 randevu → 30 randevu → 20 tedavi × ₺15,000 = ₺300,000
AI ile:  200 sorgu/ay → %40 randevu → 80 randevu → 50 tedavi × ₺15,000 = ₺750,000
Fark:    +₺450,000/ay gelir artışı
AI maliyeti: ~₺10,000/ay (Pro plan)
ROI:     45x
```

---

## MVP Scope

| Bileşen | MVP'de Var | Sonrası |
|---------|------------|---------|
| Selfie → yüz analiz raporu (TR) | ✅ | |
| Tedavi önerisi + fiyat aralığı | ✅ | |
| Tenant tedavi kataloğu yönetimi | ✅ | |
| WhatsApp entegrasyonu | ✅ | |
| Randevu linki | ✅ | |
| Disclaimer (etik/yasal) | ✅ | |
| Çoklu dil (EN/AR) | | ✅ |
| Before/after simülasyon | | ✅ (Phase 2) |
| Video konsültasyon entegrasyonu | | ✅ |
| Gülüş tasarımı (diş klinikleri) | | ✅ |
| Saç analizi (saç ekimi klinikleri) | | ✅ |

---

## Genişleme Potansiyeli

| Genişleme | Sektör | Açıklama |
|-----------|--------|----------|
| **Gülüş Analizi** | Diş | Hasta gülümseme fotoğrafı → diş düzeltme/beyazlatma/kaplama önerisi |
| **Saç Analizi** | Saç ekimi | Hasta saç fotoğrafı → Norwood skalası tespiti → tedavi önerisi |
| **Vücut Analizi** | Estetik cerahi | Vücut fotoğrafı → liposuction/karın germe/meme estetiği önerisi |
| **Cilt Analizi** | Dermatolog | Cilt fotoğrafı → akne/leke/kırışıklık → tedavi önerisi |

---

## AHA Moments

| Kategori | AHA |
|----------|-----|
| **UX** | Hasta selfie gönderdi, 5 saniyede kişiselleştirilmiş analiz raporu geldi — "gerçek doktor baktı sandım" |
| **SPEED** | Gece 2'de selfie → gece 2'de rapor + randevu linki — rakip klinikler uyuyor |
| **SALES** | Lead→randevu dönüşümü %15 → %40+ — her analiz = sıcak lead |
| **SUPPORT** | Doktor, hastanın AI raporunu görerek konsültasyona hazırlanır — 10dk'lık konsültasyon 5dk'ya düşer |
| **RELIABILITY** | "Analiz bir şey önermediyse gerçekten gerekmiyordur" güveni — etik AI = marka güveni |

---

## Riskler

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| AI yanlış tedavi önerirse → yasal risk | 🔴 Kritik | Her zaman disclaimer, "ön değerlendirme", doktor onayı zorunlu |
| Fotoğraf kalitesi düşük (aydınlatma, açı) | 🟡 Orta | Kalite kontrolü + "lütfen iyi aydınlatılmış ortamda tekrar çekin" |
| Hasta beklentisi yanlış oluşur | 🟠 Yüksek | "Kesin sonuç değil, yol gösterici" vurgusu + doktor konsültasyon zorunlu |
| Etik: AI baskıcı satış aracı olur | 🟠 Yüksek | Agresiflik limiti: max 3 öneri, "gereksiz" tedavi önerme |
| KVKK: Yüz fotoğrafı = biyometrik veri | 🟠 Yüksek | Açık rıza, analiz sonrası silme opsiyonu, şifreleme |

---

## Roadmap Referansı

> **Phase:** 3D (Face Analysis AI) — [phases/phase-3d.md](phases/phase-3d.md)
> **GR:** GR-3D.1 ~ GR-3D.5
> **Yeni Servis:** `Invekto.FaceAnalysis` (port 7110)
> **Entegre:** 2026-02-14

---

## Sonraki Adımlar

- [x] Q karar: Yeni Phase 3D olarak ayrı phase oluşturuldu (GR-3D.1-3D.5)
- [ ] Etik danışma: Türkiye'de AI estetik değerlendirme yasal çerçevesi
- [ ] PoC: MediaPipe + Claude Vision ile 50 test fotoğraf analizi
- [ ] Klinik feedback: 2-3 estetik cerrahla "bu rapor mantıklı mı?" testi
- [ ] Before/after simülasyon teknolojisi araştırması
