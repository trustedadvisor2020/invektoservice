# Invekto Platform Roadmap v3.1

> Kaynak: `ideas/fullidea.md` + Hormozi değer denklemi + 75 senaryo analizi (25 e-ticaret + 25 diş + 25 klinik/estetik)
> Tarih: 2026-02-06
> Durum: DRAFT — Q onayı bekleniyor
> Felsefe: **Revenue first. Dual niche. Outbound + Inbound. Altyapı ihtiyaç oldukça.**

---

## Dosya Yapısı

| Dosya | İçerik | Satır |
|-------|--------|-------|
| **roadmap.md** *(bu dosya)* | Navigator — strateji, mimari, özet | ~250 |
| [roadmap-phases.md](roadmap-phases.md) | Phase 0-7 detaylı plan, DB tabloları, başarı kriterleri | ~960 |
| [roadmap-scenarios.md](roadmap-scenarios.md) | 75 senaryo (25 e-ticaret + 25 diş + 25 klinik/estetik) + Outbound Engine gereksinimleri | ~100 |
| [roadmap-reviews.md](roadmap-reviews.md) | 3 uzman review (Dunford, Lemkin, Lenny) + aksiyonlar | ~130 |

---

## Temel Prensip

```
ESKİ (Mühendis kafası):   Altyapı → Altyapı → AI → Entegrasyon → Revenue
YENİ (Hormozi kafası):    Müşteri → 1 Çözüm → Revenue → Ölçekle → Altyapıyı ekle
```

Her phase'in 3 soruya cevabı olmalı:
1. **Müşteri ne kazanıyor?** (sonuç, özellik değil)
2. **Para ne zaman geliyor?** (revenue milestone)
3. **Minimal ne yapılmalı?** (overengineering yok)

---

## Mevcut Durum (Stage-0)

| Bileşen | Durum |
|---------|-------|
| `Invekto.Backend` | Gateway, Ops Dashboard, port 5000 |
| `Invekto.ChatAnalysis` | Claude Haiku ile 15 kriterli chat analizi, port 7101 |
| `Invekto.Shared` | DTOs, logging, error codes |
| Dashboard | React + Vite — health monitoring, log viewer |
| Veritabanı | YOK |
| Auth | YOK — sadece Ops dashboard'da basic auth |
| Multi-tenant | Header var (`X-Tenant-Id`) ama enforcement yok |
| Müşteri sayısı | 0 |
| MRR | 0 TL |

---

## Positioning (Tek Cümle — Dunford Kuralı)

> **Invekto:** Trendyol ve Hepsiburada satıcıları için WhatsApp üzerinden sipariş sonrası yükü
> otomatik kapatan AI. Destek maliyetini düşürür, ekstra işe alıma gerek kalmaz.
>
> **English:** Invekto helps WhatsApp-based sellers automatically resolve support and close sales using AI agents.
>
> Kaynak: 75 senaryo analizi

| Yapma | Yap |
|-------|-----|
| "AI-Powered Revenue & Support OS" | "Marketplace satıcıları için WhatsApp destek otomasyonu" |
| "Omnichannel CRM platform" | "Kargo ve iade sorularını otomatik cevapla" |
| 4 kategoriye aynı anda gir | 1 kategoride kanıtlan, sonra genişle |

### 3 Niche Paralel Giriş (Q Kararı — 2026-02-08)

> **Karar:** 3 niche'e aynı anda çıkılacak. Sağlık sektörü Phase 3-4'e ertelenmeyecek.
> Reklam ve web siteleri sektör bazlı ayrılacak. Ortak altyapı tek, offer'lar ayrı.

#### Üst Şemsiye Positioning

> **Invekto:** WhatsApp'tan gelen müşteri mesajlarını AI ile otomatik yöneten iş asistanı.
>
> - **E-ticaret satıcıları için:** Kargo ve iade sorularını otomatik çözer, temsilci maliyetini düşürür.
> - **Diş klinikleri için:** Fiyat sorularını randevuya çevirir, no-show'u %60 azaltır.
> - **Estetik klinikleri için:** Lead'leri hastaya dönüştürür, medikal turizmi ölçekler.

#### Ortak Altyapı (3 Sektörde %95+ Kullanım — 75 Senaryo Analizi)

