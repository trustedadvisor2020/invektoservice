# Sesli Mesaj AI — WhatsApp Sesli Mesaj Transkripsiyon + Intent

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm) + pazar araştırması
> **Durum:** FİKİR AŞAMASI
> **Sektör:** TÜM (e-ticaret, diş, estetik, otel — evrensel)
> **Bağımsız SaaS Potansiyeli:** Evet — herhangi bir WhatsApp Business kullanan işletme

---

## Problem

Türkiye'de WhatsApp kullanıcılarının büyük çoğunluğu **yazı yazmak yerine sesli mesaj gönderiyor**. Bu özellikle:

- **E-ticaret müşterileri:** "Ya ben geçen hafta sipariş verdim şey vardı ya kırmızı elbise..."
- **Klinik hastaları:** "Doktor bey merhabalar ben geçen implant yaptırmıştım da şimdi biraz sızı var..."
- **Medikal turizm hastaları:** 🎤 Arapça/Rusça/İngilizce uzun sesli mesajlar
- **Otel müşterileri:** "Merhaba şey sormak istiyorum deniz manzaralı odanız var mı..."

### Günlük Gerçeklik

| Metrik | Değer |
|--------|-------|
| Ortalama sesli mesaj/gün (e-ticaret) | 50-200 |
| Ortalama sesli mesaj/gün (klinik) | 20-80 |
| Ortalama sesli mesaj süresi | 15-60 saniye |
| Dinleme + anlama + cevap süresi | 3-5 dakika/mesaj |
| Günlük kayıp zaman (50 mesaj) | **2.5-4 saat** |
| Dinlenemeyen mesajlar (yoğun saatlerde) | %30-50 |

### Mevcut Çözüm Yok

| Rakip | Durumu |
|-------|--------|
| WhatsApp kendi transkript özelliği | Sadece dinleyiciye — işletme otomasyonuna bağlanmıyor |
| Genel STT servisleri (Google, Whisper) | Ham transkript veriyor, intent çıkarmıyor, CRM'e bağlanmıyor |
| Türk CRM'ler (rakipler) | **Hiçbirinde sesli mesaj desteği yok** |
| Uluslararası CRM'ler (Zendesk, Intercom) | WhatsApp sesli mesaj özellikle ele alınmıyor |

**Boşluk:** Sesli mesajı dinle → anlam çıkar → otomasyona bağla. Bu pipeline'ı hiçbir Türk CRM yapmıyor.

---

## Çözüm

**Voice Message AI:** WhatsApp'tan gelen sesli mesajları otomatik transkript et → intent çıkar → mevcut otomasyon akışlarına bağla. Müşteri sesli mesaj göndermiş gibi değil, yazılı mesaj göndermiş gibi davran.

### Müşteri Deneyimi

```
Müşteri: [🎤 45 saniyelik sesli mesaj]
         "Ya ben geçen hafta sipariş verdim kırmızı elbise vardı ya
          hani beden olarak M almıştım ama büyük geldi acaba
          değiştirebilir miyiz S bedene böyle iade falan gerekiyor mu
          bir de ne kadar sürer yani kargo falan..."

━━━ AI Pipeline (3-5 saniye) ━━━

Transkript: ✅ (yukarıdaki metin)
Dil: Türkçe
Intent: beden_degisimi (güven: 0.92)
Alt intent: kargo_süresi_sorgusu
Duygu: nötr (şikayet değil, bilgi talebi)
Ürün: kırmızı elbise, M beden

━━━ Otomatik Cevap (2 saniye) ━━━

Bot: "Merhaba! 😊 Elbisenizdeki beden değişimini hemen yapabiliriz.

     📦 Değişim süreci:
     • S beden stoğumuzda ✅ mevcut
     • Mevcut ürünü kargo ile gönderin (ücretsiz iade etiketi SMS'le gelecek)
     • Yeni ürün 2-3 iş günü içinde kapınızda

     Devam edelim mi?"
```

