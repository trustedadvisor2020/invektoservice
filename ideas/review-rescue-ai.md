# Olumsuz Yorum Önleme — Proaktif Müşteri Kurtarma

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm) + pazar araştırması
> **Durum:** FİKİR AŞAMASI
> **Sektör:** E-ticaret (primer), tüm sektörler (sekonder)
> **Bağımsız SaaS Potansiyeli:** Hayır — Invekto add-on (mevcut CRM verisi gerekli)

---

## Problem

Trendyol'da 1 olumsuz yorum = satışlarda **%30-50 düşüş**. Satıcının Trendyol müşteri hizmetleri puanı 1.7/5. Platform çözmüyor, satıcı kendi çözümünü arıyor.

**Kritik nokta:** Olumsuz yorum **yazılmadan ÖNCE** sinyalleri WhatsApp'ta zaten var. Ama kimse dinlemiyor.

### Yorum Öncesi Sinyaller

```
Sinyal Zinciri (zaman çizelgesi):

T+0:  Sipariş teslim edildi
T+1h: Müşteri WhatsApp'tan yazıyor: "Ürün beklediğim gibi değil"
      ⚡ SİNYAL: Memnuniyetsizlik (hafif)

T+4h: Müşteri tekrar yazıyor: "İade etmek istiyorum, çok kötü kalite"
      ⚡⚡ SİNYAL: Kızgınlık (orta)

T+24h: Müşteri cevap alamadı veya standart cevap aldı
       ⚡⚡⚡ SİNYAL: Hayal kırıklığı (yüksek)

T+48h: ⭐ 1 Trendyol Yorumu: "ALMAYIN! Kalitesiz, iade bile zor!"
       → GEÇ KALDI. Yorum yazıldı. Puan düştü. Satışlar azaldı.
```

**Peki ya T+1h'de müdahale etseydiniz?**

```
T+1h: Müşteri: "Ürün beklediğim gibi değil"
      → AI sentiment: KIZGIN (0.7) + Yorum Riski: YÜKSEK
      → Otomatik müdahale:
        "Çok üzgünüz! 😔 Sorunu hemen çözelim:
         ① %20 indirim kodu (bir sonraki alışverişiniz için)
         ② Ücretsiz iade + yeni ürün express kargo
         ③ Farklı ürün önerisi
         Hangisini tercih edersiniz?"
      → Müşteri: "İndirim + iade olsun"
      → Sorun çözüldü → YORUM YAZILMADI ✅
      → Hatta: "Güzel ilgilendiler" diye olumlu yorum ⭐⭐⭐⭐⭐
```

### Sayılarla Problem

| Metrik | Değer |
|--------|-------|
| 1 olumsuz yorumun satış etkisi | %30-50 düşüş (kategori bağımlı) |
| Olumsuz yorum → puan düşüşü kurtarma süresi | 2-6 ay |
| Trendyol satıcı puan ortalaması | 4.2/5 (altında satışlar ciddi düşer) |
| Yorum yazılmadan müdahale başarı oranı | %60-80 (sektör verisi) |
| Mevcut çözüm | **YOK** (reaktif — yorum geldikten sonra cevap) |

---

## Çözüm

**Review Rescue AI:** WhatsApp konuşmalarından müşteri memnuniyetsizliğini **yorum yazılmadan önce** tespit et → otomatik kurtarma akışı başlat → olumsuz yorum önle.

### Akış

