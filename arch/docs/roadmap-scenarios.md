<!-- Status: REFERENCE -->
# Invekto — Senaryo Portföyü & Outbound Engine

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 12 revenue senaryosu (3 niche + 2 evrensel) + 64 aktif saha senaryosu (3 sektör, 11 silindi) + 17 otel senaryosu (O1-O17) + 7 mobil senaryo (M1-M7) + 8 cross-sector kritik (CS-01~CS-08) + 7 e-ticaret ek (EB-01~EB-07) + 5 sağlık ek (SB-01~SB-05) + 25 güzellik salonu (GU-01~GU-25) + 25 eğitim (EG-01~EG-25) + Outbound Engine kritik bulgusu ve gereksinimleri
> Güncelleme: v2 — 75 senaryo (25 e-ticaret + 25 diş + 25 klinik/estetik) + capability mapping eklendi
> Güncelleme: v3 — 3 niche ortak capability analizi eklendi (2026-02-08)
> **Güncelleme: Mevcut ürün gerçekliği ile yeniden çerçevelendi (2026-02-08)**
> **Güncelleme: v4 — Tekrar temizliği (11 senaryo silindi) + sektör ayırıcıları + grup etiketleri + yapısal tablolar (2026-02-16)**
> **Güncelleme: v5 — 29 yeni senaryo eklendi (B bölümü): 8 cross-sector + 7 e-ticaret + 5 sağlık + 7 otel + 2 mobil (2026-02-16)**
> **Güncelleme: v6 — +S11/S12 revenue + 25 güzellik salonu (GU) + 25 eğitim (EG) senaryosu eklendi. D1-D4 stratejik kararlar verildi (2026-02-16)**
> Referans: [whatisinvekto.md](whatisinvekto.md) — Invekto mevcut ürün envanteri
> Referans: [scenarios-review-actions.md](scenarios-review-actions.md) — Review aksiyon planı (kaynak)

---

## Mevcut Ürün Gerçekliği

- Invekto **50+ aktif müşteri** ile çalışıyor
- **7 kanal** (WA Cloud API, WA BSP, IG DM, FB Messenger, Telegram, SMS, VOIP) Unified Inbox **mevcut**
- **Gelişmiş routing** (4 algoritma, grup bazlı, kanal bazlı) **mevcut**
- **Şablon mesajlar**, **CRM** (contact, etiket, 10 custom field), **VOIP** **mevcut**
- **Raporlama** (mesaj, agent performans, kanal dağılımı) **mevcut**
- Müşteri tabanı ağırlıklı: **sağlık klinikleri + otel/turizm** (e-ticaret = yeni niche)

**Senaryo öncelikleri:**
1. **Mevcut müşteri senaryoları ÖNCELİKLİ** — sağlık + otel müşterileri zaten var
2. **Core otomasyon TÜM senaryolara fayda sağlar** — chatbot, AI assist, broadcast
3. **E-ticaret niche = yeni müşteri kazanım** — Trendyol/HB API Phase 2'de
4. **Outbound Engine Phase 1'de** — 10 senaryodan 7'si outbound gerektiriyor

> Detaylı mevcut durum: bkz [whatisinvekto.md](whatisinvekto.md)

---

## 3 Sektör Ortak Capability Analizi (75 Senaryo — 2026-02-08)

> **Karar:** 3 niche'e paralel giriş. Ortak altyapı tek codebase, sektör farkı = config.

### Ortak Çekirdek (3 Sektörde Zorunlu — Tek Codebase)

| Capability | E-ticaret (25) | Diş (25) | Estetik (25) | Toplam | Oran |
|------------|----------------|----------|--------------|--------|------|
| **C8: Agent Assist** | 25/25 | 25/25 | 25/25 | **75/75** | **%100** |
| **C3: Templates & Snippets** | 25/25 | 24/25 | 24/25 | **73/75** | **%97** |
| **C1: Unified Inbox** | 25/25 | 24/25 | 23/25 | **72/75** | **%96** |
| **C2: Routing & Workload** | 25/25 | 24/25 | 22/25 | **71/75** | **%95** |

> **Sonuç:** Bu 4 capability ürünün omurgası. Phase 1'de hepsi hazır olmalı. Sektör farketmez.

### İki Sektör Ortak

| Capability | E-ticaret | Diş | Estetik | Not |
|------------|-----------|-----|---------|-----|
| **C7: Knowledge/RAG** | 24/25 | 24/25 | 0/25 | E-ticaret + Diş: bilgi tutarlılığı kritik |

### Sektöre Özel Capability'ler

| Capability | E-ticaret | Diş | Estetik | Açıklama |
|------------|-----------|-----|---------|----------|
| **C12: Ads Attribution** | 0 | 0 | **24/25** | Click-to-WhatsApp kampanya tracking — sadece estetik |
| **C4: Reporting Core** | 1 | 0 | **24/25** | Conversion takibi — estetik ağırlıklı |
| **C10: Revenue Agent** | 0 | 2 | **23/25** | Ödeme/depozit/lead dönüşümü — estetik ağırlıklı |
| **C11: E-commerce Integrations** | **2** | 0 | 0 | Trendyol/HB API — sadece e-ticaret (düşük frekans, yüksek etki) |
| **C5/C6: Security** | 0 | 1 | 1 | KVKK sağlık verisi — düşük frekans, yüksek risk |
| **C13: QA & Mining** | 1 | 2 | 0 | Kalite kontrol — düşük frekans |
| **C9: Auto-Resolution** | 0 | 0 | 0 | Hiçbir senaryoda doğrudan kullanılmıyor — Phase 2-3 future |

### Platform Yapısı: Tek Codebase, 3 Config

```
INVEKTO PLATFORM
│
├── CORE (tüm niche'ler — %95 ortak)
│   ├── C1: Unified Inbox (WhatsApp mesaj yönetimi)
│   ├── C2: Routing (mesaj yönlendirme + iş yükü)
│   ├── C3: Templates (şablon mesajlar + değişkenler)
│   ├── C8: Agent Assist (AI öneriler + kaynak referansı)
│   ├── Auth (login + tenant izolasyonu)
│   ├── Dashboard (metrikler + yönetim)
│   └── Outbound Engine (proaktif mesaj gönderimi)
│
├── CONFIG: E-TİCARET
│   ├── C11: Trendyol/HB API entegrasyonu
│   ├── C7: Knowledge (ürün bilgisi, iade politikası)
│   ├── Intent seti: kargo, iade, sipariş, fatura, stok
│   └── Dashboard: deflection rate, iade çevirme, B2B lead
│
├── CONFIG: DİŞ KLİNİĞİ
│   ├── Randevu motoru (slot yönetimi + no-show önleme)
│   ├── C7: Knowledge (tedavi bilgisi, fiyat aralıkları)
│   ├── C5/C6: KVKK sağlık verisi koruma
│   ├── Intent seti: fiyat, randevu, tedavi bilgisi, acil, sigorta
│   └── Dashboard: fiyat→randevu dönüşüm, no-show oranı, hatırlatma etkinliği
│
└── CONFIG: ESTETİK KLİNİK
    ├── Lead management (pipeline + scoring + follow-up)
    ├── C10: Revenue Agent (depozit + ödeme linki)
    ├── C12: Ads Attribution (UTM + kampanya tracking)
    ├── C4: Reporting (conversion funnel + ROAS)
    ├── Multi-language (TR/EN/AR)
    ├── Intent seti: fiyat, konsültasyon, before/after, paket, kontrendikasyon
    └── Dashboard: lead→randevu dönüşüm, kampanya ROI, yabancı hasta oranı
```

### Phase Bazlı Capability Devreye Alma (3 Niche Paralel)

> C1 (Inbox), C2 (Routing), C3 (Templates) **ZATEN MEVCUT** Invekto'da.
> Phase 1'deki asıl iş: **Otomasyon (chatbot/trigger) + AI Assist + Outbound (broadcast)** eklemek.
> Bu 3 yeteneğin TÜM sektörlere eşzamanlı faydası var.

| Phase | Ortak (InvektoServis) | E-ticaret Özel | Diş Özel | Estetik Özel |
|-------|-------|----------------|----------|--------------|
| **Mevcut** | **C1, C2, C3 zaten Invekto'da** | — | — | — |
| **1** | **Automation (chatbot/trigger) + C8 (AI Assist) + Outbound (broadcast)** | Intent seti: kargo, iade, sipariş | Fiyat sorusu AI + no-show hatırlatma (basit) | Lead tracking (basit) |
| **2** | Outbound v2 (follow-up zinciri, schedule) | Trendyol/HB API, iade çevirme, B2B lead, C11 tam | Randevu motoru, tedavi takip | Lead scoring, ads attribution (basit), multi-lang v1 |
| **3** | C7 (Knowledge/RAG), AgentAI derinleştirme, multi-lang (TR/EN) | Yorum kurtarma, iade v2 | Tedavi knowledge | Multi-lang v2 (TR/EN) |
| **4** | Auth genişleme (SSO), Audit, PII | Enterprise security | KVKK compliance tam | Enterprise security, KVKK |
| **5** | Revenue Agent tam | Cart recovery, cross-sell | Tedavi takip tam, yorum motoru, referans | Revenue agent, full ads attribution, medikal turizm tam, AR dil |

### Niche Bazlı Senaryo Öncelikleri (Phase 1-2 İçin)

> **ÖNCELİK SIRASI:**
> 1. Diş + Estetik senaryoları = **MEVCUT MÜŞTERİ** → upsell + churn azaltma
> 2. E-ticaret senaryoları = **YENİ MÜŞTERİ** kazanım
> 3. Core otomasyon (chatbot, AI assist, broadcast) = **HERKESE** fayda
>
> **⚠️ "Phase 1 launch" = KISMİ CAPABILITY İLE BAŞLAR:**
> Aşağıdaki senaryolar Phase 1'de core capability (C1+C2+C3+C8) ile başlatılır.
> Senaryo capability mapping'indeki ileri yetenekler (C7, C10, C11, C12, C13) sonraki phase'lerde
> aşamalı eklenir. "Phase 1 launch" ≠ "tüm capability'ler hazır", = "müşteriye değer vermeye başla."

**Diş — İlk 5 Senaryo (⚡ MEVCUT MÜŞTERİ — ÖNCELİKLİ):**
1. Fiyat sorusu (D26) — Phase 1 launch ← **AI Assist ile hemen fayda**
2. Randevu alma (D28) — Phase 1 launch ← **Chatbot flow ile otomatik**
3. No-show önleme (D40) — Phase 1 launch ← **Outbound hatırlatma**
4. Acil triage (D34) — Phase 2
5. Tedavi bilgisi (D29) — Phase 2

**Estetik — İlk 5 Senaryo (⚡ MEVCUT MÜŞTERİ — ÖNCELİKLİ):**
1. Fiyat sorusu Instagram DM (A51) — Phase 1 launch ← **AI Assist ile hemen fayda**
2. DM→WhatsApp geçiş (A53) — Phase 1 launch
3. Before/after fotoğraf (A52) — Phase 1 launch
4. Lead follow-up (A54) — Phase 2 ← **Outbound follow-up**
5. Yabancı hasta (A55) — Phase 2

**E-ticaret — İlk 5 Senaryo (YENİ MÜŞTERİ KAZANIM):**
1. Kargom nerede (E01) — Phase 1 launch ← **AI Assist + template**
2. İade talebi (E02/E03) — Phase 2
3. B2B/toptan lead (S5) — Phase 2
4. Fatura talebi — Phase 2
5. Sipariş iptal — Phase 2

---

## Senaryo Portföyü (12 Senaryo — 3 Niche + 2 Evrensel)

> Roadmap sadece "kargom nerede" senaryosuna değil, 12 farklı revenue senaryosuna dayanıyor.
> Her senaryo test edilmiş Hormozi değer denklemine uygun.
> **v6 (2026-02-16):** +S11 (Abonelik/Üyelik) + S12 (Churn Prevention) eklendi. Müşteri senaryosu olarak tüm sektörleri kapsar.

### E-ticaret Senaryoları (Niche 1: Trendyol/HB Satıcıları)

| # | Senaryo | Tetikleyici | AI Aksiyonu | Aylık Etki (10 müşteri) | Roadmap Phase |
|---|---------|-------------|-------------|-------------------------|---------------|
| S1 | **Negatif Yorum Kurtarma** | Trendyol'da 1-2 yıldız yorum | AI tespit → özür + çözüm mesajı → yorum güncelleme talebi | ~144K TL | Phase 3 |
| S2 | **Satış Öncesi Ürün Soruları** | "Bu ürün X'e uyar mı?" | Knowledge base'den cevap + ürün linki + alternatif | ~31.5K TL | Phase 3 |
| S3 | **İade Çevirme** | "İade etmek istiyorum" | Neden sor → değişim/kupon öner → iade maliyetinden kurtar | ~18K TL | Phase 2-3 |
| S4 | **Sipariş Sonrası Proaktif Satış** | Teslimat tamamlandı | T+3 gün "memnun musunuz?" → cross-sell önerisi | ~22.5K TL | Phase 5 |
| S5 | **Toplu/B2B Lead Tespiti** | "100 adet lazım", "toptan fiyat var mı" | VIP flag → sales team alert → özel teklif akışı | ~37.5K TL | Phase 2 |

---

## E-ticaret Senaryoları — Detay

### S1: Negatif Yorum Kurtarma (Review Recovery)

**Sahne:**

Trendyol'da müşteri 1 yıldız vermiş: *"Ürün geç geldi, ilgilenilmedi."* Bu yorum ürün sayfasında duruyor. Potansiyel 100 müşteri bunu görüyor. Her 1 yıldızlık yorum → ortalama **%8 dönüşüm kaybı**. Satıcının günde 200 siparişi var → 16 satış kaybı/gün → 480 satış/ay. Ortalama sepet 300 TL → **144.000 TL/ay kayıp — TEK BİR KÖTÜ YORUMDAN.**

**Bugün ne oluyor:**
```
→ Satıcı Trendyol panelinde yorumları görüyor
→ WhatsApp'tan müşteriyi bulmaya çalışıyor (telefon eşleştirme)
→ Çoğu zaman ulaşamıyor veya çok geç kalıyor
→ Yorum düzeltilemiyor, hasar kalıcı
```

**Invekto ile:**
```
→ Trendyol API'den düşük yıldızlı yorumlar otomatik çekilir
→ Müşteri telefonu sipariş verisinden eşleştirilir
→ AI otomatik WhatsApp mesajı hazırlar:
  "Merhaba [Ad], siparişinizden memnun kalmadığınızı gördük.
   Sorununuzu çözmek istiyoruz. Size nasıl yardımcı olabiliriz?"
→ Sorun çözülürse: "Yorumunuzu günceller misiniz?" önerisi
→ Dashboard'da: kurtarılan yorum sayısı + tahmini gelir etkisi
```

**Para etkisi:**
```
Kurtarma oranı %30 olsa → ayda 43.200 TL kurtarılan gelir
Invekto fiyatı 5.000 TL → 8.6x ROI — SADECE BU SENARYODAN
```

> **Hormozi:** *"Bu tek senaryo, kargo sorgulamadan daha değerli. Çünkü kargo sorgusu maliyet düşürür, yorum kurtarma gelir kurtarır. İnsanlar maliyet düşürmek için 1x öder, gelir kurtarmak için 3x öder."*

---

### S2: Satış Öncesi Ürün Soruları (Pre-Sale Conversion)

**Sahne:**

Instagram'dan veya WhatsApp'tan mesaj geliyor: *"Bu ceket XL bedene uyar mı?"*, *"Su geçirmez mi?"*, *"Aynı üründen mavi var mı?"*

Temsilci Trendyol paneline gidiyor → ürünü arıyor → açıklamayı okuyor → WhatsApp'a dönüp yazıyor. 5-10 dakika sürüyor. Cevap 10dk'dan fazla sürerse müşteri gidiyor. Conversion rate: **<30dk yanıt = %40 dönüşüm, >30dk yanıt = %5 dönüşüm.**

Günde 50 ürün sorusu geliyor. 30'u geç cevaplanıyor. 30 × %35 dönüşüm farkı × 300 TL sepet = 3.150 TL/gün kayıp = **94.500 TL/ay.**

**Bugün ne oluyor:**
```
→ Temsilci her soruyu manuel araştırıyor
→ Ürün bilgisi kafasında veya Trendyol sayfasında
→ Aynı soru 50 kez sorulsa 50 kez araştırıyor
→ Yoğun saatlerde cevap gecikiyor, müşteri satın almadan gidiyor
```

**Invekto ile:**
```
→ Ürün kataloğu Knowledge base'e yüklenir (PDF/CSV/Trendyol sync)
→ AI ürün sorusunu anlar → katalogdan cevap bulur → anında yanıt
→ "Bu ceket XL bedene uygundur. Göğüs: 112cm, Boy: 74cm. (kaynak: ürün kartı)"
→ Cevap 30sn'de gider, dönüşüm %40'a çıkar
→ Dashboard'da: "ürün sorusu → satın alma" dönüşüm oranı
```

**Para etkisi:**
```
30 geç cevaplanan sorunun 10'u hızlanırsa → 10 × %35 × 300 TL = 1.050 TL/gün
= 31.500 TL/ay ek gelir. Invekto 5.000 TL → 6.3x ROI
```

> **Hormozi:** *"Her cevaplanmayan ürün sorusu = kaçan satış. Bu AI'ı 'support tool' olarak değil 'sales closer' olarak konumla. Cevap hızı = para hızı."*

---

### S3: İade Çevirme (Return Deflection)

**Sahne:**

Müşteri yazıyor: *"İade etmek istiyorum."* Trendyol satıcısının iade oranı ortalama %8-15. Günde 200 sipariş × %10 iade = 20 iade/gün. Her iade: ürün maliyeti + kargo + komisyon kaybı + yorum riski ≈ 150 TL kayıp. 20 × 150 = 3.000 TL/gün = **90.000 TL/ay iade kaybı.**

**İadelerin %40'ı aslında çözülebilir:**
- "Beden küçük geldi" → değişim öner
- "Kullanmayı bilmiyorum" → kullanım videosu gönder
- "Rengi farklı çıktı" → %15 indirim kuponu teklif et
- "Geç geldi, artık lazım değil" → bir sonraki siparişe %10 indirim

**Bugün ne oluyor:**
```
→ Temsilci direkt iade prosedürünü anlatıyor
→ Neden iade istediği sorulmuyor
→ Çözülebilir iadeler de işleniyor
→ Para gidiyor
```

**Invekto ile:**
```
→ AI "iade" intent'ini yakalar ama direkt iade işlemi başlatmaz
→ Önce sebebi sorar: "Üzgünüz! Neden iade düşünüyorsunuz?"
→ Sebebe göre alternatif sunar:
  - Beden → "Değişim yapalım, kargo bizden"
  - Kullanım → "Size video/rehber göndereyim"
  - Memnuniyetsizlik → "%15 indirim kuponu" (tenant tanımlı limit)
→ Müşteri kabul ederse → iade önlendi, gelir korundu
→ Dashboard'da: önlenen iade sayısı + kurtarılan gelir
```

**Para etkisi:**
```
20 iade/gün × %40 çözülebilir × %50 başarı = 4 kurtarılan iade/gün
4 × 150 TL = 600 TL/gün = 18.000 TL/ay kurtarılan gelir
```

> **Hormozi:** *"Her iade bir müşteri kaybı. Ama her iade talebi bir kurtarma fırsatı. Çoğu satıcı bunu bilmiyor çünkü araçları yok. Sen aracı ver, parayı koru."*

---

### S4: Sipariş Sonrası Proaktif Satış (Post-Purchase Outbound)

**Sahne:**

Müşteri dün telefon kılıfı aldı. 3 gün sonra kargo teslim edildi. Sessizlik.

Ama bu müşteri muhtemelen şunlara da ihtiyaç duyuyor: ekran koruyucu, şarj kablosu, powerbank. Bugün bu cross-sell yapılmıyor. Müşteri tek ürün alıp gidiyor. Repeat purchase rate: **%5** (sektör ortalaması).

**Bugün ne oluyor:**
```
→ Hiçbir şey. Sipariş teslim edildi, bitti.
→ Müşteri başka satıcıdan alıyor
→ Lifetime value düşük kalıyor
```

**Invekto ile:**
```
→ Teslimattan 2 gün sonra otomatik WhatsApp:
  "Merhaba [Ad]! Telefon kılıfınız ulaştı mı?
   Bu kılıfla en çok tercih edilen ekran koruyucumuz şu an %15 indirimli.
   İlgilenir misiniz?"
→ Bundle rules: "kılıf alan → ekran koruyucu öner" (tenant tanımlı)
→ Müşteri "evet" derse → ürün linki veya ödeme linki gönder
→ Opt-out: "Hayır teşekkürler" → bir daha mesaj gitmesin
```

**Para etkisi:**
```
Günde 200 teslimattan 50'sine mesaj (%25 hedefleme)
50'den 5'i satın alır (%10 conversion) × 150 TL = 750 TL/gün
= 22.500 TL/ay EK GELİR (sıfırdan oluşan, daha önce yoktu)
```

> **Hormozi:** *"En ucuz müşteri zaten sana para vermiş olan müşteri. Yeni müşteri bulmak 5x daha pahalı. Ama çoğu satıcı teslimattan sonra müşteriyle konuşmuyor. Bu bedava para."*

---

### S5: Toplu/B2B Lead Tespiti (High-Value Lead Detection)

**Sahne:**

WhatsApp'tan mesaj geliyor: *"Merhaba, 50 adet sipariş vermek istiyorum. Toplu fiyat var mı?"*

Bu mesaj günlük 150+ mesajın arasında kayboluyor. Temsilci normal müşteri gibi cevaplıyor. B2B fırsatı fark edilmeden geçiyor.

Veya: *"Kurumsal fatura keser misiniz?"*, *"Bayilik vermek istiyor musunuz?"*, *"Düzenli sipariş verebilir miyiz?"*

Bunlar high-value lead. Tek sipariş 50-500 adet. Ortalama B2B sipariş: 15.000-50.000 TL. Ayda 3-5 böyle lead geliyordur. **Çoğu kaçıyor.**

**Bugün ne oluyor:**
```
→ Mesaj gürültüde kaybolur
→ Temsilci fark etse bile "müdüre sorayım" der, geri dönmez
→ Müşteri bekler, rakibe gider
→ Ayda 2-3 B2B fırsat kaybolur → 30.000-150.000 TL kayıp
```

**Invekto ile:**
```
→ AI mesajda B2B sinyallerini tespit eder:
  - "toplu", "adet", "kurumsal", "bayilik", "düzenli sipariş"
→ Otomatik olarak:
  - Konuşmayı VIP olarak etiketler
  - Satıcıya/müdüre acil bildirim gönderir
  - Müşteriye hızlı yanıt: "Toplu siparişler için özel fiyatlarımız var!
    Hemen ilgili arkadaşımız sizinle iletişime geçecek."
→ Dashboard'da: tespit edilen B2B lead'ler + durumları + kapanış oranı
```

**Para etkisi:**
```
Ayda 3 B2B lead × %50 tespit (daha önce kaçıyordu)
1.5 yeni B2B müşteri/ay × 25.000 TL ortalama sipariş = 37.500 TL/ay
```

> **Hormozi:** *"Bir B2B müşteri 100 normal müşteriye bedel. Ama çoğu satıcı B2B lead'in geldiğini bile bilmiyor. AI'ın bir tek şeyi daha iyi yapması lazım: altın nugget'ı çöpten ayır."*

---

### Sağlık Senaryoları (Niche 2: Klinikler — Mevcut Müşteriler)

> Sağlık klinik müşterileri ZATEN VAR. Core otomasyon tüm sektörlere aynı anda fayda sağlar.
> Niche-özel özellikler (randevu motoru, KVKK) Phase 2'de.

| # | Senaryo | Tetikleyici | AI Aksiyonu | Aylık Etki (5 klinik) | Phase |
|---|---------|-------------|-------------|----------------------|-------|
| S6 | **Fiyat → Randevu Dönüşümü** | "İmplant ne kadar?" | Fiyat aralığı + ücretsiz muayene teklifi + slot öner | ~60K TL | Phase 1-2 |
| S7 | **No-Show Önleme** | Randevu T-48h, T-2h | Otomatik hatırlatma + onay iste + iptal slot'u doldur | ~135K TL | Phase 1-2 |
| S8 | **Tedavi Sonrası Takip** | Tedavi tamamlandı | T+1, T+7, T+30 kontrol soruları → şikayet varsa doktora alert | ~90K TL | Phase 2 (basit hatırlatma) / Phase 5 (tam otomasyon) |
| S9 | **Medikal Turizm Lead Yönetimi** | İngilizce/Arapça mesaj | Multi-language AI → fiyat + konaklama + transfer teklifi | ~300K+ TL | Phase 5 |
| S10 | **Google Yorum + Referans Motoru** | Tedavi başarılı + hasta memnun | Yorum rica → link gönder → referans kodu → arkadaş getir kampanyası | ~105K TL | Phase 5 |

### Evrensel Senaryolar (Tüm Sektörler)

> Tüm sektörlerde (e-ticaret, sağlık, estetik, otel, güzellik, eğitim) geçerli gelir modelleri.

| # | Senaryo | Tetikleyici | AI Aksiyonu | Aylık Etki (10 müşteri) | Roadmap Phase |
|---|---------|-------------|-------------|-------------------------|---------------|
| S11 | **Abonelik / Üyelik Modeli** | Tekrarlayan satın alma, düzenli ziyaret | Abonelik teklifi → otomatik yenileme hatırlatma → upsell tier | ~75K TL | Phase 3-5 |
| S12 | **Churn Prevention / Win-back** | Frekans düşüşü, olumsuz sinyal | Churn tespiti → kurtarma mesajı → özel teklif → win-back kampanyası | ~120K TL | Phase 3-5 |

---

## Sağlık Niche — Hedef Avatar

> Dr. Ayşe, **Invekto'nun mevcut müşteri profiline** uyuyor.
> Sağlık klinik müşterileri zaten var. Aşağıdaki avatar, mevcut müşterilerin tipik profilini temsil ediyor.
> Bu senaryolar mevcut müşteriye **upsell** (AI/otomasyon) fırsatıdır.