| Capability | E-ticaret | Diş | Estetik | Toplam |
|------------|-----------|-----|---------|--------|
| **C8: Agent Assist** | 25/25 | 25/25 | 25/25 | **75/75 (%100)** |
| **C3: Templates** | 25/25 | 24/25 | 24/25 | **73/75 (%97)** |
| **C1: Unified Inbox** | 25/25 | 24/25 | 23/25 | **72/75 (%96)** |
| **C2: Routing** | 25/25 | 24/25 | 22/25 | **71/75 (%95)** |

> Bu 4 capability = ürünün omurgası. Sektör farketmez, tek codebase, farklı config.

#### Niche-Özel Capability'ler

| Capability | Hangi Niche | Neden |
|------------|------------|-------|
| C7: Knowledge/RAG | E-ticaret + Diş | Bilgi tutarlılığı (ürün bilgisi / tedavi bilgisi) |
| C11: E-commerce Integrations | Sadece E-ticaret | Trendyol/HB API |
| C10: Revenue Agent | Estetik ağırlıklı | Ödeme/depozit/lead dönüşümü |
| C12: Ads Attribution | Sadece Estetik | Click-to-WhatsApp kampanya tracking |
| C4: Reporting | Estetik ağırlıklı | Conversion takibi |
| C5/C6: Security | Diş + Estetik | KVKK sağlık verisi |

#### 3 Ayrı Offer (Tek Platform)

**OFFER 1: Invekto for Sellers (E-ticaret)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Kargo/iade sorularının %50'sini otomatik cevapla" |
| Karar verici | Marketplace satıcısı (Mehmet) |
| Fiyat | 3.000-5.000 TL/ay |
| Garanti | 30 günde %50 oto-cevap yoksa 2. ay ücretsiz |
| Niche özel | C11 (Trendyol/HB API) + C7 (Knowledge) |

**OFFER 2: Invekto for Dental (Diş Klinikleri)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Fiyat sorularını randevuya çevir, no-show'u %60 azalt" |
| Karar verici | Klinik sahibi (Dr. Burak) |
| Fiyat | 7.500 TL/ay |
| Garanti | 30 günde no-show düşmezse 2. ay ücretsiz |
| Niche özel | Randevu motoru + No-show önleme + C7 (Knowledge) + C5/C6 (KVKK) |

**OFFER 3: Invekto for Clinics (Estetik)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Lead'leri hastaya dönüştür, medikal turizmi ölçekle" |
| Karar verici | Klinik sahibi (Dr. Selin) |
| Fiyat | 15.000-25.000 TL/ay |
| Garanti | 30 günde randevu dönüşümü artmazsa 2. ay ücretsiz |
| Niche özel | C10 (Revenue) + C12 (Ads Attribution) + C4 (Reporting) + Multi-lang |

#### Web & Reklam Stratejisi (3 Niche)

| Kanal | Yapı |
|-------|------|
| Landing page | 3 ayrı sayfa: /sellers, /dental, /clinics |
| Reklam | Sektör bazlı ayrı kampanyalar, farklı ağrı noktaları, farklı hedef kitle |
| Demo | Sektöre özel demo flow (e-ticaret: kargo, diş: randevu, estetik: lead) |
| Case study | Her sektörden ayrı case study hedeflenir |
| LinkedIn / Sosyal | Sektör bazlı content (satıcı grupları vs sağlık profesyoneli grupları) |
| SEO | 3 farklı anahtar kelime seti (WhatsApp otomasyon + sektör adı) |

#### Niche Bazlı Satış Dili Karşılaştırması

| | E-ticaret | Diş | Estetik |
|--|-----------|-----|---------|
| **Ağrı** | "Kargom nerede" mesaj yükü | "Fiyat ne kadar?" + no-show | Lead dönüşümü + medikal turizm |
| **Sonuç** | Temsilci maliyeti %40 düşer | No-show %60 azalır | Randevu dönüşümü %40 artar |
| **ROI dili** | 25.000 TL/ay tasarruf | Kayıp randevu geliri geri kazanılır | Lead başına maliyet düşer |
| **İlk AHA** | Kargo sorusu otomatik cevaplandı | Fiyat sorusu randevuya döndü | Instagram lead'i WhatsApp'a geçti |
| **Karar verici** | Satıcı / E-ticaret müdürü | Klinik sahibi / Diş hekimi | Klinik sahibi / Operasyon müdürü |
| **Satın alma süreci** | Hızlı (1 hafta) | Orta (2-3 hafta) | Orta-Yavaş (3-4 hafta) |