```
┌─────────────────────────────────────────────────────────────┐
│                    DETECTION LAYER                            │
│                                                             │
│  Her müşteri mesajı → Sentiment analizi (mevcut ChatAnalysis)│
│                                                             │
│  Skor < -0.5 → ⚡ ALARM: Memnuniyetsizlik tespiti          │
│                                                             │
│  Ek kontroller:                                              │
│  • "iade" kelimesi + kızgın ton = 🔴 YÜKSEK RİSK           │
│  • Sipariş teslim sonrası (T+0-72h) mesaj = risk penceresi  │
│  • Tekrarlı mesaj (cevap alamıyor) = risk artırıcı          │
│  • "yorum yazacağım", "şikayet" = 🔴 KRİTİK                │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    RISK SCORING                               │
│                                                             │
│  Risk Skoru = f(sentiment, keywords, timing, history)        │
│                                                             │
│  🟢 DÜŞÜK (0-30):   Normal şikayet → standart akış          │
│  🟡 ORTA (30-60):   Memnuniyetsizlik → hızlı cevap          │
│  🟠 YÜKSEK (60-80): Kızgınlık → otomatik kurtarma           │
│  🔴 KRİTİK (80-100): Yorum/şikayet tehdidi → acil müdahale │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    RESCUE ENGINE                              │
│                                                             │
│  Risk seviyesine göre otomatik aksiyon:                      │
│                                                             │
│  🟡 ORTA:                                                   │
│    → Agent'a "öncelikli" uyarısı + önerilen cevap           │
│    → Cevap süresi < 15dk hedefi                              │
│                                                             │
│  🟠 YÜKSEK:                                                 │
│    → Otomatik özür mesajı (hemen)                            │
│    → Çözüm seçenekleri sun (indirim / iade / değişim)       │
│    → Supervisor'a bildirim                                   │
│                                                             │
│  🔴 KRİTİK:                                                 │
│    → Otomatik özür + üst düzey çözüm (tam iade + indirim)  │
│    → Supervisor + mağaza sahibine alert                      │
│    → "VIP müşteri" etiketiyle işaretle                      │
│    → Çözüm sonrası: "Deneyiminizi iyileştirdik, bizi       │
│      değerlendirir misiniz?" (olumlu yorum yönlendirme)     │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    FOLLOW-UP & TRACKING                       │
│                                                             │
│  T+24h: Çözüm sonrası → "Memnun kaldınız mı?"              │
│  T+48h: Memnunsa → "Bizi değerlendirir misiniz? ⭐"        │
│  T+72h: Değerlendirme yoksa → 1 kez daha hatırlatma         │
│                                                             │
│  Dashboard:                                                  │
│  • Kurtarılan yorum sayısı                                   │
│  • Kurtarma başarı oranı                                     │
│  • Olumsuz→olumlu dönüşüm oranı                             │
│  • Tahmini kurtarılan satış geliri                           │
└─────────────────────────────────────────────────────────────┘
```

---

## Teknik Detay

### Mevcut Altyapıyla Entegrasyon

| Bileşen | Mevcut mi? | Ne Yapılacak? |
|---------|-----------|--------------|
| Sentiment analizi | ✅ ChatAnalysis (:7101) | Risk skoru eklenir |
| Intent detection | ✅ AgentAI (:7105) | "iade_tehditi", "yorum_tehditi" intent'i eklenir |
| Otomatik mesaj gönderme | ✅ Outbound (:7107) | Kurtarma mesaj template'leri eklenir |
| Agent bildirim | ✅ Invekto UI | "Yüksek risk" badge eklenir |
| Sipariş bilgisi | 🟡 Phase 3A Integrations | Teslimat sonrası risk penceresi için |

**Bu özellik büyük oranda MEVCUT servislerin genişletilmesidir. Yeni servis gerekmez.**

### Risk Hesaplama Formülü

```
risk_score = (
  sentiment_score × 30          # ChatAnalysis'ten (-1 to +1 → 0-30)
  + keyword_score × 25          # "iade", "şikayet", "yorum" → 0-25
  + timing_score × 20           # Teslimat sonrası 0-72h → yüksek risk
  + response_delay_score × 15   # Cevap bekliyor → risk artıyor
  + history_score × 10          # Daha önce sorun yaşamış → yüksek
)

# Eşik değerleri (tenant ayarlayabilir):
LOW: 0-30     → normal akış
MEDIUM: 30-60 → agent önceliklendirme
HIGH: 60-80   → otomatik kurtarma
CRITICAL: 80+ → acil müdahale + supervisor alert
```