```
İsim: Dr. Ayşe (Mevcut müşteri profili)
İş: İstanbul'da 3 koltuklu özel diş kliniği
Ekip: 1 resepsiyonist, 2 asistan
WhatsApp durumu:
  → Günde 40-60 mesaj geliyor (Instagram reklamları + Google + tavsiye)
  → %70'i fiyat sorusu: "İmplant kaç para?", "Zirkonyum fiyat?"
  → Resepsiyonist mesajlara yetişemiyor, hastayla da ilgileniyor
  → Akşam/hafta sonu mesajlar cevapsız kalıyor → hasta rakibe gidiyor
  → Randevuya gelen hasta, gelmeyenden 10x değerli ama dönüşüm %15
  → No-show oranı %25 → günde 2 boş koltuk = 6.000 TL/gün kayıp
Ciro: ~300.000 TL/ay
Ağrı: Hastaları kaybediyor çünkü hızlı cevap veremiyor
```

---

## Sağlık Senaryoları — Detay

### S6: Fiyat Sorusu → Randevu Dönüşümü (Price-to-Appointment Conversion)

**Sahne:**

Instagram reklamından WhatsApp'a mesaj düşüyor: *"Merhaba, diş implant kaç lira?"*

Bu sağlık sektörünün "kargom nerede"si. Hacmi en yüksek mesaj. Günde 30+ fiyat sorusu geliyor.

**TUZAK:** Fiyat verirsen hasta gidiyor. Çünkü:
- Fiyatı başka klinikle kıyaslıyor (apple-to-orange)
- Muayene olmadan doğru fiyat verilemez
- Fiyat yüksek gelirse bir daha yazmıyor

> Sektörün bildiği gerçek: *"Kliniğe gelen hastanın %70'i tedavi olur. Gelmeyenin %5'i tedavi olur."*
> Yani ASIL HEDEF fiyat vermek değil, **RANDEVU almak**.

**Bugün ne oluyor:**
```
→ Resepsiyonist "İmplant 25.000-45.000 TL arası" yazıyor
→ Hasta "teşekkürler" yazıp gidiyor
→ Dönüşüm: %10-15
→ Günde 30 fiyat sorusu × %12 dönüşüm = 3.6 randevu
→ Kaybedilen: 26 potansiyel hasta/gün
```

**Invekto ile:**
```
→ AI fiyat sorusunu yakalar ama DİREKT FİYAT VERMEZ
→ Bunun yerine:
  "Teşekkürler! İmplant tedavisi hastaya göre farklılık gösterir.
   Size doğru bilgi verebilmemiz için ücretsiz muayene randevusu
   öneriyoruz. Bu hafta Çarşamba 14:00 veya Cuma 10:00 uygun mu?"
→ Hasta "Çarşamba olur" derse → randevu otomatik kayıt
→ Hasta ısrar ederse → fiyat aralığı ver + "kesin fiyat muayenede"
→ 48 saat cevap yoksa → follow-up: "Randevunuzu ayırmamızı ister misiniz?"
```

**Para etkisi:**
```
Dönüşüm %12 → %25'e çıkarsa (sektör benchmark'ı: iyi klinikler %30)
30 soru × %13 artış = 3.9 ek randevu/gün
3.9 × %70 tedavi × 25.000 TL ort. tedavi = 68.250 TL/gün EK GELİR
Aylık: ~1.400.000 TL ek gelir potansiyeli

(Gerçekçi: her gün bu kadar olmaz, ama ayda %20 artış bile =
 60.000 TL/ay ek gelir. Invekto fiyatı 10.000 TL → 6x ROI)
```

> **Hormozi:** *"Sağlık sektöründe fiyat vermek = müşteri kaybetmek. İnsanlar fiyat sorar ama aslında güven ister. AI'ın işi fiyat vermek değil, kapıdan içeri sokmak. Kapıdan giren hastanın %70'i para bırakır."*

---

### S7: No-Show Katili (Appointment No-Show Prevention)

**Sahne:**

Dr. Ayşe'nin günde 12 randevusu var. No-show oranı %25 → günde 3 boş koltuk. Koltuk başına ortalama gelir: 3.000 TL. 3 × 3.000 = 9.000 TL/gün kayıp = **270.000 TL/ay**.

**No-show nedenleri:**
- Unuttum (%40)
- Vazgeçtim ama söylemedim (%30)
- Başka klinikle randevu aldım (%20)
- Gerçek engel (%10)

**Bugün ne oluyor:**
```
→ Resepsiyonist randevudan 1 gün önce arar (40 dakika telefon)
→ %30'una ulaşamıyor
→ İptal eden olursa boş koltuk doldurulamıyor
→ Hasta "geleceğim" diyor ama gelmiyor
```

**Invekto ile:**
```
→ Otomatik hatırlatma zinciri:
  - R-3 gün: "Randevunuz Çarşamba 14:00. Takvime ekleyin [link]"
  - R-1 gün: "Yarın 14:00 randevunuz var. Onaylıyor musunuz? ✓/✗"
  - R-2 saat: "2 saat sonra görüşmek üzere! Adresimiz: [harita link]"
→ Hasta "gelemiyorum" derse:
  - Hemen alternatif tarih öner
  - Boşalan slota bekleme listesinden hasta çağır
→ Cevap vermezse → "iptal riski" olarak işaretle, resepsiyonist arasın
```

**Para etkisi:**
```
No-show %25 → %12'ye düşerse (sektör en iyi: %10)
Günde 1.5 ek dolu koltuk × 3.000 TL = 4.500 TL/gün
= 135.000 TL/ay kurtarılan gelir
```

> **Hormozi:** *"No-show = her gün kasanın önüne para koyup yakmak. 3 mesajla %50 azaltabilirsin. Bu dünyanın en kolay ROI'si. Her klinik sahibi bunu duyunca 'dün neden yoktun?' der."*

**Sektör Varyasyonları (eski Senaryo 29, 55 buraya taşındı):**

| Sektör | No-Show Maliyeti | Hatırlatma Özel Notları |
|--------|-----------------|------------------------|
| **Diş Kliniği** | Koltuk maliyeti ~3.000 TL/randevu, %25 no-show | Tedavi türü belirt (implant vs kontrol), refakatçi hatırlatması, açlık kuralı (cerrahi ise) |
| **Estetik Klinik** | Lead değeri 15-50K TL, kapora sistemi aktif | Kapora ödeme durumu hatırlat, before/after fotoğraf getir, pre-op hazırlık talimatı ekle |
| **Otel** | Oda geliri 1.500-5.000 TL/gece | Check-in saati, ulaşım bilgisi, özel talep hatırlatma (O2 ile ortak) |

---

### S8: Tedavi Sonrası Takip (Post-Treatment Care)

**Sahne:**

Hasta dün implant ameliyatı oldu. Gece WhatsApp'a yazıyor: *"Şişlik normal mi?"*, *"Ağrım var ne yapmalıyım?"*

Dr. Ayşe gece 23:00'te bu mesajı görüyor. Ertesi gün 15 hasta daha tedavi sonrası soru soruyor. Her soru aynı: "şişlik normal mi", "ne yiyebilirim", "ilacı ne zaman alayım". Bu soruların **%90'ı standart**. Ama her biri doktoru/resepsiyonisti meşgul ediyor. Ve cevaplanmayan sorular → hasta paniği → kötü Google yorumu → güven kaybı.

**Bugün ne oluyor:**
```
→ Doktor akşam/gece WhatsApp'tan cevap veriyor (burnout)
→ Resepsiyonist aynı bilgiyi 50 kez yazıyor
→ Bazı hastalar cevap alamıyor → endişe → kötü yorum
→ Doktor zamanı tedavide değil mesajlaşmada gidiyor
```

**Invekto ile:**
```
→ Tedavi tipi bazlı otomatik takip zinciri:
  - T+0 (ameliyat günü akşam):
    "Ameliyatınız başarılı geçti! İlk 24 saat rehberiniz:
     - Hafif şişlik normaldir
     - Soğuk kompres uygulayın
     - [İlaç adı] 8 saatte bir alın
     - Acil durumda [telefon]"
  - T+1 gün: "Bugün kendinizi nasıl hissediyorsunuz?"
    Hasta "şişlik var" → "48 saate kadar normal. Kompres devam."
    Hasta "çok ağrı" → "Hemen doktorumuzu arayın: [telefon]" + doktora alert
  - T+7 gün: "Kontrol randevunuz yaklaşıyor. [tarih] uygun mu?"
  - T+30 gün: "İyileşme nasıl? Memnuniyetinizi öğrenmek isteriz."
→ Doktor sadece GERÇEK acil durumlarda ulaşılıyor
→ Standart sorular AI tarafından %90 oranında çözülüyor
```

**Para etkisi:**
```
Doktor zamanı: günde 1 saat mesaj yazma → 15dk'ya düşer = 45dk kazanç
45dk × 1 ek tedavi yapabilir × 3.000 TL = 3.000 TL/gün
= 90.000 TL/ay (doktorun zamanını tedaviye çevirmek)
+ Hasta memnuniyeti → Google yorum skoru artışı → daha fazla yeni hasta
+ Malpractice riski azalır (takip kayıt altında)
```

> **Hormozi:** *"Doktor mesaj yazarak para kazanmıyor. Doktor tedavi yaparak para kazanıyor. Her dakika mesajlaşmada = koltuktaki para kaybı. AI doktoru mesajdan kurtar, koltuğa oturt."*

**Tedavi Tipine Göre Talimat Şablonları (eski Senaryo 32, 45, 72 buraya taşındı):**

| Tedavi Tipi | T+0 (Aynı gün) | T+1 gün | T+7 gün | T+30 gün |
|-------------|----------------|---------|---------|----------|
| **Diş Çekim** | Kompres, kanama kontrolü, yumuşak gıda | "Şişlik normal mi?" kontrol | Kontrol randevu hatırlatma | İyileşme kontrolü |
| **İmplant** | Ağrı yönetimi, ilaç dozu, sert gıda yasak | Şişlik durumu, ağız bakımı | Dikiş kontrolü randevusu | Osseointegrasyon takip |
| **Botox** | 24h yüz ovma yasak, baş yukarıda yat | Sonuç kontrolü | Tam etki değerlendirme | Tekrar seans hatırlatma |
| **Dolgu** | 48h şişlik normal, masaj yasak | Simetri kontrolü | İyileşme fotoğrafı | Touch-up önerisi |
| **Lazer** | Güneş koruma SPF50+, nemlendirici | Kızarıklık kontrolü | Seans aralığı hatırlatma | Sonraki seans planlama |
| **Rinoplasti** | Tampon bakımı, baş elevasyonu | Ağrı/şişlik takip | Tampon çıkarma randevusu | 1. ay kontrol |
| **Konsültasyon** | — | Karar desteği mesajı | Tedavi planı hatırlatma | "Sorularınız var mı?" follow-up |

---

### S9: Medikal Turizm Lead Yönetimi (Medical Tourism Pipeline)

**Sahne:**

İstanbul saç ekimi / diş / estetik kliniği. Instagram'dan Arapça, İngilizce, Rusça mesajlar geliyor. Her lead değeri: 2.000-10.000 USD (50.000-250.000 TL). Günde 20-30 uluslararası lead geliyor. Karar süreci uzun: 2-8 hafta. Zaman farkı var: hasta gece yazıyor, klinik sabah cevaplıyor → 8 saat gecikme. Hasta 3-4 klinikten aynı anda teklif alıyor.

> **İLK CEVAPLAYAN KAZANIYOR.**

**Bugün ne oluyor:**
```
→ Resepsiyonist İngilizce'de zorlanıyor
→ Arapça mesajlar Google Translate ile çevriliyor
→ Gece gelen mesajlar sabaha cevap buluyor → hasta rakibe gitmiş
→ Takip yapılmıyor → "fiyat aldım ama bir daha yazmadılar"
→ 20 lead'den 2-3'ü randevuya dönüyor (%12)
```

**Invekto ile:**
```
→ 7/24 otomatik cevap (İngilizce/Arapça/Rusça):
  "Thank you for your interest! Here's what we need to give you
   an accurate quote:
   1. A photo of your current situation
   2. Your general health condition
   3. When are you planning to visit Istanbul?"
→ AI hasta cevaplarından ön değerlendirme yapıyor
→ Lead scoring: budget, timeline, readiness
→ Otomatik follow-up zinciri:
  - T+0: İlk cevap (30sn)
  - T+1 gün: "Did you have a chance to send the photos?"
  - T+3 gün: "We prepared a special package for you"
  - T+7 gün: "Last week, 3 patients from [ülke] visited us [before/after]"
→ Sıcak lead → doktora/koordinatöre bildirim
```

**Para etkisi:**
```
Dönüşüm %12 → %20'ye çıkarsa
20 lead × %8 artış = 1.6 ek hasta/gün
1.6 × 80.000 TL ort. tedavi = 128.000 TL/gün
Aylık çalışma günü 25 → 3.200.000 TL/ay ek gelir potansiyeli

(Gerçekçi: her lead her gün gelmez, ama ayda %30 artış bile =
 300.000+ TL/ay ek gelir. Invekto fiyatı 25.000 TL → 12x ROI)
```

> **Hormozi:** *"Medical tourism en yüksek değerli niche. Bir saç ekimi hastası 5.000 USD bırakıyor. İlk cevap veren kliniğe gidiyor. Gece 3'te Suudi'den mesaj geldiğinde 30sn'de AI cevap veriyorsan, sen kazanırsın. 8 saat bekletirsen, rakip kazanır."*

**Sektör Bazlı Paket Örnekleri (eski Senaryo 34, 61 buraya taşındı):**

| Sektör | Paket Örnekleri | Fiyat Aralığı (USD) | Ortak İçerik |
|--------|----------------|---------------------|-------------|
| **Diş** | Veneer paketi (20 diş), İmplant paketi (all-on-4/6), Zirkonyum kaplama | 3.000-15.000 | Konaklama (3-7 gece) + havaalanı transfer + şehir içi ulaşım + kontrol randevusu |
| **Estetik** | Rhinoplasty paketi, Saç ekimi (FUE/DHI), BBL, Meme estetiği | 2.000-10.000 | Konaklama (5-10 gece) + transfer + post-op kontrol + hemşire eşliği |
| **Saç Ekimi** | FUE 3000-5000 greft, DHI, PRP ek tedavi | 1.500-5.000 | Otel (3 gece) + transfer + şapka/ilaç kiti + 6-12 ay online takip |

> **Ortak altyapı:** Multi-language AI (EN/AR/RU/DE), 7/24 ilk cevap, fotoğraf bazlı ön değerlendirme, lead scoring, follow-up zinciri.

---

### S10: Google Yorum Toplama + Referans Motoru (Review Engine)

**Sahne:**

Dr. Ayşe'nin Google'da 4.2 yıldız puanı var. Rakip klinik 4.8. Bu 0.6 fark → Google aramalarda **%40 daha az tıklama**. Hasta klinik seçerken ilk baktığı yer: Google yorumlar.

Her ay 150+ hasta tedavi oluyor. Yorum bırakan: 5-8 hasta (%5). Ve genellikle kötü deneyim yaşayanlar yazıyor → puan düşüyor. Memnun hastalar sessiz kalıyor.

**Bugün ne oluyor:**
```
→ Resepsiyonist bazen "bizi değerlendirir misiniz?" diyor
→ Çoğu hasta "tamam" deyip unutuyor
→ Kötü deneyim yaşayan hemen yazıyor → puan düşüyor
→ Yorum toplama sistematik değil
```

**Invekto ile:**
```
→ Tedavi sonrası otomatik memnuniyet anketi (WhatsApp'tan):
  T+3 gün: "Tedavinizden memnun kaldınız mı? (1-5 puan)"
→ Puan 4-5 ise:
  "Çok mutlu olduk! Deneyiminizi Google'da paylaşır mısınız? [link]"
→ Puan 1-3 ise:
  "Üzgünüz! Sorununuzu çözmek istiyoruz. Ne oldu?"
  → İç eskalasyon → sorun çözülsün, kötü yorum önlensin
→ Referans motoru:
  T+30 gün: "Bir yakınınız tedaviye ihtiyaç duyarsa,
   sizi yönlendirdiğinizde %10 indirim hediyemiz olsun."
→ Dashboard: yorum oranı, ortalama puan trendi, referans sayısı
```

**Para etkisi:**
```
Yorum oranı %5 → %20'ye çıkarsa (memnun hastalar da yazarsa)
Google puanı 4.2 → 4.6 çıkar → tıklama oranı %30 artar
Günde 5 ek lead × %20 dönüşüm × 3.000 TL = 3.000 TL/gün
= 90.000 TL/ay ek gelir (Google görünürlük artışından)

+ Referans programı: ayda 5 referans hasta × 3.000 TL = 15.000 TL/ay
Toplam: 105.000 TL/ay. Invekto 10.000 TL → 10.5x ROI
```

> **Hormozi:** *"Her işletmenin en ucuz müşteri edinme kanalı referans. Ama kimse sistematik olarak istemiyor. 'Bizi değerlendirir misiniz' demek vs. '3 gün sonra WhatsApp'tan memnuniyet anketi → yorum linki → referans teklifi' çok farklı şeyler. Biri umut, diğeri sistem."*

---

## Evrensel Senaryolar — Detay

### S11: Abonelik / Üyelik Modeli (Subscription Revenue)

**Sahne:**

Müşteri düzenli olarak hizmet/ürün alıyor ama her seferinde yeniden satın alma süreci yaşıyor. Bu sürtünme → frekans düşüşü → kayıp.

**Sektör bazlı gerçeklik:**
- **E-ticaret:** Abonelik kutusu (kozmetik, gıda, evcil hayvan). "Her ay aynı kremi alıyorum" → otomatik gönderim teklifi
- **Diş kliniği:** Yıllık kontrol üyeliği. "6 ayda bir diş taşı temizliği" → paket fiyat + hatırlatma
- **Estetik:** Bakım paketi. "3 seans lazer + 6 aylık kontrol" → taksitli plan + otomatik randevu
- **Otel:** Sadakat programı. "Yılda 5+ konaklama = %20 indirim" → tier sistemi + puan
- **Güzellik salonu:** Aylık üyelik. "4 saç bakımı/ay = %30 indirim" → otomatik randevu
- **Eğitim:** Dönemlik kayıt. "2 dönem kayıt = %15 indirim" → erken kayıt kampanyası

**Bugün ne oluyor:**
```
→ Müşteri her seferinde yeniden karar veriyor (satın alma sürtünmesi)
→ Düzenli müşteri ile tek seferlik müşteri aynı muameleyi görüyor
→ Abonelik teklifi yapılmıyor çünkü araç yok
→ "Sadık müşteri" tanımı yok → VIP fiyatlama yapılamıyor
→ Tekrarlayan satın almalar takip edilmiyor → frekans düşüşü fark edilmiyor
```

**Invekto ile:**
```
→ AI tekrarlayan satın alma/ziyaret pattern'ini tespit eder:
  - 3+ kez aynı ürünü alan müşteri → "Aylık otomatik gönderim ister misiniz? %10 indirimli"
  - Düzenli randevu alan hasta → "Yıllık bakım paketi: 6 seans + kontrol = paket fiyat"
→ Outbound Engine ile otomatik hatırlatma:
  - Abonelik yenileme T-7 gün: "Paketiniz sona eriyor, yenilemek ister misiniz?"
  - Tier upgrade teklifi: "5. satın almanız kutlu olsun! Gold üyeye yükseldiniz, %15 indirim"
→ Dashboard: aktif abonelik sayısı, yenileme oranı, tier dağılımı, churn
```

**Para etkisi:**
```
Düzenli müşterilerin %20'si aboneliğe geçerse:
E-ticaret: 100 düzenli müşteri × %20 × 500 TL ortalama = 10.000 TL/ay
Klinik: 50 düzenli hasta × %20 × 2.000 TL paket = 20.000 TL/ay
Toplam 10 müşteri: ~75.000 TL/ay ek düzenli gelir (tek seferlik yerine recurring)
LTV artışı: %40-60 (abonelik müşterisi 2-3x daha değerli)
```

> **Hormozi:** *"Recurring revenue tek seferlik gelirin 3-5x değerinde. Çünkü tahmin edilebilir. Ama çoğu işletme abonelik teklifi bile yapmıyor. Neden? Araçları yok. Aracı ver, geliri tahmin edilebilir yap."*

---

### S12: Churn Prevention / Win-back (Müşteri Kaybı Önleme)

**Sahne:**

Düzenli müşteri sessizleşiyor. Her ay saç boyatan müşteri 3 aydır gelmedi. Her hafta sipariş veren müşteri 1 aydır almadı. Her 6 ayda kontrol yaptıran hasta 1 yıldır gelmedi. **Bu en pahalı kayıp türü — sessiz churn.**

**Sektör bazlı churn sinyalleri:**
- **E-ticaret:** 3+ sipariş geçmişi olan müşteri → 45 gün sipariş yok → churn riski
- **Diş kliniği:** 6 aylık kontrol randevusu → randevu almadı → churn riski
- **Estetik:** Lead pipeline'da sıcak lead → 2 hafta cevap yok → soğuma
- **Otel:** Yılda 3+ konaklama → bu yıl 0 → churn riski
- **Güzellik salonu:** Her 4 haftada gelen müşteri → 8 hafta gelmedi → churn riski
- **Eğitim:** Devamsızlık artışı → 3+ ders kaçırma → kayıp riski

**Bugün ne oluyor:**
```
→ Sessiz churn fark edilmiyor — müşteri şikayet etmeden gidiyor
→ "Bir daha almayacağım" → rakibe gitmiş, biz hâlâ bilmiyoruz
→ Win-back kampanyası yok — müşteri gittikten 6 ay sonra "nerdesiniz" mesajı = çok geç
→ Churn sinyalleri yakalanmıyor:
  * "Neyse", "boş ver", "bir daha uğraşmam" → pasif agresif
  * "Rakip X daha ucuz" → karşılaştırma
  * "Eskiden daha iyiydi" → memnuniyet düşüşü
  * 3+ gün cevap yok (aktif konuşmada) → soğuma
```

**Invekto ile:**
```
→ AI churn risk skorlaması (otomatik):
  - Frekans analizi: satın alma/randevu sıklığı düşüşü tespit
  - Mesaj sentiment: negatif trend, pasif agresif kalıplar
  - Cevapsızlık: aktif konuşmada 3+ gün sessizlik
  - Risk skoru: LOW / MEDIUM / HIGH / CRITICAL

→ Otomatik aksiyon (risk seviyesine göre):
  - MEDIUM: Agent'e "⚠️ dikkat: kayıp riski" badge + müşteri geçmişi özeti
  - HIGH: Supervisor alert + önerilen kurtarma aksiyonu
  - CRITICAL: Outbound kurtarma mesajı:
    "Merhaba [Ad], sizi özledik! Size özel %20 indirim kodunuz: HOSGELDIN20"
    veya "Kontrol randevunuz için sizi bekliyoruz, randevu oluşturmamı ister misiniz?"

→ Win-back kampanyası (Outbound Engine):
  - T+30 gün (ilk hatırlatma): "Sizi özledik" + değer hatırlatma
  - T+60 gün (özel teklif): Kişiselleştirilmiş indirim/hediye
  - T+90 gün (son deneme): "Geri dönüşünüzde ilk [hizmet] ücretsiz"
  - Cevap gelmezse: Pasife al (spam olma)

→ Dashboard: churn risk pipeline, aktif alarm sayısı, kurtarılan vs kaybedilen,
  win-back kampanya dönüşüm oranı, ortalama kurtarma maliyeti
```

**Para etkisi:**
```
Sessiz churn oranı %15'ten %8'e düşürülürse:
E-ticaret: 500 aktif müşteri × %7 kurtarma × 300 TL/ay = 10.500 TL/ay
Klinik: 200 aktif hasta × %7 × 3.000 TL/tedavi = 42.000 TL/ay (yıllık tedavi değeri)
Toplam 10 müşteri: ~120.000 TL/ay korunan gelir
+ Win-back: Geri kazanılan müşterilerin %25'i aktif müşteriye dönüyor
```

> **Hormozi:** *"Yeni müşteri bulmak mevcut müşteriyi tutmaktan 5-7x daha pahalı. Ama çoğu işletme giden müşteriyi fark bile etmiyor. Churn detection yapmak = bedava para toplamak. Çünkü zaten ilişkin var, sadece harekete geçmek lazım."*

---

## Kritik Bulgu: OUTBOUND ENGINE Eksik — Phase 1'de Çözülecek

> Outbound Engine Phase 1'de yapılacak.
> Sebebi: Broadcast = müşterilerin **#1 top 3 talebi**. Mevcut Invekto **sadece tek tek mesaj** gönderebiliyor.
> Toplu mesaj, zamanlı gönderim, follow-up otomasyonu = YOK. Bu senaryoların %70'ini engeller.
> `Invekto.Outbound` (:7107) servisi Phase 1'de doğacak.

```
MEVCUT INVEKTO:
  ✅ Inbound: Unified Inbox, 7 kanal, routing, templates
  ❌ Outbound: Sadece tek tek mesaj gönderimi (broadcast YOK)
  ❌ Otomasyon: Welcome mesajı dışında trigger YOK
  ❌ AI: Agent Assist / chatbot / auto-resolution YOK

EKSİK OLAN = OUTBOUND + OTOMASYON (Phase 1'de çözülecek)
  AI + Outbound = proaktif olarak müşteriye ulaşır

10 senaryodan 7'si OUTBOUND gerektiriyor:
  S1  Yorum kurtarma        → Outbound (AI müşteriye ulaşır)
  S3  İade çevirme           → Inbound (müşteri yazar) + Outbound (follow-up)
  S4  Sipariş sonrası satış  → Outbound (AI ilk mesajı atar)
  S7  No-show önleme         → Outbound (hatırlatma)
  S8  Tedavi sonrası takip   → Outbound (kontrol soruları)
  S9  Medikal turizm         → Inbound + Outbound (follow-up)
  S10 Yorum toplama          → Outbound (rica mesajı)

OUTBOUND ENGINE olmazsa:
  → Gelir potansiyelinin %70'i kilitli kalır
  → Sadece "soruya cevap veren AI" olursun (commodity)
  → Hormozi'nin "proaktif değer yarat" ilkesi ihlal edilir

OUTBOUND ENGINE = Phase 1'de Invekto.Outbound (:7107)
  → Broadcast + zamanlı mesaj + toplu gönderim
  → WhatsApp template compliance zorunlu
  → Trigger sistemi Phase 1'de temel, Phase 2'de gelişmiş
```

---

## Outbound Engine Gereksinimleri

> Outbound Engine `Invekto.Outbound` (:7107) olarak Phase 1'de doğacak.
> Phase 1 = temel broadcast + trigger. Phase 2 = gelişmiş follow-up + zincir.