---

## Product Story

**Invekto = Marketplace satıcıları için WhatsApp sipariş sonrası destek AI'ı**

*"Kargo ve iade sorularını otomatik cevapla. Temsilci işe alma. Invekto çalışsın."*

### Müşteri Avatarları (6 Persona)

#### [E1] Mehmet - Trendyol/HB D2C Satıcı (Primary)
```
İş: Trendyol'da günde 200+ sipariş yöneten e-ticaret satıcısı
Ekip: 3 WhatsApp temsilcisi
Günlük ağrı:
  → Günde 150+ "kargom nerede" mesajı
  → Temsilciler Trendyol panelinden sipariş arıyor, WhatsApp'a dönüyor — 5dk/mesaj
  → İade soruları karışıyor, yanlış bilgi veriliyor
  → Lead'ler Instagram'dan geliyor, takip edilmiyor
  → Sepet terk edenlere ulaşamıyor
Maliyet: 3 temsilci × 25.000 TL = 75.000 TL/ay

Invekto ile:
  → Kargo soruları otomatik cevaplanıyor
  → Temsilci 5dk yerine 30sn'de cevap veriyor
  → Lead'ler otomatik skorlanıyor
  → Sepet terk edenlere otomatik mesaj gidiyor
Tasarruf: 1 temsilci azaltma = 25.000 TL/ay
Invekto fiyatı: 5.000 TL/ay → 5x ROI
```

#### [E2] Ayşe - Shopify/WooCommerce D2C (5-20 kişi)
```
İş: Kendi sitesinden satış yapan orta ölçekli e-ticaret
Ekip: 5-20 kişi
Fark: Marketplace'e bağımlı değil, kendi müşteri tabanı var
```

#### [D1] Dr. Burak - Diş Klinik Sahibi
```
İş: 2-3 şubeli diş kliniği, günde 60+ WhatsApp mesajı
Ekip: 2 sekreter
Günlük ağrı:
  → Günde 30+ "fiyat ne kadar" mesajı
  → No-show oranı %25
  → Tedavi sonrası takip unutuluyor
```

#### [D2] Elif - Diş Kliniği Ön Büro Koordinatörü
```
İş: Tek şubeli klinikte randevu ve hasta iletişimi
Ekip: 1 sekreter yardımcısı
Fark: Operasyonel verimlilik odaklı
```

#### [A1] Dr. Selin - Estetik Klinik Sahibi
```
İş: Yüksek fiyatlı estetik işlemler (botox, dolgu, lazer)
Ekip: 3 kişi (sekreter + hasta koordinatörü + medikal turizm sorumlusu)
Günlük ağrı:
  → Yurtdışından hasta İngilizce yazıyor
  → Fiyat soruları hassas (işleme göre değişken)
```

#### [A2] Zeynep - Estetik Klinik Operasyon + Satış Sorumlusu
```
İş: Lead takibi, randevu dönüşümü, medikal turizm koordinasyonu
Ekip: 1-2 asistan
Fark: Satış odaklı + multi-language ihtiyacı
```

### Grand Slam Offer (E-ticaret)

```
"WhatsApp'tan gelen kargo ve iade sorularınızın %50'sini
 ilk 30 günde otomatik cevaplayacağız.

 Cevaplayamazsak 2. ay ücretsiz.

 İlk 10 Trendyol satıcısına özel lansman fiyatı."

Değer Denklemi:
  Hayalin Sonucu: Temsilci maliyeti %40 düşer         → YÜKSEK
  Gerçekleşme İhtimali: 30 gün garanti + case study    → YÜKSEK
  Zaman Gecikmesi: Hemen kurulum, 1 hafta sonuç        → DÜŞÜK
  Harcanan Efor: Biz kuruyoruz, siz izliyorsunuz       → DÜŞÜK

  Değer = (Yüksek × Yüksek) / (Düşük × Düşük) = ÇOK YÜKSEK
```

### Offer Stratejisi (Hormozi Kuralı)

**Feature satma, sonuç sat.**

| ❌ Feature Dili | ✅ Sonuç Dili |
|----------------|--------------|
| "AI-powered inbox" | "Kargo sorularının %50'si otomatik cevaplanır" |
| "Knowledge base entegrasyonu" | "Yanlış cevap oranı %80 düşer" |
| "Outbound engine" | "No-show oranı %60 azalır" |
| "Revenue agent" | "Sepet terk edenlerin %10'u geri döner" |