### Kurtarma Stratejileri (Tenant Yapılandırılabilir)

| Strateji | Açıklama | Maliyet | Etkinlik |
|----------|----------|---------|----------|
| **Özür + Empati** | "Çok üzgünüz, hemen çözelim" | ₺0 | %20-30 |
| **İndirim Kodu** | %10-20 indirim (sonraki alışveriş) | ₺30-100 | %40-50 |
| **Ücretsiz Kargo İade** | Normal iade yerine ücretsiz kargo | ₺40-80 | %30-40 |
| **Hızlı Değişim** | Express kargo ile yeni ürün | ₺80-150 | %60-70 |
| **Tam İade + İndirim** | Para iade + sonraki için indirim | ₺200+ | %80-90 |

**Tenant kuralları:**
- Risk YÜKSEK → max indirim: %15
- Risk KRİTİK → max indirim: %25 + ücretsiz kargo
- Aylık kurtarma bütçesi: max ₺5,000 (tenant ayarlar)

---

## Maliyet Analizi

### Ek Maliyet (Mevcut altyapı üzerine)

| Bileşen | Maliyet |
|---------|---------|
| Sentiment analizi | ✅ Zaten var (ChatAnalysis) |
| Risk scoring | ~$0.001/mesaj (basit hesaplama) |
| Kurtarma mesajları | ~$0.05-0.15/mesaj (WhatsApp) |
| **Toplam ek maliyet** | **~$0.05-0.15/kurtarma girişimi** |

### ROI Hesabı

```
Senaryo: Aylık 50 olumsuz yorum riski olan müşteri

Müdahale edilmezse:
  50 olumsuz yorum × satış etkisi → ₺50,000-200,000/ay kayıp

AI ile müdahale (T+1h):
  50 riskli müşteri × %70 kurtarma başarısı = 35 kurtarılan
  Kurtarma maliyeti: 50 × ₺150 (ort. indirim/iade) = ₺7,500
  Kurtarılan satış: ₺35,000-140,000/ay

Net fayda: ₺27,500-132,500/ay
```

---

## MVP Scope

| Bileşen | MVP'de Var | Sonrası |
|---------|------------|---------|
| Sentiment bazlı risk skoru | ✅ | |
| Keyword algılama ("iade", "şikayet") | ✅ | |
| Agent'a "yüksek risk" uyarısı | ✅ | |
| Otomatik özür mesajı (YÜKSEK risk) | ✅ | |
| Supervisor alert (KRİTİK risk) | ✅ | |
| Kurtarma sonrası "memnun musunuz?" | ✅ | |
| Kurtarma dashboard'u | ✅ | |
| Otomatik indirim kodu oluşturma | | ✅ |
| Trendyol/HB sipariş bağlantısı | | ✅ (Phase 3A) |
| Teslimat sonrası risk penceresi | | ✅ (Integrations gerekli) |
| Olumlu yorum yönlendirme | | ✅ |
| A/B test (hangi kurtarma stratejisi daha iyi) | | ✅ |

---

## DB Şeması

```sql
-- Risk tespitleri
review_risks (
  id UUID PRIMARY KEY,
  tenant_id UUID NOT NULL,
  conversation_id UUID NOT NULL,
  customer_phone VARCHAR(20),
  risk_score INT NOT NULL,                -- 0-100
  risk_level VARCHAR(20) NOT NULL,        -- 'low', 'medium', 'high', 'critical'
  trigger_reason TEXT,                    -- "sentiment:-0.8, keyword:iade, timing:T+2h"
  rescue_status VARCHAR(20) DEFAULT 'pending', -- 'pending', 'in_progress', 'rescued', 'failed'
  rescue_strategy VARCHAR(50),            -- 'apology', 'discount', 'free_return', 'full_refund'
  rescue_cost DECIMAL(10,2),              -- kurtarma maliyeti (indirim tutarı vb.)
  customer_response VARCHAR(20),          -- 'satisfied', 'unsatisfied', 'no_response'
  review_posted BOOLEAN DEFAULT FALSE,    -- sonuçta yorum yazıldı mı?
  review_rating INT,                      -- yazıldıysa kaç yıldız (1-5)
  created_at TIMESTAMPTZ DEFAULT NOW(),
  resolved_at TIMESTAMPTZ
);

-- Kurtarma mesaj template'leri
rescue_templates (
  id UUID PRIMARY KEY,
  tenant_id UUID NOT NULL,
  risk_level VARCHAR(20) NOT NULL,
  strategy VARCHAR(50) NOT NULL,
  message_template TEXT NOT NULL,
  max_discount_pct INT,
  is_active BOOLEAN DEFAULT TRUE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);
```