### Medikal Turizm Senaryosu (Arapça)

```
Hasta (Dubai): [🎤 2 dakikalık Arapça sesli mesaj]
"أريد عملية تجميل الأنف، كم التكلفة تقريباً وهل تشمل الفندق..."

━━━ AI Pipeline (5 saniye) ━━━

Transkript: ✅ (Arapça metin)
Çeviri: "Burun estetiği istiyorum, yaklaşık maliyet nedir ve otel dahil mi..."
Dil: Arapça
Intent: tedavi_fiyat_sorgusu (güven: 0.95)
İşlem: rhinoplasty
Alt sorgu: konaklama_dahil_mi

━━━ Otomatik Arapça Cevap (3 saniye) ━━━

"مرحباً! شكراً لتواصلك معنا 💫

 عملية تجميل الأنف:
 💰 السعر: ٣,٠٠٠ - ٥,٠٠٠ يورو
 🏨 الفندق: ٥ ليالي مشمولة
 🚗 النقل من المطار: مشمول

 📅 أقرب موعد: ١٥ مارس
 📹 هل تريد استشارة فيديو مجانية؟"
```

---

## Neden Bağımsız Mikro Servis?

| Soru | Cevap |
|------|-------|
| Invekto'ya bağımlı mı? | **HAYIR** — API ile herhangi bir CRM/chatbot kullanabilir |
| Invekto ile nasıl çalışır? | Middleware olarak: mesaj geldi → sesli mi kontrol → transkript + intent → mevcut pipeline'a aktar |
| Başka platformlar? | Herhangi bir WhatsApp Business API kullanan sistem |
| Gelir modeli? | Ayrı SaaS veya Invekto premium add-on |

---

## Mimari

```
┌─────────────────────────────────────────────────────────────┐
│                    INPUT LAYER                               │
│                                                             │
│  WhatsApp Business API → Sesli mesaj algılama               │
│  (media type: audio/ogg)                                    │
│                                                             │
│  Ses dosyası indir → OGG/MP3 → WAV dönüşümü                │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    TRANSCRIPTION ENGINE                       │
│                                                             │
│  ┌──────────────────────────────────┐                       │
│  │ Speech-to-Text (STT)            │                       │
│  │ ─────────────────────────────── │                       │
│  │ • Whisper (OpenAI) — multi-lang │                       │
│  │ • Dil algılama (TR/EN/AR/RU/DE) │                       │
│  │ • Gürültü filtreleme            │                       │
│  │ • Konuşmacı tonu analizi        │                       │
│  └────────────────┬─────────────────┘                       │
│                   ▼                                          │
│  ┌──────────────────────────────────┐                       │
│  │ Translation (opsiyonel)          │                       │
│  │ ─────────────────────────────── │                       │
│  │ • AR/RU/DE → TR çeviri          │                       │
│  │ • Orijinal + çeviri birlikte    │                       │
│  └────────────────┬─────────────────┘                       │
└───────────────────┼─────────────────────────────────────────┘
                    ▼
┌─────────────────────────────────────────────────────────────┐
│                    INTELLIGENCE LAYER                         │
│                                                             │
│  ┌──────────────────────────────────┐                       │
│  │ Intent Extraction                │                       │
│  │ ─────────────────────────────── │                       │
│  │ • Mevcut AgentAI intent modeli   │                       │
│  │ • Sesli mesaj-özel context       │                       │
│  │   (daha uzun, daha belirsiz,     │                       │
│  │    birden fazla konu)            │                       │
│  │ • Multi-intent algılama          │                       │
│  │   ("hem iade hem kargo sorgusu") │                       │
│  └──────────────────┬───────────────┘                       │
│                     ▼                                        │
│  ┌──────────────────────────────────┐                       │
│  │ Sentiment Analysis               │                       │
│  │ ─────────────────────────────── │                       │
│  │ • Ses tonundan duygu algılama    │                       │
│  │   (kızgın/memnun/nötr/acil)     │                       │
│  │ • Metin sentiment + ses tonu     │                       │
│  │   = combo skor                   │                       │
│  │ • Acil/kızgın → öncelikli queue  │                       │
│  └──────────────────┬───────────────┘                       │
└──────────────────────┼──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    OUTPUT LAYER                               │
│                                                             │
│  Çıktı → Mevcut Invekto pipeline'ına aktar:                │
│  • Automation (chatbot flow tetikle)                        │
│  • AgentAI (agent'a transkript + önerilen cevap göster)     │
│  • Outbound (follow-up tetikle)                             │
│  • CRM (müşteri kartına not ekle)                           │
│                                                             │
│  Veya: Bağımsız API response                                │
│  { transcript, language, intent, sentiment, suggested_reply }│
└─────────────────────────────────────────────────────────────┘
```

