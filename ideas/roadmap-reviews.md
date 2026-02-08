# Invekto — Uzman Review'ları (Roadmap v3 Üzerine)

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 4 uzman perspektifinden review + aksiyonlar

**Genel hüküm:** Bu roadmap mühendislik olarak güçlü, senaryo olarak zengin —
ama positioning bulanık, SaaS metrikleri eksik, ürün akışı tanımsız, offer yapısı yok.
Şu haliyle: **iç ekip için harita, pazar için ham.**

---

## 1. April Dunford (Positioning Gözüyle)

**Ne doğru:**
- Niche seçimi net: Trendyol/HB satıcıları → sonra sağlık
- Avatar (Mehmet / Dr. Ayşe) iyi yazılmış
- ROI hesapları somut
- Persona seti gerçekçi (E1/E2 ve klinik)
- 75 senaryo saha gerçeklerinden geliyor
- Capability mapping disiplinli yapılmış

**Kritik problem — KATEGORİ YOK:**

Roadmap'te Invekto aynı anda 4 kategori:

| # | Kategori | Nerede geçiyor |
|---|----------|----------------|
| 1 | WhatsApp CRM | Product Story |
| 2 | AI Agent platformu | Phase 3 AgentAI |
| 3 | Revenue OS | Phase 5 Revenue Agent |
| 4 | E-ticaret otomasyon aracı | Trendyol entegrasyonları |

**"Bu dört ürün ayrı ayrı satılır. Sen kategori yaratmıyorsun, 4 kategoriye aynı anda girmeye çalışıyorsun."**

**Müşteri anlayamıyor: "Invekto hangi rafta duruyor?"**

Dunford'un zorlayacağı tek doğru konum:

> *"WhatsApp üzerinden satış yapan ekipler için AI destekli operasyon sistemi."*

**Sonra tüm özellikleri buna bağla:**
- Trendyol → satış operasyonu
- Agent Assist → satış hızı
- Auto-resolution → satış maliyeti
- Security → kurumsal satış

**"Bunu yapmazsan web sitesi + satış deck'i dağılır."**

**Önerilen wedge positioning:**
> "Trendyol ve Hepsiburada satıcıları için WhatsApp üzerinden sipariş sonrası yükü otomatik kapatan AI."

Hepsi bu. Revenue Agent, sağlık, outbound = sonra.

**Aksiyon durumu:**
- ✅ Positioning bölümü eklendi ([roadmap.md](roadmap.md) başına)
- ✅ Landing page'e yazılacak tek cümle sabitlendi
- ⬜ **Web sitesi + satış deck'inde tüm feature'ları wedge positioning'e bağla**
- ⬜ **"WhatsApp CRM" → "AI Destekli Operasyon Sistemi" dil değişikliği**

---

## 2. Jason Lemkin (SaaS Ölçek Gözüyle)

**Ne doğru:**
- Phase 0'da satışa çıkma fikri çok iyi
- Revenue-first yaklaşım mantıklı
- Phase bazlı genişleme gerçekçi
- Security Phase-1'e alınmış ✅
- Audit + tenant izolasyonu var
- WhatsApp policy farkındalığı mevcut

**Kritik problem 1 — Auth Phase 4 = çok geç:**

```
Kurumsal müşteri:
  → SSO sorar         ← Phase 4'te
  → Audit sorar       ← Phase 4'te
  → KVKK sorar        ← Phase 4'te

Bunlar olmadan pipeline bile açılmaz.

"Kurumsal gelince yaparız" = "Kurumsal gelmeyecek çünkü bunlar yok."
```

> **Çözüm:** Phase 3 sonunda kurumsal talep sayısını ölç. ≥3 "SSO var mı?" sorusu → Auth'u Phase 3.5'e çek.

**Kritik problem 2 — Core retention metric yok:**

MRR yazıyor ama:
- Net logo churn hedefi yok
- Activation tanımı yok
- "Customer is live" ne demek belli değil

**Kritik problem 3 — Customer onboarding akışı yok:**

**"Müşteri ilk 30 dakikada ne kuruyor? İlk değer ne zaman geliyor?"**

Enterprise SaaS'ta ilk value <48 saat olmazsa churn.

**İlk değer anı tanımlı değil:**
- Mehmet sisteme giriyor → sonra ne?
- Kurulum kaç adım?
- İlk "otomatik çözüm" ne zaman görülüyor?

**Kritik problem 4 — Expansion modeli tanımlı değil:**

Revenue driver'lar eksik:
- Agent seat pricing?
- Conversation volume pricing?
- AI credits pricing?
- Integration count pricing?

Roadmap bunları içermiyor. "Nasıl büyüyecek?" sorusu cevapsız.

**Kritik problem 5 — Timeline gerçekçi değil:**

