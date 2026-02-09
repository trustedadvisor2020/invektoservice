# Invekto — Senaryo Portföyü & Outbound Engine

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 10 revenue senaryosu (3 niche) + 75 saha senaryosu (3 sektör) + 10 otel senaryosu + 5 mobil senaryo + E-ticaret & Sağlık senaryoları detay + Outbound Engine kritik bulgusu ve gereksinimleri
> Güncelleme: v2 — 75 senaryo (25 e-ticaret + 25 diş + 25 klinik/estetik) + capability mapping eklendi
> Güncelleme: v3 — 3 niche ortak capability analizi eklendi (2026-02-08)
> **Güncelleme: Mevcut ürün gerçekliği ile yeniden çerçevelendi (2026-02-08)**
> Referans: [whatisinvekto.md](whatisinvekto.md) — Invekto mevcut ürün envanteri

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

## Senaryo Portföyü (10 Senaryo — 3 Niche)

> Roadmap sadece "kargom nerede" senaryosuna değil, 10 farklı revenue senaryosuna dayanıyor.
> Her senaryo test edilmiş Hormozi değer denklemine uygun.

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

Legend:
- Kanıt seviyesi: A=Kaynaklı/yaygın doğrulanmış, B=Yaygın saha pratiği (tekil kanıt zayıf), C=Doğrulanamadı (kullanmamaya çalıştım)
- WA/IG kuralları: 24 saat penceresi + template/opt-in kısıtları senaryolarda dikkate alındı. [WA][IG]

---
SENARYO 01 — Kargom nerede? takip linki istiyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

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
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: A

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
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

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
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

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
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

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
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

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
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

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
SENARYO 26 — Fiyat sorusu: implant kaç TL? 'tek çene' vs
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Fiyat sorusu: implant kaç TL? 'tek çene' vs'
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
   - C13: kalite skoru + kaçan lead analizi (Phase 6)

Gerekli yetenekler (capability mapping)
   - Phase 1 başlangıç: C1, C2, C3, C8 (konfigüre fiyat aralıkları + AI Assist)
   - Tam set: C1, C2, C3, C7, C8 (+ Knowledge base — Phase 3)

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

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
SENARYO 29 — No-show: hasta gelmedi, hatırlatma yok
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'No-show: hasta gelmedi, hatırlatma yok'
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
SENARYO 32 — Öncesi/sonrası bakım talimatı (çekim sonrası)
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Öncesi/sonrası bakım talimatı (çekim sonrası)'
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
SENARYO 33 — Hasta kimlik/rapor gönderdi: KVKK riski
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: A

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
SENARYO 34 — Yabancı hasta (EU): fiyat + otel/transfer sorusu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yabancı hasta (EU): fiyat + otel/transfer sorusu'
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
SENARYO 35 — Şikayet: 'dolgu düştü' tekrar randevu
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

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
Bölge: Avrupa | Dikey: Diş | Avatar: D1 | Kanıt: A

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
SENARYO 45 — Konsültasyon sonrası takip: 'nasılsınız' mesajı (template/opt-in)
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Konsültasyon sonrası takip: 'nasılsınız' mesajı (template/opt-in)'
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
SENARYO 50 — Hasta memnuniyet anketi (opt-in)
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hasta memnuniyet anketi (opt-in)'
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
SENARYO 55 — No-show: hatırlatma ve yeniden kazanım
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'No-show: hatırlatma ve yeniden kazanım'
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
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

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
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

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
SENARYO 61 — Yabancı hasta (EU): transfer/otel + fiyat
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Yabancı hasta (EU): transfer/otel + fiyat'
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
SENARYO 62 — KVKK: foto/video sağlık verisi
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

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
SENARYO 67 — İade/iptal: kapora geri ister
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İade/iptal: kapora geri ister'
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
SENARYO 72 — Post-op bakım talimatı standardizasyonu
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Post-op bakım talimatı standardizasyonu'
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
SENARYO 73 — Memnuniyet anketi + referral isteme (opt-in)
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Memnuniyet anketi + referral isteme (opt-in)'
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
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: A

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

> **Sonuç:** Oteller için Phase 1 chatbot + Outbound'dan büyük fayda var (O2, O4, O8, O10).
> PMS entegrasyonu Phase 2'de otel niche'ini farklılaştırır (O1, O3, O5).
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

**Mobil Senaryo Özeti:**
- M1-M3: TÜM sektörlerde geçerli (Phase 7)
- M4: Sağlık niche'i için kritik (doktor mesai dışı erişim)
- M5: E-ticaret niche'i için faydalı (hareket halinde yönetim)
- **Tüm senaryolar mevcut API'leri tüketir — yeni backend gerekmez.**

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