---

## Phase 3B ile Sinerji

Bu özellik Phase 3B'deki mevcut GR'larla doğrudan bağlantılı:

| GR | İlişki |
|----|--------|
| GR-3.1 (Intent Genişletme) | "iade_tehditi" ve "yorum_tehditi" intent'leri eklenir |
| GR-3.8 (İade Çevirme v1) | İade çevirme + yorum önleme birlikte çalışır |
| GR-3.16 (Negatif Yorum Kurtarma) | Yorum yazıldıktan sonraki akış — Review Rescue bunun "önleme" versiyonu |
| GR-3.7 (Outbound E-ticaret) | Kurtarma sonrası follow-up otomasyonu |

**Önerilen:** GR-3.8 ve GR-3.16'nın genişletilmiş hali olarak Phase 3B'ye entegre.

---

## AHA Moments

| Kategori | AHA |
|----------|-----|
| **UX** | Satıcı dashboard'da "Bu ay 23 olumsuz yorum önlendi" görüyor — rakipleri hâlâ yangın söndürüyor |
| **SPEED** | Müşteri kızgın mesaj attı, 30 saniye içinde özür + çözüm geldi — "bu kadar hızlı ilgilenen mağaza görmedim" |
| **SALES** | "Kurtarılan olumsuz yorumlar → korunan satış geliri: ₺85,000/ay" raporu — ROI gözle görülür |
| **RELIABILITY** | Kurtarma başarı oranı %70+ → satıcı puanı 4.2'nin altına hiç düşmüyor |
| **SUPPORT** | "En çok şikayet alan ürünler" raporu → kalite sorunu olan ürünleri tespit et, tedarikçiyle konuş |

---

## Riskler

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| Müşteri "indirim için kızıyorum" taktik yapabilir | 🟡 Orta | Müşteri geçmişi kontrolü, tekrarlayan pattern'de sınır |
| Kurtarma bütçesi kontrolsüz artabilir | 🟡 Orta | Tenant bazlı aylık bütçe limiti |
| Yanlış pozitif (kızgın değil ama risk skoru yüksek) | 🟡 Orta | ORTA riskte sadece agent uyarısı, otomatik aksiyon sadece YÜKSEK+ |
| WhatsApp anti-spam (çok mesaj atma) | 🟢 Düşük | Tenant bazlı rate limiting |

---

## Roadmap Referansı

> **Phase:** 3B (Niche Derinleştirme) — [phases/phase-3b.md](phases/phase-3b.md)
> **GR:** GR-3.24 Proactive Review Rescue
> **İlişki:** GR-3.8 (İade Çevirme v1) + GR-3.16 (Negatif Yorum Kurtarma) genişletme
> **Entegre:** 2026-02-14

---

## Sonraki Adımlar

- [x] Q karar: Phase 3B — GR-3.8/3.16 genişletme olarak entegre edildi (GR-3.24)
- [ ] Mevcut ChatAnalysis sentiment modeliyle risk skoru PoC
- [ ] 3 satıcıyla görüşme: "Olumsuz yorum öncesi WhatsApp sinyallerini biliyor musunuz?"
- [ ] Kurtarma stratejileri A/B test planı