```
Outbound Engine (Phase 1'de temel, Phase 2'de gelişmiş):

1. Trigger Engine
   ├── Event-based: sipariş teslim edildi, yorum geldi, randevu yaklaşıyor
   ├── Time-based: T+Xh/Xd delay kuralları
   ├── Condition-based: "sadece 1-2 yıldız yorum ise"
   └── Tenant-bazlı ON/OFF ve kural özelleştirme

2. Template Engine
   ├── Değişkenli mesaj şablonları ({müşteri_adı}, {ürün}, {tarih})
   ├── Tenant-bazlı şablon yönetimi
   ├── AI-generated personalization (opsiyonel — Phase 3+)
   └── Multi-language template desteği (Phase 3-4: TR/EN/AR)

3. Delivery & Compliance
   ├── WhatsApp Business API rate limiting (24h window kuralı)
   ├── Opt-out yönetimi ("STOP" → unsubscribe)
   ├── Gönderim kuyruğu + retry logic
   ├── Delivery status tracking (sent/delivered/read/failed)
   └── KVKK/GDPR uyumlu consent tracking

4. Analytics
   ├── Gönderim başarı oranı
   ├── Okunma/cevaplanma oranı
   ├── Conversion tracking (mesaj → aksiyon)
   └── ROI per campaign/trigger
```

---

> Phase detaylarında bu senaryoların hangi adımlarda implemente edildiği: [roadmap-phases.md](roadmap-phases.md)

---

# ============================================================
# 75 SAHA SENARYOSU (invektoV2 — TAM LİSTE)
# Kaynak: ideas/invektoV2.md
# 25 E-ticaret + 25 Diş + 25 Klinik/Estetik
#
# Phase referansları v4.0 roadmap'e göredir.
# Mevcut Invekto yetenekleri: bkz whatisinvekto.md
# ============================================================

INVEKTO (WapCRM) — 3 SEKTÖR / 75 GERÇEK SENARYO + CAPABILITY MAPPING + PROS/CONS
Tarih: 2026-02-07 (Türkiye %60 ağırlık; Avrupa %20; Global %20)

KAYNAKLI GERÇEKLER (sallama yok) — BU ÇALIŞMANIN DAYANDIĞI KURALLAR
- WhatsApp: Müşteri son mesaj attıktan sonraki 24 saat içinde serbest yanıt; 24 saat dışında iş başlatmak için ön onaylı şablon (template) gerekir. (Meta docs / Twilio docs) [WA]
- WhatsApp: Template mesajlar, 24 saat pencere dışında kullanıcıya mesaj atmanın tek yoludur. (Meta docs) [WA]
- WhatsApp: Template kategorileri (Utility/Marketing/Authentication) ve yanlış kategorilendirme riskleri/kuralları (Meta docs, 2025 güncellemesi). [WA]
- Instagram DM API: Kullanıcı mesajıyla başlayan 24 saatlik yanıt penceresi (Messenger/IG policy; respond.io blog). [IG]
- Türkiye Trendyol: İade hakkı 15 gün; iade kontrolü, uygun ise 2–10 iş günü içinde tamamlanabilir; paketleme/iade süreçleri ve sık yaşanan iade/kargo şikayetleri var. (Trendyol akademi/yardım + Şikayetvar örnekleri). [TR]
- Sağlık: WhatsApp üzerinden hasta verisi paylaşımı GDPR/KVKK riskleri; özel nitelikli veri (sağlık) için hukuki dayanak/açık rıza ve süreç gerekir. (KVKK özel nitelikli veri rehberi; literatür). [HEALTH]

Not: Aşağıdaki senaryolarda 'maliyet' ve 'hacim' gibi sayılar şirketten şirkete değişir. Kaynaklı olmayan sayılar 'tahmini' diye işaretlenmiştir.

============================================================
A) AVATAR SETİ (6 PERSONA) — WhatsApp + Instagram DM gerçeğine göre
============================================================

[E1] Mehmet | Bölge: Türkiye | Dikey: E-ticaret / Marketplace
- Rol: Trendyol+Hepsiburada satıcısı (2-5 kişi)
- Kanal: Mesaj kanalı: WhatsApp ağırlıklı, IG DM ikincil
- Temel ağrı: Pain: kargo/iade soruları, iade reddi kavgaları, aynı hattan çok kişi yazması, yanıt gecikmesi
- Uyumluluk/operasyon riski: Risk: template penceresi dışında 'proaktif' mesaj atamama; spam/şikayet; operasyon kaosu

[E2] Ayşe | Bölge: Türkiye/Global | Dikey: E-ticaret / D2C
- Rol: Shopify/WooCommerce growth store (5-20 kişi)
- Kanal: Mesaj kanalı: IG DM lead, WhatsApp support/sales
- Temel ağrı: Pain: DM’den WhatsApp’a geçiş, ödeme linki, sepet terk, stok/beden soruları, SLA
- Uyumluluk/operasyon riski: Risk: IG otomasyon tetik zorunluluğu ve 24 saat penceresi; rate limit; attribution kaybı

[D1] Dr. Burak | Bölge: Türkiye | Dikey: Diş
- Rol: Klinik sahibi (gelir/itibar sorumlusu)
- Kanal: Mesaj kanalı: WhatsApp randevu/plan, IG DM lead
- Temel ağrı: Pain: fiyat sorusu + fotoğrafla 'teşhis' talebi, no-show, tedavi planı takibi, ekip içi karmaşa
- Uyumluluk/operasyon riski: Risk: sağlık verisi KVKK özel nitelikli; hasta foto/rapor paylaşımı; kayıt saklama

[D2] Elif | Bölge: Türkiye | Dikey: Diş
- Rol: Ön büro / hasta koordinatörü
- Kanal: Mesaj kanalı: WhatsApp yoğun, IG DM de var
- Temel ağrı: Pain: aynı anda 30 sohbet, randevu teyidi, iptal/erteleme, yanlış bilgi verme korkusu
- Uyumluluk/operasyon riski: Risk: KVKK onam/aydınlatma; cihaz kaybı; kimin ne dediği kaydı

[A1] Dr. Selin | Bölge: Türkiye/Avrupa | Dikey: Klinik+Estetik
- Rol: Estetik klinik sahibi
- Kanal: Mesaj kanalı: IG DM lead motoru, WhatsApp kapanış
- Temel ağrı: Pain: DM’den randevuya dönüşmüyor, güven sorunu, before/after istekleri, paket satışı
- Uyumluluk/operasyon riski: Risk: sağlık/estetik verisi; yanlış vaat; reklam mevzuatı; gecikme → lead kaçar

[A2] Zeynep | Bölge: Türkiye/Global | Dikey: Klinik+Estetik
- Rol: Operasyon + satış sorumlusu
- Kanal: Mesaj kanalı: IG DM inbound, WhatsApp follow-up
- Temel ağrı: Pain: lead scoring yok, takip kaçıyor, ödeme/kapora, no-show
- Uyumluluk/operasyon riski: Risk: otomasyon spam sayılır; IG 24 saat; KVKK kayıt/erişim kontrolü

============================================================
B) CAPABILITY LİSTESİ (Invekto'nun değerlendirme sözlüğü)
============================================================

- C1: Unified Inbox — WA+IG tek inbox, konuşma listesi, arama, filtre, etiket, açık/kapalı, transfer
- C2: Routing & Workload — sırayla/tesadüfi/en az sohbet/en çok bekleyen; yeni vs eski müşteri yönlendirme
- C3: Templates & Snippets — şablon mesajlar, hazır yanıt kütüphanesi, dosya/görsel/video şablonları
- C4: Reporting Core — yanıt süresi, hacim, agent performansı, etiket bazlı raporlar
- C5: Security Baseline — rol bazlı + page bazlı izin, maskeleme, KVKK/GDPR pratikleri
- C6: Enterprise Security (hedef) — SSO, 2FA, audit log, session timeout, failed login, IP/country allowlist, retention/purge, legal hold
- C7: Knowledge (RAG) (hedef) — PDF/URL/SSS bağla; kaynaklı cevap; gap raporu
- C8: Agent Assist (hedef) — cevap önerisi + kaynak + risk uyarısı + next-best-action
- C9: Auto-Resolution Agent (hedef) — top intents otonom çözüm; düşük güven → insan; not/özet bırakma
- C10: Revenue Agent (hedef) — lead qualify/score; randevu/teklif/ödeme linki; takip akışı
- C11: E-commerce Integrations (hedef) — Trendyol/HB/Shopify/Woo sipariş-iade-kargo senk
- C12: Ads Attribution (hedef) — Click-to-WA/IG kampanya kaynağı; etiket + pipeline
- C13: QA & Mining (hedef) — kalite skor, script uyumu, şikayet temaları, win/loss ifadeleri

============================================================
C) 'BUGÜN' vs 'HEDEF' — Invekto'nun gerçek durumu (sadece konuştuğumuz kadarı)
============================================================

BUGÜN (mevcut): Unified Inbox, routing, etiket, raporlama, şablon kütüphanesi, dosya/görsel/video, maskeleme (bazı), çoklu kanal (WA/IG/FB/Telegram/VOIP/SMS entegrasyonları).
BUGÜN (eksik): SSO/2FA, session timeout, failed login logs (yok), audit log (kısıtlı/varsayım), keyword routing (yok), test routing rules (yok).
HEDEF: Knowledge RAG + Agent Assist + Auto-Resolution + Revenue + yerel e-ticaret entegrasyonları + enterprise security tamamlanması.

============================================================
D) 75 SENARYO (25 e-ticaret + 25 diş + 25 klinik/estetik) — DERİN MOD
============================================================

> **v3 Güncelleme (2026-02-16):** 11 tekrar senaryo silindi (bkz scenarios-review-actions.md A1).
> Kalan aktif senaryo sayısı: 64 (11 silindi + 60 "aktif" değil ama referans kalıyor).
>
> **Tematik Gruplar (A2):** Aşağıdaki senaryolar sektör bölümleri içinde yer almaya devam eder
> ancak ortak mantık gruplarına da aittir:
>
> | Grup | Senaryolar | Ortak Mantık |
> |------|-----------|-------------|
> | **Kargo/Lojistik** | 01, 02, 06, 07, 18, 19, 24 | Kargo takip, hasar, kayıp, gel-al noktası varyasyonları |
> | **Ödeme/Fatura** | 08, 13, 21, 22 | Platform bazlı ödeme sorunları (Trendyol, Shopify, WooCommerce, genel) |
> | **Kriz De-eskalasyon** | 03, 15, 35, 57 | Kızgın müşteri + empati + çözüm. Ortak template referansı |
> | **KVKK/Veri Güvenliği** | 33, 44, 62, 75 | Hasta verisi, foto/rapor, saklama/silme |
> | **Lead Dönüşüm + Sosyal Kanıt** | 60 | IG DM lead yönetimi, speed-to-lead, C10+C12 capability |

Legend:
- Kanıt seviyesi: A=Kaynaklı/yaygın doğrulanmış, B=Yaygın saha pratiği (tekil kanıt zayıf), C=Doğrulanamadı (kullanmamaya çalıştım)
- WA/IG kuralları: 24 saat penceresi + template/opt-in kısıtları senaryolarda dikkate alındı. [WA][IG]

---

============================================================
D-1) E-TİCARET SENARYOLARI (01-25)
============================================================

> E-ticaret = YENİ MÜŞTERİ kazanım niche'i. Trendyol/HB satıcıları + D2C markalar.
> Core otomasyon (Phase 1) ile kargo/iade sorularında hemen fayda.
> Niche-özel: marketplace API entegrasyonu (Phase 2), iade çevirme, B2B lead tespiti.

---
SENARYO 01 — Kargom nerede? takip linki istiyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kargom nerede? Takip linki atar mısın?'
   - Satıcı: (gecikiyor)
   - Müşteri: '2 gündür yazıyorum cevap yok.'

2) Bugün işletme bunu nasıl yönetiyor?
   - WhatsApp'tan sipariş no sorulur
   - Trendyol/Shopify panelinden takip bulunur
   - Link kopyalanıp yapıştırılır

3) Nerede batıyor?
   - Yoğunlukta geç yanıt → müşteri sinirlenir
   - Yanlış siparişe link atılır
   - 24 saat penceresi kapanırsa template gerektirir (WhatsApp kuralı)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) Günlük 30-200 'kargo nerede' mesajı (satıcı ölçeğine göre)
   - (Tahmini) 1 mesaj 2-4 dk → günde 1-8 saat operatör zamanı
   - Kötü yorum/iptal riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + C2 routing + C3 hazır yanıtlarla temsilci hızlanır
   - Sipariş verisi otomatik çekilmediği için temsilci manuel bakar (entegrasyon yoksa)
B) Phase-1 ile ne olur?
   - C8 Agent Assist: 'takip + gecikme özrü' hazır metin + doğru sipariş çekimi
C) Phase-2+ gerektiren
   - C11 (Trendyol/Shopify) sipariş+takip sidebar → 1 tık takip linki (Phase 2)
   - C9 Auto-Resolution: 'kargo nerede' intentini otonom yanıtla (Phase 3)
   - C12 attribution: kampanya kaynağına göre mesaj tonu/kupon (Phase 2 basit / Phase 5 tam)
   - C6 enterprise güvenlik (kurumsal satıcı için) (Phase 4)

Gerekli yetenekler (capability mapping)
   - Phase 1 başlangıç: C1, C2, C3, C8 (template + AI Assist ile kargo bilgisi)
   - Tam set: C1, C2, C3, C8, C11 (+ Trendyol/HB API ile otomatik tracking — Phase 2)

Öneri
   - Takip linkini ve kargo SLA bilgisini 'utility' içerikte tut (spam/marketing değil)
   - Sipariş no istemeden müşteriyi telefon/isim ile eşleştiren match kuralları ekle (hata azaltır)

Pros
   - Cevap süresi düşer → yorum/iptal riski azalır
   - Yanlış link verme azalır
   - Operatör başına kapasite artar
Cons
   - Entegrasyon hatası yanlış bilgi üretirse müşteri daha çok sinirlenir → fallback/human şart
   - WhatsApp 24h pencere dışı proaktif bilgilendirme template maliyeti doğurur

---
SENARYO 02 — İade kodu aldım, hangi kargoya vereceğim?
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade kodu aldım, hangi kargoya vereceğim? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 03 — İadem reddedildi / açılmadı deniyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A | Grup: Kriz De-eskalasyon

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İadem reddedildi, ürünü hiç açmadım!'
   - Müşteri: 'Paramı vermiyorsunuz.'
   - Müşteri: 'Şikayetvar'a yazacağım.'

2) Bugün işletme bunu nasıl yönetiyor?
   - Temsilci sipariş/iade kaydını arar
   - Satıcı/marketplace süreçleri açıklanmaya çalışılır
   - Bazı durumlarda platforma yönlendirilir

3) Nerede batıyor?
   - Duygusal kriz yönetimi zayıfsa alevlenir
   - Kanıt/foto istenir, dosyalar dağılır
   - Yanlış söz verilirse hukuki risk

4) Gerçek maliyet (tahmini ise belirtildi)
   - İtibar kaybı: şikayet siteleri/yorumlar
   - Operatör zamanı + iade lojistik maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + dosya paylaşımıyla kanıt toplanır
   - C2 routing ile 'şikayet' etiketiyle uzman agent'e gider
B) Phase-1 ile ne olur?
   - C8 'de-eskalasyon' cevap önerisi + doğru süreç metni (platforma göre)
C) Phase-2+ gerektiren
   - C11 entegrasyonla iade state ve gerekçe otomatik çekilir (Phase 2)
   - C7 policy+SOP kaynaklı cevap; temsilci sallamaz (Phase 3)
   - C13 mining: en çok kriz çıkaran kalıplar + QA

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8, C13, C11

Öneri
   - Kriz akışı: 1) empati 2) süreç 3) istenen kanıt 4) SLA 5) platform eskalasyonu
   - Özel durumlar için 'insan devri' zorunlu; auto-resolution bu senaryoda riskli

Pros
   - Kriz dili standardize olur → şikayet azalır
   - Yetkin agent'e hızlı yönlendirme
Cons
   - Tam otomasyon yanlış karar verirse daha büyük kriz; bu senaryoda human-in-the-loop şart

---
SENARYO 04 — Kusurlu ürün geldi, değişim mi iade mi?
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kusurlu ürün geldi, değişim mi iade mi? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 05 — Yanlış ürün geldi / eksik parça
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Yanlış ürün geldi / eksik parça ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 06 — Teslim edildi görünüyor ama gelmedi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Teslim edildi görünüyor ama gelmedi ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 07 — Kargo paketi hasarlı, tutanak istiyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kargo paketi hasarlı, tutanak istiyor ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 08 — Fatura/kurumsal fatura talebi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B | Grup: Ödeme/Fatura

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Fatura/kurumsal fatura talebi ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 09 — Adres değişikliği / teslimat saatini değiştir
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Adres değişikliği / teslimat saatini değiştir ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 10 — Kampanyalı aldım, fiyat farkı/kupon sorunu
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kampanyalı aldım, fiyat farkı/kupon sorunu ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 11 — Ürün bedeni/uyumu — Instagram DM’den soru
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Ürün bedeni/uyumu — Instagram DM’den soru ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 12 — Stok var mı? Ne zaman gelir? (D2C)
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Stok var mı? Ne zaman gelir? (D2C) ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 13 — Kapıda ödeme var mı? Havale/IBAN at
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B | Grup: Ödeme/Fatura

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kapıda ödeme var mı? Havale/IBAN at ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 14 — Sepeti terk etti — DM/WA follow-up
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Sepeti terk etti — DM/WA follow-up ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 15 — Ürün açıklamasıyla gelen farklı (görsel uyumsuz)
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: A | Grup: Kriz De-eskalasyon

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Ürün açıklamasıyla gelen farklı (görsel uyumsuz) ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 16 — İade süresi kaç gün? 15 gün kuralı
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade süresi kaç gün?'
   - Müşteri: '15 gün müydü?'
   - Müşteri: 'İadem ne zaman yatar?'

2) Bugün işletme bunu nasıl yönetiyor?
   - Temsilci Trendyol/mağaza politikasını hatırlamaya çalışır
   - Kopyala-yapıştır metin atar
   - İade durumunu panelden kontrol eder

3) Nerede batıyor?
   - Standart bilgi tutarsız verilir
   - İade süreci (2-10 iş günü) gibi detaylar atlanır
   - Müşteri tekrar tekrar yazar

4) Gerçek maliyet (tahmini ise belirtildi)
   - Tekrarlı soru yükü (tahmini): toplam mesajların %10-20'si
   - Yanlış bilgi → şikayet açılır

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C3 şablon ile standart yanıt mümkün
   - C4 raporla bu soruların hacmi görülür
B) Phase-1 ile ne olur?
   - C8 agent assist, müşteri mesajından intent→ doğru politika paragrafını önerir
C) Phase-2+ gerektiren
   - C11 iade durumu canlı çekilir (platform entegrasyonlarına bağlı) (Phase 2)
   - C7 knowledge ile 'iade politikası' tek kaynak; cevap kaynaklı gider (Phase 3)
   - C9 auto-resolution iade sorularını otonom çözer (Phase 3)

Gerekli yetenekler (capability mapping)
   - C3, C7, C8, C4

Öneri
   - Trendyol iade hakkı (15 gün) ve iade tamamlanma (2-10 iş günü) gibi sabit bilgileri knowledge'a koy; kaynağı linkle. [TR]
   - İade durumu çekilemiyorsa net 'panelden bakıp döneceğim' yerine SLA ver

Pros
   - Tutarlı cevap, daha az ping-pong
   - Yeni personel hızlı adapte olur
Cons
   - Politika değişirse knowledge güncellenmezse yanlış bilgi yayılır → versioning şart

---
SENARYO 17 — İade süreci kaç günde biter? 2-10 iş günü
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade süreci kaç günde biter? 2-10 iş günü ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 18 — Gel Al noktası kapalı / iade teslim edemiyorum
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Gel Al noktası kapalı / iade teslim edemiyorum ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 19 — Hepsiburada siparişinde kargo gecikti
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: B | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Hepsiburada siparişinde kargo gecikti ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 20 — Trendyol Express araması vs WhatsApp
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Trendyol Express araması vs WhatsApp ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 21 — Shopify: ödeme başarısız, link tekrar
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B | Grup: Ödeme/Fatura

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Shopify: ödeme başarısız, link tekrar ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 22 — WooCommerce: kargo ücretini iade eder misiniz?
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B | Grup: Ödeme/Fatura

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'WooCommerce: kargo ücretini iade eder misiniz? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 23 — Çoklu müşteri aynı hattan yazıyor — ekip çakışması
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Çoklu müşteri aynı hattan yazıyor — ekip çakışması ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 24 — İade paketi kayboldu / kargo kodu okunmadı
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: B | Grup: Kargo/Lojistik

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade paketi kayboldu / kargo kodu okunmadı ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 25 — Mesaj penceresi kapandı — template ile bilgi verme
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Mesaj penceresi kapandı — template ile bilgi verme ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C8 cevap önerisi
C) Phase-2+ gerektiren
   - C11 entegrasyon varsa veri çekimi (Phase 2)
   - C7 knowledge ile tutarlılık (Phase 3)
   - C9 otonom çözüm (uygunsa) (Phase 3)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---

============================================================
D-2) DİŞ KLİNİĞİ SENARYOLARI (26-50)
============================================================

> Diş klinikleri = MEVCUT MÜŞTERİ tabanı. Core otomasyon (Phase 1) ile hemen fayda.
> Niche-özel: randevu motoru, KVKK sağlık verisi, tedavi planı takibi.
> **11 senaryo silindi** (26→S6, 29→S7, 32→S8, 34→S9, 45→S8, 50→S6/S10). Bkz referanslar.

---
SENARYO 26 — [SİLİNDİ] Fiyat sorusu: implant kaç TL?
> **Bkz S6 (Fiyat → Randevu Dönüşümü).** Bu senaryo S6'nın diş-spesifik varyasyonuydu.
> Diş varyasyon detayları S6'daki "Sektör Varyasyonları" bölümüne taşındı.
> Diş-spesifik: tedavi türleri (implant, zirkonyum, veneer, kanal tedavisi), fiyat aralıkları kişiden kişiye değişir.

---
SENARYO 27 — Instagram DM: 'foto atsam fiyat verir misiniz?'
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Instagram DM: 'foto atsam fiyat verir misiniz?''
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 28 — Randevu alma: uygun saat ve doktor seçimi
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yarın akşam müsait misiniz?'
   - Klinik: 'Hangi işlem?'
   - Hasta: 'İmplant kontrol'

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro boşluklara bakar
   - Elle saat önerir
   - Teyit mesajı atar

3) Nerede batıyor?
   - Çakışan randevu riski
   - Yanıt gecikirse hasta başka yere gider
   - IG/WA 24h pencere içinde hızlı yanıt şart

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) lead kaçışı + boş slot maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + yönlendirme ile hızlı dönüş
   - Şablonlarla standart soru seti
B) Phase-1 ile ne olur?
   - C8: next-best-question (işlem, süre, lokasyon)
C) Phase-2+ gerektiren
   - Takvim/klinik yazılımı entegrasyonu (Phase 2)
   - C9: basit randevu niyetli mesajları otonom yönetme (riskli, kontrollü) (Phase 3)
   - C10 revenue agent: slot önerisi + teyit + kapora linki (Phase 5)

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C8, C10

Öneri
   - Ön büro için 'mini script': 3 soru ile slot öner; sonra teyit ve hatırlatma planla
   - IG DM lead’leri hızlıca WhatsApp’a taşı (tek tık link) ama kullanıcı başlatmalı. [IG]

Pros
   - Yanıt hızı artar → dönüşüm artar
   - Çakışma azalır
Cons
   - Tam otomasyon yanlış slot verirse kriz; insan onayı iyi

---
SENARYO 29 — [SİLİNDİ] No-show: hasta gelmedi, hatırlatma yok
> **Bkz S7 (No-Show Önleme).** Bu senaryo S7'nin diş-spesifik varyasyonuydu.
> Diş varyasyon detayları S7'deki "Sektör Varyasyonları" bölümüne taşındı.
> Diş-spesifik: koltuk maliyeti ~3.000 TL/randevu, %25 no-show oranı.

---
SENARYO 30 — Acil ağrı: gece mesajı, triage gerekiyor
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Acil ağrı: gece mesajı, triage gerekiyor'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 31 — Tedavi planı: 2 seans, süreç anlatımı
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Tedavi planı: 2 seans, süreç anlatımı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 32 — [SİLİNDİ] Öncesi/sonrası bakım talimatı (çekim sonrası)
> **Bkz S8 (Tedavi Sonrası Takip).** Bu senaryo S8'in diş çekim-spesifik varyasyonuydu.
> Tedavi tipine göre talimat detayları S8'deki "Tedavi Tipine Göre Talimat Şablonları" tablosuna taşındı.
> Diş çekim: kompres, kanama kontrolü, yumuşak gıda, ağrı yönetimi.

---
SENARYO 33 — Hasta kimlik/rapor gönderdi: KVKK riski
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Kimliğimi ve röntgenimi atıyorum.'
   - Hasta: (fotoğraf/dosya)
   - Klinik: 'Tamam'

2) Bugün işletme bunu nasıl yönetiyor?
   - Hasta dosyayı WhatsApp'tan yollar
   - Telefon galeride kalır
   - Kim gördü belli olmaz

3) Nerede batıyor?
   - Özel nitelikli sağlık verisi + kimlik → KVKK riski
   - Erişim kontrolü/audit yoksa iç sızıntı riski
   - Cihaz kaybı → veri kaybı

4) Gerçek maliyet (tahmini ise belirtildi)
   - Regülasyon riski (KVKK) + itibar riski
   - Operasyon: dosya aramak/iletmek zaman

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + dosya yönetimi var ama enterprise kontrol eksik kalabilir
   - Maskeleme var ama sağlık verisi için süreç/aydınlatma gerektirir
B) Phase-1 ile ne olur?
   - C8: temsilciye 'hasta verisi isteme/almada uyarı'
C) Phase-2+ gerektiren
   - C7: onam/aydınlatma metinlerini knowledge'dan kaynaklı gönderme (Phase 3)
   - C6: audit log + erişim kontrol + retention policy (Phase 4)
   - Hasta verisini CRM/EMR ile entegre güvenli arşivleme (Phase 4)
   - C13: compliance QA kontrolleri

Gerekli yetenekler (capability mapping)
   - C1, C5, C6, C7, C8, C13