**Hormozi 4 Bileşen:**
- **Setup:** Biz kuruyoruz (white-glove onboarding)
- **Garanti:** 30 günde sonuç yoksa 2. ay ücretsiz
- **Kıtlık:** İlk 10 müşteriye özel fiyat
- **Risk reversal:** Para iade garantisi

### İkinci Niche: Sağlık Sektörü (Phase 3-4'te Giriş)

```
İsim: Dr. Ayşe
İş: 3 şubeli diş kliniği, günde 80+ WhatsApp mesajı
Ekip: 2 sekreter + 1 hasta koordinatörü
Günlük ağrı:
  → Günde 30+ "fiyat ne kadar" mesajı — sekreter telefonu bırakıp cevaplıyor
  → Randevu alan hastaların %25'i gelmiyor (no-show)
  → Tedavi sonrası kontrol hatırlatması unutuluyor → komplikasyon riski
  → Google'da yorum bırakan hasta %3 — rakip klinik daha çok yorum topluyor
  → Yurtdışından gelen hastalar İngilizce yazıyor, sekreter cevap veremiyor
Maliyet: 2 sekreter × 20.000 TL + kayıp randevu geliri = ~95.000 TL/ay

Invekto ile:
  → Fiyat soruları otomatik cevaplanıyor, %40'ı randevuya dönüyor
  → No-show %25 → %8'e düşüyor (otomatik hatırlatma)
  → Tedavi sonrası takip otomatik → hasta memnuniyeti artıyor
  → Google yorum oranı %3 → %15+ (otomatik rica)
  → İngilizce hastalar AI ile anında cevap alıyor
Tasarruf + ek gelir: ~55.000 TL/ay
Invekto fiyatı: 7.500-15.000 TL/ay → 4-7x ROI
```

### Grand Slam Offer (Sağlık)

```
"Kliniğinize WhatsApp'tan gelen fiyat sorularının %40'ını
 randevuya çevireceğiz. No-show oranınızı %60 düşüreceğiz.

 30 günde sonuç yoksa 2. ay ücretsiz.

 İlk 5 kliniğe özel fiyat."

Değer Denklemi:
  Hayalin Sonucu: Daha çok randevu, daha az no-show     → ÇOK YÜKSEK
  Gerçekleşme İhtimali: 30 gün garanti + veri ile kanıt  → YÜKSEK
  Zaman Gecikmesi: 1 hafta kurulum, 2 hafta sonuç        → DÜŞÜK
  Harcanan Efor: Biz kuruyoruz, siz izliyorsunuz          → DÜŞÜK

  Değer = (Çok Yüksek × Yüksek) / (Düşük × Düşük) = MUAZZAM
```

> **Neden sağlık?** Aynı altyapı (WhatsApp AI + Outbound), 3-5x daha yüksek ARPU.
> E-ticaret niche'i kanıtlandıktan sonra Phase 3-4'te giriş.

> **⚡ GÜNCELLENDİ (2026-02-08):** Sağlık sektörü artık Phase 0'dan itibaren paralel giriş.
> Erteleme kararı kaldırıldı. 3 niche aynı anda validasyon + satış. Detay: yukarıdaki
> "3 Niche Paralel Giriş" bölümü.

---

## Senaryo Portföyü (Özet)

> Detay: bkz [roadmap-scenarios.md](roadmap-scenarios.md)

**75 senaryo**, 3 sektör, 6 persona, toplam aylık etki potansiyeli hesaplanıyor:

| Sektör | Senaryolar | Personalar |
|--------|------------|------------|
| E-ticaret | 25 senaryo | [E1] Mehmet, [E2] Ayşe |
| Diş Klinikleri | 25 senaryo | [D1] Dr. Burak, [D2] Elif |
| Klinik/Estetik | 25 senaryo | [A1] Dr. Selin, [A2] Zeynep |

**Kritik bulgu:** Senaryoların çoğu **Outbound Engine** gerektiriyor. Bu olmadan gelir potansiyelinin büyük kısmı kilitli. Outbound Engine gereksinimleri ve detaylı senaryo tabloları: [roadmap-scenarios.md](roadmap-scenarios.md)

---

## Phase Planı (Özet)

> Detay: bkz [roadmap-phases.md](roadmap-phases.md)