---

## Teknik Seçenekler

### Speech-to-Text (STT)

| Seçenek | Artı | Eksi | Maliyet |
|---------|------|------|---------|
| **OpenAI Whisper (self-host)** | Ücretsiz, çok dilli, doğruluk yüksek | GPU gerekli | GPU sunucu maliyeti |
| **OpenAI Whisper API** | Kolay, hızlı, bakım yok | API bağımlılığı | $0.006/dakika |
| **Google Cloud STT** | Türkçe iyi, streaming desteği | Pahalı | $0.016/dakika |
| **Azure Speech** | Türkçe + Arapça iyi | Karmaşık kurulum | $0.016/dakika |
| **Deepgram** | Hızlı, ucuz, real-time | Türkçe desteği sınırlı | $0.0043/dakika |

**Önerilen: OpenAI Whisper API (başlangıç) → Self-host (ölçekte)**
- Whisper large-v3 Türkçe'de %95+ doğruluk
- Arapça, Rusça, Almanca, İngilizce de destekliyor
- API ile başla ($0.006/dk = 1000 mesaj × 30sn ≈ $3/gün)
- Ölçeklenince self-host → maliyet %90 düşer

### Intent Extraction

| Seçenek | Açıklama |
|---------|----------|
| **Mevcut AgentAI** | Transkript → AgentAI intent modeline gönder (aynı pipeline) |
| **LLM bazlı** | Claude Haiku ile transkriptten intent + entity çıkarma |
| **Hibrit** | Whisper → transkript, sonra AgentAI intent modeli |

**Önerilen: Mevcut AgentAI pipeline'ı kullan**
- Sesli mesaj → transkript → yazılı mesaj gibi davran → mevcut intent modeli çalışır
- Ekstra model eğitimi gerekmez
- Sadece multi-intent desteği ekle (sesli mesajlar genelde birden fazla konu içerir)

---

## Maliyet Analizi

### Birim Maliyet (Mesaj Başına)

| Bileşen | Maliyet/mesaj (30sn) |
|---------|---------------------|
| Whisper API | $0.003 |
| Intent extraction (AgentAI) | ~$0.001 |
| Çeviri (gerekirse) | ~$0.002 |
| **Toplam** | **~$0.006/mesaj** |

### Aylık Maliyet (Tenant Başına)

| Kullanım | Mesaj/ay | Maliyet/ay |
|----------|----------|------------|
| Küçük (butik) | 500 | $3 |
| Orta (e-ticaret) | 3,000 | $18 |
| Büyük (klinik zinciri) | 10,000 | $60 |

### Fiyatlandırma Önerisi

| Plan | Fiyat | Mesaj/ay | Dil |
|------|-------|----------|-----|
| **Starter** | $19/ay | 1,000 | TR only |
| **Growth** | $49/ay | 5,000 | TR + EN |
| **Pro** | $99/ay | 15,000 | Tüm diller (TR/EN/AR/RU/DE) |
| **Enterprise** | Custom | Sınırsız | Tüm diller + özel model |