Öneri
   - WhatsApp üzerinden sağlık verisi geliyorsa: 1) aydınlatma + açık rıza metni 2) erişim sınırı 3) otomatik maskeleme/etiketleme 4) retention
   - Hasta dosyasını 'gerektiği kadar' iste; gereksiz veri isteme

Pros
   - Kurumsal/hukuki risk azalır
   - Kayıt tutarlılığı artar
Cons
   - Aşırı sıkı politika kullanıcı deneyimini yavaşlatır; kritik: doğru denge

---
SENARYO 34 — [SİLİNDİ] Yabancı hasta (EU): fiyat + otel/transfer sorusu
> **Bkz S9 (Medikal Turizm Lead Yönetimi).** Bu senaryo S9'un diş-spesifik varyasyonuydu.
> Sektör bazlı paket detayları S9'daki "Sektör Bazlı Paket Örnekleri" bölümüne taşındı.
> Diş-spesifik: veneer paketi (20 diş), implant paketi (all-on-4/6), zirkonyum kaplama + konaklama + transfer.

---
SENARYO 35 — Şikayet: 'dolgu düştü' tekrar randevu
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B | Grup: Kriz De-eskalasyon

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Şikayet: 'dolgu düştü' tekrar randevu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 36 — Ödeme/kapora: rezervasyon için ödeme linki
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Ödeme/kapora: rezervasyon için ödeme linki'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 37 — Sigorta/özel sağlık anlaşması sorusu
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Sigorta/özel sağlık anlaşması sorusu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 38 — Çocuk hasta: korku yönetimi + randevu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Çocuk hasta: korku yönetimi + randevu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 39 — WhatsApp mesajları kayıt altına alma/raporlama ihtiyacı
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'WhatsApp mesajları kayıt altına alma/raporlama ihtiyacı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 40 — Randevu iptal/erteleme yoğunluğu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yarın akşam müsait misiniz?'
   - Klinik: 'Hangi işlem?'
   - Hasta: 'İmplant kontrol'

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro boşluklara bakar
   - Elle saat önerir
   - Teyit mesajı atar

3) Nerede batıyor?
   - Çakışan randevu riski
   - Yanıt gecikirse hasta başka yere gider
   - IG/WA 24h pencere içinde hızlı yanıt şart

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) lead kaçışı + boş slot maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + yönlendirme ile hızlı dönüş
   - Şablonlarla standart soru seti
B) Phase-1 ile ne olur?
   - C8: next-best-question (işlem, süre, lokasyon)
C) Phase-2+ gerektiren
   - Takvim/klinik yazılımı entegrasyonu (Phase 2)
   - C9: basit randevu niyetli mesajları otonom yönetme (riskli, kontrollü) (Phase 3)
   - C10 revenue agent: slot önerisi + teyit + kapora linki (Phase 5)

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C8, C10

Öneri
   - Ön büro için 'mini script': 3 soru ile slot öner; sonra teyit ve hatırlatma planla
   - IG DM lead’leri hızlıca WhatsApp’a taşı (tek tık link) ama kullanıcı başlatmalı. [IG]

Pros
   - Yanıt hızı artar → dönüşüm artar
   - Çakışma azalır
Cons
   - Tam otomasyon yanlış slot verirse kriz; insan onayı iyi

---
SENARYO 41 — Diş beyazlatma kampanyası: IG DM lead
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Diş beyazlatma kampanyası: IG DM lead'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 42 — Doktor meşgul: ön büro cevap veremiyor
Bölge: Avrupa | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Doktor meşgul: ön büro cevap veremiyor'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 43 — Yanlış bilgi: fiyat/plan tutarsızlığı
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yanlış bilgi: fiyat/plan tutarsızlığı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 44 — Hasta fotoğrafları cihazda kalıyor (data loss)
Bölge: Avrupa | Dikey: Diş | Avatar: D1 | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hasta fotoğrafları cihazda kalıyor (data loss)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 45 — [SİLİNDİ] Konsültasyon sonrası takip: 'nasılsınız' mesajı
> **Bkz S8 (Tedavi Sonrası Takip).** Bu senaryo S8'in konsültasyon-spesifik varyasyonuydu.
> S8 tüm post-treatment takip zincirlerini kapsar (T+0, T+1, T+7, T+30).
> Konsültasyon-spesifik: karar desteği, tedavi planı hatırlatma, ikna follow-up'ı.

---
SENARYO 46 — Gece/hafta sonu otomatik cevap (IG/WA 24h)
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Gece/hafta sonu otomatik cevap (IG/WA 24h)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 47 — İkinci görüş: önceki tetkik dosyaları
Bölge: Global | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'İkinci görüş: önceki tetkik dosyaları'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 48 — İade/iptal: kapora geri ister
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'İade/iptal: kapora geri ister'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 49 — Hekim notları + etiketli raporlama
Bölge: Global | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hekim notları + etiketli raporlama'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2+ gerektiren
   - C7: SSS + prosedür açıklamaları (Phase 3)
   - C10: ödeme/kapora + follow-up (Phase 5)
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 50 — [SİLİNDİ] Hasta memnuniyet anketi (opt-in)
> **BAŞLIK/İÇERİK UYUMSUZLUĞU DÜZELTİLDİ:** Başlık "memnuniyet anketi" diyordu ama
> içerik fiyat sorusu/lead yönetimi hakkındaydı (S6 ile aynı mantık).
> Gerçek içerik S6 (Fiyat → Randevu Dönüşümü) kapsamında.
> Memnuniyet anketi işlevi → S10 (Google Yorum + Referans Motoru) kapsamında.

---

============================================================
D-3) ESTETİK KLİNİK SENARYOLARI (51-75)
============================================================

> Estetik klinikler = MEVCUT MÜŞTERİ tabanı + yüksek değer lead'ler (15-50K TL/hasta).
> Niche-özel: IG DM lead pipeline, kapora/ödeme, before/after kanıt, multi-language.
> **5 senaryo silindi** (55→S7, 61→S9, 67→48, 72→S8, 73→60/S10). Bkz referanslar.

---
SENARYO 51 — Instagram DM: 'fiyat nedir?' botox/dolgu
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Instagram DM: 'fiyat nedir?' botox/dolgu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script (Phase 6)

Gerekli yetenekler (capability mapping)
   - Phase 1 başlangıç: C1, C2, C3, C4, C8 (lead tracking + AI Assist + basit raporlama)
   - Tam set: C1, C2, C3, C4, C8, C10, C12 (+ Revenue Agent Phase 5, + Ads Attribution Phase 2/5)

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 52 — Before/after fotoğraf isteği + güven sorusu
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Before/after fotoğraf isteği + güven sorusu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 53 — DM’den WhatsApp’a geçiş: 'numaranızı atın' kayıp
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'DM’den WhatsApp’a geçiş: 'numaranızı atın' kayıp'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 54 — Randevu planlama + kapora
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Randevu planlama + kapora'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 55 — [SİLİNDİ] No-show: hatırlatma ve yeniden kazanım
> **Bkz S7 (No-Show Önleme).** Bu senaryo S7'nin estetik-spesifik varyasyonuydu.
> Estetik varyasyon detayları S7'deki "Sektör Varyasyonları" bölümüne taşındı.
> Estetik-spesifik: lead değeri 15-50K TL, kapora sistemi, IG DM → WA geçiş sonrası no-show riski.

---
SENARYO 56 — Uygunluk/kontrendikasyon soruları
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Uygunluk/kontrendikasyon soruları'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 57 — İşlem sonrası şikayet: şişlik/morarma panik
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B | Grup: Kriz De-eskalasyon

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İşlem sonrası şikayet: şişlik/morarma panik'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 58 — Paket satış: lazer 6 seans
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Paket satış: lazer 6 seans'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 59 — Fiyat pazarlığı + kampanya
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Fiyat pazarlığı + kampanya'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 60 — Yorum/şikayet yönetimi (sosyal kanıt)
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B | Grup: Lead Dönüşüm + Sosyal Kanıt

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Yorum/şikayet yönetimi (sosyal kanıt)'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 61 — [SİLİNDİ] Yabancı hasta (EU): transfer/otel + fiyat
> **Bkz S9 (Medikal Turizm Lead Yönetimi).** Bu senaryo S9'un estetik-spesifik varyasyonuydu.
> Sektör bazlı paket detayları S9'daki "Sektör Bazlı Paket Örnekleri" bölümüne taşındı.
> Estetik-spesifik: rhinoplasty paketi, saç ekimi paketi, botox/filler paketi + konaklama + transfer.

---
SENARYO 62 — KVKK: foto/video sağlık verisi
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Fotoğraf atıyorum, hangi işlem olur?'
   - Lead: (yüz foto)
   - Klinik: 'Tamam'

2) Bugün işletme bunu nasıl yönetiyor?
   - Foto telefonlarda kalır
   - Farklı personel görür
   - Silme/erişim takibi yok

3) Nerede batıyor?
   - Sağlık verisi özel nitelikli; aydınlatma/onam/saklama-silme süreçleri şart
   - Kayıp cihaz riski
   - Gereksiz veri toplama riski

4) Gerçek maliyet (tahmini ise belirtildi)
   - KVKK riski + itibar riski
   - Operasyon: dosya arama/iletme

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + rol bazlı erişim var ama enterprise seviyede audit/retention eksik olabilir
   - Maskeleme mekanizmaları sınırlı kalabilir
B) Phase-1 ile ne olur?
   - C8 uyarı: 'foto istemeden önce onam'
C) Phase-2+ gerektiren
   - C7 knowledge ile aydınlatma metni + rıza akışı (Phase 3)
   - C6 güvenlik seti (audit/retention) (Phase 4)
   - Güvenli arşiv + silme talepleri (DSAR) yönetimi (Phase 4)
   - C13 compliance QA

Gerekli yetenekler (capability mapping)
   - C5, C6, C7, C8, C13

Öneri
   - Foto/sağlık verisini istemeden önce kısa onam + amaç + saklama süresi mesajı gönder
   - Mümkünse ön değerlendirmeyi yapılandırılmış form ile al; DM'de dağılmasın

Pros
   - Regülasyon ve iç sızıntı riski düşer
   - Profesyonel algı artar
Cons
   - Sürtünme artar; conversion düşmemesi için akış çok kısa olmalı

---
SENARYO 63 — Reklam kaynağı: click-to-whatsapp lead
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Reklam kaynağı: click-to-whatsapp lead'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 64 — Mesaj penceresi kapandı: follow-up template
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Merhaba, fiyat?' (dün yazmış)
   - Klinik: (bugün dönüyor)
   - WhatsApp 24h penceresi kapanmış olabilir

2) Bugün işletme bunu nasıl yönetiyor?
   - Geç dönülür
   - Bazı klinikler yine mesaj atar (risk)
   - Bazıları hiç dönmez

3) Nerede batıyor?
   - WhatsApp 24h pencere dışı serbest mesaj atılamaz; template gerekir (kural)
   - Geç yanıt → lead kaçar
   - Yanlış template kategorisi maliyet/ret riski

4) Gerçek maliyet (tahmini ise belirtildi)
   - Lead kaybı
   - Template maliyeti + hesap sağlık riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C3 template desteği ile pencere dışı iletişim yönetilebilir (doğru kategori)
   - C4 ile yanıt gecikmesi raporlanır
B) Phase-1 ile ne olur?
   - C8 agent assist follow-up metni + compliance uyarısı
C) Phase-2+ gerektiren
   - C12 attribution ile geciken lead'leri önceliklendir (Phase 2 basit / Phase 5 tam)
   - C6 enterprise security + audit (özellikle sağlık verisi) (Phase 4)
   - C10 revenue agent: otomatik follow-up + randevu teklif (Phase 5)

Gerekli yetenekler (capability mapping)
   - C3, C4, C8, C12

Öneri
   - Follow-up mesajlarını 'utility' (randevu teyit/ bilgi) çerçevesinde tut; marketing spam yapma
   - IG tarafında kullanıcı tetiklemeden outbound DM atma. [IG]

Pros
   - Kaçan lead'leri geri kazanma şansı
   - Uyumluluk kontrolü ile hesap riski azalır
Cons
   - Yanlış template/yanlış içerik: maliyet + blok riski
   - Otomatik follow-up aşırı olursa spam algısı

---
SENARYO 65 — Çoklu personel: DM/WA'da çakışan cevaplar
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Çoklu personel: DM/WA'da çakışan cevaplar'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 66 — Doktor onayı gereken lead'ler
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Doktor onayı gereken lead'ler'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 67 — [SİLİNDİ] İade/iptal: kapora geri ister
> **Bkz Senaryo 48 (İade/iptal: kapora geri ister — Diş).** Bu senaryo 48'in estetik varyasyonuydu.
> Aynı kapora iade mekanizması: politika açıklama, kısmi iade veya tarih değişikliği önerisi.
> Estetik-spesifik: daha yüksek kapora tutarları (5-15K TL), uluslararası hasta iade komplikasyonları.

---
SENARYO 68 — Ödeme linki + taksit sorusu
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Ödeme linki + taksit sorusu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 69 — İşlem takvimi: yoğun günlerde slot yönetimi
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İşlem takvimi: yoğun günlerde slot yönetimi'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 70 — Müşteri 'doktorla konuşmak istiyorum' baskısı
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Müşteri 'doktorla konuşmak istiyorum' baskısı'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 71 — Ön değerlendirme formu ihtiyacı
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Ön değerlendirme formu ihtiyacı'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 72 — [SİLİNDİ] Post-op bakım talimatı standardizasyonu
> **Bkz S8 (Tedavi Sonrası Takip).** Bu senaryo S8'in estetik post-op varyasyonuydu.
> Tedavi tipine göre talimat detayları S8'deki "Tedavi Tipine Göre Talimat Şablonları" tablosuna taşındı.
> Estetik-spesifik: botox (24h yüz ovma yasak), dolgu (48h şişlik normal), lazer (güneş koruma), rinoplasti (7 gün tampon).

---
SENARYO 73 — [SİLİNDİ] Memnuniyet anketi + referral isteme (opt-in)
> **BAŞLIK/İÇERİK UYUMSUZLUĞU DÜZELTİLDİ:** Başlık "memnuniyet anketi + referral" diyordu
> ama içerik IG DM lead yönetimi/speed-to-lead hakkındaydı (Senaryo 60 ile aynı mantık).
> Gerçek içerik Senaryo 60 (IG DM lead capture + sosyal kanıt) kapsamında.
> Memnuniyet anketi + referral işlevi → S10 (Google Yorum + Referans Motoru) kapsamında.

---
SENARYO 74 — Spam/yanlış tetik: IG otomasyon limitleri
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Spam/yanlış tetik: IG otomasyon limitleri'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 75 — Hasta verisini saklama: kayıt, erişim, silme
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Hasta verisini saklama: kayıt, erişim, silme'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
C) Phase-2+ gerektiren
   - C12 attribution: iyi lead kaynağını gör (Phase 2 basit / Phase 5 tam)
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla (Phase 3)
   - C10 revenue agent: kapora + randevu (Phase 5)
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

============================================================
D-EK) OTEL/TURİZM SENARYOLARI (10 senaryo — MEVCUT MÜŞTERİ)
============================================================

> **Neden ek bölüm?** 75 saha senaryosu 3 niche'e (e-ticaret, diş, estetik) ayrılmıştı.
> Otel/turizm müşterileri ZATEN mevcut tabanın parçası ama ayrı senaryo seti yoktu.
> Core otomasyon (Phase 1) tüm otellere fayda sağlar. Niche-özel = PMS entegrasyonu (Phase 2, basit).
> Bu senaryolar mevcut otel müşterilerine upsell ve yeni otel müşterisi kazanım için.

AVATAR: [O1] Ahmet — Otel Genel Müdürü (3-4 yıldız, 50-150 oda)
- Kanal: WhatsApp ağırlıklı, Booking.com/Google üzerinden lead
- Ağrı: Resepsiyon yoğunluğu, geç yanıt, misafir şikayetleri, yorum yönetimi
- Risk: 24h penceresi, dil bariyeri (yabancı misafir), fiyat hassasiyeti

---
SENARYO O1 — Oda fiyatı ve müsaitlik sorusu
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Müşteri mesajı
   - Misafir: '15-18 Mart arası boş odanız var mı? Gecelik fiyat ne?'

2) Bugün nasıl yönetiliyor?
   - Resepsiyon PMS'e bakıyor, WhatsApp'a dönüyor — 5-10dk
   - Yoğun dönemde mesajlar biriyor, geç cevap → misafir rakibe gidiyor

3) Nerede batıyor?
   - Fiyat bilgisi güncel olmayabilir (PMS'le senkronize değil)
   - Yoğun saatlerde yanıt gecikiyor

4) Gerçek maliyet
   - (Tahmini) Geç cevaplanan her müsaitlik sorusu = kaçan rezervasyon (ortalama 1.500 TL/gece)

Invekto burada:
A) Bugün: C1 inbox + C3 template ile standart fiyat mesajı
B) Phase-1: C8 Agent Assist — sezonluk fiyat listesinden anında cevap önerisi
C) Phase-2+: PMS entegrasyonu ile gerçek zamanlı müsaitlik + fiyat çekme

Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O2 — Check-in bilgilendirme (saat, adres, ulaşım)
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Misafir: 'Check-in saati kaç? Havaalanından nasıl gelirim?'
2) Bugün: Resepsiyon her misafire aynı bilgiyi tekrar yazıyor
3) Nerede batıyor: Tekrarlı iş + gecikme
4) Maliyet: Günde 20-30 tekrarlı soru = 1-2 saat resepsiyon zamanı

Invekto: Phase 1 chatbot ile %100 otomatik cevap (FAQ). Adres + harita linki + shuttle bilgisi.
Gerekli yetenekler: C1, C3, C8

---
SENARYO O3 — Oda yükseltme / ekstra hizmet talebi
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Misafir: 'Deniz manzaralı odaya geçebilir miyim? Spa paketiniz var mı?'
2) Bugün: Resepsiyon önbüroyla konuşuyor, geç dönüyor
3) Invekto: C8 Agent Assist ile upsell cevap önerisi + PMS'ten müsait üst kategori check (Phase 2)
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O4 — Misafir şikayeti (oda temizliği, gürültü, arıza)
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: A

1) Misafir: 'Odada sıcak su yok!' / 'Klima çalışmıyor'
2) Bugün: WhatsApp'tan yazıyor, resepsiyon teknik servisi arıyor — 30dk-1saat
3) Nerede batıyor: Şikayet büyür → kötü Google yorumu
4) Maliyet: Her kötü yorum = potansiyel 5-10 kaçan rezervasyon

Invekto: C8 empati mesajı + otomatik departmana yönlendirme (C2 routing tag: 'şikayet'). Çözüm sonrası memnuniyet anketi (Outbound).
Gerekli yetenekler:
   - Phase 1 başlangıç: C1, C2, C3, C8 (routing + empati template)
   - Tam set: C1, C2, C3, C8, C13 (+ QA Mining ile şikayet kalıbı analizi — Phase 6)

---
SENARYO O5 — Rezervasyon iptal/değişiklik
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Misafir: 'Rezervasyonumu 1 gün ertelemek istiyorum' / 'İptal etmek istiyorum'
2) Bugün: Resepsiyon Booking.com veya PMS'e giriyor, iptal/değişiklik politikasını yazıyor
3) Invekto: C8 iptal politikası önerisi + C7 Knowledge (iptal kuralları). PMS entegrasyonu ile otomatik (Phase 2+).
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O6 — Yabancı misafir (İngilizce/Almanca/Rusça)
Bölge: Türkiye/Global | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: A

1) Misafir: 'Hello, do you have availability for next week? Airport transfer available?'
2) Bugün: Resepsiyon İngilizce'de zorlanıyor, Google Translate kullanıyor
3) Nerede batıyor: Yanlış çeviri → yanlış bilgi → kötü deneyim
4) Maliyet: Yabancı misafir ortalama 2x gelir (yüksek fiyat + ekstra hizmetler)

Invekto: Phase 1 AI dil algılama + Phase 3 multi-language cevap. Medikal turizm altyapısıyla ortak.
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O7 — Check-out sonrası yorum rica
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Misafir check-out yaptı. Otomatik mesaj: 'Konaklama nasıldı? (1-5 puan)'
2) Bugün: Hiçbir şey yapılmıyor. Kötü deneyim → Google'a kötü yorum, iyi deneyim → sessizlik.
3) Invekto: Outbound Engine ile T+1 gün memnuniyet anketi → puan 4-5 ise Google yorum linki, 1-3 ise iç eskalasyon.
   S10 (Google Yorum + Referans) otel için de geçerli.
Gerekli yetenekler:
   - Phase 2 başlangıç: C1, C3, C8 (Outbound anket + template)
   - Tam set: C1, C3, C8, C13 (+ Memnuniyet trend analizi — Phase 6)

---
SENARYO O8 — Shuttle / transfer rezervasyonu
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Misafir: 'Havaalanından shuttle var mı? Saat kaçta gelmem lazım?'
2) Bugün: Resepsiyon aracı organize ediyor, WhatsApp'tan bilgi veriyor — tekrarlı
3) Invekto: Phase 1 chatbot (FAQ + template). Phase 2+ PMS shuttle modülü ile entegre.
Gerekli yetenekler: C1, C3, C7, C8

---
SENARYO O9 — Grup/kurumsal rezervasyon talebi
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Müşteri: '40 kişilik bir grup için 3 gece fiyat alabilir miyim?'
2) Bugün: Bu mesaj diğer mesajlar arasında kaybolur. Grup talebi = yüksek gelir potansiyeli.
3) Invekto: S5 (B2B Lead Tespiti) ile aynı mantık — VIP flag + sales alert + özel teklif akışı.
Gerekli yetenekler: C1, C2, C3, C8

---
SENARYO O10 — Sezonluk kampanya / early bird duyurusu
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B

1) Otel yaz sezonu için early bird kampanyası başlatmak istiyor.
2) Bugün: Manuel tek tek mesaj gönderiyor. 500+ eski misafir listesi var ama toplu mesaj atamıyor.
3) Invekto: Outbound Engine ile segment bazlı kampanya (geçen yaz gelenler → early bird fiyatı). WhatsApp template compliance zorunlu.
Gerekli yetenekler: C1, C3, C4, C8

---
SENARYO O11 — Oda servisi siparişi (in-stay)
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B
Kaynak: scenarios-review-actions.md B4.1

1) Misafir: 'Odaya kahvaltı gönderin' / 'Gece oda servisi menünüz var mı?'
2) Bugün: Resepsiyon notu alıyor, mutfağa söylüyor, karışıklık ve gecikme oluyor. Gece shift'inde atlanabiliyor.
3) Nerede batıyor: Sipariş kaybolması, tahmini süre verilmemesi, otel içi hizmet gelir fırsatı kaçırılması.
4) Maliyet: (Tahmini) Her kaçırılan oda servisi = 100-300 TL gelir kaybı

Invekto burada:
A) Phase 1 (template): Menü gönderme + sipariş onay template'i + tahmini süre bilgisi
B) Phase 2 (POS entegrasyon): POS/mutfak sistemiyle entegre — otomatik iş emri + durum takibi
Gerekli yetenekler: C1, C3, C8

---
SENARYO O12 — Housekeeping talebi (in-stay)
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: A
Kaynak: scenarios-review-actions.md B4.2

1) Misafir: 'Odaya havlu lazım' / 'Temizlik yapılmamış' / 'Yastık değişimi istiyorum'
2) Bugün: Resepsiyon notu alıp housekeeping'e söylüyor — gecikme + unutma riski yüksek.
3) Nerede batıyor: En sık in-stay talebi. Unutulursa → şikayet → kötü yorum.
4) Maliyet: Housekeeping gecikmesi = şikayet eskalasyonu, Google'da 1 yıldız farkı = %5-9 doluluk farkı

Invekto burada:
A) Phase 1: AI mesaj algılama → housekeeping departmanına otomatik routing (C2 tag: 'housekeeping')
B) Çözüm sonrası: 'Talebiniz karşılandı mı?' otomatik follow-up (Outbound, T+1saat)
Gerekli yetenekler: C1, C2, C3, C8

---
SENARYO O13 — Spa/restoran rezervasyonu (in-stay)
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B
Kaynak: scenarios-review-actions.md B4.3

1) Misafir: 'Akşam 8'de 2 kişilik masa ayırtabilir miyim?' / 'Yarın 15:00'te masaj randevusu'
2) Bugün: Resepsiyon ilgili departmanı arıyor, müsaitlik soruyor, misafire dönüyor — 10-30dk gecikme.
3) Nerede batıyor: Slot çakışması, resepsiyonun departmanı aramaması, misafirin vazgeçmesi.
4) Maliyet: (Tahmini) Kaçırılan spa/restoran rezervasyonu = 300-1000 TL/misafir upsell fırsatı

Invekto burada:
A) Phase 2: Slot yönetimi (diş/estetik randevu motoruyla ortak altyapı) + otomatik onay mesajı
B) Phase 1 (geçici): Template ile bilgi toplama + manuel konfirmasyon
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O14 — Late check-out / early check-in talebi
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: A
Kaynak: scenarios-review-actions.md B4.4

1) Misafir: 'Uçuşumuz akşam, geç çıkış yapabilir miyiz?' / 'Saat 10'da geliyoruz, oda hazır mı?'
2) Bugün: Resepsiyon PMS'e bakıyor, müdüre soruyor, geç dönüyor. Otomatik bir süreç yok.
3) Nerede batıyor: En sık talep. Misafir cevap beklerken vazgeçiyor. Ücretli seçenek sunulmuyor.
4) Maliyet: (Tahmini) Late check-out = saf kâr (oda zaten boş ise 500-1500 TL ek gelir). Upsell fırsatı.

Invekto burada:
A) Phase 1 (template): Müsaitlik bilgisi + ücretli/ücretsiz seçenek sunma template'i
B) Phase 2 (PMS): Gerçek zamanlı oda müsaitlik kontrolü + otomatik onay/ret
Gerekli yetenekler: C1, C3, C7, C8

---
SENARYO O15 — Fatura/ödeme sorunları
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B
Kaynak: scenarios-review-actions.md B4.5

1) Misafir: 'Faturada hata var' / 'Ekstra ücret ne için?' / 'Kurumsal fatura keser misiniz?'
2) Bugün: Muhasebe ile iletişim, e-posta zincirleri, geç dönüş — misafir check-out'ta bekliyor.
3) Nerede batıyor: Para konusu = hassas. Geç cevap → hukuki risk + kötü yorum.
4) Maliyet: Fatura anlaşmazlığı = chargeback riski + itibar kaybı