| Phase | Hafta | Odak | MRR Hedefi (Toplam) | Müşteri |
|-------|-------|------|---------------------|---------|
| **0** | 1-2 | Müşteri validasyonu + Grand Slam Offer | 0 | 1 ödeme alan |
| **1** | 3-6 | İlk müşterinin problemini çöz (kargo) | 3-5K | 1 aktif |
| **2** | 7-12 | 5-10 müşteriye ölçekle + Outbound v1 | 15-50K | 5-10 |
| **3** | 13-20 | Knowledge + AgentAI + Outbound v2 + Multi-lang | 50-130K | 15-25 + pilot klinik |
| **4** | 21-28 | Auth + Audit + Sağlık niche girişi | 150-375K | 25-40 + 3-5 klinik |
| **5** | 29-36 | Revenue Agent + Sağlık genişleme | 360-650K | 40-60 + 8-15 klinik |
| **6** | 37-48 | Analytics + Ölçek | 650K-1.3M | 60-100 + 15-30 klinik |
| **7** | 49+ | Omnichannel + Global | 1M+ | 100+ |

Her phase'in detaylı adımları, DB tabloları, başarı kriterleri ve geçiş şartları: [roadmap-phases.md](roadmap-phases.md)

### 3 Niche Paralel Phase Planı (Güncelleme — 2026-02-08)

| Phase | Hafta | E-ticaret | Diş Klinikleri | Estetik Klinikleri |
|-------|-------|-----------|----------------|-------------------|
| **0** | 1-2 | 10 satıcıyla görüş + offer | 10 diş kliniğiyle görüş + offer | 10 estetik kliniğiyle görüş + offer |
| **1** | 3-6 | İlk satıcı: kargo oto-cevap | İlk klinik: fiyat→randevu + no-show | İlk klinik: lead→WhatsApp + randevu |
| **2** | 7-12 | 5-10 satıcı + Outbound v1 | 3-5 klinik + hatırlatma engine | 3-5 klinik + ads attribution |
| **3** | 13-20 | Knowledge + Agent AI | Knowledge + tedavi bilgisi | Revenue Agent + multi-lang |
| **4** | 21-28 | Auth + Audit + Enterprise | Auth + KVKK compliance | Auth + medikal turizm |
| **5** | 29-36 | Revenue Agent + Cart Recovery | Yorum motoru + referans | Tam medikal turizm + AR |
| **6** | 37-48 | Analytics + Ölçek | Analytics + Ölçek | Analytics + Ölçek |

**3 Niche Paralel MRR Hedefi:**

```
Hafta:   1-2       3-6        7-12         13-20        21-28         29-36        37-48
          │          │           │             │             │             │            │
E-com:    0       3-5K       15-50K       50-125K      125-300K      300-500K     500K-1M
Diş:      0       7.5K       22-37K       50-100K       75-150K      150-225K     225-450K
Estetik:  0      15-25K      45-75K       75-150K      150-250K      250-375K     375-750K
TOPLAM:   0      25-37K      82-162K     175-375K      350-700K      700K-1.1M    1.1M-2.2M
          │          │           │             │             │             │            │
E-com:    0          1         5-10         15-25         25-40         40-60       60-100
Diş:      0          1          3-5          7-13         10-20         20-30       30-60
Estetik:  0          1          3-5          5-10         10-17         17-25       25-50
```

> **Not:** Mevcut "Revenue Timeline (Dual Niche)" tablosu (aşağıda) e-ticaret + sağlık (birleşik) olarak
> kalıyor. Bu tablo 3 niche'i ayrı ayrı gösteriyor. Gerçek sayılar müşteri validasyonuna göre ayarlanacak.

---

## Mikro Servis Haritası (Evrimsel — İhtiyaç Oldukça)

| # | Servis | Doğduğu Phase | Port | Tetikleyici |
|---|--------|---------------|------|-------------|
| 0 | `Invekto.Backend` | Mevcut | 5000 | Zaten var |
| 0 | `Invekto.ChatAnalysis` | Mevcut | 7101 | Zaten var |
| 1 | `Invekto.Integrations` | Phase 1 | 7106 | İlk müşterinin Trendyol ihtiyacı |
| 2 | `Invekto.Outbound` | Phase 2-3 | 7107 | Proaktif mesaj ihtiyacı (7/10 senaryo) |
| 3 | `Invekto.Knowledge` | Phase 3 | 7104 | "AI yanlış cevap veriyor" şikayeti |
| 4 | `Invekto.AgentAI` | Phase 3 | 7105 | Auto-resolution ihtiyacı |
| 5 | `Invekto.Auth` | Phase 4 | 7102 | 10+ müşteri = multi-tenant zorunlu |
| 6 | `Invekto.Audit` | Phase 4 | 7103 | Kurumsal müşteri talebi |