90 gün gerçekçi değil. Auth+Audit+Knowledge+AgentAI+Trendyol tek kişiyle **minimum 5-6 ay**.

Doküman gizlice mini ekip varsayıyor ama yazmıyor.

**Aksiyon durumu:**
- ✅ Core SaaS Metrics kutusu eklendi ([roadmap-phases.md](roadmap-phases.md) Phase 1-2 arasına)
- ✅ 5 zorunlu metrik tanımlandı: TTFAR, Weekly Deflection %, 30-Day Logo Retention, Activation tanımı, Net Logo Churn
- ✅ Auth zamanlama uyarısı eklendi
- ⬜ **Onboarding flow tanımla (0-48 saat içinde value)**
- ⬜ **Expansion/pricing modeli belirle (seat/volume/credit)**
- ⬜ **Timeline'ı tek kişi gerçekliğiyle revize et (5-6 ay)**

---

## 3. Lenny Rachitsky (Product Gözüyle)

**Ne doğru:**
- Senaryolar (S1–S10) çok güçlü
- Pain → feature eşleşmesi iyi
- Mehmet avatarı gerçek
- Scenario → capability mapping çok iyi yapılmış

**Kritik problem 1 — PRIMARY USER FLOW yok:**

**"Mehmet Invekto'ya girer → sonra ne olur?" tanımlı değil.**

```
Roadmap'te cevabı olmayan sorular:
  → İlk kullanıcı 1. gün ne yapıyor?
  → İlk değer anı (aha moment) neresi?
  → 7 gün sonra ne görüyor?
  → 30 gün sonra neden kalıyor?

Her şey backend, phase, servis.
Kullanıcı akışı = YOK.
```

**Olması gereken primary flow:**

```
1. Hesap açar
2. WhatsApp bağlar
3. Trendyol bağlar
4. İlk "kargom nerede" otomatik çözülür
5. "Vay amk" moment
```

Lenny'nin zorlayacağı minimum flow:

```
1. Connect Trendyol
2. Seç: "Kargo Soruları"
3. Turn ON
4. İlk otomatik cevap
5. Dashboard'da: "% saved time"
```

Bu akış tanımlı değilse ürün öğrenmez.

**Kritik problem 2 — AI öğrenme eğrisi yok:**

- Temsilci AI'ya nasıl güvenecek?
- AI yanlış cevap verirse ne olacak?
- AI ne zaman devreden çıkacak?
- Human-in-the-loop flow tanımsız

**Güçlü taraf:** Scenario → capability mapping çok iyi. **Ama product layer bunun üstüne oturmamış.**

**Aksiyon durumu:**
- ✅ User First-Value Flow eklendi ([roadmap-phases.md](roadmap-phases.md) Phase 1 sonuna)
- ✅ Day 1 → Day 7 → Day 30 akışı tanımlandı
- ✅ Aha moment: "Gerçekten otomatik cevapladı!" olarak sabitlendi
- ⬜ **AI güven eğrisi tanımla (AI yanıldığında ne olur?)**
- ⬜ **Human-in-the-loop flow detaylandır**
- ⬜ **Product layer'ı scenario mapping'e bağla (UI mockup)**

---

## 4. Alex Hormozi (Offer/Pricing Gözüyle) — YENİ

**Ne doğru:**
- Teknik ürün güçlü
- Türkiye senaryoları gerçek
- ROI hesaplamaları var

**Ana problem — OFFER YOK:**

**Bu kadar ağır sistem KOBİ'ye satılmaz.**

**Şu an sattığın şeyler:**
- WhatsApp CRM
- AI agent
- Trendyol entegrasyonu

**→ Bunlar feature. OFFER değil.**

**Satman gereken:**
> "WhatsApp'tan gelen satışları %20 artıran sistem."

**Eksik olanlar:**

| Offer Component | Roadmap'te var mı? |
|-----------------|-------------------|
| Setup ücretsiz mi? | ❌ Yok |
| İlk 30 gün AI assist ücretsiz mi? | ❌ Yok |
| Revenue share opsiyonu? | ❌ Yok |
| Garantili ROI vaadi? | ❌ Yok |
| "Risk reversal" mekanizması? | ❌ Yok |

**Hormozi'nin zorlayacağı soru:**

> "Müşteri neden BUGÜN almalı? Feature listesi değil, SONUÇ ne?"

**Offer katmanı raporda yok.**

Ya "Revenue OS" diye paketlersin ya da satamazsın.

**Önerilen offer yapıları:**

1. **Freemium wedge:**
   - İlk 100 konuşma/ay ücretsiz
   - Trendyol bağlantısı ücretsiz
   - Upgrade: AI agent + auto-resolution