Invekto burada:
A) Phase 1: AI bilgilendirme (genel fatura politikası + kurumsal fatura süreci) + muhasebe eskalasyonu
B) Phase 2 (ödeme gateway): iyzico/Param entegrasyonu ile ödeme linki gönderme
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O16 — OTA mesaj entegrasyonu (Booking.com / Expedia)
Bölge: Türkiye/Global | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B
Kaynak: scenarios-review-actions.md B4.6

1) Durum: Oteller sadece WhatsApp'tan değil, Booking.com ve Expedia mesaj kanallarından da mesaj alıyor.
   Bu kanal Invekto'da TANIMLI DEĞİL — tamamen kör nokta.
2) Bugün: Otel personeli Booking extranet'e ayrı giriyor, ayrı cevaplıyor. Unified Inbox dışında.
3) Nerede batıyor: Mesaj gecikme → Booking ceza puanı, düşük reply rate → listing sıralaması düşer.
4) Maliyet: Booking reply rate <%90 = listing cezası, düşük sıralama = daha az rezervasyon

Invekto burada:
A) Phase 2: Booking.com Connectivity API ile mesaj kanalını Unified Inbox'a entegre
B) Phase 3: Expedia Partner Central API desteği
C) Aynı AI + routing kuralları OTA mesajlarına da uygulanır
Gerekli yetenekler: C1, C2, C3, C7, C8

---
SENARYO O17 — Özel gün/kutlama organizasyonu
Bölge: Türkiye | Dikey: Otel/Turizm | Avatar: O1 | Kanıt: B
Kaynak: scenarios-review-actions.md B4.7

1) Misafir: 'Eşimin doğum günü, surprise yapar mısınız?' / 'Balayı organizasyonu istiyoruz'
2) Bugün: Resepsiyon el yordamıyla organize ediyor. Standart süreç yok, kaçırılma riski var.
3) Nerede batıyor: Yüksek değer, düşük hacim. Kaçırılırsa = premium misafir memnuniyetsizliği.
4) Maliyet: Başarılı organizasyon = tekrar gelen misafir + referans. Upsell: pasta, çiçek, oda dekorasyonu (500-3000 TL ek gelir).

Invekto burada:
A) Phase 3: VIP etiket + özel gün tespit (AI intent: kutlama/organizasyon) → koordinatöre routing
B) Upsell template: pasta/çiçek/dekorasyon seçenekleri + fiyat bilgisi
Gerekli yetenekler: C1, C2, C3, C8

---

**Otel Niche Özeti:**

| Senaryo | Phase | Bağımlılık |
|---------|-------|------------|
| O1: Oda fiyatı/müsaitlik | Phase 1 (template) / Phase 2 (PMS) | PMS entegrasyonu |
| O2: Check-in bilgi | Phase 1 (chatbot FAQ) | — |
| O3: Oda yükseltme/upsell | Phase 2 (PMS) | PMS entegrasyonu |
| O4: Şikayet yönetimi | Phase 1 (routing + AI) | — |
| O5: İptal/değişiklik | Phase 1 (template) / Phase 2 (PMS) | PMS entegrasyonu |
| O6: Yabancı misafir | Phase 3 (multi-language) | Multi-lang AI |
| O7: Yorum rica | Phase 2 (Outbound) / Phase 5 (yorum motoru) | Outbound Engine |
| O8: Shuttle/transfer | Phase 1 (chatbot FAQ) | — |
| O9: Grup rezervasyon | Phase 2 (B2B lead tespiti) | — |
| O10: Kampanya broadcast | Phase 1 (Outbound broadcast) | Outbound Engine |
| O11: Oda servisi siparişi | Phase 1 (template) / Phase 2 (POS) | POS entegrasyonu |
| O12: Housekeeping talebi | Phase 1 (routing) | — |
| O13: Spa/restoran rezervasyonu | Phase 2 (slot yönetimi) | Randevu motoru altyapısı |
| O14: Late check-out / early check-in | Phase 1 (template) / Phase 2 (PMS) | PMS entegrasyonu |
| O15: Fatura/ödeme sorunları | Phase 1 (eskalasyon) | — |
| O16: OTA mesaj entegrasyonu | Phase 2-3 (Booking.com API) | Booking Connectivity API |
| O17: Özel gün/kutlama | Phase 3 (VIP routing) | — |

> **Sonuç:** Oteller için Phase 1 chatbot + Outbound'dan büyük fayda var (O2, O4, O8, O10, O11, O12, O14, O15).
> PMS entegrasyonu Phase 2'de otel niche'ini farklılaştırır (O1, O3, O5, O14).
> In-stay senaryoları (O11, O12, O13) misafir otel içindeyken fayda sağlar.
> OTA entegrasyonu (O16) otel sektörünün en büyük kör noktasını kapatır.
> Yabancı misafir desteği estetik/medikal turizm altyapısını ortak kullanır (O6).

============================================================
D-EK-2) MOBİL UYGULAMA SENARYOLARI (5 senaryo — TÜM SEKTÖRLER)
============================================================

> Phase 7'de planlanan mobil uygulama için kullanıcı senaryoları.
> Bu senaryolar sektör bağımsız — tüm niche'lerde geçerli.

---
SENARYO M1 — Sahada/evde mesaj yönetimi
Tüm sektörler | Kanıt: A (Top 3 müşteri talebi)

1) Durum: E-ticaret satıcısı depo ziyaretinde, diş doktoru öğle yemeğinde, estetik koordinatör sahada.
   Masabaşında değil ama VIP lead veya acil mesaj geldi.
2) Bugün: Bilgisayara dönene kadar mesaj cevapsız kalıyor. Mobil tarayıcıda web uygulaması kullanışsız.
3) Invekto Mobil: Push notification + konuşma listesi + AI cevap önerisi → 1 dokunuşla cevapla.
Gerekli yetenekler: C1, C2, C3, C8 (mobil arayüzden mevcut API'leri tüketir)

---
SENARYO M2 — Acil mesaj push notification
Tüm sektörler | Kanıt: A

1) Durum: SLA breach yaklaşıyor veya VIP müşteri/hasta yazdı.
2) Bugün: Bilgisayar başında değilsen haberin olmuyor. Müşteri bekliyor.
3) Invekto Mobil: Push notification (SLA breach, VIP, acil etiket) → hemen müdahale.
Gerekli yetenekler: C1, C2 (push notification altyapısı + routing kuralları)

---
SENARYO M3 — Supervisor sahada ekip izleme
Tüm sektörler | Kanıt: B

1) Durum: Supervisor dışarıda ama ekibin performansını görmek istiyor.
2) Bugün: "Şu an ne durumda?" diye temsilciyi arayıp soruyor.
3) Invekto Mobil: Basit dashboard (bekleyen mesaj, ortalama yanıt süresi, aktif agent sayısı).
Gerekli yetenekler: C4 (raporlama API'leri mobilde gösterim)

---
SENARYO M4 — Doktor/klinik sahibi mesai dışı acil triage
Diş + Estetik | Kanıt: A

1) Durum: Hasta gece "şişlik var, normal mi?" yazıyor. Doktor evde.
2) Bugün: Doktor kişisel telefonundan WhatsApp'tan cevaplıyor (kişisel/iş karışıyor) veya sabaha erteliyor.
3) Invekto Mobil: AI ön cevap + doktora push notification (sadece "acil" etiketli). Doktor mobil uygulamadan
   resmi Invekto kanalıyla cevap verir → kayıt altında, KVKK uyumlu.
Gerekli yetenekler: C1, C2, C8, C5/C6 (acil routing + AI ön cevap + KVKK uyumlu kayıt)

---
SENARYO M5 — Satıcı hareket halinde sipariş yönetimi
E-ticaret | Kanıt: B

1) Durum: Satıcı depoda, fuarda veya toplantıda. "Kargom nerede?" mesajları biriyor.
2) Bugün: Dizüstü bilgisayar açamıyor, cep telefonundan web girişi zor.
3) Invekto Mobil: Konuşma listesi + AI suggest + 1 dokunuşla cevap. Sipariş kartı (Integrations'tan).
Gerekli yetenekler: C1, C2, C3, C8, C11 (sipariş kartı için Integrations API gerekli)

---
SENARYO M6 — QR kod ile hızlı WhatsApp erişimi
Tüm sektörler | Kanıt: B
Kaynak: scenarios-review-actions.md B5.1

1) Durum: Otel odasında, restoran masasında, klinik bekleme salonunda, mağazada
   QR kod taranıyor → direkt WhatsApp konuşması başlıyor.
2) Bugün: Müşteri/misafir WhatsApp numarasını araştırmak zorunda veya telefon numarasını soruyor.
3) Invekto Mobil: Fiziksel → dijital köprü. QR kod tenant bazlı unique link içerir.
   Ek fayda: QR tarama anında opt-in toplama fırsatı (ilk mesajda rıza akışı tetiklenir).
4) Kullanım alanları:
   - Otel: oda kapısı QR → housekeeping/oda servisi
   - Klinik: bekleme salonu QR → randevu bilgi/form
   - E-ticaret: ürün kutusu QR → destek/iade başlatma
Gerekli yetenekler: C1, C3 (QR link generator + tenant bazlı deep link)

---
SENARYO M7 — Çevrimdışı mod (offline message drafting)
Tüm sektörler | Kanıt: B
Kaynak: scenarios-review-actions.md B5.2

1) Durum: Saha çalışanı (depo, fuar), doktor ameliyatta, satıcı uçuşta — internet yok.
2) Bugün: Mesajlar birikir, internet gelince hepsine birden bakmak gerekir. Kaçan mesajlar SLA breach'e yol açar.
3) Invekto Mobil: Önceki mesajları offline okuma + taslak kaydetme + sync olunca otomatik gönderme.
   Offline dönemde AI öneri çalışmaz (internet gerekli) ama taslak editlenebilir.
Gerekli yetenekler: C1, C3 (local storage + sync queue)

---

**Mobil Senaryo Özeti:**
- M1-M3: TÜM sektörlerde geçerli (Phase 7)
- M4: Sağlık niche'i için kritik (doktor mesai dışı erişim)
- M5: E-ticaret niche'i için faydalı (hareket halinde yönetim)
- M6: Fiziksel→dijital köprü, tüm sektörlerde opt-in toplama fırsatı (Phase 7)
- M7: Çevrimdışı erişim, saha çalışanları için kritik (Phase 7)
- **Tüm senaryolar mevcut API'leri tüketir — yeni backend gerekmez (M6 QR generator hariç).**

============================================================
D-EK-3) CROSS-SECTOR KRİTİK SENARYOLAR (B1 — 8 senaryo — TÜM SEKTÖRLER)
============================================================

> **Kaynak:** scenarios-review-actions.md B1 bölümü
> **Neden ayrı bölüm?** Bu senaryolar tek bir sektöre ait değil — TÜM sektörlerde zorunlu altyapı.
> 5 bağımsız AI review raporunun 4'ü bu eksiklikleri tespit etti.
> Bu senaryolar olmadan outbound, AI routing ve compliance senaryolarının çoğu ÇALIŞMAZ.

---
SENARYO CS-01 — Opt-in toplama ve onam yönetimi
Tüm sektörler | Kanıt: A (BLOCKER) | Phase: 1 (Outbound prerequisite)
Kaynak: scenarios-review-actions.md B1.1 | Tespit: Rapor 4 (ana), Rapor 1, 5

1) Sorun: WhatsApp Business Policy gereği, 24 saat penceresi dışında template mesaj göndermek için
   müşteriden AÇIK ONAM (opt-in) alınması gerekiyor. Bu onamın nasıl toplandığı, nerede saklandığı,
   nasıl yönetildiği bağımsız bir senaryo/workflow olarak tanımlanmamış.

   > Mevcut referanslar: Opt-in kavramı kaynak dokümanda parçalı olarak mevcut —
   > senaryo başlıklarında (50, 73 "(opt-in)"), Outbound Engine gereksinimlerinde
   > "KVKK/GDPR uyumlu consent tracking", ve opt-out yönetimi. Eksik olan:
   > opt-in'in nasıl TOPLANDIĞI, nerede SAKLANDI, nasıl YÖNETİLDİĞİ konusunda dedicated workflow.

2) Bu olmadan ÇALIŞMAYAN senaryolar:
   - S4 (Sipariş sonrası proaktif satış) — outbound
   - S7 (No-show hatırlatma) — outbound
   - S10 (Yorum rica) — outbound
   - O7 (Check-out sonrası yorum) — outbound
   - O10 (Sezonluk kampanya) — outbound
   - Tüm follow-up zincirleri

3) Workflow:
   - Opt-in toplama kanalları: ilk WA mesajında, web formunda, sipariş onayında, randevu formunda, QR (M6)
   - DB saklama: müşteri profilinde `wa_opt_in: true/false, date, source, category`
   - Opt-out yönetimi: "STOP" mesajı → otomatik unsubscribe (Outbound mevcut: STOP/DUR/İPTAL)
   - Kategori bazlı onam: utility vs marketing template ayrımı (Meta policy)
   - Compliance log: kim, ne zaman, hangi kanaldan opt-in verdi (audit trail)

4) Etki: BLOCKER — bu olmadan outbound senaryolarının hiçbiri yasal olarak çalışmaz
Gerekli yetenekler: C1, C3, C5/C6 (consent management + audit log)

---
SENARYO CS-02 — AI → İnsan handoff (eskalasyon kuralları)
Tüm sektörler | Kanıt: A (BLOCKER) | Phase: 1 (AI Assist prerequisite)
Kaynak: scenarios-review-actions.md B1.2 | Tespit: Rapor 1, 2, 3, 4, 5 (TÜM raporlar)

1) Sorun: AI'nın çözemediği, emin olmadığı veya hassas konularda insana devretme mekanizması.
   Parçalı referanslar var (Automation handoff mevcut) ama tutarlı bir cross-sector framework yok.

2) Bu olmadan riskler:
   - AI yanlış tıbbi bilgi verirse → malpractice
   - AI yanlış fiyat verirse → yasal risk
   - AI krizde yanlış yaklaşırsa → müşteri kaybı
   - AI kapasitesini aşarsa → sessizlik → churn

3) Handoff tetikleyicileri:
   - AI confidence < threshold (örnek: %60)
   - Belirli intent'ler: tıbbi tavsiye, hukuki, fiyat kesinleştirme
   - Müşteri açıkça "insanla konuşmak istiyorum" dediğinde
   - Sentiment skoru kritik eşiği aştığında
   - Aynı konuda 3+ mesaj döngüsü (AI çözemiyor)

4) Context aktarımı: AI'nın topladığı bilgi (intent, sentiment, müşteri profili, konuşma özeti) insana transfer
5) Handoff UX: "Sizi uzman arkadaşımıza yönlendiriyorum" mesajı
6) Geri dönüş: İnsan çözdükten sonra AI özete kayıt yazar (knowledge loop)
7) SLA: Handoff sonrası insan max X dakikada cevap vermeli

Etki: BLOCKER — bu olmadan AI güvenilir değildir
Gerekli yetenekler: C1, C2, C8 (confidence routing + context handoff + SLA tracking)

---
SENARYO CS-03 — AI hallucination guardrail
Tüm sektörler | Kanıt: A | Phase: 1 (AI Assist ile birlikte)
Kaynak: scenarios-review-actions.md B1.3 | Tespit: Rapor 2 (ana), Rapor 3, 5

1) Sorun: AI'nın tıbbi, finansal veya hukuki konularda yanlışlıkla kesin/yanıltıcı bilgi üretmesi.

2) Risk örnekleri (sektör bazlı):
   - Diş: "İmplant 25.000 TL" dedi AI ama doktor 45.000 TL yazıyor → güven kaybı
   - Estetik: "Hamileyken botox yapılabilir" derse → sağlık riski → dava
   - E-ticaret: "İadeniz onaylandı" dedi ama iade koşullarını karşılamıyor → operasyonel kaos
   - Otel: "Oda müsait" dedi ama dolu → misafir geldi, oda yok → crisis

3) Guardrail mekanizmaları:
   - "Bilmiyorum" kapasitesi: AI emin olmadığı konuda "Bu konuda kesin bilgi veremiyorum,
     sizi uzmanımıza yönlendiriyorum" demeli
   - Konu bazlı guardrail listesi:
     * Tıbbi tavsiye → ASLA kesin diagnosis verme
     * Fiyat → "aralık" ver, "kesin fiyat muayenede/görüşmede belirlenir" ekle
     * İlaç/dozaj → ASLA öneri yapma, doktora yönlendir
     * Hukuki (iade hakkı, garanti) → knowledge base'den kaynak göster, yorum ekleme
   - Confidence-based routing: düşük confidence → human handoff (CS-02 ile entegre)
   - Audit log: AI'nın verdiği her cevabın kaydı + confidence skoru

4) Etki: YÜKSEK — yasal risk azaltma, güven inşa
Gerekli yetenekler: C7, C8 (knowledge-grounded response + confidence scoring + audit)

---
SENARYO CS-04 — SLA watchdog / failover
Tüm sektörler | Kanıt: A | Phase: 1-2
Kaynak: scenarios-review-actions.md B1.4 | Tespit: Rapor 3 (ana), Rapor 1

1) Sorun: Mesaj bekleme süresi aşıldığında, agent offline olduğunda veya AI cevap üretemediğinde
   otomatik müdahale mekanizması yok.

2) Risk senaryoları:
   - Mesaj 5dk+ cevapsız → müşteri gider (e-ticaret dönüşüm %5'e düşer)
   - Agent hastalandı, hepsi offline → mesajlar birikiyor, kimse fark etmiyor
   - AI servisi down → sessizlik → müşteri "bozuk mu bu?" der
   - VIP lead 1 saat bekliyor → rakibe gitmiş

3) SLA kuralları (tenant bazlı konfigüre edilebilir):
   - Genel: 5dk içinde ilk yanıt
   - VIP: 2dk içinde ilk yanıt
   - Acil (sağlık): 1dk içinde

4) Watchdog mekanizması:
   - SLA süresi dolmadan 1dk → uyarı (agent'e push)
   - SLA süresi doldu → otomatik eskalasyon (supervisor'a)
   - SLA 2x aşıldı → emergency routing (müsait herhangi agent veya AI fallback mesajı)

5) AI failover:
   - AI servisi down → "Şu an yoğunuz, en kısa sürede döneceğiz" template mesajı
   - AI 3 kez üst üste düşük confidence → human routing

6) Dashboard metrikleri: SLA breach sayısı, ortalama bekleme, breach recovery süresi
7) Etki: YÜKSEK — operasyonel güvenilirlik
Gerekli yetenekler: C1, C2, C4, C8 (SLA timer + escalation routing + dashboard metrics)

---
SENARYO CS-05 — Churn sinyali tespiti
Tüm sektörler | Kanıt: B | Phase: 3 (sentiment altyapısı gerekir)
Kaynak: scenarios-review-actions.md B1.5 | Tespit: Rapor 2, 3

1) Sorun: Müşterinin açıkça şikayet etmeden terk etme sinyalleri veren cümlelerinin
   AI ile tespit edilip proaktif müdahale yapılması.

2) Churn sinyal kelimeleri/pattern'leri:
   - Pasif agresif: "neyse", "boş ver", "bir daha uğraşmam"
   - Karşılaştırma: "rakip X daha ucuz", "başka yere bakıyorum"
   - Soğuma: 3+ gün cevap yok (aktif konuşmada), "düşüneyim" + sessizlik
   - Frekans düşüşü: düzenli müşteri → uzun süre sipariş/randevu yok

3) Risk skoru: Low / Medium / High / Critical
4) Otomatik aksiyon:
   - Medium: Agent'e "dikkat: kayıp riski" badge
   - High: Supervisor'a alert + önerilen kurtarma aksiyonu
   - Critical: Outbound kurtarma mesajı (özel teklif, VIP ilgi)

5) Dashboard: churn risk pipeline, kurtarılan vs kaybedilen
6) Etki: ORTA-YÜKSEK — retention artışı, LTV artışı
Gerekli yetenekler: C8, C4, C13 (sentiment analysis + pattern detection + dashboard)

---
SENARYO CS-06 — Unified customer timeline
Tüm sektörler | Kanıt: B | Phase: 2-3 (CRM derinleştirme)
Kaynak: scenarios-review-actions.md B1.6 | Tespit: Rapor 3 (ana)

1) Sorun: Tek bir müşteri/hasta için TÜM kanallardaki (WA, IG DM, telefon, email, sipariş, randevu, yorum)
   etkileşim geçmişinin tek bir zaman çizelgesinde görülmesi.

2) Bugünkü durum:
   - WA'dan yazdı → ayrı, IG'den yazdı → ayrı, telefon aradı → ayrı
   - Agent müşterinin geçmişini göremiyor → "daha önce yazmıştım" → "ne için yazmıştınız?"
   - Intent AI tek mesaja bakıyor, geçmiş context yok → yüzeysel analiz
   - VIP flag anlamsızlaşıyor çünkü toplu etkileşim görülmüyor
   - Follow-up kör: S9'da hasta 3 hafta önce IG'den yazdı, şimdi WA'dan yazıyor, bağlantı kurulamıyor

3) Çözüm:
   - Müşteri profili: telefon + email + IG handle + WA numara ile eşleştirme
   - Timeline görünümü: kronolojik, kanal ikonu ile
   - Her entry'de: kanal, tarih, konu/intent, çözüm durumu, agent
   - AI için context window: son 10 etkileşim özeti → cevap önerisi için
   - CRM entegrasyonu: sipariş geçmişi, randevu geçmişi, yorum geçmişi

4) Etki: YÜKSEK — tüm AI ve routing kalitesini artırır
Gerekli yetenekler: C1, C4, C8 (cross-channel identity + timeline view + AI context)

---
SENARYO CS-07 — Revenue attribution
Tüm sektörler | Kanıt: B | Phase: 2-3
Kaynak: scenarios-review-actions.md B1.7 | Tespit: Rapor 3 (ana), Rapor 1

1) Sorun: Her satışın/randevunun hangi kanaldan, hangi mesajdan, AI mi insan mı tarafından
   kapatıldığının takibi yok.

2) Bugünkü sorun:
   - "300K TL kazanıyoruz" diyorsun ama ispat yok
   - Hangi senaryo gerçekten para kazandırıyor belli değil
   - Enterprise müşteri soruyor: "AI ROI'niz nedir?" → cevap yok
   - Kampanya optimizasyonu yapılamıyor: hangi outbound template daha iyi dönüyor?

3) Çözüm:
   - Conversion source tracking: ilk temas kanalı (WA organic, IG ad, Google, referral)
   - AI vs Human flag: cevabı AI mi önerdi, insan mı yazdı, ikisi birlikte mi
   - Deal value: randevu → tedavi tutarı, sipariş → sepet tutarı
   - Funnel: lead → first response → qualified → appointment/purchase → closed
   - Dashboard: kanal bazlı ROI, agent bazlı kapanış oranı, AI assist oranı

4) Etki: YÜKSEK — enterprise satış için şart, kendi ROI'mizi kanıtlamamız lazım
Gerekli yetenekler: C4, C8, C12 (conversion tracking + funnel + dashboard)

---
SENARYO CS-08 — Compliance otomasyonu (KVKK/GDPR framework)
Tüm sektörler (sağlıkta kritik) | Kanıt: A | Phase: 1 (temel) → Phase 4 (enterprise tam)
Kaynak: scenarios-review-actions.md B1.8 | Tespit: Rapor 1, 3, 5

1) Sorun: Sadece "KVKK'ya dikkat" demek yetmez. Sistematik compliance altyapısı gerekli.

2) Neden kritik:
   - Sağlık verisi = KVKK özel nitelikli veri → ihmal = ağır ceza
   - AB müşterisi varsa GDPR de geçerli
   - Opt-in kayıtları, veri silme talepleri, erişim hakları → hepsi otomatik olmalı
   - Denetim geldiğinde kanıt sunabilmek lazım

3) Bileşenler:
   - Explicit consent flow: her kanalda açık onam toplama + kayıt (CS-01 ile entegre)
   - Opt-in log: kim, ne zaman, hangi kanaldan, ne için onam verdi
   - Template audit trail: gönderilen her template mesajın kaydı
   - Veri silme hakkı: müşteri "verimi silin" dedi → otomatik iş akışı
   - Veri erişim hakkı: müşteri "verilerim neler?" dedi → otomatik rapor
   - Saklama süresi: sağlık verisi X yıl, ticari veri Y yıl (konfigüre edilebilir)
   - Maskeleme: TC kimlik, telefon, sağlık bilgisi görüntülemede maskelenmeli
   - Audit log: kim hangi veriye ne zaman erişti → kayıt

4) Etki: YÜKSEK — yasal zorunluluk, enterprise satış engeli
Gerekli yetenekler: C5, C6 (security + audit + consent + data lifecycle)

---

**Cross-Sector Kritik Senaryo Özeti:**

| Senaryo | Phase | Etki | Bağımlılık |
|---------|-------|------|------------|
| CS-01: Opt-in toplama | Phase 1 | BLOCKER | Outbound Engine prerequisite |
| CS-02: AI → İnsan handoff | Phase 1 | BLOCKER | AI Assist prerequisite |
| CS-03: AI guardrail | Phase 1 | YÜKSEK | AI Assist + Knowledge |
| CS-04: SLA watchdog | Phase 1-2 | YÜKSEK | Routing + Dashboard |
| CS-05: Churn tespiti | Phase 3 | ORTA-YÜKSEK | Sentiment analysis |
| CS-06: Unified timeline | Phase 2-3 | YÜKSEK | CRM derinleştirme |
| CS-07: Revenue attribution | Phase 2-3 | YÜKSEK | Dashboard + tracking |
| CS-08: Compliance framework | Phase 1→4 | YÜKSEK | CS-01 + Security |

> **Kritik bağımlılık:** CS-01 (opt-in) ve CS-02 (handoff) BLOCKER —
> bu ikisi olmadan outbound ve AI senaryolarının çoğu ÇALIŞMAZ.
> Phase 1'de EN AZ temel versiyonları hazır olmalı.

============================================================
D-EK-4) E-TİCARET EK SENARYOLAR (B2 — 7 senaryo)
============================================================

> **Kaynak:** scenarios-review-actions.md B2 bölümü
> Mevcut 25 e-ticaret saha senaryosuna (01-25) ek olarak tespit edilen boşluklar.

---
SENARYO EB-01 — Stok bildirim (back-in-stock)
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1/E2 | Kanıt: B
Kaynak: scenarios-review-actions.md B2.1 | Phase: 2-3