> **Outbound Engine** tüm roadmap'in en kritik yeni bileşeni. 10 senaryodan 7'si outbound gerektiriyor.

---

## Veritabanı Stratejisi

| Servis | DB | Ne Zaman |
|--------|-----|----------|
| Integrations | PostgreSQL | Phase 1 — ilk müşteriyle birlikte |
| Outbound | PostgreSQL | Phase 2-3 — proaktif mesaj başlayınca |
| Knowledge | PostgreSQL + pgvector | Phase 3 — RAG devreye girerken |
| Auth | PostgreSQL | Phase 4 — multi-tenant zorunlu olunca |
| Audit | PostgreSQL | Phase 4 — kurumsal müşteri isteyince |
| AgentAI | Kendi DB'si yok | Knowledge + Integrations'tan veri çeker |

> Phase 1'de tek bir PostgreSQL instance yeterli. Servis başına ayrı DB, Phase 4'ten sonra.

---

## Servis Bağımlılık Haritası (Evrimsel)

```
Phase 1-2:                    Phase 3+:                     Phase 4+:

┌──────────┐              ┌──────────┐                ┌──────────┐
│ Backend  │              │ Backend  │                │ Backend  │
│  :5000   │              │  :5000   │                │  :5000   │
└────┬─────┘              └────┬─────┘                └────┬─────┘
     │                    ┌────┼────┬────┐          ┌──────┼──────┬─────┬─────┐
┌────▼──────┐        ┌───▼──┐ │ ┌──▼──┐ │     ┌───▼──┐   │  ┌──▼──┐  │  ┌──▼────┐
│Integration│        │AgentAI│ │ │Know.│ │     │ Auth │   │  │Audit│  │  │Outbnd │
│  :7106    │        │:7105  │ │ │:7104│ │     │:7102 │   │  │:7103│  │  │:7107  │
└───────────┘        └───┬───┘ │ └─────┘ │     └──────┘   │  └─────┘  │  └───────┘
  ┌──────┐               │     │    ┌────▼──┐        ┌───▼───┐  ┌────▼────┐
  │Outbnd│          ┌────▼─────▼──┐ │Outbnd │        │AgentAI│  │Integr.  │
  │:7107 │          │Integration  │ │:7107  │        │:7105  │  │:7106    │
  └──────┘          │:7106        │ └───────┘        └───┬───┘  └─────────┘
                    └─────────────┘                       │
                                                    ┌────▼────┐
                                                    │Knowledge│
                                                    │:7104    │
                                                    └─────────┘

Outbound Engine bağımlılıkları:
  → Integrations'tan event alır (sipariş teslim, yorum geldi)
  → AgentAI'dan mesaj kişiselleştirme ister (Phase 3+)
  → Backend'den tenant config ve template alır
```

---

## Mevcut ChatAnalysis'in Kaderi

`Invekto.ChatAnalysis` (port 7101) şu an WapCRM callback pattern'i ile çalışıyor.