2. **Performans bazlı:**
   - "Saved support hours" başına ücret
   - İlk ay garanti yok (proof period)
   - ROI < %50 ise para iadesi

3. **Revenue share:**
   - Setup ücretsiz
   - Artan satışın %15'i Invekto'ya
   - Risk müşteride değil, Invekto'da

**Roadmap hiçbirini içermiyor.**

**Aksiyon durumu:**
- ⬜ **Offer yapısı tanımla (freemium/performans/revenue share)**
- ⬜ **Pricing tiers belirle (feature'dan outcome'a dönüştür)**
- ⬜ **Risk reversal mekanizması ekle (garanti/iade)**
- ⬜ **"Bugün al" incentive'i tanımla**
- ⬜ **Landing page'de feature listesi → outcome promise değişikliği**

---

## Q KARARI: 3 NİCHE PARALEL GİRİŞ (2026-02-08)

> **Karar:** Q, 4 uzmanın "önce 1 niche kanıtla" önerisine rağmen 3 niche'e paralel girmeye karar verdi.
>
> **Gerekçe:**
> - Ortak altyapı %95 aynı (C1+C2+C3+C8 tüm sektörlerde)
> - Türkiye pazarı küçük, tek niche'te tavan düşük
> - Sağlık ARPU 3-5x daha yüksek
> - Reklam ve web siteleri sektör bazlı ayrılacak
>
> **Yapı:** Tek platform (Invekto) + 3 ayrı offer (Sellers / Dental / Clinics)
>
> **Risk mitigasyonu:**
> - Phase 0'da 3 niche'te de 10'ar görüşme yapılır
> - Hangisinde 0 ilgi → o niche ertelenir (pivot değil, erteleme)
> - Ortak altyapı tek codebase — sektör farkı = config, kod değil
>
> **Etki:** roadmap.md, roadmap-phases.md güncellenmiştir.
> Phase 0-2 artık 3 niche paralel validasyon + satış + ölçekleme içerir.
> MRR hedefleri yeniden hesaplanmıştır (tek niche'e göre ~2x artış).

---

## GENEL SONUÇ (4 Uzman Konsensüs)

### ✅ Güçlü Yanlar:
- Teknik olarak güçlü
- Türkiye senaryoları gerçek
- AI vizyonu doğru
- Security sırası mükemmel
- Capability mapping disiplinli
- Persona seti gerçekçi

### ❌ Kritik Eksikler:
- **Positioning yok** → Müşteri "Invekto ne?" sorusunu cevaplayamaz
- **Ana ürün hikayesi yok** → Feature'dan outcome'a dönüşmemiş
- **Kullanıcı yolculuğu yok** → İlk 48 saat tanımsız
- **SaaS pricing motion yok** → Expansion modeli eksik
- **Offer yapısı yok** → "Neden bugün alayım?" cevabı yok
- **Tek kişiyle yapılabilir gibi yazılmış** → 5-6 ay gerçeği gizli

---

## TEK KRİTİK AKSİYON

**"Invekto kim için, hangi 1 problemi çözer?" → Tek cümle.**

### Önerilen cümle:

> **"Invekto helps WhatsApp-based sellers automatically resolve support and close sales using AI agents."**

Bu cümle:
- ✅ Kim için → WhatsApp-based sellers
- ✅ Ne yapıyor → Automatically resolve support
- ✅ Nasıl → Using AI agents
- ✅ Outcome → Close sales (revenue)

**Bu cümle her yerde tutarlı olmalı:**
- Landing page hero
- LinkedIn bio
- Satış pitch ilk cümle
- Demo başlangıcı
- Email signature

---

## Özet: 4 Uzmanın Verdikleri

| Uzman | Teşhis | Ana Aksiyon | Durum |
|-------|--------|-------------|-------|
| **Dunford** | Positioning bulanık (4 kategori) | Tek cümle positioning + üst şemsiye + 3 niche | ✅ Tamamlandı (2026-02-08) |
| **Lemkin** | SaaS metrikleri eksik, Auth geç, onboarding+expansion yok | Core metrics + 3 niche onboarding + expansion model | ✅ Büyük ölçüde tamamlandı (UI mockup hariç) |
| **Lenny** | User journey tanımsız, AI güven eğrisi yok | 3 niche First-Value Flow + AI trust flow | ✅ Büyük ölçüde tamamlandı (UI mockup hariç) |
| **Hormozi** | Offer yok, feature satıyorsun outcome değil | 3 ayrı Grand Slam Offer + risk reversal + sonuç dili | ✅ Tamamlandı (2026-02-08) |

---

## Toplam Aksiyon Listesi (Öncelik Sıralı)

### 🔴 CRITICAL (Satış öncesi zorunlu):

1. ✅ **TEK CÜMLE positioning'i tüm kanallarda sabitle** → Üst şemsiye + 3 niche positioning yazıldı (2026-02-08)
2. ✅ **Offer yapısı tanımla** → 3 ayrı Grand Slam Offer (Sellers / Dental / Clinics) + Hormozi kuralı (2026-02-08)
3. ✅ **İlk 48 saat onboarding flow'u detaylandır** → 3 niche için ayrı ayrı tanımlandı (2026-02-08)
4. ⬜ **Pricing model belirle** (seat/volume/outcome-based) — Expansion model var ama fiyat A/B testi yapılmadı

### 🟠 HIGH (Phase 1 öncesi):

5. ✅ **AI güven eğrisi tanımla** → Hafta 1-4 + Ay 2+ güven kademesi tanımlı
6. ⬜ **Human-in-the-loop flow detaylandır** — Temel kural var (override) ama UI akışı eksik
7. ✅ **Expansion revenue driver'ları belirle** → 6 driver tanımlı (seat, volume, credits, integration, outbound, storage)
8. ✅ **Timeline'ı tek kişi gerçekliğiyle revize et** → 12-14 ay gerçekçi tahmin eklendi

### 🟡 MEDIUM (Phase 2 öncesi):

9. ✅ **Web sitesi/deck'te feature → outcome dönüşümü** → 3 niche satış dili karşılaştırması + web/reklam stratejisi eklendi (2026-02-08)
10. ✅ **Risk reversal mekanizması ekle** → 3 niche için ayrı garanti tanımlandı (2026-02-08)
11. ⬜ **Product layer UI mockup'ları** (scenario → screen) — Henüz başlanmadı
12. ✅ Auth zamanlama uyarısı (Phase 3'te ≥3 kurumsal talep → hızlandır)

### ✅ TAMAMLANDI:

13. ✅ Tek cümle positioning yazıldı + üst şemsiye (3 niche) eklendi
14. ✅ Core SaaS Metrics tanımlandı (TTFAR, Deflection %, Retention, Activation, Churn)
15. ✅ User First-Value Flow eklendi — 3 niche için ayrı ayrı (e-ticaret Day 1-7-30, diş Day 1-7-30, estetik Day 1-7-30)
16. ✅ Aha moment sabitlendi — 3 niche için ayrı:
    - E-ticaret: "Gerçekten otomatik cevapladı!"
    - Diş: "Sekreter yerine AI cevapladı!"
    - Estetik: "Lead 5 dk içinde cevap aldı!"
17. ✅ 3 Niche paralel giriş kararı alındı + tüm dosyalar güncellendi (2026-02-08)
18. ✅ 3 ayrı Grand Slam Offer tasarlandı (Sellers / Dental / Clinics)
19. ✅ 3 niche Phase 0-2 paralel validasyon + satış adımları eklendi
20. ✅ 75 senaryo ortak capability analizi tamamlandı (C1/C2/C3/C8 = %95+ ortak)

---

## Son Not

**Bu roadmap mühendis için mükemmel, satış için yarım.**

Şu an:
- Backend mimari → Net ✅
- Senaryo mapping → Net ✅
- Phase planlama → Net ✅

Eksik:
- Müşteri "Bunu almalı mıyım?" sorusunun cevabı ❌
- "İlk haftada ne olacak?" akışı ❌
- "Neden Invekto, neden bugün?" offer'ı ❌

**Aksiyon:** Hormozi'nin offer framework'ünü ekle → Sonra satışa çık.

---

## GÜNCELLEME (2026-02-08) — Yukarıdaki Eksikler Büyük Ölçüde Kapatıldı

Şu an:
- Backend mimari → Net ✅
- Senaryo mapping → Net ✅
- Phase planlama → Net ✅
- **Positioning → Net ✅** (üst şemsiye + 3 niche-özel cümle)
- **Offer → Net ✅** (3 Grand Slam Offer + Hormozi kuralı)
- **Onboarding → Net ✅** (3 niche için ayrı first-value flow + 48 saat akışı)
- **AI güven eğrisi → Net ✅** (haftalık kademe + yanlış cevap protokolü)
- **Expansion model → Net ✅** (6 revenue driver)
- **3 niche paralel → Net ✅** (Phase 0'dan itibaren)

Hâlâ eksik:
- **UI mockup'ları** (product layer → screen tasarımı) ⬜
- **Pricing A/B testi** (3 farklı fiyat noktası validasyonu) ⬜
- **Human-in-the-loop UI akışı** (temel kural var, UI yok) ⬜
- **KVKK sağlık verisi rıza mekanizması detayı** ⬜

**Sonraki adım:** Phase 0'a başla — 3 niche'te 10'ar müşteri görüşmesi yap.