1) Müşteri: 'Bu ürün gelince haber verin' / 'Stokta yok mu?'
2) Bugün: Müşteri düzenli kontrol yapıyor veya unutuyor. Satıcı haber verme mekanizması yok.
3) Nerede batıyor: Talep var ama müşteriye ulaşılamıyor → rakipten alıyor.
4) Maliyet: (Tahmini) Her kaçırılan stok bildirimi = 200-500 TL satış kaybı

Invekto burada:
A) Stok girişi olunca otomatik WA mesajı (Outbound Engine + stok entegrasyonu)
B) Opt-in ZORUNLU (CS-01). Template kategorisi: utility.
C) Stok entegrasyonu: Trendyol/HB/Shopify API ile stok değişikliği webhook
Gerekli yetenekler: C1, C3, C8, C11 (stok webhook + outbound template)
Etki: ORTA — müşteri memnuniyeti + dönüşüm

---
SENARYO EB-02 — Influencer/affiliate attribution
Bölge: Türkiye/Global | Dikey: E-ticaret D2C | Avatar: E2 | Kanıt: B
Kaynak: scenarios-review-actions.md B2.2 | Phase: 3

1) Müşteri: 'Kod neydi?' / 'Link açılmıyor' / 'X influencer'ın kodunu kullanmak istiyorum'
2) Bugün: Influencer kodu WA'da paylaşılıyor ama tracking yok. Hangi influencer ne kadar sattı bilinmiyor.
3) Nerede batıyor: Pazarlama bütçesi kör — influencer ROI ölçülemiyor.
4) Maliyet: Ölçülemeyen pazarlama = israf edilen bütçe

Invekto burada:
A) UTM + influencer kodu ile kampanya bazlı etiketleme (C12 Ads Attribution)
B) AI: "Kod neydi?" sorusunda influencer kodunu algıla → doğru linki gönder
C) Dashboard: influencer bazlı satış raporu + ROI hesaplama
Gerekli yetenekler: C8, C12 (attribution + campaign tracking)
Etki: ORTA — pazarlama ROI

---
SENARYO EB-03 — Proaktif sipariş durum güncelleme
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A
Kaynak: scenarios-review-actions.md B2.3 | Phase: 2

1) Durum: Kargo gecikmesi, stok sorunu olunca MÜŞTERİDEN ÖNCE bilgilendir.
   "Siparişinizdeki X ürünü stok sorunu nedeniyle 2 gün gecikmeli gönderilecek."
2) Bugün: Müşteri kendi fark ediyor → kızgın mesaj atıyor → kriz yönetimi.
3) Nerede batıyor: Proaktif bilgilendirme = güven. Reaktif = şikayet. S4 ile ilişkili ama farklı: S4 satış, bu bilgilendirme.
4) Maliyet: Proaktif bilgilendirme şikayet oranını %40-60 azaltır (Tahmini)

Invekto burada:
A) Kargo/sipariş durumu değişiklik webhook → otomatik WA mesajı (Outbound trigger)
B) Template: "Siparişiniz hakkında bilgilendirme" (utility kategori, opt-in gerekli)
C) Negatif senaryo: gecikme bildirimi + tahmini yeni tarih + özür kuponu
Gerekli yetenekler: C1, C3, C8, C11 (sipariş webhook + outbound trigger)
Etki: YÜKSEK — şikayet önleme

---
SENARYO EB-04 — Cross-platform sipariş eşleştirme
Bölge: Türkiye | Dikey: E-ticaret Marketplace | Avatar: E1 | Kanıt: B
Kaynak: scenarios-review-actions.md B2.4 | Phase: 2

1) Müşteri: Trendyol'dan aldı, WA'dan yazıyor, HB siparişi de var. "Hangi sipariş?"
2) Bugün: Satıcı farklı panellere bakıyor, müşteriden sipariş numarası istiyor — gecikme.
3) Nerede batıyor: Telefon numarası ile cross-platform eşleştirme yok.
4) Maliyet: (Tahmini) Sipariş tespit süresi: 5dk → 30sn (otomasyon ile)

Invekto burada:
A) Phase 2: C11 entegrasyonu ile telefon numarasından otomatik sipariş çekme (Trendyol + HB + Shopify)
B) AI: Müşteri "siparişim" deyince → son siparişleri listele → doğru olanı seç
Gerekli yetenekler: C1, C8, C11 (cross-platform order lookup)
Etki: ORTA — operasyonel verimlilik

---
SENARYO EB-05 — Şikayetvar / BTK eskalasyon yönetimi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B
Kaynak: scenarios-review-actions.md B2.5 | Phase: 3

1) Durum: Müşteri Şikayetvar'a düştü. Senaryo 03'te bahsedilmiş ama bağımsız workflow yok.
2) Bugün: Satıcı Şikayetvar'ı düzenli kontrol etmiyor veya geç fark ediyor.
3) Nerede batıyor: Geç müdahale = etki sıfır. Şikayetvar skoru düşer → potansiyel müşteriler kaçar.

Invekto burada:
A) Şikayetvar'daki case'in WA üzerinden proaktif çözümü
B) "Şikayetvar'da yazınızı gördük, sorunu hemen çözmek istiyoruz" — hız kritik
C) Çözüm sonrası: Şikayetvar güncelleme hatırlatması (müşteriye "çözüldü" yazdırma)
D) Risk: Çok geç kalırsa etki sıfır → SLA watchdog (CS-04) ile entegre
Gerekli yetenekler: C1, C2, C3, C8 (proaktif outreach + eskalasyon routing)
Etki: ORTA — itibar koruma

---
SENARYO EB-06 — Garanti ve teknik servis yönlendirme
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1/E2 | Kanıt: B
Kaynak: scenarios-review-actions.md B2.6 | Phase: 3

1) Müşteri: 'Ürün bozuldu, garanti kapsamında mı?' / 'Teknik servise nasıl göndereceğim?'
2) Bugün: Satıcı garanti süresini kontrol ediyor (sipariş tarihinden hesaplama), teknik servis bilgisi veriyor — tekrarlı iş.
3) Nerede batıyor: Garanti süresi hesaplama hataları, yanlış teknik servis yönlendirmesi.

Invekto burada:
A) Knowledge base'den: garanti koşulları, teknik servis adresleri/telefonları
B) Otomatik garanti süresi kontrolü (sipariş tarihi + garanti süresi)
C) Dikkat: AI guardrail (CS-03) — garanti kararı AI tarafından verilmez, bilgi sunulur
Gerekli yetenekler: C1, C7, C8 (knowledge + garanti hesaplama)
Etki: DÜŞÜK-ORTA

---
SENARYO EB-07 — Fraud / dolandırıcılık şüphesi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1/E2 | Kanıt: A
Kaynak: scenarios-review-actions.md B2.7 | Phase: 2

1) Müşteri: 'Bu siparişi ben vermedim' / 'Hesabım çalındı' / 'Tanımadığım bir kargo geldi'
2) Bugün: Müşteri panik halinde, normal kuyruğa düşüyor, bekliyor — kriz büyüyor.
3) Nerede batıyor: Yüksek öncelikli, hassas. Gecikme = hukuki risk + güven kaybı.

Invekto burada:
A) AI panik butonu: fraud intent tespiti → normal kuyruk bypass → acil agent routing
B) Hesap dondurma talebi → operasyonel eskalasyon (yöneticiye push)
C) Template: "Hesabınız güvende — durumu inceliyoruz" hemen gönderilir (anxiety azaltma)
D) Ardından: İnceleme süreci + sonuç bildirimi (Outbound follow-up)
Gerekli yetenekler: C1, C2, C8 (fraud intent + priority routing + eskalasyon)
Etki: YÜKSEK — güvenlik

---

**E-ticaret Ek Senaryo Özeti:**

| Senaryo | Phase | Etki | Bağımlılık |
|---------|-------|------|------------|
| EB-01: Stok bildirim | Phase 2-3 | ORTA | Outbound + C11 stok webhook |
| EB-02: Influencer attribution | Phase 3 | ORTA | C12 Ads Attribution |
| EB-03: Proaktif sipariş güncelleme | Phase 2 | YÜKSEK | C11 + Outbound trigger |
| EB-04: Cross-platform eşleştirme | Phase 2 | ORTA | C11 entegrasyon |
| EB-05: Şikayetvar eskalasyon | Phase 3 | ORTA | CS-04 SLA watchdog |
| EB-06: Garanti/teknik servis | Phase 3 | DÜŞÜK-ORTA | C7 Knowledge |
| EB-07: Fraud yönetimi | Phase 2 | YÜKSEK | Priority routing |

============================================================
D-EK-5) SAĞLIK EK SENARYOLAR (B3 — 5 senaryo — DİŞ + ESTETİK)
============================================================

> **Kaynak:** scenarios-review-actions.md B3 bölümü
> Mevcut diş (26-50) ve estetik (51-75) saha senaryolarına ek olarak tespit edilen boşluklar.
> Bu senaryolar her iki sağlık sektörüne de uygulanabilir.

---
SENARYO SB-01 — Tedavi planı onay akışı
Bölge: Türkiye | Dikey: Diş + Estetik | Avatar: D1/A1 | Kanıt: A
Kaynak: scenarios-review-actions.md B3.1 | Phase: 2

1) Durum: Doktor tedavi planı gönderdi (PDF/mesaj), hasta onay vermedi.
2) Bugün: Onay takibi yapılmıyor veya sekretarya unutuyor. Plan gönderilip takip edilmeyen her hasta = kaybedilen 15-50K TL tedavi.
3) Nerede batıyor: Onay gelmezse ne olacağı belli değil. Passive churn — hasta "düşünüyorum" diyor ve kayboluyor.

Invekto burada:
A) Follow-up zinciri (Outbound):
   - T+1 gün: "Tedavi planınızı incelediniz mi?"
   - T+3 gün: "Sorularınız varsa yardımcı olabiliriz"
   - T+7 gün: Son hatırlatma
B) Onay gelmezse → supervisor'a alert (kaybedilen gelir tahmini ile)
C) Onay gelirse → randevu slotu öner (Appointments entegrasyon)

Gerekli yetenekler: C1, C3, C8, C10 (follow-up + revenue tracking + appointments)
Etki: YÜKSEK — ciddi gelir kaybı önleme (15-50K TL/hasta)

---
SENARYO SB-02 — Sigorta provizyon ön kontrol
Bölge: Türkiye | Dikey: Diş (ağırlıklı) | Avatar: D2 | Kanıt: B
Kaynak: scenarios-review-actions.md B3.2 | Phase: 3-4

1) Hasta: 'Sigortam X tedaviyi karşılıyor mu?' / 'Poliçe numaram 12345, kontrol eder misiniz?'
2) Bugün: Senaryo 37'de sigorta sorusu var ama yüzeysel. Gerçek ihtiyaç: provizyon sorgusu, kapsam kontrolü, katkı payı hesabı. Sekretarya sigorta şirketini arıyor — 30dk-1saat.
3) Nerede batıyor: Tam otomasyon zor (sigorta API'leri karmaşık ve şirketten şirkete değişiyor).

Invekto burada:
A) Phase 3 (bilgi toplama): Poliçe no + TC kimlik toplama → manuel provizyon sorgusuna sunma
B) Phase 4 (entegrasyon): Sigorta API'leri ile otomatik kapsam kontrolü (büyük şirketler önce)
C) AI: "Sigortanız X tedaviyi genellikle karşılar, kesin bilgi için provizyon sorgusu yapacağız" (guardrail — kesin sonuç söyleme)
D) KVKK: Sigorta bilgisi = özel nitelikli kişisel veri → rıza + maskeleme
Gerekli yetenekler: C1, C5, C7, C8 (bilgi toplama + knowledge + KVKK)
Etki: ORTA — operasyonel yük azaltma

---
SENARYO SB-03 — Çoklu klinik/şube yönetimi
Bölge: Türkiye | Dikey: Diş + Estetik (zincir klinikler) | Avatar: D1/A1 | Kanıt: B
Kaynak: scenarios-review-actions.md B3.3 | Phase: 2

1) Hasta: 'Kadıköy şubenizde Dr. Mehmet hangi günler?' / 'En yakın şubeniz neresi?'
2) Bugün: Sekretarya doktorun hangi şubede, hangi gün olduğunu ezbere biliyor (veya bilmiyor).
3) Nerede batıyor: Zincir klinikler için şart. Yanlış şubeye yönlendirme = hasta kaybı + zaman kaybı.

Invekto burada:
A) Knowledge base: doktor-şube-gün eşleştirme tablosu
B) AI: konum bazlı yönlendirme ("Kadıköy şubemizde Dr. Mehmet Pazartesi-Çarşamba, Beşiktaş'ta Perşembe-Cumartesi")
C) Randevu motoru: şube bazlı slot yönetimi (Appointments genişletme)
Gerekli yetenekler: C1, C7, C8 (knowledge + location routing)
Etki: ORTA — zincir klinikler için operasyonel verimlilik

---
SENARYO SB-04 — Tedavi öncesi hazırlık talimatları (pre-op)
Bölge: Türkiye | Dikey: Diş + Estetik | Avatar: D1/A1 | Kanıt: A
Kaynak: scenarios-review-actions.md B3.4 | Phase: 2

1) Durum: Ameliyat/tedavi öncesi: 8 saat açlık, X ilacı kesin, Y ilacı devam edin, randevuya refakatçi ile gelin.
   S8'in tersi — S8 post-op, bu pre-op. Aynı mekanik (mesaj zinciri) ama içerik farklı.
2) Bugün: Sekretarya telefonda anlatıyor veya basılı kağıt veriyor. Hasta unutuyor → ameliyat iptal.
3) Nerede batıyor: Hasta hazırlığı eksikse ameliyat iptal → koltuk boş → gelir kaybı.
4) Maliyet: İptal edilen ameliyat = 5-50K TL gelir kaybı + başka hasta alınamamış slot

Invekto burada:
A) Otomatik mesaj zinciri (Outbound):
   - T-3 gün: Genel hazırlık talimatları (açlık, ilaç, refakatçi)
   - T-1 gün: Hatırlatma + soru varsa "şimdi sorun"
   - T-sabah: Son kontrol + klinik adresi/ulaşım
B) Tedavi tipine göre talimat şablonu (S8 varyasyon tablosuyla paralel):
   - Diş çekim: aspirin kes, 8 saat açlık
   - İmplant: antibiyotik başla, 12 saat açlık
   - Rinoplasti: kan sulandırıcı kes, sigara bırak, refakatçi zorunlu
   - Botox/dolgu: hazırlık minimal, bilgilendirme ağırlıklı
C) KVKK: Sağlık talimatı = tıbbi veri → rıza gerekli
Gerekli yetenekler: C1, C3, C7, C8 (knowledge + outbound + treatment-specific templates)
Etki: YÜKSEK — ameliyat iptal önleme + hasta güvenliği

---
SENARYO SB-05 — Reçete/ilaç sorguları
Bölge: Türkiye | Dikey: Diş + Estetik | Avatar: D2/A2 | Kanıt: B
Kaynak: scenarios-review-actions.md B3.5 | Phase: 3

1) Hasta: 'Reçetemi yazdınız mı?' / 'İlacı nereden alacağım?' / 'Dozajı ne kadardı?'
2) Bugün: Hasta sekretaryayı arıyor, sekretarya doktora soruyor — tekrarlı sorular.
3) Nerede batıyor: Tekrarlı, zaman alıcı. Dozaj bilgisi hassas — yanlış bilgi = risk.

Invekto burada:
A) Knowledge base'den: reçete durumu, eczane bilgisi, genel ilaç bilgisi (hasta bazlı kayıt)
B) DİKKAT — Guardrail (CS-03 ile entegre):
   - Dozaj önerisi YAPMA — sadece doktorun verdiği bilgiyi tekrarla
   - "Dozaj bilgisi için doktorunuza danışınız" standardı
   - İlaç etkileşimi sorusu → ASLA yanıt → doktora yönlendir (handoff CS-02)
C) KVKK: Reçete = sağlık verisi → özel nitelikli veri koruması
Gerekli yetenekler: C1, C7, C8 (knowledge + guardrail + KVKK)
Etki: DÜŞÜK-ORTA — tekrarlı soru azaltma

---

**Sağlık Ek Senaryo Özeti:**

| Senaryo | Phase | Etki | Bağımlılık |
|---------|-------|------|------------|
| SB-01: Tedavi planı onay | Phase 2 | YÜKSEK | Outbound + Appointments |
| SB-02: Sigorta provizyon | Phase 3-4 | ORTA | Sigorta API (karmaşık) |
| SB-03: Çoklu şube yönetimi | Phase 2 | ORTA | Knowledge + Appointments |
| SB-04: Pre-op hazırlık talimatları | Phase 2 | YÜKSEK | Outbound + Knowledge |
| SB-05: Reçete/ilaç sorguları | Phase 3 | DÜŞÜK-ORTA | Knowledge + CS-03 guardrail |

> **Bağımlılık:** SB-01 ve SB-04 Outbound Engine'e bağlı (S7/S8 ile aynı altyapı).
> SB-05 mutlaka CS-03 (guardrail) ile birlikte — dozaj bilgisi verilemez!

============================================================
E) CAPABILITY GAP ÖZETİ (75 senaryodan çıkarım)
C1, C2, C3 Invekto'da MEVCUT. C4 kısmen mevcut.
   Asıl gap: C7 (Knowledge), C8 (Agent Assist), C9 (Auto-Resolution),
   C10 (Revenue), C11 (E-commerce), C12 (Ads Attribution)
============================================================

En çok tekrar eden yetenekler (senaryo sayısı):
- C8: 75/75 ← ❌ YOK — Phase 1'de InvektoServis.AgentAI ile
- C3: 73/75 ← ✅ MEVCUT (temel) — dinamik değişken YOK
- C1: 72/75 ← ✅ MEVCUT — 7 kanal Unified Inbox
- C2: 71/75 ← ✅ MEVCUT — 4 algoritma, grup bazlı routing
- C7: 48/75
- C4: 25/75
- C10: 25/75
- C12: 24/75
- C8: doğru soruları öner (yaş: 22/75
- şikayet: 22/75
- randevu niyeti): 22/75
- C13: 3/75
- C11: 2/75
- C8: next-best-question (işlem: 2/75
- süre: 2/75

Phase-1 için 'matematiksel' zorunlular (en yüksek frekans + düşük risk):
- C1 Unified Inbox + C2 Routing + C3 Templates/Snippets (çekirdek) — ✅ ZATEN VAR (Invekto).
- C8 Agent Assist (cevap önerisi + risk uyarısı) — hız + kalite için — ❌ Phase 1'de yapılacak.
- Automation Engine (chatbot/trigger/flow) — ❌ Phase 1'de yapılacak — #1 satış engeli.
- Outbound Engine (broadcast/schedule/follow-up) — ❌ Phase 1'de yapılacak — #1 talep.
- C4 Reporting Core: Speed-to-lead, SLA ve deflection ölçümü — ⚠️ KISMEN VAR, AI metrikleri eklenecek.
- C11 Entegrasyonlar: Türkiye e-ticarette Trendyol/HB; D2C'de Shopify/Woo — Phase 2'de.

Phase-2+ için yüksek değer ama daha riskli/karmaşık:
- C11 E-commerce Integrations (Trendyol/HB API) — Phase 2'de.
- C12 Ads attribution — Phase 2 (basit) / Phase 5 (tam).
- C7 Knowledge (RAG, 'tek kaynak' SSS/policy motoru) — Phase 3'te (tutarlılık için kritik).
- C9 Auto-Resolution (otonom çözüm) — Phase 3'te; yanlış cevap riski; iyi guardrails/hand-off şart.
- C5/C6 Enterprise Security full paketi — Phase 4'te; enterprise satış için şart; sağlıkta erken gelir.
- C10 Revenue Agent — Phase 5'te; ödeme/kapora/teklif; entegrasyon ve süreç tasarımı ister.
- C13 QA & Mining — ölçeklemede kaliteyi tutmak için.

============================================================
F) KAYNAK NOTLARI (bu dokümandaki 'A' seviyeli gerçekler için)
============================================================

> **Not:** Aşağıdaki kaynaklar orijinal araştırma oturumundan alınmıştır. Parantez içi kodlar
> (TurnXsearchY) oturum-içi geçici referanslardır; doğrulanabilir kaynak açıklama metnindedir.

[WA] WhatsApp 24h pencere + template zorunluluğu: Twilio 'customer service window' açıklaması ve Meta WhatsApp template docs.
[WA] Template kategorilendirme güncellemesi (2025): Meta developer docs template categorization.
[IG] 24h pencere: Messenger/IG policy overview + respond.io IG customer service yazısı.
[TR] Trendyol iade hakkı 15 gün: Trendyol Satıcı Bilgi Merkezi iade süreçleri.
[TR] İade tamamlanma 2–10 iş günü ve paketleme standartları: Trendyol kolay iade paketleme standartları sayfası.
[TR] İade/kargo şikayet patternleri: Şikayetvar Trendyol iade/kargo şikayetleri örnekleri.
[HEALTH] Özel nitelikli veri rehberi: KVKK rehberi.
[HEALTH] Sağlıkta WhatsApp kullanımı riskleri ve kayıt tutma zorluğu: PMC makalesi.
[HEALTH] KVKK kamu duyurusu (WhatsApp gibi uygulamalarla ilgili): duyuru üzerine analiz.

============================================================
G) YAPISAL EK TABLOLAR (v5 — 2026-02-16)
============================================================

> Bu bölüm scenarios-review-actions.md C1-C5 aksiyonlarının uygulamasıdır.
> **v5:** Yeni eklenen senaryolar (CS, EB, SB, O11-O17, M6-M7) tablolara dahil edildi.

---

### G1. Revenue (S#) → Saha Senaryo Mapping Tablosu (C1)

> Her revenue senaryosunun hangi saha senaryolarından beslendiği.