---

## MVP Scope

| Bileşen | MVP'de Var | Sonrası |
|---------|------------|---------|
| Whisper transkripsiyon (TR) | ✅ | |
| Intent çıkarma (mevcut AgentAI) | ✅ | |
| Invekto entegrasyonu (transkript → pipeline) | ✅ | |
| Agent ekranında transkript gösterme | ✅ | |
| Çoklu dil (EN/AR/RU/DE) | | ✅ |
| Ses tonu sentiment analizi | | ✅ |
| Multi-intent algılama | | ✅ |
| Otomatik çeviri (AR→TR) | | ✅ |
| Bağımsız API (dış müşteriler) | | ✅ |

---

## Invekto Entegrasyon Noktaları

```
Mevcut Invekto Flow:
  Müşteri yazılı mesaj gönderir → Intent algıla → Chatbot/Agent

Voice AI ile:
  Müşteri sesli mesaj gönderir
    → [YENİ] Ses algıla → Whisper transkript → Intent algıla
    → [MEVCUT] Chatbot/Agent (aynı pipeline)
```

**Değişecek yerler:**
1. **Automation (:7108):** Sesli mesaj trigger type eklenir
2. **AgentAI (:7105):** Transkript intent modeline gider (mevcut pipeline)
3. **Agent UI:** Sesli mesajın yanında transkript + intent gösterilir
4. **Flow Builder:** "Sesli mesaj geldi" trigger node'u eklenir

---

## AHA Moments

| Kategori | AHA |
|----------|-----|
| **UX** | Agent ekranında sesli mesajın yanında anında transkript + intent etiketi görünür — dinlemeye gerek yok |
| **SPEED** | Müşteri 45sn sesli mesaj gönderiyor, 5sn içinde otomatik cevap alıyor — "yazılı mesaj gönderseydim bile bu kadar hızlı cevap gelmezdi" |
| **RELIABILITY** | Arapça sesli mesaj → Türkçe çeviri + Arapça otomatik cevap = gece 2'de bile çalışıyor |
| **SALES** | Sesli mesajdaki ses tonundan kızgınlık algılama → yorum yazılmadan müdahale |
| **SUPPORT** | "En çok sesli mesaj gönderen saatler" analizi → personel planlaması |

---

## Riskler

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| Gürültülü ortam → düşük transkript kalitesi | 🟡 Orta | Whisper noise-robust, güven skoru düşükse "anlayamadım, yazılı gönderir misiniz?" |
| Ağız/lehçe farkları (Karadeniz, Güneydoğu) | 🟡 Orta | Whisper large-v3 lehçelerde iyi, zamanla fine-tune |
| WhatsApp sesli mesaj formatı (opus/ogg) | 🟢 Düşük | FFmpeg ile WAV'a çevir, standart pipeline |
| Maliyet ölçekte artabilir | 🟡 Orta | Self-host Whisper ile %90 maliyet düşüşü |
| Gizlilik endişesi (ses kaydı saklanıyor mu?) | 🟠 Yüksek | Transkript sonrası ses silinir, KVKK uyumlu |

---

## Roadmap Referansı

> **Phase:** 3B (Niche Derinleştirme) — [phases/phase-3b.md](phases/phase-3b.md)
> **GR:** GR-3.23 Voice Message AI
> **Bölüm:** Evrensel AI (v4.5)
> **Entegre:** 2026-02-14

---

## Sonraki Adımlar

- [x] Q karar: Phase 3B — Evrensel AI olarak entegre edildi (GR-3.23)
- [ ] PoC: Whisper API ile 100 gerçek sesli mesaj transkript testi
- [ ] Doğruluk ölçümü: Türkçe + Arapça transkript kalitesi
- [ ] Mevcut AgentAI intent modeli ile sesli mesaj transkriptlerinin uyumu testi
- [ ] Invekto ana uygulama: Sesli mesaj webhook desteği var mı kontrol