**Karar: Olduğu gibi kalsın.** ChatAnalysis ayrı servis olarak devam eder.
AgentAI (Phase 3'te doğar) farklı bir interaction pattern'e sahip:
- ChatAnalysis = async analiz + callback (mevcut WapCRM entegrasyonu)
- AgentAI = real-time intent detection + auto-resolution + reply generation

---

## Özet: Eski vs Yeni Roadmap

| Konu | Eski (Mühendis) | Yeni (Hormozi v3) |
|------|-----------------|-------------------|
| Phase 0 | Yok | Müşteri validasyonu + ilk satış |
| İlk para | Phase 6 (aylar sonra) | Phase 1 (hafta 3-6) |
| Auth/SSO/2FA | Phase 1-2 (en başta) | Phase 4 (müşteri isteyince) |
| İlk servis | Auth | Integrations (Trendyol) |
| Niche | Herkes | E-ticaret (Trendyol/HB) → Sağlık (klinikler) |
| Niche sayısı | 1 (herkes) | 2 (e-ticaret primary + sağlık secondary) |
| Offer | Yok | Grand Slam Offer (niche bazlı 2 farklı) |
| Dil | "RAG, pgvector, JWT" | "Kargo sorularını otomatik cevapla" |
| Senaryo | 1 (kargom nerede) | 75 (25 e-ticaret + 25 diş + 25 klinik/estetik) |
| Outbound | Yok | Phase 2-3'te kritik bileşen (7/10 senaryo) |
| Revenue Agent | Zayıf (ödeme linki) | Güçlü (ürün önerisi + margin + stok) |
| Multi-language | Yok | Phase 3'te TR/EN, Phase 5'te +AR |
| Positioning | Yok | Tek cümle — Dunford kuralı |
| SaaS Metrikleri | Yok | 5 zorunlu metrik — Lemkin kuralı |
| User Journey | Yok | First-Value Flow — Lenny kuralı |
| Başarı metriği | Feature tamamlandı | MRR + deflection rate + churn + conversion |
| Hedef MRR (48 hafta) | Belirsiz | 650K-1.3M TL (dual niche) |

---

## Teknik Tuzaklar

1. **"AI her şeyi çözer" sanma** — Phase 1'de 3 intent ile başla. Knowledge base olmadan genişletme.
2. **Overengineering** — Phase 1'de ayrı Auth servisine gerek yok. Backend'e basit login yeterli.
3. **Entegrasyonlar "tek seferlik" yapılmaz** — Sync state + retry + idempotency zorunlu.
4. **Multi-tenant izolasyon** — Knowledge embeddings tenant bazlı olmak zorunda.
5. **Trendyol/HB API'leri kararsız** — Sprint tahminlerini %30 şişir.
6. **Müşteri feedback'i olmadan genişletme** — Her phase geçişi müşteri verisine dayansın.
7. **Outbound spam riski** — WhatsApp Business API kuralları sıkı. 24h window, template approval, opt-out zorunlu. İhlal → numara ban.
8. **Sağlık sektörü compliance** — AI tıbbi tavsiye vermemeli. "Bilgi amaçlıdır, doktor görüşü yerine geçmez" disclaimer zorunlu.
9. **Multi-language kalite** — Makine çevirisi yerine ayrı dil şablonları kullan. Özellikle medikal turizm'de yanlış çeviri = hasta kaybı.
10. **İade çevirme agresifliği** — Müşteriyi çok zorlama, negatif deneyim yaratır. 1 teklif, 1 follow-up, sonra iade başlat.
11. **Dual-niche kaynak dağılımı** — Sağlık niche'i e-ticaret'i yavaşlatmamalı. Aynı altyapı, farklı konfigürasyon prensibi.

---

## Uzman Review'ları (Özet)

> Detay: bkz [roadmap-reviews.md](roadmap-reviews.md)

| Uzman | Teşhis | Aksiyon | Durum |
|-------|--------|---------|-------|
| **Dunford** | Positioning bulanık (4 kategori) | Tek cümle positioning eklendi | ✅ |
| **Lemkin** | SaaS metrikleri eksik, Auth geç | Core metrics + Auth uyarısı + Expansion model eklendi | ✅ |
| **Lenny** | User journey tanımsız | First-Value Flow eklendi | ✅ |
| **Hormozi** | Offer katmanı eksik, feature satılıyor | Offer stratejisi + sonuç dili eklendi | ✅ |

**Açık aksiyonlar:**
- **Auth zamanlamasını izle** → Phase 3'te kurumsal talep ≥3 ise hızlandır
- **Landing page yazıldığında** → Dunford positioning'i test et, başka bir şey ekleme

---

## Expansion Model (Revenue Drivers)

| Driver | Açıklama | Phase |
|--------|----------|-------|
| Agent Seat | Temsilci başına ücretlendirme | Phase 2+ |
| Conversation Volume | Aylık konuşma limiti + aşım ücreti | Phase 3+ |
| AI Credits | AI otomatik cevap kullanımı (paket bazlı) | Phase 3+ |
| Integration Count | Entegrasyon sayısına göre tier | Phase 4+ |
| Outbound Messages | Proaktif mesaj gönderim hacmi | Phase 3+ |

**Lemkin Kuralı:** SaaS'ta büyüme = yeni müşteri + mevcut müşteri genişlemesi (expansion). Expansion model net churn'ü negatife çevirir.

---

## Revenue Timeline (Dual Niche)

```
Hafta:   1-2       3-6        7-12         13-20        21-28         29-36        37-48
          │          │           │             │             │             │            │
Phase:    0          1           2             3             4             5            6
          │          │           │             │             │             │            │
E-com:    0       3-5K       15-50K       50-125K      125-300K      300-500K     500K-1M
Sağlık:   -          -           -          pilot       25-75K       60-150K     150-300K
TOPLAM:   0       3-5K       15-50K       50-130K      150-375K      360-650K    650K-1.3M
          │          │           │             │             │             │            │
E-com:    0          1         5-10         15-25         25-40         40-60       60-100
Klinik:   -          -           -           1-2           3-5          8-15        15-30
          │          │           │             │             │             │            │
Odak:   SAT      ÇÖZMEK     ÖLÇEKLE+      AI+OUTBOUND   ENTERPRISE    SATIŞ AI+   ANALİTİK
                             OUTBOUND v1   +MULTI-LANG   +SAĞLIK      SAĞLIK TAM
                             +B2B+İADE     +YORUM KURT.  NICHE GİRİŞ  +MED.TURİZM
```

---

## Açık Sorular (Q Kararı Gerekli)

### Genel Strateji
1. **Niche onayı:** Trendyol/HB satıcıları ile mi başlıyoruz? Farklı bir niche mi?
2. **İlk müşteri:** Ulaşılabilecek potansiyel müşteri var mı zaten?
3. **Fiyat:** 3.000-5.000 TL/ay uygun mu? Farklı bir fiyat noktası mı hedefliyoruz?
4. **Garanti modeli:** "30 günde sonuç yoksa 2. ay ücretsiz" cesaret ediyor muyuz?
5. **DB seçimi:** PostgreSQL + pgvector onay mı?
6. **Ödeme gateway:** iyzico mu PayTR mi?
7. **WapCRM ilişkisi:** Invekto bağımsız mı, yoksa WapCRM'in backend'i mi?
8. **Ekip:** Solo mu, ekip mi?

### Team Reality (Lemkin "tek kişiyle yazılmış" kuralı)
9. **Phase 0-2 (12-16 hafta):** Solo founder yapılabilir mi? Gerçekçi süre nedir?
10. **Phase 3+ (16+ hafta):** Minimum 2 kişi (1 backend + 1 product/sales) gerekiyor — kabul mü?
11. **Phase 4+ (21+ hafta):** 3-4 kişi (+ security engineer + sales) — bu ekip yapısı gerçekçi mi?
12. **Zaman gerçekliği:** Bu plan minimum 12 ay sürer, 90 gün DEĞİL. Kabul mü?

### Outbound Engine
9. **WhatsApp Business API:** Meta Business API erişimi var mı? Hangi BSP (Business Solution Provider)?
10. **Outbound consent:** KVKK uyumu için outbound mesaj öncesi onay mekanizması nasıl olacak?
11. **Rate limiting:** WhatsApp'ın 24h session window dışında template message limiti nedir? (BSP'ye bağlı)
12. **Outbound servis mi, Backend içi mi?** Phase 2'de ayrı servis (Invekto.Outbound) mi yoksa Backend'e gömülü mü başlamalı?

### Sağlık Niche
13. **Sağlık niche zamanlaması:** Phase 3-4'te mi giriş yoksa e-ticaret tamamen oturunca mı?
14. **İlk klinik kontağı:** Ulaşılabilecek klinik var mı? (diş, göz, estetik?)
15. **Sağlık fiyatlandırma:** 7.500 TL/ay Klinik tier uygun mu? Sektör ödeme gücü nedir?
16. **Medikal turizm:** Arapça destek ne kadar erken lazım? Müşteri talebi var mı?
17. **HBYS entegrasyonu:** İleride HBYS (Hastane Bilgi Yönetim Sistemi) entegrasyonu düşünülüyor mu?
18. **Tıbbi sorumluluk:** AI'ın sağlık bilgisi vermesi yasal risk oluşturur mu? Disclaimer yeterli mi?

### Senaryo Öncelikleri
19. **Hangi senaryodan başlayalım?** 10 senaryodan Phase 2'ye hangisi en çok değer katar?
20. **Trendyol Review API:** Trendyol'un yorum API'si erişime açık mı? Rate limit'leri nedir?