**Revenue → Saha (hangi saha senaryoları bu revenue'yu besliyor):**

| Revenue Senaryo | Besleyen Saha Senaryoları | Not |
|----------------|--------------------------|-----|
| S1 (Negatif Yorum Kurtarma) | 03, 15 (kriz senaryoları sentiment verisi sağlar) | Kriz De-eskalasyon grubu |
| S2 (Satış Öncesi Ürün Soruları) | 11, 12 (beden/stok soruları) | E-ticaret knowledge |
| S3 (İade Çevirme) | 02, 03, 16, 17, 24 (iade/kargo senaryoları) | Kargo/Lojistik grubu |
| S4 (Sipariş Sonrası Proaktif Satış) | 14 (sepet terk), tüm e-ticaret | Outbound v2 gerekli |
| S5 (B2B Lead Tespiti) | O9 (grup rezervasyon), tüm VIP sinyalleri | Cross-sektör |
| S6 (Fiyat → Randevu) | ~~26~~ (silindi), 27, 41, 51 | Diş + Estetik fiyat soruları |
| S7 (No-Show Önleme) | ~~29~~ (silindi), 40, ~~55~~ (silindi) | Hatırlatma zinciri |
| S8 (Tedavi Sonrası Takip) | ~~32, 45, 72~~ (silindi), 30, 57 | Post-treatment zinciri |
| S9 (Medikal Turizm) | ~~34, 61~~ (silindi), O6 | Multi-language + paket |
| S10 (Yorum + Referans) | ~~50, 73~~ (silindi → içerik uyumsuz), O7 | Outbound Engine |

**Saha → Revenue (her saha senaryosu hangi revenue'ya katkı sağlıyor):**

| Saha Grup | Senaryolar | Besler → |
|-----------|-----------|----------|
| Kargo/Lojistik | 01, 02, 06, 07, 18, 19, 24 | S3 (iade çevirme fırsatı) |
| Ödeme/Fatura | 08, 13, 21, 22 | Genel operasyonel verimlilik |
| Kriz De-eskalasyon | 03, 15, 35, 57 | S1 (yorum kurtarma verisi) |
| KVKK/Veri Güvenliği | 33, 44, 62, 75 | Compliance altyapısı (CS-08) |
| Lead Dönüşüm | 60 | S6, S10 (lead + sosyal kanıt) |
| Randevu/Slot | 28, 40, 54, 69 | S7 (no-show), S6 (dönüşüm) |
| Otel | O1-O17 | S5 (O9), S9 (O6), S10 (O7), in-stay (O11-O13), OTA (O16) |
| Cross-Sector Kritik | CS-01 to CS-08 | TÜM revenue senaryolarına altyapı (opt-in, handoff, guardrail, SLA) |
| E-ticaret Ek | EB-01 to EB-07 | S1 (EB-05), S3 (EB-03), S4 (EB-01), C11 (EB-04) |
| Sağlık Ek | SB-01 to SB-05 | S6 (SB-01 onay), S7 (SB-04 pre-op), S8 (SB-04 zincir) |
| Mobil | M1-M7 | Tüm operasyonel senaryolara mobil erişim |

---

### G2. Phase Bağımlılık Tablosu (C2)

| Senaryo | Phase | Bağımlılıklar (önce bunlar hazır olmalı) |
|---------|-------|------------------------------------------|
| S1 (Yorum Kurtarma) | 3 | C11 (Trendyol API), Outbound Engine, Sentiment analiz |
| S2 (Ürün Soruları) | 3 | C7 (Knowledge/RAG), ürün katalogu |
| S3 (İade Çevirme) | 2-3 | C8 (AI Assist), iade politikası knowledge |
| S4 (Proaktif Satış) | 5 | Outbound v2 (follow-up zinciri + cross-sell), C11, müşteri segmentasyonu |
| S5 (B2B Lead) | 2 | AI intent tespiti, VIP flag, routing |
| S6 (Fiyat → Randevu) | 1-2 | AI Assist (Phase 1), Randevu motoru (Phase 2) |
| S7 (No-Show) | 1 | Outbound temel (broadcast + trigger) |
| S8 (Tedavi Takip) | 2 (basit) / 5 (tam) | Outbound temel, Knowledge base (Phase 3) |
| S9 (Medikal Turizm) | 5 | Multi-language AI (Phase 3), Outbound v2, lead scoring |
| S10 (Yorum Motoru) | 5 | Outbound, Google Business Profile API (Phase 3), sentiment |
| O1 (Oda fiyat) | 1 (template) / 2 (PMS) | PMS entegrasyonu (Phase 2) |
| O7 (Yorum rica) | 2 (Outbound) / 5 (yorum motoru) | Outbound Engine |
| O10 (Kampanya) | 1 (Outbound broadcast) | Outbound Engine |
| O11 (Oda servisi) | 1 (template) / 2 (POS) | POS entegrasyonu (Phase 2) |
| O14 (Late check-out) | 1 (template) / 2 (PMS) | PMS entegrasyonu (Phase 2) |
| O16 (OTA mesaj) | 2-3 | Booking.com Connectivity API |
| CS-01 (Opt-in) | 1 | Outbound Engine prerequisite — BLOCKER |
| CS-02 (Handoff) | 1 | AI Assist prerequisite — BLOCKER |
| CS-03 (Guardrail) | 1 | AI Assist + Knowledge base |
| CS-04 (SLA Watchdog) | 1-2 | Routing + Dashboard |
| CS-05 (Churn) | 3 | Sentiment analysis altyapısı |
| CS-06 (Timeline) | 2-3 | CRM derinleştirme, cross-channel identity |
| CS-07 (Attribution) | 2-3 | Dashboard + conversion tracking |
| CS-08 (Compliance) | 1 (temel) → 4 (tam) | CS-01 + C5/C6 Security |
| EB-03 (Proaktif güncelleme) | 2 | C11 sipariş webhook + Outbound trigger |
| EB-07 (Fraud) | 2 | Priority routing + eskalasyon |
| SB-01 (Tedavi planı onay) | 2 | Outbound follow-up + Appointments |
| SB-04 (Pre-op talimat) | 2 | Outbound zincir + Knowledge base |

> **Outbound Engine iki seviye:**
> - Phase 1 = temel: broadcast + trigger (S7, O7, O10, CS-01 opt-in için yeterli)
> - Phase 2 = gelişmiş: follow-up zincirleri + cross-sell kuralları (S4, S8 tam, S9, SB-01, SB-04 için gerekli)

---

### G3. Saha Senaryoları Etki Seviyesi (C3)

| Etki | Kriter | E-ticaret | Diş | Estetik | Otel | Cross-Sector | Ek Senaryolar |
|------|--------|-----------|-----|---------|------|-------------|---------------|
| YUKSEK (>50K TL/ay veya kritik risk) | Yüksek hacim veya yüksek birim değer | 01, 03, 14 | 27, 28, 30, 35, 40 | 51, 52, 54, 57, 60 | O1, O4, O9, O11, O12, O14 | CS-01 (BLOCKER), CS-02 (BLOCKER), CS-03, CS-04, CS-06, CS-07, CS-08 | EB-03, EB-07, SB-01, SB-04 |
| ORTA (10-50K TL/ay) | Orta hacim, operasyonel iyileştirme | 02, 05, 06, 07, 08, 09, 11, 15, 19 | 31, 33, 36, 37, 38, 41, 42, 43 | 53, 56, 58, 59, 63, 64, 65, 66, 68 | O2, O3, O5, O6, O7, O8, O10, O13, O15, O16 | CS-05 | EB-01, EB-02, EB-04, EB-05, SB-02, SB-03 |
| DUSUK (<10K TL/ay veya düşük hacim) | Düşük hacim veya spesifik durum | 10, 12, 13, 16, 17, 18, 20, 21, 22, 23, 24, 25 | 39, 46, 47, 48, 49 | 69, 70, 71, 74 | O17 | — | EB-06, SB-05, M6, M7 |

---

### G4. Entegrasyon Gereksinimleri Matrisi (C4)

| Entegrasyon | Senaryolar | API/Araç | Phase |
|-------------|-----------|----------|-------|
| Trendyol Seller API v2 | S1, 01, 03, 19, 20, EB-01, EB-03, EB-04 | REST API, sipariş/yorum/kargo/stok endpoints | Phase 2 |
| Hepsiburada Open API | 19, EB-04 | REST API, sipariş/kargo endpoints | Phase 2 |
| Shopify Admin API | 21, EB-01 | GraphQL/REST, sipariş/ödeme/stok endpoints | Phase 2 |
| WooCommerce REST API | 22, EB-01 | REST API, sipariş/kargo/fatura/stok endpoints | Phase 2 |
| PMS Entegrasyonu (Otel) | O1, O3, O5, O11, O14 | OPERA, Protel, Clock PMS vb. | Phase 2 |
| POS Entegrasyonu (Otel) | O11 | Restoran/mutfak sipariş sistemi | Phase 2 |
| Booking.com Connectivity API | O16 | Mesaj kanalı entegrasyonu | Phase 2-3 |
| Expedia Partner Central API | O16 | Mesaj kanalı entegrasyonu | Phase 3 |
| Google Business Profile API | S10, O7 | Yorum yönetimi + yanıt | Phase 3 |
| Ödeme Gateway | 36, 68, O15 | iyzico/Param/PayTR | Phase 2 |
| Sigorta Provizyon API | SB-02 | Şirket bazlı API'ler (karmaşık) | Phase 3-4 |
| WhatsApp Cloud API | TÜM outbound + CS-01 opt-in | Meta Business API | Phase 1 (mevcut) |
| Instagram Graph API | 27, 41, 51, 53, 60, 63, EB-02 | DM mesaj yönetimi + attribution | Phase 1 (mevcut) |

---

### G5. KVKK Risk Skoru (C5)

| Risk | Kriter | Senaryolar |
|------|--------|-----------|
| YUKSEK | Sağlık verisi, özel nitelikli kişisel veri | 30, 33, 37, 44, 47, 56, 62, 75, M4, SB-01, SB-02, SB-04, SB-05, CS-08 |
| ORTA | Kişisel veri (ad, telefon, adres, sipariş, fotoğraf) | 08, 09, 13, 14, 27, 36, 38, 48, 52, 54, 58, 66, 68, O1, O5, O9, O15, CS-01, CS-06, EB-04, EB-07 |
| DUSUK | Anonim/genel bilgi, template mesajlar | 01, 02, 06, 07, 10, 11, 12, 16, 17, 18, 20, 25, O2, O4, O8, O11, O12, O13, O17, M6, M7, EB-01, EB-02, EB-06 |

> **KVKK YUKSEK risk senaryolarında zorunlu:**
> - Açık rıza / aydınlatma metni
> - Veri minimizasyonu (sadece gerekli bilgi)
> - Saklama süresi politikası
> - Erişim kontrolü (kim hangi veriye erişir)
> - Maskeleme (TC kimlik, sağlık bilgisi ekranda maskelenmeli)

============================================================
H) GÜZELLİK SALONU SENARYOLARI (GU-01 ~ GU-25)
============================================================

> **Sektör:** Kuaför, berber, cilt bakım, nail art, güzellik merkezi
> **Hedef müşteri:** 2-10 koltuklu salonlar, zincir güzellik merkezleri
> **Kanal:** WhatsApp + Instagram DM (Instagram özellikle önemli — before/after paylaşımları)
> **Phase:** Çoğu Phase 1-2 ile başlanabilir (randevu + hatırlatma core altyapı)
> **v6 (2026-02-16):** D1 kararı ile eklenen yeni sektör

---
SENARYO GU-01 — Randevu alma / değiştirme / iptal
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A | Grup: Randevu

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Yarın öğleden sonra saç boyama için yer var mı?'
   - Müşteri: 'Saat 3'e alabilir misiniz?'
   - Salon: (resepsiyonist meşgul, müşteri bekliyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Resepsiyonist telefon + WhatsApp + yüz yüze aynı anda randevu veriyor
   - Kağıt/Excel ajanda ile çakışma kontrolü
   - Yoğun günlerde (Cuma-Cumartesi) cevap gecikince müşteri rakibe gidiyor

3) Nerede batıyor?
   - Çift randevu verilmesi (ajanda senkron değil)
   - Cevap gecikmesi → müşteri başka salona gidiyor
   - İptal/değişiklik takibi zorlaşıyor

4) Gerçek maliyet (tahmini)
   - Günde 20-40 randevu talebi, 5-10'u geç cevaplanıyor
   - Kaçan müşteri: 5 × 200 TL = 1.000 TL/gün = 30.000 TL/ay

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + C2 routing ile mesajlar tek yerde
B) Phase-1 ile ne olur?
   - C8 Agent Assist: "Yarın saat 15:00 müsait. Onaylamak ister misiniz?" önerisi
   - Automation flow: randevu intent → müsait slot göster → onay → kayıt
C) Phase-2+ gerektiren
   - Randevu motoru (Appointments servis) ile otomatik slot yönetimi
   - Online randevu takvimi (müşteri self-service)

Gerekli yetenekler: C1, C2, C3, C8 (Phase 1) + Randevu motoru (Phase 2)
KVKK risk: DUSUK

Pros: Deflection yüksek, en sık gelen mesaj tipi, ROI hızlı
Cons: Salon ajandası senkron olmadan çift randevu riski → fallback gerekli

---
SENARYO GU-02 — Fiyat sorgulama
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A | Grup: Fiyat

1) Müşteri mesajı
   - Müşteri: 'Röfle + bakım ne kadar?'
   - Müşteri: 'Keratin fiyatı nedir?'

2) Bugün
   - Fiyat listesi elle gönderiliyor veya "gelince konuşuruz" deniyor
   - Instagram DM'den gelen fiyat sorularına geç dönülüyor

3) Nerede batıyor?
   - Fiyat vermeden "gelin görüşelim" → müşteri güvensizlik hissediyor
   - Her seferinde aynı fiyat listesini kopyala-yapıştır
   - Saç uzunluğuna göre fiyat farkı var, standart fiyat verilemez

4) Gerçek maliyet
   - Günde 15-25 fiyat sorusu, %40'ı dönüşüm kaybediyor
   - 10 kayıp × 200 TL = 2.000 TL/gün = 60.000 TL/ay

Invekto burada:
A) Bugün: C3 template ile fiyat listesi hızlı gönderim
B) Phase-1: C8 AI Assist fiyat sorusunu anlar → "Röfle: 500-800 TL (saç uzunluğuna göre). Randevu oluşturmamı ister misiniz?"
C) Phase-2+: C7 Knowledge base'den detaylı fiyat bilgisi + kampanya fiyatları

Gerekli yetenekler: C1, C2, C3, C8 (Phase 1) + C7 Knowledge (Phase 3)
KVKK risk: DUSUK

---
SENARYO GU-03 — Kuaför / stilist seçimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Gelin saçı yapan kuaförünüz var mı?'
   - 'Balayaj konusunda uzman kim?'

2) Bugün: Resepsiyonist müsait olan kişiyi söylüyor, uzmanlık eşleşmesi yapılmıyor
3) Nerede batıyor: Yanlış kuaföre yönlendirme → memnuniyetsizlik
4) Gerçek maliyet: Müşteri memnuniyetsizliği → tekrar gelmeme → lifetime value kaybı

Invekto burada:
A) Bugün: C2 routing ile kuaförlere yönlendirme
B) Phase-1: C8 AI Assist stilist uzmanlık bilgisini Knowledge'dan çeker → doğru eşleştirme
C) Phase-2+: Stilist profili + portfolyo gösterimi, müşteri tercihi kayıt

Gerekli yetenekler: C1, C2, C8 + C7 (stilist bilgisi Knowledge'da)
KVKK risk: DUSUK

---
SENARYO GU-04 — Bekleme süresi sorgulama
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Randevusuz gelsem ne kadar beklerim?'
   - 'Şu an yoğun mu?'

2) Bugün: Tahmini cevap veriliyor, genellikle tutmuyor
3) Nerede batıyor: Bekleme süresi tutmayınca müşteri sinirleniyor

Invekto burada:
A) C3 template: "Şu an ortalama bekleme süremiz 20-30 dakika. Randevu oluşturmak ister misiniz?"
B) Phase-2+: Randevu motorundan gerçek zamanlı slot doluluk bilgisi

Gerekli yetenekler: C1, C3, C8
KVKK risk: DUSUK

---
SENARYO GU-05 — Son dakika boşluk bildirimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Randevu iptali oldu, saat 14:00'te boşluk var. Bekleyen müşterilere haber ver.

2) Bugün: İptal olunca o slot boş kalıyor, kimseye haber verilmiyor
3) Nerede batıyor: Boş koltuk = kayıp gelir. Günde 1-2 iptal × 250 TL = 500 TL/gün kayıp

Invekto burada:
A) Phase-1: Outbound trigger: iptal → bekleyen müşteri listesine "Bugün 14:00'te yer açıldı! İlgilenir misiniz?"
B) Phase-2+: Otomatik waitlist sistemi, ilk cevaplayan alır

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK (opt-in zorunlu)

---
SENARYO GU-06 — No-show / iptal yönetimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A | Grup: Randevu

1) Senaryo: Müşteri randevuya gelmedi. Koltuk 45 dakika boş kaldı.

2) Bugün: Resepsiyonist arar, telefon açılmaz, konu kapanır
3) Nerede batıyor: No-show oranı %15-20 → günde 3-4 boş koltuk × 200 TL = 800 TL/gün = 24.000 TL/ay

Invekto burada:
A) Phase-1: Outbound hatırlatma: R-1gün + R-2saat "Yarın saat 15:00 randevunuz var. Onaylıyor musunuz?"
B) Phase-2+: No-show tracking, repeat offender flag, depozit/kapora sistemi

Gerekli yetenekler: C1, C3, C8, Outbound Engine + Randevu motoru (Phase 2)
KVKK risk: DUSUK

Pros: S7 ile aynı mekanik — cross-sector altyapı zaten var
Cons: Güzellik salonlarında depozit kültürü zayıf → yumuşak yaklaşım gerekli

---
SENARYO GU-07 — İşlem sonrası bakım talimatları
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Senaryo: Saç boyama yapıldı. Müşteriye: "İlk 48 saat yıkamayın, sülfatsız şampuan kullanın" talimatı gönderilmeli.

2) Bugün: Sözlü söyleniyor, müşteri unutuyor → renk çabuk açılıyor → "boyam tutmadı" şikayeti
3) Nerede batıyor: Bakım talimatı uygulanmazsa → sonuç kötü → salon suçlanıyor

Invekto burada:
A) Phase-1: Outbound trigger: işlem sonrası → otomatik bakım talimatı mesajı (işlem tipine göre template)
B) Phase-2+: T+3 gün "Saçınız nasıl?" follow-up → memnuniyet sorgusu

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK
S8 (Tedavi Sonrası Takip) ile aynı mekanik — sektör varyasyonu.

---
SENARYO GU-08 — Şikayet: beğenmeme / hata
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A | Grup: Kriz De-eskalasyon

1) Müşteri mesajı
   - 'Saç rengim hiç istediğim gibi olmadı!'
   - 'Kesimi çok kısa kestiniz, çok sinirli oldum'
   - 'Manikürüm 2 günde çıktı, para iadesi istiyorum'

2) Bugün: Mesaja geç dönülüyor veya savunmacı yaklaşım → Google'a kötü yorum
3) Nerede batıyor: Hızlı müdahale edilmezse → kötü yorum + müşteri kaybı + referans kaybı

Invekto burada:
A) Phase-1: C8 AI Assist şikayet intent'i → empati template + düzeltme randevusu teklifi
   "Çok üzgünüz! Size en kısa sürede düzeltme randevusu ayarlayalım. Ne zaman uygun olur?"
B) Phase-2+: Şikayet kategorize + severity scoring → yüksek risk → salon sahibine alert

Gerekli yetenekler: C1, C2, C3, C8 + Sentiment (Phase 3)
KVKK risk: DUSUK
S1/S10 ile ilişkili — yorum kurtarma mekanizması.

---
SENARYO GU-09 — Ürün satışı (bakım ürünleri)
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Geçen geldiğimde kullandığınız şampuan hangisiydi?'
   - 'Saç bakım yağı satıyor musunuz?'

2) Bugün: Resepsiyonist ürünü bilmiyor, kuaföre soruyor → gecikmeli cevap
3) Nerede batıyor: Ürün satışı salonda yapılıyor ama online takip yok → fırsat kaybı

Invekto burada:
A) Phase-1: C7 Knowledge: ürün katalogu + C8 AI önerisi
B) Phase-2+: Ürün linki + ödeme + kargo (e-ticaret entegrasyonu)

Gerekli yetenekler: C1, C3, C8, C7 Knowledge
KVKK risk: DUSUK

---
SENARYO GU-10 — Gelin paketi / özel gün
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Müşteri mesajı
   - 'Gelin paketi ne kadar? Saç + makyaj + cilt bakımı dahil mi?'
   - 'Düğün 15 Haziran, prova ne zaman?'
   - 'Nedimeler için de paket var mı?'

2) Bugün: Yüz yüze görüşme ile anlatılıyor, telefonda detay vermek zor
3) Nerede batıyor: Paket içeriği/fiyat standardize değil → her seferinde farklı bilgi

Invekto burada:
A) Phase-1: C7 Knowledge'dan gelin paketi detayları + C3 template ile fiyat/içerik gönderimi
B) Phase-2+: Prova randevusu + düğün günü randevusu booking, nedime paketleri

Gerekli yetenekler: C1, C3, C7, C8 + Randevu motoru (özel gün slotu)
KVKK risk: DUSUK
Yüksek birim değer (gelin paketi 5.000-15.000 TL) — düşük hacim, yüksek etki.

---
SENARYO GU-11 — Abonelik / üyelik paketi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Düzenli gelen müşteriye "Aylık 4 fön = %30 indirim" üyelik teklifi.

2) Bugün: Üyelik sistemi yok, her seferinde tek tek fiyat
3) Nerede batıyor: Düzenli müşteri sadakat hissetmiyor → rakip indirim yapınca gidiyor

Invekto burada:
A) Phase-2+: AI tekrarlayan ziyaret tespiti → üyelik teklifi Outbound mesajı
   "Her ay düzenli geliyorsunuz! Aylık bakım paketimiz size %30 tasarruf sağlar. Detay ister misiniz?"
B) Phase-3+: Üyelik CRUD + otomatik yenileme + tier sistemi

Gerekli yetenekler: C1, C3, C8, Outbound Engine + Abonelik altyapısı (Phase 3)
KVKK risk: ORTA (ödeme bilgisi)
S11 revenue senaryosunun güzellik salonu varyasyonu.

---
SENARYO GU-12 — Referral / arkadaş getir
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Memnun müşteriye "Arkadaşını getir, ikinize %15 indirim" teklifi.

2) Bugün: Sözlü söyleniyor, sistematik değil
3) Nerede batıyor: Referans takibi yok, kimin kimi getirdiği bilinmiyor

Invekto burada:
A) Phase-2+: İşlem sonrası Outbound: "Memnun kaldınız mı? Arkadaşınızı yönlendirin, ikinize %15 indirim!"
B) Phase-3+: Referans kodu + tracking + otomatik indirim uygulama

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK
S10 referans motoru ile aynı mekanik.

---
SENARYO GU-13 — Yorum / review isteme
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Senaryo: İşlem sonrası Google/Instagram yorumu rica etme.

2) Bugün: Bazen sözlü isteniliyor, çoğu zaman unutuluyor
3) Nerede batıyor: Memnun müşteri sessiz, memnuniyetsiz hemen yazıyor → puan düşüyor

Invekto burada:
A) Phase-1: Outbound: T+1 gün "İşleminizden memnun kaldınız mı? (1-5)"
   → 4-5 ise: Google yorum linki
   → 1-3 ise: "Üzgünüz, sorunu çözmek istiyoruz"

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK
S10 yorum motoru sektör varyasyonu.

---
SENARYO GU-14 — Kampanya / indirim bildirimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Senaryo: "Bu hafta keratin bakımda %20 indirim" kampanyası duyurmak.

2) Bugün: Instagram story + bireysel mesaj (tek tek gönderim)
3) Nerede batıyor: Toplu mesaj gönderimi yok → kampanya erişimi düşük

Invekto burada:
A) Phase-1: Outbound broadcast: segment bazlı kampanya mesajı
   Hedef: son 3 ayda saç bakımı yaptıran müşteriler → "Bu hafta keratin %20 indirimli!"
B) Phase-2+: Kampanya performance tracking (kaç mesaj → kaç randevu)

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK (opt-in zorunlu, marketing template)

---
SENARYO GU-15 — Frekans hatırlatma
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Senaryo: Saç boyama 6 haftada bir yapılmalı. Son boyama 5 hafta önce. Hatırlatma gönder.

2) Bugün: Müşteri kendisi hatırlayıp arıyor (veya rakibe gidiyor)
3) Nerede batıyor: Hatırlatma yapılmadığında müşteri başka salonu deniyor → kayıp

Invekto burada:
A) Phase-2+: Outbound trigger: son işlem tarihi + işlem tipi frekansı → "Saç boyamanızın üzerinden 5 hafta geçti. Randevu oluşturalım mı?"
B) Phase-3+: Müşteri bazlı kişiselleştirilmiş frekans öğrenme

Gerekli yetenekler: C1, C3, Outbound Engine + CRM (müşteri işlem geçmişi)
KVKK risk: DUSUK (opt-in zorunlu)
S7 no-show hatırlatma ile benzer mekanik, farklı tetikleyici.

---
SENARYO GU-16 — Personel mesai / çalışma günleri sorgulama
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Ayşe hanım hangi günler çalışıyor?'
   - 'Pazar açık mısınız?'

2) Bugün: Resepsiyonist elle kontrol ediyor
3) Nerede batıyor: Yanlış bilgi → müşteri geldi, kuaförü yok

Invekto burada:
A) Phase-1: C7 Knowledge'da personel mesai bilgisi → C8 AI cevabı
B) Phase-2+: Personel takvimi entegrasyonu

Gerekli yetenekler: C1, C3, C7, C8
KVKK risk: DUSUK

---
SENARYO GU-17 — Çoklu hizmet paketi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Saç + makyaj + cilt bakımı yaptırmak istiyorum, paket fiyat var mı?'
   - 'Düğün öncesi komple bakım ne kadar?'

2) Bugün: Her hizmet ayrı fiyatlandırılıyor, paket teklifi standart değil
3) Nerede batıyor: Cross-sell fırsatı kaçırılıyor

Invekto burada:
A) Phase-1: C8 AI paket önerisi: "Saç + makyaj birlikte %10 indirimli, 1.200 TL yerine 1.080 TL"
B) Phase-2+: Dinamik paket oluşturucu (hizmet seç → otomatik indirimli fiyat)

Gerekli yetenekler: C1, C3, C8 + C7 Knowledge (fiyat/paket bilgisi)
KVKK risk: DUSUK

---
SENARYO GU-18 — VIP müşteri yönetimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Ayda 3+ kez gelen, yüksek harcama yapan müşteri VIP olarak etiketlenmeli.

2) Bugün: VIP tanımı yok, herkes aynı muameleyi görüyor
3) Nerede batıyor: Sadık müşteri özel hissetmiyor → rakip salonda VIP muamelesi görünce gidiyor

Invekto burada:
A) Phase-2+: AI otomatik VIP scoring (frekans + harcama + referans)
   VIP müşteriye: öncelikli randevu, özel indirimler, doğum günü sürprizi
B) Phase-3+: VIP tier sistemi (Silver/Gold/Platinum)

Gerekli yetenekler: C1, C2, C8 + CRM scoring
KVKK risk: ORTA (kişisel bilgi profilleme)

---
SENARYO GU-19 — Alerjik reaksiyon / sorun bildirimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B | Grup: Kriz De-eskalasyon

1) Müşteri mesajı
   - 'Boyadan sonra başım çok kaşınıyor, kıpkırmızı oldu!'
   - 'Cilt bakımından sonra yüzüm şişti'

2) Bugün: Müşteri panik, salon geç cevap → müşteri hastaneye gidiyor → dava riski
3) Nerede batıyor: Sağlık riski + hukuki risk. Geç müdahale → kötüleşme

Invekto burada:
A) Phase-1: AI yüksek öncelik tespiti: alerji/reaksiyon intent → ACIL flag → salon sahibine ANINDA bildirim
   "Çok üzgünüz! Hemen uzmanımız sizinle ilgilenecek. Belirtileriniz şiddetliyse en yakın sağlık kuruluşuna başvurmanızı öneriyoruz."
B) Phase-2+: Alerji geçmişi kayıt → sonraki işlemde uyarı

Gerekli yetenekler: C1, C2, C8 (priority routing) + C5 (güvenlik kaydı)
KVKK risk: ORTA (sağlık bilgisi)
CS-02 handoff + CS-03 guardrail ile entegre çalışmalı.

---
SENARYO GU-20 — KVKK: fotoğraf çekimi / before-after
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Senaryo: Salon before/after fotoğrafı çekip Instagram'da paylaşmak istiyor. Müşteri onayı gerekli.

2) Bugün: Sözlü izin alınıyor (veya hiç alınmıyor) → KVKK ihlali
3) Nerede batıyor: Müşteri fotoğrafını görüp şikayet → ceza riski

Invekto burada:
A) Phase-1: İşlem öncesi otomatik onam mesajı:
   "İşlem sonrası fotoğraflarınızı sosyal medyamızda paylaşmamıza izin veriyor musunuz? (Evet/Hayır)"
   Cevap kayıt altına alınır (compliance log)
B) Phase-2+: Fotoğraf maskeleme (yüz bulanıklaştırma seçeneği)

Gerekli yetenekler: C1, C3, C5/C6 (consent management)
KVKK risk: ORTA (görsel kişisel veri)
CS-01 opt-in + CS-08 compliance ile entegre.

---
SENARYO GU-21 — Mevsimsel trend öneri
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Yaz sezonu → açık tonlar, kış → koyu tonlar. Müşteriye trend öneri.

2) Bugün: Kuaför yüz yüze öneriyor, uzaktan teklif yok
3) Nerede batıyor: Müşteri trend bilgisi olmadan gelip kötü seçim yapabiliyor

Invekto burada:
A) Phase-2+: Outbound sezonluk kampanya: "Bu yaz en trend saç renkleri! Sizin için hangisini öneriyoruz?"
   + AI kişiselleştirilmiş öneri (geçmiş tercihler + saç tipi)

Gerekli yetenekler: C1, C3, C8, Outbound Engine + C7 Knowledge (trend bilgisi)
KVKK risk: DUSUK

---
SENARYO GU-22 — Online ürün mağazası
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Geçen aldığım şampuandan bir daha istiyorum, kargolayabilir misiniz?'
   - 'Saç bakım seti satıyor musunuz?'

2) Bugün: Salonda fiziksel satış var, online sipariş yok
3) Nerede batıyor: Müşteri marketi/online rakibi tercih ediyor → ürün geliri kaybı

Invekto burada:
A) Phase-2+: WhatsApp üzerinden sipariş → ödeme linki → kargo/elden teslim
B) Phase-3+: Ürün katalogu entegrasyonu + otomatik stok kontrolü

Gerekli yetenekler: C1, C3, C8 + Ödeme entegrasyonu (Phase 2) + C11 stok (Phase 3)
KVKK risk: ORTA (ödeme bilgisi)

---
SENARYO GU-23 — Grup randevusu (kına gecesi, doğum günü)
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Müşteri mesajı
   - 'Kına gecesi için 8 kişilik makyaj + saç yapabilir misiniz?'
   - 'Doğum günü partisi için nail art var mı?'

2) Bugün: Grup randevusu koordinasyonu zor, tek tek mesajlaşma
3) Nerede batıyor: Kapasiteyi aşan grup kabul edilince kalite düşüyor

Invekto burada:
A) Phase-1: Grup randevusu intent → kapasite kontrolü → "8 kişi için [tarih] uygun. Onaylıyor musunuz?"
B) Phase-2+: Grup paketi fiyatlandırma + koordinasyon (tek kişi organize eder)

Gerekli yetenekler: C1, C3, C8 + Randevu motoru (grup desteği)
KVKK risk: DUSUK
Yüksek birim değer (grup 3.000-10.000 TL) — düşük hacim.

---
SENARYO GU-24 — Instagram DM lead yönetimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: A

1) Senaryo: Instagram story/reel'e gelen DM'ler: "Ne kadar?", "Nerede?", "Randevu alabilir miyim?"

2) Bugün: DM'ler geç cevaplanıyor, link gönderilemiyor (24h kuralı)
3) Nerede batıyor: Instagram → WhatsApp geçişi yapılmıyor → lead kayboluyor

Invekto burada:
A) Phase-1: C1 Unified Inbox ile IG DM tek yerden yönetim + C8 AI hızlı cevap
B) Phase-2+: IG → WA geçişi (link gönder), UTM tracking, reklam attribution

Gerekli yetenekler: C1, C2, C3, C8 + C12 Attribution (Phase 2+)
KVKK risk: DUSUK
Senaryo 53 (Estetik IG→WA) ile aynı mekanik — güzellik salonu varyasyonu.

---
SENARYO GU-25 — Franchise / çoklu şube yönetimi
Bölge: Türkiye | Dikey: Güzellik Salonu | Kanıt: B

1) Senaryo: Zincir salon, 3+ şube. Müşteri hangi şubeye gidecek? Hangi kuaför hangi şubede?

2) Bugün: Her şubenin ayrı WhatsApp'ı, merkezi yönetim yok
3) Nerede batıyor: Müşteri yanlış şubeye mesaj atıyor → yönlendirme gecikmesi

Invekto burada:
A) Phase-1: C2 routing: konum bazlı şube yönlendirme
B) Phase-2+: Merkezi dashboard (tüm şubelerin performansı), şube bazlı raporlama
C) Phase-3+: Multi-brand (aynı zincir farklı markalar)

Gerekli yetenekler: C1, C2, C3, C8 + Multi-branch routing
KVKK risk: DUSUK
SB-03 (Çoklu Klinik/Şube) ile aynı mekanik.

============================================================
I) EĞİTİM SENARYOLARI (EG-01 ~ EG-25)
============================================================

> **Sektör:** Dil kursları, dershaneler, mesleki eğitim, online eğitim, özel okullar, dans/müzik/sanat
> **Hedef müşteri:** 50-500 öğrencili kurs merkezleri, franchise eğitim zincirleri, online eğitim platformları
> **Kanal:** WhatsApp (birincil) + Instagram DM (genç kitle için) + Web (kayıt formları)
> **Phase:** Randevu/kayıt core altyapı Phase 1-2, ileri seviye (LMS entegrasyon) Phase 3+
> **Önemli:** Çocuk/genç verisi = KVKK özel koruma. Veli iletişimi çift taraflı.
> **v6 (2026-02-16):** D1 kararı ile eklenen yeni sektör

---
SENARYO EG-01 — Kayıt / başvuru süreci
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A | Grup: Kayıt

1) Müşteri mesajı (örnek konuşma)
   - Veli: 'İngilizce kursu için kayıt nasıl yapılıyor?'
   - Öğrenci: 'Yazılım kursuna kayıt olmak istiyorum, hangi belgeler lazım?'

2) Bugün işletme bunu nasıl yönetiyor?
   - Telefon + yüz yüze kayıt. WhatsApp'tan gelen sorulara geç dönülüyor
   - Kayıt formu elle dolduruluyor, evrak takibi karışıyor

3) Nerede batıyor?
   - Kayıt süreci uzun → aday başka kursa gidiyor (speed-to-response kritik)
   - Kayıt döneminde yoğunluk → tüm sorulara yetişilemiyor
   - Hangi belge gerekli net anlatılmıyor → tekrarlayan sorular

4) Gerçek maliyet
   - Kayıt döneminde günde 30-50 soru, %20'si cevapsız/gecikmeli
   - Kaçan kayıt: 5 × 3.000 TL = 15.000 TL/dönem kayıp

Invekto burada:
A) Bugün: C1 inbox + C3 template ile kayıt bilgi seti gönderimi
B) Phase-1: C8 AI Assist: kayıt intent → gerekli belgeler + fiyat + dönem bilgisi → "Kayıt formunu doldurmak ister misiniz? [link]"
   Automation flow: adım adım bilgi toplama (ad, yaş, seviye, tercih edilen gün)
C) Phase-2+: Online kayıt formu entegrasyonu, ödeme planı seçimi, otomatik sınıf ataması

Gerekli yetenekler: C1, C2, C3, C8 (Phase 1) + Kayıt motoru (Phase 2)
KVKK risk: ORTA (kişisel bilgi + çocuk verisi potansiyeli)

Pros: En yüksek hacimli intent, kayıt dönüşümüne direkt etki
Cons: Her kursun farklı belge/gereksinim kuralı → Knowledge base derinliği gerekli

---
SENARYO EG-02 — Fiyat / ücret sorgulama
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A | Grup: Fiyat

1) Müşteri mesajı
   - 'İngilizce A2 kursu ne kadar?'
   - 'Taksit yapabiliyor musunuz?'
   - 'Erken kayıt indirimi devam ediyor mu?'

2) Bugün: Telefonda fiyat veriliyor, WhatsApp'ta standart fiyat listesi yok
3) Nerede batıyor: Fiyat sorgusu = en sık mesaj. Geç cevap = kayıp

Invekto burada:
A) Phase-1: C8 AI: kurs + seviye intent tespiti → fiyat bilgisi + kampanya + taksit seçenekleri
   "A2 İngilizce: 4.500 TL / 3 taksit 1.500 TL. Erken kayıt ile 3.800 TL. Kayıt oluşturmamı ister misiniz?"
B) Phase-2+: C7 Knowledge'dan kurs katalogu + dinamik fiyatlandırma

Gerekli yetenekler: C1, C2, C3, C8 + C7 Knowledge (kurs/fiyat bilgisi)
KVKK risk: DUSUK

---
SENARYO EG-03 — Ders programı / saat sorgulama
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Hafta sonu İngilizce kursu var mı?'
   - 'Akşam 19:00 sonrası sınıf açılıyor mu?'
   - 'Online mı yoksa yüz yüze mi?'

2) Bugün: Müşteri danışmana ulaşmak zorunda, program bilgisi standartize değil
3) Nerede batıyor: Yanlış bilgi verilince müşteri geldi, ders yok → güven kaybı

Invekto burada:
A) Phase-1: C7 Knowledge'da program bilgisi → C8 AI cevabı
   "Cumartesi 10:00-13:00 A2 grubu müsait. Kontenjan 3 kişi. Kayıt oluşturmamı ister misiniz?"
B) Phase-2+: Gerçek zamanlı kontenjan bilgisi (LMS entegrasyonu)

Gerekli yetenekler: C1, C3, C7, C8
KVKK risk: DUSUK

---
SENARYO EG-04 — Eğitmen / öğretmen bilgisi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Müşteri mesajı
   - 'Bu kursu kim veriyor? Özgeçmişi var mı?'
   - 'Native speaker mı?'

2) Bugün: Sözlü anlatılıyor, eğitmen profiline link yok
3) Nerede batıyor: Eğitmen bilgisi güven faktörü — bilgi yoksa kayıt yok

Invekto burada:
A) Phase-1: C7 Knowledge'da eğitmen profilleri → C8 AI cevabı
B) Phase-2+: Eğitmen değerlendirme puanları, portfolyo gösterimi

Gerekli yetenekler: C1, C3, C7, C8
KVKK risk: DUSUK

---
SENARYO EG-05 — Seviye belirleme
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Hangi seviyeye girmem gerekiyor?'
   - 'Seviye tespit sınavı var mı?'

2) Bugün: Yüz yüze veya online test yapılıyor, WhatsApp'tan yönlendirme zor
3) Nerede batıyor: Seviye testi yaptırmadan kayıt → yanlış sınıf → memnuniyetsizlik

Invekto burada:
A) Phase-1: Automation flow: seviye testi intent → online test linki gönder → sonuç → uygun sınıf önerisi
   "Seviye testinize göre B1 grubuna uygunsunuz. Pazar 10:00 grubu müsait. Kayıt oluşturalım mı?"
B) Phase-2+: WhatsApp içinden mini seviye testi (interactive mesaj)

Gerekli yetenekler: C1, C3, C8 + Automation flow (quiz)
KVKK risk: DUSUK

---
SENARYO EG-06 — Devamsızlık bildirimi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Bugün derse gelemeyeceğim'
   - Veli: 'Çocuğum hasta, bu hafta devam edemeyecek'

2) Bugün: Telefon / SMS, kayıt tutma manuel
3) Nerede batıyor: Devamsızlık takibi zorlaşıyor, telafi planı yapılamıyor

Invekto burada:
A) Phase-1: AI devamsızlık intent → kayıt + telafi teklifi
   "Geçmiş olsun! Kaçırdığınız dersi [tarih] telafi edebilirsiniz. Uygun mu?"
B) Phase-2+: Otomatik devamsızlık takip + uyarı (3+ ders → veli bilgilendirme)

Gerekli yetenekler: C1, C3, C8 + Devamsızlık tracking
KVKK risk: ORTA (sağlık bilgisi dolaylı olarak paylaşılabilir)

---
SENARYO EG-07 — Ödeme planı / taksit
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Taksitle ödeyebilir miyim?'
   - 'Kredi kartı geçiyor mu?'
   - 'Ödeme son tarihi ne zaman?'

2) Bugün: Muhasebe ile ayrı görüşme gerekiyor
3) Nerede batıyor: Ödeme belirsizliği kayıt kararını geciktiriyor

Invekto burada:
A) Phase-1: C8 AI: ödeme seçenekleri bilgisi → "3 taksit (1.500 TL × 3), kredi kartı + havale kabul ediyoruz"
B) Phase-2+: Online ödeme linki (iyzico/PayTR) + otomatik taksit takibi + gecikme hatırlatma

Gerekli yetenekler: C1, C3, C8 + Ödeme entegrasyonu (Phase 2)
KVKK risk: ORTA (ödeme bilgisi)

---
SENARYO EG-08 — İade / cayma / kurs bırakma
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Kursu bırakmak istiyorum, paramı geri alabilir miyim?'
   - 'İlk 2 derse geldim, cayma hakkım var mı?'

2) Bugün: İade süreci uzun, net kurallar bilinmiyor → şikayet
3) Nerede batıyor: İade politikası net anlatılmazsa → Tüketici Hakem Heyeti şikayeti

Invekto burada:
A) Phase-1: C8 AI: iade intent → iade politikası bilgisi (knowledge base) + form yönlendirme
   "Cayma hakkınız ilk 7 gün içinde geçerlidir. İade işlemi başlatmak ister misiniz?"
B) Phase-2+: Iade çevirme: "Grup/gün değişikliği ister misiniz?" → bırakmayı önleme

Gerekli yetenekler: C1, C3, C8 + C7 Knowledge (iade politikası)
KVKK risk: ORTA (kişisel + ödeme bilgisi)
S3 (İade Çevirme) ile aynı mekanik — eğitim varyasyonu.

---
SENARYO EG-09 — Ders materyali / not paylaşımı
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Dün ki dersin notlarını gönderir misiniz?'
   - 'Ödev dosyası açılmıyor'
   - 'Hangi kitabı kullanıyoruz?'

2) Bugün: Eğitmen WhatsApp grubundan paylaşıyor, geçmiş dosyalar kaybolıyor
3) Nerede batıyor: Materyal kaybı → öğrenci şikayeti, tekrar gönderim yükü

Invekto burada:
A) Phase-1: Automation flow: materyal isteme intent → ilgili dosya/link gönderimi (Knowledge base'den)
B) Phase-2+: LMS entegrasyonu ile otomatik materyal paylaşımı

Gerekli yetenekler: C1, C3, C7 Knowledge + dosya paylaşım
KVKK risk: DUSUK

---
SENARYO EG-10 — Sınav / değerlendirme sonuçları
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Sınav sonuçları ne zaman açıklanıyor?'
   - 'Puanım kaç?'
   - Veli: 'Çocuğumun sınav sonucu nasıl?'

2) Bugün: Eğitmen tek tek söylüyor veya mail atıyor
3) Nerede batıyor: Gecikmeli sonuç bildirimi → öğrenci/veli memnuniyetsizliği

Invekto burada:
A) Phase-2+: Outbound trigger: sınav sonucu yayınlandı → otomatik bildirim
   Öğrenciye: "B1 sınavınızın sonucu: 82/100. Tebrikler! B2'ye geçiş için kayıt açıktır."
   Veliye: "[Öğrenci adı] sınav sonucu: 82/100 (Başarılı)"
B) Phase-3+: LMS entegrasyonu ile otomatik sonuç çekme

Gerekli yetenekler: C1, C3, Outbound Engine + LMS entegrasyonu (Phase 3)
KVKK risk: YUKSEK (eğitim verisi + çocuk verisi = özel koruma)

---
SENARYO EG-11 — Veli iletişimi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı (veli)
   - 'Çocuğumun durumu nasıl? Derslere düzenli geliyor mu?'
   - 'Hocasıyla görüşmek istiyorum'

2) Bugün: Veli toplantısı + telefon. WhatsApp'tan düzenli bilgilendirme yok
3) Nerede batıyor: Veli bilgilendirilmezse → memnuniyetsizlik → kayıt yenilememe

Invekto burada:
A) Phase-2+: Outbound periyodik rapor: aylık devam + performans özeti
   "Bu ay [Ad] 12/16 derse katıldı. Genel performans: İyi. Ödev tamamlama: %85"
B) Phase-3+: Veli portalı + anında durum sorgulama

Gerekli yetenekler: C1, C3, Outbound Engine + Veli-öğrenci ilişki yönetimi
KVKK risk: YUKSEK (çocuk verisi + eğitim performansı)
Çift taraflı iletişim: öğrenci + veli → farklı mesaj içerikleri.

---
SENARYO EG-12 — Online ders teknik destek
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Zoom açılmıyor!'
   - 'Ses gelmiyor, ne yapacağım?'
   - 'Ders linki gelmedi'

2) Bugün: Eğitmen ders başında teknik sorunla uğraşıyor → ders gecikmesi
3) Nerede batıyor: Teknik sorun = ders kaybı = müşteri memnuniyetsizliği

Invekto burada:
A) Phase-1: C8 AI Assist: teknik sorun intent → adım adım çözüm rehberi (Knowledge base)
   "Zoom açılmıyorsa: 1) Uygulamayı güncelleyin 2) Bağlantınızı kontrol edin 3) [yardım linki]"
   → Çözülmezse insan desteğe yönlendir
B) Phase-2+: Otomatik ders linki gönderimi (ders başlamadan 10dk önce)

Gerekli yetenekler: C1, C3, C7 Knowledge (teknik sorun çözümleri), C8
KVKK risk: DUSUK

---
SENARYO EG-13 — Sertifika / diploma sorgulama
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Kurs bittiğinde sertifika veriliyor mu?'
   - 'Sertifikam ne zaman hazır olacak?'
   - 'MEB onaylı mı?'

2) Bugün: Her seferinde aynı bilgi anlatılıyor (sertifika tipi, süre, koşullar)
3) Nerede batıyor: Sertifika beklentisi karşılanmazsa → şikayet + olumsuz yorum

Invekto burada:
A) Phase-1: C7 Knowledge: sertifika bilgisi → C8 AI cevabı
   "Kurs sonunda MEB onaylı sertifika verilmektedir. Koşul: %80 devam + sınav başarısı"
B) Phase-2+: Sertifika hazırlık bildirimi (Outbound: "Sertifikanız hazır, teslim almak ister misiniz?")

Gerekli yetenekler: C1, C3, C7, C8
KVKK risk: DUSUK

---
SENARYO EG-14 — Kurs değişikliği / transfer
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Müşteri mesajı
   - 'Başka gruba geçebilir miyim? Bu saat bana uymuyor'
   - 'Seviye atlamak istiyorum'

2) Bugün: Danışmanla görüşme + yönetici onayı gerekiyor → uzun süreç
3) Nerede batıyor: Transfer süreci uzun → öğrenci motivasyon kaybı veya bırakma

Invekto burada:
A) Phase-1: C8 AI: transfer intent → müsait grup bilgisi → talep formu
B) Phase-2+: Otomatik kontenjan kontrolü + müsait gruplara anlık transfer

Gerekli yetenekler: C1, C3, C8 + Sınıf/grup yönetimi (Phase 2)
KVKK risk: DUSUK

---
SENARYO EG-15 — Kampanya / erken kayıt indirimi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Erken kayıt indirimi var mı?'
   - 'Kampanya ne zaman bitiyor?'

2) Bugün: Instagram/web'de duyuru yapılıyor ama ilgili kişilere direkt bildirim yok
3) Nerede batıyor: Kampanya bilgisi doğru kitleye ulaşmıyor → kayıt potansiyeli kayboluyor

Invekto burada:
A) Phase-1: Outbound broadcast: "Erken kayıt başladı! [kurs] %20 indirimle 3.600 TL. Son 3 gün!"
   Hedef: geçmiş dönem öğrencileri + tamamlanmamış kayıtlar
B) Phase-2+: Kampanya performance tracking (mesaj → kayıt dönüşümü)

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK (opt-in zorunlu, marketing template)

---
SENARYO EG-16 — Referral / arkadaş getir indirimi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Senaryo: "Arkadaşını getir, ikinize %15 indirim" kampanyası.

2) Bugün: Sözlü söyleniyor, takip yok
3) Nerede batıyor: Referans kaynağı belirsiz → pazarlama bütçesi optimize edilemiyor

Invekto burada:
A) Phase-2+: Dönem sonu Outbound: "Memnun kaldınız mı? Arkadaşınızı yönlendirin, ikinize %15 indirim!"
B) Phase-3+: Referans kodu + tracking + otomatik indirim

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK
S10 referans motoru + GU-12 ile aynı mekanik.

---
SENARYO EG-17 — Şikayet / memnuniyetsizlik
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A | Grup: Kriz De-eskalasyon

1) Müşteri mesajı
   - 'Hocadan hiç memnun değilim, ders anlatamıyor!'
   - 'Vaat edilen içerik verilmedi, para iadesi istiyorum'
   - Veli: 'Çocuğum dersten nefret ediyor, bir şey öğrenememiş'

2) Bugün: Şikayet yönetimi yok → yorum sitelerine/sosyal medyaya yansıyor
3) Nerede batıyor: Şikayet yönetilmezse → toplu kayıt yenilememe + olumsuz WOM

Invekto burada:
A) Phase-1: C8 AI şikayet intent → empati + acil yönlendirme
   "Geri bildiriminiz bizim için çok değerli. Eğitim koordinatörümüz sizinle iletişime geçecek."
   → Severity scoring: düşük → standart, yüksek → müdür alert
B) Phase-2+: Şikayet kategorize + trend analizi + kök neden tespiti

Gerekli yetenekler: C1, C2, C3, C8 + Sentiment (Phase 3)
KVKK risk: DUSUK

---
SENARYO EG-18 — Yorum / review isteme
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Senaryo: Dönem sonu, memnun öğrenciden Google/kurumsal site yorumu rica etme.

2) Bugün: Sistematik yorum toplama yok
3) Nerede batıyor: Yeni kayıt adayları "yorum" arıyor → yorum yoksa güven düşük

Invekto burada:
A) Phase-2+: Outbound: dönem sonu "Deneyiminizi paylaşır mısınız? (1-5)" → 4-5 ise Google link
B) Phase-3+: NPS anketi + trend takibi

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK
S10 + GU-13 ile aynı mekanik.

---
SENARYO EG-19 — Devam takibi hatırlatma
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Senaryo: Öğrenci 3 ders üst üste gelmedi. Kayıp riski var.

2) Bugün: Eğitmen fark edip söylemezse kimse aramıyor
3) Nerede batıyor: Devamsız öğrenci sessizce bırakıyor → kayıt yenilemiyor → gelir kaybı

Invekto burada:
A) Phase-2+: Otomatik devamsızlık alert:
   - 2 ders: Öğrenciye "Sizi özledik! Bir sorun mu var?"
   - 3 ders: Veliye bilgilendirme + danışman alert
   - 5+ ders: Supervisor eskalasyon + kurtarma teklifi (telafi/grup değişikliği)
B) Phase-3+: Churn prediction (S12 ile entegre)

Gerekli yetenekler: C1, C3, Outbound Engine + Devamsızlık tracking
KVKK risk: ORTA (eğitim durumu)
S12 Churn Prevention eğitim varyasyonu.

---
SENARYO EG-20 — Kariyer koçluğu / iş bulma desteği
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Müşteri mesajı
   - 'Bu kursu bitirince iş bulabilir miyim?'
   - 'Staj imkanı var mı?'
   - 'CV hazırlamada yardım ediyor musunuz?'

2) Bugün: Kursun satış argümanı ama gerçek destek yok
3) Nerede batıyor: Vaat edilen iş desteği karşılanmazsa → güven kaybı + olumsuz yorum

Invekto burada:
A) Phase-1: C7 Knowledge: kariyer bilgisi + partner şirketler → C8 AI cevabı
B) Phase-3+: Mezun-şirket eşleştirme, iş fırsatı bildirimi (Outbound)

Gerekli yetenekler: C1, C3, C7, C8 + İş ortağı entegrasyonu (Phase 3+)
KVKK risk: ORTA (CV/kişisel bilgi)

---
SENARYO EG-21 — Mezun takibi / alumni
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Senaryo: Mezunlarla iletişimi sürdürme. İleri seviye kurs teklifi, etkinlik daveti, başarı hikayeleri.

2) Bugün: Mezunlarla iletişim kopuyor
3) Nerede batıyor: Repeat customer (yeni kurs kaydı) potansiyeli kaçırılıyor

Invekto burada:
A) Phase-2+: Outbound periyodik: "Yeni B2 İngilizce kursumuz başlıyor! Mezunlara %20 özel indirim"
B) Phase-3+: Alumni topluluğu, etkinlik yönetimi

Gerekli yetenekler: C1, C3, Outbound Engine
KVKK risk: DUSUK (opt-in zorunlu)

---
SENARYO EG-22 — Özel ders talebi
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A

1) Müşteri mesajı
   - 'Birebir ders alabilir miyim?'
   - 'Online özel ders fiyatı ne kadar?'
   - 'Sınav hazırlık için 10 saatlik paket var mı?'

2) Bugün: Danışman elle eşleştirme yapıyor
3) Nerede batıyor: Uygun eğitmen bulunması → gecikmeli dönüş → müşteri rakibe gidiyor

Invekto burada:
A) Phase-1: C8 AI: özel ders intent → müsait eğitmen + saat + fiyat bilgisi
B) Phase-2+: Eğitmen müsaitlik takvimi + otomatik eşleştirme

Gerekli yetenekler: C1, C3, C8 + C7 Knowledge (eğitmen bilgisi) + Randevu motoru
KVKK risk: DUSUK

---
SENARYO EG-23 — KVKK: çocuk verisi özel koruma
Bölge: Türkiye | Dikey: Eğitim | Kanıt: A | Grup: KVKK/Veri Güvenliği

1) Senaryo: 18 yaş altı öğrenci verisi KVKK kapsamında özel nitelikli kişisel veri.
   Fotoğraf, devam durumu, sınav sonucu, sağlık bilgisi → veli izni ZORUNLU.

2) Bugün: Veli izni sözlü alınıyor veya formla (sistematik değil)
3) Nerede batıyor: İzinsiz veri paylaşımı → KVKK ihlali → ceza riski

Invekto burada:
A) Phase-1: Kayıt sürecinde otomatik KVKK onam mesajı (veli'ye):
   "Çocuğunuzun eğitim sürecindeki verilerin (devam, sınav, fotoğraf) işlenmesine izin veriyor musunuz?"
   Cevap kayıt altına alınır (compliance log)
B) Phase-2+: Veri erişim/silme talepleri otomatik iş akışı
   Maskeleme: çocuk fotoğrafı, TC kimlik özel koruma

Gerekli yetenekler: C1, C3, C5/C6 (consent management) + CS-01 opt-in + CS-08 compliance
KVKK risk: YUKSEK (çocuk verisi = özel nitelikli)
CS-08 (Compliance Otomasyonu) ile ZORUNLU entegrasyon.

---
SENARYO EG-24 — Staj / pratik sorgulama
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Müşteri mesajı
   - 'Kurs süresinde staj imkanı var mı?'
   - 'Hangi şirketlerle anlaşmalısınız?'

2) Bugün: Genel bilgi veriliyor, detay yok
3) Nerede batıyor: Staj beklentisi karşılanmazsa → kurs değerliliği sorgulanıyor

Invekto burada:
A) Phase-1: C7 Knowledge: staj bilgisi + partner listesi → C8 AI cevabı
B) Phase-3+: Staj eşleştirme + takip sistemi

Gerekli yetenekler: C1, C3, C7, C8
KVKK risk: DUSUK

---
SENARYO EG-25 — Çoklu kurs / paket seçenekleri
Bölge: Türkiye | Dikey: Eğitim | Kanıt: B

1) Müşteri mesajı
   - 'İngilizce + Excel paketi var mı?'
   - '2 kurs birden alırsam indirim olur mu?'

2) Bugün: Paket seçenekleri standart değil, her seferinde ayrı hesaplanıyor
3) Nerede batıyor: Cross-sell fırsatı kaçırılıyor → tek kurs geliri ile sınırlı kalıyor

Invekto burada:
A) Phase-1: C8 AI: paket önerisi "İngilizce + Excel birlikte %15 indirimli: 6.800 TL yerine 5.780 TL"
B) Phase-2+: Dinamik paket oluşturucu + kampanya entegrasyonu

Gerekli yetenekler: C1, C3, C8 + C7 Knowledge (kurs katalogu)
KVKK risk: DUSUK
GU-17 (Çoklu hizmet paketi) ile aynı mekanik.
